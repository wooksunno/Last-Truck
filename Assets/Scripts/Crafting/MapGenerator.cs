using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// template-floor 타일을 격자로 깔아 판게아 형태의 단일 대륙 바닥을 절차적으로 생성한다.
    /// 무작위로 고른 구역(철/다이아/백금/독초)마다 타일 색을 바꾸고 중앙에 채집 노드를 하나씩 배치한다.
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
        [SerializeField] private Mesh floorMesh;
        [SerializeField] private Color baseColor = new Color(0.2f, 0.192f, 0.192f);

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
            if (floorMesh == null)
            {
                Debug.LogWarning("[MapGenerator] floorMesh(template-floor)가 지정되지 않았습니다.");
                return;
            }

            if (_root != null)
                DestroyImmediate(_root.gameObject);

            _root = new GameObject("GeneratedFloor").transform;
            _root.SetParent(transform, false);

            List<Vector2Int> zoneCenters = PickZoneCenters();

            Material baseMat = CreateMaterial(baseColor);
            var zoneMats = new Material[ZoneDefs.Length];
            for (int i = 0; i < ZoneDefs.Length; i++)
                zoneMats[i] = CreateMaterial(ZoneDefs[i].color);

            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    int zoneIndex = FindZoneIndex(x, z, zoneCenters);

                    GameObject tile = new GameObject("Tile_" + x + "_" + z);
                    tile.transform.SetParent(_root, false);
                    tile.transform.position = GridToWorld(x, z);

                    MeshFilter mf = tile.AddComponent<MeshFilter>();
                    mf.sharedMesh = floorMesh;

                    MeshRenderer mr = tile.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = zoneIndex >= 0 ? zoneMats[zoneIndex] : baseMat;
                }
            }

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

        private int FindZoneIndex(int x, int z, List<Vector2Int> zoneCenters)
        {
            Vector2 p = new Vector2(x, z);
            for (int i = 0; i < zoneCenters.Count; i++)
            {
                if (Vector2.Distance(p, zoneCenters[i]) <= zoneRadiusTiles)
                    return i;
            }

            return -1;
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
                mr.sharedMaterial = CreateMaterial(def.color);

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

        private Material CreateMaterial(Color color)
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
