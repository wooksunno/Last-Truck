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

        /// <summary>
        /// true면 아이템을 다 쓰거나 꺼내도 뒤의 칸이 앞으로 당겨지지 않는다 (핫바처럼 칸 위치가 고정).
        /// 플레이어 인벤토리는 1~4번 칸을 직접 정리할 수 있어야 해서 켠다. 트럭 보관함은 기존처럼 앞으로 당긴다.
        /// </summary>
        public bool KeepSlotPositions { get; set; }

        /// <summary>
        /// from 칸을 to 칸으로 옮긴다. 목적지가 비어 있으면 이동, 같은 아이템이면 합치기(넘치면 남김), 다른 아이템이면 서로 교환.
        /// 범위를 벗어나거나 같은 칸이면 false.
        /// </summary>
        public bool MoveOrSwap(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= maxSlots || to >= maxSlots)
                return false;

            while (slots.Count <= Mathf.Max(from, to))
                slots.Add(new InventorySlot());

            InventorySlot a = slots[from];
            InventorySlot b = slots[to];
            if (a.IsEmpty)
                return false;

            if (b.IsEmpty)
            {
                b.item = a.item; b.count = a.count;
                a.item = null; a.count = 0;
            }
            else if (ItemMatching.IsSameItem(a.item, b.item))
            {
                int space = Mathf.Max(1, b.item.maxStack) - b.count;
                int move = Mathf.Min(space, a.count);
                if (move <= 0) return false;
                b.count += move;
                a.count -= move;
                if (a.count <= 0) { a.item = null; a.count = 0; }
            }
            else
            {
                ItemData ti = a.item; int tc = a.count;
                a.item = b.item; a.count = b.count;
                b.item = ti; b.count = tc;
            }

            if (!KeepSlotPositions)
                CompactEmptySlots();
            NotifyChanged();
            return true;
        }

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

            if (!KeepSlotPositions)
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

        /// <summary>
        /// 슬롯 전체를 주어진 목록으로 덮어쓴다 (멀티플레이 클라이언트가 호스트의 트럭 인벤토리를 받아올 때 사용).
        /// </summary>
        public void ReplaceAll(IEnumerable<InventorySlot> newSlots)
        {
            slots.Clear();
            if (newSlots != null)
            {
                foreach (InventorySlot slot in newSlots)
                {
                    if (slot != null && !slot.IsEmpty)
                        slots.Add(new InventorySlot(slot.item, slot.count));
                }
            }

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
