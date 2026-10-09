using UnityEngine;

namespace LastTruck
{
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset;

        // LateUpdate: 캐릭터 이동(네트워크 보간 포함)이 끝난 뒤에 따라가야 카메라가 떨리지 않는다.
        private void LateUpdate()
        {
            // 멀티플레이에서는 내 캐릭터가 스폰되기 전까지 target이 비어 있다 (스폰되면 자동으로 연결됨).
            if (target == null) return;
            transform.position = target.position + offset;
        }
    }
}
