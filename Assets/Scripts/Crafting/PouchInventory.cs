using System;
using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 가죽 파우치 배낭 안에 들어있는 휴대용 보조 인벤토리.
    /// 플레이어가 들고 다니며 본인 인벤토리와 아이템을 주고받는 별도의 저장 공간이다.
    /// </summary>
    public class PouchInventory : MonoBehaviour
    {
        [SerializeField] private int maxSlots = 16;
        [SerializeField] private ItemStackInventory inventory = new ItemStackInventory();

        public event Action Changed;

        public ItemStackInventory Inventory => inventory;
        public IReadOnlyList<InventorySlot> Slots => inventory.Slots;
        public int MaxSlots => inventory.MaxSlots;

        private void Awake()
        {
            inventory.MaxSlots = maxSlots;
            inventory.Changed += OnInventoryChanged;
        }

        private void OnDestroy()
        {
            inventory.Changed -= OnInventoryChanged;
        }

        public int GetItemCount(ItemData item) => inventory.GetItemCount(item);

        public bool AddItem(ItemData item, int amount) => inventory.AddItem(item, amount);

        public bool RemoveItem(ItemData item, int amount) => inventory.RemoveItem(item, amount);

        private void OnInventoryChanged() => Changed?.Invoke();
    }
}
