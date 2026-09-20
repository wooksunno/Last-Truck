using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 판게아 형태의 단일 대륙 바닥을 절차적으로 생성한다.
    /// 여기저기 흩어진 개별 발판이 아니라, 격자 전체를 하나로 이어붙인 메쉬 하나를 만들고
    /// 그 위에 구역별 색을 텍스처로 입힌다 — 밟는 모든 곳에 항상 바닥이 존재한다.
    /// 자원마다 구역 개수·범위가 달라서 나무/돌은 넓고 흔하게, 철/백금/독초/다이아는
    /// 점점 좁고 희귀하게 나오며, 구역끼리 겹치는 것도 허용한다.
    /// 매 Start()마다 새로 생성되므로 씬을 다시 플레이할 때마다("매판") 배치가 바뀐다.
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        [System.Serializable]
        private struct ZoneDef
        {
            public string itemId;
            public string displayName;
            public Color color;
            public int count;      // 이 자원의 구역이 맵에 몇 개 생기는지 (많을수록 자주 출몰)
            public float radius;   // 구역 하나의 반경(타일 단위, 클수록 범위가 넓음)
            
            public float pitFlatRadius;  // 채석장 구덩이: 가장 깊은 평지 반경(월드 단위). 0이면 구덩이 없음
            public float pitOuterRadius; // 채석장 구덩이: 이 반경 밖은 원래 바닥 높이(월드 단위)
            public float pitDepth;       // 채석장 구덩이: 바닥보다 얼마나 더 파일지
public int amount;     // 채집 시 지급량
            public int minHits;           // 고갈까지 필요한 채집 횟수(최소). maxHits가 0이면 고갈되지 않음
            public int maxHits;           // 고갈까지 필요한 채집 횟수(최대)
            public float minRegenSeconds; // 고갈 후 리젠까지 걸리는 시간(초, 최소)
            public float maxRegenSeconds; // 고갈 후 리젠까지 걸리는 시간(초, 최대)

            public ZoneDef(string itemId, string displayName, Color color, int count, float radius, int amount,
                float pitFlatRadius = 0f, float pitOuterRadius = 0f, float pitDepth = 0f,
                int minHits = 0, int maxHits = 0, float minRegenSeconds = 0f, float maxRegenSeconds = 0f)
            {
                this.itemId = itemId;
                this.displayName = displayName;
                this.color = color;
                this.count = count;
                this.radius = radius;
                
                this.pitFlatRadius = pitFlatRadius;
                this.pitOuterRadius = pitOuterRadius;
                this.pitDepth = pitDepth;
this.amount = amount;
                this.minHits = minHits;
                this.maxHits = maxHits;
                this.minRegenSeconds = minRegenSeconds;
                this.maxRegenSeconds = maxRegenSeconds;
            }
        }

        private struct ZoneInstance
        {
            public int defIndex;
            public Vector2 center;
            public float radius;
        }

        [Header("Grid")]
        [SerializeField] private int gridWidth = 48;
        [SerializeField] private int gridHeight = 48;
        [SerializeField] private float tileSize = 4f;
        [SerializeField] private Vector3 gridCenter = new Vector3(1.21f, -0.81f, -4.3f);

        [Header("Floor")]
        [SerializeField] private Color baseColor = new Color(0.2f, 0.192f, 0.192f);
        [SerializeField] private int texelsPerCell = 6;

        private Transform _root;

        // 나무/돌 > 철 > 백금 > 독초 > 다이아 순으로 점점 좁고 희귀해진다.
        // count*radius(면적에 비례)가 클수록 자주/넓게 출몰한다는 뜻.
        private static readonly ZoneDef[] ZoneDefs =
        {
            new ZoneDef(ItemIds.Wood, "나무 군락지", new Color(0.55f, 0.35f, 0.15f), count: 7, radius: 6f, amount: 10, minHits: 2, maxHits: 3, minRegenSeconds: 60f, maxRegenSeconds: 120f),
            new ZoneDef(ItemIds.Stone, "돌 채석장", new Color(0.6f, 0.6f, 0.62f), count: 7, radius: 6f, amount: 10, minHits: 2, maxHits: 3, minRegenSeconds: 90f, maxRegenSeconds: 150f),
            new ZoneDef(ItemIds.IronOre, "철 매장지", Color.white, count: 4, radius: 3.5f, amount: 6, minHits: 4, maxHits: 7, minRegenSeconds: 300f, maxRegenSeconds: 600f),
            new ZoneDef(ItemIds.Platinum, "백금 매장지", new Color(0.2f, 0.5f, 1f), count: 2, radius: 2f, amount: 3, pitFlatRadius: 1.5f, pitOuterRadius: 10f, pitDepth: 3f, minHits: 3, maxHits: 4, minRegenSeconds: 420f, maxRegenSeconds: 900f),
            new ZoneDef(ItemIds.PoisonHerb, "독초 군락지", new Color(0.6f, 0.2f, 0.9f), count: 2, radius: 1.5f, amount: 3),
            new ZoneDef(ItemIds.Diamond, "다이아 매장지", Color.yellow, count: 1, radius: 1f, amount: 1, pitFlatRadius: 3f, pitOuterRadius: 20f, pitDepth: 5f, minHits: 5, maxHits: 6, minRegenSeconds: 1200f, maxRegenSeconds: 1800f),
        };

        private void Start()
        {
            Generate();
        }

public void Generate()
        {
            // _root는 직렬화되지 않는 private 필드라 Play 진입 시 도메인 리로드로 null로 리셋될 수 있다.
            // 그러면 이미 씨으로 남아있는 GeneratedFloor를 못 찾아 지우지 못하고 새로 하나 더 만들어 두 개가 겹치는 버그가 생긴다.
            // 그래서 _root 값과 무관하게 이름으로 기존 GeneratedFloor를 모두 찾아 지운다.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "GeneratedFloor")
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            _root = new GameObject("GeneratedFloor").transform;
            _root.SetParent(transform, false);

            List<ZoneInstance> zones = PickZoneInstances();
            int[,] zoneMap = BuildZoneMap(zones);

            BuildFloorMesh(zoneMap, zones);
            SpawnResourceNodes(zones);
        }

        /// <summary>
        /// 자원마다 정해진 개수만큼 무작위 위치에 구역을 뿌린다.
        /// 겹치는 것을 막지 않는다 — 중복 출몰을 허용해서 더 자연스럽게 섞이게 한다.
        /// </summary>
        private List<ZoneInstance> PickZoneInstances()
        {
            var zones = new List<ZoneInstance>();
            var rng = new System.Random();

            for (int d = 0; d < ZoneDefs.Length; d++)
            {
                ZoneDef def = ZoneDefs[d];
                int margin = Mathf.Max(1, Mathf.RoundToInt(def.radius));

                for (int n = 0; n < def.count; n++)
                {
                    float x = rng.Next(margin, Mathf.Max(margin + 1, gridWidth - margin));
                    float z = rng.Next(margin, Mathf.Max(margin + 1, gridHeight - margin));

                    zones.Add(new ZoneInstance
                    {
                        defIndex = d,
                        center = new Vector2(x, z),
                        radius = def.radius,
                    });
                }
            }

            return zones;
        }

        /// <summary>
        /// 각 셀(x, z)이 어느 구역(zoneDef 인덱스)에 속하는지 미리 계산한 표. -1이면 기본 바닥색.
        /// 여러 구역이 겹치면 먼저 나온(=희귀도가 낮은) 자원이 색을 차지한다.
        /// </summary>
        private int[,] BuildZoneMap(List<ZoneInstance> zones)
        {
            var map = new int[gridWidth, gridHeight];
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    map[x, z] = -1;
                    Vector2 p = new Vector2(x, z);
                    for (int i = 0; i < zones.Count; i++)
                    {
                        if (Vector2.Distance(p, zones[i].center) <= zones[i].radius)
                        {
                            map[x, z] = zones[i].defIndex;
                            break;
                        }
                    }
                }
            }

            return map;
        }

private float EvaluatePitOffset(float worldX, float worldZ, List<ZoneInstance> zones)
        {
            float deepest = 0f;
            for (int i = 0; i < zones.Count; i++)
            {
                ZoneDef def = ZoneDefs[zones[i].defIndex];
                if (def.pitOuterRadius <= 0f) continue;

                Vector3 zoneWorld = GridToWorld(zones[i].center.x, zones[i].center.y);
                float dx = worldX - zoneWorld.x;
                float dz = worldZ - zoneWorld.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                if (dist >= def.pitOuterRadius) continue;

                float offset;
                if (dist <= def.pitFlatRadius)
                {
                    offset = -def.pitDepth;
                }
                else
                {
                    float t = (dist - def.pitFlatRadius) / (def.pitOuterRadius - def.pitFlatRadius);
                    float curve = Mathf.SmoothStep(0f, 1f, t);
                    offset = Mathf.Lerp(-def.pitDepth, 0f, curve);
                }

                if (offset < deepest) deepest = offset;
            }

            return deepest;
        }


        /// <summary>
        /// 격자 전체를 잇는 평평한 메쉬 하나를 만든다 — 발판을 따로따로 배치하지 않고
        /// 밟는 모든 위치에 빈틈없이 바닥이 존재하도록 한다.
        /// </summary>
private void BuildFloorMesh(int[,] zoneMap, List<ZoneInstance> zones)
        {
            int vertsX = gridWidth + 1;
            int vertsZ = gridHeight + 1;
            var vertices = new Vector3[vertsX * vertsZ];
            var uvs = new Vector2[vertsX * vertsZ];
            var triangles = new int[gridWidth * gridHeight * 6];

            float originX = gridCenter.x - (gridWidth * tileSize) / 2f;
            float originZ = gridCenter.z - (gridHeight * tileSize) / 2f;

            for (int z = 0; z < vertsZ; z++)
            {
                for (int x = 0; x < vertsX; x++)
                {
                    int i = z * vertsX + x;
                    float worldX = originX + x * tileSize;
                    float worldZ = originZ + z * tileSize;
                    float y = gridCenter.y + EvaluatePitOffset(worldX, worldZ, zones);
                    vertices[i] = new Vector3(worldX, y, worldZ);
                    uvs[i] = new Vector2((float)x / gridWidth, (float)z / gridHeight);
                }
            }

            int t = 0;
            for (int z = 0; z < gridHeight; z++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    int i0 = z * vertsX + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + vertsX;
                    int i3 = i2 + 1;

                    triangles[t++] = i0;
                    triangles[t++] = i2;
                    triangles[t++] = i1;

                    triangles[t++] = i1;
                    triangles[t++] = i2;
                    triangles[t++] = i3;
                }
            }

            var mesh = new Mesh();
            mesh.indexFormat = vertices.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject floorGO = new GameObject("GeneratedFloorMesh");
            floorGO.transform.SetParent(_root, false);

            MeshFilter mf = floorGO.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = floorGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = CreateFloorMaterial(zoneMap);

            MeshCollider mc = floorGO.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
        }

        /// <summary>
        /// 구역별 색을 하나의 텍스처로 구워서, 바닥 메쉬 하나에 통째로 입힌다.
        /// </summary>
        private Material CreateFloorMaterial(int[,] zoneMap)
        {
            int texW = gridWidth * texelsPerCell;
            int texH = gridHeight * texelsPerCell;

            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color[texW * texH];
            for (int z = 0; z < gridHeight; z++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    int zoneIndex = zoneMap[x, z];
                    Color c = zoneIndex >= 0 ? ZoneDefs[zoneIndex].color : baseColor;

                    for (int py = 0; py < texelsPerCell; py++)
                    {
                        int ty = z * texelsPerCell + py;
                        int rowStart = ty * texW + x * texelsPerCell;
                        for (int px = 0; px < texelsPerCell; px++)
                            pixels[rowStart + px] = c;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader);
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);

            return mat;
        }

private void SpawnResourceNodes(List<ZoneInstance> zones)
        {
            ItemCatalog catalog = ItemCatalog.GetOrCreate();

            for (int i = 0; i < zones.Count; i++)
            {
                ZoneDef def = ZoneDefs[zones[i].defIndex];
                ItemData item = catalog.GetItem(def.itemId);

                Vector3 pos = GridToWorld(zones[i].center.x, zones[i].center.y);
                pos.y += EvaluatePitOffset(pos.x, pos.z, zones) + 0.5f;

                GameObject node = GameObject.CreatePrimitive(PrimitiveType.Cube);
                node.name = def.displayName;
                node.transform.SetParent(_root, false);
                node.transform.position = pos;
                node.transform.localScale = Vector3.one * 0.8f;
                node.layer = 31; // PlayerInteract의 interactLayer(31번)와 일치

                MeshRenderer mr = node.GetComponent<MeshRenderer>();
                mr.sharedMaterial = CreateAccentMaterial(def.color);

                Collider col = node.GetComponent<Collider>();
                col.isTrigger = true;

                SpecialResourceNode resource = node.AddComponent<SpecialResourceNode>();
                resource.Configure(def.displayName, item, def.amount, 0, def.minHits, def.maxHits, def.minRegenSeconds, def.maxRegenSeconds);
            }
        }

        private Vector3 GridToWorld(float x, float z)
        {
            float originX = gridCenter.x - (gridWidth * tileSize) / 2f + tileSize / 2f;
            float originZ = gridCenter.z - (gridHeight * tileSize) / 2f + tileSize / 2f;
            return new Vector3(originX + x * tileSize, gridCenter.y, originZ + z * tileSize);
        }

        private Material CreateAccentMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;
            return mat;
        }
    }
}
