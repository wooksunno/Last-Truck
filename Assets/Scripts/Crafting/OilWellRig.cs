using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 기름 웅덩이(기름 SpecialResourceNode)에 세우는 "유정" 모양과 시추 연출. 모양은 전부 코드로 만든다 (씬 용량을 늘리지 않음).
    ///  - 늘: 웅덩이 가운데 강철 유정 머리(케이싱 + 플랜지 + 빨간 밸브) + 나무 시추 삼각대(도르래와 늘어진 줄).
    ///        웅덩이 표면에 가끔 검은 기름 방울이 부글거린다.
    ///  - 내가 구리 수동 펌프로 퍼는 동안(E 꾹): 펌프가 유정 머리에 끼워지고(설치) 손잡이가 오르내릴 때마다 주둥이에서 기름이 콸콸 쏟아져
    ///        양동이에 찬다. 다 퍼면 기름이 크게 튀고, 양동이는 비워지고, 펌프는 다시 빠진다.
    ///  - 고갈: 부글거림이 멈춘다. 펌프잭이 설치된 동안(Suppressed): 삼각대/양동이를 치우고 유정 머리만 남긴다 (펌프잭이 그 위에서 일한다).
    /// SpecialResourceNode.Start가 자동으로 붙인다. 연출은 내 화면 기준(다른 사람이 퍼는 모습은 보이지 않음).
    /// </summary>
    public class OilWellRig : MonoBehaviour
    {
        private static readonly Dictionary<int, Material> MatCache = new Dictionary<int, Material>();

        public static readonly Color OilColor = new Color(0.045f, 0.04f, 0.04f);
        private static readonly Color Steel = new Color(0.30f, 0.32f, 0.36f);
        private static readonly Color DarkSteel = new Color(0.16f, 0.16f, 0.18f);
        private static readonly Color Copper = new Color(0.74f, 0.43f, 0.26f);
        private static readonly Color Red = new Color(0.68f, 0.17f, 0.12f);
        private static readonly Color Wood = new Color(0.45f, 0.30f, 0.18f);
        private static readonly Color Rope = new Color(0.72f, 0.62f, 0.42f);
        private static readonly Color BucketColor = new Color(0.42f, 0.44f, 0.46f);

        private SpecialResourceNode _node;
        private Transform _root, _tripod, _pump, _handle, _bucket, _bucketOil, _stream, _casing;
        private LastTruck.PlayerInteract _interact;
        private float _install;        // 0 = 펌프 없음 ~ 1 = 유정에 끼워짐
        private float _stroke;         // 손잡이 위상
        private float _fill;           // 양동이에 찬 기름 0~1
        private float _lastStrokeSin;
        private float _nextBubble;
        private float _drain;          // 다 퍼고 나서 양동이를 비우는 시간
        private bool _pumping;

        private void Awake()
        {
            _node = GetComponent<SpecialResourceNode>();
        }

        private void Start()
        {
            Build();
            if (_node != null) _node.Harvested += OnHarvested;
            _nextBubble = Time.time + Random.Range(0.5f, 3f);
        }

        private void OnDestroy()
        {
            if (_node != null) _node.Harvested -= OnHarvested;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { MatCache.Clear(); }

        // ───────── 모양 ─────────

        internal static Material Mat(Color c)
        {
            int key = Mathf.RoundToInt(c.r * 255) << 16 | Mathf.RoundToInt(c.g * 255) << 8 | Mathf.RoundToInt(c.b * 255);
            if (MatCache.TryGetValue(key, out var m) && m != null) return m;
            Shader sh = Shader.Find("LastTruck/ToonWorld");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh) { name = "OilWell_" + key };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            MatCache[key] = m;
            return m;
        }

        internal static Transform Part(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Color color, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.layer = 0;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Mat(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        internal static Transform Empty(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        private void Build()
        {
            // 노드 크기와 상관없이 같은 크기로 보이도록 크기를 되돌린 기준점에 만든다
            _root = Empty(transform, "OilWellRig", Vector3.zero);
            Vector3 ls = transform.lossyScale;
            _root.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), 1f / Mathf.Max(0.01f, ls.z));
            Vector3 p = transform.position;
            _root.rotation = Quaternion.Euler(0f, Mathf.Repeat(p.x * 23f + p.z * 41f, 360f), 0f);

            // 유정 머리: 케이싱 + 플랜지 두 겹 + 빨간 밸브 핸들
            _casing = Empty(_root, "Wellhead", Vector3.zero);
            Part(PrimitiveType.Cylinder, _casing, "Casing", new Vector3(0f, 0.28f, 0f), new Vector3(0.34f, 0.3f, 0.34f), Steel);
            Part(PrimitiveType.Cylinder, _casing, "FlangeLow", new Vector3(0f, 0.08f, 0f), new Vector3(0.62f, 0.05f, 0.62f), DarkSteel);
            Part(PrimitiveType.Cylinder, _casing, "FlangeTop", new Vector3(0f, 0.6f, 0f), new Vector3(0.46f, 0.04f, 0.46f), DarkSteel);
            for (int i = 0; i < 6; i++)   // 볼트
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Part(PrimitiveType.Cylinder, _casing, "Bolt", new Vector3(Mathf.Cos(a) * 0.26f, 0.12f, Mathf.Sin(a) * 0.26f), new Vector3(0.05f, 0.04f, 0.05f), Steel);
            }
            Part(PrimitiveType.Cylinder, _casing, "ValveStem", new Vector3(0.24f, 0.38f, 0f), new Vector3(0.06f, 0.08f, 0.06f), DarkSteel, new Vector3(0f, 0f, 90f));
            var wheel = Part(PrimitiveType.Cylinder, _casing, "ValveWheel", new Vector3(0.33f, 0.38f, 0f), new Vector3(0.26f, 0.02f, 0.26f), Red, new Vector3(0f, 0f, 90f));
            Part(PrimitiveType.Cube, wheel, "Spoke", Vector3.zero, new Vector3(0.95f, 1.2f, 0.12f), Red);
            Part(PrimitiveType.Cube, wheel, "Spoke", Vector3.zero, new Vector3(0.12f, 1.2f, 0.95f), Red);

            // 나무 시추 삼각대 (도르래 + 늘어진 줄)
            _tripod = Empty(_root, "Tripod", Vector3.zero);
            const float H = 2.7f, R = 1.15f;
            Vector3 apex = new Vector3(0f, H, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f + 30f) * Mathf.Deg2Rad;
                Vector3 foot = new Vector3(Mathf.Cos(a) * R, 0f, Mathf.Sin(a) * R);
                Vector3 mid = (foot + apex) * 0.5f;
                float len = Vector3.Distance(foot, apex);
                var leg = Part(PrimitiveType.Cylinder, _tripod, "Leg", mid, new Vector3(0.1f, len * 0.5f + 0.12f, 0.1f), Wood);
                leg.localRotation = Quaternion.FromToRotation(Vector3.up, (apex - foot).normalized);
            }
            Part(PrimitiveType.Cylinder, _tripod, "Pulley", apex + new Vector3(0f, -0.12f, 0f), new Vector3(0.26f, 0.05f, 0.26f), DarkSteel, new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cylinder, _tripod, "Rope", new Vector3(0f, (H + 1.45f) * 0.5f, 0f), new Vector3(0.025f, (H - 1.45f) * 0.5f, 0.025f), Rope);
            Part(PrimitiveType.Cube, _tripod, "Hook", new Vector3(0f, 1.4f, 0f), new Vector3(0.08f, 0.14f, 0.04f), DarkSteel);
            // 옆에 놓인 예비 파이프 두 개
            Part(PrimitiveType.Cylinder, _tripod, "SparePipe", new Vector3(-0.9f, 0.07f, -0.55f), new Vector3(0.12f, 0.6f, 0.12f), Steel, new Vector3(90f, 35f, 0f));
            Part(PrimitiveType.Cylinder, _tripod, "SparePipe", new Vector3(-0.78f, 0.07f, -0.72f), new Vector3(0.12f, 0.55f, 0.12f), Steel, new Vector3(90f, 30f, 0f));

            // 손 펌프 (구리): 펌프질할 때만 유정 머리 위에 끼워진다
            _pump = Empty(_root, "HandPump", new Vector3(0f, 0.62f, 0f));
            Part(PrimitiveType.Cylinder, _pump, "Body", new Vector3(0f, 0.25f, 0f), new Vector3(0.24f, 0.25f, 0.24f), Copper);
            Part(PrimitiveType.Cylinder, _pump, "Cap", new Vector3(0f, 0.52f, 0f), new Vector3(0.28f, 0.03f, 0.28f), DarkSteel);
            Part(PrimitiveType.Cube, _pump, "Spout", new Vector3(0f, 0.36f, 0.32f), new Vector3(0.07f, 0.07f, 0.5f), Copper);
            Part(PrimitiveType.Cube, _pump, "SpoutTip", new Vector3(0f, 0.31f, 0.56f), new Vector3(0.07f, 0.12f, 0.07f), Copper);
            _handle = Empty(_pump, "HandlePivot", new Vector3(0f, 0.58f, -0.08f));
            Part(PrimitiveType.Cylinder, _handle, "Lever", new Vector3(0f, 0.05f, -0.32f), new Vector3(0.04f, 0.34f, 0.04f), DarkSteel, new Vector3(80f, 0f, 0f));
            Part(PrimitiveType.Sphere, _handle, "Grip", new Vector3(0f, 0.11f, -0.66f), new Vector3(0.09f, 0.09f, 0.09f), Red);
            Part(PrimitiveType.Cylinder, _handle, "Rod", new Vector3(0f, -0.1f, 0.08f), new Vector3(0.03f, 0.1f, 0.03f), Steel);

            // 양동이 + 안의 기름
            _bucket = Empty(_root, "Bucket", new Vector3(0f, 0f, 0.6f));
            Part(PrimitiveType.Cylinder, _bucket, "Pail", new Vector3(0f, 0.17f, 0f), new Vector3(0.36f, 0.17f, 0.36f), BucketColor);
            Part(PrimitiveType.Cylinder, _bucket, "Hoop", new Vector3(0f, 0.27f, 0f), new Vector3(0.385f, 0.02f, 0.385f), DarkSteel);   // 양동이 테 (위 뚜껑을 덮지 않게 아래쪽에)
            Part(PrimitiveType.Cylinder, _bucket, "Inside", new Vector3(0f, 0.343f, 0f), new Vector3(0.32f, 0.004f, 0.32f), DarkSteel);   // 위에서 보이는 안쪽 바닥
            _bucketOil = Part(PrimitiveType.Cylinder, _bucket, "Oil", new Vector3(0f, 0.347f, 0f), new Vector3(0.05f, 0.004f, 0.05f), OilColor);

            // 주둥이에서 양동이로 떨어지는 기름 줄기
            _stream = Part(PrimitiveType.Cylinder, _root, "OilStream", Vector3.zero, new Vector3(0.05f, 0.3f, 0.05f), OilColor);

            _pump.localScale = Vector3.zero;
            _bucket.gameObject.SetActive(false);
            _stream.gameObject.SetActive(false);
        }

        // ───────── 연출 ─────────

        private void OnHarvested()
        {
            // 다 퍼 올렸다: 주둥이 앞에서 기름이 크게 튀고 양동이는 곧 비워진다 (기름은 가방으로)
            Vector3 spout = SpoutWorld();
            ActionFx.Burst(spout, OilColor, 22, 3.6f, 0.11f, 1.1f, 2.2f, 0.9f, false, 0.15f);
            ActionFx.Burst(_bucket.position + Vector3.up * 0.4f, new Color(0.12f, 0.1f, 0.08f), 10, 2.2f, 0.08f, 0.9f, 2.0f, 0.8f, false, 0.2f);
            _drain = 1f;
        }

        private Vector3 SpoutWorld() => _pump.TransformPoint(new Vector3(0f, 0.25f, 0.58f));

        private bool LocalPumpingThis(out float progress)
        {
            progress = 0f;
            if (_interact == null)
            {
                GameObject player = GatherRules.Player;
                if (player != null) _interact = player.GetComponent<LastTruck.PlayerInteract>();
                if (_interact == null) return false;
            }
            if (!_interact.IsHolding || !ReferenceEquals(_interact.CurrentInteractable, _node)) return false;
            progress = _interact.HoldProgress01;
            return true;
        }

        private void Update()
        {
            if (_root == null || _node == null) return;
            float dt = Time.deltaTime;

            bool suppressed = _node.Suppressed;
            bool depleted = _node.IsDepleted;
            if (_tripod.gameObject.activeSelf == suppressed) _tripod.gameObject.SetActive(!suppressed);

            float progress = 0f;
            bool pumping = !suppressed && !depleted && LocalPumpingThis(out progress);
            _pumping = pumping;

            // 펌프 설치/해제 (톡 튀어나오며 끼워진다)
            _install = Mathf.MoveTowards(_install, pumping || _drain > 0f ? 1f : 0f, dt * (pumping ? 5f : 2.2f));
            float s = _install <= 0f ? 0f : 1f + Mathf.Sin(_install * Mathf.PI) * 0.18f;
            _pump.localScale = Vector3.one * (_install * s);
            bool showBucket = _install > 0.01f;
            if (_bucket.gameObject.activeSelf != showBucket) _bucket.gameObject.SetActive(showBucket);
            if (showBucket) _bucket.localScale = Vector3.one * Mathf.Clamp01(_install * 1.4f);

            // 손잡이 펌프질: 내릴 때 주둥이에서 기름이 쏟아진다
            if (pumping)
            {
                _stroke += dt * 9f;
                float sn = Mathf.Sin(_stroke);
                _handle.localRotation = Quaternion.Euler(sn * 24f, 0f, 0f);
                _casing.localPosition = new Vector3(0f, Mathf.Abs(sn) * 0.012f, 0f);   // 유정 머리가 덜컹인다
                if (_lastStrokeSin > 0f && sn <= 0f)
                {
                    ActionFx.Burst(SpoutWorld(), OilColor, 6, 1.6f, 0.07f, 0.6f, 2.4f, 0.2f, false, 0.05f);
                    ActionFx.Burst(transform.position + Vector3.up * 0.05f, OilColor, 3, 1.0f, 0.06f, 0.6f, 1.8f, 1f, false, 0.6f);
                }
                _lastStrokeSin = sn;
                _fill = Mathf.Max(_fill, Mathf.Clamp01(progress));
            }
            else
            {
                _handle.localRotation = Quaternion.Slerp(_handle.localRotation, Quaternion.identity, dt * 6f);
                _casing.localPosition = Vector3.zero;
                if (_drain > 0f)
                {
                    _drain = Mathf.Max(0f, _drain - dt);
                    _fill = Mathf.MoveTowards(_fill, 0f, dt * 1.3f);
                }
                else _fill = Mathf.MoveTowards(_fill, 0f, dt * 0.8f);
            }

            // 양동이 속 기름 높이
            // 양동이 속 기름: 위에서 보면 검은 기름이 안쪽 바닥을 점점 덮어 간다
            if (showBucket)
            {
                float d = 0.05f + _fill * 0.27f;
                _bucketOil.localScale = new Vector3(d, 0.004f, d);
            }

            // 기름 줄기: 펌프질하는 동안 주둥이에서 양동이 바닥까지, 굵기가 출렁인다
            bool streamOn = pumping && _install > 0.9f;
            if (_stream.gameObject.activeSelf != streamOn) _stream.gameObject.SetActive(streamOn);
            if (streamOn)
            {
                Vector3 top = SpoutWorld();
                Vector3 bottom = _bucket.TransformPoint(new Vector3(0f, 0.34f, 0f));
                Vector3 mid = (top + bottom) * 0.5f;
                float len = Mathf.Max(0.05f, top.y - bottom.y);
                float w = 0.045f + Mathf.Abs(Mathf.Sin(_stroke)) * 0.05f;
                _stream.position = new Vector3(top.x, mid.y, top.z);
                _stream.rotation = Quaternion.identity;
                Vector3 parentScale = _root.lossyScale;
                _stream.localScale = new Vector3(w / parentScale.x, len * 0.5f / parentScale.y, w / parentScale.z);
            }

            // 웅덩이에서 가끔 기름 방울이 부글거린다 (고갈이면 멈춤)
            if (!depleted && Time.time >= _nextBubble)
            {
                _nextBubble = Time.time + (pumping ? Random.Range(0.25f, 0.5f) : Random.Range(1.5f, 4f));
                Vector2 off = Random.insideUnitCircle * 1.1f;
                Vector3 at = transform.position + new Vector3(off.x, 0.06f, off.y);
                ActionFx.Burst(at, OilColor, pumping ? 4 : 2, 0.7f, 0.07f, 0.55f, 1.6f, 1f, false, 0.05f);
            }
        }
    }
}
