using System.Collections;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 맵의 특산물 구역 안에 배치되는 채집 오브젝트.
    /// LastTruck.PlayerInteract(E키 꾹 누르기, OverlapSphere) 방식으로 상호작용한다.
    /// </summary>
    public class SpecialResourceNode : MonoBehaviour, LastTruck.IHoldInteractable
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
        private Renderer _cachedRenderer;
        private Collider _cachedCollider;

        public float RequiredHoldSeconds => gatherHoldSeconds;

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
            _cachedRenderer = GetComponent<Renderer>();
            _cachedCollider = GetComponent<Collider>();
            AllNodes.Add(this);
        }

        private void OnDestroy()
        {
            AllNodes.Remove(this);
        }

        #region 멀티플레이 연동 (LastTruck.Networking.NetworkGathering)

        // 멀티플레이에서는 고갈/재생성을 호스트만 계산하고 모두에게 알린다.
        // 클라이언트는 채집 요청만 보내고, 호스트가 굴린 결과 아이템을 받는다.

        /// <summary>씬에 있는 모든 특산물 노드 (위치로 같은 노드를 찾는다 - 모든 컴퓨터가 같은 시드로 같은 맵을 만든다).</summary>
        public static readonly System.Collections.Generic.List<SpecialResourceNode> AllNodes =
            new System.Collections.Generic.List<SpecialResourceNode>();

        /// <summary>호스트: 고갈(true) / 재생성(false) 순간을 알린다.</summary>
        public static event System.Action<SpecialResourceNode, bool> DepletionChanged;

        public bool IsDepleted => _depleted;
        public ItemData Item => item;
        public int Amount => amount;
        public ItemData CommonBonusItem => commonBonusItem;
        public ItemData RareBonusItem => rareBonusItem;

        /// <summary>호스트: 한 번 채집한다 (보너스 굴림 + 횟수 차감 + 고갈). 고갈 상태면 false.</summary>
        public bool TryTakeHit(out ItemData gathered, out int gatheredAmount)
        {
            gathered = null;
            gatheredAmount = 0;
            if (_depleted || item == null)
                return false;

            gathered = RollGatherItem();
            gatheredAmount = amount;

            if (_remainingHits > 0)
            {
                _remainingHits--;
                if (_remainingHits <= 0)
                    Deplete();
            }
            return true;
        }

        /// <summary>클라이언트: 호스트가 알려준 고갈 상태를 화면에 반영한다.</summary>
        public void ApplyNetworkDepleted(bool depleted)
        {
            _depleted = depleted;
            if (_cachedRenderer != null) _cachedRenderer.enabled = !depleted;
            if (_cachedCollider != null) _cachedCollider.enabled = !depleted;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AllNodes.Clear();
            DepletionChanged = null;
        }

        #endregion

        private void RollRemainingHits()
        {
            _remainingHits = maxHits > 0 ? Random.Range(minHits, maxHits + 1) : -1;
        }

        private void Deplete()
        {
            _depleted = true;
            if (_cachedRenderer != null) _cachedRenderer.enabled = false;
            if (_cachedCollider != null) _cachedCollider.enabled = false;
            DepletionChanged?.Invoke(this, true);
            StartCoroutine(RegenRoutine());
        }

        private IEnumerator RegenRoutine()
        {
            float wait = maxRegenSeconds > 0f ? Random.Range(minRegenSeconds, maxRegenSeconds) : 0f;
            yield return new WaitForSeconds(wait);

            RollRemainingHits();
            _depleted = false;
            if (_cachedRenderer != null) _cachedRenderer.enabled = true;
            if (_cachedCollider != null) _cachedCollider.enabled = true;
            DepletionChanged?.Invoke(this, false);
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

            // 멀티플레이: 호스트가 채집 결과(보너스/고갈)를 정한다.
            if (LastTruck.Networking.NetworkGathering.TryHandleGather(this, inventory))
                return;

            ItemData gatherItem = RollGatherItem();

            if (!inventory.AddItem(gatherItem, amount))
            {
                Debug.LogWarning($"[SpecialResourceNode] 인벤토리 공간이 부족하여 {gatherItem.itemName}을(를) 획득하지 못했습니다.");
                return;
            }

            Debug.Log($"[SpecialResourceNode] {displayName}에서 {gatherItem.itemName} x{amount} 획득.");

            if (_remainingHits > 0)
            {
                _remainingHits--;
                if (_remainingHits <= 0)
                    Deplete();
            }
        }
    }
}
