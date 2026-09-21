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
            Rear
        }

        [Serializable]
        public struct Wheel
        {
            public GameObject wheelModel;
            public WheelCollider wheelCollider;
            public Accelerator accelerator;
        }

        [Header("가속")]
        public float maxAcceleration = 1500.0f;
        public float brakeAcceleration = 5000.0f;

        [Header("방향")]
        public float turnSensitivility = 1.0f;
        public float maxSteerAngle = 30.0f;

        [Header("물리")]
        public Vector3 _centerOfMass;
        public Rigidbody carRb;

        public List<Wheel> wheels;
        public bool isDriving = false;

        float moveInput;
        float steerInput;

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
        }

        private void GetInputs()
        {
            moveInput = Input.GetAxis("Vertical");
            steerInput = Input.GetAxis("Horizontal");
        }

        private void Move()
        {
            if (Input.GetKey(KeyCode.Space)) return;

            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider == null) continue;
                wheel.wheelCollider.motorTorque = moveInput * maxAcceleration;
            }
        }

        private void Steer()
        {
            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider == null) continue;

                if (wheel.accelerator == Accelerator.Front)
                {
                    var _steerAngle = steerInput * turnSensitivility * maxSteerAngle;
                    wheel.wheelCollider.steerAngle = Mathf.Lerp(wheel.wheelCollider.steerAngle, _steerAngle, 0.6f);
                }
            }
        }

        private void Brake()
        {
            bool isBraking = Input.GetKey(KeyCode.Space);

            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider == null) continue;

                if (isBraking)
                {
                    wheel.wheelCollider.motorTorque = 0f;
                    wheel.wheelCollider.brakeTorque = brakeAcceleration;
                }
                else
                {
                    wheel.wheelCollider.brakeTorque = 0f;
                }
            }
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

        private void Reset_Physics()
        {
            foreach (var wheel in wheels)
            {
                if (wheel.wheelCollider == null) continue;

                wheel.wheelCollider.motorTorque = 0f;
                wheel.wheelCollider.brakeTorque = brakeAcceleration * 0.5f; // 주차용 브레이크
            }
        }
    }
}
