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

        public void Configure(string name, ItemData itemData, int amt, int tier = 0)
        {
            displayName = name;
            item = itemData;
            amount = amt;
            requiredTier = tier;
        }

        public void Interact(GameObject player)
        {
            if (item == null)
            {
                Debug.LogWarning($"[SpecialResourceNode] {displayName}: 아이템 데이터가 없습니다.");
                return;
            }

            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null)
                return;

            if (requiredTier > 0)
            {
                InventorySlot hand = inventory.SelectedSlot;
                int heldTier = hand != null && !hand.IsEmpty && hand.item != null ? hand.item.toolTier : 0;
                if (heldTier < requiredTier)
                {
                    Debug.LogWarning($"[SpecialResourceNode] {displayName}: 1/2/3 키로 선택한 슬롯에 등급 {requiredTier} 이상의 도구를 들고 있어야 채집할 수 있습니다.");
                    return;
                }
            }

            if (inventory.AddItem(item, amount))
                Debug.Log($"[SpecialResourceNode] {displayName}에서 {item.itemName} x{amount} 획득.");
            else
                Debug.LogWarning($"[SpecialResourceNode] 인벤토리 공간이 부족하여 {item.itemName}을(를) 획득하지 못했습니다.");
        }
    }
}
