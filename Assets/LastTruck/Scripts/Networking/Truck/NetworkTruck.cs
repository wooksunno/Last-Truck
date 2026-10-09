using Fusion;
using Unity.Cinemachine;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 멀티플레이 트럭 (NetworkGameState 프리팹에 같이 붙는다. 트럭 자체는 씬에 있는 기존 트럭을 그대로 쓴다).
    ///
    /// 좌석
    ///  - 0번 = 운전석(항상 1명), 1~3번 = 동승석. 먼저 타는 사람이 운전석, 운전석이 차 있으면 동승석.
    ///  - 탄 사람의 캐릭터는 숨기고(모델/충돌 끔) 트럭 좌석 위치에 붙여 둔다. 카메라는 트럭을 따라간다.
    ///  - E: 내리기 / L: 라이트(운전자) / F: 운전석이 비었을 때 동승자가 운전석으로 이동.
    ///  - 죽거나 나간 사람의 좌석은 자동으로 비운다.
    ///
    /// 운전
    ///  - 트럭 물리(WheelCollider)는 호스트에서만 돈다. 운전자의 조작(앞뒤/좌우/브레이크)은 네트워크 입력으로 호스트에 가고,
    ///    호스트가 TruckMove에 넣어 움직인다.
    ///  - 호스트는 매 틱 트럭 위치/회전을 보내고, 클라이언트는 트럭을 물리 없이(kinematic) 그 값으로 부드럽게 옮긴다.
    ///  - 라이트 상태도 모두에게 동기화된다. 내구도는 NetworkGameState가 동기화한다 (0이면 운전 불가).
    /// </summary>
    public class NetworkTruck : NetworkBehaviour
    {
        #region 인스펙터

        [Tooltip("이 거리(트럭 중심 기준, m) 안에 있어야 탈 수 있다 (호스트가 확인).")]
        [SerializeField] private float boardDistance = 8f;

        [Tooltip("타고 나서/바꾸고 나서 이 시간(초) 동안은 내리기 키를 무시한다 (탈 때 누른 E가 바로 내리기로 처리되지 않게).")]
        [SerializeField] private float exitCooldown = 0.4f;

        [Tooltip("여러 명이 내릴 때 겹치지 않게 벌리는 간격(m).")]
        [SerializeField] private float exitSpacing = 1.2f;

        [Tooltip("클라이언트 화면의 트럭이 이 거리(m) 이상 어긋나면 부드럽게 옮기지 않고 바로 맞춘다.")]
        [SerializeField] private float snapDistance = 8f;

        [Tooltip("클라이언트 화면에서 트럭을 따라가는 부드러움 (클수록 빠르게 따라감, 작을수록 부드럽지만 늦음).")]
        [SerializeField] private float clientSmoothing = 18f;

        #endregion

        #region 네트워크 상태

        public const int SeatCount = 4;
        public const int DriverSeat = 0;

        [Networked, Capacity(SeatCount)] private NetworkArray<PlayerRef> Seats => default;
        [Networked] public Vector3 TruckPosition { get; set; }
        [Networked] public Quaternion TruckRotation { get; set; }
        [Networked] public NetworkBool HasPose { get; set; }
        [Networked, OnChangedRender(nameof(OnLightsRender))] public NetworkBool LightsOn { get; set; }

        #endregion

        #region 정적 조회

        public static NetworkTruck Instance { get; private set; }

        public static bool IsReady => Instance != null && Instance.Object != null && Instance.Object.IsValid;

        /// <summary>이 플레이어가 앉은 좌석 번호 (-1 = 안 탐).</summary>
        public static int SeatOfPlayer(PlayerRef player) => IsReady ? Instance.SeatOf(player) : -1;

        /// <summary>트럭에 타 있는가.</summary>
        public static bool IsSeated(PlayerRef player) => SeatOfPlayer(player) >= 0;

        /// <summary>좌석 위치 (탄 캐릭터를 붙여 둘 곳).</summary>
        public static bool TryGetSeatPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (!IsReady || !Instance.FindTruck()) return false;

            Transform seat = Instance._truck.seatPoint != null ? Instance._truck.seatPoint : Instance._truck.transform;
            position = seat.position;
            rotation = Quaternion.Euler(0f, seat.eulerAngles.y, 0f);
            return true;
        }

        /// <summary>트럭 Transform (없으면 null).</summary>
        public static Transform TruckTransform => IsReady && Instance.FindTruck() ? Instance._truck.transform : null;

        public int SeatOf(PlayerRef player)
        {
            if (player == PlayerRef.None) return -1;
            for (int i = 0; i < SeatCount; i++)
            {
                if (Seats[i] == player) return i;
            }
            return -1;
        }

        #endregion

        #region 씬 트럭 참조

        private TruckInteractable _truck;
        private TruckMove _move;
        private Rigidbody _rigidbody;
        private TruckLight _light;
        private TruckInformation _info;
        private Collider[] _truckColliders = new Collider[0];
        private bool _clientPhysicsDisabled;

        private bool FindTruck()
        {
            if (_truck != null) return true;

            _truck = FindFirstObjectByType<TruckInteractable>();
            if (_truck == null) return false;

            _move = _truck.truckController != null ? _truck.truckController : _truck.GetComponent<TruckMove>();
            _rigidbody = _truck.GetComponent<Rigidbody>();
            _light = _truck.lightController != null ? _truck.lightController : _truck.GetComponent<TruckLight>();
            _info = _truck.GetComponent<TruckInformation>();
            _truckColliders = _truck.GetComponentsInChildren<Collider>(true);
            return true;
        }

        #endregion

        #region 생명주기

        private int _localSeat = -1;
        private float _localSeatChangedTime;
        private Transform _savedCameraTarget;

        public override void Spawned()
        {
            Instance = this;
            FindTruck();

            if (HasStateAuthority)
            {
                for (int i = 0; i < SeatCount; i++) Seats.Set(i, PlayerRef.None);
                LightsOn = false;

                // 호스트: 물리 스텝 사이도 매 프레임 부드러운 위치를 쓰도록 보간을 켠다 (보내는 위치가 덜 튄다).
                if (_rigidbody != null) _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            }

            OnLightsRender();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_localSeat >= 0) RestoreCamera();
            _localSeat = -1;
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        #region 호스트: 좌석 관리 + 운전

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || !FindTruck()) return;

            ValidateSeats();
            ApplyDriverInput();

            TruckPosition = _truck.transform.position;
            TruckRotation = _truck.transform.rotation;
            HasPose = true;
        }

        /// <summary>죽었거나 나간 사람의 좌석을 비운다.</summary>
        private void ValidateSeats()
        {
            for (int i = 0; i < SeatCount; i++)
            {
                PlayerRef player = Seats[i];
                if (player == PlayerRef.None) continue;

                NetworkPlayer character = NetworkPlayer.Find(player);
                if (character == null || !character.IsAlive)
                {
                    Seats.Set(i, PlayerRef.None);
                    if (i == DriverSeat) LightsOn = false;
                    // 탄 채로 죽은 캐릭터는 트럭 밖으로 내려놓는다 (트럭 안에서 충돌이 다시 켜지면 트럭을 밀어낸다).
                    if (character != null) MoveOut(character, i);
                }
            }
        }

        private void ApplyDriverInput()
        {
            if (_move == null) return;

            PlayerRef driver = Seats[DriverSeat];
            bool broken = _info != null && _info.IsBroken;
            bool canDrive = driver != PlayerRef.None && !broken && NetworkGameState.AllowPlayerControl;

            if (!canDrive)
            {
                if (_move.isDriving) _move.isDriving = false; // 기존 하차 처리와 같이 바로 멈춘다
                if (_move.enabled) _move.enabled = false;
                return;
            }

            _move.useExternalInput = true;
            if (!_move.enabled) _move.enabled = true;
            if (!_move.isDriving) _move.isDriving = true;

            float throttle = 0f;
            float steer = 0f;
            bool brake = false;
            if (Runner.TryGetInputForPlayer(driver, out NetworkInputData input))
            {
                throttle = Mathf.Clamp(input.TruckThrottle, -1f, 1f);
                steer = Mathf.Clamp(input.TruckSteer, -1f, 1f);
                brake = input.Buttons.IsSet(NetworkInputButton.TruckBrake);
            }
            _move.SetExternalInput(throttle, steer, brake);
        }

        private void HostBoard(PlayerRef player)
        {
            if (!FindTruck() || SeatOf(player) >= 0) return;

            NetworkPlayer character = NetworkPlayer.Find(player);
            if (character == null || !character.IsAlive) return;

            if (HorizontalDistance(character.transform.position, _truck.transform.position) > boardDistance) return;

            int seat = -1;
            if (Seats[DriverSeat] == PlayerRef.None)
            {
                seat = DriverSeat;
            }
            else
            {
                for (int i = 1; i < SeatCount; i++)
                {
                    if (Seats[i] == PlayerRef.None) { seat = i; break; }
                }
            }

            if (seat < 0)
            {
                RPC_Notify(player, "트럭 좌석이 모두 찼습니다.");
                return;
            }

            Seats.Set(seat, player);
        }

        private void HostExit(PlayerRef player)
        {
            int seat = SeatOf(player);
            if (seat < 0 || !FindTruck()) return;

            Seats.Set(seat, PlayerRef.None);
            if (seat == DriverSeat) LightsOn = false; // 기존 TruckLight.autoTurnOffOnExit와 같은 동작

            NetworkPlayer character = NetworkPlayer.Find(player);
            if (character != null) MoveOut(character, seat);
        }

        private void MoveOut(NetworkPlayer character, int seat)
        {
            Vector3 exit = GetExitPosition(seat);
            NetworkCharacterController controller = character.GetComponent<NetworkCharacterController>();
            if (controller != null)
            {
                controller.Velocity = Vector3.zero;
                controller.Teleport(exit, Quaternion.Euler(0f, _truck.transform.eulerAngles.y, 0f));
            }
            else
            {
                character.transform.position = exit;
            }
        }

        /// <summary>내릴 위치: 트럭의 Exit Point에서 좌석 번호만큼 뒤로 벌리고, 바닥 높이에 맞춘다.</summary>
        private Vector3 GetExitPosition(int seat)
        {
            Transform truck = _truck.transform;
            Vector3 basePosition = _truck.exitPoint != null ? _truck.exitPoint.position : truck.position + truck.right * 2.5f;
            Vector3 position = basePosition - truck.forward * (seat * exitSpacing);

            RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 5f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(truck)) continue;
                if (hit.collider.GetComponentInParent<NetworkPlayer>() != null) continue;
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    position.y = hit.point.y + 0.05f;
                }
            }
            return position;
        }

        private void HostTakeDriverSeat(PlayerRef player)
        {
            int seat = SeatOf(player);
            if (seat <= DriverSeat || Seats[DriverSeat] != PlayerRef.None) return;
            Seats.Set(seat, PlayerRef.None);
            Seats.Set(DriverSeat, player);
        }

        #endregion

        #region 요청 RPC (클라이언트 → 호스트)

        /// <summary>트럭 상호작용(E)에서 부른다 (TruckInteractable).</summary>
        public static void RequestBoard()
        {
            if (!IsReady)
            {
                SystemUI.Alert("트럭", "트럭 동기화 오브젝트가 없습니다.\n메뉴 'LastTruck > Multiplayer > 1. 네트워크 프리팹 생성'을 다시 실행하세요.");
                return;
            }

            NetworkPlayer local = NetworkPlayer.Local;
            if (local == null || !local.IsAlive || Instance.SeatOf(local.Owner) >= 0) return;
            Instance.RPC_RequestBoard();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestBoard(RpcInfo info = default) => HostBoard(SourceOf(info));

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestExit(RpcInfo info = default) => HostExit(SourceOf(info));

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestDriverSeat(RpcInfo info = default) => HostTakeDriverSeat(SourceOf(info));

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ToggleLights(RpcInfo info = default)
        {
            if (Seats[DriverSeat] == SourceOf(info)) LightsOn = !LightsOn;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Notify([RpcTarget] PlayerRef target, string message)
        {
            SystemUI.Alert("트럭", message);
        }

        /// <summary>RPC 보낸 사람. 호스트가 자기 자신에게 보낸 경우 Source가 비어 있을 수 있어 LocalPlayer로 본다.</summary>
        private PlayerRef SourceOf(RpcInfo info) => info.Source != PlayerRef.None ? info.Source : Runner.LocalPlayer;

        #endregion

        #region 모든 컴퓨터: 트럭 위치 / 라이트 / 내 좌석

        public override void Render()
        {
            if (!FindTruck()) return;

            // 클라이언트: 트럭 물리를 끄고 호스트 위치를 따라간다 (스냅샷 보간으로 부드럽게).
            if (!HasStateAuthority && HasPose)
            {
                DisableClientPhysics();

                Vector3 position = TruckPosition;
                Quaternion rotation = TruckRotation;
                var interpolator = new NetworkBehaviourBufferInterpolator(this);
                if (interpolator.Valid)
                {
                    position = interpolator.Vector3(nameof(TruckPosition));
                    rotation = interpolator.Quaternion(nameof(TruckRotation));
                }

                Transform truck = _truck.transform;
                Vector3 previous = truck.position;
                if ((truck.position - position).sqrMagnitude > snapDistance * snapDistance)
                {
                    truck.SetPositionAndRotation(TruckPosition, TruckRotation);
                }
                else
                {
                    // 호스트 틱과 물리 스텝이 어긋나서 생기는 미세한 끊김을 한 번 더 걸러낸다.
                    float t = 1f - Mathf.Exp(-clientSmoothing * Time.deltaTime);
                    truck.SetPositionAndRotation(Vector3.Lerp(truck.position, position, t), Quaternion.Slerp(truck.rotation, rotation, t));
                }
                SpinClientWheels(truck, truck.position - previous);
            }
        }

        /// <summary>클라이언트: 물리가 없어서 바퀴가 멈춰 보이므로, 움직인 거리만큼 바퀴 모델을 굴린다 (차축 = 트럭 오른쪽 방향).</summary>
        private void SpinClientWheels(Transform truck, Vector3 moved)
        {
            if (_move == null || _move.wheels == null) return;
            float forward = Vector3.Dot(moved, truck.forward);
            if (Mathf.Abs(forward) < 0.0001f) return;

            foreach (TruckMove.Wheel wheel in _move.wheels)
            {
                if (wheel.wheelModel == null) continue;
                float radius = wheel.wheelCollider != null ? Mathf.Max(0.1f, wheel.wheelCollider.radius) : 0.4f;
                float degrees = forward / radius * Mathf.Rad2Deg;
                wheel.wheelModel.transform.Rotate(truck.right, degrees, Space.World);
            }
        }

        private void DisableClientPhysics()
        {
            if (_clientPhysicsDisabled) return;
            _clientPhysicsDisabled = true;

            if (_move != null)
            {
                _move.useExternalInput = true;
                _move.enabled = false;
            }
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.interpolation = RigidbodyInterpolation.None;
            }
        }

        private void OnLightsRender()
        {
            if (!FindTruck() || _light == null) return;
            _light.SetLightState(LightsOn);
        }

        private void Update()
        {
            if (!IsReady || Instance != this) return;

            NetworkPlayer local = NetworkPlayer.Local;
            int seat = local != null ? SeatOf(local.Owner) : -1;
            if (seat != _localSeat)
            {
                int previous = _localSeat;
                _localSeat = seat;
                _localSeatChangedTime = Time.time;
                OnLocalSeatChanged(previous, seat);
            }

            if (seat < 0 || Time.time - _localSeatChangedTime < exitCooldown) return;
            if (SystemUI.Instance != null && SystemUI.Instance.IsPopupVisible) return;

            if (Input.GetKeyDown(KeyCode.E))
            {
                RPC_RequestExit();
            }
            else if (seat == DriverSeat && Input.GetKeyDown(KeyCode.L))
            {
                RPC_ToggleLights();
            }
            else if (seat > DriverSeat && Input.GetKeyDown(KeyCode.F) && Seats[DriverSeat] == PlayerRef.None)
            {
                RPC_RequestDriverSeat();
            }
        }

        /// <summary>내 좌석이 바뀌었다: 카메라를 트럭 ↔ 내 캐릭터로.</summary>
        private void OnLocalSeatChanged(int previous, int seat)
        {
            if (!FindTruck()) return;

            if (previous < 0 && seat >= 0) PointCamera(_truck.transform);
            else if (previous >= 0 && seat < 0) RestoreCamera();
        }

        private void PointCamera(Transform target)
        {
            foreach (CameraFollow follow in FindObjectsByType<CameraFollow>(FindObjectsSortMode.None))
            {
                if (_savedCameraTarget == null) _savedCameraTarget = follow.target;
                follow.target = target;
            }
            foreach (CinemachineCamera vcam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
            {
                vcam.Target.TrackingTarget = target;
            }
        }

        private void RestoreCamera()
        {
            NetworkPlayer local = NetworkPlayer.Local;
            Transform target = local != null ? local.transform : _savedCameraTarget;
            _savedCameraTarget = null;
            if (target == null) return;

            foreach (CameraFollow follow in FindObjectsByType<CameraFollow>(FindObjectsSortMode.None)) follow.target = target;
            foreach (CinemachineCamera vcam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None)) vcam.Target.TrackingTarget = target;
        }

        /// <summary>탄 동안 조작 안내.</summary>
        private void OnGUI()
        {
            if (_localSeat < 0 || !IsReady) return;

            string text = _localSeat == DriverSeat
                ? "운전 중   W/S: 전진·후진   A/D: 핸들   Space: 브레이크   L: 라이트   E: 내리기"
                : (Seats[DriverSeat] == PlayerRef.None ? "동승 중   F: 운전석으로 이동   E: 내리기" : "동승 중   E: 내리기");

            var style = new GUIStyle(GUI.skin.box) { fontSize = Mathf.Max(14, Screen.height / 45), alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            Vector2 size = style.CalcSize(new GUIContent(text)) + new Vector2(24f, 12f);
            GUI.Box(new Rect((Screen.width - size.x) * 0.5f, Screen.height - size.y - 24f, size.x, size.y), text, style);
        }

        #endregion

        #region 도우미

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        #endregion
    }
}
