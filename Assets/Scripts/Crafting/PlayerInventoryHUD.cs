using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CraftingSystem
{
    /// <summary>
    /// 우하단 플레이어 인벤토리(핫바) HUD. 슬롯은 에디터에서 미리 배치돼 있고,
    /// 이 컴포넌트는 PlayerInventory 상태를 슬롯 뷰에 옮겨 담기만 한다.
    /// </summary>
    public class PlayerInventoryHUD : MonoBehaviour
    {
        [SerializeField] private List<InventorySlotView> slots = new List<InventorySlotView>();
        [SerializeField] private Text usedCountText;     // 예: 3/8
        [SerializeField] private Text heldNameText;      // 선택한(손에 든) 아이템 이름

        private PlayerInventory _inventory;

        public int SlotCount => slots.Count;

        public void Bind(PlayerInventory inventory, Action<int> onSlotClicked)
        {
            _inventory = inventory;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] != null)
                    slots[i].Setup(i, onSlotClicked != null ? new UnityEngine.Events.UnityAction<int>(onSlotClicked) : null);
            Refresh();
        }

        public void Refresh()
        {
            if (_inventory == null) return;

            IReadOnlyList<InventorySlot> data = _inventory.Slots;
            int cap = Mathf.Min(slots.Count, _inventory.MaxSlots);
            int used = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null) continue;
                bool inRange = i < cap;
                if (slots[i].gameObject.activeSelf != inRange) slots[i].gameObject.SetActive(inRange);
                if (!inRange) continue;

                InventorySlot s = i < data.Count ? data[i] : null;
                bool empty = s == null || s.IsEmpty;
                if (!empty) used++;
                slots[i].Set(empty ? null : s.item, empty ? 0 : s.count, i == _inventory.SelectedSlotIndex);
            }

            if (usedCountText != null) usedCountText.text = used + "/" + cap;
            if (heldNameText != null)
            {
                ItemData held = _inventory.SelectedItem;
                heldNameText.text = held != null ? held.itemName : "빈 손";
            }
        }
    }
}
