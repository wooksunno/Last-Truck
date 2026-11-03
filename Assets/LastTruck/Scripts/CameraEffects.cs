using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 달리기 / 트럭 운전 / 착지 카메라 연출 (몰입감).
    ///
    ///  달리기(Shift) 와 트럭 속도에 따라 부드럽게 커졌다 작아지는 "속도감 수준"(0~1)으로 다음을 같이 바꾼다:
    ///   - 시야각(FOV)이 넓어진다            - 카메라가 조금 멀어진다 (궤도 반지름)
    ///   - 잔잔한 카메라 흔들림(달릴 때 위아래 출렁임 + 미세 노이즈)
    ///   - 모션 블러 + 가장자리 어두워짐(비네트)    - 화면 가장자리에서 중심 밖으로 흐르는 속도선
    ///  점프 후 착지: 떨어진 속도에 비례해 화면이 쿵 눌렸다 올라오고(딥), 시야각이 잠깐 좁아졌다 돌아오며, 카메라가 떨린다.
    ///  트럭: 속도가 최대의 약 30%를 넘으면 효과가 시작되고, 위로 크게 떨어졌다 닿으면 착지 연출이 나온다.
    ///
    /// 씬에 따로 둘 것이 없다 (게임이 시작되면 자동으로 만들어진다). 시네머신 카메라(FreeLook)와 URP 볼륨을 쓰고,
    /// 필요한 컴포넌트/에셋이 없으면 그 효과만 조용히 건너뛴다. 수치는 인스펙터에서 조절한다.
    /// </summary>
    [DefaultExecutionOrder(1001)]   // 시네머신(그리고 CameraShake, 1000)이 카메라를 놓은 뒤에 더한다
    public class CameraEffects : MonoBehaviour
    {
        private const string FxSettingsPath = "UI/LastTruckFxPanelSettings";
        private const string UssPath = "UI/LastTruckHud";

        [Header("달리기 (Shift)")]
        [SerializeField] private float sprintFov = 5f;
        [SerializeField] private float sprintDistance = 0.6f;
        [SerializeField] private float sprintShake = 0.2f;
        [SerializeField] private float sprintBlur = 0.3f;
        [SerializeField] private float sprintLines = 0.35f;

        [Header("트럭 (최대 속도일 때)")]
        [SerializeField] private float truckFov = 12f;
        [SerializeField] private float truckDistance = 2f;
        [SerializeField] private float truckShake = 0.6f;
        [SerializeField] private float truckBlur = 0.8f;
        [SerializeField] private float truckLines = 0.9f;
        [Tooltip("트럭 최대 속도의 이 비율을 넘으면 효과가 시작된다")]
        [SerializeField] private float truckStartRatio = 0.3f;

        [Header("조준 (활 당기는 중)")]
        [Tooltip("활을 당길 때 시야각이 좁아지는 정도 (도). 과녁이 가까워 보인다")]
        [SerializeField] private float aimFov = 4f;
        [Tooltip("활을 당길 때 카메라가 캐릭터에게 다가가는 거리 (m)")]
        [SerializeField] private float aimDistance = 0.35f;
        [SerializeField] private float aimTime = 0.22f;

        [Header("부드러움 (초)")]
        [SerializeField] private float riseTime = 0.35f;
        [SerializeField] private float fallTime = 0.55f;

        [Header("착지")]
        [Tooltip("이보다 느리게 떨어져 닿으면 아무 일도 없다 (m/s)")]
        [SerializeField] private float landMinSpeed = 6f;
        [Tooltip("이 속도(m/s)로 떨어져 닿으면 가장 큰 연출")]
        [SerializeField] private float landMaxSpeed = 16f;
        [SerializeField] private float landDip = 0.12f;
        [SerializeField] private float landFovPunch = -1.5f;
        [Tooltip("착지 떨림. 크게 하면 화면이 덜덜 떨려 멀미가 난다")]
        [SerializeField] private float landShake = 0.03f;

        [Header("달릴 때 출렁임")]
        [SerializeField] private float bobFrequency = 9.5f;
        [SerializeField] private float bobHeight = 0.03f;
        [SerializeField] private float noisePosition = 0.015f;
        [SerializeField] private float noiseAngle = 0.15f;

        private static CameraEffects _instance;

        // 시네머신
        private CinemachineCamera _vcam;
        private CinemachineOrbitalFollow _orbital;
        private CameraTargetSmoother _smoother;
        private float _baseFov, _baseRadius;
        private bool _baseCaptured;

        // 대상
        private Transform _lastRealTarget;
        private Transform _lastRealParent;
        private TruckMove _truck;
        private Rigidbody _truckBody;
        private Vector3 _lastPos;
        private bool _hasLastPos;
        private float _speed;
        private float _prevTruckVy;

        // 수준(0~1, 부드럽게)
        private float _footLevel, _footVel;
        private SprintGate _sprintGate;
        private float _aimLevel, _aimVel;
        private static bool _aiming;
        private float _truckLevel, _truckVel;

        // 착지
        private float _pendingLandSpeed;
        private float _dip, _dipVel, _dipTarget, _dipTargetVel;
        private float _fovPunch, _fovPunchVel, _fovPunchTarget, _fovPunchTargetVel;
        private float _landHold;
        private const float LandPressSeconds = 0.09f;    // 눌리는 데 걸리는 시간
        private const float LandHoldSeconds = 0.05f;     // 눌린 채 머무는 시간
        private const float LandReleaseSeconds = 0.3f;   // 올라오는 데 걸리는 시간

        // 후처리 / 속도선
        private Volume _volume;
        private MotionBlur _motionBlur;
        private Vignette _vignette;
        private SpeedLines _lines;

        private float _noiseSeed;

        /// <summary>효과를 전부 끄고 싶을 때 (설정창 등에서 쓸 수 있게).</summary>
        public static bool Enabled = true;

        // ------------------------------------------------------------ 시작

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("CameraEffects");
            DontDestroyOnLoad(go);
            go.AddComponent<CameraEffects>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            Enabled = true;
            _aiming = false;
        }

        /// <summary>활을 당기는 동안 true: 시야각이 좁아지고 카메라가 살짝 다가간다 (놓으면 부드럽게 돌아온다).</summary>
        public static void SetAiming(bool aiming) => _aiming = aiming;

        /// <summary>내 캐릭터가 점프/낙하 후 땅에 닿았다 (impactSpeed = 닿기 직전 아래로 떨어지던 속도, m/s).</summary>
        public static void NotifyLanded(float impactSpeed)
        {
            if (_instance != null && impactSpeed > 0f)
                _instance._pendingLandSpeed = Mathf.Max(_instance._pendingLandSpeed, impactSpeed);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _noiseSeed = Random.value * 100f;
            BuildPostProcessing();
            BuildSpeedLines();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_volume != null) Destroy(_volume.gameObject);
        }

        private void BuildPostProcessing()
        {
            var go = new GameObject("CameraEffectsVolume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.weight = 0f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _motionBlur = profile.Add<MotionBlur>(true);
            _motionBlur.mode.Override(MotionBlurMode.CameraOnly);
            _motionBlur.quality.Override(MotionBlurQuality.Medium);
            _motionBlur.intensity.Override(0.45f);
            _motionBlur.clamp.Override(0.06f);

            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.34f);
            _vignette.smoothness.Override(0.55f);

            _volume.profile = profile;
        }

        private void BuildSpeedLines()
        {
            PanelSettings settings = Resources.Load<PanelSettings>(FxSettingsPath);
            StyleSheet uss = Resources.Load<StyleSheet>(UssPath);
            if (settings == null) return;   // UI 에셋이 없으면 속도선만 빠진다

            var go = new GameObject("UIDocument_SpeedLines");
            go.transform.SetParent(transform, false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;

            VisualElement root = doc.rootVisualElement;
            root.AddToClassList("lt-root");
            if (uss != null) root.styleSheets.Add(uss);
            root.pickingMode = PickingMode.Ignore;

            _lines = new SpeedLines();
            _lines.style.position = Position.Absolute;
            _lines.style.left = 0; _lines.style.top = 0; _lines.style.right = 0; _lines.style.bottom = 0;
            root.Add(_lines);
        }

        // ------------------------------------------------------------ 매 프레임

        private void Update()
        {
            if (!Enabled)
            {
                _footLevel = _truckLevel = 0f;
                ApplyPostProcessing(0f, 0f);
                return;
            }

            if (!ResolveCamera())
            {
                ApplyPostProcessing(0f, 0f);
                return;
            }

            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            UpdateTarget(dt);
            UpdateLevels(dt);
            UpdateLanding(dt);
            ApplyLens();
            ApplyPostProcessing(Mathf.Max(_footLevel * sprintBlur, _truckLevel * truckBlur), Mathf.Max(_footLevel * sprintLines, _truckLevel * truckLines));
        }

        private bool ResolveCamera()
        {
            if (_vcam != null && _vcam.isActiveAndEnabled) return true;

            _vcam = FindFirstObjectByType<CinemachineCamera>();
            if (_vcam == null) { _baseCaptured = false; return false; }

            _orbital = _vcam.GetComponent<CinemachineOrbitalFollow>();
            _smoother = _vcam.GetComponent<CameraTargetSmoother>();
            _baseFov = _vcam.Lens.FieldOfView;
            _baseRadius = _orbital != null ? _orbital.Radius : 0f;
            _baseCaptured = true;
            return true;
        }

        private void UpdateTarget(float dt)
        {
            Transform real = _smoother != null && _smoother.RealTarget != null ? _smoother.RealTarget : _vcam.Target.TrackingTarget;
            // 탑승하면 카메라 대상은 그대로(캐릭터)인 채로 캐릭터가 트럭의 자식이 된다 → 대상뿐 아니라 부모가 바뀔 때도 트럭인지 다시 확인한다
            Transform realParent = real != null ? real.parent : null;
            if (real != _lastRealTarget || realParent != _lastRealParent)
            {
                bool targetChanged = real != _lastRealTarget;
                _lastRealTarget = real;
                _lastRealParent = realParent;
                if (targetChanged) _hasLastPos = false;
                TruckMove truck = real != null ? real.GetComponentInParent<TruckMove>() : null;
                if (truck != _truck)
                {
                    _truck = truck;
                    _truckBody = _truck != null ? _truck.GetComponent<Rigidbody>() : null;
                    _prevTruckVy = 0f;
                }
            }

            if (real == null) { _speed = Mathf.Lerp(_speed, 0f, 8f * dt); return; }

            // 수평 속도 (화면 흔들림 없이 목표 위치 변화로 잰다)
            Vector3 p = real.position;
            if (_hasLastPos)
            {
                Vector3 d = p - _lastPos; d.y = 0f;
                float raw = d.magnitude / dt;
                if (raw < 80f)   // 탑승/텔레포트로 위치가 확 바뀐 프레임은 무시
                    _speed = Mathf.Lerp(_speed, raw, 10f * dt);
            }
            _lastPos = p;
            _hasLastPos = true;
        }

        private void UpdateLevels(float dt)
        {
            float footTarget = 0f, truckTarget = 0f;

            if (_truck != null)
            {
                float max = Mathf.Max(1f, _truck.maxSpeed);
                truckTarget = Mathf.Clamp01(Mathf.InverseLerp(max * truckStartRatio, max, _speed));
                truckTarget = truckTarget * truckTarget * (3f - 2f * truckTarget);   // smoothstep
            }
            else
            {
                // Shift 연타에도 FOV/속도선이 깜빡이지 않게 달리기 상태를 잠깐 유지한다
                if (_sprintGate.Update(PlayerStamina.Sprinting && _speed > 2f))
                    footTarget = 1f;
            }

            _footLevel = Mathf.SmoothDamp(_footLevel, footTarget, ref _footVel, footTarget > _footLevel ? riseTime : fallTime, Mathf.Infinity, dt);
            _truckLevel = Mathf.SmoothDamp(_truckLevel, truckTarget, ref _truckVel, truckTarget > _truckLevel ? riseTime : fallTime, Mathf.Infinity, dt);
            _aimLevel = Mathf.SmoothDamp(_aimLevel, _aiming && _truck == null ? 1f : 0f, ref _aimVel, aimTime, Mathf.Infinity, dt);
        }

        private void UpdateLanding(float dt)
        {
            // 트럭: 위로 뜬 뒤 땅에 닿는 순간(세로 속도가 크게 줄어듦)을 감지한다
            if (_truckBody != null)
            {
                float vy = _truckBody.linearVelocity.y;
                if (_prevTruckVy < -3.5f && vy - _prevTruckVy > 3.5f)
                    _pendingLandSpeed = Mathf.Max(_pendingLandSpeed, -_prevTruckVy);
                _prevTruckVy = vy;
            }

            if (_pendingLandSpeed > 0f)
            {
                float s = _pendingLandSpeed;
                _pendingLandSpeed = 0f;
                if (s >= landMinSpeed)
                {
                    float k = Mathf.Clamp01(Mathf.InverseLerp(landMinSpeed, landMaxSpeed, s));
                    k = 0.25f + 0.75f * k;
                    // 목표값만 바꾼다: 화면은 목표를 따라 부드럽게 내려갔다가 천천히 올라온다 (순간이동처럼 뚝 눌리면 멀미가 난다)
                    _dipTarget = Mathf.Max(_dipTarget, landDip * k);
                    _fovPunchTarget = Mathf.Min(_fovPunchTarget, landFovPunch * k);
                    _landHold = LandHoldSeconds;
                    Combat.CameraShake.Shake(landShake * k);
                }
            }

            // 눌린 상태를 잠깐 유지했다가 목표를 0으로 되돌린다
            if (_landHold > 0f) _landHold -= dt;
            else
            {
                _dipTarget = Mathf.SmoothDamp(_dipTarget, 0f, ref _dipTargetVel, LandReleaseSeconds, Mathf.Infinity, dt);
                _fovPunchTarget = Mathf.SmoothDamp(_fovPunchTarget, 0f, ref _fovPunchTargetVel, LandReleaseSeconds, Mathf.Infinity, dt);
            }

            _dip = Mathf.SmoothDamp(_dip, _dipTarget, ref _dipVel, LandPressSeconds, Mathf.Infinity, dt);
            _fovPunch = Mathf.SmoothDamp(_fovPunch, _fovPunchTarget, ref _fovPunchVel, LandPressSeconds * 1.5f, Mathf.Infinity, dt);
        }

        /// <summary>시야각과 카메라 거리 (시네머신이 이번 프레임에 읽는 값).</summary>
        private void ApplyLens()
        {
            if (!_baseCaptured) return;

            float fov = _baseFov + _footLevel * sprintFov + _truckLevel * truckFov + _fovPunch - _aimLevel * aimFov;
            LensSettings lens = _vcam.Lens;
            if (!Mathf.Approximately(lens.FieldOfView, fov))
            {
                lens.FieldOfView = fov;
                _vcam.Lens = lens;
            }

            if (_orbital != null)
            {
                float r = _baseRadius + _footLevel * sprintDistance + _truckLevel * truckDistance - _aimLevel * aimDistance;
                if (!Mathf.Approximately(_orbital.Radius, r)) _orbital.Radius = r;
            }
        }

        private void ApplyPostProcessing(float blurLevel, float linesLevel)
        {
            float w = Mathf.Clamp01(blurLevel);
            if (_volume != null) _volume.weight = w;
            if (_lines != null) _lines.SetIntensity(linesLevel);
        }

        // 시네머신이 카메라를 놓은 뒤: 착지 딥 + 달릴 때 잔잔한 출렁임
        private void LateUpdate()
        {
            if (!Enabled || !_baseCaptured) return;

            Transform cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null) return;

            // 착지 딥: 화면이 아래로 눌렸다 올라온다
            if (_dip > 0.0005f)
                cam.position -= cam.up * _dip;

            float shake = _footLevel * sprintShake + _truckLevel * truckShake;
            if (shake > 0.01f)
            {
                float t = Time.unscaledTime;
                // 달리기: 발걸음에 맞춘 위아래 출렁임 / 트럭: 빠른 잔진동
                float bobF = _truck != null ? 18f : bobFrequency;
                float bob = Mathf.Sin(t * bobF) * bobHeight * shake;
                float nx = (Mathf.PerlinNoise(t * 11f, _noiseSeed) - 0.5f) * 2f;
                float ny = (Mathf.PerlinNoise(t * 11f, _noiseSeed + 31f) - 0.5f) * 2f;
                float nr = (Mathf.PerlinNoise(t * 9f, _noiseSeed + 67f) - 0.5f) * 2f;

                cam.position += cam.up * bob + cam.right * (nx * noisePosition * shake) + cam.up * (ny * noisePosition * shake * 0.6f);
                cam.rotation *= Quaternion.Euler(ny * noiseAngle * shake, nx * noiseAngle * shake, nr * noiseAngle * shake * 1.4f);
            }
        }

        // ------------------------------------------------------------ 속도선

        /// <summary>
        /// 화면 가장자리에서 중심 바깥쪽으로 흘러가는 가는 삼각형 줄무늬 (UI Toolkit Painter2D).
        /// 가운데 부분은 비워서 캐릭터/트럭을 가리지 않는다.
        /// </summary>
        private class SpeedLines : VisualElement
        {
            private const int Count = 34;
            private readonly float[] _angle = new float[Count];
            private readonly float[] _phase = new float[Count];
            private readonly float[] _width = new float[Count];
            private readonly float[] _length = new float[Count];
            private readonly float[] _rate = new float[Count];
            private float _intensity;
            private float _nextReseed;

            public SpeedLines()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Paint;
                for (int i = 0; i < Count; i++) Reseed(i, true);
                style.display = DisplayStyle.None;
            }

            public void SetIntensity(float value)
            {
                value = Mathf.Clamp01(value);
                _intensity = value;
                bool show = value > 0.02f;
                style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show) return;

                // 줄무늬 몇 개씩 번갈아 새로 뽑아서 화면이 살아 있게 한다
                if (Time.unscaledTime >= _nextReseed)
                {
                    _nextReseed = Time.unscaledTime + 0.06f;
                    for (int k = 0; k < 4; k++) Reseed(Random.Range(0, Count), false);
                }
                MarkDirtyRepaint();
            }

            private void Reseed(int i, bool first)
            {
                _angle[i] = Random.value * Mathf.PI * 2f;
                _phase[i] = first ? Random.value : _phase[i];
                _width[i] = Random.Range(0.006f, 0.02f);
                _length[i] = Random.Range(0.12f, 0.34f);
                _rate[i] = Random.Range(1.6f, 3.2f);
            }

            private void Paint(MeshGenerationContext ctx)
            {
                if (_intensity <= 0.02f) return;

                float w = resolvedStyle.width, h = resolvedStyle.height;
                if (w < 4f || h < 4f) return;

                Painter2D p = ctx.painter2D;
                Vector2 c = new Vector2(w * 0.5f, h * 0.5f);
                float half = Mathf.Sqrt(w * w + h * h) * 0.5f;
                float clearR = Mathf.Min(w, h) * 0.30f;     // 가운데는 비워 둔다
                float t = Time.unscaledTime;

                for (int i = 0; i < Count; i++)
                {
                    float frac = Mathf.Repeat(t * _rate[i] * 0.55f + _phase[i], 1f);
                    float r0 = Mathf.Lerp(clearR, half * 0.92f, frac);
                    float r1 = Mathf.Min(r0 + half * _length[i] * (0.4f + frac), half * 1.05f);
                    float alpha = Mathf.Sin(frac * Mathf.PI) * _intensity * 0.5f;
                    if (alpha < 0.01f) continue;

                    Vector2 dir = new Vector2(Mathf.Cos(_angle[i]), Mathf.Sin(_angle[i]));
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    float halfWidth = half * _width[i] * (0.4f + frac);

                    p.fillColor = new Color(1f, 1f, 1f, alpha);
                    p.BeginPath();
                    p.MoveTo(c + dir * r0);
                    p.LineTo(c + dir * r1 + perp * halfWidth);
                    p.LineTo(c + dir * r1 - perp * halfWidth);
                    p.ClosePath();
                    p.Fill();
                }
            }
        }
    }
}
