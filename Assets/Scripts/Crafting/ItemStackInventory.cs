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

        /// <summary>남은 도구 내구도. 0 = 아직 안 쓴 새 것(가득)으로 본다. 내구도가 있는 아이템(ItemData.maxDurability)에만 의미가 있다.</summary>
        public int durability;

        public InventorySlot()
        {
        }

        public InventorySlot(ItemData item, int count)
        {
            this.item = item;
            this.count = count;
        }

        public bool IsEmpty => item == null || count <= 0;

        /// <summary>남은 내구도 (내구도가 없는 아이템이면 0).</summary>
        public int CurrentDurability
        {
            get
            {
                if (IsEmpty || !item.HasDurability) return 0;
                return durability > 0 ? Mathf.Min(durability, item.maxDurability) : item.maxDurability;
            }
        }

        /// <summary>남은 내구도 비율 0~1 (내구도가 없는 아이템이면 1).</summary>
        public float DurabilityRatio =>
            IsEmpty || !item.HasDurability ? 1f : (float)CurrentDurability / item.maxDurability;

        public void Clear()
        {
            item = null;
            count = 0;
            durability = 0;
        }
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
        /// 0보다 크면 원재료(나무/돌/광석 등 ItemType.Raw)는 한 칸에 이 개수까지만 들어간다.
        /// 플레이어 인벤토리만 20으로 제한하고, 트럭 보관함은 아이템의 maxStack을 그대로 쓴다.
        /// </summary>
        public int RawStackLimit { get; set; }

        /// <summary>이 인벤토리에서 해당 아이템이 한 칸에 들어갈 수 있는 최대 개수.</summary>
        public int StackLimitFor(ItemData item)
        {
            if (item == null) return 1;
            int max = Mathf.Max(1, item.maxStack);
            if (RawStackLimit > 0 && item.itemType == ItemType.Raw)
                max = Mathf.Min(max, RawStackLimit);
            return max;
        }

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
                b.item = a.item; b.count = a.count; b.durability = a.durability;
                a.Clear();
            }
            else if (ItemMatching.IsSameItem(a.item, b.item))
            {
                int space = StackLimitFor(b.item) - b.count;
                int move = Mathf.Min(space, a.count);
                if (move <= 0) return false;
                b.count += move;
                a.count -= move;
                if (a.count <= 0) a.Clear();
            }
            else
            {
                ItemData ti = a.item; int tc = a.count; int td = a.durability;
                a.item = b.item; a.count = b.count; a.durability = b.durability;
                b.item = ti; b.count = tc; b.durability = td;
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
                    slot.Clear();
            }

            if (!KeepSlotPositions)
                CompactEmptySlots();
            NotifyChanged();
            return remaining == 0;
        }

        /// <summary>도구/무기를 닳은 채로 넣는다 (durability = 남은 내구도, 0이면 새 것). 도구는 한 칸에 하나라 빈 칸에 그대로 들어간다.</summary>
        public bool AddItem(ItemData item, int amount, int durability)
        {
            if (item == null || amount <= 0)
                return false;
            if (!item.HasDurability || durability <= 0)
                return AddItem(item, amount);

            for (int n = 0; n < amount; n++)
            {
                InventorySlot empty = FindEmptySlot();
                if (empty == null)
                {
                    if (slots.Count >= maxSlots)
                    {
                        Debug.LogWarning($"[Inventory] 슬롯 부족: {item.itemName}");
                        NotifyChanged();
                        return false;
                    }
                    empty = new InventorySlot();
                    slots.Add(empty);
                }
                empty.item = item;
                empty.count = 1;
                empty.durability = durability;
            }
            NotifyChanged();
            return true;
        }

        /// <summary>
        /// 남은 내구도가 currentDurability인 도구/무기 한 개를 뺀다 (같은 도구가 여러 개 있을 때 "그" 도구를 정확히 꺼내려고).
        /// 못 찾으면 false.
        /// </summary>
        public bool RemoveDurable(ItemData item, int currentDurability)
        {
            if (item == null) return false;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot s = slots[i];
                if (s.IsEmpty || !ItemMatching.IsSameItem(s.item, item) || s.CurrentDurability != currentDurability)
                    continue;

                s.Clear();
                if (!KeepSlotPositions)
                    CompactEmptySlots();
                NotifyChanged();
                return true;
            }
            return false;
        }

        public bool AddItem(ItemData item, int amount)
        {
            if (item == null || amount <= 0)
                return false;

            int remaining = amount;
            int maxStack = StackLimitFor(item);

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
                empty.durability = 0;   // 새로 들어온 도구는 새 것
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
                        slots.Add(new InventorySlot(slot.item, slot.count) { durability = slot.durability });
                }
            }

            NotifyChanged();
        }

        /// <summary>
        /// 칸 위치를 그대로 살려서 덮어쓴다 (null/빈 칸도 그 자리에 빈 칸으로 남긴다).
        /// 멀티플레이 재접속/방장 변경 때 내 가방(1~4번 칸)을 원래 칸 그대로 복구할 때 쓴다.
        /// </summary>
        public void ReplaceAllKeepPositions(IList<InventorySlot> newSlots)
        {
            slots.Clear();
            if (newSlots != null)
            {
                int last = -1;
                for (int i = 0; i < newSlots.Count; i++)
                {
                    if (newSlots[i] != null && !newSlots[i].IsEmpty) last = i;
                }
                for (int i = 0; i <= last && i < maxSlots; i++)
                {
                    InventorySlot slot = newSlots[i];
                    slots.Add(slot != null && !slot.IsEmpty
                        ? new InventorySlot(slot.item, slot.count) { durability = slot.durability }
                        : new InventorySlot());
                }
                if (!KeepSlotPositions) slots.RemoveAll(s => s.IsEmpty);
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
                    snapshot.Add(new InventorySlot(slots[i].item, slots[i].count) { durability = slots[i].CurrentDurability });
            }

            foreach (InventorySlot slot in snapshot)
            {
                if (!target.AddItem(slot.item, slot.count, slot.durability))
                    return false;
                if (slot.item.HasDurability) RemoveDurable(slot.item, slot.durability);   // 닳은 정도가 같은 "그" 도구를 뺀다
                else RemoveItem(slot.item, slot.count);
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
