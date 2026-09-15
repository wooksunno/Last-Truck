using UnityEngine;

public class TruckMove : MonoBehaviour
{
    [System.Serializable]
    public struct WheelInfo
    {
        public WheelCollider leftWheel;
        public WheelCollider rightWheel;
        public Transform leftWheelMesh;
        public Transform rightWheelMesh;
        public bool motor;
    }

    [Header("트럭 바퀴")]
    public WheelInfo frontWheels;
    public WheelInfo rearWheels;

    [Header("트럭 제어 수치")]
    public float maxMotorTorque = 3000f;
    public float autoBrakeTorque = 10000f;

    [Header("탑승 상태")]
    public bool isDriving = false;

    private float inputVertical;
    private Rigidbody truckRb;

    private void Awake()
    {
        truckRb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (truckRb != null) truckRb.isKinematic = false;

        Apply_Brake(frontWheels, 0f);
        Apply_Brake(rearWheels, 0f);
    }

    private void OnDisable()
    {
        StopVehicle();
    }

    private void Update()
    {
        if (isDriving)
        {
            inputVertical = Input.GetAxis("Vertical");
        }
        else
        {
            inputVertical = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (isDriving && Mathf.Abs(inputVertical) > 0.05f)
        {
            float currentTorque = inputVertical * maxMotorTorque;

            Apply_Brake(frontWheels, 0f);
            Apply_Brake(rearWheels, 0f);

            Apply_Motor(frontWheels, currentTorque);
            Apply_Motor(rearWheels, currentTorque);
        }
        else
        {
            StopVehicle();
        }

        UpdateWheelVisual(frontWheels.leftWheel, frontWheels.leftWheelMesh);
        UpdateWheelVisual(frontWheels.rightWheel, frontWheels.rightWheelMesh);
        UpdateWheelVisual(rearWheels.leftWheel, rearWheels.leftWheelMesh);
        UpdateWheelVisual(rearWheels.rightWheel, rearWheels.rightWheelMesh);
    }

    private void StopVehicle()
    {
        Apply_Motor(frontWheels, 0f);
        Apply_Motor(rearWheels, 0f);

        Apply_Brake(frontWheels, autoBrakeTorque);
        Apply_Brake(rearWheels, autoBrakeTorque);

        if (truckRb != null)
        {
            truckRb.linearVelocity = Vector3.zero;
            truckRb.angularVelocity = Vector3.zero;
        }
    }

    private void Apply_Motor(WheelInfo wheels, float torque)
    {
        if (wheels.motor && wheels.leftWheel != null && wheels.rightWheel != null)
        {
            wheels.leftWheel.motorTorque = torque;
            wheels.rightWheel.motorTorque = torque;
        }
    }

    private void Apply_Brake(WheelInfo wheels, float brake)
    {
        if (wheels.leftWheel != null && wheels.rightWheel != null)
        {
            wheels.leftWheel.brakeTorque = brake;
            wheels.rightWheel.brakeTorque = brake;
        }
    }

    private void UpdateWheelVisual(WheelCollider collider, Transform mesh)
    {
        if (collider == null || mesh == null) return;

        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);

        mesh.rotation = rotation;
    }
}