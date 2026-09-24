using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 판게아 형태의 단일 대륙 바닥을 절차적으로 생성한다.
    /// 여기저기 흩어진 개별 발판이 아니라, 격자 전체를 하나로 이어붙인 메쉬 하나를 만들고
    /// 그 위에 구역별 색을 텍스처로 입힌다 — 밟는 모든 곳에 항상 바닥이 존재한다.
    /// 나무/돌은 어디서든 한 그루씩 흔하게 발견되면서(ambient) 크게 뭉친 군락지/채석장도 따로 존재하고,
    /// 철/백금/다이아/기름/독가스는 채석장처럼 한 자리에 여러 개가 뭉쳐서 나온다.
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
            public int requiredTier;      // 채집에 필요한 최소 도구 등급. 0 = 맨손 채집 가능
            public string requiredItemId; // 채집에 필요한 특정 장비 아이템 ID(예: 장갑, 수동 펌프). null/빈문자열이면 무시
            public float gatherHoldSeconds; // E키를 꾹 누르고 있어야 하는 시간(초). 0 이하면 즉시 채집
            public string nestedInZoneName; // 비어있지 않으면, 같은 이름을 가진 구역들 중 하나의 안쪽 무작위 지점에만 배치된다(예: "울창한 숲" 안의 독가스)
            public int minNodesPerZone;     // 구역 하나당 실제로 생성되는 채집 오브젝트 개수(최소) — 채석장처럼 뭉쳐 나오게 할 때 사용
            public int maxNodesPerZone;     // 구역 하나당 실제로 생성되는 채집 오브젝트 개수(최대)
            public string commonBonusItemId; // 채집 시 일정 확률로 원래 자원 대신 나오는 흔한 보너스 아이템(예: 철광맥의 돌)
            public float commonBonusChance;
            public string rareBonusItemId;   // 채집 시 낮은 확률로 원래 자원 대신 나오는 희귀 보너스 아이템(예: 철광맥의 백금)
            public float rareBonusChance;

            public ZoneDef(string itemId, string displayName, Color color, int count, float radius, int amount,
                float pitFlatRadius = 0f, float pitOuterRadius = 0f, float pitDepth = 0f,
                int minHits = 0, int maxHits = 0, float minRegenSeconds = 0f, float maxRegenSeconds = 0f,
                int requiredTier = 0, string requiredItemId = null,
                float gatherHoldSeconds = 0f, string nestedInZoneName = null,
                int minNodesPerZone = 1, int maxNodesPerZone = 1,
                string commonBonusItemId = null, float commonBonusChance = 0f,
                string rareBonusItemId = null, float rareBonusChance = 0f)
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
                this.requiredTier = requiredTier;
                this.requiredItemId = requiredItemId;
                this.gatherHoldSeconds = gatherHoldSeconds;
                this.nestedInZoneName = nestedInZoneName;
                this.minNodesPerZone = Mathf.Max(1, minNodesPerZone);
                this.maxNodesPerZone = Mathf.Max(this.minNodesPerZone, maxNodesPerZone);
                this.commonBonusItemId = commonBonusItemId;
                this.commonBonusChance = commonBonusChance;
                this.rareBonusItemId = rareBonusItemId;
                this.rareBonusChance = rareBonusChance;
            }
        }

        private struct ZoneInstance
        {
            public int defIndex;
            public Vector2 center;
            public float radius;
        }

        [Header("Grid")]
        [SerializeField] private int gridWidth = 200;
        [SerializeField] private int gridHeight = 200;
        [SerializeField] private float tileSize = 4f;
        [SerializeField] private Vector3 gridCenter = new Vector3(1.21f, -0.81f, -4.3f);

        [Header("Floor")]
        [SerializeField] private Color baseColor = new Color(0.2f, 0.192f, 0.192f);
        [SerializeField] private int texelsPerCell = 6;

        private Transform _root;

        // 나무/돌은 ambient(어디서나 한 그루씩)+cluster(크게 뭉친 군락지) 이중 분포.
        // 철/백금/다이아/기름/독가스는 채석장처럼 한 자리에 여러 개가 뭉쳐서 나오며,
        // 철 > 백금 > 다이아 순으로 채굴지 수는 줄고 구덩이는 깊어진다.
        private static readonly ZoneDef[] ZoneDefs =
        {
            new ZoneDef(ItemIds.Wood, "나무", new Color(0.4f, 0.25f, 0.1f), count: 60, radius: 1.5f, amount: 10,
                minHits: 2, maxHits: 3, minRegenSeconds: 60f, maxRegenSeconds: 120f, gatherHoldSeconds: 0.5f),
            new ZoneDef(ItemIds.Wood, "울창한 숲", new Color(0.4f, 0.25f, 0.1f), count: 10, radius: 12f, amount: 10,
                minHits: 2, maxHits: 3, minRegenSeconds: 60f, maxRegenSeconds: 120f, gatherHoldSeconds: 0.5f,
                minNodesPerZone: 5, maxNodesPerZone: 9),

            new ZoneDef(ItemIds.Stone, "돌", new Color(0.6f, 0.6f, 0.62f), count: 60, radius: 1.5f, amount: 10,
                minHits: 2, maxHits: 3, minRegenSeconds: 90f, maxRegenSeconds: 150f, requiredTier: 1, gatherHoldSeconds: 0.8f),
            new ZoneDef(ItemIds.Stone, "돌 채석장", new Color(0.6f, 0.6f, 0.62f), count: 10, radius: 12f, amount: 10,
                minHits: 2, maxHits: 3, minRegenSeconds: 90f, maxRegenSeconds: 150f, requiredTier: 1, gatherHoldSeconds: 0.8f,
                minNodesPerZone: 5, maxNodesPerZone: 9),

            new ZoneDef(ItemIds.Herb, "약초 군락지", new Color(0.35f, 0.75f, 0.3f), count: 20, radius: 7f, amount: 4,
                minHits: 2, maxHits: 3, minRegenSeconds: 60f, maxRegenSeconds: 150f, gatherHoldSeconds: 0.5f),

            new ZoneDef(ItemIds.CopperOre, "구리 매장지", new Color(0.95f, 0.5f, 0.2f), count: 20, radius: 7f, amount: 8,
                pitFlatRadius: 1.5f, pitOuterRadius: 10f, pitDepth: 1.5f,
                minHits: 3, maxHits: 5, minRegenSeconds: 180f, maxRegenSeconds: 360f, requiredTier: 2, gatherHoldSeconds: 1.2f),

            // 철 광맥: 채석장처럼 한 자리에 여러 개가 뭉쳐 나오고, 군데군데 흔하게 보이며 깊이는 얕다.
            // 광맥이라 철만 나오지 않고 돌이 섞여 나오고 아주 가끔 백금도 발견된다.
            new ZoneDef(ItemIds.IronOre, "철 광맥", Color.white, count: 10, radius: 8f, amount: 6,
                pitFlatRadius: 1f, pitOuterRadius: 5f, pitDepth: 1f,
                minHits: 4, maxHits: 7, minRegenSeconds: 300f, maxRegenSeconds: 600f, requiredTier: 3, gatherHoldSeconds: 1.8f,
                minNodesPerZone: 4, maxNodesPerZone: 8,
                commonBonusItemId: ItemIds.Stone, commonBonusChance: 0.25f,
                rareBonusItemId: ItemIds.Platinum, rareBonusChance: 0.05f),

            // 백금: 철보다 채굴지 수가 훨씬 적고(더 희귀) 더 깊이 파야 한다.
            new ZoneDef(ItemIds.Platinum, "백금 매장지", Color.yellow, count: 4, radius: 6f, amount: 3,
                pitFlatRadius: 2.5f, pitOuterRadius: 14f, pitDepth: 5f,
                minHits: 3, maxHits: 4, minRegenSeconds: 420f, maxRegenSeconds: 900f, requiredTier: 4, gatherHoldSeconds: 3.5f,
                minNodesPerZone: 3, maxNodesPerZone: 6),

            // 다이아: 가장 드물고 가장 깊다.
            new ZoneDef(ItemIds.Diamond, "다이아 매장지", new Color(0.2f, 0.5f, 1f), count: 2, radius: 5f, amount: 1,
                pitFlatRadius: 3.5f, pitOuterRadius: 20f, pitDepth: 7f,
                minHits: 5, maxHits: 6, minRegenSeconds: 1200f, maxRegenSeconds: 1800f, requiredTier: 5, gatherHoldSeconds: 5f,
                minNodesPerZone: 2, maxNodesPerZone: 4),

            // 기름: 폐건물 지대에서 구리 수동 펌프로만 채집 가능. 지상 폐허라 구덩이는 없다.
            new ZoneDef(ItemIds.Oil, "폐건물 지대", new Color(0.25f, 0.08f, 0.05f), count: 5, radius: 8f, amount: 5,
                minHits: 3, maxHits: 5, minRegenSeconds: 180f, maxRegenSeconds: 360f,
                requiredItemId: ItemIds.CopperHandPump, gatherHoldSeconds: 1.5f,
                minNodesPerZone: 3, maxNodesPerZone: 6),

            // 독가스 발생지: "울창한 숲" 구역 안의 특정 지점에만 나타난다. 장갑 필수, 캐면 즉시 사라진다.
            new ZoneDef(ItemIds.PoisonHerb, "독가스 발생지", new Color(0.6f, 0.2f, 0.9f), count: 8, radius: 2f, amount: 3,
                minHits: 1, maxHits: 1, minRegenSeconds: 120f, maxRegenSeconds: 240f,
                requiredItemId: ItemIds.LeatherGloves, gatherHoldSeconds: 0.6f, nestedInZoneName: "울창한 숲",
                minNodesPerZone: 3, maxNodesPerZone: 5),
        };

        private void Start()
        {
            Generate();
        }

        public void Generate()
        {
            // _root는 직렬화되지 않는 private 필드라 Play 진입 시 도메인 리로드로 null로 리셋될 수 있다.
            // 그러면 이미 씬으로 남아있는 GeneratedFloor를 못 찾아 지우지 못하고 새로 하나 더 만들어 두 개가 겹치는 버그가 생긴다.
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
        /// nestedInZoneName이 설정된 구역은 독립적으로 배치되지 않고, 이미 배치된 같은 이름의
        /// 숙주 구역(예: 울창한 숲) 안쪽 무작위 지점에만 배치된다.
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
                    float x, z;
                    ZoneInstance? host = string.IsNullOrEmpty(def.nestedInZoneName)
                        ? (ZoneInstance?)null
                        : PickNestedHostZone(def.nestedInZoneName, zones, rng);

                    if (host.HasValue)
                    {
                        float offsetRadius = Mathf.Max(0.5f, host.Value.radius * 0.6f);
                        double angle = rng.NextDouble() * System.Math.PI * 2.0;
                        double dist = rng.NextDouble() * offsetRadius;
                        x = Mathf.Clamp((float)(host.Value.center.x + System.Math.Cos(angle) * dist), margin, Mathf.Max(margin, gridWidth - margin));
                        z = Mathf.Clamp((float)(host.Value.center.y + System.Math.Sin(angle) * dist), margin, Mathf.Max(margin, gridHeight - margin));
                    }
                    else
                    {
                        x = rng.Next(margin, Mathf.Max(margin + 1, gridWidth - margin));
                        z = rng.Next(margin, Mathf.Max(margin + 1, gridHeight - margin));
                    }

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

        private static ZoneInstance? PickNestedHostZone(string hostZoneName, List<ZoneInstance> zones, System.Random rng)
        {
            var candidates = new List<int>();
            for (int i = 0; i < zones.Count; i++)
            {
                if (ZoneDefs[zones[i].defIndex].displayName == hostZoneName)
                    candidates.Add(i);
            }

            if (candidates.Count == 0)
                return null;

            return zones[candidates[rng.Next(candidates.Count)]];
        }

        /// <summary>
        /// 각 셀(x, z)이 어느 구역(zoneDef 인덱스)에 속하는지 미리 계산한 표. -1이면 기본 바닥색.
        /// nestedInZoneName이 있는(숙주 구역 안에 박힌) 구역을 먼저 검사해서, 숙주보다 작은
        /// 특정 지점(예: 숲 속 독가스 지대)이 자기 색으로 제대로 표시되도록 한다.
        /// </summary>
        private int[,] BuildZoneMap(List<ZoneInstance> zones)
        {
            var map = new int[gridWidth, gridHeight];

            var nestedIndices = new List<int>();
            var normalIndices = new List<int>();
            for (int i = 0; i < zones.Count; i++)
            {
                if (!string.IsNullOrEmpty(ZoneDefs[zones[i].defIndex].nestedInZoneName))
                    nestedIndices.Add(i);
                else
                    normalIndices.Add(i);
            }

            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    map[x, z] = -1;
                    Vector2 p = new Vector2(x, z);

                    bool found = false;
                    for (int k = 0; k < nestedIndices.Count; k++)
                    {
                        int i = nestedIndices[k];
                        if (Vector2.Distance(p, zones[i].center) <= zones[i].radius)
                        {
                            map[x, z] = zones[i].defIndex;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        for (int k = 0; k < normalIndices.Count; k++)
                        {
                            int i = normalIndices[k];
                            if (Vector2.Distance(p, zones[i].center) <= zones[i].radius)
                            {
                                map[x, z] = zones[i].defIndex;
                                break;
                            }
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
            floorGO.tag = "Ground";

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

        /// <summary>
        /// 구역별로 minNodesPerZone~maxNodesPerZone개의 채집 오브젝트를 구역 반경 안에 흩어 배치한다.
        /// 채석장처럼 여러 개가 뭉쳐서 나오게 하는 핵심 로직.
        /// </summary>
        private void SpawnResourceNodes(List<ZoneInstance> zones)
        {
            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            var rng = new System.Random();

            for (int i = 0; i < zones.Count; i++)
            {
                ZoneDef def = ZoneDefs[zones[i].defIndex];
                ItemData item = catalog.GetItem(def.itemId);
                ItemData commonBonusItem = string.IsNullOrEmpty(def.commonBonusItemId) ? null : catalog.GetItem(def.commonBonusItemId);
                ItemData rareBonusItem = string.IsNullOrEmpty(def.rareBonusItemId) ? null : catalog.GetItem(def.rareBonusItemId);

                int nodeCount = def.maxNodesPerZone > def.minNodesPerZone
                    ? rng.Next(def.minNodesPerZone, def.maxNodesPerZone + 1)
                    : Mathf.Max(1, def.minNodesPerZone);

                for (int n = 0; n < nodeCount; n++)
                {
                    Vector2 gridPos = zones[i].center;
                    if (nodeCount > 1)
                    {
                        float scatterRadius = Mathf.Max(0.5f, zones[i].radius * 0.75f);
                        double angle = rng.NextDouble() * System.Math.PI * 2.0;
                        double dist = rng.NextDouble() * scatterRadius;
                        gridPos = new Vector2(
                            zones[i].center.x + (float)(System.Math.Cos(angle) * dist),
                            zones[i].center.y + (float)(System.Math.Sin(angle) * dist));
                    }

                    Vector3 pos = GridToWorld(gridPos.x, gridPos.y);
                    pos.y += EvaluatePitOffset(pos.x, pos.z, zones) + 0.5f;

                    GameObject node = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    node.name = def.displayName;
                    node.transform.SetParent(_root, false);
                    node.transform.position = pos;
                    node.transform.localScale = Vector3.one * 0.8f;
                    node.layer = 4; // PlayerInteract.interactLayer와 일치(Water 레이어 사용 중, 트럭의 TruckInteractable과 동일)

                    MeshRenderer mr = node.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = CreateAccentMaterial(def.color);

                    Collider col = node.GetComponent<Collider>();
                    col.isTrigger = true;

                    SpecialResourceNode resource = node.AddComponent<SpecialResourceNode>();
                    resource.Configure(def.displayName, item, def.amount, def.requiredTier, def.minHits, def.maxHits,
                        def.minRegenSeconds, def.maxRegenSeconds, def.requiredItemId, def.gatherHoldSeconds,
                        commonBonusItem, def.commonBonusChance, rareBonusItem, def.rareBonusChance);
                }
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
