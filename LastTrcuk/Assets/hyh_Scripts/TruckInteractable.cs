using Unity.Cinemachine;
using UnityEngine;

public class TruckInteractable : MonoBehaviour, IInteractable
{
    public TruckMove truckController;
    public Transform seatPoint;
    public Transform exitPoint;
    public CinemachineCamera virtualCam;

    private bool isDriving = false;
    private GameObject driverPlayer;

    private void Start()
    {
        if (truckController != null)
        {
            truckController.isDriving = false;
            truckController.enabled = false;
        }
    }

    public void Interact(GameObject player)
    {
        if (isDriving)
        {
            GetOut_Truck();
        }
        else
        {
            GetIn_Truck(player);
        }
    }

    private void GetIn_Truck(GameObject player)
    {
        isDriving = true;
        driverPlayer = player;

        if (player.TryGetComponent<PlayerMove>(out var moveScript)) moveScript.enabled = false;
        if (player.TryGetComponent<Collider>(out var col)) col.enabled = false;

        if (player.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        player.transform.SetParent(seatPoint != null ? seatPoint : transform);
        player.transform.localPosition = Vector3.zero;
        player.transform.localRotation = Quaternion.identity;
        player.SetActive(false);

        if (truckController != null)
        {
            truckController.isDriving = true;
            truckController.enabled = true;
        }

        if (virtualCam != null) virtualCam.Target.TrackingTarget = transform;
    }

    private void GetOut_Truck()
    {
        if (driverPlayer == null) return;

        isDriving = false;

        if (truckController != null)
        {
            truckController.isDriving = false;
            truckController.enabled = false;
        }

        if (TryGetComponent<Rigidbody>(out var truckRb))
        {
            truckRb.linearVelocity = Vector3.zero;
            truckRb.angularVelocity = Vector3.zero;
        }

        driverPlayer.transform.SetParent(null);
        driverPlayer.transform.position = exitPoint != null ? exitPoint.position : transform.position + Vector3.right * 2.5f;
        driverPlayer.SetActive(true);

        if (driverPlayer.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (driverPlayer.TryGetComponent<PlayerMove>(out var moveScript)) moveScript.enabled = true;
        if (driverPlayer.TryGetComponent<Collider>(out var col)) col.enabled = true;

        if (virtualCam != null) { virtualCam.Target.TrackingTarget = driverPlayer.transform; }

        driverPlayer = null;
    }

    private void Update()
    {
        if (isDriving && Input.GetKeyDown(KeyCode.E))
        {
            GetOut_Truck();
        }
    }
}