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

        // 멀티플레이 캐릭터(NetworkPlayerMovement가 붙은 프리팹)는 이동/회전/애니메이션을 네트워크 쪽이 처리한다.
        // 이 스크립트는 다른 스크립트(무기, 트럭 탑승 등)가 쓰는 창구 역할만 한다.
        private bool _networkControlled;
        private float _facingTime = float.NegativeInfinity;
        private Quaternion _pendingFacing;

        // 조준 요청은 한 프레임에 한 번 오지만 네트워크 틱은 한 프레임에 여러 번 돌 수 있으므로,
        // 마지막 요청 후 이 시간 동안은 계속 같은 방향을 보낸다 (조준 중 떨림 방지).
        private const float FacingHoldSeconds = 0.12f;

        /// <summary>멀티플레이 캐릭터면 true (이동은 LastTruck.Networking.NetworkPlayerMovement가 담당).</summary>
        public bool IsNetworkControlled => _networkControlled;

        private void Awake()
        {
            _networkControlled = GetComponent<LastTruck.Networking.NetworkPlayerMovement>() != null;
        }

        /// <summary>
        /// 캐릭터가 바라보는 방향을 즉시 바꾼다 (무기 조준 등).
        /// 멀티플레이에서는 transform만 바꾸면 다음 네트워크 틱에 원래 방향으로 되돌아가므로,
        /// 이 함수를 통해 바꿔야 방향이 입력으로 호스트에게 전달되어 모두에게 동기화된다.
        /// </summary>
        public static void SetFacing(Transform target, Quaternion rotation)
        {
            if (target == null) return;
            target.rotation = rotation;

            PlayerMove move = target.GetComponent<PlayerMove>();
            if (move == null) return;

            if (move._networkControlled)
            {
                move._pendingFacing = rotation;
                move._facingTime = Time.unscaledTime;
            }
            else if (move.rigidbody != null)
            {
                move.rigidbody.MoveRotation(rotation);
            }
        }

        /// <summary>네트워크 입력을 만들 때 호출: 최근(조준 중)에 요청된 방향이 있으면 돌려준다.</summary>
        public bool TryConsumeFacing(out Quaternion rotation)
        {
            rotation = _pendingFacing;
            return Time.unscaledTime - _facingTime <= FacingHoldSeconds;
        }

        private void Start()
        {
            if (_networkControlled) return;

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
            if (_networkControlled) return;

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
            if (_networkControlled) return;
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
                rigidbody.MoveRotation(Quaternion.Slerp(rigidbody.rotation, targetRotation, Time.fixedDeltaTime * 20f));
            }
        }
    }
}