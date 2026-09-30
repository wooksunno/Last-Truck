using UnityEngine;

namespace LastTruck
{
    public class PlayerMove : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float speed = 5f;
        public Transform cameraTransform;
        public Rigidbody rigidbody;

        private float hAxis;
        private float vAxis;
        private bool wDown;

        private Vector3 moveVec;
        private Animator anim;
        private Character character;

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
                rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            SyncSpeedFromCharacter();
        }

        private void OnEnable()
        {
            // 트럭 하차 등 스크립트가 다시 활성화될 때 스탯 동기화
            SyncSpeedFromCharacter();
        }

        private void SyncSpeedFromCharacter()
        {
            if (character != null)
            {
                if (character.CurrentMoveSpeed > 0)
                {
                    speed = character.CurrentMoveSpeed;
                }
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
                anim.SetBool("isRun", isMoving && !wDown);
                anim.SetBool("isWalk", isMoving && wDown);
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

            float currentSpeed = speed * (wDown ? 0.5f : 1f);

            Vector3 targetVelocity = moveVec * currentSpeed;
            targetVelocity.y = rigidbody.linearVelocity.y;

            rigidbody.linearVelocity = targetVelocity;

            if (moveVec != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveVec);
                rigidbody.MoveRotation(Quaternion.Slerp(rigidbody.rotation, targetRotation, Time.fixedDeltaTime * 20f));
            }
        }
    }
}