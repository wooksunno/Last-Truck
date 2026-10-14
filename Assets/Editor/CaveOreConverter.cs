#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using CraftingSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 동굴 안 장식용 광석/수정을 우리 자원 색으로 바꾸고 채집할 수 있게 만든다.
///  - 색: 자원과 맞지 않는 색(Blue 대부분, Green, Red, Silver, Gradient)을 구리/철/금 색으로 바꾼다.
///        파란색(=다이아몬드)은 작은 것 일부만 남겨 희귀하게 유지한다. 구리는 OreCrystalConverter의 붉은 구리 재질을 쓴다.
///  - 채집: 크기 0.6~6m 광석을 팀 노드 구조(<이름>_node: Water 레이어 + 트리거 구 + SpecialResourceNode)로 감싼다.
///        크기가 작을수록 채집량/횟수를 줄인다.
///  - 큰 것(6m 초과 기둥, 절벽/벽 광맥)은 맵 구조물이라 채집하지 않는다(사라지면 맵이 깨짐).
///        대신 큰 수정 옆 바닥에 같은 색의 채집용 광석 군락을 하나씩 새로 놓는다.
///  "동굴 안" 판정은 AtmosphereController와 같다: 발밑에 동굴 바닥(TerrainStone 레이어)이 있으면 동굴.
/// </summary>
public static class CaveOreConverter
{
    const string Dir = "Assets/PurePoly/Mining_Pack/Prefabs/Ores and Crystals/";
    static readonly string[] Roots =
    {
        "Map/03_Ores&Crystals/Crystals&Ores&Veins",
        "Map/03_Ores&Crystals/HallOres/MysticCrystals",
        "Map/03_Ores&Crystals/HallOres2/MysticCrystals",
    };
    const int CaveFloorMask = 1 << 8;       // TerrainStone
    const int NodeLayer = 4;                // Water = PlayerInteract.interactLayer

    public static float MinGatherSize = 0.6f;
    public static float MaxGatherSize = 6f;
    public static float DiamondKeepChance = 0.15f;  // 작은(2m 이하) 파란 수정 중 다이아몬드로 남길 비율
    public static float MinNodeSpacing = 1.5f;      // 노드끼리 최소 간격 (멀티플레이 노드 매칭이 위치 기반)

    struct Res
    {
        public string id, label, color; public int tier, amount, minHits, maxHits; public float hold, minRegen, maxRegen;
    }

    // 팀이 HallOres 노드에 쓰는 값과 동일
    static readonly Res Copper = new Res { id = "copper_ore", label = "구리 광맥", color = "Copper", tier = 2, amount = 8, minHits = 3, maxHits = 5, hold = 1.2f, minRegen = 180, maxRegen = 360 };
    static readonly Res Iron = new Res { id = "iron_ore", label = "철 광맥", color = "Iron", tier = 3, amount = 6, minHits = 4, maxHits = 7, hold = 1.8f, minRegen = 300, maxRegen = 600 };
    static readonly Res Gold = new Res { id = "platinum_ore", label = "금 광맥", color = "Gold", tier = 4, amount = 3, minHits = 3, maxHits = 4, hold = 3.5f, minRegen = 420, maxRegen = 900 };
    static readonly Res Diamond = new Res { id = "diamond", label = "다이아몬드 원석", color = "Blue", tier = 5, amount = 1, minHits = 5, maxHits = 6, hold = 5f, minRegen = 1200, maxRegen = 1800 };

    static Res ByColor(string c) => c == "Copper" ? Copper : c == "Iron" ? Iron : c == "Gold" ? Gold : Diamond;

    // 채집하면 맵이 깨지는 구조물 광맥
    static bool IsStructural(string n) => n.StartsWith("PP_Cliff_Veins") || n.StartsWith("PP_Stone Wall_Veins") || n.StartsWith("PP_Cave_Wall");

    static readonly Regex ColorRx = new Regex(@"_(Blue|Copper|Gold|Green|Iron|Red|Silver)\b");
    static readonly Regex GradientRx = new Regex(@"^(PP_[A-Za-z_]+?)_Gradient_(\d+)");

    // 위치 기반 고정 랜덤 (다시 돌려도 같은 결과)
    static float Hash01(Vector3 p, int salt)
    {
        unchecked
        {
            int h = Mathf.RoundToInt(p.x * 10f) * 73856093 ^ Mathf.RoundToInt(p.z * 10f) * 19349663 ^ Mathf.RoundToInt(p.y * 10f) * 83492791 ^ salt * 2654435;
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0x7fffffff) % 10000 / 10000f;
        }
    }

    static string PickResourceColor(Vector3 p)
    {
        float r = Hash01(p, 7);
        return r < 0.45f ? "Copper" : r < 0.80f ? "Iron" : "Gold";
    }

    static string BaseName(string n) => Regex.Replace(n, @"(\s*\((\d+|[A-Z])\))+\s*$", "").Trim();

    /// <summary>색만 바꾼 프리팹 이름. 없으면 null.</summary>
    static GameObject TargetPrefab(string baseName, string toColor)
    {
        string name = null;
        if (ColorRx.IsMatch(baseName)) name = ColorRx.Replace(baseName, "_" + toColor);
        else
        {
            var m = GradientRx.Match(baseName);   // PP_Crystal_Column_Gradient_02 -> PP_Crystal_Column_02_<색>
            if (m.Success) name = $"{m.Groups[1].Value}_{m.Groups[2].Value.PadLeft(2, '0')}_{toColor}";
        }
        if (name == null) return null;
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + name + ".prefab");
        if (go == null)   // 같은 종류의 01번으로 대체
            go = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + Regex.Replace(name, @"_\d+_" + toColor + "$", "_01_" + toColor) + ".prefab");
        return go;
    }

    static string ColorOf(string baseName)
    {
        var m = ColorRx.Match(baseName);
        return m.Success ? m.Groups[1].Value : (baseName.Contains("_Gradient") ? "Gradient" : null);
    }

    /// <summary>메시/재질만 목표 색 프리팹 것으로 바꾼다 (오브젝트·위치는 유지).</summary>
    static bool Recolor(GameObject go, string toColor)
    {
        var target = TargetPrefab(BaseName(go.name), toColor);
        if (target == null) return false;
        var srcMf = target.GetComponentInChildren<MeshFilter>();
        var srcMr = target.GetComponentInChildren<MeshRenderer>();
        var mf = go.GetComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();
        if (srcMf == null || srcMr == null || mf == null || mr == null) return false;

        Undo.RecordObject(mf, "Recolor ore"); Undo.RecordObject(mr, "Recolor ore");
        mf.sharedMesh = srcMf.sharedMesh;
        mr.sharedMaterials = srcMr.sharedMaterials;
        var mc = go.GetComponent<MeshCollider>();
        if (mc != null) { Undo.RecordObject(mc, "Recolor ore"); mc.sharedMesh = srcMf.sharedMesh; }
        if (toColor == "Copper") OreCrystalConverter.ApplyCopperMaterial(go.transform);
        string newName = ColorRx.IsMatch(go.name) ? ColorRx.Replace(go.name, "_" + toColor) : go.name.Replace("_Gradient", "_" + toColor);
        Undo.RecordObject(go, "Recolor ore");
        go.name = newName;
        return true;
    }

    // 광석 자신(수정도 TerrainStone 레이어일 수 있음)을 제외하고 아래에 동굴 바닥이 있는지
    static bool InCave(Bounds b, Transform self)
    {
        foreach (var h in Physics.RaycastAll(new Vector3(b.center.x, b.max.y + 0.5f, b.center.z), Vector3.down, b.size.y + 8f, CaveFloorMask, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(self)) return true;
        return false;
    }

    static int AmountFor(Res r, float size, out int minHits, out int maxHits)
    {
        if (size < 1.5f) { minHits = 1; maxHits = 2; return Mathf.Max(1, Mathf.CeilToInt(r.amount * 0.25f)); }
        if (size < 3f) { minHits = 2; maxHits = 3; return Mathf.Max(1, Mathf.CeilToInt(r.amount * 0.5f)); }
        minHits = r.minHits; maxHits = r.maxHits; return r.amount;
    }

    static SpecialResourceNode MakeNode(Transform parent, string name, Vector3 position, Res r, int amount, int minHits, int maxHits, float radius, Vector3 localCenter)
    {
        var node = new GameObject(name + "_node");
        Undo.RegisterCreatedObjectUndo(node, "Ore node");
        node.layer = NodeLayer;
        node.transform.SetParent(parent, false);
        node.transform.position = position;
        var sc = node.AddComponent<SphereCollider>();
        sc.isTrigger = true; sc.radius = radius; sc.center = localCenter;
        var srn = node.AddComponent<SpecialResourceNode>();
        var item = ItemCatalog.GetOrCreate().GetItem(r.id);
        srn.Configure(r.label, item, amount, r.tier, minHits, maxHits, r.minRegen, r.maxRegen, "", r.hold);
        EditorUtility.SetDirty(srn);
        return srn;
    }

    public static string Run()
    {
        var sb = new StringBuilder();
        var nodePositions = new List<Vector3>();
        foreach (var n in Object.FindObjectsByType<SpecialResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            nodePositions.Add(n.transform.position);

        bool TooClose(Vector3 p)
        {
            foreach (var q in nodePositions) if ((q - p).sqrMagnitude < MinNodeSpacing * MinNodeSpacing) return true;
            return false;
        }

        var counts = new Dictionary<string, int>();
        void Count(string k) { counts[k] = counts.TryGetValue(k, out int c) ? c + 1 : 1; }

        // 대상 수집 (루트 렌더러 오브젝트 단위)
        var targets = new List<MeshRenderer>();
        foreach (var path in Roots)
        {
            var root = GameObject.Find(path);
            if (root == null) { sb.AppendLine("missing " + path); continue; }
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponentInParent<SpecialResourceNode>() != null) continue;   // 이미 노드
                if (ColorOf(BaseName(r.name)) == null) continue;                        // 광석/수정 아님
                targets.Add(r);
            }
        }

        var bigOnes = new List<(MeshRenderer r, string color)>();
        foreach (var r in targets)
        {
            var go = r.gameObject;
            string baseName = BaseName(go.name);
            Bounds b = r.bounds;
            float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (!InCave(b, go.transform)) { Count("skip_not_in_cave"); continue; }
            // 프리팹 내부 자식은 다른 부모로 옮길 수 없으므로 건너뛴다
            bool nestedInPrefab = PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.IsOutermostPrefabInstanceRoot(go);

            // 1) 색 정하기
            string color = ColorOf(baseName);
            string toColor = color;
            if (color == "Blue")
                toColor = size <= 2f && Hash01(b.center, 3) < DiamondKeepChance ? "Blue" : PickResourceColor(b.center);
            else if (color != "Copper" && color != "Iron" && color != "Gold")
                toColor = PickResourceColor(b.center);

            if (toColor != color)
            {
                if (Recolor(go, toColor)) Count("recolored_" + color + "->" + toColor);
                else { Count("recolor_failed"); continue; }
            }
            else if (toColor == "Copper")
            {
                OreCrystalConverter.ApplyCopperMaterial(go.transform);   // 구리는 팀 규칙대로 붉은 구리색
            }

            // 2) 채집 가능하게
            if (IsStructural(baseName)) { Count("structural_kept"); continue; }
            if (size > MaxGatherSize) { Count("big_kept"); bigOnes.Add((r, toColor)); continue; }
            if (size < MinGatherSize) { Count("tiny_decor"); continue; }
            if (nestedInPrefab) { Count("nested_prefab_child_decor"); continue; }

            Vector3 basePos = new Vector3(b.center.x, b.min.y, b.center.z);
            if (TooClose(basePos)) { Count("too_close_decor"); continue; }

            Res res = ByColor(toColor);
            int amount = AmountFor(res, size, out int minHits, out int maxHits);
            var parent = go.transform.parent;
            int sibling = go.transform.GetSiblingIndex();
            float radius = Mathf.Max(b.extents.x, b.extents.z) + 1f;
            var node = MakeNode(parent, go.name, basePos, res, amount, minHits, maxHits, radius, new Vector3(0f, b.extents.y, 0f));
            node.transform.SetSiblingIndex(sibling);
            Undo.SetTransformParent(go.transform, node.transform, "Ore node");
            nodePositions.Add(basePos);
            Count("gatherable_" + res.id);
        }

        // 3) 큰 수정 옆 바닥에 채집용 군락 추가
        foreach (var (r, color) in bigOnes)
        {
            Bounds b = r.bounds;
            Res res = ByColor(color == "Blue" ? PickResourceColor(b.center) : color);   // 큰 파란 기둥 옆에 다이아는 두지 않는다
            bool placed = false;
            float ring = Mathf.Max(b.extents.x, b.extents.z) + 2f;
            for (int i = 0; i < 12 && !placed; i++)
            {
                float a = (i / 12f + Hash01(b.center, 11)) * Mathf.PI * 2f;
                Vector3 probe = new Vector3(b.center.x + Mathf.Cos(a) * ring, b.max.y + 2f, b.center.z + Mathf.Sin(a) * ring);
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, b.size.y + 12f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > 30f) continue;                 // 평평한 바닥만
                if (hit.point.y > b.min.y + 2.5f || hit.point.y < b.min.y - 4f) continue;  // 큰 수정과 같은 층
                if (((1 << hit.collider.gameObject.layer) & CaveFloorMask) == 0) continue; // 동굴 바닥 위
                if (Physics.CheckSphere(hit.point + Vector3.up * 1.8f, 1.4f, ~0, QueryTriggerInteraction.Ignore)) continue; // 비어 있는 자리
                if (TooClose(hit.point)) continue;

                var node = MakeNode(r.transform.parent, "AddedOre_" + res.color, hit.point, res, res.amount, res.minHits, res.maxHits, 3.2f, Vector3.zero);
                int seed = Mathf.RoundToInt(hit.point.x * 13f + hit.point.z * 7f);
                if (OreCrystalConverter.Convert(node, res.id, seed))
                {
                    // 모델은 팀 노드처럼 동굴 바닥 레이어 (노드 자신만 상호작용용 Water 레이어)
                    foreach (Transform t in node.transform)
                        foreach (var tr in t.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 8;
                    nodePositions.Add(hit.point);
                    Count("added_near_big_" + res.id);
                    placed = true;
                }
                else Object.DestroyImmediate(node.gameObject);
            }
            if (!placed) Count("big_no_spot");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        var keys = new List<string>(counts.Keys); keys.Sort();
        foreach (var k in keys) sb.AppendLine(k + " = " + counts[k]);
        return sb.ToString();
    }

    [MenuItem("Tools/Last Truck/Cave Ores To Resources")]
    static void Menu() { Debug.Log("[CaveOreConverter]\n" + Run()); }

    // ───────────── 동굴 안쪽 어둡게 ─────────────
    // 동굴에 들어갈 때 세상 전체 조명을 바꾸면 갑자기 밤이 되는 느낌이 든다.
    // 대신 동굴 벽/바닥/바위 자체를 어두운 재질로 바꿔서 "빛이 잘 안 들어오는 곳"처럼 보이게 한다.
    // 광석/수정/발광 재질/횃불은 그대로라 어둠 속에서 더 도드라진다.
    const string StandardMatPath = "Assets/PurePoly/Mining_Pack/Materials/PP_Standard_Material.mat";
    public const string CaveMatPath = "Assets/LastTruck/Materials/PP_Standard_Material_Cave.mat";
    public static Color CaveTint = new Color(0.33f, 0.34f, 0.38f);
    static readonly Regex OreNameRx = new Regex(@"^(PP_Crystal|PP_Gemstone|PP_Rock_Gem|PP_Rock_Veins|PP_Stone_Crystal|PP_Stone_Veins|PP_Stones_Crystals|Crystals_|AddedOre)");

    public static Material CaveMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(CaveMatPath);
        var std = AssetDatabase.LoadAssetAtPath<Material>(StandardMatPath);
        if (mat == null && std != null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/LastTruck/Materials")) AssetDatabase.CreateFolder("Assets/LastTruck", "Materials");
            mat = new Material(std) { name = "PP_Standard_Material_Cave" };
            AssetDatabase.CreateAsset(mat, CaveMatPath);
        }
        if (mat != null) { mat.SetColor("_BaseColor", CaveTint); EditorUtility.SetDirty(mat); }
        return mat;
    }

    /// <summary>
    /// 동굴 바닥 레이어(TerrainStone)의 기본 재질 렌더러 중, 위가 덮여 있거나 지면 아래(높이 3 미만)에 있는 것을 어두운 재질로 바꾼다.
    /// 하늘로 뚫린 지상 쪽 바위/산 외벽은 그대로 둔다.
    /// </summary>
    public static string DarkenCaveInterior()
    {
        var std = AssetDatabase.LoadAssetAtPath<Material>(StandardMatPath);
        var cave = CaveMaterial();
        if (std == null || cave == null) return "material missing";

        int changed = 0, skippedOpen = 0, skippedOre = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (r.gameObject.layer != 8) continue;
            var mats = r.sharedMaterials;
            bool usesStd = false;
            for (int i = 0; i < mats.Length; i++) if (mats[i] == std) usesStd = true;
            if (!usesStd) continue;
            if (OreNameRx.IsMatch(r.name) || r.GetComponentInParent<SpecialResourceNode>() != null) { skippedOre++; continue; }

            Bounds b = r.bounds;
            bool covered = false;
            foreach (var h in Physics.RaycastAll(new Vector3(b.center.x, b.max.y + 0.3f, b.center.z), Vector3.up, 60f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.collider.transform.IsChildOf(r.transform)) { covered = true; break; }
            if (!covered && b.max.y >= 3f) { skippedOpen++; continue; }

            Undo.RecordObject(r, "Darken cave");
            for (int i = 0; i < mats.Length; i++) if (mats[i] == std) mats[i] = cave;
            r.sharedMaterials = mats;
            changed++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        return $"darkened={changed} keptOpenSky={skippedOpen} oresKept={skippedOre}";
    }

    [MenuItem("Tools/Last Truck/Darken Cave Interior")]
    static void MenuDarken() { Debug.Log("[CaveOreConverter] " + DarkenCaveInterior()); }
}
#endif
