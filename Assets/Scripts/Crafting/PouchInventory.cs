using System;
using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 가죽 파우치 배낭 안에 들어있는 휴대용 보조 인벤토리.
    ///  - 배낭(아이템)을 가방에 가지고 있어야 쓸 수 있다. 가지고 있으면 채집/줍기로 얻은 재료가 배낭에 먼저 들어간다 (PlayerInventory.AddOrDrop).
    ///  - 처음에는 BaseSlots칸이고, 경화 가공 가죽을 덧대면 SlotsPerUpgrade칸씩 늘어난다 (최대 MaxSlotsLimit칸).
    ///  - 한 칸에 아이템의 최대 묶음(maxStack)까지 들어간다 (가방은 원재료 20개 제한, 배낭은 제한 없음).
    /// </summary>
    public class PouchInventory : MonoBehaviour
    {
        public const int BaseSlots = 6;
        public const int SlotsPerUpgrade = 2;
        public const int MaxUpgrades = 5;
        public const int MaxSlotsLimit = BaseSlots + SlotsPerUpgrade * MaxUpgrades;   // 16
        private static readonly int[] LeatherCost = { 1, 2, 3, 4, 5 };

        [SerializeField] private ItemStackInventory inventory = new ItemStackInventory();

        private int _upgrades;

        public event Action Changed;

        public ItemStackInventory Inventory => inventory;
        public IReadOnlyList<InventorySlot> Slots => inventory.Slots;
        public int MaxSlots => inventory.MaxSlots;
        public int Upgrades => _upgrades;
        public bool Maxed => _upgrades >= MaxUpgrades;

        /// <summary>다음 확장에 드는 경화 가공 가죽 수 (최대면 0).</summary>
        public int NextLeatherCost => Maxed ? 0 : LeatherCost[Mathf.Clamp(_upgrades, 0, LeatherCost.Length - 1)];

        public int UsedSlots => inventory.UsedSlotCount;

        private void Awake()
        {
            inventory.MaxSlots = BaseSlots;
            inventory.Changed += OnInventoryChanged;
        }

        private void OnDestroy()
        {
            inventory.Changed -= OnInventoryChanged;
        }

        public int GetItemCount(ItemData item) => inventory.GetItemCount(item);

        public bool AddItem(ItemData item, int amount) => inventory.AddItem(item, amount);

        public bool RemoveItem(ItemData item, int amount) => inventory.RemoveItem(item, amount);

        /// <summary>
        /// 경화 가공 가죽을 덧대어 배낭을 늘린다. 가죽은 내 가방 → 배낭 순서로 꺼낸다. 성공하면 true.
        /// message: 화면에 보여 줄 결과 안내.
        /// </summary>
        public bool TryUpgrade(PlayerInventory owner, out string message)
        {
            if (Maxed) { message = "배낭이 이미 가장 커요 (" + MaxSlots + "칸)"; return false; }

            ItemData leather = ItemCatalog.GetOrCreate().GetItem(ItemIds.TannedLeather);
            int need = NextLeatherCost;
            int inBag = owner != null && leather != null ? owner.GetItemCount(leather) : 0;
            int inPouch = leather != null ? inventory.GetItemCount(leather) : 0;
            if (leather == null || inBag + inPouch < need)
            {
                message = "경화 가공 가죽이 " + need + "개 필요해요 (가진 것 " + (inBag + inPouch) + "개). 가죽은 무두질 건조대에서 만들어요.";
                return false;
            }

            int fromBag = Mathf.Min(inBag, need);
            if (fromBag > 0) owner.RemoveItem(leather, fromBag);
            if (need - fromBag > 0) inventory.RemoveItem(leather, need - fromBag);

            _upgrades++;
            inventory.MaxSlots = BaseSlots + SlotsPerUpgrade * _upgrades;
            inventory.NotifyChanged();
            message = "배낭이 " + MaxSlots + "칸으로 늘었어요!";
            return true;
        }

        private void OnInventoryChanged() => Changed?.Invoke();
    }
}
