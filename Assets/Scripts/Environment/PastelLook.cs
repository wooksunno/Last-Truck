using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LastTruck
{
    /// <summary>
    /// "파스텔 룩": 기존 셰이더/분위기(AtmosphereController)를 바꾸지 않고 위에 덧씌우는 부드러운 화면 톤 + 하늘 구름.
    ///
    ///  - 화면 톤: 낮/밤 Volume보다 우선순위가 높은 Volume 하나를 더 둔다.
    ///    노출을 살짝 올리고 대비를 낮추며, 검은 그림자를 라벤더빛으로 들어 올리고(Lift), 밝은 곳엔 복숭아빛, 은은한 블룸.
    ///  - 맵 셰이더(ToonWorld): 전역 값 _LT_Pastel로 명암 경계를 부드러운 그라데이션으로, 그림자 면을 밝은 라벤더로.
    ///  - 구름: 둥근 덩어리를 뭉친 몽글몽글한 구름(LastTruck/SoftCloud)이 하늘에 천천히 흘러간다.
    ///  - 밤에는 효과가 약해져서(nightStrength) 밤의 어두운 분위기와 좀비 가시성은 그대로 둔다.
    ///
    /// F2: 켜기/끄기 (비포/애프터 비교). 게임 씬(AtmosphereController가 있는 씬)에서 자동으로 만들어진다.
    /// </summary>
    [DefaultExecutionOrder(60)]   // AtmosphereController(50)가 값을 정한 다음에 덧씌운다
    public class PastelLook : MonoBehaviour
    {
        public static PastelLook Instance { get; private set; }

        /// <summary>파스텔 룩이 켜져 있는가 (껐다 켜도 이 판에서는 유지).</summary>
        public static bool Enabled = true;

        public const KeyCode ToggleKey = KeyCode.F2;

        [Header("세기")]
        [Range(0f, 1f)] public float dayStrength = 1f;
        [Tooltip("밤에는 이만큼만 (밤 분위기·좀비 가시성은 유지).")]
        [Range(0f, 1f)] public float nightStrength = 0.35f;
        [Tooltip("켜고 끌 때 바뀌는 시간(초).")]
        public float fadeSeconds = 0.6f;

        [Header("구름")]
        public int cloudCount = 44;
        public float cloudMinHeight = 115f;
        public float cloudMaxHeight = 185f;
        public float cloudRadius = 750f;
        public float cloudDrift = 2.2f;             // m/s
        public Vector3 windDirection = new Vector3(1f, 0f, 0.35f);
        [Tooltip("구름이 카메라를 따라오는 정도 (1 = 하늘에 붙어 있음, 0 = 땅에 고정). 조금 덜 따라와서 움직일 때 살짝 흘러가 보인다.")]
        [Range(0f, 1f)] public float cloudFollow = 0.8f;

        private static readonly int PastelId = Shader.PropertyToID("_LT_Pastel");
        private static readonly int PastelShadeId = Shader.PropertyToID("_LT_PastelShade");
        private static readonly int NightId = Shader.PropertyToID("_GameNightFactor");

        private Volume _volume;
        private float _blend;   // 0 끔 ~ 1 켬 (부드럽게)
        private Transform _cloudRoot;
        private readonly List<Transform> _clouds = new List<Transform>();
        private readonly List<Vector3> _cloudOffsets = new List<Vector3>();   // 구름 기준점(카메라 따라오는 점)에서의 위치
        private Vector3 _anchor;
        private bool _anchorSet;

        // ───── 자동 생성 ─────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate();

        private static void TryCreate()
        {
            if (Instance != null) return;
            if (FindFirstObjectByType<AtmosphereController>() == null) return;   // 로비/메뉴 씬에는 만들지 않는다
            new GameObject("PastelLook").AddComponent<PastelLook>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _blend = Enabled ? 1f : 0f;
            BuildVolume();
            BuildClouds();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Shader.SetGlobalFloat(PastelId, 0f);
        }

        /// <summary>켜기/끄기 (비포/애프터).</summary>
        public static void Toggle()
        {
            Enabled = !Enabled;
            HudToast.Show(Enabled ? "파스텔 룩 켬 (F2)" : "파스텔 룩 끔 · 원래 모습 (F2)", 1.8f);
        }

        // ───── 매 프레임 ─────

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey)) Toggle();
        }

        private void LateUpdate()
        {
            float target = Enabled ? 1f : 0f;
            _blend = fadeSeconds > 0f ? Mathf.MoveTowards(_blend, target, Time.unscaledDeltaTime / fadeSeconds) : target;

            float night = Mathf.Clamp01(Shader.GetGlobalFloat(NightId));
            float strength = _blend * Mathf.Lerp(dayStrength, nightStrength, night);

            if (_volume != null)
            {
                _volume.weight = strength;
                bool on = strength > 0.001f;
                if (_volume.enabled != on) _volume.enabled = on;
            }
            Shader.SetGlobalFloat(PastelId, strength);
            Shader.SetGlobalVector(PastelShadeId, new Vector4(0.80f, 0.76f, 0.97f, 1f));

            UpdateClouds();
        }

        // ───── 화면 톤 ─────

        private void BuildVolume()
        {
            var go = new GameObject("PastelVolume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 50f;   // 낮/밤 Volume보다 위 (카메라 효과 Volume은 따로 섞인다)
            _volume.weight = 0f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "PastelLook";

            var ca = profile.Add<ColorAdjustments>(true);
            // 뿌옇지 않게: 노출은 거의 그대로, 대비는 오히려 조금 올리고 채도로 맑은 파스텔 색을 낸다
            ca.postExposure.Override(0.08f);
            ca.contrast.Override(10f);
            ca.saturation.Override(8f);
            ca.colorFilter.Override(new Color(1f, 0.99f, 0.97f));

            // 검은색을 없앤다: 가장 어두운 곳도 연한 라벤더 (파스텔의 핵심)
            var lgg = profile.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(1.0f, 0.99f, 1.02f, 0f));   // 검은색은 띄우지 않고 색조만 (띄우면 화면이 뿌옇다)
            lgg.gamma.Override(new Vector4(1.0f, 1.0f, 1.0f, 0f));
            lgg.gain.Override(new Vector4(1.0f, 1.0f, 1.0f, 0f));

            var st = profile.Add<SplitToning>(true);
            st.shadows.Override(new Color(0.56f, 0.56f, 0.66f));
            st.highlights.Override(new Color(0.8f, 0.74f, 0.68f));
            st.balance.Override(15f);   // 그림자 색조는 아주 어두운 곳에만 (갈색 땅이 붉은 보라로 변하지 않게)

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.15f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(new Color(1f, 0.92f, 0.94f));

            var vg = profile.Add<Vignette>(true);
            vg.intensity.Override(0.14f);
            vg.smoothness.Override(0.6f);
            vg.color.Override(new Color(0.45f, 0.38f, 0.52f));

            _volume.profile = profile;
        }

        // ───── 구름 ─────

        private void BuildClouds()
        {
            Material mat = Resources.Load<Material>("Materials/SoftCloud");
            if (mat == null)
            {
                Shader shader = Shader.Find("LastTruck/SoftCloud");
                if (shader == null) return;
                mat = new Material(shader);
            }

            _cloudRoot = new GameObject("PastelClouds").transform;
            _cloudRoot.SetParent(transform, false);

            Mesh puff = BuildIcoSphere(2);
            var rng = new System.Random(20261118);
            for (int i = 0; i < cloudCount; i++)
            {
                Mesh mesh = BuildCloudMesh(puff, rng);
                var go = new GameObject("Cloud_" + i);
                go.transform.SetParent(_cloudRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

                // 고르게 퍼지도록: 원판 안의 임의 점 (가운데가 너무 비지 않게 sqrt 분포)
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * cloudRadius;
                float h = Mathf.Lerp(cloudMinHeight, cloudMaxHeight, (float)rng.NextDouble());
                float s = Mathf.Lerp(0.8f, 1.6f, (float)rng.NextDouble());
                go.transform.localScale = new Vector3(s, s * Mathf.Lerp(0.8f, 1.05f, (float)rng.NextDouble()), s);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                _clouds.Add(go.transform);
                _cloudOffsets.Add(new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r));
            }
        }

        private void UpdateClouds()
        {
            if (_cloudRoot == null) return;
            bool show = _blend > 0.5f;
            if (_cloudRoot.gameObject.activeSelf != show) _cloudRoot.gameObject.SetActive(show);
            if (!show) return;

            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 camPos = cam.transform.position;
            if (!_anchorSet) { _anchor = new Vector3(camPos.x, 0f, camPos.z); _anchorSet = true; }

            // 구름 기준점: 카메라를 cloudFollow만큼 따라온다 (멀리 가도 하늘에 구름이 남아 있게)
            Vector3 camFlat = new Vector3(camPos.x, 0f, camPos.z);
            Vector3 center = Vector3.Lerp(_anchor, camFlat, cloudFollow);
            // 너무 뒤처지면 기준점을 끌어온다
            if ((camFlat - _anchor).sqrMagnitude > cloudRadius * cloudRadius * 0.25f)
                _anchor = Vector3.Lerp(_anchor, camFlat, 0.5f * Time.deltaTime);

            Vector3 wind = windDirection.sqrMagnitude > 0.0001f ? windDirection.normalized : Vector3.right;
            float step = cloudDrift * Time.deltaTime;
            for (int i = 0; i < _clouds.Count; i++)
            {
                Vector3 o = _cloudOffsets[i] + wind * step;
                // 바람 방향으로 원판을 벗어나면 반대편으로 돌려 보낸다
                Vector2 flat = new Vector2(o.x, o.z);
                if (flat.sqrMagnitude > cloudRadius * cloudRadius)
                {
                    flat = -flat.normalized * (cloudRadius * 0.98f);
                    o.x = flat.x; o.z = flat.y;
                }
                _cloudOffsets[i] = o;
                _clouds[i].position = center + o;
            }
        }

        /// <summary>둥근 덩어리 여러 개를 뭉쳐 아래가 평평한 구름 하나를 만든다.</summary>
        private static Mesh BuildCloudMesh(Mesh puff, System.Random rng)
        {
            Vector3[] pv = puff.vertices;
            int[] pt = puff.triangles;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();

            int puffs = rng.Next(5, 10);
            float length = 24f + (float)rng.NextDouble() * 34f;   // 구름 길이
            for (int p = 0; p < puffs; p++)
            {
                float u = puffs == 1 ? 0.5f : p / (float)(puffs - 1);
                // 가운데 덩어리가 크고 높게, 양 끝은 작게
                float bell = Mathf.Sin(u * Mathf.PI);
                float radius = Mathf.Lerp(5f, 11f, bell) * Mathf.Lerp(0.85f, 1.15f, (float)rng.NextDouble());
                Vector3 c = new Vector3((u - 0.5f) * length,
                                        radius * 0.35f + bell * 3f,
                                        ((float)rng.NextDouble() - 0.5f) * 9f);
                int baseIndex = verts.Count;
                for (int i = 0; i < pv.Length; i++)
                {
                    Vector3 v = c + pv[i] * radius;
                    Vector3 n = pv[i];
                    // 구름 바닥은 평평하게 눌러 준다
                    if (v.y < 0f) { v.y = v.y * 0.15f; n = Vector3.Lerp(n, Vector3.down, 0.6f).normalized; }
                    verts.Add(v);
                    norms.Add(n);
                }
                for (int i = 0; i < pt.Length; i++) tris.Add(baseIndex + pt[i]);
            }

            var mesh = new Mesh { name = "PastelCloud" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>반지름 1인 매끈한 구 (정이십면체를 나눠서). 노멀 = 위치.</summary>
        private static Mesh BuildIcoSphere(int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                var nf = new List<int>();
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = Mid(a, b, v, cache), bc = Mid(b, c, v, cache), ca = Mid(c, a, v, cache);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
            var mesh = new Mesh();
            mesh.SetVertices(v);
            mesh.SetNormals(v);
            mesh.SetTriangles(f, 0);
            return mesh;
        }

        private static int Mid(int a, int b, List<Vector3> v, Dictionary<long, int> cache)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            int idx;
            if (cache.TryGetValue(key, out idx)) return idx;
            v.Add(((v[a] + v[b]) * 0.5f).normalized);
            idx = v.Count - 1;
            cache[key] = idx;
            return idx;
        }
    }
}
