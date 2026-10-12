
using UnityEngine;
using Unity.Cinemachine;

[AddComponentMenu("Cinemachine/Camera Raycast Collision")]
public class CameraRaycastCollision : CinemachineExtension
{
    [Header("Target")]
    [Tooltip("플레이어 가슴 높이에 있는 기준 Transform")]
    [SerializeField] private Transform target;

    [Header("Collision")]
    [Tooltip("카메라 충돌 검사 대상 레이어")]
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Tooltip("장애물 표면에서 카메라까지의 여유 거리")]
    [SerializeField, Min(0.01f)]
    private float wallPadding = 0.2f;

    [Tooltip("장애물이 사라졌을 때 복귀하는 시간")]
    [SerializeField, Min(0.01f)]
    private float returnSmoothTime = 0.15f;

    private Vector3 currentOffset;
    private Vector3 offsetVelocity;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize)
            return;

        if (target == null)
            return;

        // Cinemachine이 계산한 최종 위치
        Vector3 desiredPosition = state.GetFinalPosition();
        Vector3 origin = target.position;

        Vector3 direction = desiredPosition - origin;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return;

        direction /= distance;

        if (Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit,
            distance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore))
        {
            // 충돌 지점보다 플레이어 쪽에 카메라 배치
            Vector3 safePosition =
                hit.point - direction * wallPadding;

            currentOffset = safePosition - desiredPosition;
            offsetVelocity = Vector3.zero;
        }
        else
        {
            // 장애물이 사라지면 원래 위치로 부드럽게 복귀
            float dt = deltaTime > 0f
                ? deltaTime
                : Time.deltaTime;

            currentOffset = Vector3.SmoothDamp(
                currentOffset,
                Vector3.zero,
                ref offsetVelocity,
                returnSmoothTime,
                Mathf.Infinity,
                dt
            );
        }

        // Cinemachine 위치 보정에 오프셋 적용
        state.PositionCorrection += currentOffset;
    }
}
