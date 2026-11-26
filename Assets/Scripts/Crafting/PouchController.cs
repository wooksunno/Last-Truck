using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 가죽 파우치 배낭 열기.
    ///  - B 키: 배낭을 가방에 가지고 있으면 어디서든 연다 (다시 누르면 닫는다).
    ///  - 배낭을 손에 들고(1~8 키) 왼쪽 클릭: 연다.
    ///  - 배낭을 손에 들고 E: 근처에 다른 상호작용 대상(트럭/채집 등)이 없을 때만 연다 (예전 방식).
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class PouchController : MonoBehaviour
    {
        [SerializeField] private KeyCode pouchKey = KeyCode.B;   // (예전 openKey=E 직렬화 값이 남지 않게 이름을 바꿨다)

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
            if (_pouch == null) _pouch = GetComponent<PouchInventory>();
            GameUIController ui = GameUIController.Instance;
            if (_pouch == null || ui == null || LastTruck.Networking.ChatHud.IsTyping) return;

            bool keyOpen = Input.GetKeyDown(pouchKey);
            bool holding = IsHoldingBackpack();
            bool clickOpen = holding && Input.GetMouseButtonDown(0) && !IsPointerOverUi();
            bool eOpen = holding && KeyBindings.Down(GameAction.Interact) && (_interact == null || !_interact.HasNearbyInteractable);
            if (!keyOpen && !clickOpen && !eOpen) return;

            if (ui.IsPopupOpen)
            {
                if (keyOpen && ui.IsPouchOpen) ui.ClosePopup();   // B를 다시 누르면 닫는다
                return;
            }

            if (!_inventory.HasBackpack)
            {
                if (keyOpen) LastTruck.HudToast.Show("배낭이 없어요. 트럭에서 '가죽 파우치 배낭'을 만들면 짐을 더 많이 들고 다닐 수 있어요.");
                return;
            }

            ui.OpenPouchPanel(_pouch, _inventory);
        }

        private bool IsHoldingBackpack()
        {
            InventorySlot hand = _inventory.SelectedSlot;
            return hand != null && !hand.IsEmpty && hand.item != null && hand.item.itemID == ItemIds.LeatherPouchBackpack;
        }

        private static bool IsPointerOverUi()
        {
            return UnityEngine.EventSystems.EventSystem.current != null &&
                   UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }
    }
}
