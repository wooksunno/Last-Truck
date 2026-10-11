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
        private Animator _animator;
        private MonsterAnimationSet _animSet;
        private bool _deathApplied;

        // 경직 (호스트)
        private float _stunDuration = 0.4f;
        private float _stunImmunity = 1f;
        private float _stunUntil;
        private float _stunImmuneUntil;
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
        }

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

        public override void Spawned()
        {
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
            _agent.stoppingDistance = attackRange * 0.9f;
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
            if (Time.time < _stunImmuneUntil) return;

            if (_pendingTarget != AttackTarget.None)
            {
                _pendingTarget = AttackTarget.None; // 타격 전에 맞았다 → 공격 취소
                _pendingPlayer = null;
            }
            _attackLockUntil = 0f;
            _stunUntil = Time.time + _stunDuration;
            _stunImmuneUntil = _stunUntil + _stunImmunity;
            _nextAttackTime = Mathf.Max(_nextAttackTime, _stunUntil);
            RPC_PlayHit();
        }

        #endregion

        #region 호스트: 추적 / 공격

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (_agent == null || !_agent.enabled) return;
            if (!_agent.isOnNavMesh)
            {
                _agent.Warp(transform.position); // 스폰 직후 내비메시에 못 붙은 경우 다시 시도
                return;
            }

            bool dead = _health != null && _health.IsDead;
            if (dead || NetworkGameState.IsGameOver)
            {
                if (!_agent.isStopped) _agent.isStopped = true;
                _pendingTarget = AttackTarget.None;
                MoveSpeed = 0f;
                return;
            }

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

            // 쫓던 플레이어 유지 여부
            if (_targetPlayer != null)
            {
                bool keep = _targetPlayer.IsAlive && !_targetPlayer.IsSeated &&
                            HorizontalDistance(position, _targetPlayer.transform.position) <= loseRange;
                if (!keep) _targetPlayer = null;
            }

            // 인식 범위 안의 가장 가까운 플레이어 (쫓던 사람보다 더 가까운 사람이 있으면 바꾼다)
            NetworkPlayer nearest = null;
            float nearestDistance = detectionRange;
            IReadOnlyList<NetworkPlayer> players = NetworkPlayer.All;
            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer player = players[i];
                if (player == null || !player.IsAlive || !player.gameObject.activeInHierarchy) continue;
                if (player.IsSeated) continue; // 트럭에 탄 사람은 트럭을 공격한다

                float distance = HorizontalDistance(position, player.transform.position);
                if (distance <= nearestDistance)
                {
                    nearest = player;
                    nearestDistance = distance;
                }
            }

            if (nearest != null) _targetPlayer = nearest;
        }

        private void MoveTowardTarget()
        {
            Vector3 destination;
            if (_targetPlayer != null)
            {
                destination = _targetPlayer.transform.position;
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

            float reach = attackRange + (_agent != null ? _agent.radius : 0.5f) + hitRangeTolerance;
            if (target == AttackTarget.Player)
            {
                if (player == null || !player.IsAlive || player.IsSeated) return;
                if (HorizontalDistance(transform.position, player.transform.position) > reach) return; // 피했다

                NetworkHealth targetHealth = player.Health;
                if (targetHealth != null && !targetHealth.IsDead)
                    targetHealth.ApplyDamage(playerDamage, player.transform.position + Vector3.up, false);
            }
            else if (target == AttackTarget.Truck)
            {
                if (_truck == null || _truck.IsBroken) return;
                if (HorizontalDistance(transform.position, ClosestPointOnTruck(transform.position)) > reach) return;
                _truck.Take_Damage(truckDamage);
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
        private void RPC_PlayHit()
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return;
            _animator.ResetTrigger(AttackHash); // 공격이 취소되었으면 아직 안 나간 공격 모션도 취소
            _animator.SetTrigger(HitHash);
        }

        public override void Render()
        {
            bool dead = _health != null && _health.IsDead;
            if (dead && !_deathApplied) ApplyDeath();

            if (_animator == null || !_animator.isActiveAndEnabled) return;
            float target = dead ? 0f : MoveSpeed;
            _shownSpeed = Mathf.MoveTowards(_shownSpeed, target, 20f * Time.deltaTime);
            _animator.SetFloat(SpeedHash, _shownSpeed);
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
