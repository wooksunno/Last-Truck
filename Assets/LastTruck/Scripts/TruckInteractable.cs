using Unity.Cinemachine;
using UnityEngine;

namespace LastTruck
{
    public class TruckInteractable : MonoBehaviour, IHoldInteractable
    {
        public TruckMove truckController;
        public Transform seatPoint;
        public Transform exitPoint;
        public CinemachineCamera virtualCam;

        public float exitCooldown = 0.2f;   // 하차 대기 시간

        private bool isDriving = false;
        private GameObject driverPlayer;
        private float driveStartTime;
        private CraftingSystem.TruckRepair _repair;
        private bool isGunning;   // 싱글플레이: 트럭 안에서 포탑을 잡고 있다 (T로 운전 ↔ 포탑, 포탑 중엔 트럭이 선다)

        private bool HasTurret
        {
            get
            {
                var state = GetComponent<CraftingSystem.TruckUpgradeState>();
                return state != null && state.HasTurret;
            }
        }

        // 수리 아이템을 들고 있고 트럭이 손상된 경우에만 E키를 꾹 눌러야 한다(그 외엔 즉시 탑승).
        public float RequiredHoldSeconds
        {
            get
            {
                if (_repair == null) _repair = GetComponent<CraftingSystem.TruckRepair>();
                return _repair != null ? _repair.CurrentRepairHoldSeconds : 0f;
            }
        }

        public TruckLight lightController;

        private void Start()
        {
            if (truckController != null)
            {
                truckController.isDriving = false;
                truckController.enabled = false;
            }
        }

        private void Update()
        {
            if (isDriving && isGunning && !HasTurret) SetGunning(false);   // 포탑을 떼어냈다

            if (isDriving && isGunning)
            {
                var turret = GetComponent<CraftingSystem.TruckTurret>();
                if (turret != null) turret.LocalGunnerUpdate();
            }

            if (isDriving && Time.time >= driveStartTime + exitCooldown)
            {
                // 운전 중에 밤이 되면 트럭을 세운다: 포탑이 있으면 포탑으로, 없으면 내리게 한다
                if (GameManager.IsNight && !isGunning)
                {
                    if (HasTurret)
                    {
                        HudToast.Show("밤이 되어 트럭이 멈췄습니다. 포탑으로 버티세요!");
                        SetGunning(true);
                        return;
                    }
                    HudToast.Show("밤이 되어 트럭에서 내렸습니다. 밤에는 트럭을 운전할 수 없습니다.");
                    GetOut_Truck();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.E))
                {
                    GetOut_Truck();
                }
                else if (Input.GetKeyDown(KeyCode.T) && HasTurret)
                {
                    if (isGunning && GameManager.IsNight) HudToast.Show("밤에는 운전할 수 없습니다.");
                    else SetGunning(!isGunning);
                }
            }
        }

public void Interact(GameObject player)
        {
            if (_repair == null) _repair = GetComponent<CraftingSystem.TruckRepair>();
            if (_repair != null && _repair.TryRepair(player))
                return;

            // 밤에는 운전할 수 없다 (낮에 이동하고 밤에는 트럭 거점에서 버틴다). 포탑이 있으면 사수로는 탈 수 있다.
            if (GameManager.IsNight && !isDriving && (!HasTurret || LastTruck.Networking.GameLauncher.IsOnlineSession))
            {
                HudToast.Show("밤에는 트럭을 운전할 수 없습니다. 날이 밝을 때까지 버티세요.");
                return;
            }

            // 멀티플레이: 운전자 1명 + 동승자 방식 (호스트가 좌석을 정한다 - LastTruck.Networking.NetworkTruck).
            // 캐릭터를 끄는 기존 방식은 네트워크 캐릭터를 망가뜨리므로 쓰지 않는다.
            if (LastTruck.Networking.GameLauncher.IsOnlineSession)
            {
                LastTruck.Networking.NetworkTruck.RequestBoard();
                return;
            }

            if (!isDriving)
            {
                GetIn_Truck(player);
                if (GameManager.IsNight && HasTurret) SetGunning(true);
            }
        }

        /// <summary>싱글플레이: 운전 ↔ 포탑 사수. 포탑을 잡는 동안 트럭은 멈춘다.</summary>
        private void SetGunning(bool on)
        {
            isGunning = on;
            if (truckController != null) truckController.isDriving = !on;
            var turret = GetComponent<CraftingSystem.TruckTurret>();
            if (turret != null && !on) turret.StopLocalGunner();
            if (on) HudToast.Show("포탑 사수   마우스: 조준   좌클릭: 발사   R: 탄환 만들기   T: 운전석으로");
        }

        private void GetIn_Truck(GameObject player)
        {
            isDriving = true;
            driverPlayer = player;
            driveStartTime = Time.time;
            gameObject.tag = "Player";

            if (player.TryGetComponent<PlayerMove>(out var moveScript)) moveScript.enabled = false;
            if (player.TryGetComponent<PlayerAttack>(out var attackScript)) attackScript.enabled = false;
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

            if (virtualCam != null)
            {
                virtualCam.Target.TrackingTarget = transform;
            }

            if (lightController != null)
            {
                lightController.SetDrivingState(true);
            }
        }

        private void GetOut_Truck()
        {
            if (driverPlayer == null) { return; }
            if (isGunning) SetGunning(false);
            isDriving = false;
            gameObject.tag = "Untagged";

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
            if (driverPlayer.TryGetComponent<PlayerAttack>(out var attackScript)) attackScript.enabled = true;
            if (driverPlayer.TryGetComponent<Collider>(out var col)) col.enabled = true;

            if (virtualCam != null)
            {
                virtualCam.Target.TrackingTarget = driverPlayer.transform;
            }

            driverPlayer = null;

            if (lightController != null)
            {
                lightController.SetDrivingState(false);
            }

            driverPlayer = null;
        }
    }
}
