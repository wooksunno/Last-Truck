using UnityEngine;

public class ObstacleTransparency : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Camera mainCamera;

    [Header("Settings")]
    [SerializeField] private LayerMask obstacleLayer; // 나무, 벽 등이 포함된 레이어
    [SerializeField] private float fadeRadius = 1.5f; // 투명화 반경

    // 성능을 위해 셰이더 프로퍼티 ID를 미리 계산
    private static readonly int PlayerPosID = Shader.PropertyToID("_PlayerPosition");
    private static readonly int FadeRadiusID = Shader.PropertyToID("_FadeRadius");

    void Update()
    {
        if (playerTransform == null || mainCamera == null) return;

        // 카메라에서 플레이어를 향하는 벡터 계산
        Vector3 direction = playerTransform.position - mainCamera.transform.position;
        float distance = direction.magnitude;

        // 레이캐스트 쏘기
        RaycastHit hit;
        if (Physics.Raycast(mainCamera.transform.position, direction.normalized, out hit, distance, obstacleLayer))
        {
            // 장애물에 부딪혔다면
            Renderer obstacleRenderer = hit.collider.GetComponent<Renderer>();
            if (obstacleRenderer != null)
            {
                // 장애물의 머티리얼에 현재 플레이어 위치와 반경을 전달
                // *주의: 전역 셰이더 변수를 쓰거나, 머티리얼 인스턴스를 조절해야 함
                MaterialPropertyBlock props = new MaterialPropertyBlock();
                props.SetVector(PlayerPosID, playerTransform.position);
                props.SetFloat(FadeRadiusID, fadeRadius);
                obstacleRenderer.SetPropertyBlock(props);
            }
        }
    }
}