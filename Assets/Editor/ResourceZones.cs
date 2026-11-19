#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using CraftingSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 맵에 "자원 특화 지역"을 추가한다. 기존 자원 배치는 하나도 지우지 않고, 지역마다 한 가지 자원을 빽빽하게 더 심는다.
///  숲(나무) / 채석장(돌) / 구리 광산 / 철 광산 / 금 광산 / 약초 초원 / 독약초 늪 / 사막(선인장) / 유전(기름) / 다이아 심부 광산.
/// 새 자원은 씬에 이미 있는 같은 종류 자원을 복제해서(크기·채집값·반짝임 그대로) 길찾기 메시(NavMesh) 위 걸을 수 있는 바닥에 놓는다.
/// "ResourceZones" 오브젝트 아래에 모아 두며, 다시 실행하면 지난번에 만든 것을 지우고 새로 만든다.
/// </summary>
public static class ResourceZones
{
    const string GroupName = "ResourceZones";
    const int NodeLayer = 4;

    struct Zone
    {
        public string name, kind;      // kind: 아이템 ID (wood/stone/herb/poison_herb/cactus = 채집 프롭, 나머지 = 광맥 노드)
        public float x, z, radius;
        public int count;
        public float yRef, yTol;       // 이 높이 근처 바닥만 쓴다 (yTol <= 0 이면 기존 같은 자원의 중간 높이를 자동으로)
        public bool lightsOk;          // 반짝이는 불빛이 달린 복제 원본도 허용
        public float minSpacing;
        public Zone(string name, string kind, float x, float z, float radius, int count, float yRef = 0f, float yTol = 0f, bool lights = true, float spacing = 2.6f)
        { this.name = name; this.kind = kind; this.x = x; this.z = z; this.radius = radius; this.count = count; this.yRef = yRef; this.yTol = yTol; lightsOk = lights; minSpacing = spacing; }
    }

    static readonly Zone[] Zones =
    {
        new Zone("숲 (나무)",            "wood",         -330f,   20f, 120f, 90),
        new Zone("채석장 (돌)",          "stone",        -640f,   10f, 100f, 70),
        new Zone("구리 광산",            "copper_ore",     60f, -290f, 110f, 45),
        new Zone("철 광산",              "iron_ore",      -90f, -170f,  90f, 45),
        new Zone("금 광산 (동)",         "platinum_ore",  250f,   50f,  80f, 20),
        new Zone("금 광산 (서)",         "platinum_ore", -555f,  -40f,  70f, 15),
        new Zone("약초 초원",            "herb",           50f,  210f, 110f, 60),
        new Zone("독약초 늪",            "poison_herb",   520f, -130f, 110f, 40),
        new Zone("사막 (선인장)",        "cactus",       -110f,  600f, 130f, 40),
        new Zone("유전 (기름)",          "oil",          -200f,  561f,  90f, 8),
        new Zone("다이아 심부 광산",     "diamond",       275f,  790f, 140f, 60, -60f, 10f),
        new Zone("다이아 중층 광산",     "diamond",        60f,  700f,  90f, 15, -50f, 10f),
    };

    static bool IsStructural(string n) => n.StartsWith("PP_Cliff_Veins") || n.StartsWith("PP_Stone Wall_Veins") || n.StartsWith("PP_Cave_Wall");

    [MenuItem("Tools/Last Truck/Resources: Build Specialty Zones")]
    static void Menu() { Debug.Log("[ResourceZones]\n" + Run()); }

    static string KindOfNode(SpecialResourceNode n) => n.Item != null ? n.Item.itemID : "";

    public static string Run()
    {
        var sb = new StringBuilder();

        // 지난번에 만든 것 지우기
        var old = GameObject.Find(GroupName);
        if (old != null) Object.DestroyImmediate(old);

        // 기존 자원 목록(복제 원본 + 간격 검사용)
        var nodes = new List<SpecialResourceNode>(Object.FindObjectsByType<SpecialResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        var props = new List<GatherableProp>(Object.FindObjectsByType<GatherableProp>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        var occupied = new Dictionary<long, List<Vector3>>();
        foreach (var n in nodes) AddOccupied(occupied, n.transform.position);
        foreach (var p in props) AddOccupied(occupied, p.Root.transform.position);

        // 걸을 수 있는 바닥(NavMesh) 삼각형
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        sb.AppendLine("NavMesh triangles = " + (tri.indices.Length / 3));
        if (tri.indices.Length == 0) return "NavMesh가 없습니다 (먼저 굽기)";

        var group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Resource Zones");

        foreach (Zone z in Zones)
        {
            var zoneGo = new GameObject(z.name);
            zoneGo.transform.SetParent(group.transform, false);

            // 이 지역 안의 바닥 삼각형 (평평한 것만, 높이 조건)
            float yRef = z.yRef, yTol = z.yTol;
            bool autoY = yTol <= 0f;
            var cand = new List<int>();
            var cum = new List<float>();
            float total = 0f;
            var autoYs = new List<float>();
            // 자동 높이: 지역 안 기존 자원의 중간 높이
            if (autoY)
            {
                foreach (var n in nodes) { Vector3 p = n.transform.position; if (Flat(p, z) < z.radius) autoYs.Add(p.y); }
                foreach (var p in props) { Vector3 q = p.Root.transform.position; if (Flat(q, z) < z.radius) autoYs.Add(q.y); }
                autoYs.Sort();
                yRef = autoYs.Count > 0 ? autoYs[autoYs.Count / 2] : 0f;
                yTol = 14f;
            }

            for (int t = 0; t < tri.indices.Length; t += 3)
            {
                Vector3 a = tri.vertices[tri.indices[t]], b = tri.vertices[tri.indices[t + 1]], c = tri.vertices[tri.indices[t + 2]];
                Vector3 m = (a + b + c) / 3f;
                if (Flat(m, z) > z.radius) continue;
                if (Mathf.Abs(m.y - yRef) > yTol) continue;
                Vector3 nrm = Vector3.Cross(b - a, c - a);
                float area = nrm.magnitude * 0.5f;
                if (area < 0.05f || Mathf.Abs(nrm.normalized.y) < 0.8f) continue;
                total += area;
                cand.Add(t);
                cum.Add(total);
            }

            int placed = 0, tries = 0;
            var rng = new System.Random(z.name.GetHashCode() ^ 12345);
            var sources = PickSources(z, nodes, props);
            bool useRay = cand.Count == 0;   // 길찾기 메시가 없는 지역(북쪽 광산/사막 등)은 위에서 아래로 쏘아 바닥을 찾는다
            if (sources.Count == 0)
            {
                sb.AppendLine(z.name + ": 복제 원본이 없어 건너뜀");
                continue;
            }

            while (placed < z.count && tries < z.count * (useRay ? 400 : 60))
            {
                tries++;
                Vector3 pos;
                if (useRay)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f, rad = Mathf.Sqrt((float)rng.NextDouble()) * z.radius;
                    float sx = z.x + Mathf.Cos(ang) * rad, sz = z.z + Mathf.Sin(ang) * rad;
                    bool deep = z.yTol > 0f;
                    float startY = deep ? z.yRef + z.yTol + 2f : yRef + 60f;      // 광산은 천장 아래(동굴 안)에서, 지상은 높은 곳에서 쏜다
                    if (!Physics.Raycast(new Vector3(sx, startY, sz), Vector3.down, out RaycastHit hit, deep ? 30f : 120f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < 0.85f) continue;
                    if (Mathf.Abs(hit.point.y - yRef) > yTol + 4f) continue;
                    if (Physics.Raycast(hit.point + Vector3.up * 0.3f, Vector3.up, 2.5f, ~0, QueryTriggerInteraction.Ignore)) continue;   // 머리 위가 막힌 곳(바위 밑)은 제외
                    if (Physics.CheckSphere(hit.point + Vector3.up * 0.9f, 0.45f, ~0, QueryTriggerInteraction.Ignore)) continue;           // 다른 물체 속
                    pos = hit.point;
                }
                else
                {
                    // 면적 가중 무작위 삼각형 → 삼각형 안 무작위 점
                    float r = (float)rng.NextDouble() * total;
                    int lo = 0, hi = cum.Count - 1;
                    while (lo < hi) { int mid = (lo + hi) / 2; if (cum[mid] < r) lo = mid + 1; else hi = mid; }
                    int ti = cand[lo];
                    Vector3 a = tri.vertices[tri.indices[ti]], b = tri.vertices[tri.indices[ti + 1]], c = tri.vertices[tri.indices[ti + 2]];
                    float u = (float)rng.NextDouble(), v = (float)rng.NextDouble();
                    if (u + v > 1f) { u = 1f - u; v = 1f - v; }
                    pos = a + (b - a) * u + (c - a) * v;
                }
                if (Flat(pos, z) > z.radius) continue;
                if (TooClose(occupied, pos, z.minSpacing)) continue;

                GameObject src = sources[rng.Next(sources.Count)];
                var clone = (GameObject)Object.Instantiate(src, zoneGo.transform, true);
                clone.name = src.name + "_zone";
                clone.SetActive(true);
                clone.transform.position = pos;
                clone.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                // 원본을 복제하면 부모 변환이 달라 크기가 변할 수 있다 → 원본 크기 유지
                clone.transform.localScale = src.transform.lossyScale;
                AddOccupied(occupied, pos);
                placed++;
            }

            sb.AppendLine(z.name + " [" + z.kind + "]: " + placed + " / " + z.count + " 심음 (" + (useRay ? "레이캐스트 바닥" : "길찾기 바닥 " + cand.Count + "곳") + ", 원본 " + sources.Count + "종, 기준 높이 " + yRef.ToString("0") + "±" + yTol.ToString("0") + ")");
        }

        EditorSceneManagerMarkDirty();
        return sb.ToString();
    }

    static void EditorSceneManagerMarkDirty() { UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty(); }

    static float Flat(Vector3 p, Zone z) { float dx = p.x - z.x, dz = p.z - z.z; return Mathf.Sqrt(dx * dx + dz * dz); }

    // 복제 원본: 같은 종류 자원 중 작은 단독 모델만 (절벽/동굴 벽 같은 구조물은 제외)
    static List<GameObject> PickSources(Zone z, List<SpecialResourceNode> nodes, List<GatherableProp> props)
    {
        var list = new List<GameObject>();
        bool isProp = z.kind == "wood" || z.kind == "stone" || z.kind == "herb" || z.kind == "poison_herb" || z.kind == "cactus";
        if (isProp)
        {
            foreach (var p in props)
            {
                if (p == null || p.ItemId != z.kind || p.IsDone) continue;
                GameObject root = p.Root;
                if (root == null || !root.scene.IsValid()) continue;
                if (root.transform.childCount > 40) continue;   // 통째로 복제하기엔 너무 큰 묶음
                list.Add(root);
            }
            return list;
        }

        foreach (var n in nodes)
        {
            if (n == null || KindOfNode(n) != z.kind) continue;
            if (IsStructural(n.name)) continue;
            if (n.transform.parent != null && n.transform.parent.name.StartsWith("PP_Treasure")) continue;
            var rends = n.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) continue;
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (size > 6f || size < 0.4f) continue;
            if (!z.lightsOk && n.GetComponentInChildren<Light>(true) != null) continue;
            list.Add(n.gameObject);
        }
        return list;
    }

    static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

    static void AddOccupied(Dictionary<long, List<Vector3>> map, Vector3 p)
    {
        long k = Key(Mathf.FloorToInt(p.x / 4f), Mathf.FloorToInt(p.z / 4f));
        if (!map.TryGetValue(k, out var l)) { l = new List<Vector3>(); map[k] = l; }
        l.Add(p);
    }

    static bool TooClose(Dictionary<long, List<Vector3>> map, Vector3 p, float spacing)
    {
        int cx = Mathf.FloorToInt(p.x / 4f), cz = Mathf.FloorToInt(p.z / 4f);
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                if (map.TryGetValue(Key(cx + dx, cz + dz), out var l))
                    foreach (var q in l)
                    {
                        float ex = q.x - p.x, ez = q.z - p.z;
                        if (ex * ex + ez * ez < spacing * spacing && Mathf.Abs(q.y - p.y) < 6f) return true;
                    }
        return false;
    }
}
#endif
