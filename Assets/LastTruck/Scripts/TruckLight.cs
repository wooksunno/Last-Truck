using UnityEngine;

namespace LastTruck
{
    public class TruckLight : MonoBehaviour
    {
        [Header("Spot Light")]
        [SerializeField] private Light frontLightL;
        [SerializeField] private Light frontLightR;

        [Header("설정")]
        [SerializeField] private bool autoTurnOffOnExit = true; // 하차 시 자동으로 전조등 끄기 여부

        private bool isLightOn = false;
        private bool isDriving = false;

        private void Start()
        {
            // 게임 시작 시 라이트 OFF 초기화
            ApplyLightState(false);
        }

        private void Update()
        {
            // 운전 중일 때만 L 키로 전조등 토글
            if (isDriving && Input.GetKeyDown(KeyCode.L))
            {
                ToggleLights();
            }
        }

        public void ToggleLights()
        {
            isLightOn = !isLightOn;
            ApplyLightState(isLightOn);
        }

        public void SetLightState(bool state)
        {
            isLightOn = state;
            ApplyLightState(isLightOn);
        }

        /// <summary>
        /// TruckInteractable에서 운전 시작/종료 시 호출
        /// </summary>
        public void SetDrivingState(bool driving)
        {
            isDriving = driving;

            // 내릴 때 자동 꺼짐 옵션이 활성화되어 있다면 꺼줌
            if (!isDriving && autoTurnOffOnExit)
            {
                SetLightState(false);
            }
        }

        private void ApplyLightState(bool state)
        {
            if (frontLightL != null) frontLightL.enabled = state;
            if (frontLightR != null) frontLightR.enabled = state;
        }
    }
}