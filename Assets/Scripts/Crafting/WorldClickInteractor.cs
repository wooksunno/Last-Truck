using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 카메라 스크린 포인트 Raycast로 월드 오브젝트를 클릭 상호작용한다.
    /// </summary>
    public class WorldClickInteractor : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float maxDistance = 500f;
        [SerializeField] private LayerMask interactMask = ~0;
        [SerializeField] private PlayerInventory playerInventory;

        public PlayerInventory PlayerInventory
        {
            get => playerInventory;
            set => playerInventory = value;
        }

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && GameUIController.Instance != null)
            {
                GameUIController.Instance.ClosePopup();
                return;
            }

            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen)
                return;

            if (!Input.GetMouseButtonDown(0))
                return;

            if (targetCamera == null)
                targetCamera = Camera.main;
            if (targetCamera == null || playerInventory == null)
                return;

            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactMask))
                return;

            IWorldInteractable interactable = hit.collider.GetComponentInParent<ProcessingFacility>();
            if (interactable == null)
                interactable = hit.collider.GetComponentInParent<TruckStation>();
            if (interactable == null)
                interactable = hit.collider.GetComponentInParent<ResourceNode>();

            if (interactable == null)
                return;

            Debug.Log($"[Interact] {interactable.InteractLabel}");
            interactable.OnInteract(playerInventory);
        }
    }
}
