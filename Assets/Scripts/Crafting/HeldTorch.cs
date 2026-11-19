using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 횃불을 1/2/3 키로 손에 들면 주변을 밝힌다 (어두운 동굴용). 들고 있는 동안만 타고, 다 타면(3분) 1개가 사라진다.
    /// PlayerInventory가 자동으로 붙인다. 빛은 내 화면에서만 보인다(멀티플레이에서 다른 사람에게 복제되지 않음).
    /// </summary>
    [DefaultExecutionOrder(300)]   // ActionPose(200)가 팔을 쳐든 뒤에 손 위치를 읽는다
    [RequireComponent(typeof(PlayerInventory))]
    public class HeldTorch : MonoBehaviour
    {
        public const float BurnSeconds = 180f;

        /// <summary>횃불을 들고 있는가 (내 화면). 날씨 안개가 약간 걷힌다 (HeldLantern.LitAmount).</summary>
        public static bool Held { get; private set; }

        private void OnDisable() { Held = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Held = false; }

        private bool _doused;
        private PlayerInventory _inventory;
        private Light _light;
        private GameObject _flame;
        private float _burned;          // 지금 들고 있는 횃불이 탄 시간
        private float _flickerSeed;
        private ActionPose _pose;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _flickerSeed = Random.value * 10f;
        }

        private bool HoldingTorch()
        {
            InventorySlot hand = _inventory.SelectedSlot;
            return hand != null && !hand.IsEmpty && hand.item != null && hand.item.itemID == ItemIds.Torch;
        }

        private void EnsureVisual()
        {
            if (_light != null) return;
            var holder = new GameObject("TorchLight");
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = new Vector3(0.45f, 1.7f, 0.35f);   // 오른손 위쪽

            _light = holder.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.62f, 0.28f);
            _light.range = 13f;
            _light.intensity = 2.4f;
            _light.shadows = LightShadows.None;

            // 작은 불꽃 덩어리 (빛나는 공)
            _flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(_flame.GetComponent<Collider>());
            _flame.transform.SetParent(holder.transform, false);
            _flame.transform.localScale = Vector3.one * 0.22f;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 0.7f, 0.25f)); else mat.color = new Color(1f, 0.7f, 0.25f);
            _flame.GetComponent<Renderer>().sharedMaterial = mat;
            _flame.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 손잡이 나무 막대 (불꽃 아래로 손까지)
            GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(stick.GetComponent<Collider>());
            stick.transform.SetParent(holder.transform, false);
            stick.transform.localPosition = new Vector3(0f, -0.22f, 0f);
            stick.transform.localScale = new Vector3(0.05f, 0.24f, 0.05f);
            var stickMat = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            if (stickMat.HasProperty("_BaseColor")) stickMat.SetColor("_BaseColor", new Color(0.36f, 0.22f, 0.12f)); else stickMat.color = new Color(0.36f, 0.22f, 0.12f);
            stick.GetComponent<Renderer>().sharedMaterial = stickMat;
            stick.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 팔을 쳐든 손 위로 불꽃이 따라온다 (손 뼈는 애니메이션이 끝난 뒤에 정해지므로 LateUpdate)
        private void LateUpdate()
        {
            if (_light == null || !_light.gameObject.activeSelf || _pose == null || _pose.RightHand == null) return;
            _light.transform.position = _pose.RightHand.position + Vector3.up * 0.44f;
        }

        private void Update()
        {
            bool holding = HoldingTorch();
            // 비가 오면 횃불이 꺼진다 (랜턴은 괜찮다). 비가 그치면 다시 켜지고, 꺼진 동안은 타지 않는다.
            if (holding && LastTruck.WeatherDirector.TorchDoused)
            {
                if (!_doused) LastTruck.HudToast.Show("비 때문에 횃불이 꺼졌어요 (랜턴은 쓸 수 있어요)");
                _doused = true;
                holding = false;
            }
            else if (!LastTruck.WeatherDirector.TorchDoused) _doused = false;
            Held = holding;
            if (_pose == null) _pose = ActionPose.For(gameObject);
            _pose.SetHold(holding ? ActionPose.HoldKind.Torch : ActionPose.HoldKind.None);
            if (holding) EnsureVisual();
            if (_light != null)
            {
                _light.gameObject.SetActive(holding);   // 불꽃/막대/빛이 함께 켜지고 꺼진다
                _light.enabled = holding;
                _flame.SetActive(holding);
            }
            if (!holding) return;

            // 일렁이는 불빛
            float f = Mathf.PerlinNoise(Time.time * 6f, _flickerSeed);
            _light.intensity = 2.0f + f * 0.9f;
            _flame.transform.localScale = Vector3.one * (0.18f + f * 0.08f);

            _burned += Time.deltaTime;
            if (_burned >= BurnSeconds)
            {
                _burned = 0f;
                _inventory.RemoveItem(_inventory.SelectedSlot.item, 1);
                LastTruck.HudToast.Show(HoldingTorch() ? "횃불 하나가 다 타서 새 횃불에 불을 붙였어요" : "횃불이 다 타서 꺼졌어요");
            }
        }
    }
}
