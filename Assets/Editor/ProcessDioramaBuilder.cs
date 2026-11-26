#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using CraftingSystem;
using LastTruck;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 가공 화면용 "3D 미니 장면" 프리팹 7개를 만든다 (Assets/Resources/ProcessDiorama/Stage_설비이름.prefab).
/// 맵에서 쓰는 모델(모닥불, 숫돌, 용광로, 모루, 통, 상자, 나무, 텐트...)과 파티클(불꽃, 불똥, 연기, 거품, 반딧불)로 꾸민다.
/// 맵에 없는 설비(롤러 프레스, 절삭기, 정제탑, 무두질 건조대)는 기본 도형으로 조립한다 (롤러/톱날은 실제로 돈다).
/// Assets/Art/ProcessScenes/Backdrops/BG_설비이름.png 가 있으면 뒷배경을 그 그림으로 바꾸고 3D 배경 소품은 끈다.
/// 다시 실행하면 덮어쓴다.
/// </summary>
public static class ProcessDioramaBuilder
{
    const string OutDir = "Assets/Resources/ProcessDiorama";
    const string MatDir = "Assets/Art/ProcessScenes/Materials";
    const string BgDir = "Assets/Art/ProcessScenes/Backdrops";

    static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();
    static System.Random rng;
    static StringBuilder log;

    [MenuItem("Tools/Last Truck/UI: Build Process Dioramas")]
    static void Menu() { Debug.Log("[ProcessDioramaBuilder]\n" + Run()); }

    // ───────── 도우미 ─────────

    static GameObject Load(string name)
    {
        if (Cache.TryGetValue(name, out var g)) return g;
        GameObject found = null;
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name) { found = AssetDatabase.LoadAssetAtPath<GameObject>(p); break; }
        }
        if (found == null) log.AppendLine("  (없음) " + name);
        Cache[name] = found;
        return found;
    }

    static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    static readonly Dictionary<string, Material> DarkCache = new Dictionary<string, Material>();

    /// 배경 소품을 어둡게: 원본 재질을 복사해 색만 곱한 재질로 바꾼다 (원본은 건드리지 않음).
    static GameObject Dark(GameObject go, float mul)
    {
        if (go == null || mul >= 0.999f) return go;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                string key = m.name + "_" + Mathf.RoundToInt(mul * 100f);
                if (!DarkCache.TryGetValue(key, out var dm))
                {
                    string path = MatDir + "/Dark/" + key + ".mat";
                    dm = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (dm == null) { dm = new Material(m); AssetDatabase.CreateAsset(dm, path); }
                    else dm.CopyPropertiesFromMaterial(m);
                    foreach (string prop in new[] { "_BaseColor", "_Color" })
                        if (dm.HasProperty(prop))
                        {
                            Color c = m.GetColor(prop);
                            dm.SetColor(prop, new Color(c.r * mul, c.g * mul, c.b * mul, c.a));
                        }
                    EditorUtility.SetDirty(dm);
                    DarkCache[key] = dm;
                }
                mats[i] = dm;
            }
            r.sharedMaterials = mats;
        }
        return go;
    }

    static GameObject Put(Transform parent, string prefab, Vector3 pos, float yaw = 0f, float scale = 1f)
    {
        var pf = Load(prefab);
        if (pf == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = pf.transform.localScale * scale;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        return go;
    }

    static GameObject Fx(Transform parent, string prefab, Vector3 pos, float scale = 1f, Color? tint = null, bool forceLoop = true)
    {
        var go = Put(parent, prefab, pos, 0f, scale);
        if (go == null) return null;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var m = ps.main;
            if (forceLoop) m.loop = true;
            m.playOnAwake = true;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            if (tint.HasValue) m.startColor = tint.Value;
        }
        return go;
    }

    static Material Mat(string name, Color color, Color? emission = null, float smooth = 0f)
    {
        string path = MatDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("LastTruck/ToonWorld")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", color);
        if (emission.HasValue) { mat.SetColor("_EmissionColor", emission.Value); mat.EnableKeyword("_EMISSION"); }
        else mat.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material GlassMat()
    {
        string path = MatDir + "/DioramaGlass.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetColor("_BaseColor", new Color(0.65f, 0.95f, 0.9f, 0.28f));
        mat.SetFloat("_Smoothness", 0.9f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject Prim(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static Light PointLight(Transform parent, string name, Vector3 pos, Color c, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.intensity = intensity * 0.3f; l.range = range * 0.8f; l.shadows = LightShadows.None;
        return l;
    }

    static Texture2D GradientTex(string name, Color top, Color bottom)
    {
        string path = MatDir + "/" + name + ".png";
        const int H = 128;
        var tex = new Texture2D(4, H, TextureFormat.RGBA32, false);
        for (int y = 0; y < H; y++)
        {
            Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, y / (float)(H - 1)));
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = false; imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material UnlitTexMat(string name, Texture tex)
    {
        string path = MatDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // 장면 공통 뼈대
    class Ctx
    {
        public GameObject root;
        public ProcessDioramaStage stage;
        public Transform env, facility, fx;
    }

    static Ctx Begin(FacilityType f, Color ground, Color skyTop, Color skyBottom, Color clear, Color ambient, Color keyColor, float keyIntensity, Vector3 keyEuler)
    {
        var c = new Ctx();
        c.root = new GameObject("Stage_" + f);
        c.stage = c.root.AddComponent<ProcessDioramaStage>();
        c.stage.clearColor = clear;
        c.stage.ambient = ambient;
        c.stage.camPosition = new Vector3(0f, 2.55f, -7.2f);
        c.stage.camTarget = new Vector3(0f, 0.85f, 0.4f);
        c.stage.camFov = 34f;

        c.env = new GameObject("Env3D").transform; c.env.SetParent(c.root.transform, false);
        c.facility = new GameObject("Facility").transform; c.facility.SetParent(c.root.transform, false);
        c.fx = new GameObject("Fx").transform; c.fx.SetParent(c.root.transform, false);
        c.stage.env3D = c.env.gameObject;

        // 바닥 (넓게) + 하늘/벽 그라데이션 판
        Prim(c.root.transform, PrimitiveType.Quad, "Ground", new Vector3(0f, 0f, 6f), new Vector3(40f, 40f, 1f), new Vector3(90f, 0f, 0f), Mat("Ground_" + f, ground));
        var sky = GradientTex("Sky_" + f, skyTop, skyBottom);
        Prim(c.env, PrimitiveType.Quad, "SkyBackdrop", new Vector3(0f, 6f, 16f), new Vector3(46f, 18f, 1f), Vector3.zero, UnlitTexMat("Sky_" + f, sky));

        // 생성 배경 그림(있으면)
        var bgTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BgDir + "/BG_" + f + ".png");
        var bg = Prim(c.root.transform, PrimitiveType.Quad, "PaintedBackdrop", new Vector3(0f, 2.4f, 6.5f), new Vector3(13.3f, 7.5f, 1f), new Vector3(-6f, 0f, 0f),
            bgTex != null ? UnlitTexMat("BG_" + f, bgTex) : Mat("BG_None", Color.black));
        c.stage.backdrop = bg.GetComponent<Renderer>();
        bg.SetActive(bgTex != null);
        if (bgTex != null) c.env.gameObject.SetActive(false);

        // 주 조명 (이 장면을 찍을 때만 켜진다)
        var keyGo = new GameObject("KeyLight");
        keyGo.transform.SetParent(c.root.transform, false);
        keyGo.transform.localRotation = Quaternion.Euler(keyEuler);
        var key = keyGo.AddComponent<Light>();
        key.type = LightType.Directional; key.color = keyColor; key.intensity = keyIntensity; key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.65f;
        c.stage.keyLight = key;
        return c;
    }

    static Transform Anchor(Ctx c, Vector3 pos)
    {
        var a = new GameObject("ItemAnchor").transform;
        a.SetParent(c.root.transform, false);
        a.localPosition = pos;
        c.stage.itemAnchor = a;
        return a;
    }

    static void DoneFx(Ctx c, Vector3 pos)
    {
        var done = new GameObject("DoneFx"); done.transform.SetParent(c.fx, false);
        Fx(done.transform, "FX_Sparkles_Small_01", pos, 0.9f);
        PointLight(done.transform, "DoneGlow", pos + Vector3.up * 0.3f, new Color(1f, 0.92f, 0.6f), 3f, 4f);
        done.SetActive(false);
        c.stage.doneFx = done;
    }

    static void Scatter(Transform parent, string[] names, int count, float xMin, float xMax, float zMin, float zMax, float sMin, float sMax, float keepOutX = 1.6f, float dark = 1f)
    {
        for (int i = 0; i < count; i++)
        {
            float x = R(xMin, xMax), z = R(zMin, zMax);
            if (Mathf.Abs(x) < keepOutX && z < 3f) x = Mathf.Sign(x == 0 ? 1 : x) * (keepOutX + R(0f, 1f));
            Dark(Put(parent, names[rng.Next(names.Length)], new Vector3(x, 0f, z), R(0f, 360f), R(sMin, sMax)), dark);
        }
    }

    static void Save(Ctx c, FacilityType f)
    {
        string path = OutDir + "/Stage_" + f + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(c.root, path);
        Object.DestroyImmediate(c.root);
        log.AppendLine("저장: " + path);
    }

    static void EnsureDir(string dir)
    {
        string parent = Path.GetDirectoryName(dir).Replace('\\', '/');
        string leaf = Path.GetFileName(dir);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(parent, leaf);
    }

    // ───────── 본체 ─────────

    public static string Run()
    {
        log = new StringBuilder();
        rng = new System.Random(1234);
        Cache.Clear(); DarkCache.Clear();
        EnsureDir(OutDir); EnsureDir(MatDir); EnsureDir(MatDir + "/Dark"); EnsureDir(BgDir);

        Material wood = Mat("DioramaWood", new Color(0.58f, 0.38f, 0.22f));
        Material darkWood = Mat("DioramaDarkWood", new Color(0.36f, 0.23f, 0.14f));
        Material metal = Mat("DioramaMetal", new Color(0.50f, 0.53f, 0.58f));
        Material darkMetal = Mat("DioramaDarkMetal", new Color(0.28f, 0.3f, 0.34f));
        Material copper = Mat("DioramaCopper", new Color(0.68f, 0.42f, 0.28f));
        Material rope = Mat("DioramaRope", new Color(0.78f, 0.68f, 0.48f));
        Material blade = Mat("DioramaBlade", new Color(0.42f, 0.45f, 0.5f));
        Material redPaint = Mat("DioramaRedPaint", new Color(0.66f, 0.22f, 0.18f));
        Material glow = Mat("DioramaGreenGlow", new Color(0.18f, 0.5f, 0.28f), new Color(0.15f, 0.7f, 0.3f));
        Material glass = GlassMat();

        string[] trees = { "PP_Fir_Tree_01", "PP_Fir_Tree_02", "PP_Fir_Tree_03", "PP_Fir_Tree_04", "PP_Fir_Tree_05" };
        string[] rocks = { "PP_Rock_01", "PP_Rock_02", "PP_Rock_03", "PP_Rock_Moss_01", "PP_Rock_Moss_02" };
        string[] grass = { "PP_Grass_01", "PP_Grass_02", "PP_Grass_03", "PP_Grass_04" };
        string[] shop = { "PP_Barrel_01", "PP_Barrel_02", "PP_Crate_Wooden_01", "PP_Crate_Wooden_02", "PP_Crate_Wooden_03", "PP_Bucket_01" };

        // 1) 모닥불: 숲속 야영지 (해질녘)
        {
            var c = Begin(FacilityType.Campfire, new Color(0.40f, 0.30f, 0.20f), new Color(0.30f, 0.30f, 0.55f), new Color(0.95f, 0.58f, 0.40f),
                new Color(0.16f, 0.14f, 0.24f), new Color(0.30f, 0.28f, 0.40f), new Color(1f, 0.72f, 0.5f), 0.7f, new Vector3(28f, -40f, 0f));
            Put(c.facility, "PP_Campfire_01", Vector3.zero, 0f, 0.8f);
            // 꼬치: Y자 받침 두 개 + 가로 막대
            foreach (float x in new[] { -0.75f, 0.75f })
            {
                Prim(c.facility, PrimitiveType.Cylinder, "SpitPost", new Vector3(x, 0.45f, 0f), new Vector3(0.06f, 0.45f, 0.06f), Vector3.zero, darkWood);
                Prim(c.facility, PrimitiveType.Cylinder, "SpitFork", new Vector3(x, 0.92f, 0f), new Vector3(0.05f, 0.1f, 0.05f), new Vector3(0f, 0f, 25f), darkWood);
            }
            Prim(c.facility, PrimitiveType.Cylinder, "Spit", new Vector3(0f, 0.95f, 0f), new Vector3(0.035f, 0.85f, 0.035f), new Vector3(0f, 0f, 90f), wood);
            Anchor(c, new Vector3(0f, 0.97f, -0.02f));
            c.stage.motion = ProcessDioramaStage.Motion.Roast;
            c.stage.itemSize = 0.55f;
            c.stage.workTint = new Color(0.62f, 0.42f, 0.3f);
            var idle = new GameObject("IdleFx"); idle.transform.SetParent(c.fx, false);
            Fx(idle.transform, "FX_Campfire_01", new Vector3(0f, 0.05f, 0f), 0.55f);
            Fx(idle.transform, "FX_Fireflies_01", new Vector3(0f, 1.2f, 2.5f), 1f);
            c.stage.idleFx = idle;
            var busy = new GameObject("BusyFx"); busy.transform.SetParent(c.fx, false);
            Fx(busy.transform, "FX_Embers_01", new Vector3(0f, 0.3f, 0f), 0.4f);
            Fx(busy.transform, "FX_Steam_01", new Vector3(0f, 1.15f, 0f), 0.2f);
            busy.SetActive(false);
            c.stage.busyFx = busy;
            c.stage.busyLight = PointLight(c.fx, "FireLight", new Vector3(0f, 0.7f, -0.2f), new Color(1f, 0.55f, 0.2f), 4f, 7f);
            c.stage.busyLightIntensity = 1.5f;
            PointLight(c.fx, "RimCool", new Vector3(-2.5f, 2.5f, 2.5f), new Color(0.45f, 0.5f, 1f), 1.0f, 8f);
            DoneFx(c, new Vector3(0f, 1.2f, 0f));
            // 배경
            Dark(Put(c.env, "PP_Tent_01", new Vector3(-5.2f, 0f, 8.0f), 35f, 0.85f), 0.7f);
            Dark(Put(c.env, "PP_Tent_02", new Vector3(5.4f, 0f, 8.6f), -35f, 0.85f), 0.7f);
            Put(c.env, "PP_Log_Pile_01", new Vector3(2.6f, 0f, 2.6f), -20f, 0.9f);
            Put(c.env, "PP_Log_Pile_03", new Vector3(-2.6f, 0f, 2.2f), 40f, 0.9f);
            Scatter(c.env, trees, 7, -12f, -2.8f, 10f, 16f, 0.8f, 1.1f, 0f, 0.75f);
            Scatter(c.env, trees, 7, 2.8f, 12f, 10f, 16f, 0.8f, 1.1f, 0f, 0.75f);
            Scatter(c.env, rocks, 5, -4.5f, 4.5f, 1.5f, 2.9f, 0.3f, 0.5f, 2.2f, 0.8f);
            Scatter(c.env, grass, 18, -5f, 5f, -1f, 5f, 0.8f, 1.3f);
            Save(c, FacilityType.Campfire);
        }

        // 2) 숫돌 연마대: 대장간 작업장 안
        {
            var c = Begin(FacilityType.Grindstone, new Color(0.46f, 0.33f, 0.22f), new Color(0.22f, 0.16f, 0.12f), new Color(0.38f, 0.27f, 0.18f),
                new Color(0.14f, 0.1f, 0.08f), new Color(0.27f, 0.23f, 0.21f), new Color(1f, 0.86f, 0.66f), 0.85f, new Vector3(40f, -30f, 0f));
            var gs = Put(c.facility, "PP_Grinding_Stone_01", Vector3.zero, 0f, 1.35f);
            if (gs != null && gs.transform.childCount > 0)
            {
                c.stage.spinners = new[] { gs.transform.GetChild(0) };
                c.stage.spinAxes = new[] { Vector3.forward };
                c.stage.spinSpeeds = new[] { 260f };
            }
            Anchor(c, new Vector3(0.2f, 1.32f, -0.35f));
            c.stage.motion = ProcessDioramaStage.Motion.Grind;
            c.stage.itemSize = 0.5f;
            c.stage.workTint = new Color(1f, 1f, 1.05f);
            var busy = new GameObject("BusyFx"); busy.transform.SetParent(c.fx, false);
            Fx(busy.transform, "FX_Sparks_01", new Vector3(0.2f, 1.22f, -0.4f), 0.45f);
            busy.SetActive(false);
            c.stage.busyFx = busy;
            var idle = new GameObject("IdleFx"); idle.transform.SetParent(c.fx, false);
            Fx(idle.transform, "FX_Dust_Blowing_Soft_01", new Vector3(0f, 1.5f, 1.5f), 0.6f);
            c.stage.idleFx = idle;
            c.stage.busyLight = PointLight(c.fx, "SparkLight", new Vector3(0.25f, 1.4f, -0.6f), new Color(1f, 0.75f, 0.35f), 2.5f, 4f);
            c.stage.busyLightIntensity = 1.0f;
            PointLight(c.fx, "Lantern", new Vector3(1.8f, 2.4f, 0.8f), new Color(1f, 0.7f, 0.4f), 1.6f, 7f);
            DoneFx(c, new Vector3(0.2f, 1.45f, -0.35f));
            // 작업장 벽과 소품
            for (int i = -3; i <= 3; i++) Dark(Put(c.env, "PP_Wooden_Wall_01", new Vector3(i * 2.4f, 0f, 3.2f), 0f, 1f), 0.7f);
            Put(c.env, "PP_Tool_Holder_01", new Vector3(-1.9f, 0f, 2.6f), 0f, 1.1f);
            Put(c.env, "PP_Tool_Holder_02", new Vector3(1.9f, 0f, 2.6f), 0f, 1.1f);
            Put(c.env, "PP_Anvil_Used_01_Used_Iron", new Vector3(2.0f, 0f, 1.0f), -25f, 0.9f);
            Put(c.env, "PP_Quenching_Trough_01", new Vector3(-2.2f, 0f, 1.2f), 15f, 1f);
            Scatter(c.env, shop, 7, -4.5f, 4.5f, 1.5f, 2.8f, 0.9f, 1.2f);
            Save(c, FacilityType.Grindstone);
        }

        // 3) 롤러 프레스기: 금속 작업장 (롤러가 실제로 돈다)
        {
            var c = Begin(FacilityType.RollerPress, new Color(0.40f, 0.42f, 0.44f), new Color(0.16f, 0.2f, 0.24f), new Color(0.3f, 0.34f, 0.36f),
                new Color(0.1f, 0.12f, 0.14f), new Color(0.28f, 0.30f, 0.34f), new Color(0.92f, 0.95f, 1f), 0.9f, new Vector3(45f, -35f, 0f));
            var f = c.facility;
            Prim(f, PrimitiveType.Cube, "Base", new Vector3(0f, 0.3f, 0f), new Vector3(1.7f, 0.6f, 0.9f), Vector3.zero, darkWood);
            foreach (float x in new[] { -0.75f, 0.75f })
                Prim(f, PrimitiveType.Cube, "Frame", new Vector3(x, 1.0f, 0f), new Vector3(0.14f, 0.9f, 0.6f), Vector3.zero, darkMetal);
            Prim(f, PrimitiveType.Cube, "TopBar", new Vector3(0f, 1.48f, 0f), new Vector3(1.66f, 0.12f, 0.6f), Vector3.zero, darkMetal);
            var r1 = Prim(f, PrimitiveType.Cylinder, "RollerTop", new Vector3(0f, 1.12f, 0f), new Vector3(0.36f, 0.68f, 0.36f), new Vector3(0f, 0f, 90f), metal);
            var r2 = Prim(f, PrimitiveType.Cylinder, "RollerBottom", new Vector3(0f, 0.76f, 0f), new Vector3(0.36f, 0.68f, 0.36f), new Vector3(0f, 0f, 90f), metal);
            var crank = Prim(f, PrimitiveType.Cylinder, "Crank", new Vector3(0.92f, 1.12f, 0f), new Vector3(0.6f, 0.04f, 0.6f), new Vector3(0f, 0f, 90f), redPaint);
            Prim(crank.transform, PrimitiveType.Cylinder, "Handle", new Vector3(0f, 1.5f, 0.35f), new Vector3(0.1f, 6f, 0.1f), Vector3.zero, wood);
            c.stage.spinners = new[] { r1.transform, r2.transform, crank.transform };
            c.stage.spinAxes = new[] { Vector3.up, Vector3.down, Vector3.up };
            c.stage.spinSpeeds = new[] { 200f, 200f, 120f };
            c.stage.shaker = f;
            Anchor(c, new Vector3(0f, 0.95f, -0.32f));
            c.stage.motion = ProcessDioramaStage.Motion.Press;
            c.stage.itemSize = 0.5f;
            c.stage.workTint = new Color(0.85f, 0.88f, 0.95f);
            var busy = new GameObject("BusyFx"); busy.transform.SetParent(c.fx, false);
            Fx(busy.transform, "FX_Steam_01", new Vector3(-0.6f, 1.6f, 0f), 0.22f);
            busy.SetActive(false);
            c.stage.busyFx = busy;
            c.stage.idleFx = new GameObject("IdleFx"); c.stage.idleFx.transform.SetParent(c.fx, false);
            PointLight(c.fx, "WorkLamp", new Vector3(-1.6f, 2.6f, -0.6f), new Color(1f, 0.78f, 0.45f), 3.2f, 7f);
            PointLight(c.fx, "CoolFill", new Vector3(2f, 2f, 1.5f), new Color(0.5f, 0.8f, 1f), 1.6f, 7f);
            DoneFx(c, new Vector3(0f, 1.2f, -0.32f));
            for (int i = -3; i <= 3; i++) Put(c.env, "PP_Log_Wall_01", new Vector3(i * 2.6f, 0f, 3.2f), 0f, 1f);
            Put(c.env, "PP_Anvil_Used_01_Used_Iron", new Vector3(-2.2f, 0f, 1.2f), 20f, 0.9f);
            for (int i = 0; i < 6; i++) Put(c.env, "PP_Bar_Iron_01", new Vector3(2.0f + R(-0.3f, 0.3f), 0.05f + i * 0.08f, 1.4f + R(-0.2f, 0.2f)), R(-10f, 10f), 1.2f);
            Put(c.env, "PP_Metal_Chain_01", new Vector3(-1.4f, 0f, 2.4f), 0f, 1.2f);
            Scatter(c.env, shop, 6, -4.5f, 4.5f, 1.6f, 2.8f, 0.9f, 1.2f);
            Save(c, FacilityType.RollerPress);
        }

        // 4) 절삭기: 정밀 작업대 (회전 톱날)
        {
            var c = Begin(FacilityType.PrecisionCutter, new Color(0.36f, 0.38f, 0.44f), new Color(0.14f, 0.18f, 0.26f), new Color(0.26f, 0.3f, 0.38f),
                new Color(0.08f, 0.1f, 0.14f), new Color(0.28f, 0.31f, 0.38f), new Color(0.9f, 0.95f, 1f), 0.9f, new Vector3(42f, -28f, 0f));
            var tbl = Put(c.facility, "PP_Wooden_Table_01", Vector3.zero, 0f, 0.9f);
            var f = c.facility;
            Prim(f, PrimitiveType.Cube, "Housing", new Vector3(0.1f, 1.02f, 0.12f), new Vector3(0.5f, 0.32f, 0.36f), Vector3.zero, redPaint);
            Prim(f, PrimitiveType.Cube, "Arm", new Vector3(0.1f, 1.4f, 0.12f), new Vector3(0.12f, 0.5f, 0.12f), Vector3.zero, darkMetal);
            var bladeGo = Prim(f, PrimitiveType.Cylinder, "Blade", new Vector3(0.1f, 1.0f, -0.12f), new Vector3(0.62f, 0.012f, 0.62f), new Vector3(90f, 0f, 0f), blade);
            for (int k = 0; k < 12; k++)
            {
                float a = k * 30f * Mathf.Deg2Rad;
                Prim(bladeGo.transform, PrimitiveType.Cube, "Tooth", new Vector3(Mathf.Cos(a) * 0.52f, 0f, Mathf.Sin(a) * 0.52f), new Vector3(0.08f, 1.2f, 0.08f), new Vector3(0f, -k * 30f + 45f, 0f), blade);
            }
            c.stage.spinners = new[] { bladeGo.transform };
            c.stage.spinAxes = new[] { Vector3.up };
            c.stage.spinSpeeds = new[] { 720f };
            c.stage.shaker = f;
            Anchor(c, new Vector3(0.1f, 0.92f, -0.42f));
            c.stage.motion = ProcessDioramaStage.Motion.Cut;
            c.stage.itemSize = 0.45f;
            c.stage.workTint = new Color(0.95f, 0.95f, 1f);
            var busy = new GameObject("BusyFx"); busy.transform.SetParent(c.fx, false);
            Fx(busy.transform, "FX_Sparks_01", new Vector3(0.1f, 0.85f, -0.45f), 0.4f);
            busy.SetActive(false);
            c.stage.busyFx = busy;
            c.stage.idleFx = new GameObject("IdleFx"); c.stage.idleFx.transform.SetParent(c.fx, false);
            c.stage.busyLight = PointLight(c.fx, "SparkLight", new Vector3(0.1f, 1.0f, -0.7f), new Color(1f, 0.8f, 0.4f), 2.2f, 3.5f);
            c.stage.busyLightIntensity = 0.7f;
            PointLight(c.fx, "DeskLamp", new Vector3(-1.2f, 2.2f, -0.4f), new Color(1f, 0.85f, 0.6f), 1.6f, 6f);
            PointLight(c.fx, "CoolFill", new Vector3(2f, 2.4f, 1.2f), new Color(0.55f, 0.75f, 1f), 1.8f, 7f);
            DoneFx(c, new Vector3(0.1f, 1.1f, -0.42f));
            for (int i = -3; i <= 3; i++) Put(c.env, "PP_Wooden_Wall_02", new Vector3(i * 2.4f, 0f, 3.2f), 0f, 1f);
            Put(c.env, "PP_Wooden_Table_02", new Vector3(-2.4f, 0f, 2.0f), 0f, 1f);
            Put(c.env, "PP_Paper_Roll_01", new Vector3(-2.4f, 0.85f, 2.0f), 30f, 1.3f);
            Put(c.env, "PP_Candle_Holder_01", new Vector3(-2.0f, 0.85f, 2.1f), 0f, 1.2f);
            Put(c.env, "PP_Tool_Holder_01", new Vector3(2.3f, 0f, 2.6f), 0f, 1.1f);
            Scatter(c.env, shop, 5, -4.5f, 4.5f, 1.6f, 2.8f, 0.9f, 1.2f);
            Save(c, FacilityType.PrecisionCutter);
        }

        // 5) 화학 정제탑: 지하 실험실 (초록 거품이 끓는다)
        {
            var c = Begin(FacilityType.ChemicalRefinery, new Color(0.30f, 0.32f, 0.32f), new Color(0.06f, 0.14f, 0.16f), new Color(0.12f, 0.24f, 0.24f),
                new Color(0.04f, 0.08f, 0.09f), new Color(0.20f, 0.27f, 0.29f), new Color(0.75f, 1f, 0.9f), 0.65f, new Vector3(50f, -30f, 0f));
            var f = c.facility;
            Prim(f, PrimitiveType.Cylinder, "Stand", new Vector3(-0.45f, 0.25f, 0f), new Vector3(0.7f, 0.25f, 0.7f), Vector3.zero, darkMetal);
            Prim(f, PrimitiveType.Cylinder, "Boiler", new Vector3(-0.45f, 0.85f, 0f), new Vector3(0.62f, 0.36f, 0.62f), Vector3.zero, copper);
            Prim(f, PrimitiveType.Sphere, "BoilerTop", new Vector3(-0.45f, 1.2f, 0f), new Vector3(0.62f, 0.32f, 0.62f), Vector3.zero, copper);
            for (int k = 0; k < 5; k++)
                Prim(f, PrimitiveType.Cylinder, "Tube", new Vector3(-0.3f + k * 0.13f, 1.35f + Mathf.Sin(k * 0.8f) * 0.08f, 0f), new Vector3(0.05f, 0.08f, 0.05f), new Vector3(0f, 0f, 70f), copper);
            Prim(f, PrimitiveType.Cylinder, "FlaskStand", new Vector3(0.5f, 0.2f, 0f), new Vector3(0.5f, 0.2f, 0.5f), Vector3.zero, darkWood);
            Prim(f, PrimitiveType.Sphere, "Liquid", new Vector3(0.5f, 0.68f, 0f), new Vector3(0.6f, 0.5f, 0.6f), Vector3.zero, glow);
            Prim(f, PrimitiveType.Sphere, "Flask", new Vector3(0.5f, 0.75f, 0f), new Vector3(0.78f, 0.78f, 0.78f), Vector3.zero, glass);
            Prim(f, PrimitiveType.Cylinder, "FlaskNeck", new Vector3(0.5f, 1.3f, 0f), new Vector3(0.18f, 0.25f, 0.18f), Vector3.zero, glass);
            Anchor(c, new Vector3(0.5f, 0.82f, -0.42f));
            c.stage.motion = ProcessDioramaStage.Motion.Bubble;
            c.stage.itemSize = 0.42f;
            c.stage.workTint = new Color(0.75f, 1f, 0.85f);
            var idle = new GameObject("IdleFx"); idle.transform.SetParent(c.fx, false);
            Fx(idle.transform, "FX_Bubbles_Float_01", new Vector3(0.5f, 0.95f, 0f), 0.18f);
            c.stage.idleFx = idle;
            var busy = new GameObject("BusyFx"); busy.transform.SetParent(c.fx, false);
            Fx(busy.transform, "FX_Steam_01", new Vector3(0.5f, 1.5f, 0f), 0.2f, new Color(0.7f, 1f, 0.8f, 0.6f));
            Fx(busy.transform, "FX_Bubbles_Float_01", new Vector3(0.5f, 0.9f, 0f), 0.3f);
            busy.SetActive(false);
            c.stage.busyFx = busy;
            c.stage.busyLight = PointLight(c.fx, "LiquidGlow", new Vector3(0.5f, 0.8f, -0.6f), new Color(0.35f, 1f, 0.5f), 3f, 5f);
            c.stage.busyLightIntensity = 1.3f;
            PointLight(c.fx, "Burner", new Vector3(-0.45f, 0.4f, -0.5f), new Color(1f, 0.55f, 0.25f), 0.8f, 2.5f);
            PointLight(c.fx, "Violet", new Vector3(-2f, 2.4f, 1.5f), new Color(0.6f, 0.4f, 1f), 2f, 8f);
            DoneFx(c, new Vector3(0.5f, 1.1f, -0.42f));
            Dark(Put(c.env, "PP_Cave_Wall_Curved_01", new Vector3(-3.5f, 0f, 4f), 180f, 1f), 0.35f);
            Dark(Put(c.env, "PP_Cave_Wall_Curved_02", new Vector3(3.5f, 0f, 4f), 180f, 1f), 0.35f);
            Put(c.env, "PP_Wooden_Table_02", new Vector3(-2.2f, 0f, 1.8f), 10f, 1f);
            string[] bottles = { "PP_Drinking_Bottle_01", "PP_Drinking_Bottle_02", "PP_Drinking_Bottle_03", "PP_Inkbottle_01" };
            for (int i = 0; i < 6; i++) Put(c.env, bottles[i % bottles.Length], new Vector3(-2.6f + i * 0.18f, 0.84f, 1.8f + R(-0.15f, 0.15f)), R(0f, 360f), 1.3f);
            string[] mush = { "PP_Mushrooms_Brown_01", "PP_Mushrooms_Brown_02", "PP_Mushrooms_Stone_Brown_01" };
            Scatter(c.env, mush, 6, -4f, 4f, 0.8f, 3.2f, 0.8f, 1.2f);
            Scatter(c.env, new[] { "PP_Barrel_01", "PP_Barrel_02", "PP_Bucket_01" }, 4, -4f, 4f, 1.4f, 2.8f, 0.9f, 1.1f);
            Save(c, FacilityType.ChemicalRefinery);
        }

        // 6) 초고온 용광로: 붉은 대장간 동굴
        {
            var c = Begin(FacilityType.SuperheatedFurnace, new Color(0.30f, 0.20f, 0.17f), new Color(0.12f, 0.05f, 0.05f), new Color(0.42f, 0.14f, 0.08f),
                new Color(0.08f, 0.03f, 0.03f), new Color(0.24f, 0.16f, 0.15f), new Color(1f, 0.7f, 0.5f), 0.6f, new Vector3(35f, -30f, 0f));
            var fur = Dark(Put(c.facility, "PP_Furnace_03", new Vector3(0f, 0f, 0.6f), 180f, 0.75f), 0.72f);
            Anchor(c, new Vector3(0f, 0.42f, 0.0f));
            c.stage.motion = ProcessDioramaStage.Motion.Heat;
            c.stage.itemSize = 0.5f;
            c.stage.workTint = new Color(1f, 0.55f, 0.3f);
            var idle = new GameObject("IdleFx"); idle.transform.SetParent(c.fx, false);
            Fx(idle.transform, "FX_Fire_Big_01", new Vector3(0f, 0.08f, 0.45f), 0.22f);
            Fx(idle.transform, "FX_Embers_01", new Vector3(0f, 0.6f, 0f), 0.6f);
            c.stage.idleFx = idle;
            var busy = new GameObject("BusyFx"); busy.transform.SetParent(c.fx, false);
            Fx(busy.transform, "FX_Fire_Big_01", new Vector3(0f, 0.12f, 0.25f), 0.3f);
            busy.SetActive(false);
            c.stage.busyFx = busy;
            c.stage.busyLight = PointLight(c.fx, "FurnaceLight", new Vector3(0f, 0.5f, -0.6f), new Color(1f, 0.45f, 0.15f), 4f, 5f);
            c.stage.busyLightIntensity = 1.8f;
            PointLight(c.fx, "LavaBack", new Vector3(0f, 1.5f, 3.5f), new Color(1f, 0.3f, 0.1f), 1.4f, 7f);
            DoneFx(c, new Vector3(0f, 0.8f, -0.2f));
            Dark(Put(c.env, "PP_Cave_Wall_Curved_03", new Vector3(-3.6f, 0f, 4.2f), 180f, 1f), 0.42f);
            Dark(Put(c.env, "PP_Cave_Wall_Curved_04", new Vector3(3.6f, 0f, 4.2f), 180f, 1f), 0.42f);
            Put(c.env, "PP_Anvil_Used_01_Used_Iron", new Vector3(1.9f, 0f, 0.6f), -30f, 0.9f);
            Put(c.env, "PP_Bellows_01", new Vector3(-1.6f, 0.02f, 0.4f), 60f, 1.6f);
            for (int i = 0; i < 5; i++) Put(c.env, "PP_Bar_Gold_01", new Vector3(2.2f + R(-0.2f, 0.2f), 0.05f + i * 0.08f, 1.6f), R(-10f, 10f), 1.2f);
            Put(c.env, "PP_Metal_Chain_02", new Vector3(-2.4f, 0f, 2.2f), 0f, 1.3f);
            Scatter(c.env, rocks, 7, -5f, 5f, 1f, 4f, 0.5f, 0.9f, 1.6f, 0.6f);
            Save(c, FacilityType.SuperheatedFurnace);
        }

        // 7) 가죽 무두질 건조대: 햇살 좋은 마당
        {
            var c = Begin(FacilityType.LeatherTanningRack, new Color(0.42f, 0.48f, 0.26f), new Color(0.5f, 0.72f, 0.95f), new Color(0.96f, 0.9f, 0.72f),
                new Color(0.55f, 0.75f, 0.95f), new Color(0.36f, 0.38f, 0.38f), new Color(1f, 0.95f, 0.82f), 1.0f, new Vector3(48f, -35f, 0f));
            var f = c.facility;
            foreach (float x in new[] { -0.8f, 0.8f })
                Prim(f, PrimitiveType.Cylinder, "Post", new Vector3(x, 0.85f, 0f), new Vector3(0.1f, 0.85f, 0.1f), new Vector3(0f, 0f, x * 4f), darkWood);
            Prim(f, PrimitiveType.Cylinder, "TopBar", new Vector3(0f, 1.62f, 0f), new Vector3(0.08f, 0.9f, 0.08f), new Vector3(0f, 0f, 90f), wood);
            Prim(f, PrimitiveType.Cylinder, "LowBar", new Vector3(0f, 0.32f, 0f), new Vector3(0.07f, 0.85f, 0.07f), new Vector3(0f, 0f, 90f), wood);
            foreach (float x in new[] { -0.35f, 0.35f })
                Prim(f, PrimitiveType.Cylinder, "Rope", new Vector3(x, 1.45f, 0f), new Vector3(0.02f, 0.17f, 0.02f), Vector3.zero, rope);
            Anchor(c, new Vector3(0f, 1.02f, -0.05f));
            c.stage.motion = ProcessDioramaStage.Motion.Dry;
            c.stage.itemSize = 0.75f;
            c.stage.workTint = new Color(0.8f, 0.6f, 0.42f);
            var idle = new GameObject("IdleFx"); idle.transform.SetParent(c.fx, false);
            Fx(idle.transform, "FX_Leaves_Green_01", new Vector3(0f, 2.5f, 1f), 0.8f);
            c.stage.idleFx = idle;
            var busy = new GameObject("BusyFx"); busy.transform.SetParent(c.fx, false);
            Fx(busy.transform, "FX_Dust_Small_01", new Vector3(0f, 0.6f, -0.2f), 0.6f);
            busy.SetActive(false);
            c.stage.busyFx = busy;
            DoneFx(c, new Vector3(0f, 1.3f, -0.05f));
            for (int i = -3; i <= 3; i++) Put(c.env, "PP_Wooden_Fence_01", new Vector3(i * 2.2f, 0f, 3.4f), 0f, 1f);
            Scatter(c.env, new[] { "PP_Tree_01", "PP_Tree_02", "PP_Tree_03" }, 8, -10f, 10f, 8f, 13f, 0.8f, 1.1f, 0f, 0.72f);
            Scatter(c.env, new[] { "PP_Barrel_01", "PP_Bucket_01", "PP_Crate_Wooden_01" }, 4, -4f, 4f, 1.2f, 2.8f, 0.9f, 1.1f);
            Scatter(c.env, grass, 22, -5f, 5f, -1f, 5f, 0.9f, 1.4f);
            Save(c, FacilityType.LeatherTanningRack);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return log.ToString();
    }
}
#endif
