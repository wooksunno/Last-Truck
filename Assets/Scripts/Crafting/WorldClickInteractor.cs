using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 월드 상호작용 입력.
    ///  - 트럭 거점: 트럭 근처에서 Tab 키로 열고 닫는다(클릭으로는 열리지 않는다).
    ///  - 가공 시설: 카메라 스크린 포인트 Raycast로 클릭 상호작용한다.
    /// </summary>
    public class WorldClickInteractor : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float maxDistance = 500f;
        [SerializeField] private LayerMask interactMask = ~0;
        [SerializeField] private PlayerInventory playerInventory;
        [Tooltip("플레이어가 트럭 외곽에서 이 거리(m) 안이면 Tab으로 트럭 거점을 열 수 있다.")]
        [SerializeField] private float truckOpenRange = 7f;

        private TruckStation _truck;
        private Vector3 _truckAnchor;
        private float _truckSearchTimer;

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

        private bool IsNearTruck()
        {
            if (playerInventory == null)
                return false;

            if (_truck == null)
            {
                _truckSearchTimer -= Time.unscaledDeltaTime;
                if (_truckSearchTimer > 0f)
                    return false;
                _truckSearchTimer = 1f;
                _truck = FindFirstObjectByType<TruckStation>();
                if (_truck == null)
                    return false;
            }

            // 트럭 전체(자식 콜라이더 포함) 외곽까지의 거리
            Vector3 p = playerInventory.transform.position;
            bool any = false;
            float best = float.MaxValue;
            Bounds all = new Bounds(_truck.transform.position, Vector3.zero);
            foreach (Collider c in _truck.GetComponentsInChildren<Collider>())
            {
                if (c == null || !c.enabled || c.isTrigger)
                    continue;
                if (!any) all = c.bounds; else all.Encapsulate(c.bounds);
                any = true;
                float d = c.bounds.SqrDistance(p);
                if (d < best) best = d;
            }
            if (!any)
                best = (_truck.transform.position - p).sqrMagnitude;
            Vector3 nearest = all.ClosestPoint(p);   // 플레이어와 가장 가까운 트럭 부분 위쪽에 띄워 화면 안에 보이게 한다
            _truckAnchor = new Vector3(nearest.x, all.max.y + 0.8f, nearest.z);

            return best <= truckOpenRange * truckOpenRange;
        }

        private void Update()
        {
            GameUIController ui = GameUIController.Instance;

            if (Input.GetKeyDown(KeyCode.Escape) && ui != null)
            {
                ui.ClosePopup();
                return;
            }

            // 트럭 거점: 근처에서 Tab으로 열기/닫기 + 안내 표시
            bool nearTruck = IsNearTruck();
            if (ui != null)
                ui.SetTruckHint(nearTruck && !ui.IsPopupOpen, _truckAnchor);

            if (Input.GetKeyDown(KeyCode.Tab) && ui != null && playerInventory != null)
            {
                if (ui.IsPopupOpen)
                {
                    ui.ClosePopup();
                    return;
                }

                if (nearTruck && _truck != null)
                {
                    _truck.OnInteract(playerInventory);
                    return;
                }
            }

            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (ui != null && ui.IsPopupOpen)
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
            // 트럭은 클릭이 아니라 Tab 키로 연다. 자원 채집은 E키 근접 상호작용(LastTruck.PlayerInteract)으로 처리한다.

            if (interactable == null)
                return;

            Debug.Log($"[Interact] {interactable.InteractLabel}");
            interactable.OnInteract(playerInventory);
        }
    }
}
