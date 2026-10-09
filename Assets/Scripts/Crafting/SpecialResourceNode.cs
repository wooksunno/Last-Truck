using System.Collections;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 맵의 특산물 구역 안에 배치되는 채집 오브젝트.
    /// LastTruck.PlayerInteract(E키 꾹 누르기, OverlapSphere) 방식으로 상호작용한다.
    /// </summary>
    public class SpecialResourceNode : MonoBehaviour, LastTruck.IHoldInteractable, LastTruck.IInteractLabel
    {
        [SerializeField] private string displayName = "특산물";
        [SerializeField] private ItemData item;
        [SerializeField] private int amount = 1;
        [Tooltip("채집에 필요한 최소 도구 등급. 0 = 맨손 채집 가능. 1/2/3 키로 선택한 슬롯을 기준으로 판정한다.")]
        [SerializeField] private int requiredTier = 0;
        [Tooltip("채집에 필요한 특정 장비 아이템 ID(예: 장갑, 수동 펌프). 비어있으면 무시된다. 1/2/3 키로 선택한 슬롯을 기준으로 판정한다.")]
        [SerializeField] private string requiredItemId = "";
        [Tooltip("E키를 꾹 누르고 있어야 하는 시간(초). 0 이하면 즉시 채집(기존 방식).")]
        [SerializeField] private float gatherHoldSeconds = 0f;

        [Tooltip("고갈까지 필요한 채집 횟수 범위(무작위). maxHits가 0이면 고갈되지 않는다.")]
        [SerializeField] private int minHits = 0;
        [SerializeField] private int maxHits = 0;
        [Tooltip("고갈 후 다시 채집 가능해지기까지 걸리는 시간(초) 범위(무작위).")]
        [SerializeField] private float minRegenSeconds = 0f;
        [SerializeField] private float maxRegenSeconds = 0f;

        [Tooltip("채집 시 원래 자원 대신 일정 확률로 나오는 흔한 보너스 아이템(예: 철광맥의 돌). 없으면 무시.")]
        [SerializeField] private ItemData commonBonusItem;
        [SerializeField] private float commonBonusChance = 0f;
        [Tooltip("채집 시 원래 자원 대신 낮은 확률로 나오는 희귀 보너스 아이템(예: 철광맥의 백금). 없으면 무시.")]
        [SerializeField] private ItemData rareBonusItem;
        [SerializeField] private float rareBonusChance = 0f;

        private int _remainingHits = -1;
        private bool _depleted;
        private Renderer[] _cachedRenderers;
        private Collider[] _cachedColliders;
        private Collider _ownCollider;

        // 채집 UI에 표시할 이름/요구 조건
        public string InteractLabel => displayName;
        public string InteractSubLabel => "";
        public Color InteractLabelColor => item != null && item.itemID == ItemIds.CopperOre ? new Color(1f, 0.62f, 0.3f)
            : item != null && item.itemID == ItemIds.IronOre ? new Color(0.72f, 0.82f, 1f)
            : item != null && item.itemID == ItemIds.Platinum ? new Color(1f, 0.95f, 0.45f)
            : item != null && item.itemID == ItemIds.Diamond ? new Color(0.45f, 0.9f, 1f) : Color.white;

        // public float RequiredHoldSeconds => gatherHoldSeconds;

        public float RequiredHoldSeconds
        {
            get
            {
                float baseSeconds = gatherHoldSeconds;
                var player = GatherRules.Player;
                bool unmet = !GatherRules.MeetsRequirement(player, requiredTier, requiredItemId);
                if (baseSeconds <= 0f && !unmet) return 0f;
                // 캘 수 있는 도구면 빠르게(요구 등급보다 좋은 도구일수록 더 빠르게). 못 캐는 경우는 아래에서 10배로 느려진다.
                if (!unmet) baseSeconds *= GatherRules.ToolSpeedFactor(player, requiredTier);
                if (player != null && player.TryGetComponent<LastTruck.CharacterAbilityController>(out var abilityController))
                    baseSeconds = abilityController.GetCalculatedMiningTime(baseSeconds);
                // 조건(도구 등급/장비)을 못 맞추면 훨씬 오래 걸리고, 끝나도 아이템을 얻지 못한다(Interact에서 막는다).
                return unmet ? Mathf.Max(baseSeconds, 1f) * GatherRules.UnmetTimeMultiplier : baseSeconds;
            }
        }

        public void Configure(string name, ItemData itemData, int amt, int tier = 0,
            int minHitsRange = 0, int maxHitsRange = 0, float minRegen = 0f, float maxRegen = 0f,
            string requiredItem = "", float holdSeconds = 0f,
            ItemData commonBonus = null, float commonBonusProbability = 0f,
            ItemData rareBonus = null, float rareBonusProbability = 0f)
        {
            displayName = name;
            item = itemData;
            amount = amt;
            requiredTier = tier;
            minHits = minHitsRange;
            maxHits = maxHitsRange;
            minRegenSeconds = minRegen;
            maxRegenSeconds = maxRegen;
            requiredItemId = requiredItem;
            gatherHoldSeconds = holdSeconds;
            commonBonusItem = commonBonus;
            commonBonusChance = commonBonusProbability;
            rareBonusItem = rareBonus;
            rareBonusChance = rareBonusProbability;

            RollRemainingHits();
        }

        private void Awake()
        {
            // 자식 오브젝트로 붙은 광석 모델도 함께 숨기고/되살리기 위해 자식까지 포함해서 캐시한다.
            _cachedRenderers = GetComponentsInChildren<Renderer>(true);
            _cachedColliders = GetComponentsInChildren<Collider>(true);
            _ownCollider = GetComponent<Collider>();

            // _remainingHits는 직렬화되지 않는다. 에디터에서 미리 Configure()를 호출해 씬에
            // 배치해둔 노드는(런타임에 Configure가 다시 불리지 않으므로) Play 진입 시 여기서
            // 최초 1회 굴려줘야 고갈 로직이 정상 동작한다. Configure가 나중에 또 불리면 거기서 다시 굴린다.
            RollRemainingHits();
        }

        private void RollRemainingHits()
        {
            _remainingHits = maxHits > 0 ? Random.Range(minHits, maxHits + 1) : -1;
        }

        private void Deplete()
        {
            _depleted = true;
            SetVisible(false);
            StartCoroutine(RegenRoutine());
        }

        private void SetVisible(bool on)
        {
            if (_cachedRenderers != null) foreach (var r in _cachedRenderers) if (r != null) r.enabled = on;
            if (_cachedColliders != null) foreach (var c in _cachedColliders) if (c != null) c.enabled = on;
        }

        private IEnumerator RegenRoutine()
        {
            float wait = maxRegenSeconds > 0f ? Random.Range(minRegenSeconds, maxRegenSeconds) : 0f;
            yield return new WaitForSeconds(wait);

            RollRemainingHits();
            _depleted = false;
            SetVisible(true);
        }

        /// <summary>
        /// 광맥처럼 원래 자원 대신 다른 아이템이 나올 확률을 굴린다.
        /// 희귀 보너스를 먼저 굴리고, 안 걸리면 흔한 보너스를 굴린다.
        /// </summary>
        private ItemData RollGatherItem()
        {
            if (rareBonusItem != null && Random.value < rareBonusChance)
                return rareBonusItem;

            if (commonBonusItem != null && Random.value < commonBonusChance)
                return commonBonusItem;

            return item;
        }

        public void Interact(GameObject player)
        {
            if (_depleted)
                return;

            if (item == null)
            {
                Debug.LogWarning($"[SpecialResourceNode] {displayName}: 아이템 데이터가 없습니다.");
                return;
            }

            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null)
                return;

            if (requiredTier > 0 || !string.IsNullOrEmpty(requiredItemId))
            {
                InventorySlot hand = inventory.SelectedSlot;
                bool hasHandItem = hand != null && !hand.IsEmpty && hand.item != null;
                bool tierOk = requiredTier <= 0 || (hasHandItem && hand.item.toolTier >= requiredTier);
                bool itemOk = string.IsNullOrEmpty(requiredItemId) || (hasHandItem && hand.item.itemID == requiredItemId);
                if (!tierOk || !itemOk)
                {
                    Debug.LogWarning($"[SpecialResourceNode] {displayName}: 1/2/3 키로 선택한 슬롯에 적합한 도구/장비를 들고 있어야 채집할 수 있습니다.");
                    return;
                }
            }

            ItemData gatherItem = RollGatherItem();

            ////////////////////////
            int finalAmount = amount;

            if (player.TryGetComponent<LastTruck.CharacterAbilityController>(out var abilityController))
            {
                // 1. 탐험가 능력 적용
                finalAmount = abilityController.GetCalculatedResourceAmount(finalAmount);

                // 2. 광부 능력 적용 (광물 아이템일 경우 추가 보너스)
                finalAmount = abilityController.GetCalculatedMiningAmount(gatherItem, finalAmount);
            }
            //////////////////////

            if (!inventory.AddItem(gatherItem, finalAmount))
            {
                Debug.LogWarning($"[SpecialResourceNode] 인벤토리 공간이 부족하여 {gatherItem.itemName}을(를) 획득하지 못했습니다.");
                return;
            }

            Debug.Log($"[SpecialResourceNode] {displayName}에서 {gatherItem.itemName} x{finalAmount} 획득.");

            if (_remainingHits > 0)
            {
                _remainingHits--;
                if (_remainingHits <= 0)
                    Deplete();
            }
        }
    }
}
