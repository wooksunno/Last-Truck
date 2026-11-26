using UnityEngine;

namespace LastTruck
{
    public enum WeatherKind { Clear = 0, Fog = 1, Rain = 2, Storm = 3 }

    /// <summary>
    /// 날씨: 맑음 / 짙은 안개 / 비 / 폭풍우(비 + 안개). 낮과 밤이 바뀔 때마다 새로 정해진다.
    ///
    ///  - 날씨는 (회차, 낮/밤)만으로 정해지는 규칙이라 멀티플레이에서도 모두 같은 날씨가 나온다 (동기화 불필요).
    ///  - 안개는 AtmosphereController.WeatherFog로 보낸다 (안개가 짙어지고 빛이 줄어 어두워진다).
    ///  - 비는 카메라를 따라다니는 파티클로 내린다.
    ///  - 랜턴이 가방에 있으면(HeldLantern.LitAmount) 안개가 걷혀 주변이 잘 보인다.
    /// </summary>
    public class WeatherDirector : MonoBehaviour
    {
        private static WeatherDirector _instance;

        /// <summary>지금 날씨.</summary>
        public static WeatherKind Kind { get { return _instance != null ? _instance._kind : WeatherKind.Clear; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("WeatherDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<WeatherDirector>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; _override = null; }

        // ================================================================ 날씨 키트 (개발/테스트용 수동 조작)

        private static WeatherKind? _override;

        /// <summary>수동으로 고른 날씨 (null = 자동). 내 화면에만 적용된다 (멀티플레이에서 다른 사람에게 보내지 않는다).</summary>
        public static WeatherKind? Override { get { return _override; } }

        /// <summary>날씨를 직접 정한다. null이면 자동(회차 규칙)으로 돌아간다.</summary>
        public static void SetOverride(WeatherKind? kind)
        {
            _override = kind;
            if (_instance != null && kind.HasValue) _instance.AnnounceKit(kind.Value);
            else if (_instance != null) HudToast.Show("날씨: 자동 (일차 규칙)");
        }

        private void AnnounceKit(WeatherKind kind)
        {
            HudToast.Show("날씨 키트: " + Label(kind) + (kind == WeatherKind.Clear ? "" : "  (" + PenaltyText(kind) + ")"));
        }

        // ================================================================ 날씨 패널티 (비/안개가 짙어지는 만큼 서서히 커진다)
        //   비      : 이동 -8%, 트럭 -12%
        //   안개    : 좀비가 가까이(약 36% 더 가까이)서 갑자기 나타난다 (랜턴으로 시야를 틔워도 좀비는 가까이서 나온다)
        //   폭풍우  : 이동 -14%, 트럭 -20%, 좀비 피해 +15%, 안개 패널티도 일부

        /// <summary>플레이어 이동 속도 배율 (비가 올수록 느려진다). 1 = 영향 없음.</summary>
        public static float PlayerSpeedScale { get { return _instance != null ? 1f - 0.14f * _instance._rain : 1f; } }

        /// <summary>트럭 최고 속도/가속 배율 (비 오는 길은 미끄럽다).</summary>
        public static float TruckSpeedScale { get { return _instance != null ? 1f - 0.20f * _instance._rain : 1f; } }

        /// <summary>좀비가 스폰되는 거리 배율 (안개 속에서는 가까이서 나타난다).</summary>
        public static float SpawnDistanceScale { get { return _instance != null ? 1f - 0.40f * _instance._fog : 1f; } }

        /// <summary>좀비 피해 배율 (폭풍우에서 +15%).</summary>
        public static float MonsterDamageScale { get { return Kind == WeatherKind.Storm && _instance != null ? 1f + 0.15f * Mathf.Clamp01(_instance._rain) : 1f; } }

        /// <summary>날씨별 패널티 한 줄 (없으면 빈 글).</summary>
        public static string PenaltyText(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Fog: return "패널티: 좀비가 가까이서 나타나요";
                case WeatherKind.Rain: return "패널티: 이동 -8% · 트럭 -12%";
                case WeatherKind.Storm: return "패널티: 이동 -14% · 트럭 -20% · 좀비 피해 +15%";
                default: return "";
            }
        }

        // ================================================================ 규칙

        /// <summary>회차/낮밤에 따른 날씨. 첫날(낮/밤)은 맑음 (아직 랜턴이 없고 첫 밤이 어렵다).</summary>
        public static WeatherKind KindFor(int cycle, bool night)
        {
            if (cycle <= 1) return WeatherKind.Clear;

            // 회차/낮밤으로 섞은 0~99 (고정 해시: 모든 화면이 같은 값)
            uint h = (uint)cycle * 2654435761u ^ (night ? 0x85EBCA6Bu : 0xC2B2AE35u);
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
            int roll = (int)(h % 100u);

            // 밤은 안개가 더 잦다
            int clear = night ? 32 : 50;
            int fog = night ? 30 : 20;
            int rain = 22;
            if (roll < clear) return WeatherKind.Clear;
            if (roll < clear + fog) return WeatherKind.Fog;
            if (roll < clear + fog + rain) return WeatherKind.Rain;
            return WeatherKind.Storm;
        }

        public static string Label(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Fog: return "짙은 안개";
                case WeatherKind.Rain: return "비";
                case WeatherKind.Storm: return "폭풍우";
                default: return "맑음";
            }
        }

        /// <summary>날씨별 한 줄 안내 (랜턴을 권한다).</summary>
        public static string Hint(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Fog: return "시야가 흐려요. 랜턴을 가방에 넣어 두면 걷혀요";
                case WeatherKind.Rain: return "비가 내려요. 랜턴이 가방에 있으면 멀리 보여요";
                case WeatherKind.Storm: return "시야가 아주 나빠요. 랜턴을 꼭 가방에 챙기세요";
                default: return "";
            }
        }

        private static float FogOf(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Fog: return 0.9f;
                case WeatherKind.Rain: return 0.3f;
                case WeatherKind.Storm: return 0.72f;
                default: return 0f;
            }
        }

        private static float RainOf(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Rain: return 0.6f;
                case WeatherKind.Storm: return 1f;
                default: return 0f;
            }
        }

        // ================================================================ 진행

        private WeatherKind _kind = WeatherKind.Clear;       // 지금 적용 중인 날씨 (키트로 고른 것 포함)
        private WeatherKind _autoKind = WeatherKind.Clear;   // 일차 규칙으로 정해진 날씨
        private float _fog, _rain;
        private int _lastCycle = -1;
        private bool _lastNight;
        private bool _announceReady;
        private AtmosphereController _atmosphere;
        private float _nextAtmosphereSearch;

        private ParticleSystem _rainFx;
        private Transform _rainRoot;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_atmosphere != null) { _atmosphere.WeatherFog = 0f; _atmosphere.LanternClear = 0f; }
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            WeatherKind target = WeatherKind.Clear;
            if (gm != null && !gm.IsWaitingForNetworkStart)
            {
                bool night = gm.CurrentPhase == GameManager.GamePhase.Night;
                target = KindFor(gm.CurrentCycle, night);

                // 날씨가 바뀐 순간 알린다 (게임 시작 첫 동기화에서는 알리지 않는다)
                if (gm.CurrentCycle != _lastCycle || night != _lastNight)
                {
                    bool first = _lastCycle < 0;
                    _lastCycle = gm.CurrentCycle;
                    _lastNight = night;
                    if (target != _autoKind)
                    {
                        _autoKind = target;
                        if (!first && _announceReady && target != WeatherKind.Clear)
                            HudToast.Show(Label(target) + " — " + Hint(target) + "  (" + PenaltyText(target) + ")");
                        else if (!first && _announceReady && target == WeatherKind.Clear)
                            HudToast.Show("날씨가 개었어요");
                    }
                    _announceReady = true;
                }
            }
            else
            {
                _autoKind = WeatherKind.Clear;
            }
            _kind = _autoKind;

            // 날씨 키트로 직접 고른 날씨가 있으면 그것이 우선 (게임이 시작된 뒤에만)
            if (_override.HasValue && gm != null && !gm.IsWaitingForNetworkStart) _kind = _override.Value;

            float dt = Time.unscaledDeltaTime;
            _fog = Mathf.MoveTowards(_fog, FogOf(_kind), dt / 8f);     // 8초에 걸쳐 서서히 끼고 걷힌다
            _rain = Mathf.MoveTowards(_rain, RainOf(_kind), dt / 5f);

            if (_atmosphere == null && Time.unscaledTime >= _nextAtmosphereSearch)
            {
                _nextAtmosphereSearch = Time.unscaledTime + 1f;
                _atmosphere = FindFirstObjectByType<AtmosphereController>();
            }
            if (_atmosphere != null)
            {
                _atmosphere.WeatherFog = _fog;
                _atmosphere.LanternClear = CraftingSystem.HeldLantern.LitAmount;
            }

            UpdateRain();
        }

        // ================================================================ 비

        private void EnsureRain()
        {
            if (_rainFx != null) return;

            var go = new GameObject("RainFx");
            _rainRoot = go.transform;
            _rainFx = go.AddComponent<ParticleSystem>();
            _rainFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _rainFx.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.85f;
            main.startSpeed = 0f;
            main.startSize = 0.028f;
            main.startColor = new Color(0.78f, 0.86f, 1f, 0.38f);
            main.maxParticles = 8000;

            // 카메라 주변 넓은 상자 모양 (납작한 판: 가로x세로 40m, 두께 2m). 이전에는 상자를 90도 눕혀서 세로 기둥 한 줄로만 내렸다.
            var shape = _rainFx.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.scale = new Vector3(36f, 2f, 36f);

            var vel = _rainFx.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(3f);
            vel.y = new ParticleSystem.MinMaxCurve(-34f);
            vel.z = new ParticleSystem.MinMaxCurve(1f);

            var renderer = _rainFx.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.022f;
            renderer.lengthScale = 1f;
            renderer.maxParticleSize = 0.01f;   // 카메라 바로 앞을 지나는 빗줄기가 굵은 막대처럼 보이지 않게 화면 비율로 상한을 둔다
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            renderer.sharedMaterial = new Material(shader);
        }

        private void UpdateRain()
        {
            if (_rain <= 0.01f)
            {
                if (_rainFx != null && _rainFx.isPlaying) _rainFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }

            EnsureRain();
            // 비는 카메라가 바라보는 땅 쪽을 중심으로, 카메라보다 높은 곳에서 내려온다 (어느 방향을 봐도 화면 전체에 내린다)
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 flat = cam.transform.forward; flat.y = 0f;
                flat = flat.sqrMagnitude > 0.001f ? flat.normalized : Vector3.forward;
                Vector3 p = cam.transform.position + flat * 7f;
                p.y = cam.transform.position.y + 8f;
                _rainRoot.position = p;
            }

            var emission = _rainFx.emission;
            emission.rateOverTime = 6000f * _rain;
            if (!_rainFx.isPlaying) _rainFx.Play();
        }
    }
}
