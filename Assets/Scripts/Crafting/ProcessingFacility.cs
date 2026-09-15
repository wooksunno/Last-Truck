using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 모닥불 / 절삭기 / 롤러프레스 등 가공 시설.
    /// 플레이어 인벤토리 재료를 소모하고 결과물을 플레이어에게 반환한다.
    /// </summary>
    public class ProcessingFacility : MonoBehaviour, IWorldInteractable
    {
        [SerializeField] private FacilityType facilityType = FacilityType.Campfire;
        [SerializeField] private string displayName = "가공 시설";
        [SerializeField] private List<RecipeData> recipes = new List<RecipeData>();

        public FacilityType FacilityType => facilityType;
        public string InteractLabel => displayName;
        public IReadOnlyList<RecipeData> Recipes => recipes;

        public void Configure(FacilityType type, string name, List<RecipeData> facilityRecipes)
        {
            facilityType = type;
            displayName = name;
            recipes = facilityRecipes ?? new List<RecipeData>();
        }

        public void LoadRecipesFromCatalog()
        {
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            recipes = catalog.GetRecipesForFacility(facilityType);
        }

        public void OnInteract(PlayerInventory player)
        {
            if (recipes == null || recipes.Count == 0)
                LoadRecipesFromCatalog();

            if (GameUIController.Instance != null)
                GameUIController.Instance.OpenFacilityPanel(this, player);
            else
                Debug.LogWarning("[ProcessingFacility] GameUIController가 없습니다.");
        }

        public bool TryProcess(RecipeData recipe, PlayerInventory player)
        {
            if (player == null || recipe == null)
                return false;

            if (recipe.requiredFacility != facilityType)
            {
                Debug.LogWarning($"[Facility] 시설 불일치: {recipe.recipeID}");
                return false;
            }

            bool ok = player.TryCraft(recipe);
            if (ok)
            {
                Debug.Log(
                    $"[Facility:{displayName}] 가공 성공 → " +
                    $"{recipe.output.item.itemName} x{recipe.output.count}");
            }
            else
            {
                Debug.LogWarning($"[Facility:{displayName}] 가공 실패 (재료 부족): {recipe.GetDisplayName()}");
            }

            return ok;
        }
    }
}
