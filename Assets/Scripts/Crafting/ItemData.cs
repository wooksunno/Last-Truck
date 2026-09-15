using UnityEngine;

namespace CraftingSystem
{
    public enum ItemType
    {
        Raw,
        Intermediate,
        Finished,
        Tool
    }

    [CreateAssetMenu(fileName = "NewItemData", menuName = "Crafting/Item Data")]
    public class ItemData : ScriptableObject
    {
        public string itemID;
        public string itemName;
        public ItemType itemType;
        public int maxStack = 99;
        public Sprite icon;

        [Tooltip("채집 도구 등급. 0 = 도구 아님. 값이 클수록 상위 등급 자원도 채집 가능.")]
        public int toolTier = 0;

        public static ItemData CreateRuntime(
            string id,
            string displayName,
            ItemType type,
            int maxStack = 99,
            Sprite icon = null,
            int toolTier = 0)
        {
            ItemData item = CreateInstance<ItemData>();
            item.itemID = id;
            item.itemName = displayName;
            item.itemType = type;
            item.maxStack = maxStack;
            item.icon = icon;
            item.toolTier = toolTier;
            item.name = id;
            return item;
        }
    }
}
