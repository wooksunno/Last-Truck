#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using CraftingSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 안개 늪 동쪽의 "독버섯 군락" (Map/10_Expansion/MysticGrove) - 신비로운 분위기의 빛나는 독버섯 숲.
///
///  - 바닥이 청록빛으로 물들고, 거대한 독버섯(보라/청록/분홍)이 둥글게 자란다. 버섯 색은 원래 버섯 팔레트를 색만 바꿔서(밝기는 그대로)
///    스스로 빛나게(emission) 만든 재질을 쓴다. 버섯마다 같은 색의 은은한 빛이 따라붙는다.
///  - 한가운데는 룬 석상 원과 기둥(청록 빛). 독약초(보라 덤불)가 많이 자라 채집할 수 있다.
///  - 공중에는 느리게 떠오르는 포자(파티클)가 흩날리고, 군락 가운데에 큰 보랏빛 조명이 깔린다.
///  - 중심 좌표는 고정이라 같은 seed면 항상 같은 자리에 만들어진다 (GroveCenter).
///  - 들어가면 "독버섯 군락" 구역 이름이 뜬다 (ZoneMarker).
/// </summary>
public static partial class OpenWorldExpansionBuilder
{
    const string GroveDir = AssetDir + "/Grove";
    public static readonly Vector2 GroveCenter = new Vector2(565f, -110f);
    public const float GroveRadius = 46f;

    /// <summary>군락 안 땅 색 (팔레트 1번 줄의 청록 칸만 써서 이웃 이끼색과 섞여도 다른 색으로 새지 않는다). 가장자리 바깥은 false.</summary>
    static bool GroveGround(float x, float z, out Vector2 uv)
    {
        // 팔레트의 청록 칸(1번 줄 0~3열)은 이 게임의 툰 셰이더에서 형광 파랑으로 번쩍여서 쓰지 않는다.
        // 땅은 원래 늪의 이끼색 그대로 두고, 빛나는 버섯 + 보랏빛 조명이 땅을 물들이게 한다.
        uv = default;
        return false;
    }

    static bool InGrove(float x, float z, float margin) => Vector2.Distance(new Vector2(x, z), GroveCenter) < GroveRadius + margin;

    // ---------------------------------------------------------------- 재질 (색만 바꾼 팔레트 + 발광)

    static Material GlowMaterial(string name, float hue, float satScale, float emission)
    {
        if (!AssetDatabase.IsValidFolder(GroveDir)) AssetDatabase.CreateFolder(AssetDir, "Grove");
        const string palettePath = "Assets/PurePoly/Mining_Pack/Textures/PP_Color_Palette.png";
        string texPath = GroveDir + "/Palette_" + name + ".png";

        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(System.IO.File.ReadAllBytes(palettePath));
        var px = src.GetPixels();
        for (int i = 0; i < px.Length; i++)
        {
            Color.RGBToHSV(px[i], out float h, out float s, out float v);
            float ns = Mathf.Clamp01(Mathf.Lerp(0.45f, 1f, s) * satScale);   // 회색 칸도 약하게 물든다
            float nv = Mathf.Clamp01(v * 0.85f + 0.12f);
            Color c = Color.HSVToRGB(hue, ns, nv);
            c.a = px[i].a;
            px[i] = c;
        }
        var dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        dst.SetPixels(px); dst.Apply();
        System.IO.File.WriteAllBytes(texPath, dst.EncodeToPNG());
        Object.DestroyImmediate(src); Object.DestroyImmediate(dst);
        AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);

        var orig = AssetImporter.GetAtPath(palettePath) as TextureImporter;
        var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (imp != null && orig != null)
        {
            imp.filterMode = orig.filterMode; imp.wrapMode = orig.wrapMode; imp.mipmapEnabled = orig.mipmapEnabled;
            imp.textureCompression = TextureImporterCompression.Uncompressed; imp.sRGBTexture = orig.sRGBTexture;
            imp.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        string matPath = GroveDir + "/Glow_" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (m == null)
        {
            var baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PurePoly/Mining_Pack/Materials/PP_Standard_Material.mat");
            m = new Material(baseMat) { name = "Glow_" + name };
            AssetDatabase.CreateAsset(m, matPath);
        }
        m.SetTexture("_BaseMap", tex);
        m.SetTexture("_EmissionMap", tex);
        m.SetColor("_EmissionColor", new Color(emission, emission, emission, 1f));
        m.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(m);
        return m;
    }

    static Texture2D SoftDotTexture()
    {
        if (!AssetDatabase.IsValidFolder(GroveDir)) AssetDatabase.CreateFolder(AssetDir, "Grove");
        string path = GroveDir + "/SoftDot.png";
        int S = 64;
        var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float dx = (x - S * 0.5f) / (S * 0.5f), dy = (y - S * 0.5f) / (S * 0.5f);
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            a = a * a * (3f - 2f * a);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply();
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null) { imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float GroveMaxDim(string prefab)
    {
        var pf = P(prefab);
        if (pf == null) return 1f;
        float d = 1f; Bounds b = new Bounds(); bool first = true;
        foreach (var mf in pf.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Vector3 sc = mf.transform.lossyScale; Bounds mb = mf.sharedMesh.bounds;
            Vector3 size = new Vector3(mb.size.x * Mathf.Abs(sc.x), mb.size.y * Mathf.Abs(sc.y), mb.size.z * Mathf.Abs(sc.z));
            if (first) { b = new Bounds(Vector3.zero, size); first = false; } else b.Encapsulate(new Bounds(Vector3.zero, size));
        }
        if (!first) d = Mathf.Max(0.1f, b.size.x, b.size.y, b.size.z);
        return d;
    }

    static Light AddGlow(Transform parent, Vector3 worldPos, Color c, float range, float intensity)
    {
        var g = new GameObject("Glow");
        g.transform.SetParent(parent, true);
        g.transform.position = worldPos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        return l;
    }

    // ---------------------------------------------------------------- 만들기

    static string BuildGrove()
    {
        var sb = new StringBuilder();
        if (!Ground(GroveCenter.x, GroveCenter.y, out float cy, out _)) return "grove: 중심에 땅이 없어요";

        var root = new GameObject("MysticGrove").transform;
        root.SetParent(_root, false);
        root.position = new Vector3(GroveCenter.x, cy, GroveCenter.y);
        var zone = root.gameObject.AddComponent<LastTruck.ZoneMarker>();
        zone.zoneName = "독버섯 군락";
        zone.hint = "빛나는 독버섯이 피어난 신비로운 숲. 독약초가 가득하다";
        zone.radius = GroveRadius - 2f; zone.maxY = 0f;

        // 재질: 보라 / 청록 / 분홍 (+ 룬 청록)
        Material violet = GlowMaterial("Violet", 0.76f, 0.9f, 0.7f);
        Material teal = GlowMaterial("Teal", 0.49f, 0.9f, 0.7f);
        Material pink = GlowMaterial("Pink", 0.89f, 0.85f, 0.65f);
        Material rune = GlowMaterial("Rune", 0.52f, 0.7f, 1.3f);
        Material[] mats = { violet, violet, teal, teal, pink };
        Color[] glow = { new Color(0.62f, 0.38f, 1f), new Color(0.62f, 0.38f, 1f), new Color(0.3f, 0.9f, 0.85f), new Color(0.3f, 0.9f, 0.85f), new Color(1f, 0.45f, 0.85f) };

        var gGiant = new GameObject("GiantMushrooms").transform; gGiant.SetParent(root, false);
        var gSmall = new GameObject("SmallMushrooms").transform; gSmall.SetParent(root, false);
        var gHerb = new GameObject("PoisonHerbs").transform; gHerb.SetParent(root, false);
        var gRune = new GameObject("RuneCircle").transform; gRune.SetParent(root, false);
        var gLight = new GameObject("Lights").transform; gLight.SetParent(root, false);

        Vector3 center = root.position;

        // ---- 가운데 큰 보랏빛 조명 (군락 전체가 물든다)
        AddGlow(gLight, center + Vector3.up * 14f, new Color(0.55f, 0.35f, 1f), 60f, 1.2f);
        AddGlow(gLight, center + new Vector3(18f, 8f, -12f), new Color(0.3f, 0.9f, 0.85f), 34f, 0.9f);
        AddGlow(gLight, center + new Vector3(-20f, 8f, 14f), new Color(1f, 0.45f, 0.85f), 34f, 0.8f);

        // ---- 룬 석상 원 + 기둥 (가운데)
        int runes = 0;
        for (int k = 0; k < 9; k++)
        {
            float a = k * Mathf.PI * 2f / 9f;
            var s = PutOnGround("PP_Rune_Stone_" + (1 + _rng.Next(12)).ToString("D2"), gRune, center.x + Mathf.Cos(a) * 11f, center.z + Mathf.Sin(a) * 11f, Rng(3.0f, 3.8f), 0.3f, 0f, false);
            if (s == null) continue;
            s.transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Cos(a), 0f, -Mathf.Sin(a)));
            Recolor(s, rune);
            runes++;
        }
        var pillar = PutOnGround("PP_Pillar_Stone_Rune_0" + (1 + _rng.Next(3)), gRune, center.x, center.z, 4.2f, 0.3f, 0f, false);
        if (pillar != null) { Recolor(pillar, rune); AddGlow(gLight, pillar.transform.position + Vector3.up * 7f, new Color(0.3f, 0.95f, 0.9f), 34f, 3.2f); }

        // ---- 거대한 독버섯 (둥글게, 서로 7m 이상 떨어져서)
        var placed = new List<Vector2>();
        int giants = 0;
        string[] giantNames = new string[21];
        for (int i = 0; i < 11; i++) giantNames[i] = "PP_Mushroom_Long_Dark_Brown_" + (i + 1).ToString("D2");
        for (int i = 0; i < 10; i++) giantNames[11 + i] = "PP_Mushroom_Dark_Brown_" + (i + 1).ToString("D2");
        for (int t = 0; t < 400 && giants < 30; t++)
        {
            float r = Mathf.Sqrt(Rng(18f * 18f, (GroveRadius - 6f) * (GroveRadius - 6f)));
            float a = Rng(0f, Mathf.PI * 2f);
            float x = center.x + Mathf.Cos(a) * r, z = center.z + Mathf.Sin(a) * r;
            bool ok = true;
            foreach (var q in placed) if ((q - new Vector2(x, z)).sqrMagnitude < 7f * 7f) { ok = false; break; }
            if (!ok) continue;
            string name = giantNames[_rng.Next(giantNames.Length)];
            float target = Rng(9f, 19f);
            var go = PutOnGround(name, gGiant, x, z, target / GroveMaxDim(name), 0.3f, 0.2f, false);
            if (go == null) continue;
            int mi = _rng.Next(mats.Length);
            Recolor(go, mats[mi]);
            placed.Add(new Vector2(x, z));
            giants++;
            if (R01() < 0.6f)
            {
                var rend = go.GetComponentInChildren<Renderer>();
                Vector3 top = rend != null ? rend.bounds.center + Vector3.up * (rend.bounds.extents.y * 0.55f) : go.transform.position + Vector3.up * 6f;
                AddGlow(gLight, top, glow[mi], Rng(14f, 22f), Rng(1.6f, 2.8f));
            }
        }

        // ---- 작은 독버섯 (바닥 가득, 빛은 없지만 스스로 빛나는 재질)
        int smallCount = 0;
        for (int t = 0; t < 260 && smallCount < 110; t++)
        {
            float r = Mathf.Sqrt(Rng(0f, (GroveRadius - 2f) * (GroveRadius - 2f)));
            float a = Rng(0f, Mathf.PI * 2f);
            float x = center.x + Mathf.Cos(a) * r, z = center.z + Mathf.Sin(a) * r;
            string name = Pick("PP_Mushrooms_Brown_0" + (1 + _rng.Next(7)), "PP_Mushrooms_Stone_Brown_0" + (1 + _rng.Next(3)), "PP_Mushroom_Dark_Brown_0" + (1 + _rng.Next(9)));
            var go = PutOnGround(name, gSmall, x, z, Rng(2.4f, 4.2f) / GroveMaxDim(name) * 1.0f, 0.05f, 0.5f, false);
            if (go == null) continue;
            Recolor(go, mats[_rng.Next(mats.Length)]);
            smallCount++;
        }

        // ---- 독약초 (보라 덤불, 채집 가능)
        var poisonMat = TintedMaterial("PP_PoisonHerb_Material", new Color(0.72f, 0.42f, 0.95f));
        int herbs = 0;
        for (int c = 0; c < 22; c++)
        {
            float r = Mathf.Sqrt(Rng(6f * 6f, (GroveRadius - 4f) * (GroveRadius - 4f)));
            float a = Rng(0f, Mathf.PI * 2f);
            float cx = center.x + Mathf.Cos(a) * r, cz = center.z + Mathf.Sin(a) * r;
            int k = 2 + _rng.Next(3);
            for (int q = 0; q < k; q++)
            {
                var go = PutOnGround("PP_Grass_0" + (1 + _rng.Next(6)), gHerb, cx + Rng(-3f, 3f), cz + Rng(-3f, 3f), Rng(2.2f, 2.8f), 0.05f, 0.5f, false);
                if (go == null) continue;
                Recolor(go, poisonMat);
                MakeGatherable(go, "독약초", ItemIds.PoisonHerb, 1, GatherablePropInstaller.HerbHoldSeconds);
                herbs++;
            }
        }

        // ---- 포자 (느리게 떠오르는 빛 알갱이)
        var dot = SoftDotTexture();
        string spMatPath = GroveDir + "/Spore.mat";
        var spMat = AssetDatabase.LoadAssetAtPath<Material>(spMatPath);
        if (spMat == null) { spMat = new Material(Shader.Find("Sprites/Default")) { name = "Spore" }; AssetDatabase.CreateAsset(spMat, spMatPath); }
        spMat.mainTexture = dot; spMat.color = Color.white;
        EditorUtility.SetDirty(spMat);

        var sp = new GameObject("Spores"); sp.transform.SetParent(root, false); sp.transform.localPosition = Vector3.up * 5f;
        var ps = sp.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true; main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
        main.startSpeed = 0.15f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.95f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.5f, 1f, 0.85f), new Color(0.4f, 1f, 0.9f, 0.85f));
        main.maxParticles = 900;
        var em = ps.emission; em.rateOverTime = 55f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(GroveRadius * 1.7f, 12f, GroveRadius * 1.7f);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.y = new ParticleSystem.MinMaxCurve(0.35f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.7f; noise.frequency = 0.25f; noise.scrollSpeed = 0.2f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);
        var pr = sp.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = spMat; pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
        ps.Play();

        sb.AppendLine($"grove: center=({GroveCenter.x:F0},{GroveCenter.y:F0}) giant mushrooms={giants} small={smallCount} poison herbs={herbs} runes={runes}");
        return sb.ToString();
    }
}
#endif
