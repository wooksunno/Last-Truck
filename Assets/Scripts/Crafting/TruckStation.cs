using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭(마차) 상호작용. 클릭 시 트럭 인벤토리 + 조립 UI를 연다.
    /// </summary>
    [RequireComponent(typeof(TruckInventory))]
    [RequireComponent(typeof(TruckCraftingManager))]
    public class TruckStation : MonoBehaviour, IWorldInteractable
    {
        [SerializeField] private string displayName = "트럭 거점";
        [SerializeField] private TruckInventory truckInventory;
        [SerializeField] private TruckCraftingManager craftingManager;

        public string InteractLabel => displayName;

        public TruckInventory TruckInventory
        {
            get
            {
                if (truckInventory == null)
                    truckInventory = GetComponent<TruckInventory>();
                return truckInventory;
            }
        }

        public TruckCraftingManager CraftingManager
        {
            get
            {
                if (craftingManager == null)
                    craftingManager = GetComponent<TruckCraftingManager>();
                return craftingManager;
            }
        }

private void Awake()
        {
            _ = TruckInventory;
            _ = CraftingManager;
        }

        private void Start()
        {
            if (craftingManager != null && craftingManager.Recipes.Count == 0)
                craftingManager.LoadAssemblyRecipesFromCatalog();
        }

        public void OnInteract(PlayerInventory player)
        {
            if (craftingManager != null && craftingManager.Recipes.Count == 0)
                craftingManager.LoadAssemblyRecipesFromCatalog();

            if (GameUIController.Instance != null)
                GameUIController.Instance.OpenTruckPanel(this, player);
            else
                Debug.LogWarning("[TruckStation] GameUIController가 없습니다.");
        }
    }
}
