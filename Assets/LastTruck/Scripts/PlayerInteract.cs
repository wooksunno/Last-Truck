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

private void Update()
        {
            Detect_Interactable();

            IHoldInteractable holdable = currentInteractable as IHoldInteractable;

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
                        _holdTimer = 0f;
                        IsHolding = false;
                        HoldProgress01 = 0f;
                    }
                }
                else
                {
                    _holdTimer = 0f;
                    IsHolding = false;
                    HoldProgress01 = 0f;
                }
            }
            else
            {
                _holdTimer = 0f;
                IsHolding = false;
                HoldProgress01 = 0f;

                if (currentInteractable != null && Input.GetKeyDown(interactKey))
                {
                    currentInteractable.Interact(gameObject);
                }
            }
        }

private void Detect_Interactable()
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, interactRange, interactLayer);

            IInteractable closestInteractable = null;
            float closestDistance = float.MaxValue;

            foreach (var col in hitColliders)
            {
                if (col.TryGetComponent<IInteractable>(out var interactable))
                {
                    float distance = Vector3.Distance(transform.position, col.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestInteractable = interactable;
                    }
                }
            }

            if (!ReferenceEquals(closestInteractable, currentInteractable))
            {
                _holdTimer = 0f;
                IsHolding = false;
                HoldProgress01 = 0f;
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
