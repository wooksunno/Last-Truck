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

        private bool _movementLocked;
        private float _speedMultiplier = 1f;

        /// <summary>공격 중 등 특정 동작 동안 WASD 이동을 잠글 때 사용한다.</summary>
        public void SetMovementLocked(bool locked)
        {
            _movementLocked = locked;
        }

        /// <summary>활 차징·화염방사 중처럼 이동을 느리게 해야 할 때 사용한다. 1이면 원래 속도.</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0f, multiplier);
        }

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
                // 회전은 전부 스크립트가 정한다. Y까지 고정하지 않으면 몬스터/트럭에 비스듬히 부딪힐 때
                // 생긴 회전 속도가 계속 남아서, 키를 놓은 뒤에도 캐릭터가 제자리에서 빙글빙글 돈다.
                rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
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
            if (_movementLocked)
            {
                hAxis = 0f;
                vAxis = 0f;
                wDown = false;
            }
            else
            {
                hAxis = Input.GetAxisRaw("Horizontal");
                vAxis = Input.GetAxisRaw("Vertical");
                wDown = Input.GetButton("Walk");
            }

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

            float currentSpeed = speed * (wDown ? 0.3f : 1f) * _speedMultiplier;

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