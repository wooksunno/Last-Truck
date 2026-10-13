using System;
using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private int maxSlots = 4;
        [Tooltip("나무/돌/광석 같은 원재료는 한 칸에 이 개수까지만 들고 다닐 수 있다 (트럭 보관함은 제한 없음).")]
        [SerializeField] private int rawStackLimit = 20;
        [SerializeField] private bool grantStarterItems = true;
        [SerializeField] private ItemStackInventory inventory = new ItemStackInventory();

        private static readonly KeyCode[] SlotHotkeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
        };

        public int MaxSlots => maxSlots;

        public event Action Changed;
        public event Action SelectedSlotChanged;

        public ItemStackInventory Inventory => inventory;
        public IReadOnlyList<InventorySlot> Slots => inventory.Slots;

        /// <summary>
        /// 1~4 키로 고른, 현재 손에 든 인벤토리 슬롯 인덱스(0~3).
        /// 채집 판정(ResourceNode 등)은 이 슬롯의 도구를 기준으로 한다.
        /// </summary>
        public int SelectedSlotIndex { get; private set; } = 0;

        public InventorySlot SelectedSlot =>
            Slots.Count > SelectedSlotIndex ? Slots[SelectedSlotIndex] : null;

        public ItemData SelectedItem =>
            SelectedSlot != null && !SelectedSlot.IsEmpty ? SelectedSlot.item : null;

        private void Awake()
        {
            inventory.MaxSlots = maxSlots;
            inventory.RawStackLimit = rawStackLimit;
            inventory.KeepSlotPositions = true;   // 1~4번 칸 위치를 고정한다 (아이템을 써도 칸이 밀리지 않고, 칸끼리 교환 가능)
            inventory.Changed += OnInventoryChanged;
        }

        private void Start()
        {
            if (grantStarterItems && inventory.UsedSlotCount == 0)
                GrantStarterItems();
        }

        private void Update()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool uiOpen = GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen;

            for (int i = 0; i < SlotHotkeys.Length; i++)
            {
                if (Input.GetKeyDown(SlotHotkeys[i]))
                {
                    // Ctrl + 숫자: 지금 든 칸과 그 칸의 아이템을 서로 바꾼다
                    if (ctrl && !uiOpen)
                        SwapSlots(SelectedSlotIndex, i);
                    else
                        SelectSlot(i);
                    break;
                }
            }

            // Q: 든 아이템 1개 버리기, Shift+Q: 칸 전부 버리기
            if (!uiOpen && Input.GetKeyDown(KeyCode.Q))
                DropSelected(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        }

        /// <summary>두 칸의 아이템을 서로 바꾼다 (빈 칸이면 이동, 같은 아이템이면 합친다).</summary>
        public bool SwapSlots(int from, int to)
        {
            bool ok = inventory.MoveOrSwap(from, to);
            // 든 칸을 옮겼으면 손에 든 칸도 같이 따라간다 (손에 든 아이템이 그대로 유지되게)
            if (ok && from == SelectedSlotIndex)
            {
                SelectedSlotIndex = to;
                SelectedSlotChanged?.Invoke();
            }
            else if (ok && to == SelectedSlotIndex)
            {
                SelectedSlotChanged?.Invoke();
            }
            return ok;
        }

        /// <summary>칸의 아이템을 바닥에 버린다 (E로 다시 주울 수 있다). count가 칸의 수량 이상이면 전부.</summary>
        public bool DropSlot(int slotIndex, int count)
        {
            if (slotIndex < 0 || slotIndex >= Slots.Count)
                return false;

            InventorySlot slot = Slots[slotIndex];
            if (slot == null || slot.IsEmpty)
                return false;

            ItemData item = slot.item;
            int amount = Mathf.Clamp(count, 1, slot.count);
            if (!RemoveFromSlot(slotIndex, amount))
                return false;

            DroppedItem.Spawn(transform, item, amount);
            return true;
        }

        public bool DropSelected(bool wholeStack)
        {
            InventorySlot slot = SelectedSlot;
            if (slot == null || slot.IsEmpty)
                return false;
            return DropSlot(SelectedSlotIndex, wholeStack ? slot.count : 1);
        }

        private bool RemoveFromSlot(int slotIndex, int amount)
        {
            InventorySlot slot = Slots[slotIndex];
            if (slot == null || slot.IsEmpty || amount <= 0 || amount > slot.count)
                return false;

            slot.count -= amount;
            if (slot.count <= 0)
            {
                slot.item = null;
                slot.count = 0;
            }
            inventory.NotifyChanged();
            return true;
        }

        public void SelectSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= maxSlots || slotIndex == SelectedSlotIndex)
                return;

            SelectedSlotIndex = slotIndex;
            SelectedSlotChanged?.Invoke();
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
