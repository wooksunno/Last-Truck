using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 바퀴 콜라이더가 없는 보조 바퀴(6륜 트럭의 중간/뒤쪽 차축)를 다른 바퀴 콜라이더의 움직임에 맞춰 돌린다.
    /// 물리(바퀴 4개)는 그대로 두고 눈에 보이는 바퀴만 늘리기 위한 것: 서스펜션 높이와 회전은 source를 따라가고,
    /// 트럭 앞뒤 방향으로 localOffset만큼 떨어진 자리에 놓인다.
    /// </summary>
    public class TruckWheelFollower : MonoBehaviour
    {
        public WheelCollider source;
        [Tooltip("source 바퀴 기준 위치 차이 (트럭 기준 좌표).")]
        public Vector3 localOffset;

        private Transform _body;

        private void Awake()
        {
            Rigidbody rb = GetComponentInParent<Rigidbody>();
            _body = rb != null ? rb.transform : transform.root;
        }

        private void LateUpdate()
        {
            if (source == null) return;
            source.GetWorldPose(out Vector3 position, out Quaternion rotation);
            transform.SetPositionAndRotation(position + _body.rotation * localOffset, rotation);
        }
    }
}
