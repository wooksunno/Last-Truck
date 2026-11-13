using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭에 끼운 파츠(엔진 / 전면 / 장갑)를 트럭 몸체에 눈에 보이게 붙인다. (지붕의 포탑은 TruckTurret이 따로 만든다.)
    ///
    ///  - 부착 위치는 트럭 프리팹의 Mount_Engine / Mount_Front / Mount_ArmorL / Mount_ArmorR 빈 오브젝트 자리다.
    ///    위치를 옮기면 장착 모델도 같이 옮겨진다 (없으면 기본 위치).
    ///  - 파츠를 바꾸면 이전 모델은 지우고 새 모델을 만든다. 빼면 모델도 사라진다.
    ///  - 모델은 기본 도형으로 만든 간단한 것이다 (충돌은 없다 → 트럭 물리는 그대로). 효과 자체는 TruckUpgradeState가 적용한다.
    ///  - 멀티플레이: 클라이언트도 호스트의 파츠 값을 TruckUpgradeState.SetState로 받아 Changed가 불리므로 똑같이 보인다.
    /// TruckInformation이 자동으로 붙인다.
    /// </summary>
    public class TruckPartVisuals : MonoBehaviour
    {
        private TruckUpgradeState _state;
        private Rigidbody _rb;
        private readonly Dictionary<TruckPartSlot, Transform> _roots = new Dictionary<TruckPartSlot, Transform>();
        private readonly Dictionary<TruckPartSlot, string> _built = new Dictionary<TruckPartSlot, string>();
        private Transform _spinner;          // 분쇄 휠: 달리는 속도에 맞춰 돈다
        private readonly List<Transform> _glow = new List<Transform>();
        private static readonly Dictionary<Color, Material> LitCache = new Dictionary<Color, Material>();
        private static readonly Dictionary<Color, Material> GlowCache = new Dictionary<Color, Material>();

        private static readonly TruckPartSlot[] Slots = { TruckPartSlot.Engine, TruckPartSlot.Front, TruckPartSlot.Armor };

        private void Awake()
        {
            _state = GetComponent<TruckUpgradeState>();
            _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable() { if (_state != null) _state.Changed += Refresh; }
        private void OnDisable() { if (_state != null) _state.Changed -= Refresh; }
        private void Start() => Refresh();

        private void Update()
        {
            if (_spinner != null)
            {
                float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
                _spinner.Rotate(Vector3.right, (180f + speed * 60f) * Time.deltaTime, Space.Self);
            }
            if (_glow.Count > 0)
            {
                // 터보 배기구 불빛이 숨 쉬듯 깜빡인다
                float k = 0.75f + 0.25f * Mathf.Sin(Time.time * 14f);
                foreach (Transform t in _glow) if (t != null) t.localScale = _glowBase * k;
            }
        }

        private Vector3 _glowBase = new Vector3(0.12f, 0.12f, 0.12f);

        private void Refresh()
        {
            if (_state == null) return;
            foreach (TruckPartSlot slot in Slots)
            {
                string id = _state.PartAt(slot);
                _built.TryGetValue(slot, out string current);
                if (id == current) continue;

                if (_roots.TryGetValue(slot, out Transform old) && old != null) Destroy(old.gameObject);
                _roots.Remove(slot);
                _built[slot] = id;
                if (slot == TruckPartSlot.Front) _spinner = null;
                if (slot == TruckPartSlot.Engine) _glow.Clear();
                if (string.IsNullOrEmpty(id)) continue;

                Transform root = Build(slot, id);
                if (root != null) _roots[slot] = root;
            }
        }

        // ---------------------------------------------------------------- 모델

        private Transform Build(TruckPartSlot slot, string id)
        {
            switch (id)
            {
                case ItemIds.TunedEngine: return BuildEngine(false);
                case ItemIds.TurboEngine: return BuildEngine(true);
                case ItemIds.SpikeBumper: return BuildFront(false);
                case ItemIds.GrinderWheel: return BuildFront(true);
                case ItemIds.TruckCompositeArmor: return BuildArmor(false);
                case ItemIds.AlloyArmor: return BuildArmor(true);
            }
            return null;
        }

        private Transform MakeRoot(string name, string mountName, Vector3 fallbackLocal)
        {
            var go = new GameObject(name).transform;
            go.SetParent(transform, false);
            Transform mount = transform.Find(mountName);
            go.localPosition = mount != null ? mount.localPosition : fallbackLocal;
            go.localRotation = Quaternion.identity;
            return go;
        }

        /// <summary>엔진: 본네트 위 흡기구 + 운전석 뒤 배기관. 터보는 더 크고 배기구에 불빛이 난다.</summary>
        private Transform BuildEngine(bool turbo)
        {
            Transform root = MakeRoot("Part_Engine", "Mount_Engine", new Vector3(0f, 1.32f, 2.1f));
            Material dark = Lit(new Color(0.18f, 0.19f, 0.21f));
            Material steel = Lit(new Color(0.62f, 0.65f, 0.7f));
            Material accent = Lit(turbo ? new Color(0.85f, 0.25f, 0.12f) : new Color(0.85f, 0.7f, 0.2f));

            // 흡기구 (본네트 가운데)
            Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.12f, 0f), new Vector3(turbo ? 0.9f : 0.7f, 0.24f, turbo ? 0.8f : 0.6f), dark);
            Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.27f, 0.12f), new Vector3(turbo ? 0.7f : 0.5f, 0.06f, turbo ? 0.5f : 0.36f), accent);
            if (turbo)
            {
                // 터보 달팽이관
                Prim(PrimitiveType.Cylinder, root, new Vector3(0.45f, 0.2f, -0.05f), new Vector3(0.34f, 0.14f, 0.34f), steel).localRotation = Quaternion.Euler(0f, 0f, 90f);
                Prim(PrimitiveType.Sphere, root, new Vector3(0.62f, 0.2f, -0.05f), new Vector3(0.26f, 0.26f, 0.26f), accent);
            }

            // 배기관: 운전석 뒤 양옆(터보) / 오른쪽 하나(개조)
            Vector3[] pipes = turbo ? new[] { new Vector3(-1.02f, -0.1f, -1.55f), new Vector3(1.02f, -0.1f, -1.55f) } : new[] { new Vector3(1.02f, -0.1f, -1.55f) };
            foreach (Vector3 p in pipes)
            {
                Prim(PrimitiveType.Cylinder, root, p + new Vector3(0f, 0.55f, 0f), new Vector3(0.11f, 0.7f, 0.11f), steel);
                Prim(PrimitiveType.Cylinder, root, p + new Vector3(0f, 1.27f, 0f), new Vector3(0.15f, 0.06f, 0.15f), dark);
                if (turbo)
                {
                    Transform g = Prim(PrimitiveType.Sphere, root, p + new Vector3(0f, 1.31f, 0f), _glowBase, Glow(new Color(1f, 0.55f, 0.15f)));
                    _glow.Add(g);
                }
            }
            return root;
        }

        /// <summary>전면: 범퍼 앞의 강철가시 / 회전 분쇄 휠.</summary>
        private Transform BuildFront(bool grinder)
        {
            Transform root = MakeRoot("Part_Front", "Mount_Front", new Vector3(0f, 0.55f, 3.12f));
            Material dark = Lit(new Color(0.2f, 0.21f, 0.23f));
            Material steel = Lit(new Color(0.7f, 0.72f, 0.76f));

            if (!grinder)
            {
                // 가로 철봉 + 앞으로 뻗은 가시
                Prim(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0f), new Vector3(2.5f, 0.22f, 0.18f), dark);
                for (int i = -4; i <= 4; i++)
                {
                    var spike = Prim(PrimitiveType.Cube, root, new Vector3(i * 0.28f, 0f, 0.28f), new Vector3(0.1f, 0.1f, 0.5f), steel);
                    spike.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }
            }
            else
            {
                // 받침대 + 가로로 누운 큰 원통에 날이 둘러 달린 분쇄 휠
                Prim(PrimitiveType.Cube, root, new Vector3(0f, 0f, -0.1f), new Vector3(2.5f, 0.2f, 0.16f), dark);
                var spinner = new GameObject("Spinner").transform;
                spinner.SetParent(root, false);
                spinner.localPosition = new Vector3(0f, 0.05f, 0.4f);
                _spinner = spinner;
                Prim(PrimitiveType.Cylinder, spinner, Vector3.zero, new Vector3(0.62f, 1.15f, 0.62f), dark).localRotation = Quaternion.Euler(0f, 0f, 90f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 2f / 8f;
                    var blade = Prim(PrimitiveType.Cube, spinner, new Vector3(0f, Mathf.Sin(a) * 0.38f, Mathf.Cos(a) * 0.38f), new Vector3(2.2f, 0.07f, 0.16f), steel);
                    blade.localRotation = Quaternion.Euler(-a * Mathf.Rad2Deg, 0f, 0f);
                }
            }
            return root;
        }

        /// <summary>장갑: 양쪽 문 바깥에 판을 붙인다. 합금은 더 크고 어두운 판 + 아래 치마.</summary>
        private Transform BuildArmor(bool alloy)
        {
            var root = new GameObject("Part_Armor").transform;
            root.SetParent(transform, false);
            Material plate = Lit(alloy ? new Color(0.27f, 0.29f, 0.33f) : new Color(0.5f, 0.55f, 0.6f));
            Material rivet = Lit(alloy ? new Color(0.65f, 0.67f, 0.72f) : new Color(0.78f, 0.8f, 0.84f));

            for (int side = 0; side < 2; side++)
            {
                string mountName = side == 0 ? "Mount_ArmorL" : "Mount_ArmorR";
                float sign = side == 0 ? -1f : 1f;
                Transform mount = transform.Find(mountName);
                Vector3 pos = mount != null ? mount.localPosition : new Vector3(0.97f * sign, 1.25f, 1.08f);

                var holder = new GameObject("Side").transform;
                holder.SetParent(root, false);
                holder.localPosition = pos;

                // 문 아래쪽을 덮는 큰 판 (창문은 가리지 않는다)
                Prim(PrimitiveType.Cube, holder, new Vector3(0f, alloy ? -0.12f : -0.18f, 0f), new Vector3(0.07f, alloy ? 0.85f : 0.65f, alloy ? 1.05f : 0.9f), plate);
                // 모서리 리벳
                for (int ix = -1; ix <= 1; ix += 2)
                    for (int iy = -1; iy <= 1; iy += 2)
                        Prim(PrimitiveType.Sphere, holder, new Vector3(0.04f * sign, (alloy ? -0.12f : -0.18f) + iy * (alloy ? 0.36f : 0.27f), ix * (alloy ? 0.45f : 0.38f)), new Vector3(0.09f, 0.09f, 0.09f), rivet);
                if (alloy)
                {
                    // 문 아래 치마 + 바퀴 위 덮개
                    Prim(PrimitiveType.Cube, holder, new Vector3(-0.02f * sign, -0.7f, 0f), new Vector3(0.1f, 0.22f, 1.5f), plate);
                    Prim(PrimitiveType.Cube, holder, new Vector3(-0.02f * sign, -0.45f, 1.55f), new Vector3(0.1f, 0.5f, 0.7f), plate);
                }
            }
            return root;
        }

        // ---------------------------------------------------------------- 도형 / 재질

        private static Transform Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);   // 장식일 뿐: 트럭 물리/충돌에 끼어들지 않는다
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            return go.transform;
        }

        private static Material Lit(Color c)
        {
            if (LitCache.TryGetValue(c, out Material m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
            LitCache[c] = m;
            return m;
        }

        private static Material Glow(Color c)
        {
            if (GlowCache.TryGetValue(c, out Material m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            m = new Material(shader) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            GlowCache[c] = m;
            return m;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { LitCache.Clear(); GlowCache.Clear(); }
    }
}
