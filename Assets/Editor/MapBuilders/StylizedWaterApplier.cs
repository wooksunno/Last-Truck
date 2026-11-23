using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 맵의 파란 임시 물(PurePoly 투명 재질, 숲 연못, 폐허 도시 바다)을 Stylized Water 3 재질로 바꾼다.
// 열려 있는 씬에만 적용된다 (프리팹 원본은 건드리지 않고 씬 인스턴스 오버라이드로 바꾼다).
public static class StylizedWaterApplier
{
    static readonly string[] WaterMatNames = { "PP_Standard_Material_Transparent", "ForestPondWater", "TD_Water" };
    static readonly Regex WaterName = new Regex("(?i)water|lake|pond|sea|ocean|river");

    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (; t != null; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    static bool IsBlue(Material m)
    {
        if (m == null) return false;
        Color c;
        if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
        else if (m.HasProperty("_Color")) c = m.GetColor("_Color");
        else return false;
        return c.b > c.r + 0.15f && c.b > c.g + 0.05f;
    }

    public static void Inspect(StringBuilder sb)
    {
        var scene = EditorSceneManager.GetActiveScene();
        Physics.SyncTransforms();
        var groups = new Dictionary<string, List<Renderer>>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.gameObject.scene != scene) continue;
            var mats = r.sharedMaterials;
            bool waterMat = mats.Any(m => m != null && WaterMatNames.Contains(m.name));
            bool waterName = WaterName.IsMatch(r.name);
            bool blue = mats.Any(IsBlue);
            if (!(waterMat || waterName || blue)) continue;
            string baseName = Regex.Replace(r.name, @"\s*\(\d+\)$", "");
            string key = baseName + " | " + string.Join(",", mats.Select(m => m == null ? "null" : m.name + "[" + m.shader.name + "]"))
                + " | flags " + (waterMat ? "M" : "") + (waterName ? "N" : "") + (blue ? "B" : "");
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<Renderer>();
            list.Add(r);
        }

        foreach (var kv in groups.OrderByDescending(k => k.Value.Count))
        {
            var list = kv.Value;
            var b = list[0].bounds;
            foreach (var r in list) b.Encapsulate(r.bounds);
            int active = list.Count(r => r.gameObject.activeInHierarchy && r.enabled);
            sb.AppendLine("== " + kv.Key + "  count=" + list.Count + " active=" + active
                + " min=" + b.min.ToString("F0") + " max=" + b.max.ToString("F0"));
            var mf = list[0].GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                sb.AppendLine("   mesh=" + mf.sharedMesh.name + " verts=" + mf.sharedMesh.vertexCount
                    + " colors=" + (mf.sharedMesh.colors.Length > 0) + " size=" + list[0].bounds.size.ToString("F1")
                    + " cast=" + list[0].shadowCastingMode + " col=" + (list[0].GetComponent<Collider>() != null));
            foreach (var r in list.Take(4))
                sb.AppendLine("   " + PathOf(r.transform) + " @" + r.bounds.center.ToString("F1") + " depth=" + DepthBelow(r).ToString("F2"));
        }
    }

    // 물 표면 중심에서 아래로 쏴서, 물 자신이 아닌 첫 바닥까지 거리 (없으면 -1)
    static float DepthBelow(Renderer r)
    {
        var b = r.bounds;
        var origin = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
        var hits = Physics.RaycastAll(origin, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
        float best = -1f;
        foreach (var h in hits)
        {
            if (h.collider.transform == r.transform) continue;
            if (best < 0 || h.distance < best) best = h.distance;
        }
        return best;
    }

    const string Folder = "Assets/Scenes/MiningMapAssets/Water";
    const string LakeMatPath = Folder + "/LT_Water_Lake.mat";   // 호수·연못·강·동굴 호수
    const string SeaMatPath = Folder + "/LT_Water_Sea.mat";     // 폐허 도시 둘레 바다
    const string PresetPath = "Assets/Stylized Water 3/Materials/StylizedWater3_Default.mat";

    [MenuItem("Tools/Last Truck/Water: Apply Stylized Water 3 (Open Scene)")]
    static void ApplyMenu()
    {
        var sb = new StringBuilder();
        Apply(sb);
        Debug.Log(sb.ToString());
    }

    // 재질 이름에 "Water"가 들어가야 FishingController가 물로 인식한다.
    public static void Apply(StringBuilder sb)
    {
        var lake = GetOrCreateMaterial(LakeMatPath);
        var sea = GetOrCreateMaterial(SeaMatPath);
        SetupLake(lake);
        SetupSea(sea);
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.GetActiveScene();
        int lakeCount = 0, seaCount = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.gameObject.scene != scene || r is ParticleSystemRenderer) continue;
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                Material target = null;
                if (m.name == "PP_Standard_Material_Transparent" && r.name.Contains("Water")) target = lake;
                else if (m.name == "ForestPondWater") target = lake;
                else if (m.name == "TD_Water") target = sea;
                if (target == null) continue;
                mats[i] = target;
                changed = true;
                if (target == lake) lakeCount++; else seaCount++;
            }
            if (!changed) continue;
            Undo.RecordObject(r, "Apply Stylized Water 3");
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 물은 그림자를 드리우지 않는다
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("lake renderers=" + lakeCount + " sea renderers=" + seaCount + " (scene " + scene.name + ", 저장은 따로)");
    }

    static Material GetOrCreateMaterial(string path)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scenes/MiningMapAssets", "Water");
        AssetDatabase.CopyAsset(PresetPath, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    static void Toggle(Material m, string prop, string keyword, bool on)
    {
        m.SetFloat(prop, on ? 1 : 0);
        if (keyword == null) return;
        if (on) m.EnableKeyword(keyword); else m.DisableKeyword(keyword);
    }

    // PurePoly 호수 물은 정점 4개짜리 평면이고 바닥까지 0.1~3m로 얕다.
    // → 정점 물결은 끄고(평면이 통째로 들썩이기만 함) 노멀맵·굴절·코스틱·물가 거품으로 물처럼 보이게, 얕아도 색이 나도록 깊이 범위를 짧게.
    static void SetupLake(Material m)
    {
        Toggle(m, "_WavesOn", "_WAVES", false);
        m.SetColor("_BaseColor", new Color(0.02f, 0.32f, 0.55f, 0.95f));
        m.SetColor("_ShallowColor", new Color(0.22f, 0.82f, 0.84f, 0.65f));
        m.SetFloat("_DepthVertical", 2.0f);
        m.SetFloat("_IntersectionLength", 2.2f);   // 기본 3m면 얕은 웅덩이 전체가 거품(하얀 얼룩)이 된다
        m.SetFloat("_FoamBaseAmount", 0.12f);
        m.SetFloat("_Speed", 0.6f);
        EditorUtility.SetDirty(m);
    }

    // 폐허 도시 바다: 큰 평면(정점 121개)이라 역시 정점 물결은 끄고, 바닥이 없으니 깊은 색이 넓게 보인다.
    static void SetupSea(Material m)
    {
        Toggle(m, "_WavesOn", "_WAVES", false);
        m.SetColor("_BaseColor", new Color(0.0f, 0.2f, 0.42f, 1f));
        m.SetColor("_ShallowColor", new Color(0.12f, 0.7f, 0.74f, 0.5f));
        m.SetFloat("_DepthVertical", 6f);
        m.SetFloat("_Speed", 1.1f);
        EditorUtility.SetDirty(m);
    }
}
