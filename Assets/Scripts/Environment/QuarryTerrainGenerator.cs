using System.Collections.Generic;
using UnityEngine;

namespace Environment
{
    // 채석장을 "지면 위에 쌓기"가 아니라 "지면을 파내는 실제 메시"로 만든다.
    // 이 메시 자체가 넓은 평지 스커트(groundMeshRadius)까지 포함하므로, 별도의 PP_Ground 평면과
    // 겹쳐서 구덩이를 가리는 일이 없다 (PP_Ground는 비활성화하고 이 메시가 바닥 역할을 대신한다).
    // 생성 순서: 0) 크레이터+바닥 메시 생성 -> 1) 나무(선택적) -> 2) 돌을 경사면에 스캐터
    // -> 3) 다이아몬드(경사면 넓게 분포) -> 4) 백금(가장 밑 중앙에 좁게 집중).
    public class QuarryTerrainGenerator : MonoBehaviour
    {
        [Header("Ground Reference")]
        [SerializeField] private Transform centerOverride;
        [SerializeField] private float groundY = -0.82f; // PP_Ground 높이와 일치
        [SerializeField] private Material craterMaterial;
        [SerializeField] private float groundMeshRadius = 50f; // 이 메시가 덮는 전체 바닥 반경

        [Header("Crater Shape (바닥보다 아래로 휘어짐)")]
        [SerializeField] private float pitFlatBottomRadius = 2f;
        [SerializeField] private float pitOuterRadius = 8f; // 이 반경 밖은 원래 바닥 높이
        [SerializeField] private float pitDepth = 7f; // 바닥보다 얼마나 더 파일지
        [SerializeField] private int radialSegments = 10;
        [SerializeField] private int angularSegments = 48;

        [Header("Phase 2: Slope Rocks (돌)")]
        [SerializeField] private GameObject[] rockPrefabs;
        [SerializeField] private int slopeRockCount = 35;
        [SerializeField] private Vector2 rockScaleRange = new Vector2(0.6f, 1.3f);

        [Header("Phase 1: Forest Ring (나무, 필요할 때만 켜기)")]
        [SerializeField] private GameObject[] treePrefabs;
        [SerializeField] private int treeCount = 0;
        [SerializeField] private float forestMinRadius = 16f;
        [SerializeField] private float forestMaxRadius = 28f;
        [SerializeField] private Vector2 treeScaleRange = new Vector2(0.85f, 1.3f);

        [Header("Phase 3: Diamond (경사면을 따라 넓게 분포)")]
        [SerializeField] private GameObject[] diamondPrefabs;
        [SerializeField] private int diamondCount = 6;
        [SerializeField] private float diamondMinRadius = 0f;
        [SerializeField] private float diamondMaxRadius = 6f;

        [Header("Phase 4: Platinum (가장 밑 중앙에 좁게 집중)")]
        [SerializeField] private GameObject[] platinumPrefabs;
        [SerializeField] private int platinumCount = 2;
        [SerializeField] private float platinumMinRadius = 0f;
        [SerializeField] private float platinumMaxRadius = 1.5f;

        [Header("Random")]
        [SerializeField] private bool useFixedSeed = true;
        [SerializeField] private int seed = 12345;

        [Header("Behaviour")]
        [SerializeField] private bool generateOnStart = true;

        private readonly List<GameObject> generatedGroups = new List<GameObject>();

        private void Start()
        {
            if (generateOnStart)
            {
                Generate();
            }
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            ClearGenerated();

            System.Random rng = useFixedSeed ? new System.Random(seed) : new System.Random();
            Vector3 center = centerOverride != null ? centerOverride.position : transform.position;
            center.y = groundY;

            BuildCraterMesh(center);

            SpawnRingScatter(rng, center, treePrefabs, treeCount, forestMinRadius, forestMaxRadius, "Generated_Forest", treeScaleRange, (radius) => groundY);
            SpawnRingScatter(rng, center, rockPrefabs, slopeRockCount, pitFlatBottomRadius + 0.5f, pitOuterRadius - 0.5f, "Generated_SlopeRocks", rockScaleRange, EvaluateCraterHeight);

            PlaceTreasureGroup(rng, center, diamondPrefabs, diamondCount, diamondMinRadius, diamondMaxRadius, "Generated_Treasure_Diamond");
            PlaceTreasureGroup(rng, center, platinumPrefabs, platinumCount, platinumMinRadius, platinumMaxRadius, "Generated_Treasure_Platinum");
        }

        [ContextMenu("Clear Generated")]
        public void ClearGenerated()
        {
            for (int i = generatedGroups.Count - 1; i >= 0; i--)
            {
                if (generatedGroups[i] != null)
                {
                    DestroyGroup(generatedGroups[i]);
                }
            }
            generatedGroups.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Generated_") || child.name.StartsWith("Centerpiece_"))
                {
                    DestroyGroup(child.gameObject);
                }
            }
        }

        private void DestroyGroup(GameObject group)
        {
            if (Application.isPlaying) Destroy(group);
            else DestroyImmediate(group);
        }

        // 중심에서 0(가장 깊음)일수록 낮고, pitOuterRadius 밖이면 원래 바닥 높이로 부드럽게 휘어져 복귀한다.
        private float EvaluateCraterHeight(float radius)
        {
            if (radius >= pitOuterRadius) return groundY;
            if (radius <= pitFlatBottomRadius) return groundY - pitDepth;

            float t = (radius - pitFlatBottomRadius) / (pitOuterRadius - pitFlatBottomRadius);
            float curve = Mathf.SmoothStep(0f, 1f, t);
            return Mathf.Lerp(groundY - pitDepth, groundY, curve);
        }

        private void BuildCraterMesh(Vector3 center)
        {
            GameObject meshObj = new GameObject("Generated_CraterMesh");
            meshObj.transform.SetParent(transform, false);
            meshObj.transform.position = new Vector3(center.x, 0f, center.z);
            generatedGroups.Add(meshObj);

            MeshFilter meshFilter = meshObj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = meshObj.AddComponent<MeshRenderer>();
            MeshCollider meshCollider = meshObj.AddComponent<MeshCollider>();

            int pitRings = Mathf.Max(2, radialSegments);
            int segs = Mathf.Max(8, angularSegments);
            int totalRings = pitRings + 1; // 마지막 한 링은 넓은 평지 스커트

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            verts.Add(new Vector3(0f, EvaluateCraterHeight(0f), 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int ring = 1; ring <= totalRings; ring++)
            {
                float radius = ring <= pitRings ? Mathf.Lerp(0f, pitOuterRadius, (float)ring / pitRings) : groundMeshRadius;
                float y = EvaluateCraterHeight(radius);
                for (int seg = 0; seg < segs; seg++)
                {
                    float angle = (float)seg / segs * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle) * radius;
                    float z = Mathf.Sin(angle) * radius;
                    verts.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(0.5f + x / groundMeshRadius * 0.5f, 0.5f + z / groundMeshRadius * 0.5f));
                }
            }

            // 중심 팬 삼각형: 위(+Y)를 향하도록 (0, c, b) 순서로 감는다.
            for (int seg = 0; seg < segs; seg++)
            {
                int a = 0;
                int b = 1 + seg;
                int c = 1 + (seg + 1) % segs;
                tris.Add(a); tris.Add(c); tris.Add(b);
            }

            for (int ring = 1; ring < totalRings; ring++)
            {
                int ringStart = 1 + (ring - 1) * segs;
                int nextRingStart = 1 + ring * segs;
                for (int seg = 0; seg < segs; seg++)
                {
                    int a = ringStart + seg;
                    int b = ringStart + (seg + 1) % segs;
                    int c = nextRingStart + seg;
                    int d = nextRingStart + (seg + 1) % segs;

                    tris.Add(a); tris.Add(b); tris.Add(d);
                    tris.Add(a); tris.Add(d); tris.Add(c);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "QuarryCraterMesh";
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            meshFilter.sharedMesh = mesh;
            meshCollider.sharedMesh = mesh;

            if (craterMaterial != null)
            {
                meshRenderer.sharedMaterial = craterMaterial;
            }
        }

        private void SpawnRingScatter(System.Random rng, Vector3 center, GameObject[] prefabs, int count, float minRadius, float maxRadius, string groupName, Vector2 scaleRange, System.Func<float, float> heightAtRadius)
        {
            if (prefabs == null || prefabs.Length == 0 || count <= 0) return;

            GameObject group = new GameObject(groupName);
            group.transform.SetParent(transform, false);
            generatedGroups.Add(group);

            for (int i = 0; i < count; i++)
            {
                GameObject prefab = prefabs[rng.Next(prefabs.Length)];
                if (prefab == null) continue;

                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float radius = Mathf.Lerp(minRadius, maxRadius, (float)rng.NextDouble());
                float y = heightAtRadius(radius);
                Vector3 pos = new Vector3(center.x + Mathf.Cos(angle) * radius, y, center.z + Mathf.Sin(angle) * radius);
                float yRotation = (float)(rng.NextDouble() * 360.0);

                GameObject instance = Instantiate(prefab, pos, Quaternion.Euler(0f, yRotation, 0f), group.transform);
                float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)rng.NextDouble());
                instance.transform.localScale = prefab.transform.localScale * scale;
            }
        }

        private void PlaceTreasureGroup(System.Random rng, Vector3 center, GameObject[] prefabs, int count, float minRadius, float maxRadius, string groupName)
        {
            if (prefabs == null || prefabs.Length == 0 || count <= 0) return;

            GameObject group = new GameObject(groupName);
            group.transform.SetParent(transform, false);
            generatedGroups.Add(group);

            for (int i = 0; i < count; i++)
            {
                GameObject prefab = prefabs[rng.Next(prefabs.Length)];
                if (prefab == null) continue;

                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float radius = Mathf.Lerp(minRadius, maxRadius, (float)rng.NextDouble());
                float y = EvaluateCraterHeight(radius);
                Vector3 pos = new Vector3(center.x + Mathf.Cos(angle) * radius, y, center.z + Mathf.Sin(angle) * radius);

                Instantiate(prefab, pos, Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f), group.transform);
            }
        }
    }
}
