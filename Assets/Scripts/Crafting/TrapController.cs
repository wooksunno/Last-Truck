using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 손에 유인 미끼 트랩을 든 채로 상호작용 키(E)를 꾹 누르면 캐릭터 발밑에 트랩을 설치한다.
    /// 진행률은 GatherProgressUI의 원형 게이지로 표시된다.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class TrapController : MonoBehaviour
    {
        [SerializeField] private float placeHoldSeconds = 1.2f;
        [SerializeField] private int trapDamage = 15;
        [SerializeField] private float trapRootSeconds = 2f;
        [SerializeField] private float trapTriggerRadius = 0.6f;

        private PlayerInventory _inventory;
        private LastTruck.PlayerInteract _interact;
        private float _holdTimer;
        private bool _pressVetoed;

        public bool IsHolding { get; private set; }
        public float HoldProgress01 { get; private set; }

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _interact = GetComponent<LastTruck.PlayerInteract>();
        }

        private void Update()
        {
            InventorySlot hand = _inventory.SelectedSlot;
            bool hasTrap = hand != null && !hand.IsEmpty && hand.item != null &&
                           hand.item.itemID == ItemIds.LureTrap;

            bool nearbyWorldInteractable = _interact != null && _interact.HasNearbyInteractable;
            bool popupOpen = GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen;
            bool canPlace = hasTrap && !nearbyWorldInteractable && !popupOpen;

            if (Input.GetKeyDown(KeyCode.E))
                _pressVetoed = !canPlace;
            if (Input.GetKeyUp(KeyCode.E))
                _pressVetoed = false;

            bool holding = canPlace && !_pressVetoed && Input.GetKey(KeyCode.E);

            if (!holding)
            {
                _holdTimer = 0f;
                IsHolding = false;
                HoldProgress01 = 0f;
                return;
            }

            _holdTimer += Time.deltaTime;
            IsHolding = true;
            HoldProgress01 = Mathf.Clamp01(_holdTimer / placeHoldSeconds);

            if (_holdTimer >= placeHoldSeconds)
            {
                PlaceTrap(hand.item);
                _holdTimer = 0f;
                IsHolding = false;
                HoldProgress01 = 0f;
                _pressVetoed = true;
            }
        }

        private void PlaceTrap(ItemData item)
        {
            if (!_inventory.RemoveItem(item, 1))
                return;

            GameObject trapGO = BuildTrapVisual();
            trapGO.transform.position = transform.position + Vector3.up * 0.02f;

            TrapPlaced placed = trapGO.AddComponent<TrapPlaced>();
            placed.Setup(trapDamage, trapRootSeconds, trapTriggerRadius, transform);

            Debug.Log($"[TrapController] {item.itemName}을(를) 설치했습니다.");
        }

        private static GameObject BuildTrapVisual()
        {
            GameObject root = new GameObject("LureTrap");

            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plate.name = "Plate";
            plate.transform.SetParent(root.transform, false);
            plate.transform.localScale = new Vector3(0.5f, 0.03f, 0.5f);
            Object.Destroy(plate.GetComponent<Collider>());
            ApplyColor(plate, new Color(0.25f, 0.2f, 0.15f));

            for (int i = 0; i < 6; i++)
            {
                GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spike.name = "Spike";
                spike.transform.SetParent(root.transform, false);
                float angle = i * 60f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.18f;
                spike.transform.localPosition = offset + Vector3.up * 0.05f;
                spike.transform.localScale = new Vector3(0.04f, 0.08f, 0.04f);
                Object.Destroy(spike.GetComponent<Collider>());
                ApplyColor(spike, new Color(0.5f, 0.45f, 0.4f));
            }

            return root;
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
