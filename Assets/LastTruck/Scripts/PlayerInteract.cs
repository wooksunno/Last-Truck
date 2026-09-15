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

        private void Update()
        {
            Detect_Interactable();

            if (currentInteractable != null && Input.GetKeyDown(interactKey))
            {
                currentInteractable.Interact(gameObject);
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

            currentInteractable = closestInteractable;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactRange);
        }
    }
}
