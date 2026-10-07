using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastTruck
{
    /// <summary>
    /// 낮/밤 분위기 제어. GameManager의 페이즈가 바뀌면 몇 초에 걸쳐 서서히 전환한다.
    ///  - 포스트 프로세싱: 낮(밝고 선명한 캐주얼) / 밤(푸른 톤, 강한 블룸, 비네트) Volume 두 개의 가중치를 섞는다
    ///  - 라이팅: 낮 태양 색/세기, 밤 달빛(청색 방향광), 주변광
    ///  - 안개/배경색, 캐릭터 셰이더용 전역 값(_GameNightFactor)
    /// 기획서 톤: "낮은 밝고, 밤은 약간 어둡게(완전한 암흑은 아님), 둥글고 단순화된 캐주얼 느낌".
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class AtmosphereController : MonoBehaviour
    {
        private static readonly int NightFactorId = Shader.PropertyToID("_GameNightFactor");

        [Header("연동")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Volume dayVolume;
        [SerializeField] private Volume nightVolume;
        [Tooltip("낮에 켜지는 태양(방향광)들. 밤에는 세기가 0으로 줄어든다. 비워두면 씬의 모든 방향광을 자동 수집한다.")]
        [SerializeField] private List<Light> sunLights = new List<Light>();
        [Tooltip("밤에 켜지는 달빛 방향광. 없으면 자동 생성한다.")]
        [SerializeField] private Light moonLight;

        [Header("전환")]
        [SerializeField] private float transitionSeconds = 4f;
        [Range(0f, 1f)] [SerializeField] private float startNightFactor = 0f;

        [Header("낮")]
        [SerializeField] private Color daySunColor = new Color(1f, 0.95f, 0.82f);
        [SerializeField] private float daySunIntensityScale = 1f;
        [SerializeField] private Color dayAmbient = new Color(0.22f, 0.26f, 0.34f);
        [SerializeField] private float dayAmbientIntensity = 0.5f;
        [SerializeField] private Color dayBackground = new Color(0.52f, 0.78f, 0.97f);
        [SerializeField] private Color dayFog = new Color(0.78f, 0.88f, 0.96f);
        [SerializeField] private float dayFogDensity = 0.0022f;

        [Header("밤")]
        [SerializeField] private Color moonColor = new Color(0.52f, 0.62f, 1f);
        [SerializeField] private float moonIntensity = 0.8f;
        [SerializeField] private Color nightAmbient = new Color(0.12f, 0.16f, 0.32f);
        [SerializeField] private float nightAmbientIntensity = 0.55f;
        [SerializeField] private Color nightBackground = new Color(0.05f, 0.07f, 0.17f);
        [SerializeField] private Color nightFog = new Color(0.08f, 0.10f, 0.24f);
        [SerializeField] private float nightFogDensity = 0.0055f;

        private float _nightFactor;      // 현재 값 (0 낮 ~ 1 밤)
        private float _target;
        private Camera _camera;
        private readonly List<float> _sunBaseIntensity = new List<float>();
        private readonly List<Color> _sunBaseColor = new List<Color>();
        private float _ambientBaseIntensity;

        public float NightFactor => _nightFactor;

        private void Awake()
        {
            _nightFactor = startNightFactor;
            _target = startNightFactor;
        }

        private void OnEnable()
        {
            if (gameManager == null) gameManager = GameManager.Instance != null ? GameManager.Instance : FindFirstObjectByType<GameManager>();
            if (gameManager != null) gameManager.OnPhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.OnPhaseChanged -= HandlePhaseChanged;
            Shader.SetGlobalFloat(NightFactorId, 0f);
        }

        private void Start()
        {
            _camera = Camera.main;
            if (sunLights.Count == 0)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional && l != moonLight && l.renderingLayerMask != 2) sunLights.Add(l);   // 렌더링 레이어 2 = 동굴 전용 보조광은 건드리지 않는다
            }
            foreach (var l in sunLights) { _sunBaseIntensity.Add(l.intensity); _sunBaseColor.Add(l.color); }
            EnsureMoon();

            if (gameManager != null)
                _target = _nightFactor = gameManager.CurrentPhase == GameManager.GamePhase.Night ? 1f : 0f;
            Apply(_nightFactor);
        }

        private void EnsureMoon()
        {
            if (moonLight != null) return;
            var go = new GameObject("MoonLight");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(52f, 200f, 0f);
            moonLight = go.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.shadows = LightShadows.Soft;
            moonLight.shadowStrength = 0.75f;
            moonLight.cullingMask = 55;   // 지상 레이어만 (동굴 조각 레이어 8은 점광원으로만 밝힌다)
            moonLight.intensity = 0f;
        }

        private void HandlePhaseChanged(GameManager.GamePhase phase)
        {
            _target = phase == GameManager.GamePhase.Night ? 1f : 0f;
        }

        private void Update()
        {
            if (transitionSeconds <= 0f) _nightFactor = _target;
            else _nightFactor = Mathf.MoveTowards(_nightFactor, _target, Time.deltaTime / transitionSeconds);
            Apply(_nightFactor);
        }

        private static float Ease(float t) { return t * t * (3f - 2f * t); }

        private void Apply(float nf)
        {
            float t = Ease(nf);
            Shader.SetGlobalFloat(NightFactorId, t);

            if (dayVolume != null) dayVolume.weight = 1f;
            if (nightVolume != null) nightVolume.weight = t;

            for (int i = 0; i < sunLights.Count; i++)
            {
                var l = sunLights[i]; if (l == null || i >= _sunBaseIntensity.Count) continue;
                l.intensity = _sunBaseIntensity[i] * daySunIntensityScale * (1f - t);
                l.color = Color.Lerp(_sunBaseColor[i], daySunColor, 0.4f);
                if (l.shadows != LightShadows.None) l.shadowStrength = Mathf.Lerp(0.85f, 0.4f, t);
            }
            if (moonLight != null)
            {
                moonLight.color = moonColor;
                moonLight.intensity = moonIntensity * t;
                moonLight.enabled = t > 0.01f;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(dayAmbient, nightAmbient, t);
            RenderSettings.ambientIntensity = Mathf.Lerp(dayAmbientIntensity, nightAmbientIntensity, t);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(dayFog, nightFog, t);
            RenderSettings.fogDensity = Mathf.Lerp(dayFogDensity, nightFogDensity, t);

            if (_camera == null) _camera = Camera.main;
            if (_camera != null) _camera.backgroundColor = Color.Lerp(dayBackground, nightBackground, t);
        }
    }
}
