using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 아이템/레시피 카탈로그. Resources 또는 런타임 생성으로 항상 사용 가능하게 유지한다.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Crafting/Item Catalog")]
    public class ItemCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Crafting/ItemCatalog";

        [SerializeField] private List<ItemData> items = new List<ItemData>();
        [SerializeField] private List<RecipeData> recipes = new List<RecipeData>();

        private static ItemCatalog _runtimeInstance;
        private Dictionary<string, ItemData> _itemLookup;

        public IReadOnlyList<ItemData> Items => items;
        public IReadOnlyList<RecipeData> Recipes => recipes;

        public static ItemCatalog GetOrCreate()
        {
            if (_runtimeInstance != null)
                return _runtimeInstance;

            ItemCatalog fromResources = Resources.Load<ItemCatalog>(ResourcesPath);
            if (fromResources != null)
            {
                _runtimeInstance = fromResources;
                _runtimeInstance.BuildLookup();
                return _runtimeInstance;
            }

            _runtimeInstance = CreateInstance<ItemCatalog>();
            _runtimeInstance.name = "RuntimeItemCatalog";
            CraftingContentFactory.PopulateCatalog(_runtimeInstance);
            return _runtimeInstance;
        }

        public void SetContent(List<ItemData> itemList, List<RecipeData> recipeList)
        {
            items = itemList ?? new List<ItemData>();
            recipes = recipeList ?? new List<RecipeData>();
            BuildLookup();
        }

        public ItemData GetItem(string itemId)
        {
            BuildLookup();
            if (_itemLookup.TryGetValue(itemId, out ItemData item))
                return item;
            return null;
        }

        public List<RecipeData> GetRecipesForFacility(FacilityType facility)
        {
            var result = new List<RecipeData>();
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeData recipe = recipes[i];
                if (recipe != null && recipe.requiredFacility == facility)
                    result.Add(recipe);
            }

            return result;
        }

        private void BuildLookup()
        {
            if (_itemLookup != null && _itemLookup.Count == items.Count)
                return;

            _itemLookup = new Dictionary<string, ItemData>();
            for (int i = 0; i < items.Count; i++)
            {
                ItemData item = items[i];
                if (item == null || string.IsNullOrEmpty(item.itemID))
                    continue;
                _itemLookup[item.itemID] = item;
            }
        }

        private void OnEnable() => BuildLookup();
    }
}
