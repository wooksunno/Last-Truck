#if UNITY_EDITOR
using System.Text;
using CraftingSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬의 일반 돌/나무 프롭에 채집용 트리거(GatherableProp)를 설치/제거한다. 너무 큰 프롭은 건너뛴다.
/// </summary>
public static class GatherablePropInstaller
{
    public const string TriggerName = "GatherTrigger";
    public static float MaxRockSize = 5f;     // 렌더러 bounds의 가장 긴 변(미터)
    public static float MaxTreeSize = 10f;
    public static float HoldSeconds = 1.5f;

    enum Kind { None, Rock, Tree, Log }

    static Kind Classify(string n)
    {
        if (n.StartsWith("PP_Rock_Plateau") || n.StartsWith("PP_Rock_Veins") || n.StartsWith("PP_Rock_Gem")) return Kind.None;
        if (n.StartsWith("PP_Rock_") || n.StartsWith("PP_Stone_Plain")) return Kind.Rock;
        if (n.StartsWith("PP_Tree_Trunk") || n.StartsWith("PP_Log_Pile")) return Kind.Log;
        if (n.StartsWith("PP_Tree_Root")) return Kind.None;
        if (n.StartsWith("PP_Tree_") || n.StartsWith("PP_Fir_Tree") || n.StartsWith("PP_Fantasy_Birch") || n.StartsWith("PP_Leafless_Tree")) return Kind.Tree;
        return Kind.None;
    }

    public static void Remove()
    {
        int n = 0;
        foreach (var g in Object.FindObjectsByType<GatherableProp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { Object.DestroyImmediate(g.gameObject); n++; }
        Debug.Log("[GatherablePropInstaller] removed " + n);
    }

    public static string Install()
    {
        Remove();
        int rocks = 0, trees = 0, logs = 0, tooBig = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var go = r.gameObject;
            if (!go.scene.IsValid() || go.name == TriggerName) continue;
            if (PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.NotAPrefab && go.transform.parent == null) continue;
            Kind kind = Classify(go.name); if (kind == Kind.None) continue;
            // only prop roots (the object that carries the renderer and is not itself inside another gatherable prop)
            if (go.GetComponentInParent<GatherableProp>() != null) continue;

            Bounds b = r.bounds; float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            float limit = kind == Kind.Tree ? MaxTreeSize : kind == Kind.Rock ? MaxRockSize : 6f;
            if (size > limit) { tooBig++; continue; }

            string itemId = kind == Kind.Rock ? ItemIds.Stone : ItemIds.Wood;
            int amount = kind == Kind.Tree ? 3 : kind == Kind.Log ? 2 : (size > 3f ? 2 : 1);
            string label = kind == Kind.Rock ? "돌" : kind == Kind.Tree ? "나무" : "통나무";

            var t = new GameObject(TriggerName); t.transform.SetParent(go.transform, true);
            t.transform.position = b.center; t.transform.rotation = Quaternion.identity; t.layer = 4;     // PlayerInteract.interactLayer (Water)
            float ls = Mathf.Max(0.0001f, Mathf.Max(t.transform.lossyScale.x, t.transform.lossyScale.y, t.transform.lossyScale.z));
            var sc = t.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = (Mathf.Max(b.extents.x, b.extents.z) + 1.0f) / ls;
            var gp = t.AddComponent<GatherableProp>(); gp.Configure(label, itemId, amount, HoldSeconds, go);

            if (kind == Kind.Rock) rocks++; else if (kind == Kind.Tree) trees++; else logs++;
        }
        EditorSceneManagerHelper.MarkDirty();
        string s = $"gatherable: rocks={rocks} trees={trees} logs={logs} skipped(too big)={tooBig}";
        Debug.Log("[GatherablePropInstaller] " + s);
        return s;
    }

    [MenuItem("Tools/Gatherable Props/Install")] static void MenuInstall() { Install(); }
    [MenuItem("Tools/Gatherable Props/Remove")] static void MenuRemove() { Remove(); EditorSceneManagerHelper.MarkDirty(); }
}

static class EditorSceneManagerHelper
{
    public static void MarkDirty() { UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); }
}
#endif
