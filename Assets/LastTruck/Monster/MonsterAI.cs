using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 싱글플레이 몬스터 AI: "Player" 태그 대상을 쫓아가서 사거리 안이면 공격한다.
/// 멀티플레이에서는 꺼지고 LastTruck.Networking.NetworkMonster가 대신 동작한다.
///
/// 애니메이션/타이밍은 같은 오브젝트의 MonsterAnimationSet을 따른다 (Monster Animator2: Speed/Attack/Hit/Dead).
///  - 공격: 클립 길이만큼 제자리에서 휘두르고, 타격 순간(OnAttackHit 이벤트 또는 attackHitTiming)에 사거리 안이면 피해
///  - 경직: 맞으면 피격 클립 길이만큼 멈춤 (타격 전이면 공격 취소), 끝난 뒤 stunImmunitySeconds 동안 면역
///  - 사망: 사망 클립을 끝까지 재생한 뒤 사라짐 (그동안 충돌 끔)
///  - 기절한 플레이어는 공격하지 않는다
/// </summary>
public class MonsterAI : MonoBehaviour
{
    #region 인스펙터

    [Header("공격")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.2f;
    [Tooltip("타격 순간에 대상이 이만큼 더 멀어져도 맞은 것으로 친다(m).")]
    [SerializeField] private float hitRangeTolerance = 0.5f;

    #endregion

    #region 상태

    private Transform playerTarget;
    private LastTruck.PlayerStats _targetStats;
    private NavMeshAgent agent;
    private Animator animator;
    private MonsterAnimationSet _animSet;
    private Combat.Damageable _damageable;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    private float _hitDelay = 0.083f;
    private float _attackDuration = 0.333f;
    private float _stunDuration = 0.4f;
    private float _stunImmunity = 1f;

    private float _nextAttackTime;
    private float _attackLockUntil;
    private bool _hitPending;
    private bool _hitIsTruck;
    private LastTruck.TruckInformation _truck;
    private Collider[] _truckColliders;
    private float _hitFallbackTime;
    private float _stunUntil;
    private float _stunImmuneUntil;
    private bool _dead;

    #endregion

    #region 생명주기

    void Awake()
    {
        _damageable = GetComponent<Combat.Damageable>();
    }

    void OnEnable()
    {
        if (_damageable == null) _damageable = GetComponent<Combat.Damageable>();
        if (_damageable != null)
        {
            _damageable.Damaged += OnDamaged;
            _damageable.Died += OnDied;
            _damageable.Stunned += OnStunned;
        }
    }

    void OnDisable()
    {
        // 멀티플레이에서는 NetworkMonster가 이 스크립트를 끈다 → 이벤트도 해제 (싱글 전용 동작)
        if (_damageable != null)
        {
            _damageable.Damaged -= OnDamaged;
            _damageable.Died -= OnDied;
            _damageable.Stunned -= OnStunned;
        }
    }

    void Start()
    {
        LastTruck.TruckStabilizer.IgnoreMonster(gameObject); // 트럭이 몬스터에 올라타 튀지 않게
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        _animSet = GetComponent<MonsterAnimationSet>();
        ReadAttackTiming();
        _nextAttackTime = Time.time + attackCooldown;

        // "Player" 태그를 가진 오브젝트를 찾아서 그 트랜스폼을 타겟으로 지정합니다.
        GameObject playerObj = GameObject.FindWithTag("Player");

        if (playerObj != null)
        {
            playerTarget = playerObj.transform;
            _targetStats = playerObj.GetComponentInParent<LastTruck.PlayerStats>();
        }
        else
        {
            Debug.LogError("씬에 'Player' 태그를 가진 오브젝트가 없습니다!");
        }

        if (agent != null) agent.stoppingDistance = Mathf.Max(agent.stoppingDistance, attackRange * 0.9f);
    }

    void Update()
    {
        if (_dead || agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        // 애니메이션 이벤트가 안 왔을 때 대신 타격 (조금 여유)
        if (_hitPending && Time.time >= _hitFallbackTime) DealHit();

        bool stunned = Time.time < _stunUntil;
        bool attacking = Time.time < _attackLockUntil;
        bool targetDowned = _targetStats != null && _targetStats.IsDownedOffline;

        if (stunned || attacking || targetDowned)
        {
            agent.isStopped = true; // 경직 / 휘두르는 동안 / 대상 기절: 제자리
        }
        else if (playerTarget != null)
        {
            // 독버섯 미끼가 가까이 있으면 플레이어/트럭 대신 미끼로 몰려가서 먹는다 (플레이어가 바로 곁에 있으면 플레이어가 먼저)
            CraftingSystem.PoisonBait bait = CraftingSystem.PoisonBait.FindFor(_damageable, transform.position);
            if (bait != null && !CraftingSystem.PoisonBait.PlayerIsCloser(transform.position, playerTarget.position))
            {
                agent.isStopped = false;
                if (bait.Reached(transform.position))
                {
                    bait.Feed(_damageable);   // 먹는 동안은 멈춰 서서 독에 중독된다
                    agent.isStopped = true;
                }
                else
                {
                    agent.SetDestination(bait.transform.position);
                }
            }
            else
            {
                // 독성 유인 페로몬을 바른 덫이 있으면 그쪽으로 끌려가서 덫에 걸린다
                CraftingSystem.TrapPlaced lureTrap = CraftingSystem.PoisonBait.PlayerIsCloser(transform.position, playerTarget.position)
                    ? null : CraftingSystem.TrapPlaced.FindLureFor(transform.position);
                if (lureTrap != null)
                {
                    agent.isStopped = false;
                    if (lureTrap.Reached(transform.position)) lureTrap.TryCatch(_damageable);
                    else agent.SetDestination(lureTrap.transform.position);
                }
                else if (WantsTruck())
                {
                    // 트럭 방어 밤: 플레이어가 멀리 있으면 트럭을 노린다 (멀티플레이 몬스터와 같은 규칙)
                    Vector3 point = ClosestPointOnTruck(transform.position);
                    agent.isStopped = false;
                    agent.SetDestination(point);
                    TryAttackAt(point, true);
                }
                else
                {
                    agent.isStopped = false;
                    agent.SetDestination(playerTarget.position);
                    TryAttack();
                }
            }
        }

        // 걷기/대기 애니메이션 (Speed 파라미터)
        if (animator != null)
        {
            Vector3 v = agent.velocity;
            v.y = 0f;
            animator.SetFloat(SpeedHash, stunned || attacking ? 0f : v.magnitude);
        }
    }

    #endregion

    #region 공격

    private void ReadAttackTiming()
    {
        if (_animSet != null)
        {
            _attackDuration = _animSet.AttackDuration;
            _hitDelay = Mathf.Min(_animSet.AttackHitDelay, _attackDuration);
            _stunDuration = _animSet.StunDuration;
            _stunImmunity = _animSet.stunImmunitySeconds;
        }
        else if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null || clip.name.IndexOf("Attack", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                _attackDuration = clip.length;
                foreach (AnimationEvent e in clip.events)
                    if (e.functionName == nameof(OnAttackHit)) _hitDelay = e.time;
                break;
            }
        }
        attackCooldown = Mathf.Max(attackCooldown, _attackDuration);
    }

    private float Reach => attackRange + (agent != null ? agent.radius : 0.5f);

    private void TryAttack()
    {
        if (playerTarget == null) return;
        TryAttackAt(playerTarget.position, false);
    }

    private void TryAttackAt(Vector3 targetPos, bool truck)
    {
        if (Time.time < _nextAttackTime) return;
        if (HorizontalDistance(transform.position, targetPos) > Reach) return;

        _nextAttackTime = Time.time + attackCooldown;
        _attackLockUntil = Time.time + _attackDuration;
        _hitPending = true;
        _hitIsTruck = truck;
        _hitFallbackTime = Time.time + _hitDelay + 0.15f;

        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir.normalized);

        if (animator != null) animator.SetTrigger(AttackHash);
    }

    private void DealHit()
    {
        _hitPending = false;
        if (_dead) return;
        float scale = LastTruck.MonsterTier.DamageOf(gameObject);

        if (_hitIsTruck)
        {
            if (_truck == null || _truck.IsBroken) return;
            if (HorizontalDistance(transform.position, ClosestPointOnTruck(transform.position)) > Reach + hitRangeTolerance) return;
            _truck.Take_Damage(attackDamage * scale);
            return;
        }

        if (playerTarget == null) return;
        if (_targetStats != null && _targetStats.IsDownedOffline) return;
        if (HorizontalDistance(transform.position, playerTarget.position) > Reach + hitRangeTolerance) return; // 피했다

        LastTruck.IDamageable target = playerTarget.GetComponentInParent<LastTruck.IDamageable>();
        if (target != null) target.TakeDamage(attackDamage * scale, gameObject);
    }

    // ---- 트럭 방어 밤 (SurvivalDirector.TruckTargeting): 플레이어가 멀리 있으면 트럭을 공격한다

    private bool WantsTruck()
    {
        if (!LastTruck.SurvivalDirector.TruckTargeting) return false;
        if (_truck == null)
        {
            _truck = FindFirstObjectByType<LastTruck.TruckInformation>();
            if (_truck != null) _truckColliders = _truck.GetComponentsInChildren<Collider>();
        }
        if (_truck == null || _truck.IsBroken) return false;
        return HorizontalDistance(transform.position, playerTarget.position) > 10f;
    }

    private Vector3 ClosestPointOnTruck(Vector3 from)
    {
        Vector3 best = _truck.transform.position;
        float bestSqr = float.MaxValue;
        if (_truckColliders != null)
        {
            foreach (Collider c in _truckColliders)
            {
                if (c == null || !c.enabled || c.isTrigger || c is WheelCollider) continue;
                bool supported = c is BoxCollider || c is SphereCollider || c is CapsuleCollider || (c is MeshCollider m && m.convex);
                Vector3 p = supported ? c.ClosestPoint(from) : c.bounds.ClosestPoint(from);
                float d = (p - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = p; }
            }
        }
        return best;
    }

    #endregion

    #region 경직 / 사망

    private void OnDamaged(int amount)
    {
        if (_dead || !enabled || Time.time < _stunImmuneUntil) return;

        _hitPending = false;      // 타격 전에 맞았다 → 공격 취소
        _attackLockUntil = 0f;
        _stunUntil = Time.time + _stunDuration;
        _stunImmuneUntil = _stunUntil + _stunImmunity;
        _nextAttackTime = Mathf.Max(_nextAttackTime, _stunUntil);

        if (animator != null)
        {
            animator.ResetTrigger(AttackHash);
            animator.SetTrigger(HitHash);
        }
    }

    /// <summary>복서 기절 등: 피격 경직과 달리 면역 시간을 무시하고 지정 시간 동안 멈춘다.</summary>
    private void OnStunned(float duration)
    {
        if (_dead || !enabled || duration <= 0f) return;

        _hitPending = false;      // 휘두르던 공격 취소
        _attackLockUntil = 0f;
        _stunUntil = Mathf.Max(_stunUntil, Time.time + duration);
        _stunImmuneUntil = Mathf.Max(_stunImmuneUntil, _stunUntil + _stunImmunity);
        _nextAttackTime = Mathf.Max(_nextAttackTime, _stunUntil);

        if (animator != null)
        {
            animator.ResetTrigger(AttackHash);
            animator.SetTrigger(HitHash);
        }
    }

    private void OnDied()
    {
        if (_dead) return;
        _dead = true;
        _hitPending = false;

        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = false;
        if (animator != null)
        {
            animator.SetFloat(SpeedHash, 0f);
            animator.SetBool(DeadHash, true);
        }

        float delay = _animSet != null ? _animSet.DeathDuration + _animSet.corpseLingerSeconds : 1.5f;
        Destroy(gameObject, delay);
    }

    #endregion

    #region 애니메이션 이벤트 (공격 클립)

    public void OnAttackStart() { }

    public void OnAttackHit()
    {
        if (enabled && _hitPending) DealHit();
    }

    public void OnAttackEnd() { }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    #endregion
}
