using System;

namespace CraftingSystem
{
    public static class ItemIds
    {
        public const string Wood = "wood";
        public const string Stone = "stone";
        public const string IronOre = "iron_ore";
        public const string CopperOre = "copper_ore";
        public const string Iron = "iron";
        public const string Copper = "copper";
        public const string WoodHandle = "wood_handle";
        public const string CopperBlade = "copper_blade";
        public const string Machete = "machete";
        public const string WoodSpear = "wood_spear";
        public const string StonePickaxe = "stone_pickaxe";
        public const string CopperPickaxe = "copper_pickaxe";
        public const string IronPickaxe = "iron_pickaxe";

        // 맵 특산물 구역 전용 자원
        public const string Diamond = "diamond";
        public const string Platinum = "platinum";
        public const string PoisonHerb = "poison_herb";
    }

    public static class ItemMatching
    {
        public static bool IsSameItem(ItemData a, ItemData b)
        {
            if (a == null || b == null)
                return false;

            if (ReferenceEquals(a, b))
                return true;

            if (!string.IsNullOrEmpty(a.itemID) && !string.IsNullOrEmpty(b.itemID))
                return string.Equals(a.itemID, b.itemID, StringComparison.Ordinal);

            return false;
        }
    }
}
