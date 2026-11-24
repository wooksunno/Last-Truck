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
            public float staminaAmount;     // 스태미나 회복량 (0 = 없음)
            public float repairFraction;    // 가방 속 가장 닳은 도구를 최대 내구도의 이 비율만큼 수리 (0 = 없음)
            public bool weaponUpgrade;      // 무기 강화 1단계 (PlayerGear)
            public string verb;             // 안내 문구 동사 (비우면 "먹기"): 붕대는 "사용", 음료는 "마시기"
        }

        private static readonly Dictionary<string, Edible> Edibles = new Dictionary<string, Edible>
        {
            // 음식
            { ItemIds.Herb, new Edible { holdSeconds = 1.2f, healAmount = 5f } },   // 약초: 날것으로 먹을 수 있다 (빨리 먹지만 조금 회복)
            { ItemIds.GrilledFood, new Edible { holdSeconds = 1.6f, healAmount = 8f } },
            { ItemIds.Jerky, new Edible { holdSeconds = 1.2f, healAmount = 6f, staminaAmount = 30f } },   // 전투용 육포: 질기지만 힘이 난다
            { ItemIds.Steak, new Edible { holdSeconds = 2f, healAmount = 12f } },
            { ItemIds.HerbPorridge, new Edible { holdSeconds = 2f, healAmount = 18f } },
            { ItemIds.HeartyStew, new Edible { holdSeconds = 2.6f, healAmount = 32f, staminaAmount = 40f } },
            // 사막 식물 요리 (선인장/들풀은 날것으로는 못 먹는다)
            { ItemIds.CactusSteak, new Edible { holdSeconds = 1.8f, healAmount = 14f } },
            { ItemIds.CactusJuice, new Edible { holdSeconds = 1.2f, healAmount = 8f, staminaAmount = 60f, verb = "마시기" } },   // 마시면 힘이 난다
            { ItemIds.GrassPorridge, new Edible { holdSeconds = 2f, healAmount = 16f } },
            { ItemIds.DesertStew, new Edible { holdSeconds = 2.6f, healAmount = 30f, staminaAmount = 50f } },
            // 소모품
            { ItemIds.HerbBandage, new Edible { holdSeconds = 0.9f, healAmount = 12f, verb = "사용" } },
            { ItemIds.CombatStimulant, new Edible { holdSeconds = 1f, healAmount = 4f, staminaAmount = 100f, verb = "사용" } },
            { ItemIds.VitalityTonic, new Edible { holdSeconds = 1.8f, healAmount = 60f, staminaAmount = 100f, verb = "마시기" } },
            // 도구 수리 (왼쪽 클릭 꾹: 가장 닳은 도구를 고친다)
            { ItemIds.ToolRepairKit, new Edible { holdSeconds = 1.5f, repairFraction = 0.5f } },
            // 무기 강화 (왼쪽 클릭 꾹: 내가 주는 피해가 영구히 오른다)
            { ItemIds.WeaponUpgradeKit, new Edible { holdSeconds = 1.5f, weaponUpgrade = true } },
        };

        [SerializeField] private Camera targetCamera;
        [Tooltip("게임 시작 시 깎아둘 체력(회복 아이템 테스트용).")]
        [SerializeField] private float startingDamage = 30f;
        [Tooltip("켜면 시작할 때 startingDamage만큼 체력을 깎는다 (회복 아이템 테스트용). 기본은 꺼짐 = 체력 100으로 시작.")]
        [SerializeField] private bool applyStartingDamage;

        private PlayerInventory _inventory;
        private LastTruck.PlayerStats _stats;
        private float _holdTimer;
        private bool _pressVetoed;
        private string _activeItemId;
        private ActionPose _pose;

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

            if (_stats == null || startingDamage <= 0f || !applyStartingDamage)   // 시작 체력은 기본으로 가득
                yield break;

            // 멀티플레이: 체력은 호스트(NetworkHealth)가 관리한다. 여기서 내 화면 값만 깎으면 호스트는 체력이 가득 찬 상태라
            // 먹어도 회복이 안 되고 음식만 사라진다. 테스트용 시작 피해는 싱글플레이에서만 준다.
            if (LastTruck.Networking.GameLauncher.IsOnlineSession)
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

            // 얻을 게 없으면(체력도 스태미나도 가득, 고칠 도구도 없음) 먹어도 아무 효과가 없다(시작도, 소모도 되지 않는다).
            bool canEat = hasEdible && HasUseFor(edible);

            // 수리/강화 키트는 트럭 옆에서 쓰는 일이 많다: 화면 가운데에 트럭이 있어도 클릭을 막지 않는다 (UI 위만 막는다)
            bool kit = hasEdible && (edible.repairFraction > 0f || edible.weaponUpgrade);
            if (Input.GetMouseButtonDown(0))
            {
                bool blocked = kit ? IsPointerOverUi() : IsPressBlocked();
                _pressVetoed = !canEat || blocked;
                if (hasEdible && !canEat && !blocked && Time.unscaledTime >= _nextNoUseToast)
                {
                    _nextNoUseToast = Time.unscaledTime + 1.5f;
                    LastTruck.HudToast.Show(NoUseReason(hand.item, edible));
                }
            }
            if (Input.GetMouseButtonUp(0))
                _pressVetoed = false;

            bool popupOpen = GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen;
            bool holding = canEat && !_pressVetoed && !popupOpen && Input.GetMouseButton(0);

            // 먹는 동안 팔을 입으로 올리고 고개를 까딱이는 몸짓
            if (_pose == null) _pose = ActionPose.For(gameObject);
            _pose.SetEating(holding);

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

        private bool HasUseFor(Edible e)
        {
            if (e.healAmount > 0f && !IsHealthFull()) return true;
            if (e.staminaAmount > 0f && LastTruck.PlayerStamina.Instance != null &&
                LastTruck.PlayerStamina.Instance.Current < LastTruck.PlayerStamina.Instance.Max - 0.5f) return true;
            if (e.repairFraction > 0f && _inventory.HasDamagedTool()) return true;
            if (e.weaponUpgrade && PlayerGear.CanUpgradeWeapon) return true;
            return false;
        }


        private float _nextNoUseToast;

        private string NoUseReason(ItemData item, Edible e)
        {
            if (e.repairFraction > 0f) return "고칠 도구가 없어요 (가방에 닳은 도구/무기가 있어야 해요)";
            if (e.weaponUpgrade) return "무기 강화가 이미 최대예요 (" + PlayerGear.WeaponSummary + ")";
            string used = e.verb == "사용" ? "써도" : e.verb == "마시기" ? "마셔도" : "먹어도";
            return item.itemName + ": 지금은 " + used + " 효과가 없어요 (체력/기력이 가득해요)";
        }

        private bool IsPointerOverUi()
        {
            if (GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen) return true;
            return UnityEngine.EventSystems.EventSystem.current != null &&
                   UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>손에 든 아이템이 먹거나 쓸 수 있는 것이면 화면 안내 문구 (아니면 null). 핫바 위 사용법 줄에 쓴다.</summary>
        public static string UseHint(ItemData item)
        {
            if (item == null || !Edibles.TryGetValue(item.itemID, out Edible e)) return null;
            if (e.repairFraction > 0f)
                return "왼쪽 클릭 꾹 · 가방에서 가장 닳은 도구를 " + Mathf.RoundToInt(e.repairFraction * 100f) + "% 수리";
            if (e.weaponUpgrade)
                return PlayerGear.CanUpgradeWeapon
                    ? "왼쪽 클릭 꾹 · 무기 강화 Lv." + PlayerGear.WeaponLevel + " → " + (PlayerGear.WeaponLevel + 1) + " (모든 공격 피해 +" + Mathf.RoundToInt(PlayerGear.DamagePerLevel * 100f) + "%)"
                    : "무기 강화 최대 (" + PlayerGear.WeaponSummary + ")";
            string fx = "";
            if (e.healAmount > 0f) fx += "체력 +" + Mathf.RoundToInt(e.healAmount);
            if (e.staminaAmount > 0f) fx += (fx.Length > 0 ? ", " : "") + "기력 +" + Mathf.RoundToInt(e.staminaAmount);
            return "왼쪽 클릭 꾹 · " + (string.IsNullOrEmpty(e.verb) ? "먹기" : e.verb) + " (" + fx + ")";
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
            // 수리는 소모하기 전에 고칠 도구를 정한다 (키트 자신은 내구도가 없으므로 상관없다)
            ItemData repaired = null;
            if (edible.repairFraction > 0f && !_inventory.RepairWorstTool(edible.repairFraction, out repaired))
                return;

            if (edible.weaponUpgrade && !PlayerGear.CanUpgradeWeapon)
                return;

            if (!_inventory.RemoveItem(item, 1))
                return;

            // 효과 빛: 체력(초록) / 스태미나(노랑) / 수리·강화(주황)
            ActionFx.Consume(transform.position + Vector3.up * 1.1f, edible.healAmount > 0f, edible.staminaAmount > 0f,
                             edible.repairFraction > 0f || edible.weaponUpgrade);

            if (edible.weaponUpgrade && PlayerGear.UpgradeWeapon())
                LastTruck.HudToast.Show("무기를 강화했어요! " + PlayerGear.WeaponSummary);

            if (repaired != null)
                LastTruck.HudToast.Show($"{repaired.itemName}을(를) 수리했어요");

            if (edible.staminaAmount > 0f && LastTruck.PlayerStamina.Instance != null)
                LastTruck.PlayerStamina.Instance.Restore(edible.staminaAmount);

            if (edible.healAmount <= 0f)
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
