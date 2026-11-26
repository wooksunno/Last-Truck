using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace LastTruck.Networking
{
    /// <summary>
    /// 멀티플레이 몬스터 AI (호스트에서만 실행, 위치는 NetworkTransform으로 모두에게 동기화).
    ///
    /// 추적 규칙
    ///  - 기본 목표: 트럭 (거리 제한 없음)
    ///  - 인식 범위(detectionRange) 안에 살아 있는 플레이어가 들어오면 그 플레이어로 목표 변경.
    ///    여러 명이면 가장 가까운 플레이어.
    ///  - 쫓던 플레이어가 loseRange 밖으로 나가거나 죽으면 다시 트럭(또는 범위 안 다른 플레이어).
    ///    (인식 범위보다 조금 넓게 잡아서 경계에서 목표가 깜빡이지 않게 한다)
    ///  - 목표는 retargetInterval초마다만 다시 고른다.
    ///
    /// 공격
    ///  - 목표가 공격 사거리 안이면 멈춰서 cooldown마다 공격 (공격 애니메이션 동안은 제자리).
    ///  - 피해는 공격 애니메이션의 OnAttackHit 이벤트 순간에 들어간다 (그때도 사거리 안이어야 맞음).
    ///    이벤트가 오지 않으면(애니메이터 없음 등) 클립에서 읽은 타격 시간 뒤에 대신 넣는다.
    ///  - 플레이어: NetworkHealth 피해 (피격 연출은 모두에게), 트럭: TruckInformation.Take_Damage.
    ///  - 공격 사거리에서 멈추므로 플레이어 몸을 파고들거나 밀지 않는다 (플레이어는 CharacterController라 밀리지도 않음).
    ///
    /// 애니메이션 (Monster Animator2: Speed float / Attack trigger / Hit trigger / Dead bool)
    ///  - 이동 속도는 호스트가 [Networked] MoveSpeed로 보내고, 모든 화면이 Speed 파라미터에 넣는다 (Idle ↔ Walk).
    ///  - 공격/피격은 RPC로 모두에게 트리거, 사망은 NetworkHealth.IsDead로 모두에게.
    ///  - 클립/타이밍은 같은 오브젝트의 MonsterAnimationSet에서 읽는다 (없으면 아래 기본값).
    ///
    /// 경직 (호스트)
    ///  - 맞으면 피격 클립 길이만큼 멈춘다. 타격 순간 전이었으면 그 공격은 취소된다.
    ///  - 경직이 끝난 뒤 MonsterAnimationSet.stunImmunitySeconds 동안은 다시 경직되지 않는다.
    ///
    /// 사망: 사망 클립을 끝까지 재생한 뒤 사라진다 (그동안 충돌 끔).
    ///
    /// 종류 (MonsterCatalog, 호스트가 스폰 직전에 [Networked] KindValue로 정함)
    ///  - 체력/공격력/공격속도/이동속도/사거리/피해 범위를 종류 표에서 읽는다 (밤이 지날수록 체력/공격력 증가).
    ///  - 근접 반원(탱커/빅 보이): 앞쪽 반원 안의 모두에게 피해.
    ///  - 원거리(스나이퍼/타겟터/오염체): 사거리에서 멈춰 투사체를 던진다. 떨어진 곳 근처에 있으면 맞는다(피할 수 있음).
    ///    타겟터는 트럭을 먼저 노리고, 오염체의 투사체는 바닥에 오염 지대(3초, 1초마다 피해)를 남긴다.
    ///  - 붐버: 빠르게 다가가며, 플레이어를 처음 본 뒤 fuseSeconds초 뒤 주변에 폭발 피해를 주고 죽는다.
    ///  - 모습: 크기/색/Synty 좀비 몸 메시 목록/애니메이션 속도.
    ///
    /// 싱글플레이(오프라인)에서는 이 스크립트가 아무것도 하지 않고 기존 MonsterAI가 동작한다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NetworkMonster : NetworkBehaviour
    {
        #region 인스펙터

        [Header("추적")]
        [Tooltip("이 거리 안에 플레이어가 들어오면 플레이어를 쫓는다.")]
        [SerializeField] private float detectionRange = 12f;
        [Tooltip("쫓던 플레이어가 이 거리 밖으로 나가면 포기한다 (detectionRange보다 크게).")]
        [SerializeField] private float loseRange = 15f;
        [Tooltip("목표를 다시 고르는 간격(초).")]
        [SerializeField] private float retargetInterval = 0.3f;

        [Header("공격")]
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float playerDamage = 10f;
        [SerializeField] private float truckDamage = 10f;
        [SerializeField] private float attackCooldown = 1.2f;

        [Header("공격 애니메이션 (클립 이벤트가 없을 때 쓰는 기본값)")]
        [Tooltip("공격 시작부터 피해가 들어가는 순간까지(초). 클립의 OnAttackHit 이벤트가 있으면 그 시간을 쓴다.")]
        [SerializeField] private float defaultHitDelay = 0.083f;
        [Tooltip("공격 애니메이션 길이(초). 이 시간 동안 제자리에서 휘두른다.")]
        [SerializeField] private float defaultAttackDuration = 0.333f;
        [Tooltip("타격 순간에 목표가 이만큼 더 멀어져도 맞은 것으로 친다(m).")]
        [SerializeField] private float hitRangeTolerance = 0.5f;

        [Header("디버그")]
        [Tooltip("스폰 위치/내비메시 여부를 콘솔에 출력한다 (호스트).")]
        [SerializeField] private bool logSpawn = true;

        #endregion

        #region 상태

        private NavMeshAgent _agent;
        private NetworkHealth _health;
        private Combat.Damageable _damageable;
        private NetworkPlayer _targetPlayer;
        private LastTruck.TruckInformation _truck;
        private Collider[] _truckColliders = new Collider[0];
        private Renderer[] _truckRenderers = new Renderer[0];
        private float _nextRetargetTime;
        private float _nextAttackTime;

        // 애니메이션
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DeadHash = Animator.StringToHash("Dead");
        // Mixamo 좀비 애니메이터 (MixamoAnimatorSetup): 없는 컨트롤러(다른 몬스터)에서는 건드리지 않는다
        private static readonly int LocoMulHash = Animator.StringToHash("LocoMul");
        private static readonly int RunVariantHash = Animator.StringToHash("RunVariant");
        private static readonly int HitVariantHash = Animator.StringToHash("HitVariant");
        private static readonly int DeathVariantHash = Animator.StringToHash("DeathVariant");
        private static readonly int KnockdownHash = Animator.StringToHash("Knockdown");
        private bool _mixamoAnim, _variantsSet;
        private float _walkClipSpeed, _runClipSpeedA, _runClipSpeedB;   // 클립이 원래 걷는 속도 (m/s, 몸 크기 1 기준)

        // 넘어짐 (호스트): 세게 맞으면 뒤로 넘어졌다가 일어난다
        private const float KnockdownSeconds = 3.7f;      // 넘어지기(1.9초) + 일어나기(1.8초)
        private const float KnockdownCooldown = 8f;
        private float _nextKnockdownTime;
        private float _knockdownUntil;
        private Animator _animator;
        private MonsterAnimationSet _animSet;
        private bool _deathApplied;

        // 경직 (호스트)
        private float _stunDuration = 0.4f;
        private float _stunImmunity = 1f;
        private float _stunUntil;
        private float _stunImmuneUntil;
        private Vector3 _knock;   // 넉백 속도 (호스트, 맞으면 때린 쪽 반대로 잠깐 밀린다)

        // ---- 동굴 잠복 좀비 (CaveLurkers가 만든다): 제자리에 있다가 가까이 오면 쫓아오고, 너무 멀리 끌려가면 돌아간다
        private const float LurkDetect = 14f, LurkLose = 28f, LurkLeash = 45f;
        private const float LurkMaxHeight = 6f;   // 굴 위 지면을 걷는 사람(높이 차가 큰 사람)은 알아채지 못한다
        /// <summary>동굴 잠복 좀비인가 (트럭을 노리지 않고, 아침 정리에도 지워지지 않는다).</summary>
        [Networked] public NetworkBool Lurker { get; private set; }
        private Vector3 _home;
        private float _lurkCalmUntil;

        /// <summary>호스트: 동굴 잠복 좀비로 만든다. speed > 0이면 그 속도로 달린다 (뛰는 좀비).</summary>
        public void HostMakeLurker(Vector3 home, float speed)
        {
            if (!HasStateAuthority) return;
            Lurker = true;
            _home = home;
            if (speed > 0f && _agent != null)
            {
                _agent.speed = speed;
                _agent.acceleration = Mathf.Max(_agent.acceleration, speed * 4f);
            }
        }
        private float _hitDelay;
        private float _attackDuration;
        private float _shownSpeed;

        // 진행 중인 공격 (호스트)
        private enum AttackTarget { None, Player, Truck }
        private AttackTarget _pendingTarget;
        private NetworkPlayer _pendingPlayer;
        private float _pendingHitTime;
        private float _attackLockUntil;

        /// <summary>호스트가 정한 이동 속도 (애니메이션 Idle/Walk용).</summary>
        [Networked] private float MoveSpeed { get; set; }

        /// <summary>몬스터 종류 (MonsterKind 번호).</summary>
        [Networked] public int KindValue { get; private set; }
        /// <summary>스폰된 밤 번호 (체력/공격력 배율).</summary>
        [Networked] private int SpawnNight { get; set; }
        /// <summary>붐버: 터지는 시각.</summary>
        [Networked] private TickTimer FuseTimer { get; set; }

        public MonsterKind Kind => (MonsterKind)KindValue;
        public MonsterTypeData TypeData => _type ?? MonsterCatalog.Instance.Get(Kind);

        private MonsterTypeData _type;
        private float _loseRangeExtra = 3f;
        private Vector3 _baseScale = Vector3.one;
        private bool _fuseFlashOn;
        private Color _tint = Color.white;

        // 원거리 투사체 / 오염 지대 (호스트)
        private struct PendingShot
        {
            public float ImpactTime;
            public Vector3 Point;
        }
        private struct Pool
        {
            public float EndTime;
            public float NextTick;
            public Vector3 Center;
        }
        private readonly List<PendingShot> _shots = new List<PendingShot>();
        private readonly List<Pool> _pools = new List<Pool>();

        /// <summary>호스트가 정한 몸집 배율 (중간보스 등). 1이면 보통. 모든 화면이 모델 크기를 맞춘다.</summary>
        [Networked] public float TierSize { get; set; }

        // 강함 등급 (호스트): 체력은 NetworkHealth가 최대 체력을 정한 뒤 첫 틱에 곱해 준다.
        private bool _tierQueued;
        private float _queuedHp = 1f;
        private bool _tierSizeApplied;

        /// <summary>호스트: 스폰 직후 부른다. 체력/피해 배율과 몸집(모두에게 동기화)을 정한다.</summary>
        public void QueueTier(float hpScale, float dmgScale, float size, bool boss)
        {
            if (!HasStateAuthority) return;
            MonsterTier tier = GetComponent<MonsterTier>();
            if (tier == null) tier = gameObject.AddComponent<MonsterTier>();
            tier.IsBoss = boss;
            tier.DamageScale = dmgScale;
            _queuedHp = hpScale;
            _tierQueued = true;
            TierSize = size;
        }

        #endregion

        #region 생명주기

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<NetworkHealth>();
            _damageable = GetComponent<Combat.Damageable>();
            _animator = GetComponentInChildren<Animator>();
            _animSet = GetComponent<MonsterAnimationSet>();
            ReadAttackTiming();
            ReadLocomotionClips();
        }

        /// <summary>Mixamo 좀비 애니메이터면 걷기/달리기 클립의 원래 속도를 읽는다 (재생 속도를 실제 이동 속도에 맞추려고).</summary>
        private void ReadLocomotionClips()
        {
            _mixamoAnim = false;
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            // (애니메이터가 아직 초기화 전일 수 있어 파라미터 대신 클립 이름으로 알아본다)
            foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;
                float v = clip.averageSpeed.magnitude;
                if (clip.name == "Zombie_Limp_Walk") _walkClipSpeed = v;
                else if (clip.name == "Zombie_Limp_Run") _jogClipSpeed = v;
                else if (clip.name == "Zombie_Run") { _runClipSpeedA = v; _mixamoAnim = true; }
                else if (clip.name == "Zombie_Running") _runClipSpeedB = v;
            }
        }

        private static readonly int StateWalk = Animator.StringToHash("Walk");
        private static readonly int StateJog = Animator.StringToHash("Jog");
        private static readonly int StateRunA = Animator.StringToHash("RunA");
        private static readonly int StateRunB = Animator.StringToHash("RunB");
        private float _jogClipSpeed;

        /// <summary>
        /// 지금 이동 속도(m/s)에 맞는 걷기/뛰기 재생 배율 (발이 바닥에서 미끄러지지 않게).
        /// 애니메이터가 실제로 재생 중인(또는 넘어가는 중인) 이동 상태의 클립 속도로 나눈다.
        /// </summary>
        private float LocomotionMultiplier(float speed)
        {
            // 서 있을 때(대기 동작)는 몬스터마다 재생 빠르기를 ±10% 다르게 (이동 중에는 이동 속도 자체가 ±10% 달라서 따로 줄 필요가 없다)
            if (speed < 0.15f) return _animJitter;
            AnimatorStateInfo st = _animator.IsInTransition(0) ? _animator.GetNextAnimatorStateInfo(0) : _animator.GetCurrentAnimatorStateInfo(0);
            int h = st.shortNameHash;
            float clip; Vector2 range;
            if (h == StateRunA) { clip = _runClipSpeedA; range = new Vector2(0.6f, 2.2f); }
            else if (h == StateRunB) { clip = _runClipSpeedB; range = new Vector2(0.6f, 2.2f); }
            else if (h == StateJog) { clip = _jogClipSpeed; range = new Vector2(0.7f, 1.5f); }
            else if (h == StateWalk) { clip = _walkClipSpeed; range = new Vector2(0.6f, 1.6f); }
            else return _animJitter;
            if (clip < 0.05f) return 1f;
            float scale = Mathf.Max(0.1f, _animator.humanScale * transform.lossyScale.y) * Mathf.Max(0.1f, _animator.speed);
            return Mathf.Clamp(speed / (clip * scale), range.x, range.y);
        }
        private int _variantRun;

        /// <summary>공격 클립의 길이와 OnAttackHit 이벤트 시간을 읽는다 (애니메이션과 피해 타이밍을 맞추기 위해).</summary>
        private void ReadAttackTiming()
        {
            _hitDelay = defaultHitDelay;
            _attackDuration = defaultAttackDuration;

            if (_animSet != null)
            {
                _attackDuration = _animSet.AttackDuration;
                _hitDelay = Mathf.Min(_animSet.AttackHitDelay, _attackDuration);
                _stunDuration = _animSet.StunDuration;
                _stunImmunity = _animSet.stunImmunitySeconds;
                attackCooldown = Mathf.Max(attackCooldown, _attackDuration);
                return;
            }

            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null || clip.name.IndexOf("Attack", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                _attackDuration = clip.length;
                foreach (AnimationEvent animationEvent in clip.events)
                {
                    if (animationEvent.functionName == nameof(OnAttackHit)) _hitDelay = animationEvent.time;
                }
                break;
            }
            // 쿨다운이 애니메이션보다 짧으면 휘두르는 도중에 또 공격하게 되므로 맞춘다.
            attackCooldown = Mathf.Max(attackCooldown, _attackDuration);
        }

        /// <summary>호스트: 스폰 직전(onBeforeSpawned)에 종류와 밤 번호를 정한다.</summary>
        public void InitializeBeforeSpawn(MonsterKind kind, int night)
        {
            KindValue = (int)kind;
            SpawnNight = Mathf.Max(1, night);
            MonsterCatalog catalog = MonsterCatalog.Instance;
            NetworkHealth health = GetComponent<NetworkHealth>();
            if (health != null) health.SpawnMaxOverride = catalog.Get(kind).maxHealth * catalog.HealthScale(SpawnNight);
        }

        /// <summary>모든 컴퓨터: 종류에 맞게 모습을 바꾸고, 호스트는 수치도 넣는다.</summary>
        private void ApplyType()
        {
            MonsterCatalog catalog = MonsterCatalog.Instance;
            _type = catalog.Get(Kind);

            // 모습: 종류별 프리팹(MonsterAppearance)이면 프리팹 값, 아니면(기본 프리팹) 종류 표의 값
            MonsterAppearance appearance = GetComponent<MonsterAppearance>();
            ZombieVariant variant = GetComponent<ZombieVariant>();
            int seed = (int)(Object.Id.Raw % 100000);
            Color tint;
            float animSpeed;
            if (appearance != null)
            {
                _baseScale = transform.localScale;
                if (variant != null) variant.ApplyAllowed(seed);
                tint = appearance.tint;
                animSpeed = appearance.animationSpeed;
            }
            else
            {
                _baseScale = Vector3.one * Mathf.Max(0.2f, _type.scale);
                transform.localScale = _baseScale;
                if (variant != null) variant.ApplyNames(_type.bodies, seed);
                tint = _type.tint;
                animSpeed = _type.animationSpeed;
            }
            _tint = tint;
            if (tint != Color.white) SetBodyColor(tint);
            if (_damageable != null) _damageable.RefreshRenderers();
            animSpeed = Mathf.Max(0.1f, animSpeed);
            if (_animator != null) _animator.speed = animSpeed;

            // 수치 (호스트만 쓰지만 모두 넣어도 무방)
            float damageScale = catalog.DamageScale(SpawnNight);
            playerDamage = _type.damage * damageScale;
            truckDamage = playerDamage * _type.truckDamageMultiplier;
            attackRange = Mathf.Max(0.5f, _type.attackRange);
            detectionRange = Mathf.Max(attackRange, _type.detectionRange);
            loseRange = detectionRange + _loseRangeExtra;

            _attackDuration /= animSpeed;
            _hitDelay /= animSpeed;
            float interval = _type.attacksPerSecond > 0.01f ? 1f / _type.attacksPerSecond : 999f;
            attackCooldown = Mathf.Max(interval, _attackDuration);
        }

        private void SetBodyColor(Color tint)
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer == null || renderer is ParticleSystemRenderer) continue;
                Material material = renderer.material;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", material.GetColor("_BaseColor") * tint);
                if (material.HasProperty("_Color")) material.SetColor("_Color", material.GetColor("_Color") * tint);
            }
        }

        public override void Spawned()
        {
            ApplyType();

            // 기존 싱글플레이 AI는 멀티플레이에서 쓰지 않는다 (모든 컴퓨터에서 끔).
            MonsterAI legacy = GetComponent<MonsterAI>();
            if (legacy != null) legacy.enabled = false;

            // 트럭과 물리 충돌하지 않게 (트럭이 몬스터에 올라타 튀지 않게)
            TruckStabilizer.IgnoreMonster(gameObject);

            if (!HasStateAuthority)
            {
                // 클라이언트: 위치는 호스트에서 받아오므로 길찾기를 끈다.
                if (_agent != null) _agent.enabled = false;
                return;
            }

            if (_health != null)
            {
                _health.DamagedOnHost += OnDamagedOnHost;
                // 사망 애니메이션을 끝까지 보여준 뒤 사라지게
                if (_animSet != null) _health.DespawnDelay = _animSet.DeathDuration + _animSet.corpseLingerSeconds;
            }

            // 길찾기 에이전트는 경로 계산만 하고, 실제 위치는 네트워크 틱(FixedUpdateNetwork)에서 옮긴다.
            // (에이전트가 Update에서 직접 옮기면 NetworkTransform과 서로 당겨서 몬스터가 떨린다)
            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _agent.Warp(transform.position);
            _agent.speed = Mathf.Max(0.5f, _type.moveSpeed * MonsterCatalog.Instance.speedScale);
            // 이동 속도도 몬스터마다 ±10% (모션 재생 빠르기와 같은 값 → 발은 그대로 맞는다, 무리가 줄 맞춰 오지 않게)
            _agent.speed *= SpeedJitter(Object.Id.Raw);
            _agent.acceleration = Mathf.Max(_agent.acceleration, _agent.speed * 3f);
            _agent.stoppingDistance = _type.ranged ? attackRange * 0.85f : attackRange * 0.9f;
            _truck = FindFirstObjectByType<LastTruck.TruckInformation>();
            if (_truck != null)
            {
                _truckColliders = _truck.GetComponentsInChildren<Collider>();
                _truckRenderers = _truck.GetComponentsInChildren<Renderer>();
            }
            _nextAttackTime = Time.time + attackCooldown;

            if (logSpawn)
            {
                float distance = _truck != null ? HorizontalDistance(transform.position, _truck.transform.position) : -1f;
                Debug.Log($"[NetworkMonster] 스폰 {transform.position} (트럭까지 {distance:0.0}m, 내비메시 {(_agent.isOnNavMesh ? "O" : "X")})");
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_health != null) _health.DamagedOnHost -= OnDamagedOnHost;
        }

        #endregion

        #region 호스트: 경직

        /// <summary>호스트: 피해를 받은 순간. 면역 시간이 아니면 경직 (진행 중인 공격은 타격 전이면 취소).</summary>
        private void OnDamagedOnHost(float amount)
        {
            if (_health == null || _health.IsDead || _health.Current <= 0f) return;
            AddKnockback(amount);   // 경직 면역 중이어도 맞으면 조금은 밀린다
            if (Time.time < _knockdownUntil) return;   // 넘어져 있는 동안은 피격 동작으로 끊지 않는다

            // 묵직한 한 방(체력의 30% 이상, 최소 25)이면 뒤로 넘어졌다가 일어난다 (중간보스 제외, 자주 넘어지지 않게 쿨다운)
            if (_mixamoAnim && TierSize < 1.5f && Time.time >= _nextKnockdownTime &&
                amount >= Mathf.Max(25f, _health.Max * 0.3f))
            {
                Knockdown(amount);
                return;
            }
            if (Time.time < _stunImmuneUntil) return;

            if (_pendingTarget != AttackTarget.None)
            {
                _pendingTarget = AttackTarget.None; // 타격 전에 맞았다 → 공격 취소
                _pendingPlayer = null;
            }
            _attackLockUntil = 0f;
            _stunUntil = Mathf.Max(_stunUntil, Time.time + _stunDuration);   // 더 긴 기절(복서)이 걸려 있으면 줄이지 않는다
            _stunImmuneUntil = _stunUntil + _stunImmunity;
            _nextAttackTime = Mathf.Max(_nextAttackTime, _stunUntil);
            RPC_PlayHit(UnityEngine.Random.Range(0, 2));
        }

        /// <summary>호스트: 뒤로 넘어졌다가 일어난다 (그동안 제자리, 공격 없음, 더 세게 밀린다).</summary>
        private void Knockdown(float amount)
        {
            _pendingTarget = AttackTarget.None;
            _pendingPlayer = null;
            _attackLockUntil = 0f;
            _knockdownUntil = Time.time + KnockdownSeconds;
            _nextKnockdownTime = _knockdownUntil + KnockdownCooldown;
            _stunUntil = Mathf.Max(_stunUntil, _knockdownUntil);
            _stunImmuneUntil = _stunUntil + _stunImmunity;
            _nextAttackTime = Mathf.Max(_nextAttackTime, _stunUntil);
            RPC_PlayKnockdown();   // (밀려나는 거리는 보통 피격과 같다: 30피해 ≈ 1.3m)
        }

        /// <summary>
        /// 호스트: 가장 가까운 플레이어(= 때렸을 가능성이 가장 큰 사람) 반대쪽으로 밀어낸다.
        /// 피해가 클수록 세게, 중간보스(몸집이 큰 몬스터)는 덜 밀린다. 실제 이동은 FixedUpdateNetwork에서 내비메시 위로만.
        /// </summary>
        private void AddKnockback(float amount)
        {
            Vector3 pos = transform.position;
            NetworkPlayer nearest = null;
            float best = 8f * 8f;
            foreach (NetworkPlayer p in NetworkPlayer.All)
            {
                if (p == null || !p.IsAlive) continue;
                float d = (p.transform.position - pos).sqrMagnitude;
                if (d < best) { best = d; nearest = p; }
            }
            if (nearest == null) return;

            Vector3 dir = pos - nearest.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = -transform.forward;
            // 약한 연사(총)는 살짝, 묵직한 한 방(도끼/전투 도끼)은 크게: 10피해 ≈ 0.4m, 25 ≈ 1m, 40 이상 ≈ 2m
            float strength = Mathf.Clamp(2.5f + amount * 0.16f, 2.5f, 9f);
            if (TierSize > 1.5f) strength /= TierSize * 1.6f;   // 중간보스(몸집 2배)만 묵직하게 (일반 좀비도 1.18배라 1.01 기준이면 다 덜 밀렸다)
            _knock = dir.normalized * Mathf.Max(_knock.magnitude, strength);
        }

        /// <summary>호스트: 남은 넉백만큼 내비메시 위로 밀고 빠르게 줄인다.</summary>
        private void StepKnockback()
        {
            if (_knock.sqrMagnitude < 0.01f) { _knock = Vector3.zero; return; }
            float dt = Runner.DeltaTime;
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.Move(_knock * dt);
            _knock = Vector3.MoveTowards(_knock, Vector3.zero, 20f * dt);
        }

        /// <summary>
        /// 기절 요청 (복서 능력 등, Damageable.ApplyStun이 부른다). 호스트면 바로, 클라이언트면 호스트에게 보낸다.
        /// 피격 경직과 달리 면역 시간을 무시한다.
        /// </summary>
        public void RequestStun(float duration)
        {
            if (Object == null || !Object.IsValid || duration <= 0f) return;
            if (HasStateAuthority) ApplyStunOnHost(duration);
            else RPC_RequestStun(duration);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestStun(float duration)
        {
            ApplyStunOnHost(duration);
        }

        private void ApplyStunOnHost(float duration)
        {
            if (!HasStateAuthority) return;
            if (_health == null || _health.IsDead || _health.Current <= 0f) return;

            duration = Mathf.Clamp(duration, 0f, 10f);   // 비정상 값 방어
            if (duration <= 0f) return;

            _pendingTarget = AttackTarget.None;   // 휘두르던 공격 취소
            _pendingPlayer = null;
            _attackLockUntil = 0f;
            _stunUntil = Mathf.Max(_stunUntil, Time.time + duration);
            _stunImmuneUntil = Mathf.Max(_stunImmuneUntil, _stunUntil + _stunImmunity);
            _nextAttackTime = Mathf.Max(_nextAttackTime, _stunUntil);
            if (Time.time >= _knockdownUntil) RPC_PlayHit(UnityEngine.Random.Range(0, 2));   // 넘어져 있으면 동작은 그대로
        }

        #endregion

        #region 호스트: 추적 / 공격

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (NetworkGameState.IsHeld) return; // 게임 전체 멈춤 (방장 변경 중)

            if (_tierQueued && _health != null && _health.Max > 0f)
            {
                float max = _health.Max * Mathf.Max(0.1f, _queuedHp);
                _health.Max = max;
                _health.Current = max;
                _tierQueued = false;
            }

            if (_agent == null || !_agent.enabled) return;
            if (!_agent.isOnNavMesh)
            {
                _agent.Warp(transform.position); // 스폰 직후 내비메시에 못 붙은 경우 다시 시도
                return;
            }

            // 이미 날아간 투사체 / 깔린 오염 지대는 몬스터가 죽어도 마저 처리한다.
            UpdateShots();
            UpdatePools();

            bool dead = _health != null && _health.IsDead;
            if (dead || NetworkGameState.IsGameOver)
            {
                if (!_agent.isStopped) _agent.isStopped = true;
                _pendingTarget = AttackTarget.None;
                MoveSpeed = 0f;
                return;
            }

            // 붐버: 플레이어를 보면 도화선에 불이 붙고, 시간이 되면 터진다.
            if (_type != null && _type.selfDestruct && UpdateFuse()) return;

            // 맞아서 밀려나는 중 (경직과 함께 보통 0.1~0.2초)
            StepKnockback();

            // 경직 중: 제자리 (공격/이동 없음)
            if (Time.time < _stunUntil)
            {
                if (!_agent.isStopped) _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
                MoveSpeed = 0f;
                ApplyAgentMovement();
                return;
            }

            // 애니메이션 이벤트가 오지 않았을 때 대신 피해를 넣는다 (조금 여유를 둔다).
            if (_pendingTarget != AttackTarget.None && Time.time >= _pendingHitTime + 0.15f) DealPendingHit();

            // 공격 애니메이션 동안은 제자리에서 휘두른다.
            if (Time.time < _attackLockUntil)
            {
                if (!_agent.isStopped) _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
                MoveSpeed = 0f;
                ApplyAgentMovement();
                return;
            }

            // 덫 등에 묶였으면 제자리에 멈춘다 (공격은 가능).
            bool rooted = _damageable != null && _damageable.IsRooted;
            if (rooted)
            {
                if (!_agent.isStopped) _agent.isStopped = true;
            }
            else if (Time.time >= _nextRetargetTime || _agent.isStopped)
            {
                _nextRetargetTime = Time.time + retargetInterval;
                UpdateTarget();
                MoveTowardTarget();
            }

            TryAttack();
            ApplyAgentMovement();
            MoveSpeed = rooted ? 0f : HorizontalSpeed(_agent.velocity);
        }

        /// <summary>에이전트가 계산한 다음 위치/방향을 실제 Transform에 반영 (NetworkTransform이 모두에게 동기화).</summary>
        private void ApplyAgentMovement()
        {
            transform.position = _agent.nextPosition;

            Vector3 velocity = _agent.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(velocity.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, _agent.angularSpeed * Runner.DeltaTime);
            }
        }

        private void UpdateTarget()
        {
            Vector3 position = transform.position;

            // 타겟터: 트럭이 멀쩡하면 플레이어를 무시하고 트럭만 노린다.
            if (!Lurker && _type != null && _type.prefersTruck && _truck != null && !_truck.IsBroken)
            {
                _targetPlayer = null;
                return;
            }

            // 동굴 잠복 좀비: 집에서 너무 멀리 끌려 나왔으면 포기하고 돌아간다 (잠깐은 다시 쫓지 않는다)
            if (Lurker && _targetPlayer != null && HorizontalDistance(_home, position) > LurkLeash)
            {
                _targetPlayer = null;
                _lurkCalmUntil = Time.time + 4f;
            }
            if (Lurker && Time.time < _lurkCalmUntil) return;
            float lose = Lurker ? LurkLose : loseRange;

            // 쫓던 플레이어 유지 여부
            if (_targetPlayer != null)
            {
                bool keep = _targetPlayer.IsAlive && !_targetPlayer.IsSeated &&
                            HorizontalDistance(position, _targetPlayer.transform.position) <= lose;
                if (!keep) _targetPlayer = null;
            }

            // 인식 범위 안의 가장 가까운 플레이어 (쫓던 사람보다 더 가까운 사람이 있으면 바꾼다)
            NetworkPlayer nearest = null;
            float nearestDistance = Lurker ? LurkDetect : detectionRange;
            IReadOnlyList<NetworkPlayer> players = NetworkPlayer.All;
            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer player = players[i];
                if (player == null || !player.IsAlive || !player.gameObject.activeInHierarchy) continue;
                if (player.IsSeated) continue; // 트럭에 탄 사람은 트럭을 공격한다
                if (Lurker && _targetPlayer == null && Mathf.Abs(player.transform.position.y - position.y) > LurkMaxHeight) continue;

                float distance = HorizontalDistance(position, player.transform.position);
                if (distance <= nearestDistance)
                {
                    nearest = player;
                    nearestDistance = distance;
                }
            }

            if (nearest != null) _targetPlayer = nearest;
        }

        /// <summary>독버섯 미끼에 이끌리는 중인가 (플레이어가 바로 곁에 있으면 플레이어가 먼저). 미끼를 돌려준다.</summary>
        private CraftingSystem.PoisonBait LuringBait()
        {
            CraftingSystem.PoisonBait bait = CraftingSystem.PoisonBait.FindFor(_damageable, transform.position);
            if (bait == null) return null;
            if (_targetPlayer != null && CraftingSystem.PoisonBait.PlayerIsCloser(transform.position, _targetPlayer.transform.position)) return null;
            return bait;
        }

        /// <summary>독성 유인 페로몬을 바른 덫에 이끌리는 중인가 (플레이어가 바로 곁에 있으면 플레이어가 먼저).</summary>
        private CraftingSystem.TrapPlaced LuringTrap()
        {
            if (_targetPlayer != null && CraftingSystem.PoisonBait.PlayerIsCloser(transform.position, _targetPlayer.transform.position)) return null;
            return CraftingSystem.TrapPlaced.FindLureFor(transform.position);
        }

        private void MoveTowardTarget()
        {
            // 독버섯 미끼: 플레이어/트럭 대신 미끼로 가서 먹는다 (먹는 동안은 멈춰 서서 독에 중독된다)
            CraftingSystem.PoisonBait bait = LuringBait();
            if (bait != null)
            {
                if (bait.Reached(transform.position))
                {
                    bait.Feed(_damageable);
                    _agent.isStopped = true;
                }
                else
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(bait.transform.position);
                }
                return;
            }

            // 페로몬 덫: 냄새를 따라 덫으로 가서 걸린다
            CraftingSystem.TrapPlaced lureTrap = LuringTrap();
            if (lureTrap != null)
            {
                _agent.isStopped = false;
                if (lureTrap.Reached(transform.position)) lureTrap.TryCatch(_damageable);
                else _agent.SetDestination(lureTrap.transform.position);
                return;
            }

            Vector3 destination;
            if (_targetPlayer != null)
            {
                destination = _targetPlayer.transform.position;
            }
            else if (Lurker)
            {
                // 잠복 좀비: 쫓을 사람이 없으면 제자리(집)로 돌아가 멈춰 선다
                if (HorizontalDistance(transform.position, _home) < 1.5f) { _agent.isStopped = true; return; }
                destination = _home;
            }
            else if (_truck != null)
            {
                destination = ClosestPointOnTruck(transform.position);
            }
            else
            {
                return;
            }

            _agent.isStopped = false;
            _agent.SetDestination(destination);
        }

        private void TryAttack()
        {
            if (Time.time < _nextAttackTime) return;
            if (_type != null && _type.selfDestruct) return; // 붐버는 때리지 않고 터진다
            if (LuringBait() != null || LuringTrap() != null) return;   // 미끼/페로몬 덫에 정신이 팔려 공격하지 않는다

            if (_targetPlayer != null && _targetPlayer.IsSeated) _targetPlayer = null; // 방금 트럭에 탔으면 트럭을 노린다

            if (_targetPlayer != null)
            {
                if (HorizontalDistance(transform.position, _targetPlayer.transform.position) > attackRange + _agent.radius) return;

                NetworkHealth targetHealth = _targetPlayer.Health;
                if (targetHealth == null || targetHealth.IsDead) return;

                FaceTowards(_targetPlayer.transform.position);
                StartAttack(AttackTarget.Player, _targetPlayer);
            }
            else if (_truck != null && !_truck.IsBroken)
            {
                Vector3 closest = ClosestPointOnTruck(transform.position);
                if (HorizontalDistance(transform.position, closest) > attackRange + _agent.radius) return;

                FaceTowards(closest);
                StartAttack(AttackTarget.Truck, null);
            }
        }

        /// <summary>공격 시작: 모두에게 공격 애니메이션, 피해는 타격 순간(OnAttackHit)에.</summary>
        private void StartAttack(AttackTarget target, NetworkPlayer player)
        {
            _nextAttackTime = Time.time + attackCooldown;
            _attackLockUntil = Time.time + _attackDuration;
            _pendingTarget = target;
            _pendingPlayer = player;
            _pendingHitTime = Time.time + _hitDelay;
            RPC_PlayAttack();
        }

        /// <summary>타격 순간: 목표가 아직 사거리 안에 있으면 피해.</summary>
        private void DealPendingHit()
        {
            AttackTarget target = _pendingTarget;
            NetworkPlayer player = _pendingPlayer;
            _pendingTarget = AttackTarget.None;
            _pendingPlayer = null;
            if (!HasStateAuthority || (_health != null && _health.IsDead) || NetworkGameState.IsGameOver) return;

            // 원거리: 지금 목표 위치로 투사체를 던진다 (떨어지는 곳 근처에 있으면 맞는다).
            if (_type != null && _type.ranged)
            {
                FireAt(target, player);
                return;
            }

            float reach = attackRange + (_agent != null ? _agent.radius : 0.5f) + hitRangeTolerance;
            float dmgScale = MonsterTier.DamageOf(gameObject);

            // 반원/원 공격: 범위 안의 모두
            if (_type != null && _type.hitShape != MonsterHitShape.Single)
            {
                AreaHit(transform.position, Mathf.Max(reach, _type.hitRadius), _type.hitShape == MonsterHitShape.FrontArc, playerDamage * dmgScale, truckDamage * dmgScale);
                return;
            }

            if (target == AttackTarget.Player)
            {
                if (player == null || !player.IsAlive || player.IsSeated) return;
                if (HorizontalDistance(transform.position, player.transform.position) > reach) return; // 피했다

                NetworkHealth targetHealth = player.Health;
                if (targetHealth != null && !targetHealth.IsDead)
                {
                    targetHealth.ApplyDamage(playerDamage * dmgScale, player.transform.position + Vector3.up, false);
                    PushPlayer(player, transform.position, playerDamage * dmgScale);
                }
            }
            else if (target == AttackTarget.Truck)
            {
                if (_truck == null || _truck.IsBroken) return;
                if (HorizontalDistance(transform.position, ClosestPointOnTruck(transform.position)) > reach) return;
                _truck.Take_Damage(truckDamage * dmgScale);
            }
        }

        #endregion

        #region 호스트: 종류별 특수 공격

        /// <summary>붐버: 도화선 처리. 터졌으면 true (이번 틱 나머지 처리 안 함).</summary>
        private bool UpdateFuse()
        {
            if (!FuseTimer.IsRunning)
            {
                bool seen = false;
                IReadOnlyList<NetworkPlayer> players = NetworkPlayer.All;
                for (int i = 0; i < players.Count && !seen; i++)
                {
                    NetworkPlayer player = players[i];
                    if (player != null && player.IsAlive && HorizontalDistance(transform.position, player.transform.position) <= detectionRange) seen = true;
                }
                // 트럭 바로 옆까지 왔으면 (트럭에 탄 사람만 있을 때 등) 그때 불을 붙인다.
                if (!seen && _truck != null && HorizontalDistance(transform.position, ClosestPointOnTruck(transform.position)) <= 4f) seen = true;
                if (seen) FuseTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.5f, _type.fuseSeconds));
                return false;
            }

            if (!FuseTimer.Expired(Runner)) return false;
            Explode();
            return true;
        }

        /// <summary>붐버: 주변 원 범위에 피해를 주고 죽는다.</summary>
        private void Explode()
        {
            FuseTimer = TickTimer.None;
            float radius = Mathf.Max(1f, _type.hitRadius);
            RPC_Explode(transform.position, radius);
            AreaHit(transform.position, radius, false, playerDamage, truckDamage);

            if (_agent != null && !_agent.isStopped) _agent.isStopped = true;
            MoveSpeed = 0f;
            if (_health != null && !_health.IsDead) _health.ApplyDamage(_health.Current + 1f, transform.position + Vector3.up, false);
        }

        /// <summary>호스트: 맞은 플레이어를 때린 곳 반대쪽으로 밀어낸다 (8피해 ≈ 0.7m, 40피해 ≈ 1.4m). 모두에게 같은 위치로 동기화된다.</summary>
        private static void PushPlayer(NetworkPlayer player, Vector3 from, float damage)
        {
            if (player == null || player.Health == null || player.Health.IsDead) return;
            NetworkPlayerMovement movement = player.GetComponent<NetworkPlayerMovement>();
            if (movement == null) return;
            Vector3 dir = player.transform.position - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = -player.transform.forward;
            movement.PushFromHit(dir.normalized * Mathf.Clamp(3.5f + damage * 0.05f, 3.5f, 5.5f));   // 밀려나며 때린 쪽으로 몸을 돌린다
        }

        /// <summary>
        /// 범위 피해: center에서 radius 안의 플레이어(트럭에 탄 사람 제외)와 트럭.
        /// frontArc면 몬스터 앞쪽 반원(180도)만.
        /// </summary>
        private void AreaHit(Vector3 center, float radius, bool frontArc, float damageToPlayer, float damageToTruck)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            IReadOnlyList<NetworkPlayer> players = NetworkPlayer.All;
            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer player = players[i];
                if (player == null || !player.IsAlive || player.IsSeated) continue;
                Vector3 offset = player.transform.position - center;
                offset.y = 0f;
                if (offset.magnitude > radius) continue;
                if (frontArc && offset.sqrMagnitude > 0.04f && Vector3.Dot(forward, offset.normalized) < 0f) continue;

                NetworkHealth health = player.Health;
                if (health != null && !health.IsDead)
                {
                    health.ApplyDamage(damageToPlayer, player.transform.position + Vector3.up, false);
                    PushPlayer(player, center, damageToPlayer);
                }
            }

            if (_truck != null && !_truck.IsBroken && damageToTruck > 0f)
            {
                Vector3 closest = ClosestPointOnTruck(center);
                Vector3 offset = closest - center;
                offset.y = 0f;
                bool inArc = !frontArc || offset.sqrMagnitude < 0.04f || Vector3.Dot(forward, offset.normalized) >= 0f;
                if (offset.magnitude <= radius && inArc) _truck.Take_Damage(damageToTruck);
            }
        }

        /// <summary>원거리: 목표의 지금 위치로 투사체를 던진다.</summary>
        private void FireAt(AttackTarget target, NetworkPlayer player)
        {
            Vector3 aim;
            if (target == AttackTarget.Player && player != null && player.IsAlive && !player.IsSeated)
            {
                aim = player.transform.position;
            }
            else if (_truck != null && !_truck.IsBroken)
            {
                aim = ClosestPointOnTruck(transform.position);
            }
            else
            {
                return;
            }

            Vector3 from = transform.position + Vector3.up * (1.4f * _baseScale.y);
            aim.y = Mathf.Min(aim.y, transform.position.y + 3f);
            float distance = Vector3.Distance(from, aim);
            float speed = Mathf.Max(1f, _type.projectileSpeed);
            _shots.Add(new PendingShot { ImpactTime = Time.time + distance / speed, Point = aim });
            RPC_FireProjectile(from, aim);
        }

        private void UpdateShots()
        {
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                if (Time.time < _shots[i].ImpactTime) continue;
                Vector3 point = _shots[i].Point;
                _shots.RemoveAt(i);
                if (NetworkGameState.IsGameOver || _type == null) continue;

                if (_type.leavesPool)
                {
                    _pools.Add(new Pool { EndTime = Time.time + _type.poolDuration, NextTick = Time.time, Center = point });
                    RPC_SpawnPool(point, _type.poolRadius, _type.poolDuration);
                }
                else
                {
                    AreaHit(point, Mathf.Max(0.3f, _type.projectileHitRadius), false, playerDamage, truckDamage);
                }
            }
        }

        private void UpdatePools()
        {
            for (int i = _pools.Count - 1; i >= 0; i--)
            {
                Pool pool = _pools[i];
                if (Time.time >= pool.EndTime)
                {
                    _pools.RemoveAt(i);
                    continue;
                }
                if (Time.time < pool.NextTick) continue;
                pool.NextTick = Time.time + 1f;
                _pools[i] = pool;

                float tick = _type.poolDamagePerSecond * MonsterCatalog.Instance.DamageScale(SpawnNight);
                AreaHit(pool.Center, _type.poolRadius, false, tick, 0f); // 오염 지대는 사람만 (트럭 바퀴는 버틴다)
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_FireProjectile(Vector3 from, Vector3 to)
        {
            MonsterTypeData type = TypeData;
            Color color = type.leavesPool ? MonsterEffects.PoisonColor
                        : type.prefersTruck ? MonsterEffects.TargeterColor
                        : MonsterEffects.SniperColor;
            float arc = type.leavesPool ? 2.5f : 0.3f;
            MonsterEffects.Projectile(from, to, color, type.projectileSpeed, arc);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SpawnPool(Vector3 center, float radius, float duration)
        {
            MonsterEffects.Pool(center, radius, duration);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Explode(Vector3 center, float radius)
        {
            MonsterEffects.Explosion(center, radius);
        }

        /// <summary>모든 화면: 붐버 도화선이 타는 동안 점점 빠르게 깜빡이고 부풀었다 줄었다 한다.</summary>
        private void RenderFuse(bool dead)
        {
            if (_type == null || !_type.selfDestruct) return;
            bool burning = !dead && Runner != null && FuseTimer.IsRunning;
            if (!burning)
            {
                if (_fuseFlashOn) SetFuseFlash(false);
                transform.localScale = _baseScale;
                return;
            }

            float remaining = FuseTimer.RemainingTime(Runner) ?? 0f;
            float speed = Mathf.Lerp(14f, 3f, Mathf.Clamp01(remaining / Mathf.Max(0.5f, _type.fuseSeconds)));
            bool on = Mathf.Sin(Time.time * speed * Mathf.PI) > 0f;
            if (on != _fuseFlashOn) SetFuseFlash(on);
            transform.localScale = _baseScale * (1f + (on ? 0.08f : 0f));
        }

        private void SetFuseFlash(bool on)
        {
            _fuseFlashOn = on;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer) continue;
                Material material = renderer.material;
                Color color = on ? new Color(1f, 0.25f, 0.15f) : _tint;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", on ? new Color(0.9f, 0.15f, 0.05f) : Color.black);
                }
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            }
        }

        #endregion

        #region 애니메이션

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayAttack()
        {
            if (_animator != null && _animator.isActiveAndEnabled) _animator.SetTrigger(AttackHash);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayHit(int variant)
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return;
            _animator.ResetTrigger(AttackHash); // 공격이 취소되었으면 아직 안 나간 공격 모션도 취소
            if (_mixamoAnim) _animator.SetInteger(HitVariantHash, variant);
            _animator.SetTrigger(HitHash);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayKnockdown()
        {
            if (_animator == null || !_animator.isActiveAndEnabled || !_mixamoAnim) return;
            _animator.ResetTrigger(AttackHash);
            _animator.ResetTrigger(HitHash);
            _animator.SetTrigger(KnockdownHash);
        }

        /// <summary>달리기/사망 동작 종류: 몬스터마다 다르게, 모든 화면에서 같게 (네트워크 번호로 고른다).</summary>
        private void ApplyVariants()
        {
            _variantsSet = true;
            if (!_mixamoAnim) return;
            uint id = Object != null && Object.IsValid ? Object.Id.Raw : 0u;
            // 몬스터마다 다르게 (네트워크 번호로 정하므로 모든 화면에서 같다): 무리가 똑같이 움직이지 않게
            // (번호가 연속이라 그냥 나누면 비슷한 값이 몰린다 → 항목마다 다른 소금을 넣어 골고루 섞는다)
            _variantRun = (int)(Mix(id, 1u) % 2u);
            _animator.SetInteger(RunVariantHash, _variantRun);
            _animator.SetInteger(DeathVariantHash, (int)(Mix(id, 2u) % 4u));
            _animator.SetBool(MirrorHash, (Mix(id, 3u) & 1u) == 1u);                    // 좌우 반전: 절뚝이는 다리/휘두르는 팔이 바뀐다
            _animator.SetFloat(CycleOffsetHash, (Mix(id, 4u) % 1000u) / 1000f);        // 걸음 박자
            _animator.SetFloat(IdleVariantHash, Mix(id, 5u) % 4u);                      // 대기 동작 4종
            _animJitter = SpeedJitter(id);                                              // 재생 빠르기 ±10%
        }

        /// <summary>정수 섞기 (같은 번호 + 같은 소금이면 어느 컴퓨터에서나 같은 값).</summary>
        private static uint Mix(uint x, uint salt)
        {
            x ^= salt * 0x9E3779B9u;
            x ^= x >> 16; x *= 0x7FEB352Du;
            x ^= x >> 15; x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }

        /// <summary>몬스터마다 이동 속도/대기 동작 빠르기 배율 (0.9 ~ 1.1).</summary>
        private static float SpeedJitter(uint id) => 0.9f + (Mix(id, 6u) % 200u) / 1000f;
        private static readonly int MirrorHash = Animator.StringToHash("Mirror");
        private static readonly int CycleOffsetHash = Animator.StringToHash("CycleOffset");
        private static readonly int IdleVariantHash = Animator.StringToHash("IdleVariant");
        private float _animJitter = 1f;

        public override void Render()
        {
            bool dead = _health != null && _health.IsDead;
            if (dead && !_deathApplied) ApplyDeath();
            RenderFuse(dead);

            // 몸집 (중간보스): 호스트가 정한 값을 모든 화면에서 한 번 적용
            if (!_tierSizeApplied && TierSize > 1.01f)
            {
                _tierSizeApplied = true;
                MonsterTier tier = GetComponent<MonsterTier>();
                if (tier == null) tier = gameObject.AddComponent<MonsterTier>();
                tier.ApplySize(TierSize);
            }

            if (_animator == null || !_animator.isActiveAndEnabled) return;
            if (!_variantsSet) ApplyVariants();
            float target = dead ? 0f : MoveSpeed;
            _shownSpeed = Mathf.MoveTowards(_shownSpeed, target, 20f * Time.deltaTime);
            _animator.SetFloat(SpeedHash, _shownSpeed);
            if (_mixamoAnim) _animator.SetFloat(LocoMulHash, LocomotionMultiplier(_shownSpeed));
            _animator.SetBool(DeadHash, dead);
        }

        /// <summary>모든 화면: 사망 애니메이션 동안 총알/근접이 시체에 맞지 않게 충돌을 끈다.</summary>
        private void ApplyDeath()
        {
            _deathApplied = true;
            foreach (Collider collider in GetComponentsInChildren<Collider>())
            {
                if (collider != null) collider.enabled = false;
            }
        }

        // 공격 클립(Monster Attack)의 애니메이션 이벤트. 피해 판정은 호스트에서만.
        public void OnAttackStart() { }

        public void OnAttackHit()
        {
            if (Object != null && Object.IsValid && HasStateAuthority && _pendingTarget != AttackTarget.None) DealPendingHit();
        }

        public void OnAttackEnd() { }

        #endregion

        #region 도우미

        private static float HorizontalSpeed(Vector3 velocity)
        {
            velocity.y = 0f;
            return velocity.magnitude;
        }

        /// <summary>
        /// 트럭 몸체 콜라이더 중 가장 가까운 표면 점.
        /// 주의: WheelCollider/볼록이 아닌 MeshCollider 등은 Collider.ClosestPoint를 지원하지 않아
        /// "입력 좌표를 그대로" 돌려준다 → 거리 0으로 계산되어 몬스터가 멀리서(화면 밖) 트럭을 때리는 버그가 있었다.
        /// 그래서 바퀴는 빼고, 지원하지 않는 콜라이더는 bounds로 대신한다.
        /// </summary>
        private Vector3 ClosestPointOnTruck(Vector3 from)
        {
            Vector3 best = _truck.transform.position;
            float bestDistance = float.MaxValue;
            foreach (Collider collider in _truckColliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (collider is WheelCollider) continue;

                bool supported = collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider
                                 || (collider is MeshCollider mesh && mesh.convex);
                Vector3 point = supported ? collider.ClosestPoint(from) : collider.bounds.ClosestPoint(from);
                float distance = (point - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = point;
                }
            }

            // 몸체 콜라이더가 하나도 없으면 트럭 모델(렌더러) 범위로 대신한다.
            if (bestDistance == float.MaxValue && _truckRenderers.Length > 0)
            {
                Bounds bounds = _truckRenderers[0].bounds;
                for (int i = 1; i < _truckRenderers.Length; i++)
                {
                    if (_truckRenderers[i] != null) bounds.Encapsulate(_truckRenderers[i].bounds);
                }
                best = bounds.ClosestPoint(from);
            }
            return best;
        }

        private void FaceTowards(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        #endregion
    }
}
