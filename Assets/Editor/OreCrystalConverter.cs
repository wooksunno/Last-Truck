#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using CraftingSystem;

/// <summary>
/// 광물 노드(SpecialResourceNode)의 모델을 광석 색상별 "크리스탈 군락"으로 바꾼다.
///  구리=주황(Copper) / 철=강철색(Iron) / 백금=은백색(Silver) / 다이아=청색(Blue)
/// 노드의 상호작용 구조(SphereCollider + SpecialResourceNode + Glow 라이트)는 그대로 두고 모델 자식만 교체한다.
/// </summary>
public static class OreCrystalConverter
{
    const string Dir = "Assets/PurePoly/Mining_Pack/Prefabs/Ores and Crystals/";

    static GameObject Load(string name) { return AssetDatabase.LoadAssetAtPath<GameObject>(Dir + name + ".prefab"); }

    static string ColorOf(string itemId)
    {
        switch (itemId)
        {
            case "copper_ore": return "Copper";
            case "iron_ore": return "Iron";
            case "platinum_ore": return "Silver";
            case "diamond": return "Blue";
        }
        return null;
    }

    static Color GlowOf(string itemId)
    {
        switch (itemId)
        {
            case "copper_ore": return new Color(1f, 0.55f, 0.22f);
            case "iron_ore": return new Color(0.62f, 0.78f, 1f);
            case "platinum_ore": return new Color(0.95f, 0.95f, 1f);
            case "diamond": return new Color(0.35f, 0.9f, 1f);
        }
        return Color.white;
    }

    static float HeightOf(string itemId)
    {
        switch (itemId)
        {
            case "diamond": return 4.2f;
            case "platinum_ore": return 3.6f;
            case "iron_ore": return 3.1f;
            default: return 2.9f;
        }
    }

    static GameObject Place(Transform parent, string prefabName, Vector3 localPos, float targetHeight, float yaw, float tilt, bool keepCollider)
    {
        var src = Load(prefabName);
        if (src == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        var r = go.GetComponentInChildren<Renderer>();
        float native = r != null ? Mathf.Max(0.05f, r.bounds.size.y) : 1f;
        float s = targetHeight / native;
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(Random.Range(-tilt, tilt), yaw, Random.Range(-tilt, tilt));
        go.transform.localScale = Vector3.one * s;
        if (!keepCollider) foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        return go;
    }

    /// <summary>한 노드의 모델을 군락으로 교체. 성공하면 true.</summary>
    public static bool Convert(SpecialResourceNode node, string itemId, int seed)
    {
        string col = ColorOf(itemId);
        if (col == null) return false;
        var t = node.transform;
        Random.InitState(seed);

        // 기존 모델 자식(= SphereCollider/Glow가 아닌 자식)을 찾아 레이어를 물려받고 제거
        int layer = t.gameObject.layer;
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var ch = t.GetChild(i);
            if (ch.name == "Glow" || ch.name == "OreGlow") continue;
            layer = ch.gameObject.layer;
            Object.DestroyImmediate(ch.gameObject);
        }

        float h = HeightOf(itemId);
        var model = new GameObject("Crystals_" + col);
        model.transform.SetParent(t, false);
        model.layer = layer;

        // 중심 큰 결정: 클러스터 또는 기둥형
        string[] mains = { "PP_Crystal_Cluster_01_", "PP_Crystal_Cluster_02_", "PP_Crystal_Cluster_03_", "PP_Crystal_Cluster_05_", "PP_Crystal_Column_01_", "PP_Crystal_Column_02_", "PP_Crystal_Column_04_" };
        string main = mains[Mathf.Abs(seed) % mains.Length] + col;
        var m0 = Place(model.transform, main, Vector3.zero, h, Random.Range(0f, 360f), 4f, true);

        // 주변 결정 3~5개 (크기 다양, 약간 기울임)
        int n = Random.Range(3, 6);
        string[] sides = { "PP_Crystal_Single_01_", "PP_Crystal_Single_03_", "PP_Crystal_Single_05_", "PP_Crystal_Single_07_", "PP_Crystal_Single_09_", "PP_Crystal_04_", "PP_Crystal_06_" };
        for (int i = 0; i < n; i++)
        {
            float a = (i + Random.value * 0.6f) / n * Mathf.PI * 2f;
            float rad = Random.Range(0.55f, 1.05f) * (h / 3f);
            Vector3 p = new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad);
            string nm = sides[Random.Range(0, sides.Length)] + col;
            Place(model.transform, nm, p, h * Random.Range(0.35f, 0.7f), Random.Range(0f, 360f), 18f, false);
        }
        foreach (var tr in model.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = layer;
        model.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // 색상 맞춘 글로우 + 상호작용 범위(구)를 군락 크기에 맞춤
        var glow = t.Find("Glow"); if (glow == null) glow = t.Find("OreGlow");
        if (glow != null)
        {
            var l = glow.GetComponent<Light>();
            if (l != null) { l.color = GlowOf(itemId); }
            glow.localPosition = new Vector3(0f, h * 0.55f, 0f);
        }
        var sc = t.GetComponent<SphereCollider>();
        if (sc != null) { sc.center = new Vector3(0f, h * 0.35f, 0f); }
        EditorUtility.SetDirty(node);
        return true;
    }

    public static string ConvertAll(bool alsoDiamond = true, string onlySceneName = null)
    {
        var sb = new StringBuilder();
        int conv = 0, kept = 0, idx = 0;
        foreach (var n in Object.FindObjectsByType<SpecialResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (onlySceneName != null && n.gameObject.scene.name != onlySceneName) continue;
            if (n.transform.Find("Crystals_Blue") != null || n.transform.Find("Crystals_Copper") != null
                || n.transform.Find("Crystals_Iron") != null || n.transform.Find("Crystals_Silver") != null) continue;   // 이미 변환됨
            var so = new SerializedObject(n);
            var it = so.FindProperty("item").objectReferenceValue as ItemData;
            if (it == null) continue;
            string id = it.itemID;
            if (ColorOf(id) == null) continue;
            bool isDiamond = id == "diamond";
            idx++;
            // 철/구리/백금은 3개 중 1개는 기존 광맥 바위를 남겨 변주를 준다
            bool keepRock = !isDiamond && (idx % 3 == 0);
            if (keepRock) { kept++; continue; }
            if (isDiamond && !alsoDiamond) continue;
            if (Convert(n, id, Mathf.RoundToInt(n.transform.position.x * 13f + n.transform.position.z * 7f))) conv++;
        }
        sb.AppendLine("converted=" + conv + " keptRock=" + kept);
        return sb.ToString();
    }

    [MenuItem("Tools/Last Truck/Convert Ores To Crystals")]
    static void Menu()
    {
        Debug.Log(ConvertAll());
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }
}
#endif
