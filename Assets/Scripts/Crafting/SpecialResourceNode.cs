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

        public void Configure(string name, ItemData itemData, int amt)
        {
            displayName = name;
            item = itemData;
            amount = amt;
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

            if (inventory.AddItem(item, amount))
                Debug.Log($"[SpecialResourceNode] {displayName}에서 {item.itemName} x{amount} 획득.");
            else
                Debug.LogWarning($"[SpecialResourceNode] 인벤토리 공간이 부족하여 {item.itemName}을(를) 획득하지 못했습니다.");
        }
    }
}
