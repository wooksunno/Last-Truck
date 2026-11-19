using UnityEngine;

namespace LastTruck
{
    public class PlayerInteract : MonoBehaviour
    {
        [Header("상호작용 설정")]
        public float interactRange = 3f;
        public LayerMask interactLayer;
        public KeyCode interactKey = KeyCode.E;

        [Header("나무/돌 채집 연출")]
        [Tooltip("E키로 나무/돌을 캐는 동안 주먹으로 때리는 간격(초).")]
        public float gatherPunchInterval = 0.6f;
        [Tooltip("곡괭이를 들고 캘 때 내려찍는 간격(초). 곡괭이 모션이 주먹보다 길다.")]
        public float gatherPickaxeInterval = 0.7f;

        private IInteractable currentInteractable;
        private float _holdTimer;
        private float _nextGatherPunch;
        private Combat.WeaponController _weapon;
        private CraftingSystem.PlayerInventory _inventory;

        public bool IsHolding { get; private set; }
        public float HoldProgress01 { get; private set; }
        public bool HasNearbyInteractable => currentInteractable != null;
        public IInteractable CurrentInteractable => currentInteractable;

        /// <summary>지금 E키를 꾹 눌러 트럭을 고치고 있다 (Working_B 애니메이션 + 주변 먼지).</summary>
        public bool IsRepairingTruck => isActiveAndEnabled && IsHolding && currentInteractable is TruckInteractable;

        private static readonly int IsRepairingHash = Animator.StringToHash("isRepairing");
        private Animator _anim;
        private bool _animHasRepairParam;
        private bool _animChecked;
        private bool _shownRepairing;
        private float _nextRepairDust;
        private bool? _isOnline;

        /// <summary>싱글플레이 전용: 수리 애니메이션과 먼지 (멀티플레이는 NetworkPlayerMovement가 모두에게 동기화해서 보여 준다).</summary>
        private void LateUpdate()
        {
            if (_isOnline == null) _isOnline = GetComponent<LastTruck.Networking.NetworkPlayerMovement>() != null;
            if (_isOnline.Value) return;
            ShowRepairing(this, transform, IsRepairingTruck, ref _anim, ref _animChecked, ref _animHasRepairParam, ref _shownRepairing, ref _nextRepairDust);
        }

        private void OnDisable()
        {
            if (_isOnline == true) return;
            if (_anim != null && _animHasRepairParam) _anim.SetBool(IsRepairingHash, false);
            _shownRepairing = false;
        }

        /// <summary>수리 중 표시 (애니메이터 isRepairing + 주변 먼지). 싱글/멀티가 같이 쓴다.</summary>
        public static void ShowRepairing(Component owner, Transform body, bool repairing, ref Animator anim, ref bool animChecked, ref bool hasParam, ref bool shown, ref float nextDust)
        {
            if (anim == null || !animChecked)
            {
                anim = owner.GetComponentInChildren<Animator>();
                animChecked = true;
                hasParam = false;
                if (anim != null)
                    foreach (AnimatorControllerParameter p in anim.parameters)
                        if (p.nameHash == IsRepairingHash) { hasParam = true; break; }
            }

            if (repairing != shown)
            {
                shown = repairing;
                if (anim != null && hasParam) anim.SetBool(IsRepairingHash, repairing);
                if (repairing) nextDust = 0f;
            }

            if (repairing && Time.time >= nextDust)
            {
                nextDust = Time.time + 0.14f;
                MovementDust.RepairPuff(body.position);
            }
        }

        private void Update()
        {
            Detect_Interactable();

            if (currentInteractable == null)
            {
                ResetHoldState();
                return;
            }

            IHoldInteractable holdable = currentInteractable as IHoldInteractable;

            // 홀드형 상호작용 (누르고 있기)
            if (holdable != null && holdable.RequiredHoldSeconds > 0f)
            {
                if (Input.GetKey(interactKey))
                {
                    // 진행도는 "초"가 아니라 "비율"로 쌓는다.
                    // (초로 쌓으면 느린 도구로 캐다가 빠른 도구로 바꿨을 때, 쌓인 시간이 빠른 도구 기준으로 계산돼 바로 완료되던 문제)
                    _holdTimer += Time.deltaTime / holdable.RequiredHoldSeconds;
                    IsHolding = true;
                    HoldProgress01 = Mathf.Clamp01(_holdTimer);
                    UpdateGatherPunch();

                    if (_holdTimer >= 1f)
                    {
                        currentInteractable.Interact(gameObject);
                        ResetHoldState();
                    }
                }
                else
                {
                    ResetHoldState();
                }
            }
            // 일반 클릭형 상호작용 (단발성 E키)
            else
            {
                ResetHoldState();

                if (Input.GetKeyDown(interactKey))
                {
                    currentInteractable.Interact(gameObject);
                }
            }
        }

        /// <summary>
        /// 나무/돌을 캐는 동안 일정 간격으로 때리는 연출 + 지금 캐고 있는 대상 흔들기 (처음 누르는 순간 바로 한 번).
        /// 손(1/2/3 슬롯)에 곡괭이를 들고 있으면 곡괭이로 내려찍고, 아니면 맨손 주먹.
        /// </summary>
        private void UpdateGatherPunch()
        {
            // 나무/돌(GatherableProp) 또는 광물(구리/철/금/다이아 - SpecialResourceNode)
            Transform target = null;
            if (currentInteractable is CraftingSystem.GatherableProp prop && Combat.WeaponController.IsPunchGatherItem(prop.ItemId))
                target = prop.transform;
            else if (currentInteractable is CraftingSystem.SpecialResourceNode ore && ore.IsMineral && !ore.IsDepleted)
                target = ore.transform;
            if (target == null)
                return;
            if (Time.time < _nextGatherPunch)
                return;

            if (_weapon == null)
                _weapon = GetComponent<Combat.WeaponController>();
            if (_weapon == null || !_weapon.isActiveAndEnabled)
                return;

            if (_inventory == null)
                _inventory = GetComponent<CraftingSystem.PlayerInventory>();
            CraftingSystem.ItemData hand = _inventory != null ? _inventory.SelectedItem : null;
            // 곡괭이, 또는 나무를 벨 때의 도끼는 내려찍는 모션
            bool woodWithAxe = hand != null && Combat.WeaponController.IsAxe(hand.itemID)
                               && currentInteractable is CraftingSystem.GatherableProp wp && wp.ItemId == CraftingSystem.ItemIds.Wood;
            bool withPickaxe = hand != null && (Combat.WeaponController.IsPickaxe(hand.itemID) || woodWithAxe);

            _nextGatherPunch = Time.time + (withPickaxe ? gatherPickaxeInterval : gatherPunchInterval);
            _weapon.PlayGatherHit(target, withPickaxe);
        }

        private void ResetHoldState()
        {
            // E키를 뗐다가 다시 누르면 바로 첫 타격이 나가게 한다 (계속 누른 채 다음 나무로 넘어가면 간격 유지)
            if (!IsHolding)
                _nextGatherPunch = 0f;
            _holdTimer = 0f;
            IsHolding = false;
            HoldProgress01 = 0f;
        }

        private void Detect_Interactable()
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, interactRange, interactLayer);

            IInteractable closestInteractable = null;
            float closestDistance = float.MaxValue;
            int bestPriority = int.MinValue;

            // 우선순위가 높은 대상 먼저, 같으면 가장 가까운 대상 (예: 나무 옆의 풀보다 나무를 먼저 캔다)
            foreach (var col in hitColliders)
            {
                if (col.TryGetComponent<IInteractable>(out var interactable))
                {
                    int priority = interactable is IInteractPriority p ? p.InteractPriority : 0;
                    float distance = Vector3.Distance(transform.position, col.transform.position);
                    if (priority > bestPriority || (priority == bestPriority && distance < closestDistance))
                    {
                        bestPriority = priority;
                        closestDistance = distance;
                        closestInteractable = interactable;
                    }
                }
            }

            if (!ReferenceEquals(closestInteractable, currentInteractable))
            {
                ResetHoldState();
            }

            currentInteractable = closestInteractable;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactRange);
        }
    }
}