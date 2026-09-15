using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 콘솔 전용 레거시 테스트. 기본 비활성 (UI 씬 테스트와 충돌 방지).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class TruckCraftingTester : MonoBehaviour
    {
        [SerializeField] private TruckInventory truckInventory;
        [SerializeField] private TruckCraftingManager craftingManager;
        [SerializeField] private bool runOnStart = false;

        private void Awake()
        {
            if (truckInventory == null)
                truckInventory = GetComponent<TruckInventory>();
            if (craftingManager == null)
                craftingManager = GetComponent<TruckCraftingManager>();
        }

        private void Start()
        {
            if (runOnStart)
                RunTest();
        }

        [ContextMenu("Run Crafting Test")]
        public void RunTest()
        {
            if (truckInventory == null || craftingManager == null)
            {
                Debug.LogError("[TruckCraftingTester] 참조 없음");
                return;
            }

            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            craftingManager.SetRecipes(catalog.GetRecipesForFacility(FacilityType.None));
            Debug.Log(truckInventory.GetInventorySummary());
            RecipeData machete = craftingManager.FindRecipe("assemble_machete");
            if (machete != null)
                craftingManager.TryCraft(machete);
            Debug.Log(truckInventory.GetInventorySummary());
        }
    }
}
