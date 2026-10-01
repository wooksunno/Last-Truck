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
    ///  - 사망하면 조작 스크립트(무기/상호작용 등)를 끈다. 오브젝트는 몇 초 뒤 호스트가 없앤다.
    ///
    /// 다른 사람 캐릭터 (내 화면에 보이는 복제본)
    ///  - 키보드/마우스를 직접 읽는 컴포넌트(상호작용, 인벤토리 단축키, 무기, 근접 공격)를 끈다.
    ///    그렇지 않으면 내가 E를 누를 때 다른 사람 캐릭터도 같이 상호작용해 버린다.
    ///
    /// 프리팹은 에디터 메뉴 "LastTruck > Multiplayer > 1. 네트워크 프리팹 생성"이 자동으로 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkPlayer : NetworkBehaviour
    {
        #region 정적 목록 / 이벤트

        private static readonly List<NetworkPlayer> AllPlayers = new List<NetworkPlayer>();
        private static readonly HashSet<PlayerRef> LoadedPlayers = new HashSet<PlayerRef>();

        /// <summary>지금 이 컴퓨터에 스폰되어 있는 모든 플레이어 캐릭터.</summary>
        public static IReadOnlyList<NetworkPlayer> All => AllPlayers;

        /// <summary>내 캐릭터 (없으면 null - 아직 스폰 전이거나 사망 후).</summary>
        public static NetworkPlayer Local { get; private set; }

        public static event Action<NetworkPlayer> LocalPlayerSpawned;
        public static event Action<NetworkPlayer> LocalPlayerDespawned;
        public static event Action<NetworkPlayer> LocalPlayerDied;

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

        private NetworkHealth _health;

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
        public void InitializeBeforeSpawn(string nickname, int characterIndex)
        {
            Nickname = PlayerProfile.ClampForNetwork(nickname);
            CharacterIndex = characterIndex;
        }

        public override void Spawned()
        {
            if (!AllPlayers.Contains(this)) AllPlayers.Add(this);
            gameObject.name = $"Player_{Nickname}{(Object.HasInputAuthority ? " (나)" : string.Empty)}";

            if (_health != null) _health.Died += OnDied;

            if (Object.HasInputAuthority)
            {
                Local = this;
                try
                {
                    LocalPlayerBinder.BindLocalPlayer(this);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                LocalPlayerSpawned?.Invoke(this);

                // 내 캐릭터가 보인다 = 게임 씬 로딩이 끝났다 → 호스트에게 알린다.
                RPC_ReportLoaded();
            }
            else
            {
                DisableLocalOnlyBehaviours(keepWeaponView: true);
            }
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
            if (_health != null) _health.Died -= OnDied;
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

        #region 사망

        private void OnDied(NetworkHealth health)
        {
            if (!IsLocal) return;

            // 조작 불가: 무기/상호작용 등 입력을 읽는 스크립트를 끈다 (이동은 NetworkPlayerMovement가 막는다).
            DisableLocalOnlyBehaviours();
            LocalPlayerDied?.Invoke(this);
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
            if (IsAlive) // 탄 채로 죽었으면 조작 스크립트는 다시 켜지 않는다
            {
                foreach (UnityEngine.Behaviour behaviour in _seatDisabledBehaviours)
                {
                    if (behaviour != null) behaviour.enabled = true;
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
        }

        #endregion
    }
}
