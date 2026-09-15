using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 판게아 형태의 단일 대륙 바닥을 절차적으로 생성한다.
    /// 여기저기 흩어진 개별 발판이 아니라, 격자 전체를 하나로 이어붙인 메쉬 하나를 만들고
    /// 그 위에 구역별 색을 텍스처로 입힌다 — 밟는 모든 곳에 항상 바닥이 존재한다.
    /// 무작위로 고른 구역(철/다이아/백금/독초)마다 색을 다르게 칠하고 중앙에 채집 노드를 하나씩 배치한다.
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

            public ZoneDef(string itemId, string displayName, Color color)
            {
                this.itemId = itemId;
                this.displayName = displayName;
                this.color = color;
            }
        }

        [Header("Grid")]
        [SerializeField] private int gridWidth = 20;
        [SerializeField] private int gridHeight = 20;
        [SerializeField] private float tileSize = 4f;
        [SerializeField] private Vector3 gridCenter = new Vector3(1.21f, -0.81f, -4.3f);

        [Header("Floor")]
        [SerializeField] private Color baseColor = new Color(0.2f, 0.192f, 0.192f);
        [SerializeField] private int texelsPerCell = 8;

        [Header("Zones")]
        [SerializeField] private int zoneRadiusTiles = 2;
        [SerializeField] private int nodeItemAmount = 5;

        private Transform _root;

        private static readonly ZoneDef[] ZoneDefs =
        {
            new ZoneDef(ItemIds.IronOre, "철 매장지", Color.white),
            new ZoneDef(ItemIds.Diamond, "다이아 매장지", Color.yellow),
            new ZoneDef(ItemIds.Platinum, "백금 매장지", new Color(0.2f, 0.5f, 1f)),
            new ZoneDef(ItemIds.PoisonHerb, "독초 군락지", new Color(0.6f, 0.2f, 0.9f)),
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

            List<Vector2Int> zoneCenters = PickZoneCenters();
            int[,] zoneMap = BuildZoneMap(zoneCenters);

            BuildFloorMesh(zoneMap);
            SpawnResourceNodes(zoneCenters);
        }

        private List<Vector2Int> PickZoneCenters()
        {
            var centers = new List<Vector2Int>();
            var rng = new System.Random();
            int minGap = zoneRadiusTiles * 2 + 2;

            for (int i = 0; i < ZoneDefs.Length; i++)
            {
                Vector2Int candidate = new Vector2Int(gridWidth / 2, gridHeight / 2);
                int attempts = 0;
                bool ok = false;
                while (attempts < 60 && !ok)
                {
                    candidate = new Vector2Int(
                        rng.Next(zoneRadiusTiles, Mathf.Max(zoneRadiusTiles + 1, gridWidth - zoneRadiusTiles)),
                        rng.Next(zoneRadiusTiles, Mathf.Max(zoneRadiusTiles + 1, gridHeight - zoneRadiusTiles)));

                    ok = true;
                    foreach (Vector2Int existing in centers)
                    {
                        if (Vector2.Distance(candidate, existing) < minGap)
                        {
                            ok = false;
                            break;
                        }
                    }

                    attempts++;
                }

                centers.Add(candidate);
            }

            return centers;
        }

        /// <summary>
        /// 각 셀(x, z)이 어느 구역에 속하는지 미리 계산한 표. -1이면 기본 바닥색.
        /// </summary>
        private int[,] BuildZoneMap(List<Vector2Int> zoneCenters)
        {
            var map = new int[gridWidth, gridHeight];
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    map[x, z] = -1;
                    Vector2 p = new Vector2(x, z);
                    for (int i = 0; i < zoneCenters.Count; i++)
                    {
                        if (Vector2.Distance(p, zoneCenters[i]) <= zoneRadiusTiles)
                        {
                            map[x, z] = i;
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

        private void SpawnResourceNodes(List<Vector2Int> zoneCenters)
        {
            ItemCatalog catalog = ItemCatalog.GetOrCreate();

            for (int i = 0; i < zoneCenters.Count; i++)
            {
                ZoneDef def = ZoneDefs[i];
                ItemData item = catalog.GetItem(def.itemId);

                Vector3 pos = GridToWorld(zoneCenters[i].x, zoneCenters[i].y);
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
                resource.Configure(def.displayName, item, nodeItemAmount);
            }
        }

        private Vector3 GridToWorld(int x, int z)
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
