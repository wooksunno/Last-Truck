using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CraftingSystem
{
    /// <summary>
    /// 달리는 트럭 앞에 있는 좀비를 밀쳐내고 피해를 준다 (기획서: 트럭 로드킬).
    ///  - 피해 = 트럭 공격력(강화) × 전면 파츠 배율 × 속도 비율. 피해가 좀비 체력보다 크면 그대로 쓰러진다(로드킬).
    ///  - 전면 파츠가 없으면 약하게 밀치기만 하고, 강철가시 범퍼/분쇄 휠을 달면 세게 밀치고 크게 다친다.
    /// 몬스터와 트럭 물리는 호스트에서만 돌므로, 멀티플레이에서는 호스트(또는 싱글)만 판정한다.
    /// </summary>
    public class TruckRam : MonoBehaviour
    {
        [SerializeField] private float minSpeed = 2.5f;         // 이 속도(m/s) 이상일 때만 들이받는다
        [SerializeField] private float hitCooldown = 0.7f;      // 같은 대상을 다시 칠 수 있는 간격

        private TruckUpgradeState _state;
        private Rigidbody _rb;
        private LastTruck.TruckMove _move;
        private Bounds _localBounds;
        private bool _hasBounds;
        private readonly Dictionary<Combat.Damageable, float> _lastHit = new Dictionary<Combat.Damageable, float>();

        private void Awake()
        {
            _state = GetComponent<TruckUpgradeState>();
            _rb = GetComponent<Rigidbody>();
            _move = GetComponent<LastTruck.TruckMove>();
        }

        private bool HasAuthority()
        {
            if (!LastTruck.Networking.GameLauncher.IsOnlineSession) return true;
            var net = LastTruck.Networking.NetworkTruck.Instance;
            return net != null && net.Object != null && net.Object.IsValid && net.HasStateAuthority;
        }

        private void CaptureBounds()
        {
            // 트럭 몸체(로컬 좌표) 크기: 바퀴/포탑 같은 자식 콜라이더까지 포함한 대략적인 상자
            var inv = transform.worldToLocalMatrix;
            bool any = false; Bounds b = new Bounds();
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || c is WheelCollider) continue;
                Bounds wb = c.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? wb.min.x : wb.max.x, (i & 2) == 0 ? wb.min.y : wb.max.y, (i & 4) == 0 ? wb.min.z : wb.max.z);
                    Vector3 lp = inv.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(lp, Vector3.zero); any = true; } else b.Encapsulate(lp);
                }
            }
            if (any) { _localBounds = b; _hasBounds = true; }
        }

        private void FixedUpdate()
        {
            if (_rb == null || _state == null || !HasAuthority()) return;
            float speed = _rb.linearVelocity.magnitude;
            if (speed < minSpeed) return;
            if (!_hasBounds) CaptureBounds();
            if (!_hasBounds) return;

            // 달리는 방향(앞/뒤)의 범퍼 쪽 상자
            float forward = Vector3.Dot(_rb.linearVelocity, transform.forward) >= 0f ? 1f : -1f;
            Vector3 half = new Vector3(_localBounds.extents.x + 0.3f, Mathf.Max(0.9f, _localBounds.extents.y * 0.6f), 1.1f);
            Vector3 localCenter = new Vector3(_localBounds.center.x, _localBounds.center.y, forward > 0 ? _localBounds.max.z + 0.6f : _localBounds.min.z - 0.6f);
            Vector3 center = transform.TransformPoint(localCenter);

            TruckUpgradeRules.FrontRam(_state.PartAt(TruckPartSlot.Front), out float dmgMul, out float push);
            float maxSpeed = _move != null ? Mathf.Max(1f, _move.maxSpeed) : 18f;
            float speed01 = Mathf.Clamp01(speed / (maxSpeed * 0.7f));
            int damage = Mathf.RoundToInt(_state.StatValue(TruckStat.Attack) * dmgMul * Mathf.Lerp(0.5f, 1f, speed01));

            foreach (var col in Physics.OverlapBox(center, half, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (col.transform.IsChildOf(transform)) continue;
                var target = col.GetComponentInParent<Combat.Damageable>();
                if (target == null) continue;
                // 플레이어는 치지 않는다 (싱글: PlayerMove / 멀티: NetworkPlayer)
                if (target.GetComponentInParent<LastTruck.PlayerMove>() != null) continue;
                if (target.GetComponentInParent<LastTruck.Networking.NetworkPlayer>() != null) continue;
                if (_lastHit.TryGetValue(target, out float t) && Time.time - t < hitCooldown) continue;
                _lastHit[target] = Time.time;

                // 밀치기: 트럭 진행 방향 + 옆으로 비켜나는 방향
                Vector3 away = target.transform.position - transform.position; away.y = 0f;
                Vector3 side = Vector3.Project(away, transform.right);
                Vector3 dir = (transform.forward * forward * 0.7f + side.normalized * 0.6f).normalized;
                var agent = target.GetComponentInParent<NavMeshAgent>();
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(dir * push * Mathf.Lerp(0.6f, 1f, speed01));
                else target.transform.position += dir * push * 0.5f;
                target.Root(0.5f);   // 잠깐 휘청

                if (damage > 0) target.TakeDamage(damage, col.ClosestPoint(center), null);
            }
        }
    }
}
