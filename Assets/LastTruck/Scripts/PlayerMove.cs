using UnityEngine;

namespace LastTruck
{
    public class PlayerMove : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float speed = 5f;
        public new Rigidbody rigidbody;
        public Transform cameraTransform;
        public Behaviour cinemachineInputController;

        private float hAxis;
        private float vAxis;
        private bool wDown;

        private Vector3 moveVec;
        private Animator anim;
        private Character character;

        private bool _movementLocked;
        private float _speedMultiplier = 1f;

        private bool _isCursorUnlocked = false;

        public bool IsUIOpen { get; set; } = false;

        public void SetMovementLocked(bool locked)
        {
            _movementLocked = locked;
        }

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
                rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (cinemachineInputController == null)
            {
                var components = FindObjectsOfType<Behaviour>();
                foreach (var comp in components)
                {
                    if (comp != null && comp.GetType().Name.Contains("CinemachineInput"))
                    {
                        cinemachineInputController = comp;
                        break;
                    }
                }
            }

            _isCursorUnlocked = false;
            UpdateCursorState();
            SyncSpeedFromCharacter();
        }

        private void OnEnable()
        {
            SyncSpeedFromCharacter();
            UpdateCursorState();
        }

        private void SyncSpeedFromCharacter()
        {
            if (character != null && character.CurrentMoveSpeed > 0)
            {
                speed = character.CurrentMoveSpeed;
            }
        }

        private void Update()
        {
            // Left Alt 키로 자유 커서 / 시점 고정 토글
            if (Input.GetKeyDown(KeyCode.LeftAlt))
            {
                _isCursorUnlocked = !_isCursorUnlocked;
                UpdateCursorState();
            }

            // UI가 켜져 있거나 이동이 잠긴 경우에만 이동 입력 차단
            bool shouldLockMovement = _movementLocked || IsUIOpen;

            if (shouldLockMovement)
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
                anim.SetBool("isRun", isMoving && !wDown);
                anim.SetBool("isWalk", isMoving && wDown);
            }
        }

        /// <summary>
        /// 카메라 및 마우스 커서 상태 제어
        /// </summary>
        public void UpdateCursorState()
        {
            bool unlockCursor = _isCursorUnlocked || IsUIOpen;

            if (unlockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SetCinemachineInputEnabled(false);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                SetCinemachineInputEnabled(true);
            }
        }

        private void SetCinemachineInputEnabled(bool isEnabled)
        {
            if (cinemachineInputController != null)
            {
                cinemachineInputController.enabled = isEnabled;
            }
        }

        private void FixedUpdate()
        {
            if (cameraTransform == null || rigidbody == null) return;

            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0f;
            camRight.y = 0f;

            if (camForward.sqrMagnitude > 0.001f) camForward.Normalize();
            if (camRight.sqrMagnitude > 0.001f) camRight.Normalize();

            moveVec = (camForward * vAxis + camRight * hAxis);

            if (moveVec.sqrMagnitude > 1f)
            {
                moveVec.Normalize();
            }

            float currentSpeed = speed * (wDown ? 0.3f : 1f) * _speedMultiplier;

            Vector3 targetVelocity = moveVec * currentSpeed;

            // Unity 버전 호환성 지원 (linearVelocity / velocity)
#if UNITY_6000_0_OR_NEWER
            targetVelocity.y = rigidbody.linearVelocity.y;
            rigidbody.linearVelocity = targetVelocity;
#else
            targetVelocity.y = rigidbody.velocity.y;
            rigidbody.velocity = targetVelocity;
#endif

            if (moveVec.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveVec);
                rigidbody.MoveRotation(Quaternion.Slerp(rigidbody.rotation, targetRotation, Time.fixedDeltaTime * 15f));
            }
        }
    }
}