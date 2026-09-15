using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CraftingSystem
{
    [Serializable]
    public class InventorySlot
    {
        public ItemData item;
        public int count;

        public InventorySlot()
        {
        }

        public InventorySlot(ItemData item, int count)
        {
            this.item = item;
            this.count = count;
        }

        public bool IsEmpty => item == null || count <= 0;
    }

    /// <summary>
    /// 플레이어/트럭이 공유하는 슬롯 인벤토리 로직.
    /// </summary>
    [Serializable]
    public class ItemStackInventory
    {
        [SerializeField] private int maxSlots = 32;
        [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();

        public event Action Changed;

        public IReadOnlyList<InventorySlot> Slots => slots;
        public int MaxSlots
        {
            get => maxSlots;
            set => maxSlots = Mathf.Max(1, value);
        }

        public int UsedSlotCount
        {
            get
            {
                int used = 0;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (!slots[i].IsEmpty)
                        used++;
                }

                return used;
            }
        }

        public void NotifyChanged() => Changed?.Invoke();

        public bool HasIngredients(IEnumerable<RecipeIngredient> ingredients)
        {
            if (ingredients == null)
                return false;

            foreach (RecipeIngredient ingredient in ingredients)
            {
                if (ingredient == null || ingredient.item == null || ingredient.count <= 0)
                    continue;

                if (GetItemCount(ingredient.item) < ingredient.count)
                    return false;
            }

            return true;
        }

        public bool HasIngredients(RecipeData recipe)
        {
            return recipe != null && HasIngredients(recipe.inputs);
        }

        public int GetItemCount(ItemData item)
        {
            if (item == null)
                return 0;

            int total = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (!slot.IsEmpty && ItemMatching.IsSameItem(slot.item, item))
                    total += slot.count;
            }

            return total;
        }

        public bool RemoveItem(ItemData item, int amount)
        {
            if (item == null || amount <= 0)
                return false;

            if (GetItemCount(item) < amount)
                return false;

            int remaining = amount;
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                InventorySlot slot = slots[i];
                if (slot.IsEmpty || !ItemMatching.IsSameItem(slot.item, item))
                    continue;

                int take = Mathf.Min(slot.count, remaining);
                slot.count -= take;
                remaining -= take;

                if (slot.count <= 0)
                {
                    slot.item = null;
                    slot.count = 0;
                }
            }

            CompactEmptySlots();
            NotifyChanged();
            return remaining == 0;
        }

        public bool AddItem(ItemData item, int amount)
        {
            if (item == null || amount <= 0)
                return false;

            int remaining = amount;
            int maxStack = Mathf.Max(1, item.maxStack);

            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                InventorySlot slot = slots[i];
                if (slot.IsEmpty || !ItemMatching.IsSameItem(slot.item, item))
                    continue;

                int space = maxStack - slot.count;
                if (space <= 0)
                    continue;

                int add = Mathf.Min(space, remaining);
                slot.count += add;
                remaining -= add;
            }

            while (remaining > 0)
            {
                InventorySlot empty = FindEmptySlot();
                if (empty == null)
                {
                    if (slots.Count >= maxSlots)
                    {
                        Debug.LogWarning($"[Inventory] 슬롯 부족: {item.itemName} x{remaining}");
                        NotifyChanged();
                        return false;
                    }

                    empty = new InventorySlot();
                    slots.Add(empty);
                }

                int add = Mathf.Min(maxStack, remaining);
                empty.item = item;
                empty.count = add;
                remaining -= add;
            }

            NotifyChanged();
            return true;
        }

        public void Clear()
        {
            slots.Clear();
            NotifyChanged();
        }

        public bool TransferAllTo(ItemStackInventory target)
        {
            if (target == null)
                return false;

            var snapshot = new List<InventorySlot>();
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty)
                    snapshot.Add(new InventorySlot(slots[i].item, slots[i].count));
            }

            foreach (InventorySlot slot in snapshot)
            {
                if (!target.AddItem(slot.item, slot.count))
                    return false;
                RemoveItem(slot.item, slot.count);
            }

            return true;
        }

        public string GetSummary(string title)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- {title} ({UsedSlotCount}/{maxSlots}) ---");

            bool any = false;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (slot.IsEmpty)
                    continue;

                any = true;
                sb.AppendLine($"[{i}] {slot.item.itemName} x{slot.count}");
            }

            if (!any)
                sb.AppendLine("(비어 있음)");

            return sb.ToString().TrimEnd();
        }

        private InventorySlot FindEmptySlot()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                    return slots[i];
            }

            return null;
        }

        private void CompactEmptySlots()
        {
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                if (slots[i].IsEmpty)
                    slots.RemoveAt(i);
            }
        }
    }
}
