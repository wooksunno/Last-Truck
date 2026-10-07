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

        [Header("지형 이동 보정")]
        [Tooltip("자동으로 넘어갈 수 있는 턱 높이(m). 철도 레일(받침목 포함 최대 약 0.42m)을 넘을 수 있는 높이")]
        [SerializeField] private float stepHeight = 0.45f;
        [Tooltip("걸어서 오를 수 있는 최대 경사(도)")]
        [SerializeField] private float maxSlopeAngle = 50f;
        [SerializeField] private float groundCheckDistance = 0.3f;
        [SerializeField] private LayerMask groundMask = ~0;

        private CapsuleCollider _capsule;
        private bool _grounded;
        private Vector3 _groundNormal = Vector3.up;

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

            // 마찰 0 재질: 벽/경사면/메시 이음새에 몸이 달라붙어 멈추는 현상 방지
            _capsule = GetComponent<CapsuleCollider>();
            if (_capsule != null && _capsule.sharedMaterial == null)
            {
                _capsule.sharedMaterial = new PhysicsMaterial("PlayerFrictionless")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
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

        private void OnDisable()
        {
            // 트럭 탑승 등으로 꺼질 때 중력을 원래대로 돌려둔다 (땅 위에서는 중력을 꺼두므로)
            if (rigidbody != null) rigidbody.useGravity = true;
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

            CheckGround();

            if (_grounded)
            {
                // 땅 위: 경사면을 따라 이동하고, 마찰 0이어도 미끄러지지 않도록 중력 대신 땅에 붙여준다
                rigidbody.useGravity = false;
                targetVelocity = Vector3.ProjectOnPlane(targetVelocity, _groundNormal).normalized * targetVelocity.magnitude;
                targetVelocity -= _groundNormal * 1.5f;

                if (moveVec.sqrMagnitude > 0.001f)
                {
                    TryStepUp(moveVec.normalized);
                }
            }
            else
            {
                rigidbody.useGravity = true;
                // 점프가 없으므로 공중에서는 위로 올라가지 않는다.
                // (경사로/레일 끝에서 오르던 속도나 충돌 밀어내기 때문에 몸이 붕 뜨는 현상 방지)
#if UNITY_6000_0_OR_NEWER
                targetVelocity.y = Mathf.Min(rigidbody.linearVelocity.y, 0f);
#else
                targetVelocity.y = Mathf.Min(rigidbody.velocity.y, 0f);
#endif
            }

            // Unity 버전 호환성 지원 (linearVelocity / velocity)
#if UNITY_6000_0_OR_NEWER
            rigidbody.linearVelocity = targetVelocity;
#else
            rigidbody.velocity = targetVelocity;
#endif

            if (moveVec.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveVec);
                rigidbody.MoveRotation(Quaternion.Slerp(rigidbody.rotation, targetRotation, Time.fixedDeltaTime * 15f));
            }
        }

        // 캡슐 아래쪽 구의 중심 (월드 좌표)
        private Vector3 CapsuleBottomCenter(out float radius)
        {
            float scaleXZ = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            radius = _capsule.radius * scaleXZ;
            float halfHeight = Mathf.Max(_capsule.height * 0.5f * Mathf.Abs(transform.lossyScale.y), radius);
            return transform.TransformPoint(_capsule.center) - Vector3.up * (halfHeight - radius);
        }

        private void CheckGround()
        {
            _grounded = false;
            _groundNormal = Vector3.up;
            if (_capsule == null) return;

            Vector3 bottom = CapsuleBottomCenter(out float radius);
            Vector3 origin = bottom + Vector3.up * 0.1f;
            if (Physics.SphereCast(origin, radius * 0.9f, Vector3.down, out RaycastHit hit,
                    0.1f + groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore)
                && hit.collider != _capsule
                && Vector3.Angle(hit.normal, Vector3.up) <= maxSlopeAngle)
            {
                _grounded = true;

                // 경사 방향은 발 정중앙 바로 아래 지점 기준으로 잰다.
                // 구체가 레일·돌 모서리에 걸쳐 닿으면 모서리의 기울어진 면이 잡혀서 몸을 위로 띄우기 때문.
                if (Physics.Raycast(bottom + Vector3.up * 0.1f, Vector3.down, out RaycastHit center,
                        radius + 0.1f + groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore)
                    && center.collider != _capsule
                    && Vector3.Angle(center.normal, Vector3.up) <= maxSlopeAngle)
                {
                    _groundNormal = center.normal;
                }
            }
        }

        // 앞이 낮은 턱(stepHeight 이하)으로 막혀 있으면 그 위로 살짝 올려준다
        private void TryStepUp(Vector3 dir)
        {
            if (_capsule == null) return;

            Vector3 bottom = CapsuleBottomCenter(out float radius);
            Vector3 foot = bottom - Vector3.up * radius;
            float probe = radius + 0.15f;

            // 발 높이에서 앞이 막혔는지 (걸을 수 없는 급경사/벽면)
            if (!Physics.Raycast(foot + Vector3.up * 0.05f, dir, out RaycastHit wall, probe, groundMask, QueryTriggerInteraction.Ignore))
                return;
            if (wall.collider == _capsule || Vector3.Angle(wall.normal, Vector3.up) <= maxSlopeAngle)
                return;

            // 턱 높이 위로는 비어 있는지
            if (Physics.Raycast(foot + Vector3.up * stepHeight, dir, probe, groundMask, QueryTriggerInteraction.Ignore))
                return;

            // 턱 윗면 높이 찾기
            Vector3 down = foot + Vector3.up * stepHeight + dir * probe;
            if (Physics.Raycast(down, Vector3.down, out RaycastHit top, stepHeight, groundMask, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(top.normal, Vector3.up) <= maxSlopeAngle)
            {
                float rise = top.point.y - foot.y;
                if (rise > 0.01f && rise <= stepHeight)
                {
                    rigidbody.position += Vector3.up * (rise + 0.02f);
                }
            }
        }
    }
}