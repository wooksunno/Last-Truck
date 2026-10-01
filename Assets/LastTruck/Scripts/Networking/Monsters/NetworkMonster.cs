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
    ///  - 목표가 공격 사거리 안이면 멈춰서 cooldown마다 공격.
    ///  - 플레이어: NetworkHealth 피해 (피격 연출은 모두에게), 트럭: TruckInformation.Take_Damage.
    ///  - 공격 사거리에서 멈추므로 플레이어 몸을 파고들거나 밀지 않는다 (플레이어는 CharacterController라 밀리지도 않음).
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

        [Header("디버그")]
        [Tooltip("스폰 위치/내비메시 여부를 콘솔에 출력한다 (호스트).")]
        [SerializeField] private bool logSpawn = true;

        #endregion

        #region 상태

        private NavMeshAgent _agent;
        private NetworkHealth _health;
        private NetworkPlayer _targetPlayer;
        private LastTruck.TruckInformation _truck;
        private Collider[] _truckColliders = new Collider[0];
        private Renderer[] _truckRenderers = new Renderer[0];
        private float _nextRetargetTime;
        private float _nextAttackTime;

        #endregion

        #region 생명주기

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<NetworkHealth>();
        }

        public override void Spawned()
        {
            // 기존 싱글플레이 AI는 멀티플레이에서 쓰지 않는다 (모든 컴퓨터에서 끔).
            MonsterAI legacy = GetComponent<MonsterAI>();
            if (legacy != null) legacy.enabled = false;

            if (!HasStateAuthority)
            {
                // 클라이언트: 위치는 호스트에서 받아오므로 길찾기를 끈다.
                if (_agent != null) _agent.enabled = false;
                return;
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
                return;
            }

            if (Time.time >= _nextRetargetTime)
            {
                _nextRetargetTime = Time.time + retargetInterval;
                UpdateTarget();
                MoveTowardTarget();
            }

            TryAttack();
            ApplyAgentMovement();
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

                _nextAttackTime = Time.time + attackCooldown;
                FaceTowards(_targetPlayer.transform.position);
                targetHealth.ApplyDamage(playerDamage, _targetPlayer.transform.position + Vector3.up, false);
            }
            else if (_truck != null && !_truck.IsBroken)
            {
                Vector3 closest = ClosestPointOnTruck(transform.position);
                if (HorizontalDistance(transform.position, closest) > attackRange + _agent.radius) return;

                _nextAttackTime = Time.time + attackCooldown;
                FaceTowards(closest);
                _truck.Take_Damage(truckDamage);
            }
        }

        #endregion

        #region 도우미

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
