using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 물 미리보기: 열린 씬의 물(LT_Water_Lake / LT_Water_Sea)에 Stylized Water 3 프리셋·물결 프로필을 잠깐 끼워 본다.
// - 프리셋 원본은 복사본(메모리)으로만 쓰고, 씬은 "변경됨"으로 표시하지 않는다.
// - 미리보기 중에 씬을 저장하면 저장 직전에 원래 재질로 돌려놓고, 저장이 끝나면 미리보기를 다시 씌운다.
// - 컴파일·플레이 진입 뒤에도 되돌릴 수 있도록, 원래 재질은 기억하지 않고 미리보기 재질 이름(LT_Water_Preview_<Lake|Sea>_...)으로 알아낸다.
// - "확정"만 실제 재질 파일(LT_Water_Lake/Sea.mat)을 바꾼다.
[InitializeOnLoad]
public static class StylizedWaterPreview
{
    const string Menu = "Tools/Last Truck/Water/";
    const string LakeAsset = "Assets/Scenes/MiningMapAssets/Water/LT_Water_Lake.mat";
    const string SeaAsset = "Assets/Scenes/MiningMapAssets/Water/LT_Water_Sea.mat";
    const string PresetDir = "Assets/Stylized Water 3/Materials";
    const string ProfileDir = "Assets/Stylized Water 3/Profiles";
    const string Prefix = "LT_Water_Preview_";

    enum Kind { Lake, Sea }
    public enum Target { Lake = 0, Sea = 1, All = 2 }

    // 에디터를 끄기 전까지 유지 (컴파일해도 남는다)
    const string KeyPreset = "LTWaterPreview.Preset";     // -1 = 원래 재질
    const string KeyProfile = "LTWaterPreview.Profile";   // -1 = 프리셋 자체 물결
    const string KeyTarget = "LTWaterPreview.Target";

    static int PresetIndex { get => SessionState.GetInt(KeyPreset, -1); set => SessionState.SetInt(KeyPreset, value); }
    static int ProfileIndex { get => SessionState.GetInt(KeyProfile, -1); set => SessionState.SetInt(KeyProfile, value); }
    public static Target CurrentTarget { get => (Target)SessionState.GetInt(KeyTarget, (int)Target.All); set => SessionState.SetInt(KeyTarget, (int)value); }

    static readonly List<Material> created = new List<Material>();
    static bool reapplyAfterSave;

    static StylizedWaterPreview()
    {
        EditorSceneManager.sceneSaving += (scene, path) =>
        {
            if (!AnyPreviewActive()) return;
            Restore(Kind.Lake); Restore(Kind.Sea);
            reapplyAfterSave = true;
        };
        EditorSceneManager.sceneSaved += scene =>
        {
            if (!reapplyAfterSave) return;
            reapplyAfterSave = false;
            ApplyCurrent(false);
        };
        // 플레이를 멈추면 Unity가 장면을 플레이 전 상태로 되돌린다 → 플레이 중에 마지막으로 고른 프리셋을 다시 씌운다
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            if (PresetIndex >= 0 || ProfileIndex >= 0 || AnyPreviewActive()) ApplyCurrent(false);
        };
    }

    // ───────── 메뉴 ─────────
    [MenuItem(Menu + "미리보기 - 다음 프리셋 %&]", false, 100)]
    public static void NextPreset() { Step(+1); }

    [MenuItem(Menu + "미리보기 - 이전 프리셋 %&[", false, 101)]
    public static void PrevPreset() { Step(-1); }

    [MenuItem(Menu + "미리보기 - 다음 물결 프로필", false, 102)]
    public static void NextProfile()
    {
        int n = Profiles().Count;
        int p = ProfileIndex + 1;
        ProfileIndex = p >= n ? -1 : p;
        ApplyCurrent(true);
    }

    [MenuItem(Menu + "미리보기 - 원래대로", false, 103)]
    public static void ResetPreview()
    {
        PresetIndex = -1; ProfileIndex = -1;
        ApplyCurrent(true);
    }

    [MenuItem(Menu + "미리보기 대상/호수·강", false, 120)] static void TargetLake() { SetTarget(Target.Lake); }
    [MenuItem(Menu + "미리보기 대상/바다", false, 121)] static void TargetSea() { SetTarget(Target.Sea); }
    [MenuItem(Menu + "미리보기 대상/전부", false, 122)] static void TargetAll() { SetTarget(Target.All); }
    [MenuItem(Menu + "미리보기 대상/호수·강", true)] static bool TargetLakeCheck() { UnityEditor.Menu.SetChecked(Menu + "미리보기 대상/호수·강", CurrentTarget == Target.Lake); return true; }
    [MenuItem(Menu + "미리보기 대상/바다", true)] static bool TargetSeaCheck() { UnityEditor.Menu.SetChecked(Menu + "미리보기 대상/바다", CurrentTarget == Target.Sea); return true; }
    [MenuItem(Menu + "미리보기 대상/전부", true)] static bool TargetAllCheck() { UnityEditor.Menu.SetChecked(Menu + "미리보기 대상/전부", CurrentTarget == Target.All); return true; }

    [MenuItem(Menu + "미리보기 - 지금 프리셋으로 확정", false, 140)]
    static void ConfirmMenu()
    {
        if (PresetIndex < 0 && ProfileIndex < 0) { Notify("확정할 미리보기가 없어요. 먼저 프리셋을 골라 주세요."); return; }
        string what = Label();
        string files = string.Join(", ", Kinds().Select(k => Path.GetFileNameWithoutExtension(AssetPath(k))));
        if (!EditorUtility.DisplayDialog("물 프리셋 확정",
                files + " 재질을 '" + what + "'(으)로 덮어씁니다.\n지금 맞춰 둔 색·거품 설정은 사라집니다. (Ctrl+Z 또는 git으로 되돌릴 수 있어요)",
                "덮어쓰기", "취소")) return;
        Confirm();
    }

    [MenuItem(Menu + "미리보기 - 지금 프리셋으로 확정", true)]
    static bool ConfirmCheck() { return PresetIndex >= 0 || ProfileIndex >= 0; }

    // ───────── 동작 ─────────
    public static void SetTarget(Target t)
    {
        CurrentTarget = t;
        ApplyCurrent(true);
    }

    static void Step(int dir)
    {
        int n = Presets().Count;
        int i = PresetIndex + dir;            // -1(원래 재질) → 0 … n-1 → -1
        if (i >= n) i = -1;
        if (i < -1) i = n - 1;
        PresetIndex = i;
        ProfileIndex = -1;
        ApplyCurrent(true);
    }

    // 확정: 대상 물의 실제 재질 파일을 지금 미리보기 재질로 덮어쓴다
    public static void Confirm()
    {
        foreach (var k in Kinds())
        {
            var asset = AssetDatabase.LoadAssetAtPath<Material>(AssetPath(k));
            var preview = Build(k);
            Undo.RecordObject(asset, "물 프리셋 확정");
            asset.CopyPropertiesFromMaterial(preview);
            asset.shaderKeywords = preview.shaderKeywords;
            asset.renderQueue = preview.renderQueue;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
        string what = Label();
        PresetIndex = -1; ProfileIndex = -1;
        ApplyCurrent(false);
        Notify("확정: " + what);
    }

    // 지금 상태(프리셋·물결·대상)를 씬에 반영. 대상이 아닌 물은 원래 재질로 둔다.
    static void ApplyCurrent(bool notify)
    {
        bool any = PresetIndex >= 0 || ProfileIndex >= 0;
        foreach (Kind k in new[] { Kind.Lake, Kind.Sea })
        {
            if (any && Kinds().Contains(k)) Assign(k, Build(k));
            else Restore(k);
        }
        CleanupUnused();
        if (notify) Notify(any ? "물 미리보기: " + Label() + " · 대상 " + TargetName() : "물 미리보기: 원래 재질");
    }

    static Material Build(Kind k)
    {
        var presets = Presets();
        Material baseMat = PresetIndex >= 0
            ? AssetDatabase.LoadAssetAtPath<Material>(presets[PresetIndex])
            : AssetDatabase.LoadAssetAtPath<Material>(AssetPath(k));
        string presetName = PresetIndex >= 0 ? ShortName(presets[PresetIndex]) : "Current";
        var m = new Material(baseMat) { name = Prefix + k + "_" + presetName };

        if (PresetIndex >= 0)
        {
            // 이 맵 물 메시는 UV가 팔레트용 → 월드 좌표 UV (강 모드 프리셋 제외), 정점 색이 없으니 정점 색 옵션은 끈다
            if (m.GetFloat("_RiverModeOn") < 0.5f) m.SetFloat("_WorldSpaceUV", 1f);
            m.SetFloat("_VertexColorWaveFlattening", 0f);
            m.SetFloat("_VertexColorTransparency", 0f);
            m.SetFloat("_VertexColorFoam", 0f);
        }

        var profiles = Profiles();
        if (ProfileIndex >= 0)
        {
            var prof = AssetDatabase.LoadAssetAtPath<ScriptableObject>(profiles[ProfileIndex]);
            var lut = new SerializedObject(prof).FindProperty("shaderParametersLUT").objectReferenceValue as Texture;
            bool wavesWereOn = baseMat.IsKeywordEnabled("_WAVES");
            m.SetFloat("_WavesOn", 1f); m.EnableKeyword("_WAVES");
            if (!wavesWereOn || m.GetFloat("_WaveHeight") <= 0f) m.SetFloat("_WaveHeight", 0.5f);
            m.SetFloat("_VertexColorWaveFlattening", 0f);
            m.SetTexture("_WaveProfile", lut);
            m.name += "_" + Path.GetFileNameWithoutExtension(profiles[ProfileIndex]).Replace(" ", "");
        }
        created.Add(m);
        return m;
    }

    // ───────── 렌더러 찾기 / 바꾸기 ─────────
    static Kind? KindOf(Material m)
    {
        if (m == null) return null;
        if (m.name == "LT_Water_Lake" || m.name.StartsWith(Prefix + "Lake_")) return Kind.Lake;
        if (m.name == "LT_Water_Sea" || m.name.StartsWith(Prefix + "Sea_")) return Kind.Sea;
        return null;
    }

    static IEnumerable<Renderer> WaterRenderers()
    {
        return Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(r => r.sharedMaterials.Any(m => KindOf(m) != null));
    }

    static void Assign(Kind k, Material mat)
    {
        foreach (var r in WaterRenderers())
        {
            var arr = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < arr.Length; i++)
                if (KindOf(arr[i]) == k) { arr[i] = mat; changed = true; }
            if (changed) r.sharedMaterials = arr;   // Undo/SetDirty 없이 → 씬이 "변경됨"이 되지 않는다
        }
    }

    static void Restore(Kind k)
    {
        var asset = AssetDatabase.LoadAssetAtPath<Material>(AssetPath(k));
        foreach (var r in WaterRenderers())
        {
            var arr = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] != null && arr[i] != asset && KindOf(arr[i]) == k) { arr[i] = asset; changed = true; }
            if (changed) r.sharedMaterials = arr;
        }
    }

    static bool AnyPreviewActive()
    {
        return WaterRenderers().Any(r => r.sharedMaterials.Any(m => m != null && m.name.StartsWith(Prefix)));
    }

    static void CleanupUnused()
    {
        var used = new HashSet<Material>(WaterRenderers().SelectMany(r => r.sharedMaterials));
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] == null) { created.RemoveAt(i); continue; }
            if (used.Contains(created[i])) continue;
            Object.DestroyImmediate(created[i]);
            created.RemoveAt(i);
        }
    }

    // ───────── 목록 / 표시 ─────────
    static List<string> Presets()
    {
        return Directory.GetFiles(PresetDir, "*.mat").Select(p => p.Replace('\\', '/'))
            .Where(p => Path.GetFileNameWithoutExtension(p) != "WaterCutoutMask").OrderBy(p => p).ToList();
    }

    static List<string> Profiles()
    {
        return Directory.GetFiles(ProfileDir, "*.asset").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToList();
    }

    static List<Kind> Kinds()
    {
        switch (CurrentTarget)
        {
            case Target.Lake: return new List<Kind> { Kind.Lake };
            case Target.Sea: return new List<Kind> { Kind.Sea };
            default: return new List<Kind> { Kind.Lake, Kind.Sea };
        }
    }

    static string AssetPath(Kind k) { return k == Kind.Lake ? LakeAsset : SeaAsset; }
    static string ShortName(string path) { return Path.GetFileNameWithoutExtension(path).Replace("StylizedWater3_", ""); }
    static string TargetName() { return CurrentTarget == Target.Lake ? "호수·강" : CurrentTarget == Target.Sea ? "바다" : "전부"; }

    public static string Label()
    {
        var presets = Presets();
        string s = PresetIndex >= 0 ? ShortName(presets[PresetIndex]) + " (" + (PresetIndex + 1) + "/" + presets.Count + ")" : "현재 재질";
        if (ProfileIndex >= 0)
        {
            var profiles = Profiles();
            s += " · 물결 " + Path.GetFileNameWithoutExtension(profiles[ProfileIndex]) + " (" + (ProfileIndex + 1) + "/" + profiles.Count + ")";
        }
        return s;
    }

    static void Notify(string text)
    {
        Debug.Log("[물 미리보기] " + text);
        var content = new GUIContent(text);
        foreach (SceneView sv in SceneView.sceneViews) sv.ShowNotification(content, 2.0);
        var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        if (gameViewType != null)
            foreach (EditorWindow w in Resources.FindObjectsOfTypeAll(gameViewType)) w.ShowNotification(content, 2.0);
    }
}
