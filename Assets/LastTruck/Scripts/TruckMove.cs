using UnityEngine;
using System;
using System.Collections.Generic;

namespace LastTruck
{
    public class TruckMove : MonoBehaviour
    {
        public enum Accelerator
        {
            Front,
            Rear,
            All
        }

        [Serializable]
        public struct Wheel
        {
            public GameObject wheelModel;
            public WheelCollider wheelCollider;
            public Accelerator accelerator;
        }

        [Header("가속")]
        public float maxAcceleration = 2000.0f;
        public float brakeAcceleration = 6000.0f;
        public float decelerationForce = 150.0f; // 키 입력x => 감속
        public Accelerator driveType = Accelerator.Rear;

        [Header("방향")]
        public float turnSensitivity = 1.0f;
        public float maxSteerAngle = 30.0f;

        [Header("물리")]
        public Vector3 _centerOfMass = new Vector3(0, -0.8f, 0);
        public float downForce = 150.0f;
        public Rigidbody carRb;

        [Header("안정성 (무게감 / 안 뜨게)")]
        [Tooltip("트럭 최소 질량(kg). 가벼우면 작은 턱에도 튕겨 난다")]
        public float minMass = 6000f;
        [Tooltip("서스펜션 스프링/댐퍼를 질량에 맞춰 자동으로 설정할지")]
        public bool autoSuspension = true;
        [Tooltip("서스펜션 이동 거리(m)")]
        public float suspensionTravel = 0.4f;
        [Tooltip("최고 속도(m/s). 넘으면 가속을 멈춘다")]
        public float maxSpeed = 18f;
        [Tooltip("속도에 비례해 땅으로 누르는 힘 (클수록 안 뜬다)")]
        public float speedDownForce = 700f;
        [Tooltip("공중에 떠 있을 때 추가로 끌어내리는 중력 배율")]
        public float airExtraGravity = 1.5f;
        [Tooltip("위로 튀어 오르는 속도 상한(m/s)")]
        public float maxUpwardSpeed = 6f;
        [Tooltip("좌우 바퀴 높이 차이를 줄여 쓰러지지 않게 잡는 힘")]
        public float antiRoll = 18000f;
        [Tooltip("너무 기울었을 때 바로 세우는 힘")]
        public float uprightTorque = 9000f;
        [Tooltip("회전 속도 상한(rad/s)")]
        public float maxAngularSpeed = 2.5f;

        // 트럭이 지나갈 수 있는 맵 오브젝트(철도/나무/작은 바위)는 TruckPass 레이어(9)에 있다 → 충돌 무시
        public const int PassLayer = 9;
        public const int WheelLayer = 10;

        public List<Wheel> wheels;

        [SerializeField]
        private bool _isDriving = false;

        public bool isDriving
        {
            get => _isDriving;
            set
            {
                _isDriving = value;
                if (!_isDriving)
                {
                    Stop_Immediately();
                }
            }
        }

        private float moveInput;
        private float steerInput;
        private bool isBrakingInput;

        /// <summary>
        /// 멀티플레이: true면 키보드를 직접 읽지 않고 SetExternalInput으로 받은 값으로 운전한다
        /// (호스트가 운전자의 네트워크 입력을 넣어 준다 - LastTruck.Networking.NetworkTruck).
        /// </summary>
        [HideInInspector] public bool useExternalInput;

        public void SetExternalInput(float move, float steer, bool brake)
        {
            moveInput = move;
            steerInput = steer;
            isBrakingInput = brake;
        }

        private void Awake()
        {
            ConfigureCollisionLayers();
        }

        private void Start()
        {
            carRb = GetComponent<Rigidbody>();
            if (carRb != null)
            {
                carRb.centerOfMass = _centerOfMass;
                if (carRb.mass < minMass) carRb.mass = minMass;
                carRb.maxAngularVelocity = maxAngularSpeed;
            }

            if (autoSuspension) ConfigureSuspension();
        }

        /// <summary>바퀴를 TruckWheel 레이어로 옮기고, 트럭(바퀴+몸체)이 TruckPass 레이어와 충돌하지 않게 한다.</summary>
        private void ConfigureCollisionLayers()
        {
            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider != null) wheel.wheelCollider.gameObject.layer = WheelLayer;
            }

            Physics.IgnoreLayerCollision(WheelLayer, PassLayer, true);
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                int layer = c.gameObject.layer;
                // 기본 레이어(0)는 플레이어와 같이 쓰므로 건드리지 않는다 (플레이어는 나무/철도에 계속 막힌다)
                if (layer != 0 && layer != PassLayer) Physics.IgnoreLayerCollision(layer, PassLayer, true);
            }
        }

        /// <summary>질량에 맞는 스프링/댐퍼. (기존 값은 4톤에 비해 너무 약해서 바닥까지 눌려 튕겨 났다)</summary>
        private void ConfigureSuspension()
        {
            if (carRb == null || wheels == null || wheels.Count == 0) return;

            int count = 0;
            foreach (var w in wheels) if (w.wheelCollider != null) count++;
            if (count == 0) return;

            float loadPerWheel = carRb.mass * Physics.gravity.magnitude / count;
            foreach (var w in wheels)
            {
                WheelCollider c = w.wheelCollider;
                if (c == null) continue;

                c.suspensionDistance = suspensionTravel;
                JointSpring spring = c.suspensionSpring;
                spring.targetPosition = 0.5f;
                // 정지 상태에서 서스펜션이 절반쯤 눌리도록: F = k * (거리 * 0.5)
                spring.spring = loadPerWheel / (suspensionTravel * 0.5f);
                // 임계 감쇠의 약 80%
                float sprungMass = carRb.mass / count;
                spring.damper = 0.8f * 2f * Mathf.Sqrt(spring.spring * sprungMass);
                c.suspensionSpring = spring;
                c.mass = Mathf.Max(c.mass, 40f);
            }
        }

        private void Update()
        {
            if (!isDriving) { return; }

            GetInputs();
            Ani_Wheels();
        }

        private void FixedUpdate()
        {
            if (!isDriving)
            {
                Reset_Physics();
                return;
            }

            Move();
            Steer();
            Brake();
            Apply_downForce();
        }

        private void GetInputs()
        {
            if (useExternalInput) return;

            moveInput = Input.GetAxis("Vertical");
            steerInput = Input.GetAxis("Horizontal");
            isBrakingInput = Input.GetKey(KeyCode.Space);
        }

        private void Move()
        {
            if (isBrakingInput) return;

            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider == null) continue;

                bool isDriveWheel = (driveType == Accelerator.All) || (driveType == wheel.accelerator);

                if (isDriveWheel)
                {
                    if (Mathf.Abs(moveInput) > 0.05f)
                    {
                        // 최고 속도를 넘으면 더 가속하지 않는다 (같은 방향으로 갈 때만)
                        bool overSpeed = carRb != null && carRb.linearVelocity.magnitude > maxSpeed &&
                                         Vector3.Dot(carRb.linearVelocity, transform.forward * moveInput) > 0f;
                        wheel.wheelCollider.motorTorque = overSpeed ? 0f : moveInput * maxAcceleration;
                        wheel.wheelCollider.brakeTorque = 0f;
                    }
                    else
                    {
                        wheel.wheelCollider.motorTorque = 0f;
                        wheel.wheelCollider.brakeTorque = decelerationForce;
                    }
                }
                else
                {
                    wheel.wheelCollider.motorTorque = 0f;
                    wheel.wheelCollider.brakeTorque = 0f;
                }
            }
        }

        private void Steer()
        {
            float targetSteerAngle = steerInput * maxSteerAngle;

            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider == null) continue;

                if (wheel.accelerator == Accelerator.Front)
                {
                    wheel.wheelCollider.steerAngle = Mathf.MoveTowards(
                        wheel.wheelCollider.steerAngle,
                        targetSteerAngle,
                        turnSensitivity * maxSteerAngle * Time.fixedDeltaTime * 4f
                    );
                }
            }
        }

        private void Brake()
        {
            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider == null) continue;

                if (isBrakingInput)
                {
                    wheel.wheelCollider.motorTorque = 0f;
                    wheel.wheelCollider.brakeTorque = brakeAcceleration;
                }
                else if (Mathf.Abs(moveInput) > 0.05f)
                {
                    wheel.wheelCollider.brakeTorque = 0f;
                }
            }
        }

        private void Apply_downForce()
        {
            if (carRb == null) return;
            float speed = carRb.linearVelocity.magnitude;
            carRb.AddForce(-transform.up * (downForce + speedDownForce) * speed);

            int grounded = 0;
            foreach (var wheel in wheels)
                if (wheel.wheelCollider != null && wheel.wheelCollider.isGrounded) grounded++;

            // 공중에 뜨면 더 빨리 내려오게 (붕 떠서 날아다니지 않게)
            if (grounded == 0)
                carRb.AddForce(Physics.gravity * (carRb.mass * airExtraGravity));

            ApplyAntiRoll();
            ApplyUpright(grounded);

            // 위로 튀어 오르는 속도 제한
            Vector3 v = carRb.linearVelocity;
            if (v.y > maxUpwardSpeed)
            {
                v.y = maxUpwardSpeed;
                carRb.linearVelocity = v;
            }
        }

        /// <summary>앞/뒤 차축마다 좌우 바퀴의 서스펜션 눌림 차이를 줄이는 힘 (스웨이바).</summary>
        private void ApplyAntiRoll()
        {
            if (antiRoll <= 0f) return;

            for (int i = 0; i + 1 < wheels.Count; i += 2)
            {
                WheelCollider left = wheels[i].wheelCollider;
                WheelCollider right = wheels[i + 1].wheelCollider;
                if (left == null || right == null) continue;

                // 같은 차축인지(앞쪽 두 개/뒤쪽 두 개) 확인: 로컬 z가 비슷해야 한다
                if (Mathf.Abs(transform.InverseTransformPoint(left.transform.position).z -
                              transform.InverseTransformPoint(right.transform.position).z) > 0.5f) continue;

                float travelL = SuspensionTravel01(left, out bool groundedL);
                float travelR = SuspensionTravel01(right, out bool groundedR);
                float force = (travelL - travelR) * antiRoll;

                if (groundedL) carRb.AddForceAtPosition(left.transform.up * -force, left.transform.position);
                if (groundedR) carRb.AddForceAtPosition(right.transform.up * force, right.transform.position);
            }
        }

        private static float SuspensionTravel01(WheelCollider wheel, out bool grounded)
        {
            grounded = wheel.GetGroundHit(out WheelHit hit);
            if (!grounded) return 1f;
            float compression = -wheel.transform.InverseTransformPoint(hit.point).y - wheel.radius;
            return Mathf.Clamp01(1f - compression / Mathf.Max(0.01f, wheel.suspensionDistance));
        }

        /// <summary>많이 기울면(옆으로 쓰러지려 하면) 위쪽을 하늘로 향하게 하는 토크를 준다.</summary>
        private void ApplyUpright(int groundedWheels)
        {
            float tilt = Vector3.Angle(transform.up, Vector3.up);
            if (tilt < 12f) return;

            Vector3 axis = Vector3.Cross(transform.up, Vector3.up);
            float strength = Mathf.InverseLerp(12f, 60f, tilt) * uprightTorque * (groundedWheels == 0 ? 1.5f : 1f);
            carRb.AddTorque(axis * strength, ForceMode.Force);
        }

        private void Ani_Wheels()
        {
            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider == null || wheel.wheelModel == null) continue;

                Quaternion rotation;
                Vector3 position;
                wheel.wheelCollider.GetWorldPose(out position, out rotation);
                wheel.wheelModel.transform.position = position;
                wheel.wheelModel.transform.rotation = rotation;
            }
        }

        public void Stop_Immediately()
        {
            moveInput = 0f;
            steerInput = 0f;
            isBrakingInput = false;

            if (carRb != null)
            {
                carRb.linearVelocity = Vector3.zero;
                carRb.angularVelocity = Vector3.zero;
            }

            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider == null) continue;

                wheel.wheelCollider.motorTorque = 0f;
                wheel.wheelCollider.brakeTorque = brakeAcceleration * 2f;
            }
        }

        private void Reset_Physics()
        {
            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider == null) continue;

                wheel.wheelCollider.motorTorque = 0f;
                wheel.wheelCollider.brakeTorque = brakeAcceleration * 2f;
            }

            if (carRb != null)
            {
                carRb.linearVelocity = Vector3.zero;
                carRb.angularVelocity = Vector3.zero;
            }
        }
    }
}