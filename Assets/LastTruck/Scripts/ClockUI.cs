using UnityEngine;
using UnityEngine.UI;

namespace LastTruck
{
    public class UIClock : MonoBehaviour
    {
        [Header("게임 매니저")]
        [SerializeField] private GameManager gameManager;

        [Header("UI")]
        [SerializeField] private RectTransform clockHand;
        [SerializeField] private Text dayText;

        [Header("시작 각도")]
        [Tooltip("낮")]
        [SerializeField] private float dayStartAngleZ = 0f;
        [Tooltip("밤")]
        [SerializeField] private float nightStartAngleZ = 180f;

        private void OnEnable()
        {
            if (gameManager == null)
            {
                gameManager = GameManager.Instance;
            }

            if (gameManager != null)
            {
                gameManager.OnDayStarted += Handle_DayStarted;
                gameManager.OnNightStarted += Handle_NightStarted;
            }
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.OnDayStarted -= Handle_DayStarted;
                gameManager.OnNightStarted -= Handle_NightStarted;
            }
        }

        private void Start()
        {
            if (gameManager == null)
            {
                gameManager = GameManager.Instance;
            }

            if (gameManager != null)
            {
                UpdateDayText(gameManager.CurrentCycle);

                bool isDay = (gameManager.CurrentPhase == GameManager.GamePhase.Day);
                float initialAngle = isDay ? dayStartAngleZ : nightStartAngleZ;
                Apply_ClockHandRotation(initialAngle);
            }
        }

        private void Update()
        {
            if (gameManager == null)
            {
                gameManager = GameManager.Instance;
                if (gameManager == null) return;
            }

            float currentAngle = 0f;

            if (gameManager.CurrentPhase == GameManager.GamePhase.Day)
            {
                float totalDayTime = gameManager.DayDurationSeconds;
                if (totalDayTime > 0f)
                {
                    float progress = Mathf.Clamp01(1f - (gameManager.DayTimeRemaining / totalDayTime));
                    currentAngle = Mathf.Lerp(dayStartAngleZ, nightStartAngleZ, progress);
                }
            }
            else if (gameManager.CurrentPhase == GameManager.GamePhase.Night)
            {
                int quota = gameManager.NightMonsterQuota;
                if (quota > 0)
                {
                    float progress = Mathf.Clamp01((float)gameManager.NightMonstersKilled / quota);
                    currentAngle = Mathf.Lerp(nightStartAngleZ, dayStartAngleZ + 360f, progress);
                }
                else
                {
                    currentAngle = nightStartAngleZ;
                }
            }

            Apply_ClockHandRotation(currentAngle);
        }

        private void Handle_DayStarted(int cycle)
        {
            UpdateDayText(cycle);
            Apply_ClockHandRotation(dayStartAngleZ);
        }

        private void Handle_NightStarted(int quota)
        {
            Apply_ClockHandRotation(nightStartAngleZ);
        }

        private void Apply_ClockHandRotation(float angleZ)
        {
            if (clockHand != null)
            {
                clockHand.localRotation = Quaternion.Euler(0f, 0f, -angleZ);
            }
        }

        private void UpdateDayText(int cycle)
        {
            if (dayText != null)
            {
                dayText.text = $"DAY {cycle}";
            }
        }
    }
}