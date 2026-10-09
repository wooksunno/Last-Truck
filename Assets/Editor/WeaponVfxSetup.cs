#if UNITY_EDITOR
using Combat;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 무기 이펙트 연결: 화염방사기(vfx_Flamethrower_01)와 근접 타격(단순 임팩트)을 모든 플레이어 프리팹/씬 인스턴스의 WeaponController에 연결한다.
/// 메뉴: Tools/Last Truck/Setup Weapon VFX
/// </summary>
public static class WeaponVfxSetup
{
    const string VfxDir = "Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs/";
    public const string FlamePrefab = "vfx_Flamethrower_01";
    public const string MeleeHitPrefab = "vfx_Impact_01";

    [MenuItem("Tools/Last Truck/Setup Weapon VFX")]
    static void Menu() { Debug.Log(Setup(FlamePrefab, MeleeHitPrefab)); }

    public static string Setup(string flameName, string meleeHitName)
    {
        var sb = new StringBuilder();
        var flame = AssetDatabase.LoadAssetAtPath<GameObject>(VfxDir + flameName + ".prefab");
        var hit = AssetDatabase.LoadAssetAtPath<GameObject>(VfxDir + meleeHitName + ".prefab");
        if (flame == null) sb.AppendLine("불꽃 프리팹 없음: " + flameName);
        if (hit == null) sb.AppendLine("타격 프리팹 없음: " + meleeHitName);

        int n = 0;
        foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/LastTruck/Character/Prefabs" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var root = PrefabUtility.LoadPrefabContents(p);
            var wc = root.GetComponent<WeaponController>();
            if (wc != null && Apply(wc, flame, hit)) { PrefabUtility.SaveAsPrefabAsset(root, p); n++; }
            PrefabUtility.UnloadPrefabContents(root);
        }
        foreach (var wc in Object.FindObjectsByType<WeaponController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (Apply(wc, flame, hit)) n++;

        sb.AppendLine("WeaponController에 이펙트 연결: " + n + "곳");
        return sb.ToString();
    }

    static bool Apply(WeaponController wc, GameObject flame, GameObject hit)
    {
        var so = new SerializedObject(wc);
        var f = so.FindProperty("flamethrowerVfxPrefab");
        var h = so.FindProperty("meleeHitVfxPrefab");
        if (f == null || h == null) return false;
        f.objectReferenceValue = flame;
        h.objectReferenceValue = hit;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(wc);
        return true;
    }
}
#endif
