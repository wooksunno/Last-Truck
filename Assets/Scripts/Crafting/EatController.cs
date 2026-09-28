using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 1/2/3 키로 선택한 슬롯의 음식(스테이크 등)을 좌클릭으로 꾹 눌러 먹는다.
    /// 다 먹으면 1개 소모하고 체력을 회복한다. 진행률은 GatherProgressUI의 원형 게이지로 표시된다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerInventory))]
    public class EatController : MonoBehaviour
    {
        private struct Edible
        {
            public float holdSeconds;
            public float healAmount;
        }

        private static readonly Dictionary<string, Edible> Edibles = new Dictionary<string, Edible>
        {
            { ItemIds.Steak, new Edible { holdSeconds = 2f, healAmount = 2f } },
        };

        [SerializeField] private Camera targetCamera;
        [Tooltip("게임 시작 시 깎아둘 체력(회복 아이템 테스트용).")]
        [SerializeField] private float startingDamage = 30f;

        private PlayerInventory _inventory;
        private LastTruck.PlayerStats _stats;
        private float _holdTimer;
        private bool _pressVetoed;
        private string _activeItemId;

        public bool IsHolding { get; private set; }
        public float HoldProgress01 { get; private set; }

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _stats = GetComponent<LastTruck.PlayerStats>();
            if (_stats == null)
                _stats = GetComponentInChildren<LastTruck.PlayerStats>();
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

private void Start()
        {
            StartCoroutine(ApplyStartingDamage());
        }

private System.Collections.IEnumerator ApplyStartingDamage()
        {
            // PlayerStats.Start()가 체력을 최대로 채운 뒤에 깎아야 하므로 한 프레임 기다린다.
            yield return null;

            if (_stats == null || startingDamage <= 0f)
                yield break;

            _stats.currentHealth = Mathf.Max(1f, _stats.MaxHealth - startingDamage);
            _stats.Heal(0f);
        }



private void Update()
        {
            InventorySlot hand = _inventory.SelectedSlot;
            Edible edible = default;
            bool hasEdible = hand != null && !hand.IsEmpty && hand.item != null &&
                             Edibles.TryGetValue(hand.item.itemID, out edible);

            // 체력이 가득 차 있으면 먹어도 아무 효과가 없다(시작도, 소모도 되지 않는다).
            bool canEat = hasEdible && !IsHealthFull();

            if (Input.GetMouseButtonDown(0))
                _pressVetoed = !canEat || IsPressBlocked();
            if (Input.GetMouseButtonUp(0))
                _pressVetoed = false;

            bool popupOpen = GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen;
            bool holding = canEat && !_pressVetoed && !popupOpen && Input.GetMouseButton(0);

            if (!holding || hand.item.itemID != _activeItemId)
            {
                _holdTimer = 0f;
                _activeItemId = hasEdible ? hand.item.itemID : null;
            }

            if (!holding)
            {
                IsHolding = false;
                HoldProgress01 = 0f;
                return;
            }

            _holdTimer += Time.deltaTime;
            IsHolding = true;
            HoldProgress01 = Mathf.Clamp01(_holdTimer / edible.holdSeconds);

            if (_holdTimer >= edible.holdSeconds)
            {
                Eat(hand.item, edible);
                _holdTimer = 0f;
                IsHolding = false;
                HoldProgress01 = 0f;
                _pressVetoed = true;
            }
        }

private bool IsHealthFull()
        {
            return _stats != null && _stats.currentHealth >= _stats.MaxHealth;
        }


        private bool IsPressBlocked()
        {
            if (GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen)
                return true;

            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return true;

            if (targetCamera == null)
                targetCamera = Camera.main;
            if (targetCamera == null)
                return false;

            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                return hit.collider.GetComponentInParent<TruckStation>() != null ||
                       hit.collider.GetComponentInParent<ProcessingFacility>() != null;
            }

            return false;
        }

        private void Eat(ItemData item, Edible edible)
        {
            if (!_inventory.RemoveItem(item, 1))
                return;

            if (_stats == null)
            {
                Debug.LogWarning($"[EatController] {item.itemName}을(를) 먹었지만 PlayerStats가 없어 체력을 회복할 수 없습니다.");
                return;
            }

            _stats.Heal(edible.healAmount);
            Debug.Log($"[EatController] 체력이 회복되었습니다. ({item.itemName} +{edible.healAmount}) 현재 체력: {_stats.currentHealth}/{_stats.MaxHealth}");
        }
    }
}
