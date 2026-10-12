using UnityEngine;

public class FadeController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("씬에 있는 플레이어 캐릭터의 Transform")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("플레이어와 카메라 사이를 체크할 메인 카메라 (또는 시네머신 카메라)")]
    [SerializeField] private Transform cameraTransform;

    [Header("Settings")]
    [Tooltip("나무 머티리얼이 적용된 렌더러 (나무 객체 본인 혹은 하위 렌더러)")]
    [SerializeField] private Renderer treeRenderer;

    // 셰이더 그래프에서 만든 프로퍼티 이름들을 코드용 ID로 변환 (최적화 목적)
    private static readonly int PlayerPosID = Shader.PropertyToID("_PlayerPosition");
    private static readonly int FadeRadiusID = Shader.PropertyToID("_FadeRadius");

    void Update()
    {
        if (playerTransform == null || cameraTransform == null || treeRenderer == null) return;

        // 1. 매 프레임 플레이어의 월드 좌표를 셰이더의 _PlayerPosition에 전달
        // MaterialPropertyBlock을 사용해 개별 나무 머티리얼마다 독립적으로 값을 전달합니다.
        MaterialPropertyBlock props = new MaterialPropertyBlock();
        treeRenderer.GetPropertyBlock(props);

        props.SetVector(PlayerPosID, playerTransform.position);

        // 셰이더에 설정한 반경 값 (필요시 코드에서 조절 가능)
        props.SetFloat(FadeRadiusID, 1.5f);

        treeRenderer.SetPropertyBlock(props);
    }
}