using System;
using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private int maxSlots = 3;
        [SerializeField] private bool grantStarterItems = true;
        [SerializeField] private ItemStackInventory inventory = new ItemStackInventory();

        public event Action Changed;

        public ItemStackInventory Inventory => inventory;
        public IReadOnlyList<InventorySlot> Slots => inventory.Slots;

        private void Awake()
        {
            inventory.MaxSlots = maxSlots;
            inventory.Changed += OnInventoryChanged;
        }

        private void Start()
        {
            if (grantStarterItems && inventory.UsedSlotCount == 0)
                GrantStarterItems();
        }

        private void OnDestroy()
        {
            inventory.Changed -= OnInventoryChanged;
        }

        [ContextMenu("Grant Starter Items")]
        public void GrantStarterItems()
        {
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            AddItem(catalog.GetItem(ItemIds.Wood), 10);
            AddItem(catalog.GetItem(ItemIds.Stone), 10);
            AddItem(catalog.GetItem(ItemIds.IronOre), 10);
            Debug.Log($"[PlayerInventory] 시작 원재료 지급\n{inventory.GetSummary("Player")}");
        }

        public bool HasIngredients(RecipeData recipe) => inventory.HasIngredients(recipe);

        public int GetItemCount(ItemData item) => inventory.GetItemCount(item);

        public bool AddItem(ItemData item, int amount) => inventory.AddItem(item, amount);

        public bool RemoveItem(ItemData item, int amount) => inventory.RemoveItem(item, amount);

        public bool TryCraft(RecipeData recipe)
        {
            if (recipe == null || recipe.output == null || recipe.output.item == null)
                return false;

            if (!inventory.HasIngredients(recipe))
                return false;

            var removed = new List<RecipeIngredient>();
            foreach (RecipeIngredient ingredient in recipe.inputs)
            {
                if (ingredient == null || ingredient.item == null || ingredient.count <= 0)
                    continue;

                if (!inventory.RemoveItem(ingredient.item, ingredient.count))
                {
                    RollbackRemoved(removed);
                    return false;
                }

                removed.Add(ingredient);
            }

            if (inventory.AddItem(recipe.output.item, recipe.output.count))
                return true;

            // 결과물을 넣을 공간이 없으면 이미 소모한 재료를 되돌려 손실을 막는다.
            RollbackRemoved(removed);
            Debug.LogWarning($"[PlayerInventory] 인벤토리 공간이 부족하여 {recipe.output.item.itemName} 가공을 취소했습니다.");
            return false;
        }

        private void RollbackRemoved(List<RecipeIngredient> removed)
        {
            foreach (RecipeIngredient ingredient in removed)
                inventory.AddItem(ingredient.item, ingredient.count);
        }

        public string GetSummary() => inventory.GetSummary("Player");

        private void OnInventoryChanged() => Changed?.Invoke();
    }
}
