using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastTruck
{
    /// <summary>
    /// 낮/밤 분위기 제어. GameManager.DuskProgress(낮이 끝나기 전 해질녘 구간)를 따라 천천히 밤이 된다.
    ///  - 한낮 → 노을(주황·분홍빛, 해가 낮아짐) → 해가 지며 햇빛/그림자가 서서히 사라짐 → 달빛과 달 그림자가 서서히 생김 → 밤
    ///  - URP는 그림자를 메인 방향광 하나만 그리므로, 해가 거의 졌을 때(햇빛 그림자가 0에 가까울 때) 메인 광원을 달로 넘겨서
    ///    그림자가 툭 바뀌지 않게 한다. 밤 → 낮(동틀녘)은 dawnSeconds 동안 돌아온다.
    ///  - 포스트 프로세싱: 낮(밝고 선명한 캐주얼) / 밤(푸른 톤, 강한 블룸, 비네트) Volume 두 개의 가중치를 섞는다
    ///  - 라이팅: 낮 태양 색/세기, 밤 달빛(청색 방향광), 주변광
    ///  - 안개/배경색, 캐릭터 셰이더용 전역 값(_GameNightFactor)
    /// 기획서 톤: "낮은 밝고, 밤은 약간 어둡게(완전한 암흑은 아님), 둥글고 단순화된 캐주얼 느낌".
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class AtmosphereController : MonoBehaviour
    {
        private static readonly int NightFactorId = Shader.PropertyToID("_GameNightFactor");
        private static readonly int WeatherFogId = Shader.PropertyToID("_GameWeatherFog");

        [Header("연동")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Volume dayVolume;
        [SerializeField] private Volume nightVolume;
        [Tooltip("낮에 켜지는 태양(방향광)들. 밤에는 세기가 0으로 줄어든다. 비워두면 씬의 모든 방향광을 자동 수집한다.")]
        [SerializeField] private List<Light> sunLights = new List<Light>();
        [Tooltip("밤에 켜지는 달빛 방향광. 없으면 자동 생성한다.")]
        [SerializeField] private Light moonLight;

        [Header("전환")]
        [Tooltip("연출 값이 해질녘 진행률을 따라가는 최대 속도 (진행률 1을 몇 초에). 해질녘 길이는 GameManager의 duskSeconds로 정한다.")]
        [SerializeField] private float transitionSeconds = 4f;
        [Tooltip("밤이 끝나고 다시 낮이 될 때(동틀녘) 걸리는 시간(초).")]
        [SerializeField] private float dawnSeconds = 10f;
        [Range(0f, 1f)] [SerializeField] private float startNightFactor = 0f;

        [Header("낮")]
        [SerializeField] private Color daySunColor = new Color(1f, 0.96f, 0.84f);
        [SerializeField] private float daySunIntensityScale = 0.95f;
        [SerializeField] private Color dayAmbient = new Color(0.34f, 0.36f, 0.40f);
        [SerializeField] private float dayAmbientIntensity = 0.8f;
        [SerializeField] private Color dayBackground = new Color(0.55f, 0.80f, 0.98f);
        [SerializeField] private Color dayFog = new Color(0.84f, 0.91f, 0.97f);
        [SerializeField] private float dayFogDensity = 0.0022f;

        [Header("노을 (해질녘 중간)")]
        [SerializeField] private Color sunsetSunColor = new Color(1f, 0.58f, 0.32f);
        [SerializeField] private Color sunsetAmbient = new Color(0.36f, 0.27f, 0.30f);
        [SerializeField] private Color sunsetBackground = new Color(0.96f, 0.58f, 0.44f);
        [SerializeField] private Color sunsetFog = new Color(0.93f, 0.66f, 0.52f);
        [SerializeField] private float sunsetFogDensity = 0.0032f;

        [Header("밤")]
        [SerializeField] private Color moonColor = new Color(0.52f, 0.62f, 1f);
        [SerializeField] private float moonIntensity = 0.8f;
        [SerializeField] private Color nightAmbient = new Color(0.12f, 0.16f, 0.32f);
        [SerializeField] private float nightAmbientIntensity = 0.55f;
        [SerializeField] private Color nightBackground = new Color(0.05f, 0.07f, 0.17f);
        [SerializeField] private Color nightFog = new Color(0.08f, 0.10f, 0.24f);
        [SerializeField] private float nightFogDensity = 0.0055f;

        [Header("날씨 (안개)")]
        [Tooltip("날씨 안개 정도 (0 맑음 ~ 1 짙은 안개). 나중에 날씨 시스템이 WeatherFog로 바꾼다.")]
        [Range(0f, 1f)] [SerializeField] private float weatherFog = 0f;
        [Tooltip("안개가 짙을 때 안개 밀도 배율.")]
        [SerializeField] private float weatherFogDensityScale = 4f;
        [Tooltip("안개가 짙을 때 해/달빛 세기 배율 (빛이 안개에 막힘).")]
        [Range(0f, 1f)] [SerializeField] private float weatherLightScale = 0.45f;
        [Tooltip("안개가 짙을 때 주변광 배율.")]
        [Range(0f, 1f)] [SerializeField] private float weatherAmbientScale = 0.7f;
        [Tooltip("짙은 안개 색 (낮). 밤에는 밤 안개 색과 섞여 더 어두워진다.")]
        [SerializeField] private Color weatherFogColor = new Color(0.62f, 0.66f, 0.70f);

        [Header("동굴 안 (낮)")]
        [Tooltip("낮에 내 캐릭터가 동굴 안에 있을 때 햇빛 세기 배율. 밤에는 원래 어두우므로 적용하지 않는다.")]
        [Range(0f, 1f)] [SerializeField] private float caveSunScale = 0.4f;
        [Tooltip("낮에 동굴 안에 있을 때 주변광 세기 배율.")]
        [Range(0f, 1f)] [SerializeField] private float caveAmbientScale = 0.6f;
        [Tooltip("동굴에 들어가고 나올 때 밝기가 바뀌는 시간(초).")]
        [SerializeField] private float caveFadeSeconds = 1.2f;
        [Tooltip("동굴 바닥 레이어. 발밑 바로 아래에 이 레이어가 있으면 동굴 안으로 본다 (TerrainStone = 8).")]
        [SerializeField] private LayerMask caveFloorMask = 1 << 8;
        [Tooltip("동굴 바닥을 잠깐 못 찾아도(받침목/종유석 위 등) 이 시간(초) 동안은 동굴 안으로 친다.")]
        [SerializeField] private float caveExitGrace = 0.6f;

        private float _caveFactor;       // 0 바깥 ~ 1 동굴 안
        private float _lastInCaveTime = -999f;
        private float _nextCaveCheck;
        private float _nextPlayerSearch;
        private Transform _localPlayer;

        private float _nightFactor;      // 현재 값 (0 낮 ~ 1 밤)
        private float _target;
        private Camera _camera;
        private readonly List<float> _sunBaseIntensity = new List<float>();
        private readonly List<Color> _sunBaseColor = new List<Color>();
        private Light _mainSun;          // 낮에 그림자를 그리는 해 (RenderSettings.sun)

        // 해질녘 단계 (진행률 기준)
        private const float SunsetPeak = 0.42f;      // 노을이 가장 진한 때
        private const float SunGoneStart = 0.45f;    // 햇빛이 줄기 시작
        private const float SunGoneEnd = 0.78f;      // 해가 완전히 짐
        private const float ShadowHandover = 0.74f;  // 그림자를 해 → 달로 넘기는 때

        /// <summary>해질녘 진행률 (0 한낮 ~ 1 밤). DayNightController가 해 각도를 맞춘다.</summary>
        public float NightFactor => _nightFactor;

        /// <summary>날씨 안개 (0 맑음 ~ 1 짙은 안개). 짙을수록 안개가 두꺼워지고 빛·주변광이 줄어 더 어두워진다.</summary>
        public float WeatherFog
        {
            get => weatherFog;
            set => weatherFog = Mathf.Clamp01(value);
        }

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
            Shader.SetGlobalFloat(WeatherFogId, 0f);
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
            // 그림자를 그리는 해 = 해질녘에 각도가 바뀌는 해(DayNightController). 그래야 노을 때 그림자가 해를 따라 길어진다.
            var rotatingSun = FindFirstObjectByType<DayNightController>();
            _mainSun = rotatingSun != null ? rotatingSun.GetComponent<Light>() : null;
            if (_mainSun == null && RenderSettings.sun != null && RenderSettings.sun != moonLight) _mainSun = RenderSettings.sun;
            if (_mainSun == null)
                foreach (var l in sunLights) if (l != null && l.shadows != LightShadows.None) { _mainSun = l; break; }
            EnsureMoon();

            if (gameManager != null)
                _target = _nightFactor = gameManager.DuskProgress;
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
            if (gameManager != null) _target = gameManager.DuskProgress;
            // 해질녘은 진행률 자체가 느리게 오르므로 그대로 따라가고, 동틀녘(값이 내려갈 때)은 dawnSeconds에 걸쳐 돌아온다
            float seconds = _target < _nightFactor ? dawnSeconds : transitionSeconds;
            if (seconds <= 0f) _nightFactor = _target;
            else _nightFactor = Mathf.MoveTowards(_nightFactor, _target, Time.deltaTime / seconds);

            float caveTarget = IsLocalPlayerInCave() ? 1f : 0f;
            _caveFactor = caveFadeSeconds <= 0f ? caveTarget : Mathf.MoveTowards(_caveFactor, caveTarget, Time.deltaTime / caveFadeSeconds);

            Apply(_nightFactor);
        }

        /// <summary>
        /// 내 캐릭터(입력을 받는 PlayerInteract가 켜진 캐릭터) 발밑 바로 아래에 동굴 바닥 레이어가 있으면 동굴 안.
        /// 동굴 바닥(돌바닥/동굴 호수/터널)은 TerrainStone 레이어, 바깥 땅은 Default 레이어라 이것으로 구분된다.
        /// </summary>
        private bool IsLocalPlayerInCave()
        {
            if (_localPlayer == null || !_localPlayer.gameObject.activeInHierarchy)
            {
                _localPlayer = null;
                if (Time.time >= _nextPlayerSearch)
                {
                    _nextPlayerSearch = Time.time + 1f;
                    foreach (var pi in FindObjectsByType<PlayerInteract>(FindObjectsSortMode.None))
                        if (pi.isActiveAndEnabled) { _localPlayer = pi.transform; break; }
                }
                if (_localPlayer == null)
                    return false;
            }

            if (Time.time >= _nextCaveCheck)
            {
                _nextCaveCheck = Time.time + 0.2f;
                if (Physics.Raycast(_localPlayer.position + Vector3.up * 0.5f, Vector3.down, 3f, caveFloorMask, QueryTriggerInteraction.Ignore))
                    _lastInCaveTime = Time.time;
            }
            return Time.time - _lastInCaveTime <= caveExitGrace;
        }

        private static float Ease(float t) { return t * t * (3f - 2f * t); }
        private static float Step(float a, float b, float x) => Ease(Mathf.Clamp01((x - a) / (b - a)));

        /// <summary>노을 세기 (0 → 노을 정점에서 1 → 해가 지면 0).</summary>
        private static float SunsetGlow(float s) => s <= SunsetPeak ? Step(0.05f, SunsetPeak, s) : 1f - Step(SunsetPeak, 0.9f, s);

        private void Apply(float nf)
        {
            float s = Mathf.Clamp01(nf);
            float t = Step(0.5f, 1f, s);          // 밤 정도 (달빛/밤 포스트/주변광)
            float glow = SunsetGlow(s);             // 노을
            float sunLeft = 1f - Step(SunGoneStart, SunGoneEnd, s);
            Shader.SetGlobalFloat(NightFactorId, t);
            float w = Mathf.Clamp01(weatherFog);
            Shader.SetGlobalFloat(WeatherFogId, w);
            float weatherLight = Mathf.Lerp(1f, weatherLightScale, w);

            if (dayVolume != null) dayVolume.weight = 1f;
            if (nightVolume != null) nightVolume.weight = t;

            // 낮에 동굴 안이면 햇빛/주변광을 줄인다 (밤에는 이미 어두우므로 낮 비율만큼만 적용)
            float cave = Ease(_caveFactor) * (1f - t);
            float sunCave = Mathf.Lerp(1f, caveSunScale, cave);
            float ambientCave = Mathf.Lerp(1f, caveAmbientScale, cave);

            // 해: 노을 색으로 물들며 낮아지고, 지평선 근처에서 빛과 그림자가 함께 옅어진다 (길어진 그림자가 갑자기 끊기지 않게)
            float sunShadow = 1f - Step(0.5f, ShadowHandover, s);
            for (int i = 0; i < sunLights.Count; i++)
            {
                var l = sunLights[i]; if (l == null || i >= _sunBaseIntensity.Count) continue;
                l.intensity = _sunBaseIntensity[i] * daySunIntensityScale * sunLeft * sunCave * weatherLight * Mathf.Lerp(1f, 0.85f, glow);
                Color dayCol = Color.Lerp(_sunBaseColor[i], daySunColor, 0.4f);
                l.color = Color.Lerp(dayCol, sunsetSunColor, glow);
                if (l.shadows != LightShadows.None) l.shadowStrength = 0.85f * sunShadow;
                l.enabled = sunLeft > 0.001f;
            }
            if (moonLight != null)
            {
                float moonUp = Step(0.62f, 1f, s);
                moonLight.color = moonColor;
                moonLight.intensity = moonIntensity * moonUp * weatherLight;
                moonLight.shadowStrength = 0.7f * Step(ShadowHandover, 0.95f, s);
                moonLight.enabled = moonUp > 0.01f;
                // 달은 낮게 떠서 천천히 올라온다 (그림자가 길었다가 짧아짐)
                moonLight.transform.rotation = Quaternion.Euler(Mathf.Lerp(18f, 52f, moonUp), 200f, 0f);
            }
            // 그림자를 그리는 메인 광원 넘기기: 두 그림자가 모두 거의 0인 순간에 바꾼다
            if (_mainSun != null && moonLight != null)
            {
                Light main = s < ShadowHandover ? _mainSun : moonLight;
                if (RenderSettings.sun != main) RenderSettings.sun = main;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            // Flat 모드는 ambientIntensity를 쓰지 않으므로 동굴 배율은 색에 곱한다
            Color ambient = Color.Lerp(Color.Lerp(dayAmbient, sunsetAmbient, glow), nightAmbient, t) * ambientCave * Mathf.Lerp(1f, weatherAmbientScale, w);
            ambient.a = 1f;
            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientIntensity = Mathf.Lerp(dayAmbientIntensity, nightAmbientIntensity, t);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            Color fogCol = Color.Lerp(Color.Lerp(dayFog, sunsetFog, glow), nightFog, t);
            // 짙은 안개: 낮엔 잿빛, 밤엔 밤 안개를 더 어둡게 (안개 낀 밤은 더 깜깜하다)
            Color weatherCol = Color.Lerp(weatherFogColor, nightFog * 0.6f, t);
            RenderSettings.fogColor = Color.Lerp(fogCol, weatherCol, w);
            RenderSettings.fogDensity = Mathf.Lerp(Mathf.Lerp(dayFogDensity, sunsetFogDensity, glow), nightFogDensity, t) * Mathf.Lerp(1f, weatherFogDensityScale, w);

            if (_camera == null) _camera = Camera.main;
            if (_camera != null) _camera.backgroundColor = Color.Lerp(Color.Lerp(Color.Lerp(dayBackground, sunsetBackground, glow), nightBackground, t), RenderSettings.fogColor, w * 0.8f);
        }
    }
}
