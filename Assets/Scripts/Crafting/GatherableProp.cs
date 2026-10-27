using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 맵에 놓인 일반 돌/나무 프리팹을 E키 꾹 누르기(LastTruck.PlayerInteract)로 채집한다.
    /// 채집에 성공하면 인벤토리에 아이템이 들어오고 이 오브젝트(프롭 전체)는 사라진다.
    /// PlayerInteract는 interactLayer(Water, 레이어 4)의 콜라이더만 찾으므로, 프롭의 자식 "GatherTrigger"
    /// (레이어 4, 트리거 콜라이더)에 이 컴포넌트를 붙여 사용한다. 설치는 에디터 메뉴 Tools/Gatherable Props 로 한다.
    /// </summary>
    public class GatherableProp : MonoBehaviour, LastTruck.IHoldInteractable, LastTruck.IInteractPriority, LastTruck.IInteractLabel
    {
        [SerializeField] private string displayName = "채집";
        [SerializeField] private string itemId = ItemIds.Stone;
        [SerializeField] private int amount = 1;
        [Tooltip("E키를 꾹 누르고 있어야 하는 시간(초).")]
        [SerializeField] private float gatherHoldSeconds = 1.5f;
        [Tooltip("채집이 끝나면 사라질 오브젝트. 비어 있으면 부모 오브젝트.")]
        [SerializeField] private GameObject target;

        [Tooltip("채집에 필요한 최소 도구 등급(1/2/3 키로 고른 손 슬롯). 돌은 항상 최소 1(나무 곡괭이) 이상이 필요하다.")]
        [SerializeField] private int requiredTier = 0;

        private bool _done;

        #region 멀티플레이 연동 (LastTruck.Networking.NetworkGathering)

        // 멀티플레이에서는 "누가 먼저 캤는지"를 호스트가 정한다. 캐면 모든 화면에서 같이 사라진다.
        // 프롭은 씬에 같은 자리로 놓여 있으므로 위치로 같은 프롭을 찾는다.

        public static readonly System.Collections.Generic.List<GatherableProp> AllProps =
            new System.Collections.Generic.List<GatherableProp>();

        public bool IsDone => _done;
        public string ItemId => itemId;
        public int Amount => amount;
        /// <summary>채집이 끝나면 사라지는 프롭 전체 (나무 흔들기 연출 대상).</summary>
        public GameObject Root => target != null ? target : (transform.parent != null ? transform.parent.gameObject : gameObject);

        private void Awake() => AllProps.Add(this);
        private void OnDestroy() => AllProps.Remove(this);

        /// <summary>채집된 것으로 처리하고 프롭을 없앤다 (호스트 판정 결과를 모든 컴퓨터에서 반영).</summary>
        public void RemoveGathered()
        {
            if (_done)
                return;
            _done = true;
            GameObject remove = target != null ? target : (transform.parent != null ? transform.parent.gameObject : gameObject);
            SettleNeighbors(remove);
            Destroy(remove);
        }

        /// <summary>
        /// 돌/나무를 없애기 전에 호출: 그 위에 얹혀 있던 약초(풀)가 공중에 뜨지 않도록 땅으로 내려놓는다. 내려놓을 땅이 없으면 같이 지운다.
        /// </summary>
        private static void SettleNeighbors(GameObject removed)
        {
            if (removed == null)
                return;

            Renderer[] renderers = removed.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            bounds.Expand(0.8f);

            Collider[] around = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider col in around)
            {
                GatherableProp herb = col.GetComponent<GatherableProp>();
                if (herb == null || herb._done || herb.itemId != ItemIds.Herb)
                    continue;

                GameObject root = herb.Root;
                if (root == null || root == removed || root.transform.IsChildOf(removed.transform))
                    continue;

                // 이 프롭(과 그 자식)을 뺀 첫 바닥을 찾는다
                Vector3 from = root.transform.position + Vector3.up * 0.5f;
                RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                bool found = false;
                RaycastHit ground = default;
                foreach (RaycastHit hit in hits)
                {
                    Transform t = hit.collider.transform;
                    if (t.IsChildOf(removed.transform) || t.IsChildOf(root.transform))
                        continue;
                    ground = hit;
                    found = true;
                    break;
                }

                if (!found)
                {
                    Destroy(root);
                    continue;
                }

                float gap = ground.distance - 0.5f;   // 풀 발끝 ~ 바닥
                if (gap > 0.2f)
                    root.transform.position = new Vector3(root.transform.position.x, ground.point.y, root.transform.position.z);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => AllProps.Clear();

        #endregion

        private int EffectiveTier => Mathf.Max(requiredTier, itemId == ItemIds.Stone ? 1 : 0);

        public float RequiredHoldSeconds
        {
            get
            {
                int tier = EffectiveTier;
                // 조건(곡괭이 등급)을 못 맞추면 시간이 훨씬 오래 걸리고, 끝나도 아이템을 얻지 못한다(Interact에서 막는다).
                if (tier > 0 && !GatherRules.MeetsRequirement(tier))
                    return Mathf.Max(gatherHoldSeconds, 1f) * GatherRules.UnmetTimeMultiplier;
                // 캘 수 있는 도구면 빠르게(요구 등급보다 좋은 도구일수록 더 빠르게)
                return gatherHoldSeconds * GatherRules.ToolSpeedFactor(GatherRules.Player, tier);
            }
        }

        public string InteractLabel => displayName;
        public string InteractSubLabel => "";
        public Color InteractLabelColor => Color.white;

        // 풀(약초)은 어디에나 깔려 있으므로, 근처에 나무/돌 등 다른 대상이 있으면 그쪽을 먼저 잡게 한다.
        public int InteractPriority => itemId == ItemIds.Herb ? -1 : 0;

        public void Configure(string name, string item, int amt, float holdSeconds, GameObject toRemove)
        {
            displayName = name;
            itemId = item;
            amount = amt;
            gatherHoldSeconds = holdSeconds;
            target = toRemove;
        }

        public void Interact(GameObject player)
        {
            if (_done || player == null)
                return;

            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null)
                return;

            int tier = EffectiveTier;
            if (tier > 0)
            {
                InventorySlot hand = inventory.SelectedSlot;
                if (hand == null || hand.IsEmpty || hand.item == null || hand.item.toolTier < tier)
                {
                    Debug.LogWarning($"[GatherableProp] {displayName}: 1/2/3 키로 선택한 슬롯에 등급 {tier} 이상의 곡괭이를 들고 있어야 채집할 수 있습니다.");
                    return;
                }
            }

            ItemData item = ItemCatalog.GetOrCreate().GetItem(itemId);
            if (item == null)
            {
                Debug.LogWarning($"[GatherableProp] {displayName}: 아이템 '{itemId}'를 찾을 수 없습니다.");
                return;
            }

            // 멀티플레이: 호스트가 먼저 캔 사람을 정하고, 모든 화면에서 프롭을 없앤다.
            if (LastTruck.Networking.NetworkGathering.TryHandleProp(this, inventory, item))
                return;

            int finalAmount = amount;
            if (player.TryGetComponent<LastTruck.CharacterAbilityController>(out var abilityController))
                finalAmount = abilityController.GetCalculatedResourceAmount(amount);

            // 가방에 안 들어가는 만큼은 발밑에 떨어진다
            inventory.AddOrDrop(item, finalAmount);

            _done = true;
            Debug.Log($"[GatherableProp] {displayName}에서 {item.itemName} x{finalAmount} 채집.");

            GameObject remove = target != null ? target : (transform.parent != null ? transform.parent.gameObject : gameObject);
            SettleNeighbors(remove);
            Destroy(remove);
        }
    }
}
