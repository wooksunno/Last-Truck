using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 싱글플레이 몬스터 AI: "Player" 태그 대상을 쫓아가서 사거리 안이면 공격한다.
/// 멀티플레이에서는 꺼지고 LastTruck.Networking.NetworkMonster가 대신 동작한다.
///
/// 공격 타이밍은 Monster Attack 클립의 애니메이션 이벤트를 따른다.
///  - OnAttackHit: 이 순간 대상이 아직 사거리(+여유) 안이면 피해
///  - 클립 길이만큼 제자리에서 휘두른다
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
    private NavMeshAgent agent;
    private Animator animator;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private float _hitDelay = 0.083f;
    private float _attackDuration = 0.333f;
    private float _nextAttackTime;
    private float _attackLockUntil;
    private bool _hitPending;
    private float _hitFallbackTime;

    #endregion

    #region 생명주기

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        ReadAttackTiming();
        _nextAttackTime = Time.time + attackCooldown;

        // "Player" 태그를 가진 오브젝트를 찾아서 그 트랜스폼을 타겟으로 지정합니다.
        GameObject playerObj = GameObject.FindWithTag("Player");

        if (playerObj != null)
        {
            playerTarget = playerObj.transform;
        }
        else
        {
            Debug.LogError("씬에 'Player' 태그를 가진 오브젝트가 없습니다!");
        }

        if (agent != null) agent.stoppingDistance = Mathf.Max(agent.stoppingDistance, attackRange * 0.9f);
    }

    void Update()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        // 애니메이션 이벤트가 안 왔을 때 대신 타격 (조금 여유)
        if (_hitPending && Time.time >= _hitFallbackTime) DealHit();

        bool attacking = Time.time < _attackLockUntil;
        if (attacking)
        {
            agent.isStopped = true; // 휘두르는 동안 제자리
        }
        else if (playerTarget != null)
        {
            agent.isStopped = false;
            agent.SetDestination(playerTarget.position);
            TryAttack();
        }

        // 걷기/대기 애니메이션 (Monster Animator의 Speed 파라미터)
        if (animator != null)
        {
            Vector3 v = agent.velocity;
            v.y = 0f;
            animator.SetFloat(SpeedHash, attacking ? 0f : v.magnitude);
        }
    }

    #endregion

    #region 공격

    private void ReadAttackTiming()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null || clip.name.IndexOf("Attack", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            _attackDuration = clip.length;
            foreach (AnimationEvent e in clip.events)
                if (e.functionName == nameof(OnAttackHit)) _hitDelay = e.time;
            break;
        }
        attackCooldown = Mathf.Max(attackCooldown, _attackDuration);
    }

    private float Reach => attackRange + (agent != null ? agent.radius : 0.5f);

    private void TryAttack()
    {
        if (Time.time < _nextAttackTime || playerTarget == null) return;
        if (HorizontalDistance(transform.position, playerTarget.position) > Reach) return;

        _nextAttackTime = Time.time + attackCooldown;
        _attackLockUntil = Time.time + _attackDuration;
        _hitPending = true;
        _hitFallbackTime = Time.time + _hitDelay + 0.15f;

        Vector3 dir = playerTarget.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir.normalized);

        if (animator != null) animator.SetTrigger(AttackHash);
    }

    private void DealHit()
    {
        _hitPending = false;
        if (playerTarget == null) return;
        if (HorizontalDistance(transform.position, playerTarget.position) > Reach + hitRangeTolerance) return; // 피했다

        LastTruck.IDamageable target = playerTarget.GetComponentInParent<LastTruck.IDamageable>();
        if (target != null) target.TakeDamage(attackDamage, gameObject);
    }

    // Monster Attack 클립 애니메이션 이벤트. (멀티플레이에서는 이 스크립트가 꺼져 있으므로 무시)
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
