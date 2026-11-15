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

        // 나무 가시 덫: 미끼 트랩보다 약하지만 나무/돌로 싸게 만든다
        private const int SpikeTrapDamage = 10;
        private const float SpikeTrapRootSeconds = 1.2f;

        /// <summary>손에 들고 E 꾹으로 설치하는 아이템인지 (덫 2종 + 바리케이드 2종).</summary>
        public static bool IsPlaceable(string itemId) =>
            itemId == ItemIds.LureTrap || itemId == ItemIds.WoodSpikeTrap ||
            itemId == ItemIds.WoodBarricade || itemId == ItemIds.StoneWall ||
            itemId == ItemIds.PoisonMushroomBait;

        private void Update()
        {
            InventorySlot hand = _inventory.SelectedSlot;
            bool hasTrap = hand != null && !hand.IsEmpty && hand.item != null &&
                           IsPlaceable(hand.item.itemID);

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

            // 바리케이드/돌 방벽: 캐릭터 앞에 세운다
            if (item.itemID == ItemIds.WoodBarricade || item.itemID == ItemIds.StoneWall)
            {
                Barricade.Kind kind = item.itemID == ItemIds.StoneWall ? Barricade.Kind.Stone : Barricade.Kind.Wood;
                Vector3 pos = Barricade.PlacementPoint(transform);
                float yaw = transform.eulerAngles.y;
                if (!LastTruck.Networking.NetworkGathering.TryPlaceBarricade((int)kind, pos, yaw))
                    Barricade.Spawn(kind, pos, yaw);
                Debug.Log($"[TrapController] {item.itemName}을(를) 설치했습니다.");
                return;
            }

            // 독버섯 미끼: 캐릭터 앞에 놓는다. 주변 몬스터가 플레이어/트럭 대신 몰려와서 먹고 독에 중독된다.
            if (item.itemID == ItemIds.PoisonMushroomBait)
            {
                Vector3 baitPos = Barricade.PlacementPoint(transform);
                if (!LastTruck.Networking.NetworkGathering.TryPlaceBait(baitPos))
                    PoisonBait.Spawn(baitPos, false);
                LastTruck.HudToast.Show("독버섯 미끼를 놓았어요: 근처 몬스터가 몰려와서 먹고 중독됩니다");
                return;
            }

            int damage = trapDamage;
            float rootSeconds = trapRootSeconds;
            if (item.itemID == ItemIds.WoodSpikeTrap) { damage = SpikeTrapDamage; rootSeconds = SpikeTrapRootSeconds; }
            // 유인 미끼 트랩 = 가시 덫에 독성 유인 페로몬을 붙인 것: 주변 몬스터를 덫으로 끌어당긴다
            bool lure = item.itemID == ItemIds.LureTrap;

            // 멀티플레이: 호스트가 덫을 만들어 모두에게 보여주고, 밟았는지 판정도 호스트가 한다 (몬스터는 호스트에서 움직이므로).
            if (LastTruck.Networking.NetworkGathering.TryPlaceTrap(transform.position + Vector3.up * 0.02f, damage, rootSeconds, trapTriggerRadius, lure))
            {
                Debug.Log($"[TrapController] {item.itemName}을(를) 설치했습니다.");
                return;
            }

            GameObject trapGO = BuildTrapVisual(lure);
            trapGO.transform.position = transform.position + Vector3.up * 0.02f;

            TrapPlaced placed = trapGO.AddComponent<TrapPlaced>();
            placed.Setup(damage, rootSeconds, trapTriggerRadius, transform);
            if (lure) placed.EnableLure();

            if (lure) LastTruck.HudToast.Show("페로몬 덫을 놓았어요: 근처 몬스터가 냄새를 따라 덫으로 몰려옵니다");
            Debug.Log($"[TrapController] {item.itemName}을(를) 설치했습니다.");
        }

        public static GameObject BuildTrapVisual(bool lure = false)
        {
            GameObject root = new GameObject("LureTrap");

            // 페로몬을 바른 덫: 바닥에 연두색 독 기운이 은은히 번진다
            if (lure)
            {
                GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                aura.name = "Aura";
                aura.transform.SetParent(root.transform, false);
                aura.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                aura.transform.localScale = new Vector3(1.4f, 0.005f, 1.4f);
                Object.Destroy(aura.GetComponent<Collider>());
                Material m = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.45f, 0.95f, 0.3f, 0.28f) };
                aura.GetComponent<Renderer>().sharedMaterial = m;
            }

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
