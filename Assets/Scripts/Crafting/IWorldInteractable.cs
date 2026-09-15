namespace CraftingSystem
{
    public interface IWorldInteractable
    {
        string InteractLabel { get; }
        void OnInteract(PlayerInventory player);
    }
}
