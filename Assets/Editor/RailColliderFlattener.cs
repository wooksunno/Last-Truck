#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 철도(레일+침목) 메시 콜라이더를 납작하게 눌러 만든 복사본으로 교체한다.
/// 레일/침목은 노반 위로 0.3~0.45m 솟아 있어서 걸을 때 몸이 위아래로 튀고 카메라가 흔들렸다.
/// 눈에 보이는 모양(렌더러)은 그대로 두고, 충돌 메시만 높이를 기본 높이의 일부로 줄인다.
/// 노반(PP_Rail_Track_*, PP_Train_Track_*)의 콜라이더는 건드리지 않는다.
/// 메뉴: Tools/Last Truck/Flatten Rail Colliders (이미 처리된 것은 건너뛴다)
/// </summary>
public static class RailColliderFlattener
{
    const string OutDir = "Assets/LastTruck/Colliders";
    const string Prefix = "FlatRail_";
    public static float HeightFactor = 0.2f;

    static bool IsRailMesh(string objName, Mesh mesh)
    {
        if (mesh == null || !(objName.StartsWith("PP_Rail") || objName.StartsWith("PP_Train"))) return false;
        if (objName.Contains("Track")) return false;            // 노반
        if (objName.Contains("Wood_Slat_05") || objName.Contains("Wood_Slat_06")) return false;   // 큰 구조물
        return mesh.bounds.size.y <= 0.6f;                      // 레일+침목 정도의 낮은 메시만
    }

    [MenuItem("Tools/Last Truck/Flatten Rail Colliders")]
    static void Menu() { Debug.Log(Flatten()); }

    public static string Flatten()
    {
        if (!AssetDatabase.IsValidFolder(OutDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/LastTruck")) AssetDatabase.CreateFolder("Assets", "LastTruck");
            AssetDatabase.CreateFolder("Assets/LastTruck", "Colliders");
        }

        var cache = new Dictionary<Mesh, Mesh>();
        int changed = 0, skipped = 0;
        foreach (var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Mesh src = mc.sharedMesh;
            if (src == null || src.name.StartsWith(Prefix)) { skipped++; continue; }
            if (!IsRailMesh(mc.gameObject.name, src)) continue;

            if (!cache.TryGetValue(src, out Mesh flat))
            {
                flat = MakeFlat(src);
                cache[src] = flat;
            }
            if (flat == null) { skipped++; continue; }

            Undo.RecordObject(mc, "Flatten rail collider");
            mc.sharedMesh = flat;
            EditorUtility.SetDirty(mc);
            changed++;
        }

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }
        return "철도 콜라이더 납작하게: " + changed + "개 (고유 메시 " + cache.Count + "종, 건너뜀 " + skipped + ")";
    }

    static Mesh MakeFlat(Mesh src)
    {
        Vector3[] v;
        int[] tris;
        try
        {
            v = src.vertices;
            tris = src.triangles;
        }
        catch { return null; }
        if (v == null || v.Length == 0) return null;

        float minY = float.MaxValue;
        foreach (var p in v) if (p.y < minY) minY = p.y;
        for (int i = 0; i < v.Length; i++) v[i].y = minY + (v[i].y - minY) * HeightFactor;

        var flat = new Mesh { name = Prefix + src.name };
        flat.indexFormat = v.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        flat.vertices = v;
        flat.triangles = tris;
        flat.RecalculateBounds();
        flat.RecalculateNormals();

        string path = OutDir + "/" + flat.name + ".asset";
        AssetDatabase.CreateAsset(flat, AssetDatabase.GenerateUniqueAssetPath(path));
        return flat;
    }
}
#endif
