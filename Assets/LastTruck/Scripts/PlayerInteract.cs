using UnityEngine;

namespace LastTruck
{
    public class PlayerInteract : MonoBehaviour
    {
        [Header("상호작용 설정")]
        public float interactRange = 3f;
        public LayerMask interactLayer;
        public KeyCode interactKey = KeyCode.E;

        private IInteractable currentInteractable;
        private float _holdTimer;

        public bool IsHolding { get; private set; }
        public float HoldProgress01 { get; private set; }
        public bool HasNearbyInteractable => currentInteractable != null;

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
                    _holdTimer += Time.deltaTime;
                    IsHolding = true;
                    HoldProgress01 = Mathf.Clamp01(_holdTimer / holdable.RequiredHoldSeconds);

                    if (_holdTimer >= holdable.RequiredHoldSeconds)
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

        private void ResetHoldState()
        {
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