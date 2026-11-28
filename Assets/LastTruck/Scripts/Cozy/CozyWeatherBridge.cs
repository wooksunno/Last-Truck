using System.Collections.Generic;
using DistantLands.Cozy;
using DistantLands.Cozy.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastTruck
{
    /// <summary>
    /// COZY 3 날씨·낮밤 연출 연결. 프리팹(Assets/LastTruck/Prefabs/CozyWeatherBridge.prefab)을 넣은 씬에서만 동작한다.
    /// 날씨를 정하는 규칙·패널티·멀티 동기화는 WeatherDirector가 맡고, 이 스크립트는 그리기만 COZY에 넘긴다.
    ///  - Use Cozy를 끄면 원래 방식(AtmosphereController·DayNightController)으로 돌아간다 (문제가 생겼을 때 비상 스위치).
    ///
    ///  - 플레이가 시작되면 COZY 프리팹(Resources "Cozy Prefabs/Cozy Weather Sphere")을 만들어 DontDestroyOnLoad로 옮긴다.
    ///    씬에 미리 넣지 않는 이유: COZY는 에디터에서도 돌며 씬 조명 설정을 바꿔 저장하고, 씬에 있으면 WorldChunkCuller가
    ///    COZY 하늘/비를 고정 물체로 등록해 멀어지면 숨긴다.
    ///  - 시간·날씨는 우리 코드가 정한다: COZY 시계는 멈추고(프로필 복제본), 매 프레임 CozyDayClock 시각과 WeatherDirector 날씨를 넣는다.
    ///  - COZY 켬 동안 우리 해·달(방향광), AtmosphereController, DayNightController, 낮·밤 Volume, PastelLook, 우리 비 파티클을 끄고,
    ///    우리 셰이더의 안개 값(_GameWeatherFog)은 0, 밤 값(_GameNightFactor)은 ourNightOnTop(기본 켬)일 때만 얹는다.
    ///    끄면(F12 또는 씬을 떠날 때) 전부 되돌린다. 동굴 전용 보조광(렌더링 레이어 2)은 건드리지 않는다.
    ///  - F12: COZY ↔ 원래 방식 전환 (개발용 — 에디터·개발 빌드에서만, 정식 빌드에서는 키가 먹지 않는다).
    /// </summary>
    [DefaultExecutionOrder(-50)]   // COZY Update보다 먼저 시각을 넣는다
    public partial class CozyWeatherBridge : MonoBehaviour   // 조명 보정: .Lighting.cs, 환경 연출 규칙: .Ambience.cs
    {
        [SerializeField] private bool useCozy = true;
        [Tooltip("AtmosphereController가 쓰는 밤 포스트 프로세싱 Volume (COZY 켬 동안 끈다).")]
        [SerializeField] private Volume nightVolume;
        [Tooltip("AtmosphereController가 쓰는 낮(기본 색감) 포스트 프로세싱 Volume (COZY 켬 동안 끈다 — COZY 원래 색 그대로 비교).")]
        [SerializeField] private Volume dayVolume;
        [Tooltip("켜면 COZY 밤 위에 우리 셰이더의 밤 표현(_GameNightFactor: 그림자 어둡게, 캐릭터 테두리 빛)을 얹는다. 끄면 COZY 빛만 (비교용).")]
        [SerializeField] private bool ourNightOnTop = true;   // 기본: COZY 밤 + 우리 셰이더 밤 표현(좀비 테두리 등이 잘 보이게)

        [Header("우리 날씨 → COZY 날씨 프로필")]
        [SerializeField] private WeatherProfile clearProfile;
        [SerializeField] private WeatherProfile fogProfile;
        [SerializeField] private WeatherProfile rainProfile;
        [SerializeField] private WeatherProfile stormProfile;
        [Tooltip("눈 (넣어 두면 이 씬에서 눈 날씨가 나온다 — WeatherDirector.SnowAvailable).")]
        [SerializeField] private WeatherProfile snowProfile;

        [Header("시간")]
        [SerializeField] private float weatherTransitionSeconds = 6f;
        [SerializeField] private float dawnSeconds = 10f;
        [SerializeField] private float secondsPerNightHour = 30f;

        private const string PrefabPath = "Cozy Prefabs/Cozy Weather Sphere";
        private const KeyCode ToggleKey = KeyCode.F12;   // F10·F11은 AbilityDebugOverlay가 쓴다
        private const int CaveOnlyLightLayer = 2;   // AtmosphereController와 같은 규칙: 동굴 전용 보조광은 건드리지 않는다
        private static readonly int NightFactorId = Shader.PropertyToID("_GameNightFactor");
        private static readonly int WeatherFogId = Shader.PropertyToID("_GameWeatherFog");

        public bool CozyOn { get; private set; }
        public CozyWeather Cozy { get; private set; }

        private GameObject _holder;
        private CozyTimeModule _time;
        private CozyWeatherModule _weather;
        private PerennialProfile _profileCopy;
        private CozyDayClock _clock;
        private AtmosphereController _atmosphere;
        private DayNightController _dayNight;
        private readonly List<Light> _ourLights = new List<Light>();
        private readonly List<bool> _ourLightsWasOn = new List<bool>();
        private bool _nightVolumeWasOn;
        private bool _dayVolumeWasOn;
        private bool _pastelWasOn;
        private bool _weatherPending;
        private WeatherKind _appliedKind;
        private RenderSnapshot _render;

        private void Start()
        {
            _clock = new CozyDayClock(dawnSeconds, secondsPerNightHour);
            _atmosphere = FindFirstObjectByType<AtmosphereController>();
            _dayNight = FindFirstObjectByType<DayNightController>();
            if (_atmosphere != null)   // 프리팹에는 씬 Volume을 넣을 수 없어서, 비어 있으면 그 씬 AtmosphereController의 것을 쓴다
            {
                if (nightVolume == null) nightVolume = _atmosphere.NightVolume;
                if (dayVolume == null) dayVolume = _atmosphere.DayVolume;
            }

            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[CozyWeatherBridge] COZY 프리팹을 찾지 못했습니다: Resources/" + PrefabPath);
                enabled = false;
                return;
            }

            // 꺼진 부모 아래에 만들어서, 우리 상태를 저장하기 전에 COZY가 먼저 조명을 바꾸지 않게 한다
            _holder = new GameObject("COZY (CozyWeatherBridge)");
            _holder.SetActive(false);
            DontDestroyOnLoad(_holder);
            Cozy = Instantiate(prefab, _holder.transform).GetComponent<CozyWeather>();
            _time = Cozy.GetComponentInChildren<CozyTimeModule>(true);
            _weather = Cozy.GetComponentInChildren<CozyWeatherModule>(true);

            // COZY 시계는 멈춘다 (원본 프로필 에셋은 건드리지 않고 복제본에)
            if (_time != null && _time.perennialProfile != null)
            {
                _profileCopy = Instantiate(_time.perennialProfile);
                _profileCopy.pauseTime = true;
                _time.perennialProfile = _profileCopy;
            }
            if (_weather != null && _weather.ecosystem != null)
                _weather.ecosystem.weatherSelectionMode = CozyEcosystem.EcosystemStyle.manual;
            if (Cozy.cozyCamera == null) Cozy.cozyCamera = Camera.main;

            WeatherDirector.SnowAvailable = snowProfile != null;   // 눈을 그릴 수 있는 씬에서만 눈 날씨가 나온다
            HookLighting();
            SetCozy(useCozy);
        }

        private void OnDestroy()
        {
            UnhookLighting();
            WeatherDirector.SnowAvailable = false;
            if (CozyOn) SetCozy(false);
            if (_holder != null) Destroy(_holder);
            if (_profileCopy != null) Destroy(_profileCopy);
        }

        /// <summary>COZY 켜기/끄기. 끄면 우리 날씨·낮밤 연출을 원래 상태로 되돌린다.</summary>
        public void SetCozy(bool on)
        {
            if (Cozy == null || on == CozyOn) return;

            if (on)
            {
                _render = RenderSnapshot.Capture();
                if (_atmosphere != null) _atmosphere.enabled = false;
                if (_dayNight != null) _dayNight.enabled = false;
                CollectOurLights();
                _ourLightsWasOn.Clear();
                foreach (Light l in _ourLights)
                {
                    _ourLightsWasOn.Add(l != null && l.enabled);
                    if (l != null) l.enabled = false;
                }
                if (nightVolume != null) { _nightVolumeWasOn = nightVolume.enabled; nightVolume.enabled = false; }
                if (dayVolume != null) { _dayVolumeWasOn = dayVolume.enabled; dayVolume.enabled = false; }
                _pastelWasOn = PastelLook.Enabled;
                PastelLook.Enabled = false;   // Toggle()은 알림을 띄우므로 값만 바꾼다
                WeatherDirector.VisualsSuppressed = true;
                Shader.SetGlobalFloat(WeatherFogId, 0f);

                _holder.SetActive(true);
                _weatherPending = true;   // 첫 Update에서 지금 날씨를 전환 없이 바로 적용
                CozyOn = true;
            }
            else
            {
                _holder.SetActive(false);
                WeatherDirector.VisualsSuppressed = false;
                PastelLook.Enabled = _pastelWasOn;
                if (nightVolume != null) nightVolume.enabled = _nightVolumeWasOn;
                if (dayVolume != null) dayVolume.enabled = _dayVolumeWasOn;
                for (int i = 0; i < _ourLights.Count && i < _ourLightsWasOn.Count; i++)
                    if (_ourLights[i] != null) _ourLights[i].enabled = _ourLightsWasOn[i];
                if (_dayNight != null) _dayNight.enabled = true;
                if (_atmosphere != null) _atmosphere.enabled = true;
                _render.Restore();
                CozyOn = false;
            }
        }

        // 켤 때마다 다시 모은다: AtmosphereController가 달빛(MoonLight)을 자기 Start에서 만들어서, 처음엔 없을 수 있다.
        // 꺼진 오브젝트도 포함하고, COZY 빛은 다른 씬(DontDestroyOnLoad)이라 빠진다. 동굴 전용 보조광은 건드리지 않는다.
        private void CollectOurLights()
        {
            _ourLights.Clear();
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.renderingLayerMask != CaveOnlyLightLayer && l.gameObject.scene == gameObject.scene) _ourLights.Add(l);
        }

        private void Update()
        {
            if (Debug.isDebugBuild && Input.GetKeyDown(ToggleKey))
            {
                SetCozy(!CozyOn);
                HudToast.Show(CozyOn ? "개발용 F12: COZY 날씨" : "개발용 F12: 원래 방식 날씨");
            }
            if (!CozyOn) return;

            GameManager gm = GameManager.Instance;
            bool hasGame = gm != null && !gm.IsWaitingForNetworkStart;
            bool night = hasGame && gm.CurrentPhase == GameManager.GamePhase.Night;
            float dayLength = hasGame ? gm.DayDurationSeconds : 0f;
            float dayElapsed = dayLength > 0f ? 1f - gm.DayTimeRemaining / dayLength : 0f;
            float t = _clock.Step(hasGame, night, hasGame ? gm.DuskProgress : 0f, dayElapsed, Time.deltaTime);
            if (_time != null) _time.currentTime = t;
            // ourNightOnTop(기본 켬): COZY 밤 위에 우리 셰이더 밤 표현을 얹는다. 끄면 0 (COZY 빛만)
            Shader.SetGlobalFloat(NightFactorId, ourNightOnTop ? _clock.NightFactor : 0f);

            WeatherKind kind = WeatherDirector.Kind;
            if (_weatherPending || kind != _appliedKind)
            {
                WeatherProfile profile = ProfileFor(kind);
                if (profile != null && _weather != null && _weather.ecosystem != null)
                    _weather.ecosystem.SetWeather(profile, _weatherPending ? 0f : weatherTransitionSeconds);
                _appliedKind = kind;
                _weatherPending = false;
            }

            UpdateAmbience(hasGame ? gm.CurrentCycle : 0, kind);
        }

        private WeatherProfile ProfileFor(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Fog: return fogProfile;
                case WeatherKind.Rain: return rainProfile;
                case WeatherKind.Storm: return stormProfile;
                case WeatherKind.Snow: return snowProfile;
                default: return clearProfile;
            }
        }

        /// <summary>COZY가 바꾸는 RenderSettings를 켜기 전에 저장해 두고 끌 때 되돌린다.</summary>
        private struct RenderSnapshot
        {
            private AmbientMode _ambientMode;
            private Color _sky, _equator, _ground;
            private float _ambientIntensity;
            private bool _fog;
            private Color _fogColor;
            private float _fogDensity;
            private FogMode _fogMode;
            private Light _sun;
            private DefaultReflectionMode _reflectionMode;
            private Material _skybox;

            public static RenderSnapshot Capture()
            {
                return new RenderSnapshot
                {
                    _ambientMode = RenderSettings.ambientMode,
                    _sky = RenderSettings.ambientSkyColor,
                    _equator = RenderSettings.ambientEquatorColor,
                    _ground = RenderSettings.ambientGroundColor,
                    _ambientIntensity = RenderSettings.ambientIntensity,
                    _fog = RenderSettings.fog,
                    _fogColor = RenderSettings.fogColor,
                    _fogDensity = RenderSettings.fogDensity,
                    _fogMode = RenderSettings.fogMode,
                    _sun = RenderSettings.sun,
                    _reflectionMode = RenderSettings.defaultReflectionMode,
                    _skybox = RenderSettings.skybox,
                };
            }

            public void Restore()
            {
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientSkyColor = _sky;
                RenderSettings.ambientEquatorColor = _equator;
                RenderSettings.ambientGroundColor = _ground;
                RenderSettings.ambientIntensity = _ambientIntensity;
                RenderSettings.fog = _fog;
                RenderSettings.fogColor = _fogColor;
                RenderSettings.fogDensity = _fogDensity;
                RenderSettings.fogMode = _fogMode;
                RenderSettings.sun = _sun;
                RenderSettings.defaultReflectionMode = _reflectionMode;
                RenderSettings.skybox = _skybox;
            }
        }
    }
}
