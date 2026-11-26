using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 랜턴이 가방(인벤토리 어느 칸이든)에 있으면 허리춤에 매달려 주변을 환하게 밝히고, 안개/비 같은 날씨로 흐려진 시야를 걷어 준다
    /// (WeatherDirector가 LitAmount를 읽는다). 손에 들 필요가 없어서 무기/도구를 같이 쓸 수 있다. 다 타지 않는다.
    /// PlayerInventory가 자동으로 붙인다. 빛은 내 화면에서만 보인다(멀티플레이에서 다른 사람에게 복제되지 않음).
    /// </summary>
    [DefaultExecutionOrder(300)]   // ActionPose(200)가 뼈를 움직인 뒤에 허리 위치를 읽는다
    [RequireComponent(typeof(PlayerInventory))]
    public class HeldLantern : MonoBehaviour
    {
        private static float _lit;      // 0..1 (부드럽게 켜지고 꺼진다)

        /// <summary>랜턴이 켜진 정도 (0 ~ 1).</summary>
        public static float LitAmount => _lit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _lit = 0f; }

        private PlayerInventory _inventory;
        private Light _light;
        private GameObject _model;
        private float _glowAmount;
        private float _seed;
        private Animator _anim;
        private Transform _hips;
        private Vector3 _lastAnchor;
        private Vector3 _swing;
        private bool _hasLantern;
        private float _nextCheck;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _seed = Random.value * 10f;
        }

        private void OnEnable()
        {
            if (_inventory == null) _inventory = GetComponent<PlayerInventory>();
            if (_inventory != null) _inventory.Changed += Recheck;
            Recheck();
        }

        private void OnDisable()
        {
            if (_inventory != null) _inventory.Changed -= Recheck;
            _lit = 0f;
        }

        private void Recheck()
        {
            _hasLantern = false;
            if (_inventory == null) return;
            foreach (InventorySlot s in _inventory.Slots)
                if (s != null && !s.IsEmpty && s.item != null && s.item.itemID == ItemIds.Lantern) { _hasLantern = true; break; }
        }

        private void EnsureVisual()
        {
            if (_model != null) return;
            _model = new GameObject("LanternModel");
            _model.transform.SetParent(transform, false);
            _model.transform.localPosition = new Vector3(0.52f, 0.85f, 0.1f);
            _model.transform.localScale = Vector3.one * 0.72f;   // 허리에 차는 작은 랜턴

            Material metal = Unlit(new Color(0.16f, 0.15f, 0.14f));
            Material glass = Unlit(new Color(1f, 0.93f, 0.7f));

            Part(PrimitiveType.Cylinder, "Cap", new Vector3(0f, 0.2f, 0f), new Vector3(0.2f, 0.03f, 0.2f), metal);
            Part(PrimitiveType.Cylinder, "Base", new Vector3(0f, -0.2f, 0f), new Vector3(0.2f, 0.03f, 0.2f), metal);
            Part(PrimitiveType.Cylinder, "Handle", new Vector3(0f, 0.3f, 0f), new Vector3(0.03f, 0.1f, 0.03f), metal);
            Part(PrimitiveType.Sphere, "Glow", Vector3.zero, new Vector3(0.2f, 0.3f, 0.2f), glass);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                Part(PrimitiveType.Cylinder, "Bar" + i, new Vector3(Mathf.Cos(a) * 0.105f, 0f, Mathf.Sin(a) * 0.105f), new Vector3(0.012f, 0.2f, 0.012f), metal);
            }

            // 빛은 모델 크기와 상관없이 같은 범위로 (모델만 작게)
            var holder = new GameObject("LanternLight");
            holder.transform.SetParent(transform, false);
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
            // 가방 내용이 바뀔 때 다시 확인하지만, 혹시 놓친 경우를 위해 가끔 한 번 더 본다
            if (Time.time >= _nextCheck) { _nextCheck = Time.time + 1f; Recheck(); }
            if (_hasLantern) EnsureVisual();

            _glowAmount = Mathf.MoveTowards(_glowAmount, _hasLantern ? 1f : 0f, Time.deltaTime * 2.5f);   // 켜고 끌 때 부드럽게
            _lit = _glowAmount;   // 내 캐릭터(입력을 받는 쪽)만 이 컴포넌트가 켜져 있다

            if (_model == null) return;
            bool on = _glowAmount > 0.01f;
            _model.SetActive(on);
            _light.gameObject.SetActive(on);
            if (!on) return;

            float f = Mathf.PerlinNoise(Time.time * 3f, _seed);
            _light.intensity = (3.0f + f * 0.7f) * _glowAmount;
        }

        // 허리 뼈는 애니메이션이 끝난 뒤(LateUpdate)에 정해지므로 여기서 따라간다
        private void LateUpdate()
        {
            if (_model == null || _glowAmount <= 0.01f) return;

            if (_hips == null)
            {
                if (_anim == null) _anim = GetComponentInChildren<Animator>();
                if (_anim != null && _anim.isHuman) _hips = _anim.GetBoneTransform(HumanBodyBones.Hips);
            }

            // 오른쪽 허리춤에 매달려, 걸으면 시계추처럼 뒤늦게 흔들린다
            Vector3 anchor = _hips != null
                ? _hips.position + transform.right * 0.52f + transform.forward * 0.1f + Vector3.up * 0.06f
                : transform.position + transform.right * 0.52f + Vector3.up * 0.95f;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 vel = (anchor - _lastAnchor) / dt;
            _lastAnchor = anchor;
            if (vel.sqrMagnitude > 400f) vel = Vector3.zero;   // 순간이동/첫 프레임 값은 무시
            _swing = Vector3.Lerp(_swing, -vel * 0.05f, 1f - Mathf.Exp(-7f * dt));
            Vector3 dir = (Vector3.down + new Vector3(_swing.x, 0f, _swing.z)).normalized;
            _model.transform.position = anchor + dir * 0.2f;
            _model.transform.rotation = Quaternion.FromToRotation(Vector3.down, dir) * Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            _light.transform.position = _model.transform.position + Vector3.up * 0.15f;
        }
    }
}
