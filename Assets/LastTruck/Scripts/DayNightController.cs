using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 해(방향광) 각도. AtmosphereController의 해질녘 진행률을 따라 한낮 → 노을 → 지평선 아래로 천천히 내려가고,
    /// 동틀녘에는 같은 길을 거꾸로 올라온다. 해가 낮아질수록 그림자가 길어지고, 지평선 근처에서는
    /// AtmosphereController가 햇빛 그림자를 옅게 만들어 달 그림자로 넘긴다.
    /// </summary>
    public class DayNightController : MonoBehaviour
    {
        [Header("게임 매니저 연동")]
        [SerializeField] private GameManager gameManager;
        [Tooltip("비워두면 씬에서 찾는다.")]
        [SerializeField] private AtmosphereController atmosphere;

        [Header("해 높이 (X 각도)")]
        [Tooltip("한낮 각도 (90 = 바로 위).")]
        [SerializeField] private float dayAngleX = 90f;
        [Tooltip("노을이 가장 진할 때의 각도. 너무 낮으면 그림자가 화면 밖까지 길어진다.")]
        [SerializeField] private float sunsetAngleX = 22f;
        [Tooltip("해가 완전히 졌을 때의 각도 (지평선 아래).")]
        [SerializeField] private float setAngleX = -8f;
        [Tooltip("해가 지는 방향 (Y 각도).")]
        [SerializeField] private float sunYaw = 170f;

        [Tooltip("해 각도가 진행률을 따라갈 때의 부드러움.")]
        [SerializeField] private float smoothing = 3f;

        private float _angle;

        private void Start()
        {
            if (atmosphere == null) atmosphere = FindFirstObjectByType<AtmosphereController>();
            _angle = TargetAngle();
            Apply();
        }

        private float Progress()
        {
            if (atmosphere != null) return atmosphere.NightFactor;
            if (gameManager != null) return gameManager.DuskProgress;
            return 0f;
        }

        private static float Ease(float t) => t * t * (3f - 2f * t);

        private float TargetAngle()
        {
            float s = Mathf.Clamp01(Progress());
            // 0 ~ 0.45: 한낮 → 노을 높이 (천천히 기욺) / 0.45 ~ 0.78: 지평선 아래로 (AtmosphereController 단계와 맞춤)
            if (s < 0.45f) return Mathf.Lerp(dayAngleX, sunsetAngleX, Ease(s / 0.45f));
            return Mathf.Lerp(sunsetAngleX, setAngleX, Ease(Mathf.Clamp01((s - 0.45f) / 0.33f)));
        }

        private void Update()
        {
            float target = TargetAngle();
            _angle = Mathf.Lerp(_angle, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            Apply();
        }

        private void Apply()
        {
            transform.rotation = Quaternion.Euler(_angle, sunYaw, 0f);
        }
    }
}
