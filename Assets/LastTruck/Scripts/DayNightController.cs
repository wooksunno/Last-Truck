using System.Collections;
using UnityEngine;

namespace LastTruck
{
    public class DayNightController : MonoBehaviour
    {
        [Header("게임 매니저 연동")]
        [SerializeField] private GameManager gameManager;

        [Header("고정 각도 (X값 기준)")]
        [SerializeField] private float dayAngleX = 90f;
        [SerializeField] private float nightAngleX = 270f;

        [Header("밤 -> 낮 전환 속도")]
        [Tooltip("밤에서 낮으로 바뀔 때 90도로 빠르게 돌아가는 속도 (초당 각도)")]
        [SerializeField] private float dayTransitionSpeedDegPerSec = 720f;

        private float currentAngle;
        private Coroutine rotateRoutine;

        private void OnEnable()
        {
            if (gameManager != null)
            {
                gameManager.OnDayStarted += HandleDayStarted;
                gameManager.OnNightStarted += HandleNightStarted;
            }
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.OnDayStarted -= HandleDayStarted;
                gameManager.OnNightStarted -= HandleNightStarted;
            }
        }

        private void Start()
        {
            // 스크립트 실행 순서에 관계없이 시작 시점의 현재 페이즈에 맞춰 즉시 동기화
            if (gameManager != null)
            {
                currentAngle = gameManager.CurrentPhase == GameManager.GamePhase.Day ? dayAngleX : nightAngleX;
                ApplyRotation();
            }
        }

        /// <summary>낮이 시작되면(밤 종료 포함) 90도까지 빠르게 회전 애니메이션으로 이동.</summary>
        private void HandleDayStarted(int cycle)
        {
            if (rotateRoutine != null)
            {
                StopCoroutine(rotateRoutine);
            }

            rotateRoutine = StartCoroutine(RotateToAngle(dayAngleX, dayTransitionSpeedDegPerSec));
        }

        /// <summary>밤이 시작되면 회전 없이 270도로 즉시 고정.</summary>
        // private void HandleNightStarted(int quota)
        // {
        //     if (rotateRoutine != null)
        //     {
        //         StopCoroutine(rotateRoutine);
        //         rotateRoutine = null;
        //     }

        //     currentAngle = nightAngleX;
        //     ApplyRotation();
        // }

        /// <summary>밤이 시작되면 270도까지 빠르게 회전 애니메이션으로 이동.</summary>
        private void HandleNightStarted(int quota)
        {
            if (rotateRoutine != null)
            {
                StopCoroutine(rotateRoutine);
            }

            // 낮 -> 밤도 코루틴으로 회전
            rotateRoutine = StartCoroutine(RotateToAngle(nightAngleX, dayTransitionSpeedDegPerSec));
        }


        private IEnumerator RotateToAngle(float targetAngle, float speedDegPerSec)
        {
            while (Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle)) > 0.01f)
            {
                currentAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, speedDegPerSec * Time.deltaTime);
                ApplyRotation();
                yield return null;
            }

            currentAngle = targetAngle;
            ApplyRotation();
            rotateRoutine = null;
        }

        private void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(new Vector3(currentAngle, 170f, 0f));
        }
    }
}