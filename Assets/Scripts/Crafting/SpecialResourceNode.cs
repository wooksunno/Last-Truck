using System.Collections;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 맵의 특산물 구역 중앙에 배치되는 채집 오브젝트.
    /// LastTruck.PlayerInteract(E키, OverlapSphere) 방식으로 상호작용한다.
    /// </summary>
    public class SpecialResourceNode : MonoBehaviour, LastTruck.IInteractable
    {
        [SerializeField] private string displayName = "특산물";
        [SerializeField] private ItemData item;
        [SerializeField] private int amount = 1;
        [Tooltip("채집에 필요한 최소 도구 등급. 0 = 맨손 채집 가능. 1/2/3 키로 선택한 슬롯을 기준으로 판정한다.")]
        [SerializeField] private int requiredTier = 0;
        [Tooltip("채집에 필요한 특정 장비 아이템 ID(예: 장갑). 비어있으면 무시된다. 1/2/3 키로 선택한 슬롯을 기준으로 판정한다.")]
        [SerializeField] private string requiredItemId = "";

        [Tooltip("고갈까지 필요한 채집 횟수 범위(무작위). maxHits가 0이면 고갈되지 않는다.")]
        [SerializeField] private int minHits = 0;
        [SerializeField] private int maxHits = 0;
        [Tooltip("고갈 후 다시 채집 가능해지기까지 걸리는 시간(초) 범위(무작위).")]
        [SerializeField] private float minRegenSeconds = 0f;
        [SerializeField] private float maxRegenSeconds = 0f;

        private int _remainingHits = -1;
        private bool _depleted;
        private Renderer _cachedRenderer;
        private Collider _cachedCollider;

public void Configure(string name, ItemData itemData, int amt, int tier = 0,
            int minHitsRange = 0, int maxHitsRange = 0, float minRegen = 0f, float maxRegen = 0f, string requiredItem = "")
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

            RollRemainingHits();
        }

private void Awake()
        {
            _cachedRenderer = GetComponent<Renderer>();
            _cachedCollider = GetComponent<Collider>();
        }

        private void RollRemainingHits()
        {
            _remainingHits = maxHits > 0 ? Random.Range(minHits, maxHits + 1) : -1;
        }

        private void Deplete()
        {
            _depleted = true;
            if (_cachedRenderer != null) _cachedRenderer.enabled = false;
            if (_cachedCollider != null) _cachedCollider.enabled = false;
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

            if (!inventory.AddItem(item, amount))
            {
                Debug.LogWarning($"[SpecialResourceNode] 인벤토리 공간이 부족하여 {item.itemName}을(를) 획득하지 못했습니다.");
                return;
            }

            Debug.Log($"[SpecialResourceNode] {displayName}에서 {item.itemName} x{amount} 획득.");

            if (_remainingHits > 0)
            {
                _remainingHits--;
                if (_remainingHits <= 0)
                    Deplete();
            }
        }
    }
}
