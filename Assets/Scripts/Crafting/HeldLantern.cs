using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 랜턴을 1/2/3 키로 손에 들면 주변을 환하게 밝히고, 안개/비 같은 날씨로 흐려진 시야를 걷어 준다 (WeatherDirector가 LitAmount를 읽는다).
    /// 횃불(HeldTorch)보다 넓고 안정적인 흰빛이고 다 타지 않는다. 대신 손을 쓰므로 무기를 같이 들 수 없다.
    /// PlayerInventory가 자동으로 붙인다. 빛은 내 화면에서만 보인다(멀티플레이에서 다른 사람에게 복제되지 않음).
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class HeldLantern : MonoBehaviour
    {
        private static float _lit;      // 0..1 (부드럽게 켜지고 꺼진다)

        /// <summary>랜턴을 든 정도 (0 ~ 1). 횃불은 작게(0.35) 보탠다.</summary>
        public static float LitAmount { get { return Mathf.Max(_lit, HeldTorch.Held ? 0.35f : 0f); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _lit = 0f; }

        private PlayerInventory _inventory;
        private Light _light;
        private GameObject _model;
        private Renderer _glow;
        private float _glowAmount;
        private float _seed;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _seed = Random.value * 10f;
        }

        private void OnDisable() { _lit = 0f; }

        private bool Holding()
        {
            InventorySlot hand = _inventory != null ? _inventory.SelectedSlot : null;
            return hand != null && !hand.IsEmpty && hand.item != null && hand.item.itemID == ItemIds.Lantern;
        }

        private void EnsureVisual()
        {
            if (_model != null) return;
            _model = new GameObject("LanternModel");
            _model.transform.SetParent(transform, false);
            _model.transform.localPosition = new Vector3(0.48f, 1.05f, 0.42f);   // 오른손 아래로 늘어뜨린다

            Material metal = Unlit(new Color(0.16f, 0.15f, 0.14f));
            Material glass = Unlit(new Color(1f, 0.93f, 0.7f));

            Part(PrimitiveType.Cylinder, "Cap", new Vector3(0f, 0.2f, 0f), new Vector3(0.2f, 0.03f, 0.2f), metal);
            Part(PrimitiveType.Cylinder, "Base", new Vector3(0f, -0.2f, 0f), new Vector3(0.2f, 0.03f, 0.2f), metal);
            Part(PrimitiveType.Cylinder, "Handle", new Vector3(0f, 0.3f, 0f), new Vector3(0.03f, 0.1f, 0.03f), metal);
            GameObject flame = Part(PrimitiveType.Sphere, "Glow", Vector3.zero, new Vector3(0.2f, 0.3f, 0.2f), glass);
            _glow = flame.GetComponent<Renderer>();
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                Part(PrimitiveType.Cylinder, "Bar" + i, new Vector3(Mathf.Cos(a) * 0.105f, 0f, Mathf.Sin(a) * 0.105f), new Vector3(0.012f, 0.2f, 0.012f), metal);
            }

            var holder = new GameObject("LanternLight");
            holder.transform.SetParent(_model.transform, false);
            holder.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            _light = holder.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.93f, 0.76f);
            _light.range = 24f;
            _light.intensity = 3.4f;
            _light.shadows = LightShadows.None;
        }

        private GameObject Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_model.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static Material Unlit(Color c)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            return m;
        }

        private void Update()
        {
            bool holding = Holding();
            if (holding) EnsureVisual();

            _glowAmount = Mathf.MoveTowards(_glowAmount, holding ? 1f : 0f, Time.deltaTime * 2.5f);   // 켜고 끌 때 부드럽게
            _lit = _glowAmount;   // 내 캐릭터(입력을 받는 쪽)만 이 컴포넌트가 켜져 있다

            if (_model == null) return;
            _model.SetActive(_glowAmount > 0.01f);
            if (_glowAmount <= 0.01f) return;

            // 걸을 때 살짝 흔들리고, 불빛이 은은하게 일렁인다
            float f = Mathf.PerlinNoise(Time.time * 3f, _seed);
            _light.intensity = (3.0f + f * 0.7f) * _glowAmount;
            _model.transform.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 2.2f) * 3f, 0f, Mathf.Cos(Time.time * 1.7f) * 3f);
        }
    }
}
