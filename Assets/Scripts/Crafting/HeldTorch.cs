using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 횃불을 1/2/3 키로 손에 들면 주변을 밝힌다 (어두운 동굴용). 들고 있는 동안만 타고, 다 타면(3분) 1개가 사라진다.
    /// PlayerInventory가 자동으로 붙인다. 빛은 내 화면에서만 보인다(멀티플레이에서 다른 사람에게 복제되지 않음).
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class HeldTorch : MonoBehaviour
    {
        public const float BurnSeconds = 180f;

        private PlayerInventory _inventory;
        private Light _light;
        private GameObject _flame;
        private float _burned;          // 지금 들고 있는 횃불이 탄 시간
        private float _flickerSeed;

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
        }

        private void Update()
        {
            bool holding = HoldingTorch();
            if (holding) EnsureVisual();
            if (_light != null)
            {
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
