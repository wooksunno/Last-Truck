using System;
using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 전용 대용량 인벤토리.
    /// </summary>
    public class TruckInventory : MonoBehaviour
    {
        [SerializeField] private int maxSlots = 64;
        [SerializeField] private bool fillDefaultsOnStart = true;
        [SerializeField] private ItemStackInventory inventory = new ItemStackInventory();

        public event Action Changed;

        public ItemStackInventory Inventory => inventory;
        public IReadOnlyList<InventorySlot> Slots => inventory.Slots;
        public int MaxSlots => inventory.MaxSlots;
        public int UsedSlotCount => inventory.UsedSlotCount;

        private void Awake()
        {
            inventory.MaxSlots = maxSlots;
            inventory.Changed += OnInventoryChanged;
        }

        private void Start()
        {
            if (fillDefaultsOnStart && inventory.UsedSlotCount == 0)
                FillDefaultResources();
        }

        private void OnDestroy()
        {
            inventory.Changed -= OnInventoryChanged;
        }

        [ContextMenu("Fill Default Resources")]
        public void FillDefaultResources()
        {
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            AddItem(catalog.GetItem(ItemIds.Wood), 100);
            AddItem(catalog.GetItem(ItemIds.Stone), 100);
            AddItem(catalog.GetItem(ItemIds.IronOre), 50);
            AddItem(catalog.GetItem(ItemIds.CopperOre), 50);
            AddItem(catalog.GetItem(ItemIds.Iron), 20);
            AddItem(catalog.GetItem(ItemIds.Copper), 20);
            Debug.Log($"[TruckInventory] 기본 원재료 지급\n{GetInventorySummary()}");
        }

        public bool HasIngredients(IEnumerable<RecipeIngredient> ingredients) =>
            inventory.HasIngredients(ingredients);

        public bool HasIngredients(RecipeData recipe) => inventory.HasIngredients(recipe);

        public int GetItemCount(ItemData item) => inventory.GetItemCount(item);

        public bool RemoveItem(ItemData item, int amount) => inventory.RemoveItem(item, amount);

        public bool AddItem(ItemData item, int amount) => inventory.AddItem(item, amount);

        public void Clear() => inventory.Clear();

        public string GetInventorySummary() => inventory.GetSummary("Truck Inventory");

        public bool DepositFrom(PlayerInventory player)
        {
            if (player == null)
                return false;
            return player.Inventory.TransferAllTo(inventory);
        }

        public bool WithdrawAllTo(PlayerInventory player)
        {
            if (player == null)
                return false;
            return inventory.TransferAllTo(player.Inventory);
        }

        private void OnInventoryChanged() => Changed?.Invoke();
    }
}
