using System.Collections.Generic;
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
    /// - 기절했거나 게임이 아직 시작 전/끝난 뒤에는 움직이지 않는다 (중력만 적용).
    /// - Space: 점프 (땅에 있을 때만). 점프 횟수(JumpCount)가 동기화되어 모든 화면에서 점프 애니메이션이 나온다.
    /// - 트럭에 타 있으면 좌석 위치에 붙어 있고 충돌을 끈다 (NetworkTruck).
    ///
    /// 기존 싱글플레이 PlayerMove와 같은 속도 규칙: 이동 속도 = Character.CurrentMoveSpeed (스탯 moveSpeed × 0.3), 걷기 = × 0.3.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkCharacterController))]
    public class NetworkPlayerMovement : NetworkBehaviour
    {
        #region 인스펙터 / 상태

        [Tooltip("달리기 키(Shift, 입력 이름은 Walk)를 누르고 있을 때 속도 배율 (싱글플레이 PlayerMove의 sprintMultiplier와 같은 값).")]
        [SerializeField] private float sprintSpeedMultiplier = 1.6f;

        [Tooltip("캐릭터 스탯이 없을 때 쓸 이동 속도.")]
        [SerializeField] private float fallbackMoveSpeed = 5f;

        [Tooltip("이 속도(m/s)보다 빠르게 움직이고 있으면 달리기 애니메이션.")]
        [SerializeField] private float runAnimationThreshold = 0.15f;

        [Header("점프")]
        [Tooltip("점프할 때 위로 주는 속도 (m/s). NetworkCharacterController의 중력과 함께 점프 높이를 정한다.")]
        [SerializeField] private float jumpImpulse = 7.4f;
        [Tooltip("중력 (m/s², 음수). 클수록 점프가 짧고 묵직하다. 싱글플레이 점프와 비슷한 느낌으로 맞춰 두었다.")]
        [SerializeField] private float jumpGravity = -28f;
        [Tooltip("점프할 때 재생할 애니메이터 상태 이름 (없으면 애니메이션 없이 점프만).")]
        [SerializeField] private string jumpStateName = "Jumping Up";

        [Header("가속감")]
        [Tooltip("최고 속도까지 붙는 가속도 (m/s²). 45면 달리기 속도(6)까지 약 0.13초.")]
        [SerializeField] private float moveAcceleration = 45f;
        [Tooltip("키를 뗐을 때 멈추는 감속도 (m/s²). 55면 약 0.11초, 0.3m 미끄러지며 선다.")]
        [SerializeField] private float moveDeceleration = 55f;
        [Tooltip("반대쪽으로 꺾을 때 감속도 (m/s²). 크면 방향 전환이 빠릿하다.")]
        [SerializeField] private float moveTurnDeceleration = 95f;
        [Tooltip("몸이 이동 방향으로 도는 빠르기 (Slerp 배율/초). 12면 90도에 약 0.15초.")]
        [SerializeField] private float turnSpeed = 12f;

        [Header("달리기 모션 발 맞추기")]
        [Tooltip("달리기 모션(Running_B)이 1배속일 때 발이 땅을 미는 속도 (m/s, 모델 크기 1 기준). 실제 속도 ÷ 이 값으로 재생한다.")]
        [SerializeField] private float runClipGroundSpeed = 3.56f;
        [Tooltip("전력 질주 모션(Running_A)의 발 속도 (m/s).")]
        [SerializeField] private float sprintClipGroundSpeed = 2.54f;
        [Tooltip("달리기 모션 재생 배율 범위 (너무 빠르면 다리가 헛돈다).")]
        [SerializeField] private Vector2 runAnimMulRange = new Vector2(0.75f, 2.1f);

        /// <summary>방향키로 만든 수평 이동 속도 (튕겨 나가는 속도/중력 제외). 예측과 호스트가 같은 값으로 계산하도록 동기화한다.</summary>
        [Networked] private Vector3 MoveVel { get; set; }

        [Networked] public NetworkBool IsWalking { get; set; }
        /// <summary>이 플레이어가 가방에 가진 가장 좋은 갑옷 등급 (0~3). 호스트가 받는 피해를 줄일 때 쓴다.</summary>
        [Networked] public int ArmorTier { get; set; }
        [Networked] private NetworkButtons PreviousButtons { get; set; }
        [Networked] private int JumpCount { get; set; }

        [Header("튕겨 나가기 (달리는 트럭에서 내릴 때)")]
        [Tooltip("땅에서 튕겨 나가는 속도가 줄어드는 정도 (m/s²). 작을수록 멀리 미끄러진다.")]
        [SerializeField] private float flingGroundDecel = 11f;
        [Tooltip("공중에서 줄어드는 정도 (m/s²).")]
        [SerializeField] private float flingAirDecel = 2.5f;
        [Tooltip("튕겨 나가는 속도가 이만큼(m/s) 이상이면 방향키가 거의 듣지 않는다 (몸을 못 가누는 느낌).")]
        [SerializeField] private float flingNoControlSpeed = 7f;

        /// <summary>트럭에서 튕겨 나갈 때 남은 수평 속도 (모든 컴퓨터가 같은 값으로 예측한다).</summary>
        [Networked] private Vector3 FlingVelocity { get; set; }
        /// <summary>이번에 위로 튀어 오를 속도 (한 번 쓰고 0).</summary>
        [Networked] private float FlingUp { get; set; }

        /// <summary>호스트 전용: 캐릭터를 velocity로 튕겨 보낸다 (y는 위로 튀는 속도).</summary>
        public void Fling(Vector3 velocity)
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority) return;
            FlingVelocity = new Vector3(velocity.x, 0f, velocity.z);
            FlingUp = Mathf.Max(0f, velocity.y);
        }

        /// <summary>맞은 쪽(때린 몬스터 방향). 잠깐 그쪽으로 몸을 돌려서, 앞에서 맞는 피격 동작과 방향이 맞게 한다.</summary>
        [Networked] private Vector3 HitFaceDir { get; set; }
        [Networked] private TickTimer HitFaceTimer { get; set; }

        /// <summary>호스트 전용: 몬스터에게 맞았다. 때린 쪽 반대로 밀려나고, 때린 쪽으로 몸을 돌린다.</summary>
        public void PushFromHit(Vector3 velocity)
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority) return;
            Fling(velocity);
            Vector3 face = -velocity;
            face.y = 0f;
            if (face.sqrMagnitude < 0.0001f) return;
            HitFaceDir = face.normalized;
            HitFaceTimer = TickTimer.CreateFromSeconds(Runner, 0.25f);
        }

        /// <summary>지금 튕겨 나가는 중인가 (넘어진 듯한 연출용).</summary>
        public bool IsFlung => Object != null && Object.IsValid && FlingVelocity.sqrMagnitude > 1f;

        [Header("맵 밖 추락 복구")]
        [Tooltip("이 시간(초) 넘게 떨어지기만 하면 맵 밖으로 떨어진 것으로 보고 마지막으로 안전하게 서 있던 곳으로 되돌린다.")]
        [SerializeField] private float fallRecoverAirSeconds = 3.5f;
        [Tooltip("이 높이보다 아래로 내려가면 바로 되돌린다 (맵에서 가장 깊은 곳은 약 -95m).")]
        [SerializeField] private float fallRecoverMinY = -150f;

        // 호스트 전용 (호스트만 위치를 정한다)
        private Vector3 _lastSafePosition;
        private bool _hasSafePosition;
        private float _airTime;
        private int _safeCheckTick;

        private int _shownJumpCount = -1;
        private int _lastJumpTick;

        private NetworkCharacterController _controller;
        private CharacterController _characterController;
        private Character _character;
        private Animator _animator;
        private NetworkHealth _health;
        private CharacterAbilityController _ability;   // 간호사: 다친 동료에게 다가갈 때 이동 속도 증가

        [Header("간호사 - 다친 동료에게 접근")]
        [Tooltip("이 거리(m) 안의 동료만 '다가가는 중'으로 본다.")]
        [SerializeField] private float allyApproachRange = 20f;
        [Tooltip("이동 방향과 동료 방향의 내적이 이 값 이상이면 '다가가는 중' (0.5 = 약 60도 이내).")]
        [Range(-1f, 1f)]
        [SerializeField] private float allyApproachMinDot = 0.5f;

        private static readonly int IsRunHash = Animator.StringToHash("isRun");
        private static readonly int IsWalkHash = Animator.StringToHash("isWalk");
        private static readonly int IsSprintHash = Animator.StringToHash("isSprint");
        private SprintGate _sprintGate;
        private float _nextDustTime;

        #endregion

        #region 조회

        /// <summary>달리기 속도 (m/s).</summary>
        public float MoveSpeed
        {
            get
            {
                // 기존 PlayerMove와 같은 규칙: Character.CurrentMoveSpeed (= 스탯 moveSpeed × 0.3, 능력으로 바뀔 수 있음)
                if (_character != null && _character.CurrentMoveSpeed > 0f) return _character.CurrentMoveSpeed;
                if (_character != null && _character.StatsData != null && _character.StatsData.ScaledMoveSpeed > 0f)
                    return _character.StatsData.ScaledMoveSpeed;
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
            _ability = GetComponent<CharacterAbilityController>();
            _animator = GetComponentInChildren<Animator>();
            _health = GetComponent<NetworkHealth>();
        }

        public override void Spawned()
        {
            if (_animator != null) _animator.applyRootMotion = false;
            if (_controller != null)
            {
                _controller.jumpImpulse = jumpImpulse;
                _controller.gravity = jumpGravity;
                // 수평 속도는 이 스크립트(MoveVel)가 가속/감속까지 전부 정한다.
                // NetworkCharacterController 자체 가속(100)/제동(100, 키를 떼면 즉시 0)이 위에 겹치면
                // 출발/정지가 딱딱 끊기고, 서 있을 때 맞아서 밀려나는 속도도 바로 지워졌다.
                _controller.acceleration = 0f;
                _controller.braking = 0f;
                _controller.maxSpeed = 1000f;
                _controller.rotationSpeed = turnSpeed;
            }
            _shownJumpCount = JumpCount; // 늦게 보이기 시작한 사람이 지난 점프를 다시 재생하지 않게
            TruckStabilizer.IgnoreCharacter(gameObject);   // 트럭이 캐릭터를 밟거나 겹쳐서 튕겨 날아가지 않게
        }

        #endregion

        #region 이동 (네트워크 틱)

        /// <summary>
        /// 간호사: 체력이 낮은(살아 있는) 동료 쪽으로 움직이면 이동 속도가 빨라진다.
        /// 호스트와 내 컴퓨터(예측)가 같은 동기화 값(위치/체력)으로 계산하므로 결과가 같다.
        /// </summary>
        private float AllyApproachSpeedMultiplier(Vector3 moveDirection)
        {
            if (_ability == null || moveDirection.sqrMagnitude < 0.01f) return 1f;

            Vector3 moveDir = moveDirection.normalized;
            float best = 1f;
            IReadOnlyList<NetworkPlayer> players = NetworkPlayer.All;
            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer other = players[i];
                if (other == null || other.gameObject == gameObject || !other.IsAlive || other.IsSeated) continue;

                NetworkHealth health = other.Health;
                if (health == null || health.Max <= 0f) continue;

                Vector3 toAlly = other.transform.position - transform.position;
                toAlly.y = 0f;
                float distance = toAlly.magnitude;
                if (distance < 0.5f || distance > allyApproachRange) continue;

                bool approaching = Vector3.Dot(moveDir, toAlly / distance) >= allyApproachMinDot;
                float multiplier = _ability.GetAllyApproachSpeedMultiplier(health.Current / health.Max, approaching);
                if (multiplier > best) best = multiplier;
            }
            return best;
        }

        public override void FixedUpdateNetwork()
        {
            // 입력은 호스트(모든 캐릭터)와 자기 캐릭터를 예측하는 본인 컴퓨터에서만 받을 수 있다.
            bool hasInput = GetInput(out NetworkInputData input);
            if (!hasInput && !Object.HasStateAuthority) return;

            // 갑옷 등급: 호스트가 입력에서 읽어 두고 피해 계산(NetworkHealth.ApplyDamage)에 쓴다 (값 범위는 호스트가 다시 제한)
            if (hasInput && Object.HasStateAuthority)
                ArmorTier = Mathf.Clamp(input.ArmorTier, 0, CraftingSystem.PlayerArmor.MaxTier);

            // 트럭에 타 있으면 걷지 않고 좌석에 붙어 있는다 (충돌 끔 → 트럭을 밀지 않음).
            if (NetworkTruck.IsSeated(Object.InputAuthority))
            {
                SetSeatedCollision(true);
                StaySeated();
                _airTime = 0f;
                return;
            }
            SetSeatedCollision(false);

            if (!hasInput || !CanControl)
            {
                // 입력이 없거나(참가자 로딩 중 등) 조작할 수 없는 상태: 중력만 적용하고 멈춰 세운다.
                StandStill();
                UpdateFallRecovery();
                return;
            }

            Vector3 direction = input.MoveDirection;
            bool walking = input.Buttons.IsSet(NetworkInputButton.Walk);
            direction.y = 0f;
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            IsWalking = walking;

            // 활 차징/화염 분사 중 감속 등 PlayerMove 속도 배율은 입력에 실려 온다 (0이면 1로 본다).
            float speedMultiplier = input.SpeedMultiplier > 0f ? Mathf.Min(input.SpeedMultiplier, 2f) : 1f;
            float speed = MoveSpeed * (walking ? sprintSpeedMultiplier : 1f) * speedMultiplier;
            speed *= AllyApproachSpeedMultiplier(direction);   // 간호사 능력 (다른 캐릭터는 항상 1)

            // 수평 속도: 목표 속도까지 짧게(약 0.1초) 가속/감속한다. 예전에는 키를 누르는 즉시 최고 속도, 떼는 즉시 정지라
            // 로봇처럼 딱딱했다. 너무 길면 굼떠지므로 반응성은 거의 그대로 두고 첫 발/멈춤/꺾기에만 무게감을 준다.
            // 반대쪽으로 꺾을 때는 빨리 줄여서 미끄러지는 느낌이 나지 않게 한다.
            // (NetworkCharacterController가 다음 틱 속도를 실제 이동 거리로 다시 계산하므로, 우리 속도는 MoveVel에 따로 들고 간다)
            Vector3 targetVel = direction * speed;
            Vector3 moveVel = MoveVel;
            bool speedingUp = targetVel.sqrMagnitude > moveVel.sqrMagnitude + 0.01f;
            bool reversing = moveVel.sqrMagnitude > 1f && targetVel.sqrMagnitude > 0.01f && Vector3.Dot(moveVel, targetVel) < 0f;
            float rate = (reversing ? moveTurnDeceleration : speedingUp ? moveAcceleration : moveDeceleration) * Runner.DeltaTime;
            moveVel = Vector3.MoveTowards(moveVel, targetVel, rate);
            MoveVel = moveVel;
            Vector3 velocity = _controller.Velocity;
            velocity.x = moveVel.x;
            velocity.z = moveVel.z;

            // 트럭에서 튕겨 나가는 중이면 그 속도를 더하고, 빠를수록 방향키가 덜 듣는다. 땅에 닿으면 빠르게 줄어든다.
            Vector3 fling = FlingVelocity;
            if (fling.sqrMagnitude > 0.0001f)
            {
                float control = Mathf.Clamp01(1f - fling.magnitude / Mathf.Max(0.1f, flingNoControlSpeed));
                velocity.x = velocity.x * control + fling.x;
                velocity.z = velocity.z * control + fling.z;
                float decel = _controller.Grounded ? flingGroundDecel : flingAirDecel;
                FlingVelocity = Vector3.MoveTowards(fling, Vector3.zero, decel * Runner.DeltaTime);
            }
            if (FlingUp > 0f)
            {
                velocity.y = Mathf.Max(velocity.y, Mathf.Min(FlingUp, jumpImpulse));
                FlingUp = 0f;
                _lastJumpTick = Runner.Tick;   // 아래에서 땅에 붙이는 처리가 위로 튀는 속도를 지우지 않게
            }

            // NetworkCharacterController는 다음 틱의 속도를 "실제로 움직인 거리"로 다시 계산한다.
            // 경사면을 오르면 움직인 거리에 위쪽 성분이 생기고, 그게 다음 틱 속도로 더해져서 점점 쌓이며 하늘로 튀어 올랐다.
            // 땅에 붙어 있는 동안은 위로 가는 속도를 0으로 눌러 두고(점프 임펄스는 이 다음에 더해진다),
            // 공중에서도 점프 속도보다 빠르게 올라가지 못하게 한다.
            // (isGrounded는 경사면에서 가끔 false가 되므로, 발 바로 밑에 바닥이 있는지도 같이 본다. 방금 점프했을 때는 건드리지 않는다.)
            bool justJumped = _lastJumpTick != 0 && Runner.Tick - _lastJumpTick < 12;
            bool onGround = _controller.Grounded ||
                            Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 0.45f, ~0, QueryTriggerInteraction.Ignore);
            velocity.y = Mathf.Min(velocity.y, (onGround && !justJumped) ? 0f : jumpImpulse);
            _controller.Velocity = velocity;

            // 점프: 이번 틱에 새로 눌렸고 땅에 있으면
            bool jumpPressed = input.Buttons.WasPressed(PreviousButtons, NetworkInputButton.Jump);
            PreviousButtons = input.Buttons;
            if (jumpPressed && _controller.Grounded)
            {
                _controller.Jump();
                _lastJumpTick = Runner.Tick;
                JumpCount++;
            }

            _controller.Move(direction);
            UpdateFallRecovery();

            // 맞은 직후: 때린 쪽으로 빠르게 몸을 돌린다 (피격 동작이 "앞에서 맞는" 모양이라 방향을 맞춘다)
            if (HitFaceTimer.IsRunning)
            {
                if (HitFaceTimer.Expired(Runner)) HitFaceTimer = TickTimer.None;
                else if (HitFaceDir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(HitFaceDir), 0.35f);
            }

            // 무기 조준 등으로 "이쪽을 봐라"는 요청이 있으면 이동 방향 회전보다 우선한다.
            Vector3 face = input.FaceDirection;
            face.y = 0f;
            if (face.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(face.normalized);
            }
        }

        /// <summary>
        /// 호스트 전용: 맵 밖으로 떨어지면 마지막으로 안전하게 서 있던 곳으로 되돌린다.
        /// 땅에 서 있을 때 0.3초마다 발 주변 네 방향 모두 바닥이 있으면(가장자리가 아니면) 그 위치를 기억해 둔다.
        /// 너무 오래 떨어지기만 하거나 아주 깊이 내려가면 그 위치로 순간이동시킨다.
        /// </summary>
        private void UpdateFallRecovery()
        {
            if (!Object.HasStateAuthority || _controller == null) return;

            if (_controller.Grounded)
            {
                _airTime = 0f;
                if (Runner.Tick - _safeCheckTick >= 10)
                {
                    _safeCheckTick = Runner.Tick;
                    if (IsSolidGroundAround(transform.position))
                    {
                        _lastSafePosition = transform.position;
                        _hasSafePosition = true;
                    }
                }
                return;
            }

            _airTime += Runner.DeltaTime;
            bool fellTooLong = _airTime > fallRecoverAirSeconds && _controller.Velocity.y < 0f;
            bool fellTooDeep = transform.position.y < fallRecoverMinY;
            if (!_hasSafePosition || (!fellTooLong && !fellTooDeep)) return;

            _controller.Teleport(_lastSafePosition + Vector3.up * 0.3f, transform.rotation);
            _controller.Velocity = Vector3.zero;
            FlingVelocity = Vector3.zero;
            FlingUp = 0f;
            _airTime = 0f;
        }

        private static bool IsSolidGroundAround(Vector3 position)
        {
            const float offset = 1.5f;
            Vector3 top = position + Vector3.up;
            return Physics.Raycast(top + Vector3.forward * offset, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore)
                && Physics.Raycast(top + Vector3.back * offset, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore)
                && Physics.Raycast(top + Vector3.left * offset, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore)
                && Physics.Raycast(top + Vector3.right * offset, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
        }

        private void StaySeated()
        {
            _controller.Velocity = Vector3.zero;
            MoveVel = Vector3.zero;
            IsWalking = false;

            // 좌석 위치는 호스트가 정한다 (클라이언트의 내 캐릭터는 호스트 값으로 맞춰진다).
            // 포탑 사수는 포탑 위에 서서 포탑 방향을 따라 돈다 (다른 사람에게도 보인다).
            if (Object.HasStateAuthority && NetworkTruck.TryGetSeatPose(Object.InputAuthority, out Vector3 position, out Quaternion rotation))
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
            MoveVel = Vector3.zero;
            _controller.Velocity = idle;
            _controller.Move(Vector3.zero);
            IsWalking = false;
        }

        #endregion

        #region 애니메이션

        private bool _wasGroundedForCamera = true;
        private float _airFallSpeedForCamera;

        /// <summary>내 캐릭터가 공중에서 땅에 닿는 순간 카메라 착지 연출(CameraEffects)에 알린다 (내 화면에서만).</summary>
        private void TrackLandingForCamera()
        {
            if (_controller == null || Object == null || !Object.IsValid || !Object.HasInputAuthority) return;

            bool grounded = _controller.Grounded;
            if (grounded)
            {
                if (!_wasGroundedForCamera && _airFallSpeedForCamera > 3.5f)
                {
                    LastTruck.CameraEffects.NotifyLanded(_airFallSpeedForCamera);
                    LastTruck.MovementDust.Landing(transform.position, _airFallSpeedForCamera);
                }
                _airFallSpeedForCamera = 0f;
            }
            else
            {
                _airFallSpeedForCamera = Mathf.Max(_airFallSpeedForCamera, -_controller.Velocity.y);
            }
            _wasGroundedForCamera = grounded;
        }

        // ---- 몸 기울기 (보이기만 하는 연출, 모든 화면) ----
        private Transform _leanBody;
        private Quaternion _leanBase;
        private bool _leanInit;
        private Vector3 _leanPrevVel;
        private float _leanPrevYaw;
        private Vector2 _lean;   // x = 앞뒤(가속), y = 좌우(회전)

        /// <summary>
        /// 달리기 시작하면 앞으로, 멈추면 뒤로, 방향을 틀면 안쪽으로 몸이 살짝 기운다 (최대 약 8~10도).
        /// 모델(애니메이터가 붙은 자식)만 기울이고 캐릭터 루트/충돌은 건드리지 않는다.
        /// </summary>
        private void UpdateBodyLean()
        {
            if (!_leanInit)
            {
                _leanInit = true;
                if (_animator != null && _animator.transform != transform)
                {
                    _leanBody = _animator.transform;
                    _leanBase = _leanBody.localRotation;
                }
                _leanPrevYaw = transform.eulerAngles.y;
            }
            if (_leanBody == null || _controller == null) return;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            bool seated = Object != null && Object.IsValid && NetworkTruck.IsSeated(Object.InputAuthority);
            Vector3 vel = _controller.Velocity; vel.y = 0f;
            if (seated) vel = Vector3.zero;

            Vector3 accel = (vel - _leanPrevVel) / dt;
            _leanPrevVel = vel;
            float forwardAccel = Vector3.Dot(accel, transform.forward);
            float yaw = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(_leanPrevYaw, yaw) / dt;
            _leanPrevYaw = yaw;

            float speed01 = Mathf.Clamp01(vel.magnitude / 8f);
            Vector2 target = new Vector2(
                Mathf.Clamp(forwardAccel * 0.12f, -7f, 7f) + speed01 * 3f,       // 달리는 동안 살짝 앞으로 숙인 자세
                Mathf.Clamp(-yawRate * 0.02f * speed01, -10f, 10f));
            _lean = Vector2.Lerp(_lean, target, 1f - Mathf.Exp(-10f * dt));
            _leanBody.localRotation = _leanBase * Quaternion.Euler(_lean.x, 0f, _lean.y);
        }

        public override void Render()
        {
            TrackLandingForCamera();
            UpdateBodyLean();

            if (_animator == null) return;

            if (_shownJumpCount != JumpCount)
            {
                bool first = _shownJumpCount < 0;
                _shownJumpCount = JumpCount;
                if (!first) PlayJumpAnimation();
            }

            // 착지하면 점프 모션을 바로 끝낸다 (점프 클립이 실제 체공 시간보다 길어서 땅에 닿은 뒤에도 남아 있었다)
            if (_jumpAnimStart >= 0f && Time.time - _jumpAnimStart > 0.2f)
            {
                bool onGround = _controller != null && _controller.Grounded ||
                                Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 0.45f, ~0, QueryTriggerInteraction.Ignore);
                if (onGround || Time.time - _jumpAnimStart > 3f)
                {
                    _jumpAnimStart = -1f;
                    if (HasAnimatorParam(JumpLandHash)) _animator.SetTrigger(JumpLandHash);
                }
            }

            bool alive = (_health == null || !_health.IsDead) && !NetworkTruck.IsSeated(Object.InputAuthority);
            bool moving = alive && HorizontalSpeed > runAnimationThreshold;
            _animator.SetBool(IsRunHash, moving);
            _animator.SetBool(IsWalkHash, false);   // Shift는 이제 느린 걷기가 아니라 달리기라 걷기 애니메이션은 쓰지 않는다

            // 달리기(Shift): IsWalking은 이제 "달리는 중"이다. 더 빠른 달리기 모션이 있으면 그것을, 없으면 같은 모션을 빠르게 재생한다.
            // Shift 연타에도 모션이 뒤집히지 않게 상태를 잠깐 유지하고, 재생 속도도 부드럽게 바꾼다.
            bool sprinting = _sprintGate.Update(moving && IsWalking) && moving;
            _animator.SetBool(IsSprintHash, sprinting);
            if (_hasRunAnimMul < 0) _hasRunAnimMul = HasAnimatorParam(RunAnimMulHash) ? 1 : 0;
            if (_hasRunAnimMul == 1)
            {
                // 달리기 상태에만 재생 배율을 건다 (발이 실제 이동 속도로 땅을 밀게 → 미끄러지지 않게).
                // 애니메이터 전체 속도는 1로 둬서 공격/피격 동작은 원래 빠르기로.
                float ground = (sprinting ? sprintClipGroundSpeed : runClipGroundSpeed) * Mathf.Max(0.1f, _animator.transform.lossyScale.y);
                float target = moving ? Mathf.Clamp(HorizontalSpeed / Mathf.Max(0.1f, ground), runAnimMulRange.x, runAnimMulRange.y) : 1f;
                _runAnimMul = Mathf.MoveTowards(_runAnimMul, target, 6f * Time.deltaTime);
                _animator.SetFloat(RunAnimMulHash, _runAnimMul);
                _animator.speed = 1f;
            }
            else
            {
                _animator.speed = _sprintGate.StepAnimSpeed(sprinting, Time.deltaTime);
            }

            // 달리는 동안 발밑에 먼지가 인다 (다른 사람 캐릭터도 같이 보인다)
            if (sprinting && _controller != null && _controller.Grounded && Time.time >= _nextDustTime)
            {
                _nextDustTime = Time.time + 0.18f;
                MovementDust.SprintPuff(transform.position, transform.forward);
            }
        }

        private static readonly int JumpLandHash = Animator.StringToHash("jumpLand");
        private static readonly int RunAnimMulHash = Animator.StringToHash("RunAnimMul");
        private int _hasRunAnimMul = -1;
        private float _runAnimMul = 1f;
        private float _jumpAnimStart = -1f;

        private bool HasAnimatorParam(int hash)
        {
            foreach (AnimatorControllerParameter p in _animator.parameters)
                if (p.nameHash == hash) return true;
            return false;
        }

        private void PlayJumpAnimation()
        {
            if (string.IsNullOrEmpty(jumpStateName)) return;
            int hash = Animator.StringToHash(jumpStateName);
            if (!_animator.HasState(0, hash)) return;
            if (HasAnimatorParam(JumpLandHash)) _animator.ResetTrigger(JumpLandHash);
            _animator.CrossFadeInFixedTime(hash, 0.05f, 0);
            _jumpAnimStart = Time.time;
        }

        #endregion
    }
}
