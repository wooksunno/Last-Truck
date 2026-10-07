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
    public static float HoldSeconds = 1.5f;

    // 나무는 크기 제한 없이 모두 채집 가능. 키가 클수록 오래 걸리고 많이 나온다.
    public static float SmallTreeHeight = 5f;    // 이 키 이하 = 작은 나무 (기본값)
    public static float LargeTreeHeight = 20f;   // 이 키 이상 = 최대치
    public static int MinTreeWood = 3, MaxTreeWood = 15;
    public static float MinTreeHold = 1.5f, MaxTreeHold = 6f;

    static void TreeYield(float height, out int amount, out float hold)
    {
        float t = Mathf.InverseLerp(SmallTreeHeight, LargeTreeHeight, height);
        amount = Mathf.RoundToInt(Mathf.Lerp(MinTreeWood, MaxTreeWood, t));
        hold = Mathf.Round(Mathf.Lerp(MinTreeHold, MaxTreeHold, t) * 10f) / 10f;
    }

    // 잡초처럼 생긴 풀(PP_Grass_*) = 약초. 나무/돌 옆에서 E키를 가로채지 않도록 트리거를 작게 잡는다.
    public static int HerbAmount = 1;
    public static float HerbHoldSeconds = 0.8f;
    public static float HerbTriggerPadding = 0.2f;
    public static float HerbChance = 0.2f;   // 풀 덤불 중 약초로 지정할 비율 (0~1). 한 포기짜리 풀(PP_Grass_Single)은 제외.

    // 위치 기반 고정 랜덤: 다시 Install해도 같은 풀이 약초로 선택된다.
    static bool IsHerbPick(Vector3 pos)
    {
        unchecked
        {
            int h = Mathf.RoundToInt(pos.x * 10f) * 73856093 ^ Mathf.RoundToInt(pos.z * 10f) * 19349663;
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0x7fffffff) % 1000 < HerbChance * 1000f;
        }
    }

    enum Kind { None, Rock, Tree, Log, Herb }

    static Kind Classify(string n)
    {
        if (n.StartsWith("PP_Grass_Single")) return Kind.None;
        if (n.StartsWith("PP_Grass_")) return Kind.Herb;
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

    /// <summary>
    /// 풀(PP_Grass_*)의 물리 콜라이더를 제거한다. 캐릭터가 풀에 걸리지 않게 하기 위함. 채집 트리거는 남긴다.
    /// </summary>
    public static int RemoveGrassColliders()
    {
        int n = 0;
        foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (col.isTrigger || !col.gameObject.scene.IsValid()) continue;
            Transform p = col.transform;
            while (p != null && !p.name.StartsWith("PP_Grass_")) p = p.parent;
            if (p == null) continue;
            Object.DestroyImmediate(col);
            n++;
        }
        Debug.Log("[GatherablePropInstaller] grass colliders removed " + n);
        return n;
    }

    public static string Install()
    {
        Remove();
        RemoveGrassColliders();
        int rocks = 0, trees = 0, logs = 0, herbs = 0, tooBig = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var go = r.gameObject;
            if (!go.scene.IsValid() || go.name == TriggerName) continue;
            if (PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.NotAPrefab && go.transform.parent == null) continue;
            Kind kind = Classify(go.name); if (kind == Kind.None) continue;
            // only prop roots (the object that carries the renderer and is not itself inside another gatherable prop)
            if (go.GetComponentInParent<GatherableProp>() != null) continue;

            if (kind == Kind.Herb && !IsHerbPick(go.transform.position)) continue;

            Bounds b = r.bounds; float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (kind == Kind.Rock || kind == Kind.Log)
            {
                float limit = kind == Kind.Rock ? MaxRockSize : 6f;
                if (size > limit) { tooBig++; continue; }
            }

            string itemId = kind == Kind.Rock ? ItemIds.Stone : kind == Kind.Herb ? ItemIds.Herb : ItemIds.Wood;
            int amount = kind == Kind.Log ? 2 : (size > 3f ? 2 : 1);
            float hold = HoldSeconds;
            if (kind == Kind.Tree) TreeYield(b.size.y, out amount, out hold);
            if (kind == Kind.Herb) { amount = HerbAmount; hold = HerbHoldSeconds; }
            string label = kind == Kind.Rock ? "돌" : kind == Kind.Tree ? (b.size.y >= 12f ? "큰 나무" : "나무")
                : kind == Kind.Herb ? "약초" : "통나무";

            var t = new GameObject(TriggerName); t.transform.SetParent(go.transform, true);
            t.transform.rotation = Quaternion.identity; t.layer = 4;     // PlayerInteract.interactLayer (Water)
            Vector3 ls = t.transform.lossyScale;
            float lsXZ = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z)));
            float lsY = Mathf.Max(0.0001f, Mathf.Abs(ls.y));
            if (kind == Kind.Tree)
            {
                // 나무는 줄기 밑동에 세운 캡슐: 키 큰 나무도 발밑 기준 감지(PlayerInteract.OverlapSphere)에 닿는다.
                t.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
                float radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 0.25f, 0.6f, 2f) + 1.0f;
                var cc = t.AddComponent<CapsuleCollider>(); cc.isTrigger = true; cc.direction = 1;
                cc.radius = radius / lsXZ;
                cc.height = Mathf.Max(b.size.y, radius * 2f) / lsY;
                cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            }
            else
            {
                t.transform.position = b.center;
                float pad = kind == Kind.Herb ? HerbTriggerPadding : 1.0f;
                var sc = t.AddComponent<SphereCollider>(); sc.isTrigger = true;
                sc.radius = (Mathf.Max(b.extents.x, b.extents.z) + pad) / Mathf.Max(lsXZ, lsY);
            }
            var gp = t.AddComponent<GatherableProp>(); gp.Configure(label, itemId, amount, hold, go);

            if (kind == Kind.Rock) rocks++; else if (kind == Kind.Tree) trees++; else if (kind == Kind.Herb) herbs++; else logs++;
        }
        EditorSceneManagerHelper.MarkDirty();
        string s = $"gatherable: rocks={rocks} trees={trees} logs={logs} herbs={herbs} skipped(too big)={tooBig}";
        Debug.Log("[GatherablePropInstaller] " + s);
        return s;
    }

    [MenuItem("Tools/Gatherable Props/Install")] static void MenuInstall() { Install(); }
    [MenuItem("Tools/Gatherable Props/Remove Grass Colliders")] static void MenuRemoveGrassColliders() { RemoveGrassColliders(); EditorSceneManagerHelper.MarkDirty(); }
    [MenuItem("Tools/Gatherable Props/Remove")] static void MenuRemove() { Remove(); EditorSceneManagerHelper.MarkDirty(); }
}

static class EditorSceneManagerHelper
{
    public static void MarkDirty() { UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); }
}
#endif
