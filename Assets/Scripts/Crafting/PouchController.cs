using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 1/2/3 키로 선택한 슬롯에 가죽 파우치 배낭을 들고 있는 상태로 E키를 누르면 파우치 UI를 연다.
    /// 근처에 다른 월드 상호작용 대상(자원 채집 등)이 있으면 그쪽 상호작용을 우선한다.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class PouchController : MonoBehaviour
    {
        [SerializeField] private KeyCode openKey = KeyCode.E;

        private PlayerInventory _inventory;
        private LastTruck.PlayerInteract _interact;
        private PouchInventory _pouch;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _interact = GetComponent<LastTruck.PlayerInteract>();
            _pouch = GetComponent<PouchInventory>();
        }

        private void Update()
        {
            if (!Input.GetKeyDown(openKey))
                return;

            if (GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen)
                return;

            if (_interact != null && _interact.HasNearbyInteractable)
                return;

            if (_pouch == null || GameUIController.Instance == null)
                return;

            InventorySlot hand = _inventory.SelectedSlot;
            if (hand == null || hand.IsEmpty || hand.item == null || hand.item.itemID != ItemIds.LeatherPouchBackpack)
                return;

            GameUIController.Instance.OpenPouchPanel(_pouch, _inventory);
        }
    }
}
