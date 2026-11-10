using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 바퀴 콜라이더가 없는 보조 바퀴(6륜 트럭의 중간/뒤쪽 차축)를 다른 바퀴 콜라이더의 움직임에 맞춰 돌린다.
    /// 물리(바퀴 4개)는 그대로 두고 눈에 보이는 바퀴만 늘리기 위한 것: 회전은 source를 따라가고,
    /// 트럭 앞뒤 방향으로 localOffset만큼 떨어진 자리에 놓인다.
    /// 높이는 자기 바로 밑 땅을 따로 찾아서 맞춘다 (뒤쪽 두 바퀴가 콜라이더 하나를 같이 쓰면 울퉁불퉁한 땅에서
    /// 한쪽은 공중에 뜨고 한쪽은 땅에 파묻혀 보였다). source가 움직일 수 있는 범위(서스펜션) 안에서만 움직인다.
    /// </summary>
    public class TruckWheelFollower : MonoBehaviour
    {
        public WheelCollider source;
        [Tooltip("source 바퀴 기준 위치 차이 (트럭 기준 좌표).")]
        public Vector3 localOffset;

        // 땅으로 보지 않는 레이어: 상호작용 트리거(4), 트럭이 지나가는 소품(9), 트럭 바퀴(10), 트럭만 막는 충돌체(11)
        private const int IgnoreMask = (1 << 2) | (1 << 4) | (1 << 9) | (1 << 10) | (1 << 11);

        private Transform _body;
        private float _heightOffset;   // 바퀴 콜라이더 높이 대비 이 바퀴의 높이 차이 (부드럽게 따라간다)

        private void Awake()
        {
            Rigidbody rb = GetComponentInParent<Rigidbody>();
            _body = rb != null ? rb.transform : transform.root;
        }

        private void LateUpdate()
        {
            if (source == null) return;
            source.GetWorldPose(out Vector3 position, out Quaternion rotation);
            Vector3 pos = position + _body.rotation * localOffset;

            float target = 0f;
            if (localOffset.sqrMagnitude > 0.0001f && FindGround(pos, out float groundY))
            {
                // 바퀴 아래쪽이 땅에 닿는 높이로 (서스펜션 거리 안에서만)
                float wantedCenterY = groundY + source.radius;
                float limit = Mathf.Max(0.05f, source.suspensionDistance);
                target = Mathf.Clamp(wantedCenterY - pos.y, -limit, limit);
            }
            _heightOffset = Mathf.Lerp(_heightOffset, target, 1f - Mathf.Exp(-20f * Time.deltaTime));

            transform.SetPositionAndRotation(pos + _body.up * _heightOffset, rotation);
        }

        private bool FindGround(Vector3 center, out float groundY)
        {
            groundY = 0f;
            Vector3 up = _body.up;
            float reach = source.radius + source.suspensionDistance + 0.6f;
            RaycastHit[] hits = Physics.RaycastAll(center + up * 0.6f, -up, reach + 0.6f, ~IgnoreMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (RaycastHit h in hits)
            {
                if (h.collider.transform.IsChildOf(_body)) continue;
                if (h.distance < best) { best = h.distance; groundY = h.point.y; }
            }
            return best < float.MaxValue;
        }
    }
}
