#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트럭이 지나갈 수 있는 맵 오브젝트(철도, 나무/풀/버섯, 작은 바위·결정)를 "TruckPass" 레이어(9)로 옮긴다.
/// 트럭의 바퀴/몸체는 TruckMove가 실행될 때 이 레이어와의 충돌을 끈다. 플레이어/총알/상호작용은 영향이 없다
/// (레이어만 바뀌고 충돌 매트릭스는 트럭 쪽만 건드린다).
/// 메뉴: Tools/Last Truck/Setup Truck Passable (열려 있는 모든 씬에 적용, 저장은 따로)
/// </summary>
public static class TruckPassSetup
{
    public const int PassLayer = 9;
    public const int WheelLayer = 10;
    const float SmallSize = 3.2f;   // 이보다 작은 바위/결정은 지나갈 수 있다(가장 긴 변, m)

    // 통째로 지나갈 수 있는 그룹(나무/풀/버섯/장식)
    static readonly string[] PassGroups = { "Vegetation", "Mushrooms", "ForestDecor", "ForestNature", "Coins" };
    // 작은 것만 지나갈 수 있는 그룹
    static readonly string[] SmallGroups = { "Stones&Rocks", "ForestRocks", "Crystals&Ores&Veins", "HallOres", "HallOres2", "Props", "Tools", "Weapons" };

    [MenuItem("Tools/Last Truck/Setup Truck Passable")]
    static void Menu()
    {
        EnsureLayerNames();
        var sb = new StringBuilder();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            int n = Apply(s);
            sb.AppendLine(s.name + ": " + n + "개 충돌체를 TruckPass 레이어로");
        }
        Debug.Log(sb.ToString());
    }

    /// <summary>레이어 이름 등록 (9 = TruckPass, 10 = TruckWheel)</summary>
    public static void EnsureLayerNames()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
        SerializedProperty layers = tagManager.FindProperty("layers");
        bool changed = false;
        changed |= SetLayerName(layers, PassLayer, "TruckPass");
        changed |= SetLayerName(layers, WheelLayer, "TruckWheel");
        if (changed)
        {
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }

    static bool SetLayerName(SerializedProperty layers, int index, string name)
    {
        SerializedProperty p = layers.GetArrayElementAtIndex(index);
        if (p.stringValue == name) return false;
        if (!string.IsNullOrEmpty(p.stringValue)) return false;   // 다른 이름이 이미 있으면 건드리지 않는다
        p.stringValue = name;
        return true;
    }

    public static int Apply(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                if (c.gameObject.layer != 0) continue;           // 동굴(8)·상호작용(4) 등 특수 레이어는 그대로
                if (c.GetComponentInParent<Rigidbody>() != null) continue;   // 트럭/움직이는 물체 제외
                if (!ShouldPass(c)) continue;
                c.gameObject.layer = PassLayer;
                count++;
            }
        }

        IncludeLayerInLights(scene);   // 이미 옮겨진 씬에서도 조명 마스크는 항상 맞춰 둔다
        if (count > 0) EditorSceneManager.MarkSceneDirty(scene);
        return count;
    }

    /// <summary>
    /// 일부 조명(해/달)은 컬링 마스크가 일부 레이어만 켜져 있다. 오브젝트를 TruckPass 레이어로 옮기면 그 조명을
    /// 못 받으므로, 마스크가 제한된 조명에 TruckPass 레이어를 추가한다.
    /// </summary>
    static void IncludeLayerInLights(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
            {
                if (l.cullingMask == -1) continue;
                int mask = l.cullingMask | (1 << PassLayer);
                if (mask != l.cullingMask)
                {
                    l.cullingMask = mask;
                    EditorUtility.SetDirty(l);
                }
            }
        }
    }

    static bool ShouldPass(Collider c)
    {
        string n = c.name;
        if (n.StartsWith("PP_Rail") || n.StartsWith("PP_Train")) return true;   // 철도 전부

        string group = TopGroup(c.transform);
        if (group == null) return false;

        foreach (string g in PassGroups)
            if (group == g) return true;

        Vector3 size = c.bounds.size;
        float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        foreach (string g in SmallGroups)
            if (group == g) return longest < SmallSize;

        return false;
    }

    /// <summary>Map 아래 그룹 이름(02_Nature 안의 Vegetation 등). 못 찾으면 null.</summary>
    static string TopGroup(Transform t)
    {
        Transform cur = t;
        while (cur.parent != null)
        {
            Transform parent = cur.parent;
            string pn = parent.name;
            // 숫자 그룹(01_Terrain, 02_Nature ...) 바로 아래가 "그룹"
            if (parent.parent != null && parent.parent.name == "Map") return cur.name;
            if (pn == "Map") return cur.name;
            cur = parent;
        }
        return null;
    }
}
#endif
