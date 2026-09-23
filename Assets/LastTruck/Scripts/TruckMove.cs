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