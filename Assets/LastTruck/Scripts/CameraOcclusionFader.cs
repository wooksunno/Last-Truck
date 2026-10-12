using System.Collections.Generic;
using UnityEngine;

namespace LastTruck
{
    /// <summary>
    /// 카메라와 플레이어 사이를 가리는 나무/건물/바위를 반투명하게 만들어서(벽을 뚫어서) 캐릭터가 항상 보이게 한다.
    ///
    /// - 씬에 따로 둘 필요가 없다: 게임이 시작되면 자동으로 만들어지고, "Player" 태그 오브젝트(걸어 다닐 땐 캐릭터, 운전 중엔 트럭)를 따라간다.
    /// - 가리는 물체의 머티리얼만 투명 복사본으로 잠깐 바꿔 끼우고(원본은 그대로), 가려지지 않으면 원래 머티리얼로 되돌린다.
    ///   그래서 수백 개의 오브젝트에 미리 컴포넌트를 붙일 필요가 없고 평소에는 아무 비용이 없다.
    /// - 땅, 작은 물체(돌멩이 등), 움직이는 물체(트럭/몬스터), 트리거는 건드리지 않는다.
    /// - URP Lit 머티리얼(맵 대부분)은 부드럽게 투명해지고, 그 밖의 셰이더는 가려지는 동안 그림자만 남기고 숨긴다.
    /// </summary>
    [DefaultExecutionOrder(1100)]   // 시네머신이 카메라를 옮긴 뒤에 계산한다
    public class CameraOcclusionFader : MonoBehaviour
    {
        [Header("가림 판정")]
        [Tooltip("카메라에서 캐릭터 쪽으로 쏘는 구의 반지름(m). 클수록 화면 가장자리에 걸친 물체도 투명해진다")]
        [SerializeField] private float probeRadius = 0.6f;
        [Tooltip("캐릭터의 어느 높이를 향해 쏠지(m, 발 기준)")]
        [SerializeField] private float targetHeight = 1.0f;
        [Tooltip("이보다 낮은 물체(돌멩이 등)는 가려도 투명하게 하지 않는다")]
        [SerializeField] private float minObstacleHeight = 1.4f;
        [Tooltip("이보다 큰 물체(지형 덩어리, 동굴 전체 등)는 건드리지 않는다")]
        [SerializeField] private float maxObstacleSize = 90f;
        [Tooltip("한 물체에 렌더러가 이보다 많으면(지형 통짜 등) 건드리지 않는다")]
        [SerializeField] private int maxRenderersPerObstacle = 12;

        [Header("투명도")]
        [Range(0.05f, 0.9f)]
        [SerializeField] private float fadedAlpha = 0.2f;
        [SerializeField] private float fadeSeconds = 0.22f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private class Entry
        {
            public Renderer renderer;
            public Material[] original;
            public Material[] faded;         // null이면 그림자만 남기고 숨기는 방식
            public Color[] baseColors;
            public float alpha = 1f;
            public bool blocking;
            public UnityEngine.Rendering.ShadowCastingMode originalShadows;
        }

        private readonly Dictionary<Renderer, Entry> _entries = new Dictionary<Renderer, Entry>();
        private readonly Dictionary<Material, Material> _fadedCache = new Dictionary<Material, Material>();
        private readonly List<Renderer> _toRemove = new List<Renderer>();
        private readonly List<Renderer> _renderBuffer = new List<Renderer>();
        private readonly RaycastHit[] _hits = new RaycastHit[48];
        private MaterialPropertyBlock _block;

        private Transform _target;
        private float _nextFindTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CameraOcclusionFader>() != null)
                return;

            var go = new GameObject("CameraOcclusionFader");
            DontDestroyOnLoad(go);
            go.AddComponent<CameraOcclusionFader>();
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            RestoreAll();
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                MarkAllNotBlocking();
                Apply();
                return;
            }

            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                if (Time.unscaledTime >= _nextFindTarget)
                {
                    _nextFindTarget = Time.unscaledTime + 0.5f;
                    GameObject p = GameObject.FindWithTag("Player");
                    _target = p != null ? p.transform : null;
                }
            }

            MarkAllNotBlocking();
            if (_target != null)
                FindBlockers(cam);
            Apply();
        }

        private void MarkAllNotBlocking()
        {
            foreach (var kv in _entries)
                kv.Value.blocking = false;
        }

        private void FindBlockers(Camera cam)
        {
            Vector3 origin = cam.transform.position;
            Vector3 to = _target.position + Vector3.up * targetHeight;
            Vector3 dir = to - origin;
            float dist = dir.magnitude;
            if (dist < 0.8f)
                return;
            dir /= dist;

            int n = Physics.SphereCastNonAlloc(origin, probeRadius, dir, _hits, dist - 0.4f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider col = _hits[i].collider;
                if (col == null || col.transform.IsChildOf(_target) || _target.IsChildOf(col.transform))
                    continue;
                if (col is TerrainCollider || col.CompareTag("Ground") || col.attachedRigidbody != null)
                    continue;

                Bounds b = col.bounds;
                if (b.size.y < minObstacleHeight || b.size.x > maxObstacleSize || b.size.z > maxObstacleSize)
                    continue;

                _renderBuffer.Clear();
                col.GetComponentsInChildren(true, _renderBuffer);
                if (_renderBuffer.Count == 0 || _renderBuffer.Count > maxRenderersPerObstacle)
                    continue;

                foreach (Renderer r in _renderBuffer)
                {
                    if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                        continue;
                    GetOrCreate(r).blocking = true;
                }
            }
        }

        private Entry GetOrCreate(Renderer r)
        {
            if (_entries.TryGetValue(r, out Entry e))
                return e;

            e = new Entry { renderer = r, original = r.sharedMaterials, originalShadows = r.shadowCastingMode };
            e.baseColors = new Color[e.original.Length];
            e.faded = new Material[e.original.Length];
            bool allFadeable = true;
            for (int i = 0; i < e.original.Length; i++)
            {
                Material m = e.original[i];
                if (m == null) { allFadeable = false; continue; }
                e.baseColors[i] = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : (m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white);
                e.faded[i] = GetFadedMaterial(m);
                if (e.faded[i] == null) allFadeable = false;
            }
            if (!allFadeable)
                e.faded = null;

            _entries[r] = e;
            return e;
        }

        /// <summary>URP Lit 계열이면 투명 복사본을 만들어 돌려준다 (그 밖의 셰이더는 null).</summary>
        private Material GetFadedMaterial(Material source)
        {
            if (_fadedCache.TryGetValue(source, out Material cached))
                return cached;

            Material result = null;
            if (source.HasProperty("_Surface") && source.HasProperty(BaseColorId))
            {
                result = new Material(source) { name = source.name + " (Faded)", hideFlags = HideFlags.HideAndDontSave };
                result.SetFloat("_Surface", 1f);
                result.SetFloat("_Blend", 0f);
                result.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                result.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                result.SetFloat("_ZWrite", 0f);
                result.SetOverrideTag("RenderType", "Transparent");
                result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                result.DisableKeyword("_ALPHATEST_ON");
                result.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            _fadedCache[source] = result;
            return result;
        }

        private void Apply()
        {
            float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
            _toRemove.Clear();

            foreach (var kv in _entries)
            {
                Entry e = kv.Value;
                Renderer r = e.renderer;
                if (r == null)
                {
                    _toRemove.Add(kv.Key);
                    continue;
                }

                float target = e.blocking ? fadedAlpha : 1f;
                e.alpha = Mathf.MoveTowards(e.alpha, target, step * (1f - fadedAlpha));

                if (!e.blocking && e.alpha >= 0.999f)
                {
                    Restore(e);
                    _toRemove.Add(kv.Key);
                    continue;
                }

                if (e.faded != null)
                {
                    // 투명 머티리얼로 바꿔 끼우고, 칸마다 원래 색에 알파만 곱한다
                    if (r.sharedMaterials.Length != e.faded.Length || r.sharedMaterial != e.faded[0])
                        r.sharedMaterials = e.faded;

                    for (int i = 0; i < e.faded.Length; i++)
                    {
                        _block.Clear();
                        Color c = e.baseColors[i];
                        c.a *= e.alpha;
                        _block.SetColor(BaseColorId, c);
                        r.SetPropertyBlock(_block, i);
                    }
                }
                else
                {
                    // 투명 처리가 안 되는 셰이더: 거의 가려지면 그림자만 남기고 숨긴다
                    r.shadowCastingMode = e.alpha < 0.6f
                        ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                        : e.originalShadows;
                }
            }

            foreach (Renderer r in _toRemove)
                _entries.Remove(r);
        }

        private void Restore(Entry e)
        {
            Renderer r = e.renderer;
            if (r == null)
                return;

            if (e.faded != null)
            {
                for (int i = 0; i < e.faded.Length; i++)
                    r.SetPropertyBlock(null, i);
                r.sharedMaterials = e.original;
            }
            r.shadowCastingMode = e.originalShadows;
        }

        private void RestoreAll()
        {
            foreach (var kv in _entries)
                Restore(kv.Value);
            _entries.Clear();
        }

        private void OnDestroy()
        {
            foreach (var kv in _fadedCache)
                if (kv.Value != null) Destroy(kv.Value);
        }
    }
}
