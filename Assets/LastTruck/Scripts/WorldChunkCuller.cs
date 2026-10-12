using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LastTruck
{
    /// <summary>
    /// 맵을 격자(청크)로 나눠서, 내(카메라/캐릭터)가 있는 곳에서 먼 청크의 물체를 단계별로 줄이는 프레임 관리 장치.
    ///
    /// 씬에 따로 둘 필요가 없다: 게임이 시작되면 자동으로 만들어지고, 씬이 로드될 때 맵의 고정된 물체를 청크에 등록한다.
    /// 단계(거리는 청크 경계 상자까지의 3D 거리):
    ///   가까움(nearRange 이내)  : 전부 보인다 (원래 그림자 그대로).
    ///   중간(midRange 이내)     : 그림자를 끄고, 작은 물체(풀/돌멩이 등)는 숨긴다.
    ///   멀리(farRange 이내)     : 큰 물체(건물/지형 조각/큰 바위)만 그림자 없이 보인다.
    ///   그 너머                 : 전부 숨긴다.
    /// 조명(점 조명/스포트)은 lightRange 밖이면 끄고, 그림자 조명은 lightShadowRange 밖에서 그림자만 끈다.
    /// 반복 재생되는 파티클(불/연기 등)은 particleRange 밖에서 멈추고 숨긴다.
    ///
    /// 움직이는 물체(Rigidbody/Animator/CharacterController가 붙은 것: 트럭, 플레이어, 몬스터 등)와 나중에 생기는 물체는 건드리지 않는다.
    /// 콜라이더는 끄지 않는다 (멀리 있는 동료/트럭이 바닥 밑으로 떨어지지 않게).
    /// 한 프레임에 바꾸는 양을 제한해서 청크가 바뀔 때 프레임이 튀지 않게 한다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class WorldChunkCuller : MonoBehaviour
    {
        [Header("청크 크기(m)")]
        [SerializeField] private float cellSizeXZ = 40f;
        [SerializeField] private float cellSizeY = 30f;

        [Header("거리(m) - 메시")]
        [Tooltip("이 거리 안: 전부 그대로")]
        [SerializeField] private float nearRange = 90f;
        [Tooltip("이 거리 안: 그림자 끄고 작은 물체 숨김")]
        [SerializeField] private float midRange = 180f;
        [Tooltip("이 거리 안: 큰 물체만. 그 너머는 전부 숨김")]
        [SerializeField] private float farRange = 380f;
        [Tooltip("이 크기(가장 긴 변, m)보다 작으면 중간 거리부터 숨긴다")]
        [SerializeField] private float smallObjectSize = 2.5f;
        [Tooltip("이 크기보다 크면 멀리서도 보인다")]
        [SerializeField] private float bigObjectSize = 12f;

        [Header("거리(m) - 조명 / 파티클")]
        [SerializeField] private float lightRange = 85f;
        [SerializeField] private float lightShadowRange = 35f;
        [SerializeField] private float particleRange = 100f;

        [Header("갱신")]
        [Tooltip("이 간격(초)마다 어느 청크가 가깝고 먼지 다시 계산한다")]
        [SerializeField] private float updateInterval = 0.2f;
        [Tooltip("경계에서 깜빡이지 않도록, 멀어질 때는 이만큼 더 멀어져야 단계를 낮춘다(m)")]
        [SerializeField] private float hysteresis = 10f;
        [Tooltip("한 프레임에 켜고 끄는 물체 수 상한 (청크 단위로 처리)")]
        [SerializeField] private int budgetPerFrame = 700;

        [Header("LOD")]
        [Tooltip("LODGroup 전환 거리 배율 (Ultra 품질은 2라서 멀리까지 고화질 모델을 쓴다). 이 값보다 크면 낮춘다")]
        [SerializeField] private float maxLodBias = 1.2f;

        private enum Kind { Mesh, Light, Particle }

        private class Entry
        {
            public Kind kind;
            public Renderer renderer;
            public Light light;
            public ParticleSystem particles;
            public ParticleSystemRenderer particleRenderer;
            public float size;
            public ShadowCastingMode originalShadows;
            public LightShadows originalLightShadows;
            public int applied;    // 0 = 원래대로, 1 = 그림자 없음, 2 = 숨김/꺼짐
        }

        private class Chunk
        {
            public readonly List<Entry> entries = new List<Entry>();
            public Bounds bounds;
            public bool hasBounds;
            public int meshTier = -1;       // 0 가까움, 1 중간, 2 멀리, 3 숨김
            public int lightTier = -1;      // 0 그림자까지, 1 켜짐, 2 꺼짐
            public int particleTier = -1;   // 0 재생, 1 정지
            public bool queued;
        }

        private readonly Dictionary<Vector3Int, Chunk> _chunks = new Dictionary<Vector3Int, Chunk>();
        private readonly List<Chunk> _chunkList = new List<Chunk>();
        private readonly Queue<Chunk> _pending = new Queue<Chunk>();
        private readonly Dictionary<Transform, bool> _dynamicCache = new Dictionary<Transform, bool>();

        private float _nextUpdate;
        private bool _firstPass = true;
        private Transform _player;
        private float _nextFindPlayer;
        private float _originalLodBias = -1f;
        private int _registered;

        public int ChunkCount => _chunkList.Count;
        public int RegisteredCount => _registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<WorldChunkCuller>() != null) return;
            var go = new GameObject("WorldChunkCuller");
            DontDestroyOnLoad(go);
            go.AddComponent<WorldChunkCuller>();
        }

        private float[] _meshLimits, _lightLimits, _particleLimits;

        private void Awake()
        {
            _meshLimits = new[] { nearRange, midRange, farRange };
            _lightLimits = new[] { lightShadowRange, lightRange };
            _particleLimits = new[] { particleRange };

            if (maxLodBias > 0f && QualitySettings.lodBias > maxLodBias)
            {
                _originalLodBias = QualitySettings.lodBias;
                QualitySettings.lodBias = maxLodBias;
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.isLoaded) StartCoroutine(RegisterNextFrame(s));
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            RestoreAll();
            if (_originalLodBias > 0f) QualitySettings.lodBias = _originalLodBias;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(RegisterNextFrame(scene));
        }

        private IEnumerator RegisterNextFrame(Scene scene)
        {
            // 씬의 Awake/Start가 끝난 뒤에 등록한다 (시작할 때 렌더러를 끄는 스크립트와 겹치지 않게)
            yield return null;
            yield return null;
            if (!scene.isLoaded) yield break;
            Register(scene);
            _firstPass = true;
            _nextUpdate = 0f;
        }

        // ------------------------------------------------------------ 등록

        private void Register(Scene scene)
        {
            _dynamicCache.Clear();
            int before = _registered;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (r == null || !r.enabled || IsDynamic(r.transform)) continue;
                    var e = new Entry { kind = Kind.Mesh, renderer = r, originalShadows = r.shadowCastingMode };
                    Vector3 s = r.bounds.size;
                    e.size = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                    Add(e, r.bounds);
                }

                foreach (Light l in root.GetComponentsInChildren<Light>(false))
                {
                    if (l == null || !l.enabled || l.type == LightType.Directional || IsDynamic(l.transform)) continue;
                    var e = new Entry { kind = Kind.Light, light = l, originalLightShadows = l.shadows };
                    Add(e, new Bounds(l.transform.position, Vector3.one * 0.1f));
                }

                foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(false))
                {
                    // 반복 재생 중인 장식용 파티클만 (한 번 터지는 효과를 다시 재생시키지 않도록)
                    if (ps == null || !ps.main.loop || !ps.isPlaying || IsDynamic(ps.transform)) continue;
                    var e = new Entry
                    {
                        kind = Kind.Particle,
                        particles = ps,
                        particleRenderer = ps.GetComponent<ParticleSystemRenderer>(),
                    };
                    Add(e, new Bounds(ps.transform.position, Vector3.one * 0.1f));
                }
            }

            _dynamicCache.Clear();
            Debug.Log("[WorldChunkCuller] " + scene.name + ": " + (_registered - before) + "개 등록, 청크 " + _chunkList.Count + "개");
        }

        private void Add(Entry e, Bounds b)
        {
            Vector3 c = b.center;
            var key = new Vector3Int(
                Mathf.FloorToInt(c.x / cellSizeXZ),
                Mathf.FloorToInt(c.y / cellSizeY),
                Mathf.FloorToInt(c.z / cellSizeXZ));

            if (!_chunks.TryGetValue(key, out Chunk chunk))
            {
                chunk = new Chunk();
                _chunks[key] = chunk;
                _chunkList.Add(chunk);
            }

            chunk.entries.Add(e);
            if (!chunk.hasBounds) { chunk.bounds = b; chunk.hasBounds = true; }
            else chunk.bounds.Encapsulate(b);
            _registered++;
        }

        /// <summary>움직이는 물체(또는 그 일부)인가: Rigidbody/Animator/CharacterController가 위쪽에 있으면 건드리지 않는다.</summary>
        private bool IsDynamic(Transform t)
        {
            if (_dynamicCache.TryGetValue(t, out bool cached)) return cached;

            bool result;
            if (t.GetComponent<Rigidbody>() != null || t.GetComponent<Animator>() != null || t.GetComponent<CharacterController>() != null ||
                t.CompareTag("Player"))
                result = true;
            else
                result = t.parent != null && IsDynamic(t.parent);

            _dynamicCache[t] = result;
            return result;
        }

        // ------------------------------------------------------------ 갱신

        private void Update()
        {
            if (_chunkList.Count == 0) return;

            if (Time.unscaledTime >= _nextUpdate)
            {
                _nextUpdate = Time.unscaledTime + updateInterval;
                Reevaluate();
            }

            int budget = _firstPass ? int.MaxValue : budgetPerFrame;
            while (_pending.Count > 0 && budget > 0)
            {
                Chunk c = _pending.Dequeue();
                c.queued = false;
                budget -= Apply(c);
            }
            if (_pending.Count == 0) _firstPass = false;
        }

        private void Reevaluate()
        {
            Camera cam = Camera.main;
            if (_player == null && Time.unscaledTime >= _nextFindPlayer)
            {
                _nextFindPlayer = Time.unscaledTime + 1f;
                GameObject p = GameObject.FindWithTag("Player");
                _player = p != null ? p.transform : null;
            }
            if (cam == null && _player == null) return;

            Vector3 camPos = cam != null ? cam.transform.position : _player.position;
            bool hasPlayer = _player != null && _player.gameObject.activeInHierarchy;
            Vector3 playerPos = hasPlayer ? _player.position : camPos;

            for (int i = 0; i < _chunkList.Count; i++)
            {
                Chunk c = _chunkList[i];
                float d = Mathf.Min(Dist(c.bounds, camPos), Dist(c.bounds, playerPos));

                int mesh = NextTier(c.meshTier, d, _meshLimits);
                int light = NextTier(c.lightTier, d, _lightLimits);
                int particle = NextTier(c.particleTier, d, _particleLimits);

                if (mesh != c.meshTier || light != c.lightTier || particle != c.particleTier)
                {
                    c.meshTier = mesh;
                    c.lightTier = light;
                    c.particleTier = particle;
                    if (!c.queued)
                    {
                        c.queued = true;
                        _pending.Enqueue(c);
                    }
                }
            }
        }

        private static float Dist(Bounds b, Vector3 p)
        {
            return Mathf.Sqrt(b.SqrDistance(p));
        }

        /// <summary>거리 d가 몇 번째 단계인가. 멀어질 때(단계가 커질 때)는 hysteresis만큼 더 멀어져야 바뀐다.</summary>
        private int NextTier(int current, float d, float[] limits)
        {
            int raw = limits.Length;
            for (int i = 0; i < limits.Length; i++)
            {
                if (d <= limits[i]) { raw = i; break; }
            }

            if (current >= 0 && raw > current)
            {
                // 멀어지는 방향: 현재 단계의 경계 + 여유를 넘었는지 본다
                int hold = limits.Length;
                for (int i = 0; i < limits.Length; i++)
                {
                    if (d <= limits[i] + hysteresis) { hold = i; break; }
                }
                raw = Mathf.Min(raw, Mathf.Max(current, hold));
            }
            return raw;
        }

        /// <summary>청크의 물체를 현재 단계에 맞게 켜고 끈다. 건드린 물체 수를 돌려준다.</summary>
        private int Apply(Chunk c)
        {
            int touched = 0;
            for (int i = c.entries.Count - 1; i >= 0; i--)
            {
                Entry e = c.entries[i];
                switch (e.kind)
                {
                    case Kind.Mesh:
                        if (e.renderer == null) { c.entries.RemoveAt(i); _registered--; continue; }
                        touched += ApplyMesh(e, c.meshTier);
                        break;
                    case Kind.Light:
                        if (e.light == null) { c.entries.RemoveAt(i); _registered--; continue; }
                        touched += ApplyLight(e, c.lightTier);
                        break;
                    case Kind.Particle:
                        if (e.particles == null) { c.entries.RemoveAt(i); _registered--; continue; }
                        touched += ApplyParticle(e, c.particleTier);
                        break;
                }
            }
            return Mathf.Max(touched, 1);
        }

        private int ApplyMesh(Entry e, int tier)
        {
            int want;
            switch (tier)
            {
                case 0: want = 0; break;
                case 1: want = e.size >= smallObjectSize ? 1 : 2; break;
                case 2: want = e.size >= bigObjectSize ? 1 : 2; break;
                default: want = 2; break;
            }
            if (want == e.applied) return 0;

            Renderer r = e.renderer;
            switch (want)
            {
                case 0:
                    r.enabled = true;
                    r.shadowCastingMode = e.originalShadows;
                    break;
                case 1:
                    r.enabled = true;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    break;
                default:
                    r.enabled = false;
                    break;
            }
            e.applied = want;
            return 1;
        }

        private int ApplyLight(Entry e, int tier)
        {
            int want = tier <= 0 ? 0 : (tier == 1 ? 1 : 2);
            if (want == e.applied) return 0;

            Light l = e.light;
            switch (want)
            {
                case 0:
                    l.enabled = true;
                    l.shadows = e.originalLightShadows;
                    break;
                case 1:
                    l.enabled = true;
                    l.shadows = LightShadows.None;
                    break;
                default:
                    l.enabled = false;
                    break;
            }
            e.applied = want;
            return 1;
        }

        private int ApplyParticle(Entry e, int tier)
        {
            int want = tier <= 0 ? 0 : 2;
            if (want == e.applied) return 0;

            if (want == 0)
            {
                if (e.particleRenderer != null) e.particleRenderer.enabled = true;
                e.particles.Play(true);
            }
            else
            {
                e.particles.Pause(true);
                if (e.particleRenderer != null) e.particleRenderer.enabled = false;
            }
            e.applied = want;
            return 1;
        }

        // ------------------------------------------------------------ 복원

        private void RestoreAll()
        {
            foreach (Chunk c in _chunkList)
            {
                foreach (Entry e in c.entries)
                {
                    if (e.applied == 0) continue;
                    switch (e.kind)
                    {
                        case Kind.Mesh:
                            if (e.renderer != null) { e.renderer.enabled = true; e.renderer.shadowCastingMode = e.originalShadows; }
                            break;
                        case Kind.Light:
                            if (e.light != null) { e.light.enabled = true; e.light.shadows = e.originalLightShadows; }
                            break;
                        case Kind.Particle:
                            if (e.particles != null)
                            {
                                if (e.particleRenderer != null) e.particleRenderer.enabled = true;
                                e.particles.Play(true);
                            }
                            break;
                    }
                    e.applied = 0;
                }
            }
        }
    }
}
