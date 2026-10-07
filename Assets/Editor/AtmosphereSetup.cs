#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LastTruck;

/// <summary>
/// 낮/밤 포스트 프로세싱 프로파일, 씬의 Atmosphere 오브젝트, SSAO, 카메라 설정, 캐릭터 셰이더 머티리얼을 만들어 준다.
/// 메뉴: Tools/Last Truck/Setup Atmosphere (여러 번 실행해도 안전)
/// </summary>
public static class AtmosphereSetup
{
    const string Dir = "Assets/Settings/Atmosphere";
    const string CharMatDir = "Assets/LastTruck/Materials";

    static T Ensure<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (!p.TryGet<T>(out var c))
        {
            c = p.Add<T>(true);
            // 프로파일 에셋의 서브 에셋으로 저장해야 도메인 리로드/재시작 후에도 남는다
            if (!AssetDatabase.Contains(c)) AssetDatabase.AddObjectToAsset(c, p);
        }
        c.active = true;
        EditorUtility.SetDirty(c);
        return c;
    }

    static VolumeProfile LoadOrCreateProfile(string name)
    {
        string path = Dir + "/" + name + ".asset";
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (p == null)
        {
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
        }
        return p;
    }

    static void P<TParam, TVal>(VolumeParameter<TVal> prm, TVal v) { prm.Override(v); }

    public static VolumeProfile BuildDayProfile()
    {
        var p = LoadOrCreateProfile("Day_Profile");
        var tm = Ensure<Tonemapping>(p); tm.mode.Override(TonemappingMode.Neutral);
        var bl = Ensure<Bloom>(p);
        bl.threshold.Override(1.1f); bl.intensity.Override(0.22f); bl.scatter.Override(0.62f); bl.tint.Override(new Color(1f, 0.96f, 0.86f)); bl.highQualityFiltering.Override(true);
        var ca = Ensure<ColorAdjustments>(p);
        ca.postExposure.Override(-0.2f); ca.contrast.Override(14f); ca.saturation.Override(16f); ca.colorFilter.Override(new Color(1f, 0.99f, 0.95f));
        var wb = Ensure<WhiteBalance>(p); wb.temperature.Override(10f); wb.tint.Override(-2f);
        var st = Ensure<SplitToning>(p);
        st.shadows.Override(new Color(0.42f, 0.46f, 0.80f)); st.highlights.Override(new Color(1f, 0.86f, 0.62f)); st.balance.Override(-10f);
        var vg = Ensure<Vignette>(p);
        vg.intensity.Override(0.26f); vg.smoothness.Override(0.45f); vg.rounded.Override(false); vg.color.Override(new Color(0.12f, 0.1f, 0.2f));
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
    }

    public static VolumeProfile BuildNightProfile()
    {
        // 밤은 라이팅(달빛/안개/주변광)이 이미 푸른 톤을 만들어 주므로, 포스트는 은은하게만 더한다.
        var p = LoadOrCreateProfile("Night_Profile");
        var tm = Ensure<Tonemapping>(p); tm.mode.Override(TonemappingMode.Neutral);
        var bl = Ensure<Bloom>(p);
        bl.threshold.Override(0.9f); bl.intensity.Override(0.55f); bl.scatter.Override(0.7f); bl.tint.Override(new Color(0.85f, 0.92f, 1f)); bl.highQualityFiltering.Override(true);
        var ca = Ensure<ColorAdjustments>(p);
        ca.postExposure.Override(0f); ca.contrast.Override(10f); ca.saturation.Override(4f); ca.colorFilter.Override(new Color(1f, 1f, 1f));
        var wb = Ensure<WhiteBalance>(p); wb.temperature.Override(-6f); wb.tint.Override(0f);
        var st = Ensure<SplitToning>(p);
        st.shadows.Override(new Color(0.35f, 0.38f, 0.62f)); st.highlights.Override(new Color(0.95f, 0.88f, 0.8f)); st.balance.Override(-10f);
        var vg = Ensure<Vignette>(p);
        vg.intensity.Override(0.34f); vg.smoothness.Override(0.5f); vg.rounded.Override(false); vg.color.Override(new Color(0.02f, 0.03f, 0.09f));
        var cab = Ensure<ChromaticAberration>(p); cab.intensity.Override(0.05f);
        var fg = Ensure<FilmGrain>(p); fg.type.Override(FilmGrainLookup.Thin1); fg.intensity.Override(0.1f); fg.response.Override(0.8f);
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
    }

    static void EnsureSSAO(StringBuilder sb)
    {
        var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        var so = new SerializedObject(urp);
        var list = so.FindProperty("m_RendererDataList");
        var rd = list.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData;
        foreach (var f in rd.rendererFeatures) if (f is ScreenSpaceAmbientOcclusion) { sb.AppendLine("SSAO feature already present"); return; }
        var feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
        feature.name = "ScreenSpaceAmbientOcclusion";
        AssetDatabase.AddObjectToAsset(feature, rd);
        var rso = new SerializedObject(rd);
        var feats = rso.FindProperty("m_RendererFeatures");
        feats.arraySize++; feats.GetArrayElementAtIndex(feats.arraySize - 1).objectReferenceValue = feature;
        var map = rso.FindProperty("m_RendererFeatureMap");
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string guid, out long local);
        map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = local;
        rso.ApplyModifiedPropertiesWithoutUndo();
        // 부드럽고 은은한 AO (카툰 톤 유지)
        var fso = new SerializedObject(feature);
        var settings = fso.FindProperty("m_Settings");
        if (settings != null)
        {
            SetF(settings, "Intensity", 0.7f); SetF(settings, "Radius", 0.32f); SetF(settings, "DirectLightingStrength", 0.15f);
        }
        fso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rd); EditorUtility.SetDirty(feature);
        sb.AppendLine("SSAO renderer feature added");
    }

    static void SetF(SerializedProperty parent, string name, float v) { var p = parent.FindPropertyRelative(name); if (p != null) p.floatValue = v; }

    public static string Setup()
    {
        var sb = new StringBuilder();
        if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Settings", "Atmosphere");
        var day = BuildDayProfile(); var night = BuildNightProfile();
        AssetDatabase.SaveAssets();

        // 씬 오브젝트
        var root = GameObject.Find("Atmosphere");
        if (root == null) root = new GameObject("Atmosphere");
        var ctrl = root.GetComponent<AtmosphereController>(); if (ctrl == null) ctrl = root.AddComponent<AtmosphereController>();
        Volume Mk(string n, VolumeProfile prof, float prio)
        {
            var t = root.transform.Find(n); GameObject g = t != null ? t.gameObject : new GameObject(n);
            g.transform.SetParent(root.transform, false);
            var v = g.GetComponent<Volume>(); if (v == null) v = g.AddComponent<Volume>();
            v.isGlobal = true; v.sharedProfile = prof; v.priority = prio; v.weight = prio > 0 ? 0f : 1f;
            return v;
        }
        var dv = Mk("DayVolume", day, 0f); var nv = Mk("NightVolume", night, 1f);
        var so = new SerializedObject(ctrl);
        so.FindProperty("dayVolume").objectReferenceValue = dv; so.FindProperty("nightVolume").objectReferenceValue = nv;
        so.FindProperty("gameManager").objectReferenceValue = Object.FindFirstObjectByType<GameManager>();
        so.ApplyModifiedPropertiesWithoutUndo();

        // 카메라: 포스트 프로세싱 + SMAA
        foreach (var cam in Camera.allCameras)
        {
            var d = cam.GetComponent<UniversalAdditionalCameraData>(); if (d == null) d = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            d.renderPostProcessing = true; d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; d.antialiasingQuality = AntialiasingQuality.High;
            d.requiresDepthTexture = true; d.requiresColorTexture = false;
            cam.allowHDR = true;
            EditorUtility.SetDirty(d);
            sb.AppendLine("camera " + cam.name + ": post-processing + SMAA");
        }

        EnsureSSAO(sb);
        sb.AppendLine(SetupCharacterMaterials());
        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return sb.ToString();
    }

    public static string SetupCharacterMaterials()
    {
        var sb = new StringBuilder();
        var shader = Shader.Find("LastTruck/StylizedCharacter");
        if (shader == null) return "StylizedCharacter shader not found (compile error?)";
        if (!AssetDatabase.IsValidFolder(CharMatDir)) AssetDatabase.CreateFolder("Assets/LastTruck", "Materials");

        // 치비 캐릭터 6종 공용 머티리얼
        var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/LastTruck/PolyOne/Chibi Character/Materials/Chibi_Character_M.mat");
        string path = CharMatDir + "/Chibi_Stylized.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        if (src != null) { mat.SetTexture("_BaseMap", src.GetTexture("_BaseMap")); mat.SetColor("_BaseColor", src.GetColor("_BaseColor")); }
        EditorUtility.SetDirty(mat);

        int n = 0;
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/LastTruck/Character/Prefabs", "Assets/LastTruck/PolyOne/Chibi Character/Prefabs" }))
        {
            string pp = AssetDatabase.GUIDToAssetPath(g);
            var root = PrefabUtility.LoadPrefabContents(pp);
            bool changed = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) if (ms[i] != null && src != null && ms[i] == src) { ms[i] = mat; changed = true; }
                if (changed) r.sharedMaterials = ms;
            }
            if (changed) { PrefabUtility.SaveAsPrefabAsset(root, pp); n++; }
            PrefabUtility.UnloadPrefabContents(root);
        }
        sb.AppendLine("character prefabs updated: " + n);

        // 몬스터(임시 캡슐/구체) - 같은 셰이더로, 원래 색 유지 + 빨간 림
        var mp = "Assets/Mr.No/Monster/Monster.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(mp) != null)
        {
            string mpath = CharMatDir + "/Monster_Stylized.mat";
            var mm = AssetDatabase.LoadAssetAtPath<Material>(mpath);
            if (mm == null) { mm = new Material(shader); AssetDatabase.CreateAsset(mm, mpath); }
            mm.shader = shader;
            mm.SetColor("_BaseColor", new Color(0.46f, 0.72f, 0.42f)); mm.SetColor("_ShadeColor", new Color(0.35f, 0.48f, 0.55f));
            mm.SetColor("_RimColor", new Color(1f, 0.42f, 0.38f)); mm.SetFloat("_RimNight", 1.6f); mm.SetFloat("_RimDay", 0.45f);
            mm.SetColor("_OutlineColor", new Color(0.1f, 0.14f, 0.08f));
            EditorUtility.SetDirty(mm);
            var root = PrefabUtility.LoadPrefabContents(mp); bool changed = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) { var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) { if (ms[i] != null && ms[i].shader.name == "Universal Render Pipeline/Lit") { ms[i] = mm; changed = true; } } r.sharedMaterials = ms; }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, mp);
            PrefabUtility.UnloadPrefabContents(root);
            sb.AppendLine("monster prefab material: " + (changed ? "swapped" : "unchanged"));
        }
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    [MenuItem("Tools/Last Truck/Setup Atmosphere")] static void MenuSetup() { Debug.Log(Setup()); }
}
#endif
