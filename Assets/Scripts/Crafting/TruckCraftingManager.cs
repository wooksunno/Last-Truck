using System.Collections.Generic;
using UnityEngine;
using LastTruck;

namespace CraftingSystem
{
    [RequireComponent(typeof(TruckInventory))]
    public class TruckCraftingManager : MonoBehaviour
    {
        [SerializeField] private TruckInventory truckInventory;
        [SerializeField] private List<RecipeData> recipes = new List<RecipeData>();

        public TruckInventory Inventory
        {
            get
            {
                if (truckInventory == null)
                    truckInventory = GetComponent<TruckInventory>();
                return truckInventory;
            }
        }
        public IReadOnlyList<RecipeData> Recipes => recipes;

        private void Awake()
        {
            _ = Inventory;
        }

        public void SetRecipes(IEnumerable<RecipeData> recipeList)
        {
            recipes.Clear();
            if (recipeList == null)
                return;

            foreach (RecipeData recipe in recipeList)
            {
                if (recipe != null)
                    recipes.Add(recipe);
            }
        }

        public void RegisterRecipe(RecipeData recipe)
        {
            if (recipe == null)
                return;

            if (!recipes.Contains(recipe))
                recipes.Add(recipe);
        }

        public void LoadAssemblyRecipesFromCatalog()
        {
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            SetRecipes(catalog.GetRecipesForFacility(FacilityType.None));
        }

        public bool TryCraft(RecipeData recipe)
        {
            if (recipe == null)
            {
                Debug.LogWarning("[TruckCrafting] 레시피가 null입니다.");
                return false;
            }



            TruckInventory inv = Inventory;
            if (inv == null)
            {
                Debug.LogError("[TruckCrafting] TruckInventory가 없습니다.");
                return false;
            }

            if (recipe.output == null || recipe.output.item == null || recipe.output.count <= 0)
            {
                Debug.LogWarning($"[TruckCrafting] 결과물 없음: {recipe.recipeID}");
                return false;
            }

            // 대장장이 능력 추가
            CharacterAbilityController abilityController = FindObjectOfType<CharacterAbilityController>();

            ItemType type = recipe.output.item.itemType;
            bool isWeapon = (type == ItemType.Finished);

            foreach (RecipeIngredient ingredient in recipe.inputs)
            {
                if (ingredient == null || ingredient.item == null || ingredient.count <= 0)
                    continue;

                int requiredCount = ingredient.count;
                if (abilityController != null)
                {
                    requiredCount = abilityController.GetCalculatedCraftingCost(ingredient.count, isWeapon);
                }

                if (inv.GetItemCount(ingredient.item) < requiredCount)
                {
                    Debug.LogWarning($"[TruckCrafting] 재료 부족: {recipe.recipeID}");
                    return false;
                }
            }

            var removed = new List<(ItemData item, int count)>();
            foreach (RecipeIngredient ingredient in recipe.inputs)
            {
                if (ingredient == null || ingredient.item == null || ingredient.count <= 0)
                    continue;

                int requiredCount = ingredient.count;
                if (abilityController != null)
                {
                    requiredCount = abilityController.GetCalculatedCraftingCost(ingredient.count, isWeapon);
                }

                if (!inv.RemoveItem(ingredient.item, requiredCount))
                {
                    foreach (var r in removed)
                        inv.AddItem(r.item, r.count);
                    return false;
                }

                removed.Add((ingredient.item, requiredCount));
            }

            if (!inv.AddItem(recipe.output.item, recipe.output.count))
            {
                foreach (var r in removed)
                    inv.AddItem(r.item, r.count);
                Debug.LogError($"[TruckCrafting] 결과물 수납 실패(공간 부족): {recipe.output.item.itemName}");
                return false;
            }

            Debug.Log(
                $"[TruckCrafting] 제작 성공: {recipe.GetDisplayName()} → " +
                $"{recipe.output.item.itemName} x{recipe.output.count}");
            return true;
        }

        public RecipeData FindRecipe(string recipeID)
        {
            if (string.IsNullOrEmpty(recipeID))
                return null;

            for (int i = 0; i < recipes.Count; i++)
            {
                if (recipes[i] != null && recipes[i].recipeID == recipeID)
                    return recipes[i];
            }

            return null;
        }
    }
}
