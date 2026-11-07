using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 플레이어가 이 트리거 안에 있는 동안 카메라 최대 거리와 최대 높이를 제한한다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CameraZone : MonoBehaviour
    {
        [Tooltip("피벗(머리) 기준 카메라 최대 거리(m)")]
        [SerializeField] private float maxCameraDistance = 3f;
        [Tooltip("캐릭터 발밑 기준 카메라 최대 높이(m). 0이면 높이 제한 없음")]
        [SerializeField] private float maxCameraHeight = 2.5f;
        [SerializeField] private string playerTag = "Player";

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            CameraDistanceLimiter.Instance?.EnterZone(maxCameraDistance, maxCameraHeight);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            CameraDistanceLimiter.Instance?.ExitZone(maxCameraDistance, maxCameraHeight);
        }
    }
}
