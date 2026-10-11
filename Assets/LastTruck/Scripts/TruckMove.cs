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

        [Header("핸들 조작감")]
        [Tooltip("핸들을 꺾는 속도 (도/초)")]
        public float steerInSpeed = 110f;
        [Tooltip("키를 놓거나 반대로 꺾을 때 핸들이 돌아오는 속도 (도/초). 빠를수록 차가 바로 펴진다.")]
        public float steerReturnSpeed = 240f;
        [Tooltip("빠르게 달릴 때의 최대 조향각 (속도가 오를수록 maxSteerAngle에서 이 값으로 줄어든다 → 고속에서 휙 돌지 않음)")]
        public float highSpeedSteerAngle = 12f;
        [Tooltip("이 속도(m/s)에서 조향각이 highSpeedSteerAngle까지 줄어든다")]
        public float highSpeedForSteer = 18f;

        [Header("물리")]
        public Vector3 _centerOfMass = new Vector3(0, -0.8f, 0);
        public float downForce = 150.0f;
        public Rigidbody carRb;

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
            // 무게/무게 중심/튐 억제 보정 (운전 중이 아닐 때도 동작해야 해서 따로 붙인다)
            if (GetComponent<TruckStabilizer>() == null && GetComponent<Rigidbody>() != null)
                gameObject.AddComponent<TruckStabilizer>();
        }

        private void Start()
        {
            carRb = GetComponent<Rigidbody>();
            if (carRb != null)
            {
                carRb.centerOfMass = _centerOfMass;
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
            steerInput = Input.GetAxisRaw("Horizontal"); // 부드럽게 꺾는 것은 Steer()에서 한다 (입력 지연 없이)
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
                        wheel.wheelCollider.motorTorque = moveInput * maxAcceleration;
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
            // 속도가 빠를수록 최대 조향각을 줄인다
            float speed = carRb != null ? carRb.linearVelocity.magnitude : 0f;
            float speed01 = highSpeedForSteer > 0f ? Mathf.Clamp01(speed / highSpeedForSteer) : 0f;
            float maxAngle = Mathf.Lerp(maxSteerAngle, Mathf.Min(maxSteerAngle, highSpeedSteerAngle), speed01);
            float targetSteerAngle = Mathf.Clamp(steerInput, -1f, 1f) * maxAngle;

            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider == null) continue;

                if (wheel.accelerator == Accelerator.Front)
                {
                    float current = wheel.wheelCollider.steerAngle;
                    // 놓거나 반대로 꺾을 때는 빠르게 돌아오고, 같은 쪽으로 더 꺾을 때는 조금 천천히
                    bool returning = Mathf.Abs(targetSteerAngle) < Mathf.Abs(current) || targetSteerAngle * current < 0f;
                    float rate = (returning ? steerReturnSpeed : steerInSpeed) * Mathf.Max(0.1f, turnSensitivity);
                    wheel.wheelCollider.steerAngle = Mathf.MoveTowards(current, targetSteerAngle, rate * Time.fixedDeltaTime);
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
            carRb.AddForce(-transform.up * downForce * carRb.linearVelocity.magnitude);
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