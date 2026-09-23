using UnityEngine;

namespace LastTruck
{
    public class PlayerMove : MonoBehaviour
    {
        public float speed = 5f;
        public Transform cameraTransform;
        public Rigidbody rigidbody;

        float hAxis;
        float vAxis;
        bool wDown;

        Vector3 moveVec;
        Animator anim;
        Character character;

        private void Start()
        {
            anim = GetComponentInChildren<Animator>();
            character = GetComponent<Character>();

            if (rigidbody == null)
            {
                rigidbody = GetComponent<Rigidbody>();
            }

            if (rigidbody != null)
            {
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }

            if (character != null && character.stats != null && character.stats.moveSpeed > 0)
            {
                speed = character.stats.baseSpeed;
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            hAxis = Input.GetAxisRaw("Horizontal");
            vAxis = Input.GetAxisRaw("Vertical");
            wDown = Input.GetButton("Walk");

            if (anim != null)
            {
                bool isMoving = (hAxis != 0 || vAxis != 0);
                anim.SetBool("isRun", isMoving);
                anim.SetBool("isWalk", wDown);
            }
        }

        private void FixedUpdate()
        {
            if (cameraTransform == null || rigidbody == null) return;

            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            moveVec = (camForward * vAxis + camRight * hAxis).normalized;

            float currentSpeed = speed * (wDown ? 0.3f : 1f);

            Vector3 targetVelocity = moveVec * currentSpeed;
            targetVelocity.y = rigidbody.linearVelocity.y;

            rigidbody.linearVelocity = targetVelocity;

            if (moveVec != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveVec);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 20f);
            }
        }
    }
}