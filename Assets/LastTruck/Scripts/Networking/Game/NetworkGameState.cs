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

        #endregion

        #region 네트워크 상태

        public enum GameOverReason
        {
            None = 0,
            AllPlayersDead = 1,
            TruckDestroyed = 2,
        }

        [Networked] public NetworkBool Started { get; set; }
        [Networked] public int Phase { get; set; }
        [Networked] public int Cycle { get; set; }
        [Networked] public float DayTimeRemaining { get; set; }
        [Networked] public int NightQuota { get; set; }
        [Networked] public int NightKilled { get; set; }
        [Networked, OnChangedRender(nameof(OnGameOverRender))] public int GameOver { get; set; }
        [Networked] public float TruckDurability { get; set; }
        [Networked] private TickTimer LoadTimeout { get; set; }

        #endregion

        #region 정적 조회

        public static NetworkGameState Instance { get; private set; }

        /// <summary>게임 오버가 됐는가 (피해/이동을 멈춘다).</summary>
        public static bool IsGameOver => Instance != null && Instance.Object != null && Instance.Object.IsValid && Instance.GameOver != 0;

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
                return Instance.Started && Instance.GameOver == 0;
            }
        }

        #endregion

        #region 생명주기

        private bool _loadingHidden;
        private bool _gameOverShown;
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
                LoadTimeout = TickTimer.CreateFromSeconds(Runner, loadWaitTimeout);
            }
            else
            {
                GameManager gm = GameManager.Instance;
                if (gm != null) gm.SetNetworkMirror();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
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

            if (GameOver == 0)
            {
                GameOverReason reason = CheckGameOver();
                if (reason != GameOverReason.None) GameOver = (int)reason;
            }
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
            // 트럭 파괴
            if (Truck != null && Truck.IsBroken) return GameOverReason.TruckDestroyed;

            // 모든 플레이어 사망 (사망 후 사라진 캐릭터도 사망으로 본다, 나간 사람은 제외)
            int activePlayers = 0;
            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                activePlayers++;
                NetworkPlayer character = NetworkPlayer.Find(player);
                if (character != null && character.IsAlive) return GameOverReason.None;
            }
            return activePlayers > 0 ? GameOverReason.AllPlayersDead : GameOverReason.None;
        }

        #endregion

        #region 모든 컴퓨터: 화면 반영

        public override void Render()
        {
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
            }
        }

        private void OnGameOverRender()
        {
            if (GameOver == 0 || _gameOverShown) return;
            _gameOverShown = true;

            string message = (GameOverReason)GameOver == GameOverReason.TruckDestroyed
                ? "트럭이 파괴되었습니다."
                : "모든 플레이어가 사망했습니다.";

            GameLauncher launcher = GameLauncher.Instance;
            if (launcher != null) launcher.NotifyGameOver();

            SystemUI.Alert("게임 오버", message, () =>
            {
                GameLauncher current = GameLauncher.Instance;
                if (current != null) current.LeaveAfterGameOver();
            }, "방 목록으로");
        }

        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
    }
}
