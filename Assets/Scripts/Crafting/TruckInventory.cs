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
        [SerializeField] private int maxSlots = 120;
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

public void FillDefaultResources()
        {
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            AddItem(catalog.GetItem(ItemIds.Wood), 100);
            AddItem(catalog.GetItem(ItemIds.Stone), 100);
            AddItem(catalog.GetItem(ItemIds.IronOre), 50);
            AddItem(catalog.GetItem(ItemIds.CopperOre), 50);
            AddItem(catalog.GetItem(ItemIds.Iron), 20);
            AddItem(catalog.GetItem(ItemIds.Copper), 20);
            AddItem(catalog.GetItem(ItemIds.Platinum), 10);
            AddItem(catalog.GetItem(ItemIds.Diamond), 5);
            AddItem(catalog.GetItem(ItemIds.PoisonHerb), 10);
            AddItem(catalog.GetItem(ItemIds.WetHide), 20);
            AddItem(catalog.GetItem(ItemIds.Herb), 20);
            AddItem(catalog.GetItem(ItemIds.Oil), 20);
            AddItem(catalog.GetItem(ItemIds.Meat), 20);
            AddItem(catalog.GetItem(ItemIds.EmergencyPatchBoard), 2);
            AddItem(catalog.GetItem(ItemIds.WeldingKit), 2);
            AddItem(catalog.GetItem(ItemIds.HighTensionRepairPack), 2);

            // 테스트용: 무기 5종 + 곡괭이 3종. 무기/도구는 겹치지 않아 각각 한 칸씩 차지한다.
            AddItem(catalog.GetItem(ItemIds.Ak47), 1);
            AddItem(catalog.GetItem(ItemIds.PlatinumSniperRifle), 1);
            AddItem(catalog.GetItem(ItemIds.Flamethrower), 1);
            AddItem(catalog.GetItem(ItemIds.HuntingBow), 1);
            AddItem(catalog.GetItem(ItemIds.Machete), 1);
            AddItem(catalog.GetItem(ItemIds.WoodPickaxe), 1);
            AddItem(catalog.GetItem(ItemIds.StonePickaxe), 1);
            AddItem(catalog.GetItem(ItemIds.CopperPickaxe), 1);
            AddItem(catalog.GetItem(ItemIds.IronPickaxe), 1);

            // 테스트 편의: 위에서 지급되지 않은 나머지 모든 아이템도 최소 1개씩 채워 넣는다.
            foreach (ItemData item in catalog.Items)
            {
                if (item == null)
                    continue;
                if (GetItemCount(item) <= 0)
                    AddItem(item, 1);
            }

            Debug.Log($"[TruckInventory] 기본 원재료/테스트용 무기·곡괭이 + 전체 아이템 1개씩 지급\n{GetInventorySummary()}");
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
