using System.Linq;
using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 게임 진행 상태를 모두에게 맞춰 주는 오브젝트 (호스트가 게임 씬에서 하나 스폰).
    ///
    ///  1. 준비 대기: 모든 참가자가 게임 씬 로딩을 마칠 때까지(최대 loadWaitTimeout초) 낮 타이머를 시작하지 않는다.
    ///     그동안 화면에는 "다른 플레이어를 기다리는 중..." 로딩이 뜨고 이동이 막힌다.
    ///  2. 낮/밤: 호스트의 GameManager가 진행하고, 그 값(페이즈/회차/남은 시간/쿼터/처치 수)을 매 틱 복사해서 보낸다.
    ///     클라이언트의 GameManager는 그 값을 받아 같은 이벤트를 발생시킨다 (시계 UI, 조명이 그대로 동작).
    ///  3. 인원 보정: 접속 인원을 GameManager.PlayerCount에 넣어 밤 몬스터 수를 늘린다.
    ///  4. 게임 오버: 모든 플레이어 사망 또는 트럭 파괴 → 모두에게 팝업 → 확인하면 방 목록으로.
    /// </summary>
    public class NetworkGameState : NetworkBehaviour
    {
        #region 인스펙터

        [Tooltip("참가자 로딩을 기다리는 최대 시간(초). 넘으면 온 사람끼리 시작한다.")]
        [SerializeField] private float loadWaitTimeout = 30f;

        [Tooltip("관심 영역(AOI) 반경(m). 이 거리 안의 몬스터만 각 플레이어에게 동기화한다. 0이면 끈다.")]
        [SerializeField] private float interestRadius = 60f;

        #endregion

        #region 네트워크 상태

        public enum GameOverReason
        {
            None = 0,
            AllPlayersDead = 1,
            TruckDestroyed = 2,
            Escaped = 3,   // 탈출 성공 (엔딩)
        }

        [Networked] public NetworkBool Started { get; set; }
        [Networked] public int Phase { get; set; }
        [Networked] public int Cycle { get; set; }
        [Networked] public float DayTimeRemaining { get; set; }
        [Networked] public int NightQuota { get; set; }
        [Networked] public int NightKilled { get; set; }
        [Networked, OnChangedRender(nameof(OnGameOverRender))] public int GameOver { get; set; }
        [Networked] public float TruckDurability { get; set; }

        // 생존 조건 진행 (SurvivalDirector, 호스트가 계산해서 모두에게 보여 준다. 조건 종류는 회차로 정해지므로 보내지 않는다)
        [Networked] public int SurvState { get; set; }
        [Networked] public float SurvElapsed { get; set; }
        [Networked] public int SurvCount { get; set; }
        [Networked] public float SurvDamage { get; set; }
        [Networked] public float SurvNext { get; set; }
        [Networked] public int SurvBoss { get; set; }
        [Networked] private TickTimer LoadTimeout { get; set; }
        [Networked] public NetworkString<_32> HostName { get; set; }
        /// <summary>방장 변경 직후, 다른 사람들이 캐릭터를 되찾을 때까지 게임 전체를 멈춘다.</summary>
        [Networked] public NetworkBool Held { get; set; }

        #endregion

        #region 정적 조회

        public static NetworkGameState Instance { get; private set; }

        /// <summary>게임 오버가 됐는가 (피해/이동을 멈춘다).</summary>
        public static bool IsGameOver => Instance != null && Instance.Object != null && Instance.Object.IsValid && Instance.GameOver != 0;

        /// <summary>게임 전체 일시정지 중인가 (방장 변경 직후 재연결 대기). 몬스터/피해/시간이 멈춘다.</summary>
        public static bool IsHeld
        {
            get
            {
                if (NetworkPause.IsPaused) return true;
                return Instance != null && Instance.Object != null && Instance.Object.IsValid && Instance.Held;
            }
        }

        /// <summary>
        /// 플레이어가 움직여도 되는가. 게임 상태 오브젝트가 아직 없으면(또는 설정이 없어 스폰되지 않으면) 막지 않는다.
        /// </summary>
        public static bool AllowPlayerControl
        {
            get
            {
                if (Instance == null || Instance.Object == null || !Instance.Object.IsValid)
                {
                    // 멀티플레이인데 아직 게임 상태를 못 받았으면(로딩 대기 중) 막는다. 설정이 없어 아예 안 오는 경우는 막지 않는다.
                    NetworkPrefabRegistry registry = NetworkPrefabRegistry.Instance;
                    bool expected = GameLauncher.IsOnlineSession && registry != null && registry.gameStatePrefab != null;
                    return !expected;
                }
                return Instance.Started && Instance.GameOver == 0 && !IsHeld;
            }
        }

        #endregion

        #region 생명주기

        private bool _loadingHidden;
        private bool _gameOverShown;
        private bool _holdShown;
        private LastTruck.TruckInformation _truck;

        private LastTruck.TruckInformation Truck
        {
            get
            {
                if (_truck == null) _truck = FindFirstObjectByType<LastTruck.TruckInformation>();
                return _truck;
            }
        }

        public override void Spawned()
        {
            Instance = this;
            InGameHud.Ensure(); // ESC 메뉴, 동료 이름표/HP바, 미니맵 동료 마커

            if (HasStateAuthority)
            {
                HostName = PlayerProfile.Nickname ?? string.Empty;
                if (Object.IsResume)
                {
                    // 방장 변경: 이 컴퓨터가 새 방장이 됐다 → GameManager가 직접 낮/밤을 진행한다 (값은 그대로 이어받음).
                    GameManager gm = GameManager.Instance;
                    if (gm != null) gm.TakeOverAsHost();
                }
                else
                {
                    LoadTimeout = TickTimer.CreateFromSeconds(Runner, loadWaitTimeout);
                }
            }
            else
            {
                GameManager gm = GameManager.Instance;
                if (gm != null) gm.SetNetworkMirror();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_holdShown) NetworkPause.End(NetworkPause.HoldReason);
            _holdShown = false;
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        #region 호스트: 진행

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            HostUpdateInterest();
            Held = HostResolveReclaiming();
            if (!Held) HostFlushPendingAnnouncement();

            GameManager gm = GameManager.Instance;
            if (gm != null) gm.PlayerCount = Mathf.Max(1, Runner.ActivePlayers.Count());

            if (!Started)
            {
                if (AllPlayersLoaded() || LoadTimeout.Expired(Runner))
                {
                    Started = true;
                    if (gm != null) gm.BeginNetworkGame();
                }
                return;
            }

            if (Truck != null) TruckDurability = Truck.CurrentDurability;

            if (gm != null)
            {
                Phase = (int)gm.CurrentPhase;
                Cycle = gm.CurrentCycle;
                DayTimeRemaining = gm.DayTimeRemaining;
                NightQuota = gm.NightMonsterQuota;
                NightKilled = gm.NightMonstersKilled;
            }

            LastTruck.SurvivalDirector survival = LastTruck.SurvivalDirector.Instance;
            if (survival != null)
            {
                SurvState = (int)survival.State;
                SurvElapsed = survival.Elapsed;
                SurvCount = survival.Count;
                SurvDamage = survival.Damage;
                SurvNext = survival.NextIn;
                SurvBoss = survival.BossState;
            }

            if (GameOver == 0)
            {
                GameOverReason reason = CheckGameOver();
                if (reason != GameOverReason.None) GameOver = (int)reason;
            }
        }

        /// <summary>
        /// 방장 변경 뒤 주인을 기다리는 캐릭터 처리. 멈춰야 하면 true.
        ///  - 지금 방에 있는 사람이 모두 자기 캐릭터를 받았으면, 남은 주인 없는 캐릭터(나간 방장 등)는 바로 "자리 비움"(3분 재접속 대기)으로 → 바로 이어서 진행.
        ///  - 아직 캐릭터를 못 받은 사람이 있으면 멈춘 채 기다린다 (각 캐릭터의 되찾기 대기 시간이 지나면 자동으로 자리 비움).
        /// </summary>
        private bool HostResolveReclaiming()
        {
            bool anyUnclaimed = false;
            foreach (NetworkPlayer character in NetworkPlayer.AllIncludingDeparted)
            {
                if (character != null && character.IsUnclaimed) { anyUnclaimed = true; break; }
            }
            if (!anyUnclaimed) return false;

            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                if (NetworkPlayer.Find(player) == null) return true; // 아직 자기 캐릭터를 못 받은 사람이 있다
            }

            foreach (NetworkPlayer character in NetworkPlayer.AllIncludingDeparted)
            {
                if (character != null && character.IsUnclaimed) character.HostMarkDeparted(GameLauncher.ReconnectWindowSeconds);
            }
            return false;
        }

        private bool AllPlayersLoaded()
        {
            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                if (!NetworkPlayer.HasLoaded(player)) return false;
            }
            return true;
        }

        private GameOverReason CheckGameOver()
        {
            // 탈출 성공 (게이트가 열렸고 부품 3종을 실은 트럭이 게이트에 도착)
            if (LastTruck.StoryDirector.EscapeConditionMet()) return GameOverReason.Escaped;

            // 트럭 파괴
            if (Truck != null && Truck.IsBroken) return GameOverReason.TruckDestroyed;

            // 모든 플레이어 기절 (나간 사람은 제외)
            int activePlayers = 0;
            NetworkPlayer last = null;
            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                activePlayers++;
                NetworkPlayer character = NetworkPlayer.Find(player);
                // 캐릭터가 아직 없으면(로딩/방장 변경 후 되찾는 중) 판정을 미룬다.
                if (character == null || character.IsAlive) { _soloDownAt = -1f; return GameOverReason.None; }
                last = character;
            }

            // 혼자 플레이: 살려 줄 동료가 없으니 쓰러져도 바로 끝내지 않는다.
            // 잠시 뒤 트럭 옆에서 다시 일어나고, 대가로 트럭 내구도가 깎인다 (트럭이 부서지면 그때 게임 오버).
            if (activePlayers == 1 && last != null && HostSoloRecover(last)) return GameOverReason.None;

            return activePlayers > 0 ? GameOverReason.AllPlayersDead : GameOverReason.None;
        }

        private const float SoloRecoverSeconds = 8f;
        private const float SoloRecoverTruckCost = 0.15f;   // 다시 일어날 때마다 트럭 최대 내구도의 15%
        private float _soloDownAt = -1f;

        /// <summary>혼자 플레이 중 쓰러진 캐릭터를 잠시 뒤 트럭 옆에서 일으킨다. 아직 기다리는 중이거나 일으켰으면 true (게임 오버 아님).</summary>
        private bool HostSoloRecover(NetworkPlayer character)
        {
            LastTruck.TruckInformation truck = Truck;
            if (truck == null || truck.IsBroken) return false;

            if (_soloDownAt < 0f)
            {
                _soloDownAt = Time.time;
                HostAnnounce("쓰러졌다!", Mathf.RoundToInt(SoloRecoverSeconds) + "초 뒤 트럭에서 다시 일어납니다 (트럭 내구도 -" + Mathf.RoundToInt(SoloRecoverTruckCost * 100f) + "%)");
                return true;
            }
            if (Time.time - _soloDownAt < SoloRecoverSeconds) return true;

            _soloDownAt = -1f;
            NetworkHealth health = character.GetComponent<NetworkHealth>();
            if (health == null) return false;
            health.Revive(0.5f);
            health.GrantInvincibility(3f);

            // 트럭 옆(트럭 뒤쪽 3m)으로 옮긴다
            Vector3 spot = truck.transform.position - truck.transform.forward * 5f + Vector3.up * 1f;
            if (Physics.Raycast(spot + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                spot = hit.point + Vector3.up * 0.3f;
            var controller = character.GetComponent<NetworkCharacterController>();
            if (controller != null) controller.Teleport(spot, character.transform.rotation);

            truck.SetDurability(Mathf.Max(1f, truck.CurrentDurability - truck.MaxDurability * SoloRecoverTruckCost));
            HostSystemMessage("트럭 곁에서 겨우 정신을 차렸다. (트럭 내구도 -" + Mathf.RoundToInt(SoloRecoverTruckCost * 100f) + "%)");
            return true;
        }

        /// <summary>
        /// 관심 영역: 프로젝트 설정에서 AOI가 켜져 있을 때만, 매 틱 각 플레이어 위치(트럭에 탔으면 트럭 위치)를 등록한다.
        /// AOI 대상은 몬스터뿐이고 나머지 오브젝트는 전역(Global)이라 항상 보인다.
        /// </summary>
        private void HostUpdateInterest()
        {
            if (interestRadius <= 0f || !NetworkInterest.Enabled(Runner)) return;

            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                NetworkPlayer character = NetworkPlayer.Find(player);
                if (character == null) continue;
                Vector3 position = character.transform.position;
                if (character.IsSeated && NetworkTruck.TruckTransform != null) position = NetworkTruck.TruckTransform.position;
                Runner.AddPlayerAreaOfInterest(player, position, interestRadius);
            }
        }

        #endregion

        #region 모든 컴퓨터: 화면 반영

        public override void Render()
        {
            if (!HasStateAuthority && GameLauncher.Instance != null) GameLauncher.Instance.SetCachedHostNickname(HostName.ToString());

            // 클라이언트도 인원 수를 알아야 생존 조건 목표(혼자면 줄어드는 처치 수/보스 수)를 호스트와 같게 보여 준다
            if (!HasStateAuthority && GameManager.Instance != null)
                GameManager.Instance.PlayerCount = Mathf.Max(1, NetworkPlayer.All.Count);

            // 게임 전체 멈춤 (모든 컴퓨터 같은 화면)
            if (Held && !_holdShown)
            {
                _holdShown = true;
                NetworkPause.Begin(NetworkPause.HoldReason, "새 방장에게 다시 연결하는 중입니다.\n모든 플레이어가 연결되면 바로 이어집니다.");
            }
            else if (!Held && _holdShown)
            {
                _holdShown = false;
                NetworkPause.End(NetworkPause.HoldReason);
            }

            // 준비 대기 로딩 화면 닫기
            if (Started && !_loadingHidden)
            {
                _loadingHidden = true;
                SystemUI.HideLoading();
            }

            // 클라이언트: GameManager에 호스트 값 반영
            if (!HasStateAuthority)
            {
                GameManager gm = GameManager.Instance;
                if (gm != null)
                {
                    if (!gm.IsNetworkMirror) gm.SetNetworkMirror();
                    gm.ApplyNetworkState((GameManager.GamePhase)Phase, Cycle, DayTimeRemaining, NightQuota, NightKilled, Started);
                }

                // 트럭 내구도 (몬스터가 호스트에서 깎은 값)
                if (Started && Truck != null) Truck.ApplyNetworkDurability(TruckDurability);

                // 생존 조건 진행 (호스트 값)
                LastTruck.SurvivalDirector survival = LastTruck.SurvivalDirector.Instance;
                if (Started && survival != null) survival.ApplyNetworkState(SurvState, SurvElapsed, SurvCount, SurvDamage, SurvNext, SurvBoss);
            }
        }

        private void OnGameOverRender()
        {
            if (GameOver == 0 || _gameOverShown) return;
            _gameOverShown = true;

            GameLauncher launcher = GameLauncher.Instance;
            if (launcher != null) launcher.NotifyGameOver();

            // 탈출 성공은 패배가 아니라 엔딩 화면
            if ((GameOverReason)GameOver == GameOverReason.Escaped)
            {
                LastTruck.StoryDirector.ShowEnding(false);
                return;
            }

            string message = (GameOverReason)GameOver == GameOverReason.TruckDestroyed
                ? "트럭이 파괴되었습니다."
                : "모든 플레이어가 기절했습니다.";

            SystemUI.Alert("게임 오버", message, () =>
            {
                GameLauncher current = GameLauncher.Instance;
                if (current != null) current.LeaveAfterGameOver();
            }, "방 목록으로");
        }

        #endregion

        #region 채팅 / 시스템 메시지 / 배너

        public const int ChatMaxLength = 80;

        /// <summary>내 채팅을 보낸다 (호스트가 확인 후 모두에게 뿌린다).</summary>
        public static void SendChat(string text)
        {
            text = ChatHud.Sanitize(text, ChatMaxLength);
            if (string.IsNullOrEmpty(text)) return;
            if (Instance == null || Instance.Object == null || !Instance.Object.IsValid)
            {
                ChatHud.AddLocal(PlayerProfile.Nickname, text, ChatKind.Player); // 혼자(오프라인)
                return;
            }
            Instance.RPC_SendChat(text);
        }

        /// <summary>호스트: 모두에게 시스템 메시지 (채팅창 노란 글자).</summary>
        public static void HostSystemMessage(string text, ChatKind kind = ChatKind.System)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (Instance == null || Instance.Object == null || !Instance.Object.IsValid || !Instance.HasStateAuthority)
            {
                if (!GameLauncher.IsOnlineSession) ChatHud.AddLocal(string.Empty, text, kind);
                return;
            }
            Instance.RPC_Chat(string.Empty, text, (int)kind);
        }

        /// <summary>호스트: 모두에게 화면 가운데 배너 + 채팅 경고.</summary>
        public static void HostAnnounce(string title, string description)
        {
            if (Instance == null || Instance.Object == null || !Instance.Object.IsValid || !Instance.HasStateAuthority)
            {
                if (!GameLauncher.IsOnlineSession)
                {
                    WaveBanner.Show(title, description);
                    ChatHud.AddLocal(string.Empty, title, ChatKind.Warning);
                }
                return;
            }
            Instance.RPC_Banner(title, description ?? string.Empty);
            Instance.RPC_Chat(string.Empty, title, (int)ChatKind.Warning);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SendChat(string text, RpcInfo info = default)
        {
            if (!RpcGuard.Allow(Runner, info.Source, nameof(RPC_SendChat), 3)) return;
            text = ChatHud.Sanitize(text, ChatMaxLength);
            if (string.IsNullOrEmpty(text)) return;

            // "/"로 시작하면 테스트 명령 (호스트가 실행하고 결과를 모두에게 알린다)
            if (text.StartsWith("/"))
            {
                string reply = DebugCommands.HostExecute(text, SenderName(info.Source));
                if (!string.IsNullOrEmpty(reply)) RPC_Chat(string.Empty, reply, (int)ChatKind.System);
                return;
            }

            string sender;
            if (info.Source == PlayerRef.None || info.Source == Runner.LocalPlayer)
            {
                sender = PlayerProfile.Nickname;
            }
            else
            {
                NetworkPlayer character = NetworkPlayer.Find(info.Source);
                sender = character != null ? character.Nickname.ToString() : $"플레이어{info.Source.PlayerId}";
            }
            RPC_Chat(ChatHud.Sanitize(sender, 32), text, (int)ChatKind.Player);
        }

        private string SenderName(PlayerRef source)
        {
            if (source == PlayerRef.None || source == Runner.LocalPlayer) return PlayerProfile.Nickname;
            NetworkPlayer character = NetworkPlayer.Find(source);
            return character != null ? character.Nickname.ToString() : $"플레이어{source.PlayerId}";
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Chat(string sender, string text, int kind)
        {
            ChatHud.AddLocal(sender, text, (ChatKind)kind);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Banner(string title, string description)
        {
            WaveBanner.Show(title, description);
        }

        /// <summary>호스트: 방장 변경 등으로 미뤄 둔 알림을, 모두가 다시 연결된 뒤(멈춤이 풀린 뒤) 보낸다.</summary>
        private void HostFlushPendingAnnouncement()
        {
            GameLauncher launcher = GameLauncher.Instance;
            if (launcher == null) return;
            string message = launcher.TakePendingAnnouncement();
            if (!string.IsNullOrEmpty(message)) HostSystemMessage(message);
        }

        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
    }
}
