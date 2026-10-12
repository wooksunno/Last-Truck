using UnityEngine;
using System.Collections.Generic;

public class CameraOcclusionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform cameraTransform;

    [Header("Settings")]
    [SerializeField] private Material fadeMaterial; // 반투명 머티리얼
    [SerializeField] private LayerMask treeLayer;   // 나무들만 속한 레이어
    [SerializeField] private List<string> excludeTags = new List<string> { "Untagged" }; // 제외할 태그

    [Header("Distance Range Settings")]
    [Tooltip("카메라와 플레이어 거리가 이 이상 멀어지면 투명화 기능을 아예 끕니다 (최대 시야 거리)")]
    [SerializeField] private float maxCameraDistance = 15f;
    [Tooltip("플레이어 중심 기준으로 이 반경 내에 있는 장애물만 투명화합니다 (멀리 있는 나무 무시용)")]
    [SerializeField] private float playerCheckRadius = 3f;

    private List<RendererAndMaterial> fadedRenderers = new List<RendererAndMaterial>();

    private struct RendererAndMaterial
    {
        public Renderer renderer;
        public Material originalMaterial;
    }

    void LateUpdate()
    {
        if (playerTransform == null || cameraTransform == null) return;

        // 1. 기존에 가려졌다가 벗어난 나무들은 원래 머티리얼 복구
        ResetFadedObjects();

        // 2. 카메라와 플레이어 간의 거리 계산
        Vector3 direction = playerTransform.position - cameraTransform.position;
        float totalDistance = direction.magnitude;

        // 카메라가 플레이어로부터 너무 멀리 떨어져 있다면 투명화 로직 스킵
        if (totalDistance > maxCameraDistance) return;

        // 3. 카메라와 플레이어 사이에 있는 모든 오브젝트 감지
        RaycastHit[] hits = Physics.RaycastAll(cameraTransform.position, direction.normalized, totalDistance, treeLayer);

        foreach (var hit in hits)
        {
            GameObject hitObj = hit.collider.gameObject;

            // 제외 태그 체크
            if (ShouldExclude(hitObj)) continue;

            // [추가된 핵심 조절]: 나무가 플레이어로부터 너무 멀리 떨어져 있다면 무시함!
            // 즉, 플레이어 근처에 가려진 나무만 투명하게 만듦
            float distToPlayer = Vector3.Distance(hit.point, playerTransform.position);
            if (distToPlayer > playerCheckRadius) continue;

            Renderer rend = hit.collider.GetComponent<Renderer>();
            if (rend != null)
            {
                if (!IsAlreadyFaded(rend))
                {
                    Material origMat = rend.sharedMaterial;
                    rend.material = fadeMaterial;

                    fadedRenderers.Add(new RendererAndMaterial { renderer = rend, originalMaterial = origMat });
                }
            }
        }
    }

    bool ShouldExclude(GameObject obj)
    {
        foreach (string tag in excludeTags)
        {
            if (obj.CompareTag(tag)) return true;
        }
        return false;
    }

    bool IsAlreadyFaded(Renderer rend)
    {
        foreach (var item in fadedRenderers)
        {
            if (item.renderer == rend) return true;
        }
        return false;
    }

    void ResetFadedObjects()
    {
        foreach (var item in fadedRenderers)
        {
            if (item.renderer != null)
            {
                item.renderer.material = item.originalMaterial;
            }
        }
        fadedRenderers.Clear();
    }
}