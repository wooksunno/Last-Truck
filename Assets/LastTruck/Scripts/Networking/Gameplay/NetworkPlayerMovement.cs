using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 멀티플레이 캐릭터의 이동 + 이동 애니메이션.
    ///
    /// - 이동은 Fusion의 NetworkCharacterController(Unity CharacterController 기반)로 한다.
    ///   Rigidbody와 달리 몬스터/다른 플레이어에게 물리적으로 밀리지 않는다.
    /// - 입력(NetworkInputData)은 각자 자기 컴퓨터에서 만들고(GameLauncher.OnInput),
    ///   호스트가 그 입력으로 모든 캐릭터를 움직인다. 내 캐릭터는 내 컴퓨터에서도 미리 움직여서(예측) 지연이 느껴지지 않는다.
    /// - 위치/회전은 NetworkCharacterController가, "걷기 키" 상태는 이 스크립트의 IsWalking이 모두에게 동기화된다.
    /// - 애니메이터의 isRun / isWalk는 동기화된 속도와 IsWalking으로 모든 컴퓨터에서 각자 계산한다.
    /// - 사망했거나 게임이 아직 시작 전/끝난 뒤에는 움직이지 않는다 (중력만 적용).
    /// - 트럭에 타 있으면 좌석 위치에 붙어 있고 충돌을 끈다 (NetworkTruck).
    ///
    /// 기존 싱글플레이 PlayerMove와 같은 속도 규칙: 이동 속도 = 캐릭터 스탯 baseSpeed (moveSpeed × 0.3), 걷기 = × 0.3.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkCharacterController))]
    public class NetworkPlayerMovement : NetworkBehaviour
    {
        #region 인스펙터 / 상태

        [Tooltip("걷기 키(Walk)를 누르고 있을 때 속도 배율 (기존 PlayerMove와 같은 0.3).")]
        [SerializeField] private float walkSpeedMultiplier = 0.3f;

        [Tooltip("캐릭터 스탯이 없을 때 쓸 이동 속도.")]
        [SerializeField] private float fallbackMoveSpeed = 5f;

        [Tooltip("이 속도(m/s)보다 빠르게 움직이고 있으면 달리기 애니메이션.")]
        [SerializeField] private float runAnimationThreshold = 0.15f;

        [Networked] public NetworkBool IsWalking { get; set; }

        private NetworkCharacterController _controller;
        private CharacterController _characterController;
        private Character _character;
        private Animator _animator;
        private NetworkHealth _health;

        private static readonly int IsRunHash = Animator.StringToHash("isRun");
        private static readonly int IsWalkHash = Animator.StringToHash("isWalk");

        #endregion

        #region 조회

        /// <summary>달리기 속도 (m/s).</summary>
        public float MoveSpeed
        {
            get
            {
                if (_character != null && _character.stats != null && _character.stats.moveSpeed > 0f)
                {
                    return _character.stats.baseSpeed;
                }
                return fallbackMoveSpeed;
            }
        }

        /// <summary>수평 이동 속도 (m/s). 애니메이션/발소리 등에 사용.</summary>
        public float HorizontalSpeed
        {
            get
            {
                if (_controller == null || Object == null || !Object.IsValid) return 0f;
                Vector3 velocity = _controller.Velocity;
                velocity.y = 0f;
                return velocity.magnitude;
            }
        }

        /// <summary>지금 조작할 수 있는가 (살아 있고, 게임이 진행 중).</summary>
        private bool CanControl => (_health == null || !_health.IsDead) && NetworkGameState.AllowPlayerControl;

        #endregion

        #region 생명주기

        private void Awake()
        {
            _controller = GetComponent<NetworkCharacterController>();
            _characterController = GetComponent<CharacterController>();
            _character = GetComponent<Character>();
            _animator = GetComponentInChildren<Animator>();
            _health = GetComponent<NetworkHealth>();
        }

        public override void Spawned()
        {
            if (_animator != null) _animator.applyRootMotion = false;
        }

        #endregion

        #region 이동 (네트워크 틱)

        public override void FixedUpdateNetwork()
        {
            // 입력은 호스트(모든 캐릭터)와 자기 캐릭터를 예측하는 본인 컴퓨터에서만 받을 수 있다.
            bool hasInput = GetInput(out NetworkInputData input);
            if (!hasInput && !Object.HasStateAuthority) return;

            // 트럭에 타 있으면 걷지 않고 좌석에 붙어 있는다 (충돌 끔 → 트럭을 밀지 않음).
            if (NetworkTruck.IsSeated(Object.InputAuthority))
            {
                SetSeatedCollision(true);
                StaySeated();
                return;
            }
            SetSeatedCollision(false);

            if (!hasInput || !CanControl)
            {
                // 입력이 없거나(참가자 로딩 중 등) 조작할 수 없는 상태: 중력만 적용하고 멈춰 세운다.
                StandStill();
                return;
            }

            Vector3 direction = input.MoveDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            bool walking = input.Buttons.IsSet(NetworkInputButton.Walk);
            IsWalking = walking;

            float speed = MoveSpeed * (walking ? walkSpeedMultiplier : 1f);
            _controller.maxSpeed = speed;

            // NetworkCharacterController는 기본적으로 가속/감속이 있는데, 기존 조작감(키를 누르는 즉시 최고 속도,
            // 떼는 즉시 정지)과 같게 하려고 수평 속도를 목표값으로 먼저 맞춰 둔다. 수직 속도(중력)는 그대로 둔다.
            Vector3 velocity = _controller.Velocity;
            velocity.x = direction.x * speed;
            velocity.z = direction.z * speed;
            _controller.Velocity = velocity;

            _controller.Move(direction);

            // 무기 조준 등으로 "이쪽을 봐라"는 요청이 있으면 이동 방향 회전보다 우선한다.
            Vector3 face = input.FaceDirection;
            face.y = 0f;
            if (face.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(face.normalized);
            }
        }

        private void StaySeated()
        {
            _controller.Velocity = Vector3.zero;
            IsWalking = false;

            // 좌석 위치는 호스트가 정한다 (클라이언트의 내 캐릭터는 호스트 값으로 맞춰진다).
            if (Object.HasStateAuthority && NetworkTruck.TryGetSeatPose(out Vector3 position, out Quaternion rotation))
            {
                _controller.Teleport(position, rotation);
            }
        }

        /// <summary>트럭 안에 숨어 있는 동안 캐릭터 충돌을 끈다 (트럭 물리와 부딪혀 트럭을 밀어내지 않게).</summary>
        private void SetSeatedCollision(bool seated)
        {
            if (_characterController == null) return;
            bool detect = !seated;
            if (_characterController.detectCollisions != detect) _characterController.detectCollisions = detect;
        }

        private void StandStill()
        {
            Vector3 idle = _controller.Velocity;
            idle.x = 0f;
            idle.z = 0f;
            _controller.Velocity = idle;
            _controller.Move(Vector3.zero);
            IsWalking = false;
        }

        #endregion

        #region 애니메이션

        public override void Render()
        {
            if (_animator == null) return;
            bool alive = (_health == null || !_health.IsDead) && !NetworkTruck.IsSeated(Object.InputAuthority);
            _animator.SetBool(IsRunHash, alive && HorizontalSpeed > runAnimationThreshold);
            _animator.SetBool(IsWalkHash, alive && IsWalking);
        }

        #endregion
    }
}
