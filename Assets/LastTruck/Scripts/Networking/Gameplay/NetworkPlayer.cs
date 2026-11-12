using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 멀티플레이 캐릭터의 "신분증". 누가 조종하는 캐릭터인지, 닉네임/캐릭터 번호는 무엇인지 들고 있고,
    /// 스폰되는 순간 "내 캐릭터"와 "다른 사람 캐릭터"를 다르게 준비한다.
    ///
    /// 내 캐릭터 (Input Authority = 나)
    ///  - 카메라가 따라가게 하고, HP바 / 인벤토리 UI / 미니맵 / 채집 진행 UI / 클릭 상호작용을 이 캐릭터에 연결한다.
    ///  - 호스트에게 "씬 로딩 끝, 준비됨"을 알린다 (모두 준비되면 낮 타이머가 시작된다).
    ///  - 체력이 0이 되면 기절한다: 조작 스크립트(무기/상호작용 등)를 끄고, 동료가 옆에서 E를 reviveSeconds초 누르면
    ///    체력 reviveHealthFraction으로 다시 일어난다 (끈 스크립트도 다시 켠다). 전원 기절이면 게임 오버.
    ///
    /// 다른 사람 캐릭터 (내 화면에 보이는 복제본)
    ///  - 키보드/마우스를 직접 읽는 컴포넌트(상호작용, 인벤토리 단축키, 무기, 근접 공격)를 끈다.
    ///    그렇지 않으면 내가 E를 누를 때 다른 사람 캐릭터도 같이 상호작용해 버린다.
    ///
    /// 프리팹은 에디터 메뉴 "LastTruck > Multiplayer > 1. 네트워크 프리팹 생성"이 자동으로 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkPlayer : NetworkBehaviour, IInputAuthorityGained, IInputAuthorityLost
    {
        #region 정적 목록 / 이벤트

        private static readonly List<NetworkPlayer> AllPlayers = new List<NetworkPlayer>();
        private static readonly List<NetworkPlayer> PresentPlayers = new List<NetworkPlayer>();
        private static readonly HashSet<PlayerRef> LoadedPlayers = new HashSet<PlayerRef>();

        /// <summary>
        /// 지금 게임에 "있는" 플레이어 캐릭터 (나가서 재접속을 기다리는 캐릭터는 빠진다).
        /// 몬스터 타깃, 이름표, 미니맵, 스폰 기준점 등은 모두 이 목록을 쓴다.
        /// </summary>
        public static IReadOnlyList<NetworkPlayer> All
        {
            get
            {
                PresentPlayers.Clear();
                foreach (NetworkPlayer p in AllPlayers)
                {
                    if (p != null && p.IsPresent) PresentPlayers.Add(p);
                }
                return PresentPlayers;
            }
        }

        /// <summary>나간 사람(재접속 대기)까지 포함한 모든 캐릭터 (호스트의 재접속/호스트 이전 처리용).</summary>
        public static IReadOnlyList<NetworkPlayer> AllIncludingDeparted => AllPlayers;

        /// <summary>내 캐릭터 (없으면 null - 아직 스폰 전이거나 사망 후).</summary>
        public static NetworkPlayer Local { get; private set; }

        public static event Action<NetworkPlayer> LocalPlayerSpawned;
        public static event Action<NetworkPlayer> LocalPlayerDespawned;
        public static event Action<NetworkPlayer> LocalPlayerDied;
        public static event Action<NetworkPlayer> LocalPlayerRevived;

        /// <summary>호스트 전용: 게임 씬 로딩을 마쳤다고 알려온 플레이어들.</summary>
        public static bool HasLoaded(PlayerRef player) => LoadedPlayers.Contains(player);

        /// <summary>새 게임을 시작할 때 호스트가 비운다.</summary>
        public static void ClearLoadedPlayers() => LoadedPlayers.Clear();

        #endregion

        #region 네트워크 상태 / 인스펙터

        [Networked] public NetworkString<_32> Nickname { get; set; }
        [Networked] public int CharacterIndex { get; set; }

        [Tooltip("다른 사람 캐릭터(복제본)와 사망한 내 캐릭터에서 끌 컴포넌트. 비워 두면 기본 목록(상호작용/인벤토리/무기/근접 공격 등)을 자동으로 찾는다.")]
        [SerializeField] private UnityEngine.Behaviour[] localOnlyBehaviours = new UnityEngine.Behaviour[0];

        /// <summary>키보드/마우스를 직접 읽어서 복제본에서는 꺼야 하는 기본 컴포넌트 이름들.</summary>
        private static readonly HashSet<string> DefaultLocalOnlyTypes = new HashSet<string>
        {
            "PlayerInteract",
            "PlayerInventory",
            "WeaponController",
            "PlayerAttack",
            "EatController",
            "TrapController",
            "PouchController",
        };

        [Header("기절 / 부활")]
        [Tooltip("동료가 옆에서 E를 이 시간(초) 동안 누르면 부활한다.")]
        [SerializeField] private float reviveSeconds = 5f;
        [Tooltip("부활할 때 체력 (최대 체력 대비 비율).")]
        [Range(0.05f, 1f)]
        [SerializeField] private float reviveHealthFraction = 0.3f;
        [Tooltip("부활시키는 사람이 이 거리(m)보다 멀어지면 처음부터 다시.")]
        [SerializeField] private float reviveRange = 2.5f;
        [Tooltip("내가 기절해 있는 동안 화면을 흑백으로 바꾼다.")]
        [SerializeField] private bool grayscaleWhenDowned = true;

        /// <summary>지금 이 캐릭터를 살리고 있는 사람 (없으면 None).</summary>
        [Networked] public PlayerRef Reviver { get; private set; }
        [Networked] private TickTimer ReviveTimer { get; set; }

        private NetworkHealth _health;
        private readonly List<UnityEngine.Behaviour> _downDisabledBehaviours = new List<UnityEngine.Behaviour>();

        public float ReviveSeconds => reviveSeconds;
        public float ReviveRange => reviveRange;
        public bool GrayscaleWhenDowned => grayscaleWhenDowned;

        /// <summary>기절해 있는가.</summary>
        public bool IsDowned => Object != null && Object.IsValid && _health != null && _health.IsDead;

        /// <summary>누군가 살리는 중이면 0~1 진행도, 아니면 0.</summary>
        public float ReviveProgress01
        {
            get
            {
                if (!IsDowned || Reviver == PlayerRef.None || Runner == null) return 0f;
                float? left = ReviveTimer.RemainingTime(Runner);
                if (!left.HasValue) return 1f;
                return 1f - Mathf.Clamp01(left.Value / Mathf.Max(0.01f, reviveSeconds));
            }
        }

        public PlayerRef Owner => Object != null ? Object.InputAuthority : PlayerRef.None;
        public bool IsLocal => Object != null && Object.HasInputAuthority;
        public NetworkHealth Health => _health;

        /// <summary>살아 있는가 (체력 컴포넌트가 없으면 항상 살아 있는 것으로 본다).</summary>
        public bool IsAlive => Object != null && Object.IsValid && (_health == null || !_health.IsDead);

        #endregion

        #region 생명주기

        private void Awake()
        {
            _health = GetComponent<NetworkHealth>();
        }

        /// <summary>호스트가 Runner.Spawn의 onBeforeSpawned 안에서 부른다 (스폰과 동시에 값이 모두에게 전달됨).</summary>
        public void InitializeBeforeSpawn(string nickname, int characterIndex, string deviceId = null)
        {
            Nickname = PlayerProfile.ClampForNetwork(nickname);
            CharacterIndex = characterIndex;
            DeviceId = ClampDeviceId(deviceId);
        }

        public static string ClampDeviceId(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return string.Empty;
            return deviceId.Length > 60 ? deviceId.Substring(0, 60) : deviceId;
        }

        public override void Spawned()
        {
            if (!AllPlayers.Contains(this)) AllPlayers.Add(this);
            gameObject.name = $"Player_{Nickname}{(Object.HasInputAuthority ? " (나)" : string.Empty)}";

            if (_health != null)
            {
                _health.Died += OnDied;
                _health.Revived += OnRevived;
            }

            if (GetComponent<PlayerReviver>() == null) gameObject.AddComponent<PlayerReviver>();

            if (Object.HasInputAuthority)
            {
                BecomeLocal();
            }
            else
            {
                DisableLocalOnlyBehaviours(keepWeaponView: true);
            }

            _departedApplied = false;
            if (Departed) ApplyDeparted(true);
        }

        /// <summary>이 캐릭터가 "내 캐릭터"가 됐다 (처음 스폰 / 재접속 / 방장 변경 후 다시 붙음).</summary>
        private void BecomeLocal()
        {
            if (Local == this) return;
            Local = this;
            _becameLocalFrame = Time.frameCount;
            try
            {
                LocalPlayerBinder.BindLocalPlayer(this);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            SubscribeBag();
            LocalPlayerSpawned?.Invoke(this);

            // 내 캐릭터가 보인다 = 게임 씬 로딩이 끝났다 → 호스트에게 알린다.
            RPC_ReportLoaded();
        }

        /// <summary>재접속 / 방장 변경으로 이 캐릭터의 조종 권한을 받았다.</summary>
        void IInputAuthorityGained.InputAuthorityGained() => OnLocalAuthorityGained();

        private void OnLocalAuthorityGained()
        {
            // 다른 사람 캐릭터로 보이던 동안 꺼 둔 입력 스크립트를 다시 켠다 (기절해 있으면 OnDied 규칙대로 다시 끈다).
            foreach (UnityEngine.Behaviour behaviour in GetLocalOnlyBehaviours())
            {
                if (behaviour != null) behaviour.enabled = true;
            }
            GetComponent<NetworkWeaponVisuals>()?.RefreshAuthority();
            BecomeLocal();
            if (_health != null && _health.IsDead) OnDied(_health);
        }

        void IInputAuthorityLost.InputAuthorityLost() => OnLocalAuthorityLost();

        private void OnLocalAuthorityLost()
        {
            UnsubscribeBag();
            if (Local == this)
            {
                Local = null;
                LocalPlayerDespawned?.Invoke(this);
            }
            DisableLocalOnlyBehaviours(keepWeaponView: true);
            GetComponent<NetworkWeaponVisuals>()?.RefreshAuthority();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Unregister();
        }

        private void OnDestroy()
        {
            Unregister();
        }

        private void Unregister()
        {
            UnsubscribeBag();
            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.Revived -= OnRevived;
            }
            AllPlayers.Remove(this);
            if (Local == this)
            {
                Local = null;
                LocalPlayerDespawned?.Invoke(this);
            }
        }

        #endregion

        #region 로딩 완료 알림

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_ReportLoaded()
        {
            LoadedPlayers.Add(Object.InputAuthority);
        }

        #endregion

        #region 기절 / 부활

        private void OnDied(NetworkHealth health)
        {
            if (!IsLocal) return;

            // 조작 불가: 무기/상호작용 등 입력을 읽는 스크립트를 끈다 (이동은 NetworkPlayerMovement가 막는다).
            // (트럭에서 내려지며 이미 목록에 들어간 것은 그대로 둔다)
            foreach (UnityEngine.Behaviour behaviour in GetLocalOnlyBehaviours())
            {
                if (behaviour == null || !behaviour.enabled) continue;
                behaviour.enabled = false;
                if (!_downDisabledBehaviours.Contains(behaviour)) _downDisabledBehaviours.Add(behaviour);
            }
            LocalPlayerDied?.Invoke(this);
        }

        private void OnRevived(NetworkHealth health)
        {
            if (!IsLocal) return;

            // 기절할 때 끈 것만 다시 켠다. 트럭에 타 있으면 내릴 때 켜지도록 넘긴다.
            foreach (UnityEngine.Behaviour behaviour in _downDisabledBehaviours)
            {
                if (behaviour == null) continue;
                if (IsSeated)
                {
                    if (!_seatDisabledBehaviours.Contains(behaviour)) _seatDisabledBehaviours.Add(behaviour);
                }
                else
                {
                    behaviour.enabled = true;
                }
            }
            _downDisabledBehaviours.Clear();
            LocalPlayerRevived?.Invoke(this);
        }

        /// <summary>
        /// 다른 스크립트가 잠깐 끈 컴포넌트를 다시 켜려 할 때: 지금 기절/탑승 중이면 바로 켜지 않고
        /// 부활/하차할 때 같이 켜지도록 맡긴다 (예: 동료를 살리다가 내가 기절한 경우의 PlayerInteract).
        /// </summary>
        public void ReenableWhenAble(UnityEngine.Behaviour behaviour)
        {
            if (behaviour == null) return;
            if (!IsAlive)
            {
                if (!_downDisabledBehaviours.Contains(behaviour)) _downDisabledBehaviours.Add(behaviour);
            }
            else if (_seatedApplied)
            {
                if (!_seatDisabledBehaviours.Contains(behaviour)) _seatDisabledBehaviours.Add(behaviour);
            }
            else
            {
                behaviour.enabled = true;
            }
        }

        /// <summary>내 캐릭터가 이 (기절한) 동료를 살리기 시작/중단할 때 부른다 (PlayerReviver).</summary>
        public void RequestRevive(bool start)
        {
            if (Object == null || !Object.IsValid) return;
            if (start) RPC_BeginRevive();
            else RPC_CancelRevive();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_BeginRevive(RpcInfo info = default)
        {
            if (!RpcGuard.Allow(Runner, info.Source, nameof(RPC_BeginRevive), 15)) return;
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            if (!IsDowned || source == Owner) return;
            if (Reviver != PlayerRef.None && Reviver != source) return; // 이미 다른 사람이 살리는 중

            NetworkPlayer reviver = Find(source);
            if (reviver == null || !reviver.IsAlive || reviver.IsSeated) return;
            if (HorizontalDistance(reviver.transform.position, transform.position) > reviveRange + 1f) return;

            if (Reviver != source)
            {
                Reviver = source;
                ReviveTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.1f, reviveSeconds));
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_CancelRevive(RpcInfo info = default)
        {
            if (!RpcGuard.Allow(Runner, info.Source, nameof(RPC_CancelRevive), 15)) return;
            PlayerRef source = info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;
            if (Reviver == source) ClearReviver();
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            HostUpdatePresence();
            if (Object == null || !Object.IsValid || Reviver == PlayerRef.None) return;

            NetworkPlayer reviver = Find(Reviver);
            bool valid = IsDowned && reviver != null && reviver.IsAlive && !reviver.IsSeated &&
                         HorizontalDistance(reviver.transform.position, transform.position) <= reviveRange + 1f &&
                         !NetworkGameState.IsGameOver;
            if (!valid)
            {
                ClearReviver();
                return;
            }

            if (ReviveTimer.Expired(Runner))
            {
                ClearReviver();
                _health.Revive(reviveHealthFraction);
            }
        }

        private void ClearReviver()
        {
            Reviver = PlayerRef.None;
            ReviveTimer = TickTimer.None;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        #endregion

        #region 자리 비움 (나감 → 재접속 대기) / 방장 변경 후 주인 찾기

        /// <summary>이 캐릭터 주인의 기기 ID (재접속/방장 변경 때 같은 사람인지 알아보는 값).</summary>
        [Networked] public NetworkString<_64> DeviceId { get; set; }

        /// <summary>주인이 나가서 재접속을 기다리는 중 (모습/충돌을 숨기고 게임 판정에서 빠진다).</summary>
        [Networked] public NetworkBool Departed { get; private set; }
        [Networked] private TickTimer DepartTimer { get; set; }

        /// <summary>방장 변경 직후: 주인이 새 방에 다시 붙기를 기다리는 중 (시간 안에 안 오면 자리 비움으로).</summary>
        [Networked] private TickTimer ClaimTimer { get; set; }

        /// <summary>게임에 "있는" 캐릭터인가 (나가서 재접속을 기다리는 캐릭터는 false).</summary>
        public bool IsPresent => Object != null && Object.IsValid && !Departed;

        /// <summary>재접속 대기 중이고 아직 시간이 남았는가 (호스트가 재접속을 받아줄지 판단).</summary>
        public bool IsAwaitingReturn => Object != null && Object.IsValid && Departed && Runner != null && !DepartTimer.ExpiredOrNotRunning(Runner);

        /// <summary>방장 변경 직후 주인을 기다리는 중인가.</summary>
        public bool IsUnclaimed => Object != null && Object.IsValid && !Departed && Object.InputAuthority == PlayerRef.None;

        /// <summary>호스트: 주인이 나갔다. 좌석/부활을 정리하고 숨긴 채 seconds초 동안 재접속을 기다린다.</summary>
        public void HostMarkDeparted(float seconds)
        {
            if (!HasStateAuthority || Departed) return;

            PlayerRef owner = Object.InputAuthority;
            if (owner != PlayerRef.None && NetworkTruck.IsReady) NetworkTruck.Instance.HostForceExit(owner);
            ClearReviver();

            HostSetInputAuthority(PlayerRef.None);
            Departed = true;
            DepartTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(1f, seconds));
            ClaimTimer = TickTimer.None;
            Debug.Log($"[NetworkPlayer] {Nickname} 나감 → {seconds:0}초 동안 재접속 대기");
            NetworkGameState.HostSystemMessage($"{Nickname}님이 나갔습니다. {seconds / 60f:0}분 안에 돌아오면 이어서 할 수 있습니다.");
        }

        /// <summary>
        /// 호스트: 조종 권한을 바꾼다. Fusion은 InputAuthorityGained/Lost를 클라이언트(복제 받을 때)에서만 부르고
        /// 호스트 자신의 캐릭터에는 부르지 않는다 → 방장 변경 뒤 새 방장이 자기 캐릭터를 받을 때 직접 불러 준다.
        /// </summary>
        private void HostSetInputAuthority(PlayerRef player)
        {
            PlayerRef before = Object.InputAuthority;
            if (before == player) return;
            Object.AssignInputAuthority(player);

            PlayerRef local = Runner.LocalPlayer;
            if (local == PlayerRef.None) return;
            if (player == local) OnLocalAuthorityGained();
            else if (before == local) OnLocalAuthorityLost();
        }

        // ---------------- 채팅 입력 중 조작 막기 (내 캐릭터) ----------------

        private readonly List<UnityEngine.Behaviour> _chatDisabledBehaviours = new List<UnityEngine.Behaviour>();

        /// <summary>채팅 입력 중: 키를 직접 읽는 내 캐릭터 스크립트(상호작용/인벤토리/무기 등)를 잠시 끈다.</summary>
        public void SetChatSuspended(bool suspended)
        {
            if (suspended)
            {
                if (!IsLocal) return;
                foreach (UnityEngine.Behaviour behaviour in GetLocalOnlyBehaviours())
                {
                    if (behaviour == null || !behaviour.enabled) continue;
                    behaviour.enabled = false;
                    _chatDisabledBehaviours.Add(behaviour);
                }
                return;
            }

            // 그사이 기절했거나 트럭에 탔으면, 부활/하차할 때 켜지도록 그쪽 목록으로 넘긴다.
            foreach (UnityEngine.Behaviour behaviour in _chatDisabledBehaviours)
            {
                if (behaviour == null || !IsLocal) continue;
                if (!IsAlive)
                {
                    if (!_downDisabledBehaviours.Contains(behaviour)) _downDisabledBehaviours.Add(behaviour);
                }
                else if (IsSeated)
                {
                    if (!_seatDisabledBehaviours.Contains(behaviour)) _seatDisabledBehaviours.Add(behaviour);
                }
                else
                {
                    behaviour.enabled = true;
                }
            }
            _chatDisabledBehaviours.Clear();
        }

        /// <summary>호스트: 방장 변경 직후 이 캐릭터의 주인이 다시 붙기를 seconds초 기다린다.</summary>
        public void HostBeginClaimWait(float seconds)
        {
            if (!HasStateAuthority) return;
            HostSetInputAuthority(PlayerRef.None);
            ClearReviver();
            ClaimTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(1f, seconds));
        }

        /// <summary>호스트: 주인 없는 캐릭터인데 되찾기 대기 시간이 안 걸려 있으면 건다 (복원 직후 놓친 경우 대비).</summary>
        public void HostEnsureClaimWait(float seconds)
        {
            if (!HasStateAuthority || !IsUnclaimed || ClaimTimer.IsRunning) return;
            HostBeginClaimWait(seconds);
        }

        /// <summary>호스트: 같은 사람이 돌아왔다 → 조종 권한을 돌려주고 가방을 복구하게 한다.</summary>
        public void HostReattach(PlayerRef player)
        {
            if (!HasStateAuthority || player == PlayerRef.None) return;

            HostSetInputAuthority(player);
            Runner.SetPlayerObject(player, Object);
            Departed = false;
            DepartTimer = TickTimer.None;
            ClaimTimer = TickTimer.None;
            RestoreBag = true;
            LoadedPlayers.Add(player);
            Debug.Log($"[NetworkPlayer] {Nickname} 재접속 → 캐릭터를 다시 연결 ({player})");
        }

        /// <summary>호스트: 재접속 시간이 지났다 → 가방 아이템을 트럭에 넣고 캐릭터를 없앤다.</summary>
        private void HostExpireDeparted()
        {
            int moved = 0;
            CraftingSystem.TruckInventory truck = FindFirstObjectByType<CraftingSystem.TruckInventory>();
            if (truck != null)
            {
                for (int i = 0; i < BagCapacity; i++)
                {
                    CraftingSystem.ItemData item = ItemIndexLookup.ItemAt(BagItems[i] - 1);
                    int count = BagCounts[i];
                    if (item == null || count <= 0) continue;
                    moved += TruckInventorySync.AddAndCount(truck.Inventory, item, count, BagDurability[i]);
                }
            }
            Debug.Log($"[NetworkPlayer] {Nickname} 재접속 시간 초과 → 가방 아이템 {moved}개를 트럭으로 옮기고 캐릭터 제거");
            NetworkGameState.HostSystemMessage($"{Nickname}님이 돌아오지 않아 가방 아이템 {moved}개를 트럭으로 옮겼습니다.");
            Runner.Despawn(Object);
        }

        private void HostUpdatePresence()
        {
            if (Departed)
            {
                if (DepartTimer.Expired(Runner)) HostExpireDeparted();
                return;
            }
            if (ClaimTimer.IsRunning && Object.InputAuthority == PlayerRef.None && ClaimTimer.Expired(Runner))
            {
                HostMarkDeparted(GameLauncher.ReconnectWindowSeconds);
            }
        }

        // ---------------- 숨기기 (모든 컴퓨터) ----------------

        private bool _departedApplied;
        private readonly List<Renderer> _departHiddenRenderers = new List<Renderer>();
        private readonly List<Collider> _departDisabledColliders = new List<Collider>();

        private void ApplyDeparted(bool departed)
        {
            _departedApplied = departed;
            if (departed)
            {
                foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer != null && renderer.enabled)
                    {
                        renderer.enabled = false;
                        _departHiddenRenderers.Add(renderer);
                    }
                }
                foreach (Collider collider in GetComponentsInChildren<Collider>(true))
                {
                    if (collider != null && collider.enabled && !(collider is CharacterController))
                    {
                        collider.enabled = false;
                        _departDisabledColliders.Add(collider);
                    }
                }
                CharacterController cc = GetComponent<CharacterController>();
                if (cc != null) cc.detectCollisions = false;
                return;
            }

            foreach (Renderer renderer in _departHiddenRenderers)
            {
                if (renderer != null) renderer.enabled = true;
            }
            foreach (Collider collider in _departDisabledColliders)
            {
                if (collider != null) collider.enabled = true;
            }
            _departHiddenRenderers.Clear();
            _departDisabledColliders.Clear();
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null && !IsSeated) controller.detectCollisions = true;
        }

        #endregion

        #region 가방 동기화 (호스트가 보관 → 재접속/방장 변경 때 복구)

        /// <summary>가방(플레이어 칸 + 파우치 칸)을 호스트에 보관하는 칸 수.</summary>
        public const int BagCapacity = 24;

        [Networked, Capacity(BagCapacity)] private NetworkArray<int> BagItems => default;      // 아이템 번호 + 1 (0 = 빈 칸)
        [Networked, Capacity(BagCapacity)] private NetworkArray<int> BagCounts => default;
        [Networked, Capacity(BagCapacity)] private NetworkArray<int> BagDurability => default;
        [Networked] private int BagPlayerSlots { get; set; } // 앞에서부터 이만큼이 플레이어 칸, 나머지는 파우치

        /// <summary>재접속/방장 변경 뒤: 주인이 호스트에 보관된 가방으로 복구해야 한다.</summary>
        [Networked] public NetworkBool RestoreBag { get; private set; }

        /// <summary>
        /// 방장 변경 중 이 컴퓨터가 붙잡아 둔 내 가방 (호스트 보관본은 몇 초 늦을 수 있어서, 내 컴퓨터의 최신 상태를 우선한다).
        /// GameLauncher가 방장 변경 직전에 채운다.
        /// </summary>
        public static string PendingLocalBag;

        private CraftingSystem.PlayerInventory _bagInventory;
        private CraftingSystem.PouchInventory _bagPouch;
        private bool _bagDirty;
        private float _bagSendTime;
        private int _becameLocalFrame = -1;
        private const float BagSendInterval = 0.4f;

        private void SubscribeBag()
        {
            UnsubscribeBag();
            _bagInventory = GetComponent<CraftingSystem.PlayerInventory>();
            _bagPouch = GetComponent<CraftingSystem.PouchInventory>();
            if (_bagInventory != null) _bagInventory.Changed += MarkBagDirty;
            if (_bagPouch != null) _bagPouch.Changed += MarkBagDirty;
            _bagDirty = true;
        }

        private void UnsubscribeBag()
        {
            if (_bagInventory != null) _bagInventory.Changed -= MarkBagDirty;
            if (_bagPouch != null) _bagPouch.Changed -= MarkBagDirty;
            _bagInventory = null;
            _bagPouch = null;
        }

        private void MarkBagDirty() => _bagDirty = true;

        /// <summary>내 캐릭터: 복구가 필요하면 복구하고, 가방이 바뀌었으면 잠깐 모아서 호스트에 보낸다.</summary>
        private void UpdateBagSync()
        {
            if (!IsLocal || Object == null || !Object.IsValid) return;

            // 복구는 스폰/권한을 받은 다음 프레임 이후에 (PlayerInventory.Start의 기본 지급보다 나중에 덮어쓰도록)
            if (RestoreBag && Time.frameCount > _becameLocalFrame + 1)
            {
                string source = !string.IsNullOrEmpty(PendingLocalBag) ? PendingLocalBag : EncodeNetworkBag();
                PendingLocalBag = null;
                ApplyBag(source);
                RPC_BagRestored();
                _bagDirty = true;
                return;
            }

            if (_bagDirty && Time.unscaledTime - _bagSendTime >= BagSendInterval && !RestoreBag)
            {
                _bagDirty = false;
                _bagSendTime = Time.unscaledTime;
                RPC_ReportBag(EncodeLocalBag());
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_ReportBag(string data)
        {
            if (data == null || data.Length > 2048) return;
            WriteNetworkBag(data);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_BagRestored()
        {
            RestoreBag = false;
        }

        /// <summary>내 컴퓨터의 가방을 문자열로 ("플레이어칸수|아이템,개수,내구도;...").</summary>
        public string EncodeLocalBag()
        {
            var builder = new System.Text.StringBuilder();
            CraftingSystem.PlayerInventory inventory = _bagInventory != null ? _bagInventory : GetComponent<CraftingSystem.PlayerInventory>();
            CraftingSystem.PouchInventory pouch = _bagPouch != null ? _bagPouch : GetComponent<CraftingSystem.PouchInventory>();

            int playerSlots = inventory != null ? Mathf.Min(inventory.MaxSlots, BagCapacity) : 0;
            builder.Append(playerSlots).Append('|');
            int written = 0;
            if (inventory != null)
            {
                for (int i = 0; i < playerSlots; i++)
                {
                    CraftingSystem.InventorySlot slot = i < inventory.Slots.Count ? inventory.Slots[i] : null;
                    AppendSlot(builder, slot);
                    written++;
                }
            }
            if (pouch != null)
            {
                foreach (CraftingSystem.InventorySlot slot in pouch.Slots)
                {
                    if (written >= BagCapacity) break;
                    if (slot == null || slot.IsEmpty) continue;
                    AppendSlot(builder, slot);
                    written++;
                }
            }
            return builder.ToString();
        }

        private static void AppendSlot(System.Text.StringBuilder builder, CraftingSystem.InventorySlot slot)
        {
            if (slot == null || slot.IsEmpty) { builder.Append("-;"); return; }
            builder.Append(ItemIndexLookup.IndexOf(slot.item)).Append(',').Append(slot.count).Append(',').Append(slot.durability).Append(';');
        }

        /// <summary>호스트: 받은 문자열을 보관 칸에 쓴다.</summary>
        private void WriteNetworkBag(string data)
        {
            ParseBag(data, out int playerSlots, out List<int[]> entries);
            BagPlayerSlots = playerSlots;
            for (int i = 0; i < BagCapacity; i++)
            {
                int[] e = i < entries.Count ? entries[i] : null;
                BagItems.Set(i, e != null ? e[0] + 1 : 0);
                BagCounts.Set(i, e != null ? e[1] : 0);
                BagDurability.Set(i, e != null ? e[2] : 0);
            }
        }

        /// <summary>호스트 보관본을 문자열로 (복구용).</summary>
        private string EncodeNetworkBag()
        {
            var builder = new System.Text.StringBuilder();
            builder.Append(BagPlayerSlots).Append('|');
            for (int i = 0; i < BagCapacity; i++)
            {
                if (BagItems[i] <= 0 || BagCounts[i] <= 0) { builder.Append("-;"); continue; }
                builder.Append(BagItems[i] - 1).Append(',').Append(BagCounts[i]).Append(',').Append(BagDurability[i]).Append(';');
            }
            return builder.ToString();
        }

        private static void ParseBag(string data, out int playerSlots, out List<int[]> entries)
        {
            playerSlots = 0;
            entries = new List<int[]>();
            if (string.IsNullOrEmpty(data)) return;

            int bar = data.IndexOf('|');
            if (bar < 0) return;
            int.TryParse(data.Substring(0, bar), out playerSlots);
            playerSlots = Mathf.Clamp(playerSlots, 0, BagCapacity);

            foreach (string part in data.Substring(bar + 1).Split(';'))
            {
                if (entries.Count >= BagCapacity) break;
                if (part.Length == 0) continue;
                if (part == "-") { entries.Add(null); continue; }
                string[] f = part.Split(',');
                if (f.Length < 3 || !int.TryParse(f[0], out int index) || !int.TryParse(f[1], out int count) || !int.TryParse(f[2], out int dur))
                {
                    entries.Add(null);
                    continue;
                }
                entries.Add(index >= 0 && count > 0 ? new[] { index, Mathf.Min(count, 9999), Mathf.Max(0, dur) } : null);
            }
        }

        /// <summary>내 캐릭터: 문자열 가방으로 플레이어 칸/파우치를 덮어쓴다 (칸 위치 유지).</summary>
        private void ApplyBag(string data)
        {
            ParseBag(data, out int playerSlots, out List<int[]> entries);
            CraftingSystem.PlayerInventory inventory = GetComponent<CraftingSystem.PlayerInventory>();
            CraftingSystem.PouchInventory pouch = GetComponent<CraftingSystem.PouchInventory>();

            var playerList = new List<CraftingSystem.InventorySlot>();
            var pouchList = new List<CraftingSystem.InventorySlot>();
            for (int i = 0; i < entries.Count; i++)
            {
                CraftingSystem.InventorySlot slot = ToSlot(entries[i]);
                if (i < playerSlots) playerList.Add(slot);
                else if (slot != null) pouchList.Add(slot);
            }

            if (inventory != null) inventory.Inventory.ReplaceAllKeepPositions(playerList);
            if (pouch != null) pouch.Inventory.ReplaceAll(pouchList);
        }

        private static CraftingSystem.InventorySlot ToSlot(int[] entry)
        {
            if (entry == null) return null;
            CraftingSystem.ItemData item = ItemIndexLookup.ItemAt(entry[0]);
            if (item == null) return null;
            return new CraftingSystem.InventorySlot(item, entry[1]) { durability = entry[2] };
        }

        #endregion

        #region 트럭 탑승 (숨기기)

        private bool _seatedApplied;
        private readonly List<Renderer> _seatHiddenRenderers = new List<Renderer>();
        private readonly List<Collider> _seatDisabledColliders = new List<Collider>();
        private readonly List<UnityEngine.Behaviour> _seatDisabledBehaviours = new List<UnityEngine.Behaviour>();

        /// <summary>트럭에 타 있는가 (NetworkTruck 좌석).</summary>
        public bool IsSeated => Object != null && Object.IsValid && NetworkTruck.IsSeated(Owner);

        public override void Render()
        {
            bool seated = IsSeated;
            if (seated != _seatedApplied) ApplySeated(seated);
            if (Departed != _departedApplied) ApplyDeparted(Departed);
            UpdateBagSync();
        }

        /// <summary>
        /// 트럭에 타면 캐릭터 모델/충돌을 끄고, 내 캐릭터면 무기/상호작용 등 입력 스크립트도 끈다.
        /// 내리면 끈 것만 다시 켠다 (원래 꺼져 있던 것은 그대로).
        /// </summary>
        private void ApplySeated(bool seated)
        {
            _seatedApplied = seated;

            if (seated)
            {
                foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer != null && renderer.enabled)
                    {
                        renderer.enabled = false;
                        _seatHiddenRenderers.Add(renderer);
                    }
                }
                foreach (Collider collider in GetComponentsInChildren<Collider>(true))
                {
                    if (collider != null && collider.enabled && !(collider is CharacterController))
                    {
                        collider.enabled = false;
                        _seatDisabledColliders.Add(collider);
                    }
                }
                if (IsLocal)
                {
                    foreach (UnityEngine.Behaviour behaviour in GetLocalOnlyBehaviours())
                    {
                        if (behaviour != null && behaviour.enabled)
                        {
                            behaviour.enabled = false;
                            _seatDisabledBehaviours.Add(behaviour);
                        }
                    }
                }
                return;
            }

            foreach (Renderer renderer in _seatHiddenRenderers)
            {
                if (renderer != null) renderer.enabled = true;
            }
            foreach (Collider collider in _seatDisabledColliders)
            {
                if (collider != null) collider.enabled = true;
            }
            if (IsAlive)
            {
                foreach (UnityEngine.Behaviour behaviour in _seatDisabledBehaviours)
                {
                    if (behaviour != null) behaviour.enabled = true;
                }
            }
            else
            {
                // 탄 채로 기절했으면 지금은 켜지 않고, 부활할 때 다시 켠다.
                foreach (UnityEngine.Behaviour behaviour in _seatDisabledBehaviours)
                {
                    if (behaviour != null && !_downDisabledBehaviours.Contains(behaviour)) _downDisabledBehaviours.Add(behaviour);
                }
            }
            _seatHiddenRenderers.Clear();
            _seatDisabledColliders.Clear();
            _seatDisabledBehaviours.Clear();
        }

        #endregion

        #region 도우미

        /// <summary>키보드/마우스를 직접 읽는 컴포넌트 목록 (인스펙터 지정 또는 기본 이름 목록).</summary>
        private List<UnityEngine.Behaviour> GetLocalOnlyBehaviours()
        {
            var result = new List<UnityEngine.Behaviour>();
            if (localOnlyBehaviours != null && localOnlyBehaviours.Length > 0)
            {
                foreach (UnityEngine.Behaviour behaviour in localOnlyBehaviours)
                {
                    if (behaviour != null) result.Add(behaviour);
                }
                return result;
            }

            foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && DefaultLocalOnlyTypes.Contains(behaviour.GetType().Name)) result.Add(behaviour);
            }
            return result;
        }

        /// <summary>
        /// 입력을 읽는 스크립트를 끈다.
        /// keepWeaponView: 다른 사람 캐릭터(복제본)는 무기 컨트롤러를 "보여주기 전용"으로 남긴다 (NetworkWeaponVisuals가 있으면).
        /// </summary>
        private void DisableLocalOnlyBehaviours(bool keepWeaponView = false)
        {
            bool hasWeaponView = keepWeaponView && GetComponent<NetworkWeaponVisuals>() != null;
            foreach (UnityEngine.Behaviour behaviour in GetLocalOnlyBehaviours())
            {
                if (behaviour == null) continue;
                if (hasWeaponView && behaviour is Combat.WeaponController) continue;
                behaviour.enabled = false;
            }
        }

        /// <summary>해당 PlayerRef의 캐릭터 (없으면 null).</summary>
        public static NetworkPlayer Find(PlayerRef player)
        {
            if (player == PlayerRef.None) return null;
            foreach (NetworkPlayer p in AllPlayers)
            {
                if (p != null && p.Owner == player) return p;
            }
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AllPlayers.Clear();
            LoadedPlayers.Clear();
            Local = null;
            LocalPlayerSpawned = null;
            LocalPlayerDespawned = null;
            LocalPlayerDied = null;
            LocalPlayerRevived = null;
        }

        #endregion
    }
}
