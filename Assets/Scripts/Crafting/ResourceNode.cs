using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 나무/돌/광석 등 월드 자원 채집 오브젝트.
    /// 캐릭터 모델이 없는 동안은 플레이어 인벤토리의 가장 왼쪽 칸(0번)을 "손"으로 취급한다.
    /// 손에 든 도구의 등급(toolTier)이 요구 등급 이상이어야 채집 가능(요구 등급 0 = 맨손 채집 허용).
    /// </summary>
    public class ResourceNode : MonoBehaviour, IWorldInteractable
    {
        private const int HandSlotIndex = 0;

        [SerializeField] private string displayName = "자원 채집";
        [SerializeField] private ItemData outputItem;
        [SerializeField] private int outputAmount = 1;
        [Tooltip("채집에 걸리는 시간(초). 기획서에 명시된 값이 없어 기본값 0(즉시 채집).")]
        [SerializeField] private float gatherSeconds = 0f;
        [Tooltip("채집에 필요한 최소 도구 등급. 0 = 맨손 채집 가능.")]
        [SerializeField] private int requiredTier = 0;

        private bool _isGathering;

        public string InteractLabel => displayName;

        public void Configure(string name, ItemData item, int amount, float seconds, int tier)
        {
            displayName = name;
            outputItem = item;
            outputAmount = amount;
            gatherSeconds = seconds;
            requiredTier = tier;
        }

        public void OnInteract(PlayerInventory player)
        {
            if (_isGathering || player == null || outputItem == null)
                return;

            if (!HandMeetsRequiredTier(player))
            {
                Debug.LogWarning($"[ResourceNode] {displayName}: 손(인벤토리 첫 칸)에 등급 {requiredTier} 이상의 도구가 있어야 채집할 수 있습니다.");
                return;
            }

            if (gatherSeconds <= 0f)
            {
                GatherNow(player);
                return;
            }

            StartCoroutine(GatherRoutine(player));
        }

        private bool HandMeetsRequiredTier(PlayerInventory player)
        {
            if (requiredTier <= 0)
                return true;

            IReadOnlyList<InventorySlot> slots = player.Slots;
            if (slots.Count <= HandSlotIndex)
                return false;

            InventorySlot hand = slots[HandSlotIndex];
            if (hand == null || hand.IsEmpty || hand.item == null)
                return false;

            return hand.item.toolTier >= requiredTier;
        }

        private void GatherNow(PlayerInventory player)
        {
            if (!player.AddItem(outputItem, outputAmount))
            {
                Debug.LogWarning($"[ResourceNode] 인벤토리 공간이 부족하여 {outputItem.itemName}을(를) 채집하지 못했습니다.");
                return;
            }

            Debug.Log($"[ResourceNode] {displayName}에서 {outputItem.itemName} x{outputAmount} 채집.");
        }

        private IEnumerator GatherRoutine(PlayerInventory player)
        {
            _isGathering = true;
            yield return new WaitForSeconds(gatherSeconds);
            GatherNow(player);
            _isGathering = false;
        }
    }
}
