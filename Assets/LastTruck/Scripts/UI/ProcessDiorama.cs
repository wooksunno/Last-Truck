using System.Collections.Generic;
using CraftingSystem;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastTruck
{
    /// <summary>
    /// 가공 화면 가운데의 "움직이는 3D 미니 장면".
    /// 설비별 장면 프리팹(Resources/ProcessDiorama/Stage_설비이름)을 맵 아래 먼 곳에 띄워 두고, 전용 카메라로 찍은 화면(RenderTexture)을 UI에 넘긴다.
    /// 진짜 모델과 파티클(불, 불똥, 연기, 거품)이 계속 움직이고, 재료 아이콘이 설비 위에서 구워지고/갈리고/눌린다.
    ///
    /// 낮/밤과 상관없이 같은 모습: 이 카메라가 찍는 동안만 해·달을 끄고 장면 전용 빛과 주변광으로 바꿨다가, 다 찍으면 되돌린다.
    /// 가공 창이 닫히면 카메라도 꺼져서 비용이 없다.
    /// </summary>
    public class ProcessDiorama : MonoBehaviour
    {
        private const string StagePath = "ProcessDiorama/Stage_";
        private static readonly Vector3 RootPosition = new Vector3(0f, -4000f, 0f);
        private const int RtWidth = 900, RtHeight = 1160;

        private static ProcessDiorama _inst;
        private static readonly Dictionary<FacilityType, bool> Availability = new Dictionary<FacilityType, bool>();

        private Transform _root;
        private Camera _cam;
        private RenderTexture _rt;
        private readonly Dictionary<FacilityType, ProcessDioramaStage> _stages = new Dictionary<FacilityType, ProcessDioramaStage>();
        private ProcessDioramaStage _current;
        private SpriteRenderer _item, _output;
        private int _state;
        private float _progress;
        private float _stateSince;
        private ItemData _shownItem;

        // 찍는 동안 바꿨다가 되돌릴 것들
        private readonly List<Light> _worldDirectional = new List<Light>();
        private readonly List<Light> _disabled = new List<Light>();
        private float _nextLightScan;
        private Light _savedSun;
        private Color _savedAmbient;
        private AmbientMode _savedAmbientMode;
        private SphericalHarmonicsL2 _savedProbe;
        private bool _savedFog;
        private float _savedNight;
        private Vector4 _savedHaze;
        private bool _overriding;
        private static readonly int NightId = Shader.PropertyToID("_GameNightFactor");
        private static readonly int HazeId = Shader.PropertyToID("_LT_HazeParams");

        public static bool Has(FacilityType facility)
        {
            if (Availability.TryGetValue(facility, out bool ok)) return ok;
            ok = Resources.Load<GameObject>(StagePath + facility) != null;
            Availability[facility] = ok;
            return ok;
        }

        /// <summary>
        /// 장면을 켜고 찍은 화면을 돌려준다 (없으면 null).
        /// state: 0 빈 칸, 1 가공 중, 2 완성. fresh = 방금 넣어서 떨어지는 연출을 보여 줄지.
        /// </summary>
        public static RenderTexture Show(FacilityType facility, ItemData input, ItemData output, int state, float progress, bool fresh)
        {
            if (!Has(facility)) return null;
            if (_inst == null)
            {
                var go = new GameObject("ProcessDiorama");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<ProcessDiorama>();
                _inst.Build();
            }
            return _inst.ShowInternal(facility, input, output, state, progress, fresh);
        }

        public static void SetProgress(float progress)
        {
            if (_inst != null) _inst._progress = progress;
        }

        public static void Hide()
        {
            if (_inst == null) return;
            if (_inst._cam != null) _inst._cam.enabled = false;
            if (_inst._root != null) _inst._root.gameObject.SetActive(false);
        }

        private void Build()
        {
            _root = new GameObject("DioramaRoot").transform;
            _root.SetParent(transform, false);
            _root.position = RootPosition;

            _rt = new RenderTexture(RtWidth, RtHeight, 24, RenderTextureFormat.ARGB32) { name = "ProcessDioramaRT", antiAliasing = 4 };
            _rt.Create();

            var camGo = new GameObject("DioramaCamera");
            camGo.transform.SetParent(_root, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.targetTexture = _rt;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 60f;
            _cam.depth = -50;
            _cam.allowHDR = true;
            var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = true;

            _item = MakeSprite("DioramaItem");
            _output = MakeSprite("DioramaOutput");

            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        private SpriteRenderer MakeSprite(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 20;
            go.SetActive(false);
            return sr;
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            if (_rt != null) _rt.Release();
            if (_inst == this) _inst = null;
        }

        private RenderTexture ShowInternal(FacilityType facility, ItemData input, ItemData output, int state, float progress, bool fresh)
        {
            _root.gameObject.SetActive(true);
            ProcessDioramaStage stage = GetStage(facility);
            if (stage == null) return null;
            if (_current != stage)
            {
                if (_current != null) _current.gameObject.SetActive(false);
                _current = stage;
                _current.gameObject.SetActive(true);
                _stateSince = Time.time - 10f;
            }

            ItemData shown = state == 2 ? output : input;
            if (state != _state || shown != _shownItem || fresh) _stateSince = fresh ? Time.time : (state != _state ? Time.time : _stateSince);
            _state = state;
            _progress = progress;
            _shownItem = shown;

            _item.sprite = input != null ? DroppedItem.CleanIcon(input.icon) : null;
            _output.sprite = output != null ? DroppedItem.CleanIcon(output.icon) : null;

            _cam.transform.position = stage.transform.TransformPoint(stage.camPosition);
            _cam.transform.LookAt(stage.transform.TransformPoint(stage.camTarget));
            _cam.fieldOfView = stage.camFov;
            _cam.backgroundColor = stage.clearColor;
            _cam.enabled = true;
            return _rt;
        }

        private ProcessDioramaStage GetStage(FacilityType facility)
        {
            if (_stages.TryGetValue(facility, out var s) && s != null) return s;
            var prefab = Resources.Load<GameObject>(StagePath + facility);
            if (prefab == null) return null;
            int index = _stages.Count;
            var go = Instantiate(prefab, _root);
            go.name = "Stage_" + facility;
            go.transform.localPosition = new Vector3(index * 60f, 0f, 0f);   // 장면끼리 서로 비치지 않게 멀리 떨어뜨린다
            s = go.GetComponent<ProcessDioramaStage>();
            if (s != null && s.keyLight != null) s.keyLight.enabled = false;   // 이 카메라가 찍을 때만 켠다
            go.SetActive(false);
            _stages[facility] = s;
            return s;
        }

        private void Update()
        {
            if (_current == null || _cam == null || !_cam.enabled) return;
            _current.Animate(_state, _progress, Time.time - _stateSince, _item, _output, Time.deltaTime);
        }

        // ───────── 찍는 동안만 낮/밤 영향을 없앤다 ─────────

        private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _cam || _current == null) return;

            if (Time.unscaledTime >= _nextLightScan)
            {
                _nextLightScan = Time.unscaledTime + 2f;
                _worldDirectional.Clear();
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional && !l.transform.IsChildOf(_root)) _worldDirectional.Add(l);
            }
            _disabled.Clear();
            foreach (var l in _worldDirectional)
                if (l != null && l.enabled) { l.enabled = false; _disabled.Add(l); }

            if (_current.keyLight != null) _current.keyLight.enabled = true;
            _savedSun = RenderSettings.sun;
            _savedAmbient = RenderSettings.ambientLight;
            _savedAmbientMode = RenderSettings.ambientMode;
            _savedProbe = RenderSettings.ambientProbe;
            _savedFog = RenderSettings.fog;
            _savedNight = Shader.GetGlobalFloat(NightId);
            _savedHaze = Shader.GetGlobalVector(HazeId);

            RenderSettings.sun = _current.keyLight;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = _current.ambient;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(_current.ambient);
            RenderSettings.ambientProbe = sh;
            RenderSettings.fog = false;
            Shader.SetGlobalFloat(NightId, 0f);
            Shader.SetGlobalVector(HazeId, Vector4.zero);
            _overriding = true;
        }

        private void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _cam || !_overriding) return;
            _overriding = false;
            if (_current != null && _current.keyLight != null) _current.keyLight.enabled = false;
            foreach (var l in _disabled) if (l != null) l.enabled = true;
            _disabled.Clear();
            RenderSettings.sun = _savedSun;
            RenderSettings.ambientMode = _savedAmbientMode;
            RenderSettings.ambientLight = _savedAmbient;
            RenderSettings.ambientProbe = _savedProbe;
            RenderSettings.fog = _savedFog;
            Shader.SetGlobalFloat(NightId, _savedNight);
            Shader.SetGlobalVector(HazeId, _savedHaze);
        }
    }
}
