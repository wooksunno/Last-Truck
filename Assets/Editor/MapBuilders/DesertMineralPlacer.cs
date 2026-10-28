#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using CraftingSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 사막(08_Desert) + 채석장 평지(09_Quarry)를 사막답게 정리한다.
///  1) 채석장 가장자리 판석(RimSlabs, PP_Cliff)과 그 위에 얹혀 있던 장식을 지운다.
///  2) 분홍 나무(PinkTrees)를 지우고, 그 자리와 사막 곳곳에 채집 가능한 광맥 바위(구리/철/금)를 놓는다.
///  광맥 노드 구조/수치는 팀 노드(HallOres)와 같다: "<이름>_node"(Water 레이어 + 트리거 구 + SpecialResourceNode) 아래 바위 모델.
///  구리는 OreCrystalConverter의 붉은 구리 재질을 입힌다(PurePoly Copper가 금색과 구분이 안 돼서).
/// </summary>
public static class DesertMineralPlacer
{
    const string Dir = "Assets/PurePoly/Mining_Pack/Prefabs/Ores and Crystals/";
    const int NodeLayer = 4;   // Water = PlayerInteract.interactLayer

    public static int DesertCount = 55;       // 사막에 새로 흩뿌릴 개수
    public static float MinSpacing = 7f;      // 광맥끼리 / 기존 채집 노드와의 최소 간격
    public static float MaxSlope = 25f;

    struct Res { public string id, label, color; public int tier, amount, minHits, maxHits; public float hold, minRegen, maxRegen; }
    static readonly Res Copper = new Res { id = "copper_ore", label = "구리 광맥", color = "Copper", tier = 2, amount = 8, minHits = 3, maxHits = 5, hold = 1.2f, minRegen = 180, maxRegen = 360 };
    static readonly Res Iron = new Res { id = "iron_ore", label = "철 광맥", color = "Iron", tier = 3, amount = 6, minHits = 4, maxHits = 7, hold = 1.8f, minRegen = 300, maxRegen = 600 };
    static readonly Res Gold = new Res { id = "platinum_ore", label = "금 광맥", color = "Gold", tier = 4, amount = 3, minHits = 3, maxHits = 4, hold = 3.5f, minRegen = 420, maxRegen = 900 };

    // 사막 분위기: 붉은 구리 바위가 가장 흔하고, 금맥이 숲/동굴보다 조금 더 흔하다
    static Res Pick(float r) => r < 0.45f ? Copper : r < 0.75f ? Iron : Gold;

    // 걸어 다닐 수 있는 영역 (각 지역 경계 벽 안쪽). 채석장 구덩이 안쪽은 제외.
    static readonly Rect DesertArea = Rect.MinMaxRect(-512f, 596f, -198f, 866f);
    static readonly Rect QuarryArea = Rect.MinMaxRect(-188f, 572f, 240f, 912f);
    static readonly Rect QuarryPit = Rect.MinMaxRect(-156f, 597f, 157f, 887f);

    static bool IsGround(Collider c) => c.name.StartsWith("PP_Ground") || c.name.StartsWith("Dune") || c.name.StartsWith("DesertTile");

    static bool TryGroundAt(Vector2 xz, out RaycastHit ground)
    {
        ground = default;
        if (!Physics.Raycast(new Vector3(xz.x, 120f, xz.y), Vector3.down, out ground, 240f, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (!IsGround(ground.collider)) return false;                              // 첫 히트가 바닥(모래 타일)이어야 함 = 위에 다른 물체 없음
        if (Vector3.Angle(ground.normal, Vector3.up) > MaxSlope) return false;
        // 주변 2.5m에 바닥이 아닌 물체(철로, 폐허, 기름밭, 바위 등)가 있으면 피한다.
        // 우리가 놓는 광맥끼리의 간격은 따로 계산하므로(무리 지어 놓기 위해) 광맥 묶음 안의 것은 무시한다.
        foreach (var c in Physics.OverlapSphere(ground.point + Vector3.up * 2.2f, 2.5f, ~0, _ignoreRoots != null ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide))
        {
            if (IsGround(c)) continue;
            // 자연 배치 중에는 자갈/풀 같은 작은 장식(1.4m 미만)은 무시한다 (울타리·비계·벽 같은 큰 것만 피한다)
            if (_ignoreRoots != null) { var cb = c.bounds; if (Mathf.Max(cb.size.x, Mathf.Max(cb.size.y, cb.size.z)) < 1.4f) continue; }
            bool ours = false;
            if (_ignoreRoots != null) foreach (var root in _ignoreRoots) if (root != null && c.transform.IsChildOf(root)) ours = true;
            if (!ours) return false;
        }
        return true;
    }

    static float Hash01(float a, float b, int salt)
    {
        unchecked
        {
            int h = Mathf.RoundToInt(a * 10f) * 73856093 ^ Mathf.RoundToInt(b * 10f) * 19349663 ^ salt * 2654435;
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0x7fffffff) % 10000 / 10000f;
        }
    }

    static GameObject MakeMineral(Transform parent, Vector3 pos, Res r, int seed) => MakeMineral(parent, pos, r, seed, 1f, 0f, 0f);

    /// <summary>sizeMul: 크기 배율(0.45~1.7). 클수록 한 번에 더 많이/여러 번 캔다. tiltX/Z: 자연스럽게 살짝 기울임.</summary>
    static GameObject MakeMineral(Transform parent, Vector3 pos, Res r, int seed, float sizeMul, float tiltX, float tiltZ)
    {
        string prefabName = $"PP_Rock_Veins_0{1 + Mathf.Abs(seed) % 3}_{r.color}";
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + prefabName + ".prefab");
        if (src == null) return null;

        var node = new GameObject(prefabName + "_node");
        Undo.RegisterCreatedObjectUndo(node, "Desert mineral");
        node.layer = NodeLayer;
        node.transform.SetParent(parent, false);
        node.transform.position = pos;

        var model = (GameObject)PrefabUtility.InstantiatePrefab(src, node.transform);
        float h = Mathf.Lerp(2.2f, 3.6f, Hash01(pos.x, pos.z, 5)) * sizeMul;
        var mr = model.GetComponentInChildren<Renderer>();
        float native = mr != null ? Mathf.Max(0.1f, mr.bounds.size.y) : 1f;
        model.transform.localScale = Vector3.one * (h / native);
        model.transform.localRotation = Quaternion.Euler(tiltX, Hash01(pos.x, pos.z, 9) * 360f, tiltZ);
        model.transform.localPosition = new Vector3(0f, -0.15f, 0f);   // 모래에 살짝 묻힌다
        foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        if (r.id == "copper_ore") OreCrystalConverter.ApplyCopperMaterial(model.transform);

        Bounds b = new Bounds(pos, Vector3.zero);
        foreach (var rr in model.GetComponentsInChildren<Renderer>()) b.Encapsulate(rr.bounds);
        var sc = node.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = Mathf.Max(b.extents.x, b.extents.z) + 1.2f;
        sc.center = new Vector3(0f, h * 0.35f, 0f);

        // 크기에 따라 채집량/횟수: 작은 돌은 조금, 큰 바위는 많이
        float k = Mathf.Clamp(sizeMul, 0.45f, 1.7f);
        int amount = Mathf.Max(1, Mathf.RoundToInt(r.amount * Mathf.Lerp(0.5f, 1.5f, Mathf.InverseLerp(0.45f, 1.7f, k))));
        int minHits = Mathf.Max(1, Mathf.RoundToInt(r.minHits * Mathf.Min(1f, k)));
        int maxHits = Mathf.Max(minHits, Mathf.RoundToInt(r.maxHits * Mathf.Max(0.6f, k)));
        var srn = node.AddComponent<SpecialResourceNode>();
        srn.Configure(r.label, ItemCatalog.GetOrCreate().GetItem(r.id), amount, r.tier, minHits, maxHits, r.minRegen, r.maxRegen, "", r.hold);
        EditorUtility.SetDirty(srn);
        return node;
    }

    static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
    static readonly Vector2Int[] Dirs4 = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

    /// <summary>
    /// 채석장/사막 경계 안에서 1m 간격으로 "아래에 아무것도 없는 곳"을 찾아, 주변 바닥 높이·재질의 판(PP_Ground_01)으로 메운다.
    /// 경계 벽에 붙은 3m 미만 띠는 캐릭터가 빠질 수 없으므로 건너뛴다.
    /// </summary>
    static string FillVoids(Transform quarry)
    {
        var area = new RectInt(-200, 565, 445, 351);   // x -200~245, z 565~916
        var pitRect = Rect.MinMaxRect(-145f, 606f, 146f, 876f);
        var voids = new HashSet<long>();
        for (int x = area.xMin; x <= area.xMax; x++)
            for (int z = area.yMin; z <= area.yMax; z++)
                if (!Physics.Raycast(new Vector3(x + 0.5f, 120f, z + 0.5f), Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore))
                    voids.Add(Key(x, z));

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PurePoly/Mining_Pack/Prefabs/Environment/PP_Ground_01.prefab");
        var tiles = quarry.Find("PlateauTiles");
        if (prefab == null || tiles == null) return "FillVoids: ground prefab / PlateauTiles missing";

        var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Bounds pb = probe.GetComponentInChildren<Renderer>().bounds;
        float topOffset = pb.max.y - probe.transform.position.y;
        Object.DestroyImmediate(probe);

        Transform parent = tiles.Find("GapPatches");
        int pieces = 0, regionsDone = 0;
        var seen = new HashSet<long>();
        foreach (long start in voids)
        {
            if (!seen.Add(start)) continue;
            var cells = new List<Vector2Int>();
            var queue = new Queue<long>(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                long c = queue.Dequeue(); int cx = (int)(c >> 32), cz = (int)(uint)(c & 0xffffffff);
                cells.Add(new Vector2Int(cx, cz));
                foreach (var d in Dirs4) { long n = Key(cx + d.x, cz + d.y); if (voids.Contains(n) && seen.Add(n)) queue.Enqueue(n); }
            }
            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
            foreach (var c in cells) { minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); minZ = Mathf.Min(minZ, c.y); maxZ = Mathf.Max(maxZ, c.y); }
            if (maxX < -192) continue;                                         // 사막 남쪽 벽 바깥
            if (maxX - minX + 1 < 3 || maxZ - minZ + 1 < 3) continue;          // 벽에 붙은 좁은 띠

            // 둘레 바닥 높이와 가장 가까운 바닥 타일 재질
            var heights = new List<float>(); Renderer near = null; float nearD = float.MaxValue;
            for (int x = minX - 2; x <= maxX + 2; x++)
                for (int z = minZ - 2; z <= maxZ + 2; z++)
                {
                    if (voids.Contains(Key(x, z))) continue;
                    bool border = false; foreach (var d in Dirs4) if (voids.Contains(Key(x + d.x, z + d.y))) border = true;
                    if (!border) continue;
                    if (!Physics.Raycast(new Vector3(x + 0.5f, 120f, z + 0.5f), Vector3.down, out RaycastHit h, 300f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!h.collider.name.StartsWith("PP_Ground")) continue;   // 벽/바위/산 꼭대기 높이는 쓰지 않는다
                    heights.Add(h.point.y);
                    float dist = Mathf.Abs(x - (minX + maxX) * 0.5f) + Mathf.Abs(z - (minZ + maxZ) * 0.5f);
                    var rr = h.collider.GetComponent<Renderer>();
                    if (rr != null && dist < nearD) { nearD = dist; near = rr; }
                }
            if (heights.Count == 0) continue;
            heights.Sort();
            bool insidePit = pitRect.Contains(new Vector2(minX, minZ)) && pitRect.Contains(new Vector2(maxX, maxZ));
            float topY = insidePit ? heights[heights.Count / 2] : heights[heights.Count - 1];

            if (parent == null)
            {
                var pg = new GameObject("GapPatches"); Undo.RegisterCreatedObjectUndo(pg, "Desert patch");
                pg.transform.SetParent(tiles, false); parent = pg.transform;
            }

            // 이어진 칸을 직사각형으로 합쳐서 판을 깐다
            var set = new HashSet<long>(); foreach (var c in cells) set.Add(Key(c.x, c.y));
            var used = new HashSet<long>();
            foreach (var c in cells)
            {
                if (used.Contains(Key(c.x, c.y))) continue;
                int x2 = c.x; while (set.Contains(Key(x2 + 1, c.y)) && !used.Contains(Key(x2 + 1, c.y))) x2++;
                int z2 = c.y;
                for (int guard = 0; guard < 1000; guard++)
                {
                    bool ok = true;
                    for (int x = c.x; x <= x2; x++) if (!set.Contains(Key(x, z2 + 1)) || used.Contains(Key(x, z2 + 1))) { ok = false; break; }
                    if (!ok) break; z2++;
                }
                for (int x = c.x; x <= x2; x++) for (int z = c.y; z <= z2; z++) used.Add(Key(x, z));

                var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                Undo.RegisterCreatedObjectUndo(g, "Desert patch");
                g.name = "GapPatch";
                g.transform.localScale = new Vector3((x2 - c.x + 2f) / pb.size.x, 1f, (z2 - c.y + 2f) / pb.size.z);   // 0.5m씩 겹치게
                g.transform.position = new Vector3((c.x + x2 + 1) * 0.5f, topY - topOffset - 0.02f, (c.y + z2 + 1) * 0.5f);
                if (near != null) foreach (var r in g.GetComponentsInChildren<Renderer>()) r.sharedMaterials = near.sharedMaterials;
                pieces++;
            }
            regionsDone++;
        }
        return $"filled holes: regions={regionsDone} patches={pieces}";
    }

    public static string Run()
    {
        var sb = new StringBuilder();
        var desert = GameObject.Find("Map/08_Desert");
        var quarry = GameObject.Find("Map/09_Quarry");
        if (desert == null || quarry == null) return "08_Desert / 09_Quarry not found";

        // 1) 판석 + 그 위에 얹힌 장식 지우기
        var rim = quarry.transform.Find("RimSlabs");
        int slabs = 0, onTop = 0;
        if (rim != null)
        {
            var slabCols = new HashSet<Collider>(rim.GetComponentsInChildren<Collider>(true));
            var toDelete = new HashSet<GameObject>();
            // 판석 위 "소품"만 지운다. 바닥 타일/절벽/산/철로/폐허 등 지형 묶음은 절대 건드리지 않는다.
            var propGroups = new List<Transform>();
            foreach (var root in new[] { desert.transform, quarry.transform })
                foreach (var g in new[] { "Decor", "Rocks", "Camps" })
                    if (root.Find(g) != null) propGroups.Add(root.Find(g));
            foreach (var root in propGroups)
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.transform.IsChildOf(rim)) continue;
                    if (r.name.StartsWith("PP_Ground") || r.name.StartsWith("PP_Cliff") || r.name.StartsWith("PP_Mountain") || r.name.StartsWith("PP_Cave_Wall")) continue;
                    Bounds b = r.bounds;
                    if (!Physics.Raycast(new Vector3(b.center.x, b.min.y + 0.3f, b.center.z), Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!slabCols.Contains(hit.collider)) continue;
                    // 프리팹 루트(또는 이 그룹 바로 아래 오브젝트) 단위로 지운다
                    Transform t = r.transform;
                    while (t.parent != null && t.parent != root) t = t.parent;
                    var victim = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject) ?? t.gameObject;
                    if (victim.GetComponentsInChildren<Renderer>(true).Length > 30) continue;   // 큰 묶음(동굴 등)은 절대 지우지 않는다
                    toDelete.Add(victim);
                }
            foreach (var g in toDelete) if (g != null) { Undo.DestroyObjectImmediate(g); onTop++; }
            slabs = rim.childCount;
            Undo.DestroyObjectImmediate(rim.gameObject);
        }
        sb.AppendLine($"removed RimSlabs={slabs}, objects resting on them={onTop}");

        // 2) 분홍 나무 지우고 그 자리 기록
        var treeSpots = new List<Vector3>();
        var pink = quarry.transform.Find("PinkTrees");
        if (pink != null)
        {
            foreach (Transform t in pink) treeSpots.Add(t.position);
            sb.AppendLine("removed PinkTrees=" + pink.childCount);
            Undo.DestroyObjectImmediate(pink.gameObject);
        }
        Physics.SyncTransforms();   // 지운 뒤 물리 정보를 갱신해야 아래 바닥 검사가 맞다

        // 2-1) 판석만 덮고 있던 구멍(아래에 아무것도 없는 곳)을 주변 바닥 높이/재질의 판으로 메운다
        sb.AppendLine(FillVoids(quarry.transform));
        Physics.SyncTransforms();

        // 3) 광맥 배치
        var taken = new List<Vector3>();
        foreach (var n in Object.FindObjectsByType<SpecialResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None)) taken.Add(n.transform.position);
        bool Free(Vector3 p) { foreach (var q in taken) if ((new Vector2(q.x - p.x, q.z - p.z)).sqrMagnitude < MinSpacing * MinSpacing) return false; return true; }

        Transform Group(GameObject owner)
        {
            var g = owner.transform.Find("DesertMinerals");
            if (g != null) return g;
            var go = new GameObject("DesertMinerals"); Undo.RegisterCreatedObjectUndo(go, "Desert mineral");
            go.transform.SetParent(owner.transform, false);
            return go.transform;
        }
        var desertGroup = Group(desert); var quarryGroup = Group(quarry);
        var counts = new Dictionary<string, int>();
        void Place(Vector2 xz, Transform parent, int salt)
        {
            if (!TryGroundAt(xz, out RaycastHit g)) return;
            if (!Free(g.point)) return;
            var r = Pick(Hash01(g.point.x, g.point.z, salt));
            if (MakeMineral(parent, g.point, r, Mathf.RoundToInt(g.point.x * 13f + g.point.z * 7f)) == null) return;
            taken.Add(g.point);
            string k = parent.parent.name + " " + r.id; counts[k] = counts.TryGetValue(k, out int c) ? c + 1 : 1;
        }

        // 사막에 고르게 흩뿌리기 (고정 시드). 채석장은 아래 깊이 기준 배치로 따로 한다.
        void Scatter(Rect area, int want, Transform parent, int salt, bool avoidPit)
        {
            int placedHere = 0;
            for (int i = 0; i < want * 40 && placedHere < want; i++)
            {
                float x = Mathf.Lerp(area.xMin, area.xMax, Hash01(i, salt, 1));
                float z = Mathf.Lerp(area.yMin, area.yMax, Hash01(salt, i, 2));
                if (avoidPit && QuarryPit.Contains(new Vector2(x, z))) continue;
                int before = taken.Count;
                Place(new Vector2(x, z), parent, salt);
                if (taken.Count > before) placedHere++;
            }
        }
        // 사막 + 채석장: 시작 지점 숲 밀도 기준의 자연스러운 무리 배치 (채석장은 깊을수록 많게)
        sb.AppendLine(NaturalLayout());
        return sb.ToString();
    }

    // ───────────── 채석장: 깊을수록 광물이 많게 ─────────────
    static readonly Res Diamond = new Res { id = "diamond", label = "다이아몬드 원석", color = "Blue", tier = 5, amount = 1, minHits = 5, maxHits = 6, hold = 5f, minRegen = 1200, maxRegen = 1800 };
    static readonly Rect PitArea = Rect.MinMaxRect(-150f, 600f, 152f, 884f);
    public static int PlateauKeep = 10;          // 맨 위 평지에는 구리만 조금
    public static float PitBottomDepth = 55f;   // 평지(1.5)에서 구덩이 바닥(-53)까지

    // 깊이 비율 t(0=평지, 1=바닥)에 따른 광물 종류: 얕으면 구리, 중간은 철, 깊으면 금/다이아
    static Res PickByDepth(float t, float r)
    {
        if (t < 0.3f) return r < 0.7f ? Copper : Iron;
        if (t < 0.6f) return r < 0.35f ? Copper : r < 0.8f ? Iron : Gold;
        return r < 0.2f ? Copper : r < 0.65f ? Iron : r < 0.92f ? Gold : Diamond;
    }

    /// <summary>
    /// 채석장 광맥을 깊이 기준으로 다시 배치한다.
    /// 평지는 구리 몇 개만, 구덩이는 깊을수록 촘촘하고(간격 14m→7m) 좋은 광물(금/다이아)이 많다.
    /// (1회 채집량도 SpecialResourceNode의 깊이 보너스로 깊을수록 늘어난다)
    /// </summary>
    public static string RebalanceQuarryByDepth()
    {
        var quarry = GameObject.Find("Map/09_Quarry");
        if (quarry == null) return "09_Quarry not found";
        var group = quarry.transform.Find("DesertMinerals");
        int removed = 0;
        if (group != null)
            for (int i = group.childCount - 1; i >= 0; i--) { Undo.DestroyObjectImmediate(group.GetChild(i).gameObject); removed++; }
        else
        {
            var go = new GameObject("DesertMinerals"); Undo.RegisterCreatedObjectUndo(go, "Desert mineral");
            go.transform.SetParent(quarry.transform, false); group = go.transform;
        }
        Physics.SyncTransforms();

        var taken = new List<Vector3>();
        foreach (var n in Object.FindObjectsByType<SpecialResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None)) taken.Add(n.transform.position);
        bool FarFrom(Vector3 p, float spacing) { foreach (var q in taken) if ((new Vector2(q.x - p.x, q.z - p.z)).sqrMagnitude < spacing * spacing && Mathf.Abs(q.y - p.y) < 6f) return false; return true; }

        var counts = new SortedDictionary<string, int>();
        void Count(string k) { counts[k] = counts.TryGetValue(k, out int c) ? c + 1 : 1; }

        // 평지: 구리만 드문드문
        int plateau = 0;
        for (int i = 0; i < 4000 && plateau < PlateauKeep; i++)
        {
            float x = Mathf.Lerp(QuarryArea.xMin, QuarryArea.xMax, Hash01(i, 71, 1));
            float z = Mathf.Lerp(QuarryArea.yMin, QuarryArea.yMax, Hash01(71, i, 2));
            if (QuarryPit.Contains(new Vector2(x, z))) continue;
            if (!TryGroundAt(new Vector2(x, z), out RaycastHit g) || g.point.y < -3f || !FarFrom(g.point, 30f)) continue;
            if (MakeMineral(group, g.point, Copper, Mathf.RoundToInt(g.point.x * 13f + g.point.z * 7f)) == null) continue;
            taken.Add(g.point); plateau++; Count("plateau copper_ore");
        }

        // 구덩이: 깊을수록 촘촘하게
        for (float x = PitArea.xMin; x <= PitArea.xMax; x += 2.5f)
            for (float z = PitArea.yMin; z <= PitArea.yMax; z += 2.5f)
            {
                Vector2 p = new Vector2(x + (Hash01(x, z, 3) - 0.5f) * 2f, z + (Hash01(z, x, 4) - 0.5f) * 2f);
                if (!TryGroundAt(p, out RaycastHit g) || g.point.y > -3f) continue;   // 구덩이 안 바닥만
                float t = Mathf.Clamp01((1.5f - g.point.y) / PitBottomDepth);
                float spacing = Mathf.Lerp(14f, 7f, t);
                if (!FarFrom(g.point, spacing)) continue;
                if (Hash01(g.point.x, g.point.z, 5) > 0.35f + 0.65f * t) continue;      // 얕은 곳은 덜 놓는다
                Res r = PickByDepth(t, Hash01(g.point.x, g.point.z, 6));
                var node = MakeMineral(group, g.point, r, Mathf.RoundToInt(g.point.x * 13f + g.point.z * 7f));
                if (node == null) continue;
                // 깊을수록 광맥 바위도 더 크다
                var model = node.transform.childCount > 0 ? node.transform.GetChild(0) : null;
                if (model != null) model.localScale *= 1f + 0.4f * t;
                taken.Add(g.point);
                Count($"pit {(t < 0.3f ? "1 shallow" : t < 0.6f ? "2 middle" : "3 deep")} {r.id}");
            }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        var sb = new StringBuilder($"removed old quarry minerals={removed}\n");
        foreach (var kv in counts) sb.AppendLine(kv.Key + " = " + kv.Value);
        return sb.ToString();
    }

    // ───────────── 자연스러운 배치 (시작 지점 숲의 나무 밀도 기준) ─────────────
    // 시작 지점(플레이어 스폰) 주변 숲: 1000m²당 나무 약 1.6그루, 가장 가까운 이웃 중앙값 약 11m, 무리 지어 자람.
    // 격자 대신 무작위 무리(1~4개)로 놓고, 무리 안에서 큰 바위 하나 + 작은 바위들로 크기를 섞는다.
    public static float ForestDensity = 1.6f;        // 1000m²당 개수
    public static float DesertDensityMul = 1.0f;     // 사막 = 숲과 비슷
    public static float PlateauDensityMul = 0.12f;    // 채석장 맨 위 평지 = 드문드문
    public static float PitShallowMul = 1.0f, PitDeepMul = 3.0f;   // 구덩이: 얕은 곳 1배 → 바닥 3배 (깊을수록 많이)
    public static float ClusterSpacing = 10f;         // 무리 중심끼리 최소 간격
    static readonly int[] ClusterSizes = { 1, 1, 1, 1, 2, 2, 2, 3, 3, 4 };   // 평균 2개
    static Transform[] _ignoreRoots;

    struct Cell { public Vector3 p; public float w; public int region; public float t; }   // region 0 사막, 1 평지, 2 구덩이

    public static string NaturalLayout()
    {
        var desert = GameObject.Find("Map/08_Desert");
        var quarry = GameObject.Find("Map/09_Quarry");
        if (desert == null || quarry == null) return "08_Desert / 09_Quarry not found";

        Transform Group(GameObject owner)
        {
            var g = owner.transform.Find("DesertMinerals");
            if (g == null) { var go = new GameObject("DesertMinerals"); Undo.RegisterCreatedObjectUndo(go, "Desert mineral"); go.transform.SetParent(owner.transform, false); g = go.transform; }
            for (int i = g.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(g.GetChild(i).gameObject);
            return g;
        }
        var desertGroup = Group(desert); var quarryGroup = Group(quarry);
        _ignoreRoots = new[] { desertGroup, quarryGroup };
        Physics.SyncTransforms();

        // 다른 채집 노드(동굴/기름 등)와는 겹치지 않게
        var placed = new List<Vector4>();   // xyz + 반경
        foreach (var n in Object.FindObjectsByType<SpecialResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            placed.Add(new Vector4(n.transform.position.x, n.transform.position.y, n.transform.position.z, 2f));

        // 4m 간격으로 놓을 수 있는 바닥을 조사하고, 지역/깊이별 밀도를 매긴다
        var cells = new List<Cell>();
        float expected = 0f;
        void Survey(Rect area, int region)
        {
            for (float x = area.xMin; x <= area.xMax; x += 4f)
                for (float z = area.yMin; z <= area.yMax; z += 4f)
                {
                    if (!TryGroundAt(new Vector2(x, z), out RaycastHit g)) continue;   // 평지/구덩이는 아래에서 높이로 나눈다
                    int reg = region;
                    float t = 0f, mul;
                    if (region == 0) mul = DesertDensityMul;
                    else if (g.point.y > -3f) { reg = 1; mul = PlateauDensityMul; }
                    else { reg = 2; t = Mathf.Clamp01((1.5f - g.point.y) / PitBottomDepth); mul = Mathf.Lerp(PitShallowMul, PitDeepMul, t); }
                    float w = ForestDensity * mul;
                    cells.Add(new Cell { p = g.point, w = w, region = reg, t = t });
                    expected += w * 16f / 1000f;
                }
        }
        Survey(DesertArea, 0);
        Survey(QuarryArea, 1);
        if (cells.Count == 0) return "no ground cells";

        float total = 0f; var cum = new float[cells.Count];
        for (int i = 0; i < cells.Count; i++) { total += cells[i].w; cum[i] = total; }

        var rng = new System.Random(20261028);
        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        var centers = new List<Vector3>();
        int targetNodes = Mathf.RoundToInt(expected), made = 0;
        var counts = new SortedDictionary<string, int>();

        for (int attempt = 0; attempt < targetNodes * 30 && made < targetNodes; attempt++)
        {
            // 밀도에 비례해서 무리 중심을 뽑는다 (격자 대신 칸 안에서 무작위 위치)
            float pick = Rand(0f, total);
            int idx = System.Array.BinarySearch(cum, pick); if (idx < 0) idx = ~idx; idx = Mathf.Clamp(idx, 0, cells.Count - 1);
            Cell cell = cells[idx];
            Vector2 c2 = new Vector2(cell.p.x + Rand(-2f, 2f), cell.p.z + Rand(-2f, 2f));
            bool farFromCenters = true;
            foreach (var c in centers) if (new Vector2(c.x - c2.x, c.z - c2.y).sqrMagnitude < ClusterSpacing * ClusterSpacing && Mathf.Abs(c.y - cell.p.y) < 6f) { farFromCenters = false; break; }
            if (!farFromCenters) continue;

            // 무리: 같은 광맥 종류. 중심에 큰 바위, 주변에 작은 바위
            Res r = cell.region == 0 ? Pick((float)rng.NextDouble()) : cell.region == 1 ? Copper : PickByDepth(cell.t, (float)rng.NextDouble());
            Transform parent = cell.region == 0 ? desertGroup : quarryGroup;
            int size = ClusterSizes[rng.Next(ClusterSizes.Length)];
            int inCluster = 0;
            for (int m = 0; m < size * 4 && inCluster < size; m++)
            {
                Vector2 p2 = inCluster == 0 ? c2 : c2 + Rand(3.5f, 7f) * new Vector2(Mathf.Cos(Rand(0f, 6.283f)), Mathf.Sin(Rand(0f, 6.283f)));
                if (!TryGroundAt(p2, out RaycastHit g)) continue;
                if (Mathf.Abs(g.point.y - cell.p.y) > 2f) continue;   // 같은 층에만
                float sizeMul = inCluster == 0 ? Rand(1.0f, 1.7f) : Rand(0.45f, 0.95f);
                float radius = 1.1f * sizeMul + 0.6f;
                bool free = true;
                foreach (var q in placed)
                {
                    float min = Mathf.Max(2f, radius + q.w);
                    if (new Vector2(q.x - g.point.x, q.z - g.point.z).sqrMagnitude < min * min && Mathf.Abs(q.y - g.point.y) < 4f) { free = false; break; }
                }
                if (!free) continue;
                int seed = Mathf.RoundToInt(g.point.x * 13f + g.point.z * 7f);
                if (MakeMineral(parent, g.point, r, seed, sizeMul, Rand(-8f, 8f), Rand(-8f, 8f)) == null) continue;
                placed.Add(new Vector4(g.point.x, g.point.y, g.point.z, radius));
                inCluster++; made++;
                string region = cell.region == 0 ? "desert" : cell.region == 1 ? "quarry plateau" : cell.t < 0.3f ? "quarry pit 1 shallow" : cell.t < 0.6f ? "quarry pit 2 middle" : "quarry pit 3 deep";
                string key = region + " " + r.id; counts[key] = counts.TryGetValue(key, out int cc) ? cc + 1 : 1;
            }
            if (inCluster > 0) centers.Add(new Vector3(c2.x, cell.p.y, c2.y));
        }
        _ignoreRoots = null;
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        var sb = new StringBuilder($"natural layout: target≈{targetNodes} placed={made} clusters={centers.Count}\n");
        foreach (var kv in counts) sb.AppendLine("  " + kv.Key + " = " + kv.Value);
        return sb.ToString();
    }

    [MenuItem("Tools/Last Truck/Desert+Quarry: Natural Mineral Layout")]
    static void MenuNatural() { Debug.Log("[DesertMineralPlacer]\n" + NaturalLayout()); }

    [MenuItem("Tools/Last Truck/Desert: Remove Slabs+Trees, Place Minerals")]
    static void Menu() { Debug.Log("[DesertMineralPlacer]\n" + Run()); }
}
#endif
