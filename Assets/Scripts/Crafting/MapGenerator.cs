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
            public int amount;     // 채집 시 지급량

            public ZoneDef(string itemId, string displayName, Color color, int count, float radius, int amount)
            {
                this.itemId = itemId;
                this.displayName = displayName;
                this.color = color;
                this.count = count;
                this.radius = radius;
                this.amount = amount;
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
            new ZoneDef(ItemIds.Wood, "나무 군락지", new Color(0.55f, 0.35f, 0.15f), count: 7, radius: 6f, amount: 10),
            new ZoneDef(ItemIds.Stone, "돌 채석장", new Color(0.6f, 0.6f, 0.62f), count: 7, radius: 6f, amount: 10),
            new ZoneDef(ItemIds.IronOre, "철 매장지", Color.white, count: 4, radius: 3.5f, amount: 6),
            new ZoneDef(ItemIds.Platinum, "백금 매장지", new Color(0.2f, 0.5f, 1f), count: 2, radius: 2f, amount: 3),
            new ZoneDef(ItemIds.PoisonHerb, "독초 군락지", new Color(0.6f, 0.2f, 0.9f), count: 2, radius: 1.5f, amount: 3),
            new ZoneDef(ItemIds.Diamond, "다이아 매장지", Color.yellow, count: 1, radius: 1f, amount: 1),
        };

        private void Start()
        {
            Generate();
        }

        [ContextMenu("Regenerate Map")]
        public void Generate()
        {
            if (_root != null)
                DestroyImmediate(_root.gameObject);

            _root = new GameObject("GeneratedFloor").transform;
            _root.SetParent(transform, false);

            List<ZoneInstance> zones = PickZoneInstances();
            int[,] zoneMap = BuildZoneMap(zones);

            BuildFloorMesh(zoneMap);
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

        /// <summary>
        /// 격자 전체를 잇는 평평한 메쉬 하나를 만든다 — 발판을 따로따로 배치하지 않고
        /// 밟는 모든 위치에 빈틈없이 바닥이 존재하도록 한다.
        /// </summary>
        private void BuildFloorMesh(int[,] zoneMap)
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
                    vertices[i] = new Vector3(originX + x * tileSize, gridCenter.y, originZ + z * tileSize);
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

            // 맵이 기존 PP_Ground보다 커질 수 있으므로, 바닥 메쉬 자체에 충돌체를 붙여
            // 밟는 모든 곳에 실제로 발이 닿도록 보장한다.
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
                pos.y += 0.5f;

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
                resource.Configure(def.displayName, item, def.amount);
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
