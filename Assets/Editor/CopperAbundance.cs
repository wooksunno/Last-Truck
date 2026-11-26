#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using CraftingSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 구리를 가장 흔한 광물로 만들고 구하기 쉽게 한다.
///  1) 붉은 구리 모양인데 캘 수 없던 장식 결정(약 370개)을 구리 광맥으로 만든다 ("빨간 수정인데 왜 안 캐져?" 해결).
///  2) 철광맥 일부(약 40%)를 구리 광맥으로 바꾼다 → 구리 > 철 > 금 > 다이아 순으로 흔해진다.
///  3) 원래 있던 구리 광맥은 한 번에 더 많이 나오고(×1.5), 더 빨리 캐지고, 더 빨리 다시 생긴다.
/// 다시 실행해도 안전하다 (이미 구리가 된 것은 건너뛴다).
/// </summary>
public static class CopperAbundance
{
    const string PrefabDir = "Assets/PurePoly/Mining_Pack/Prefabs/Ores and Crystals/";
    const int NodeLayer = 4;   // Water = PlayerInteract.interactLayer

    // 구리 광맥 값 (CaveOreConverter의 Copper보다 넉넉하게)
    const int CopperAmount = 9;
    const float CopperHold = 1.0f;
    const float CopperMinRegen = 100f, CopperMaxRegen = 200f;
    const float MinNodeSpacing = 1.0f;
    public const float IronToCopperFraction = 0.40f;

    static readonly Regex CopperRx = new Regex(@"_Copper\b");
    static readonly Regex IronRx = new Regex(@"_Iron\b");
    static readonly Regex SuffixRx = new Regex(@"(\s*\((\d+|[A-Z])\))+\s*$|\(Clone\)$");

    static float Hash01(Vector3 p, int salt)
    {
        unchecked
        {
            int h = Mathf.RoundToInt(p.x * 10f) * 73856093 ^ Mathf.RoundToInt(p.z * 10f) * 19349663 ^ Mathf.RoundToInt(p.y * 10f) * 83492791 ^ salt * 2654435;
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0x7fffffff) % 10000 / 10000f;
        }
    }

    static bool IsStructural(string n) => n.StartsWith("PP_Cliff_Veins") || n.StartsWith("PP_Stone Wall_Veins") || n.StartsWith("PP_Cave_Wall");

    static string ItemOf(SpecialResourceNode n)
    {
        return n.Item != null ? n.Item.itemID : "";
    }

    [MenuItem("Tools/Last Truck/Ores: Make Copper The Most Common")]
    static void Menu() { Debug.Log("[CopperAbundance]\n" + Run()); }

    public static string Run()
    {
        var sb = new StringBuilder();
        var copper = ItemCatalog.GetOrCreate().GetItem(ItemIds.CopperOre);
        if (copper == null) return "copper_ore item not found";

        var nodes = new List<SpecialResourceNode>(Object.FindObjectsByType<SpecialResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        var positions = new List<Vector3>();
        foreach (var n in nodes) positions.Add(n.transform.position);

        int beforeCopper = 0, beforeIron = 0;
        foreach (var n in nodes) { string id = ItemOf(n); if (id == ItemIds.CopperOre) beforeCopper++; else if (id == ItemIds.IronOre) beforeIron++; }

        // 3) 원래 구리 광맥 보강 (먼저 한다: 아래에서 새로 만드는 것은 이미 새 값이라 중복 보강하지 않게)
        int boosted = 0;
        foreach (var n in nodes)
        {
            if (ItemOf(n) != ItemIds.CopperOre) continue;
            var so = new SerializedObject(n);
            int amt = so.FindProperty("amount").intValue;
            if (so.FindProperty("gatherHoldSeconds").floatValue > CopperHold + 0.001f)   // 캐는 시간이 이미 줄어 있으면 보강된 것이다 → 다시 하지 않는다
            {
                so.FindProperty("amount").intValue = Mathf.Max(amt + 1, Mathf.CeilToInt(amt * 1.5f));
                var hold = so.FindProperty("gatherHoldSeconds"); hold.floatValue = Mathf.Min(hold.floatValue, CopperHold);
                var minR = so.FindProperty("minRegenSeconds"); minR.floatValue = Mathf.Min(minR.floatValue, CopperMinRegen);
                var maxR = so.FindProperty("maxRegenSeconds"); maxR.floatValue = Mathf.Min(maxR.floatValue, CopperMaxRegen);
                so.ApplyModifiedProperties();
                boosted++;
            }
        }
        sb.AppendLine("boosted existing copper nodes = " + boosted);

        // 1) 캘 수 없던 붉은 장식 결정을 구리 광맥으로
        int made = 0, skipTooClose = 0, skipOther = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!CopperRx.IsMatch(r.name)) continue;
            if (r.GetComponentInParent<SpecialResourceNode>() != null) continue;
            var go = r.gameObject;
            if (IsStructural(go.name)) { skipOther++; continue; }
            Bounds b = r.bounds;
            float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (size > 6f || size < 0.6f) { skipOther++; continue; }
            bool nested = PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.IsOutermostPrefabInstanceRoot(go);
            if (nested) { skipOther++; continue; }
            if (go.transform.parent != null && go.transform.parent.name.StartsWith("PP_Treasure")) { skipOther++; continue; }

            Vector3 basePos = new Vector3(b.center.x, b.min.y, b.center.z);
            bool close = false;
            foreach (var q in positions) if ((q - basePos).sqrMagnitude < MinNodeSpacing * MinNodeSpacing) { close = true; break; }
            if (close) { skipTooClose++; continue; }

            int amount, minHits, maxHits;
            if (size < 1.5f) { amount = Mathf.Max(2, Mathf.CeilToInt(CopperAmount * 0.3f)); minHits = 1; maxHits = 2; }
            else if (size < 3f) { amount = Mathf.CeilToInt(CopperAmount * 0.55f); minHits = 2; maxHits = 3; }
            else { amount = CopperAmount; minHits = 3; maxHits = 5; }

            var parent = go.transform.parent;
            int sibling = go.transform.GetSiblingIndex();
            var node = new GameObject(go.name + "_node");
            node.layer = NodeLayer;
            node.transform.SetParent(parent, false);
            node.transform.position = basePos;
            var sc = node.AddComponent<SphereCollider>();
            sc.isTrigger = true; sc.radius = Mathf.Max(b.extents.x, b.extents.z) + 1f; sc.center = new Vector3(0f, b.extents.y, 0f);
            var srn = node.AddComponent<SpecialResourceNode>();
            srn.Configure("구리 광맥", copper, amount, 2, minHits, maxHits, CopperMinRegen, CopperMaxRegen, "", CopperHold);
            node.transform.SetSiblingIndex(sibling);
            go.transform.SetParent(node.transform, true);
            EditorUtility.SetDirty(srn);
            positions.Add(basePos);
            made++;
        }
        sb.AppendLine("red decor crystals -> copper nodes = " + made + " (too close " + skipTooClose + ", other skipped " + skipOther + ")");

        // 2) 철광맥 일부를 구리로
        int converted = 0, notConvertible = 0;
        foreach (var n in nodes)
        {
            if (n == null || ItemOf(n) != ItemIds.IronOre) continue;
            if (Hash01(n.transform.position, 21) >= IronToCopperFraction) continue;

            // 모델 렌더러 중 _Iron 이름인 것을 구리 모델로 바꾼다
            var swaps = new List<(MeshFilter mf, MeshRenderer mr, GameObject target)>();
            foreach (var mf in n.GetComponentsInChildren<MeshFilter>(true))
            {
                string nm = SuffixRx.Replace(mf.gameObject.name, "").Trim();
                if (!IronRx.IsMatch(nm)) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                string target = IronRx.Replace(nm, "_Copper");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + target + ".prefab");
                if (prefab == null) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + Regex.Replace(target, @"_\d+_Copper$", "_01_Copper") + ".prefab");
                if (prefab == null) continue;
                swaps.Add((mf, mr, prefab));
            }
            if (swaps.Count == 0) { notConvertible++; continue; }

            foreach (var (mf, mr, prefab) in swaps)
            {
                var srcMf = prefab.GetComponentInChildren<MeshFilter>();
                var srcMr = prefab.GetComponentInChildren<MeshRenderer>();
                if (srcMf == null || srcMr == null) continue;
                mf.sharedMesh = srcMf.sharedMesh;
                mr.sharedMaterials = srcMr.sharedMaterials;
                var mc = mf.GetComponent<MeshCollider>();
                if (mc != null) mc.sharedMesh = srcMf.sharedMesh;
                mf.gameObject.name = IronRx.Replace(SuffixRx.Replace(mf.gameObject.name, "").Trim(), "_Copper");
                OreCrystalConverter.ApplyCopperMaterial(mf.transform);
                EditorUtility.SetDirty(mf); EditorUtility.SetDirty(mr);
            }

            // 크기별 채집량 (원래 철광맥의 상대 크기를 따라간다)
            var so = new SerializedObject(n);
            int oldAmt = so.FindProperty("amount").intValue;
            float rel = Mathf.Clamp01(oldAmt / 6f);   // 철 전체 크기 = 6
            int amt = Mathf.Max(2, Mathf.RoundToInt(CopperAmount * rel));
            n.Configure("구리 광맥", copper, amt, 2, 3, 5, CopperMinRegen, CopperMaxRegen, "", CopperHold);
            EditorUtility.SetDirty(n);

            var glow = n.transform.Find("Glow"); if (glow == null) glow = n.transform.Find("OreGlow");
            if (glow != null) { var l = glow.GetComponent<Light>(); if (l != null) { l.color = new Color(1f, 0.42f, 0.22f); EditorUtility.SetDirty(l); } }
            converted++;
        }
        sb.AppendLine("iron nodes -> copper = " + converted + " (not convertible " + notConvertible + ")");

        // 결과 집계
        int cu = 0, fe = 0, au = 0, di = 0;
        foreach (var n in Object.FindObjectsByType<SpecialResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string id = ItemOf(n);
            if (id == ItemIds.CopperOre) cu++; else if (id == ItemIds.IronOre) fe++; else if (id == ItemIds.PlatinumOre) au++; else if (id == ItemIds.Diamond) di++;
        }
        sb.AppendLine("BEFORE copper=" + beforeCopper + " iron=" + beforeIron);
        sb.AppendLine("AFTER  copper=" + cu + " iron=" + fe + " gold(platinum_ore)=" + au + " diamond=" + di);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        return sb.ToString();
    }
}
#endif
