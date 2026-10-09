using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 메인 카메라 흔들림. 시네머신이 카메라 위치를 정한 다음(실행 순서 뒤) 작은 오프셋/회전을 더한다.
    /// 시네머신이 매 프레임 카메라 위치를 새로 쓰므로 흔들림이 누적되지 않는다.
    /// 사용: CameraShake.Shake(0.3f)  (0~1, 클수록 세게. 여러 번 호출하면 쌓이고 서서히 가라앉는다)
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class CameraShake : MonoBehaviour
    {
        private static CameraShake _instance;

        [Tooltip("최대 이동 거리(m). 실제 흔들림은 trauma^2에 비례해서 작은 흔들림은 거의 안 느껴진다")]
        [SerializeField] private float maxOffset = 0.4f;
        [Tooltip("최대 회전 각도(도)")]
        [SerializeField] private float maxAngle = 3f;
        [SerializeField] private float frequency = 24f;
        [Tooltip("초당 가라앉는 양(trauma)")]
        [SerializeField] private float decayPerSecond = 2.2f;

        private float _trauma;
        private float _seed;

        public static void Shake(float amount)
        {
            if (amount <= 0f)
                return;

            if (_instance == null)
            {
                Camera cam = Camera.main;
                if (cam == null)
                    return;
                _instance = cam.GetComponent<CameraShake>();
                if (_instance == null)
                    _instance = cam.gameObject.AddComponent<CameraShake>();
            }

            _instance.AddTrauma(amount);
        }

        private void Awake()
        {
            _instance = this;
            _seed = Random.value * 100f;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        private void LateUpdate()
        {
            if (_trauma <= 0f)
                return;

            float s = _trauma * Mathf.Sqrt(_trauma);
            float t = Time.time * frequency + _seed;
            Vector3 offset = new Vector3(Noise(t, 0f), Noise(t, 17f), Noise(t, 31f)) * (maxOffset * s);
            Vector3 euler = new Vector3(Noise(t, 47f), Noise(t, 63f), Noise(t, 79f)) * (maxAngle * s);

            transform.position += transform.TransformVector(offset);
            transform.rotation *= Quaternion.Euler(euler);

            _trauma = Mathf.Max(0f, _trauma - decayPerSecond * Time.deltaTime);
        }

        private static float Noise(float t, float offset)
        {
            return Mathf.PerlinNoise(t, offset) * 2f - 1f;
        }
    }
}
