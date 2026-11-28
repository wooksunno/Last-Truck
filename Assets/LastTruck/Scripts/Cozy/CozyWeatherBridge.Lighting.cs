using CraftingSystem;
using DistantLands.Cozy;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastTruck
{
    /// <summary>
    /// COZY 켬 동안 COZY 조명 위에 얹는 보정 3가지 (COZY에 없는 우리 게임 기능).
    ///  - 밤 밝기: COZY 밤은 달빛 색이 검정이라(주변광만으로 밝힘) 우리 툰 셰이더에서 땅이 거의 검게 나온다.
    ///    밤 정도만큼 COZY 달빛을 원래 방식의 달빛(색·세기)으로 바꾸고, 주변광을 조금 올린다.
    ///  - 동굴: AtmosphereController와 같은 규칙. 낮에 동굴 바닥(caveFloorMask) 위에 있으면 햇빛·주변광을 줄인다.
    ///  - 랜턴: 가방에 랜턴이 있으면(HeldLantern.LitAmount) COZY 안개를 lanternFogClear만큼 걷는다.
    ///
    /// COZY가 쓴 값 위에 곱하면 프레임마다 쌓일 수 있어서, 렌더 직전(beginContextRendering)에
    /// COZY 자신의 공개 값(주변광 색·안개 농도 등)으로 최종 값을 매번 새로 계산해 쓴다.
    /// </summary>
    public partial class CozyWeatherBridge
    {
        [Header("밤 밝기 (COZY 밤 위에 얹는 값)")]
        [Tooltip("밤일 때 COZY 달빛 색. 원래 방식(AtmosphereController)의 달빛 색과 같다.")]
        [SerializeField] private Color nightMoonColor = new Color(0.52f, 0.62f, 1f, 1f);
        [Tooltip("밤일 때 COZY 달빛 세기. 원래 방식의 달빛 세기와 같다.")]
        [SerializeField] private float nightMoonIntensity = 0.8f;
        [Tooltip("밤 정도(0~1)만큼 COZY 주변광(하늘·지평선·땅)에 더하는 색. 밤 땅 밝기를 원래 방식과 비슷하게 맞춘 값.")]
        [SerializeField] private Color nightAmbientLift = new Color(0.10f, 0.12f, 0.24f, 0f);
        [Tooltip("원래 방식의 밤 Volume(푸른 톤·블룸)을 밤 정도 × 이 비율로 약하게 섞는다. 0이면 섞지 않음.")]
        [Range(0f, 1f)] [SerializeField] private float nightVolumeBlend = 0.35f;

        [Header("동굴 (AtmosphereController와 같은 값)")]
        [Range(0f, 1f)] [SerializeField] private float caveSunScale = 0.4f;
        [Range(0f, 1f)] [SerializeField] private float caveAmbientScale = 0.6f;
        [SerializeField] private float caveFadeSeconds = 1.2f;
        [SerializeField] private LayerMask caveFloorMask = 1 << 8;
        [SerializeField] private float caveExitGrace = 0.6f;

        [Header("랜턴")]
        [Tooltip("가방에 랜턴이 있으면 COZY 안개를 이 비율만큼 걷는다 (원래 방식과 같은 62%).")]
        [Range(0f, 1f)] [SerializeField] private float lanternFogClear = 0.62f;

        private Light _cozySun, _cozyMoon;
        private float _sunBase = -1f, _moonBase = -1f;   // COZY 해·달 세기 (COZY는 세기는 고정하고 색으로 밝기를 바꾼다)
        private float _caveFactor, _lastInCaveTime = -999f, _nextCaveCheck, _nextPlayerSearch;
        private Transform _localPlayer;

        /// <summary>지금 동굴 안 정도 (0 바깥 ~ 1 동굴 안).</summary>
        public float CaveFactor => _caveFactor;

        private void HookLighting()
        {
            RenderPipelineManager.beginContextRendering -= OnBeginRender;
            RenderPipelineManager.beginContextRendering += OnBeginRender;
        }

        private void UnhookLighting()
        {
            RenderPipelineManager.beginContextRendering -= OnBeginRender;
        }

        private void OnBeginRender(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras)
        {
            if (!CozyOn || Cozy == null || _clock == null) return;
            if (_cozySun == null || _cozyMoon == null)
            {
                foreach (var l in Cozy.GetComponentsInChildren<Light>(true))
                {
                    if (l.name == "Sun Light") _cozySun = l;
                    else if (l.name == "Moon Light") _cozyMoon = l;
                }
            }

            float night = _clock.NightFactor;
            float cave = Ease(UpdateCave()) * (1f - night);   // 동굴 보정은 낮에만 (원래 방식과 같음)

            if (nightVolume != null)   // 밤 푸른 톤: 원래 밤 Volume을 약하게 (COZY 켬 동안은 이 스크립트가 가중치를 정한다)
            {
                float w = nightVolumeBlend * night;
                bool on = w > 0.001f;
                if (nightVolume.enabled != on) nightVolume.enabled = on;
                nightVolume.weight = w;
            }

            if (_cozySun != null)
            {
                if (_sunBase < 0f) _sunBase = _cozySun.intensity;
                _cozySun.intensity = _sunBase * Mathf.Lerp(1f, caveSunScale, cave);
            }
            if (_cozyMoon != null)
            {
                if (_moonBase < 0f) _moonBase = _cozyMoon.intensity;
                _cozyMoon.color = Color.Lerp(Cozy.moonlightColor * Cozy.sunFilter, nightMoonColor, night);
                _cozyMoon.intensity = Mathf.Lerp(_moonBase, nightMoonIntensity, night);
            }

            if (Cozy.handleSceneLighting)   // COZY가 쓰는 식과 같게 다시 계산 (CozyWeather 주변광)
            {
                float m = Cozy.ambientLightMultiplier, cloud = 1f - Cozy.cumulus / 2f;
                float ak = Mathf.Lerp(1f, caveAmbientScale, cave);
                Color lift = nightAmbientLift * night; lift.a = 0f;
                // COZY는 Trilight를 켤 때 한 번만 정한다. COZY를 껐다 켜면 원래 방식(Flat)이 남아 있어서 매번 맞춘다.
                if (RenderSettings.ambientMode != AmbientMode.Trilight) RenderSettings.ambientMode = AmbientMode.Trilight;
                bool unityFog = Cozy.fogStyle == CozyWeather.FogStyle.unity;   // COZY도 켤 때 한 번만 정한다
                if (RenderSettings.fog != unityFog) RenderSettings.fog = unityFog;
                RenderSettings.ambientSkyColor = Cozy.ambientLightZenithColor * m * ak + lift;
                RenderSettings.ambientEquatorColor = Cozy.ambientLightHorizonColor * cloud * m * ak + lift;
                RenderSettings.ambientGroundColor = Cozy.ambientLightHorizonColor * Color.gray * cloud * m * ak + lift;
            }

            float keep = 1f - lanternFogClear * Mathf.Clamp01(HeldLantern.LitAmount);
            Shader.SetGlobalFloat(CozyShaderIDs.CZY_FogDepthMultiplierID, Cozy.fogDensityMultiplier * Cozy.fogDensity * keep);
        }

        private float UpdateCave()
        {
            float target = IsLocalPlayerInCave() ? 1f : 0f;
            _caveFactor = caveFadeSeconds <= 0f ? target : Mathf.MoveTowards(_caveFactor, target, Time.deltaTime / caveFadeSeconds);
            return _caveFactor;
        }

        // AtmosphereController.IsLocalPlayerInCave와 같은 방식: 발밑 3m 안에 동굴 바닥 레이어가 있으면 동굴 안
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
                if (_localPlayer == null) return false;
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
    }
}
