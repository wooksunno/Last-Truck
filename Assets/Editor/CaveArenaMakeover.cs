#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// "숲 광산(ForestMine)" 가운데 둥근 광장(룬 기둥 10개가 둘러선 곳)을 꾸민다. 기존 것은 하나도 지우지 않고 위에 얹는다.
///  - 가운데: 3단 제단 + 큰 보라/청록 수정 + 네 귀퉁이 화로
///  - 바닥: 룬 문자가 빛나는 마법진 고리 3개 + 바깥으로 뻗는 빛줄기 길 8개
///  - 기둥 10개에서 가운데 수정으로 에너지 줄기가 맥박처럼 이어진다
///  - 사이사이: 석순, 수정 무더기(빛 포함), 이끼 바위, 버섯, 무너진 기둥, 석상, 보물상자
///  - 분위기: 반딧불/마법 빛 입자, 수정 불빛
/// "ForestMine/Makeover" 아래에 모아 두며, 다시 실행하면 지난번 것을 지우고 새로 만든다.
/// </summary>
public static class CaveArenaMakeover
{
    const string GroupName = "Makeover";
    const string ArtDir = "Assets/Art/CaveMakeover";
    static readonly Vector2 Center = new Vector2(-355f, 372f);

    static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();
    static System.Random rng;
    static int placed;

    static GameObject Load(string name)
    {
        if (PrefabCache.TryGetValue(name, out var g)) return g;
        GameObject found = null;
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name) { found = AssetDatabase.LoadAssetAtPath<GameObject>(path); break; }
        }
        PrefabCache[name] = found;
        return found;
    }

    static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    // 광장 바닥(Shell_*)의 높이. 소품이나 기둥 위가 아니라 진짜 바닥만 잡는다.
    static bool Ground(float x, float z, out Vector3 p)
    {
        p = default;
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, 40f, z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; bool ok = false;
        foreach (var h in hits)
        {
            if (!h.collider.name.StartsWith("Shell_") || h.normal.y < 0.75f) continue;
            if (h.distance < best) { best = h.distance; p = h.point; ok = true; }
        }
        return ok;
    }

    static Vector2 Polar(float radius, float angDeg) => Center + new Vector2(Mathf.Cos(angDeg * Mathf.Deg2Rad), Mathf.Sin(angDeg * Mathf.Deg2Rad)) * radius;

    static GameObject Place(Transform parent, string prefabName, Vector3 pos, float yaw, float scale, bool removeColliders = false, Vector3? tilt = null)
    {
        GameObject pf = Load(prefabName);
        if (pf == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(tilt ?? Vector3.zero) * Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = pf.transform.localScale * scale;
        if (removeColliders) foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        placed++;
        return go;
    }

    static Light AddLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range, bool pulse = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        if (pulse) go.AddComponent<CaveGlowPulse>().Configure(R(1.4f, 2.8f), 0.22f, R(0f, 6f));
        return l;
    }

    // ───────────── 재질 / 메쉬 / 글자 무늬 ─────────────

    static Material MakeMat(string name, Color baseColor, Color emission, Texture2D emissionMap = null)
    {
        string path = ArtDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("LastTruck/ToonWorld")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_EmissionColor", emission);
        mat.EnableKeyword("_EMISSION");
        if (emissionMap != null) mat.SetTexture("_EmissionMap", emissionMap);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void Recolor(GameObject go, Material mat)
    {
        if (go == null || mat == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
    }

    static Texture2D MakeRuneTexture()
    {
        string path = ArtDir + "/RuneBand.png";
        const int W = 2048, H = 128, Cells = 32;
        var px = new Color32[W * H];
        var rr = new System.Random(77);
        System.Action<int, int, int, int, int> line = (x0, y0, x1, y1, th) =>
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int s = 0; s <= steps; s++)
            {
                float t = steps == 0 ? 0 : s / (float)steps;
                int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int dx = -th; dx <= th; dx++)
                    for (int dy = -th; dy <= th; dy++)
                    {
                        int X = (cx + dx + W) % W, Y = cy + dy;
                        if (Y >= 0 && Y < H) px[Y * W + X] = new Color32(255, 255, 255, 255);
                    }
            }
        };
        // 위아래 테두리 선
        line(0, 7, W - 1, 7, 2); line(0, H - 8, W - 1, H - 8, 2);
        for (int c = 0; c < Cells; c++)
        {
            int x = c * (W / Cells); int cw = W / Cells;
            int a = x + cw / 2;
            int kind = rr.Next(8);
            switch (kind)
            {
                case 0: line(a, 26, a, 102, 2); line(a, 64, a + 16, 42, 2); line(a, 84, a + 16, 62, 2); break;
                case 1: line(a - 14, 26, a - 14, 102, 2); line(a - 14, 102, a + 14, 80, 2); line(a + 14, 80, a - 14, 58, 2); break;
                case 2: line(a - 14, 26, a + 14, 102, 2); line(a + 14, 26, a - 14, 102, 2); break;
                case 3: line(a, 26, a - 15, 64, 2); line(a - 15, 64, a, 102, 2); line(a, 102, a + 15, 64, 2); line(a + 15, 64, a, 26, 2); break;
                case 4: line(a - 14, 30, a + 14, 30, 2); line(a, 30, a, 100, 2); line(a - 14, 100, a + 14, 100, 2); break;
                case 5: line(a - 14, 26, a - 14, 102, 2); line(a - 14, 64, a + 14, 102, 2); line(a - 14, 64, a + 14, 26, 2); break;
                case 6: line(a, 26, a, 102, 2); line(a - 15, 80, a, 64, 2); line(a, 64, a + 15, 80, 2); line(a - 15, 44, a + 15, 44, 2); break;
                default: line(a - 14, 40, a + 14, 40, 2); line(a - 14, 88, a + 14, 88, 2); line(a - 14, 40, a - 14, 88, 2); line(a + 14, 40, a + 14, 88, 2); break;
            }
        }
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.SetPixels32(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.sRGBTexture = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 바닥의 굴곡을 따라가는 고리 모양 메쉬 (rInner~rOuter). repeats는 글자 무늬가 둘레에 몇 번 반복되는지(정수).
    static Mesh MakeRing(string name, float rInner, float rOuter, int segments, int repeats)
    {
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        int rows = 3;
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            for (int r = 0; r <= rows; r++)
            {
                float rad = Mathf.Lerp(rInner, rOuter, r / (float)rows);
                float x = Center.x + Mathf.Cos(a) * rad, z = Center.y + Mathf.Sin(a) * rad;
                float y = Ground(x, z, out Vector3 g) ? g.y + 0.07f : 1.5f;
                verts.Add(new Vector3(x, y, z));
                uvs.Add(new Vector2(i / (float)segments * repeats, r / (float)rows));
            }
        }
        for (int i = 0; i < segments; i++)
            for (int r = 0; r < rows; r++)
            {
                int a = i * (rows + 1) + r, b = a + 1, c = a + (rows + 1), d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        // 월드 좌표로 만든 메쉬라서 위치는 원점에 둔다
        string path = ArtDir + "/" + name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { EditorUtility.CopySerialized(m, old); return old; }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // 중심에서 바깥으로 뻗는 길(막대): 바닥 굴곡을 따라간다.
    static Mesh MakeSpoke(string name, float angDeg, float r0, float r1, float width)
    {
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        int steps = Mathf.Max(8, Mathf.RoundToInt((r1 - r0) / 1.2f));
        Vector2 dir = new Vector2(Mathf.Cos(angDeg * Mathf.Deg2Rad), Mathf.Sin(angDeg * Mathf.Deg2Rad));
        Vector2 side = new Vector2(-dir.y, dir.x);
        for (int i = 0; i <= steps; i++)
        {
            float r = Mathf.Lerp(r0, r1, i / (float)steps);
            for (int s = 0; s < 2; s++)
            {
                Vector2 p = Center + dir * r + side * (s == 0 ? -width * 0.5f : width * 0.5f);
                float y = Ground(p.x, p.y, out Vector3 g) ? g.y + 0.07f : 1.5f;
                verts.Add(new Vector3(p.x, y, p.y)); uvs.Add(new Vector2(s, i / (float)steps));
            }
        }
        for (int i = 0; i < steps; i++) { int a = i * 2; tris.Add(a); tris.Add(a + 2); tris.Add(a + 1); tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3); }
        var m = new Mesh { name = name };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        string path = ArtDir + "/" + name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { EditorUtility.CopySerialized(m, old); return old; }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject MeshObj(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static GameObject Disc(Transform parent, string name, Vector3 pos, float radius, float height, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos + Vector3.up * height * 0.5f;
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    // ───────────── 본체 ─────────────

    [MenuItem("Tools/Last Truck/Map: Decorate Forest Mine Arena")]
    static void Menu() { Debug.Log("[CaveArenaMakeover]\n" + Run()); }

    public static string Run()
    {
        var sb = new StringBuilder();
        var fm = GameObject.Find("ForestMine");
        if (fm == null) return "ForestMine 을 찾을 수 없습니다";
        var old = fm.transform.Find(GroupName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder(ArtDir)) AssetDatabase.CreateFolder("Assets/Art", "CaveMakeover");

        rng = new System.Random(20261120);
        placed = 0;
        var root = new GameObject(GroupName).transform;
        root.SetParent(fm.transform, false);
        Transform grp(string n) { var t = new GameObject(n).transform; t.SetParent(root, false); return t; }

        if (!Ground(Center.x, Center.y, out Vector3 cg)) return "광장 바닥을 찾지 못했습니다";
        float baseY = cg.y;

        Texture2D runeTex = MakeRuneTexture();
        Material runeMat = MakeMat("CaveRuneGlow", new Color(0.07f, 0.06f, 0.11f), new Color(0.85f, 0.4f, 2.2f), runeTex);
        Material barMat = MakeMat("CaveRuneBar", new Color(0.07f, 0.06f, 0.11f), new Color(0.22f, 0.5f, 1.25f), null);
        Material daisMat = MakeMat("CaveDaisStone", new Color(0.36f, 0.34f, 0.44f), new Color(0.03f, 0.02f, 0.06f), null);
        Material daisTrim = MakeMat("CaveDaisTrim", new Color(0.12f, 0.1f, 0.18f), new Color(0.7f, 0.35f, 1.8f), null);
        Material crystalPurple = MakeMat("CaveCrystalPurple", new Color(0.55f, 0.30f, 0.95f), new Color(0.55f, 0.18f, 1.3f), null);
        Material crystalCyan = MakeMat("CaveCrystalCyan", new Color(0.25f, 0.72f, 0.98f), new Color(0.10f, 0.55f, 1.2f), null);
        // 빛 기둥 선용 재질
        string beamPath = ArtDir + "/CaveBeam.mat";
        Material beamMat = AssetDatabase.LoadAssetAtPath<Material>(beamPath);
        if (beamMat == null) { beamMat = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(beamMat, beamPath); }

        // 1) 바닥 마법진: 고리 3개 + 길 8개
        var floor = grp("FloorRunes");
        MeshObj(floor, "RuneRing_Inner", MakeRing("RuneRingInner", 12.0f, 15.0f, 96, 1), runeMat);
        MeshObj(floor, "RuneRing_Mid", MakeRing("RuneRingMid", 24.5f, 26.0f, 128, 1), barMat);
        MeshObj(floor, "RuneRing_Mid2", MakeRing("RuneRingMid2", 26.6f, 28.2f, 128, 3), runeMat);
        MeshObj(floor, "RuneRing_Outer", MakeRing("RuneRingOuter", 40.0f, 43.5f, 160, 2), runeMat);
        for (int i = 0; i < 8; i++)
        {
            float ang = i * 45f + 22.5f;
            MeshObj(floor, "RuneSpoke_" + i, MakeSpoke("RuneSpoke" + i, ang, 15.3f, 24.2f, 0.7f), barMat);
            MeshObj(floor, "RuneSpoke_Out" + i, MakeSpoke("RuneSpokeOut" + i, ang, 28.4f, 39.8f, 0.7f), barMat);
        }

        // 2) 가운데 제단
        var altar = grp("Altar");
        Disc(altar, "Step3", new Vector3(Center.x, baseY - 0.6f, Center.y), 9.2f, 1.7f, daisMat);
        Disc(altar, "Step2", new Vector3(Center.x, baseY - 0.2f, Center.y), 7.2f, 1.7f, daisMat);
        Disc(altar, "Step1", new Vector3(Center.x, baseY + 0.2f, Center.y), 5.0f, 1.5f, daisMat);
        Disc(altar, "Trim", new Vector3(Center.x, baseY + 1.55f, Center.y), 5.15f, 0.12f, daisTrim);
        float topY = baseY + 1.6f;

        // 큰 수정 (장식용: 파란 수정은 다이아 광맥과 헷갈려서 쓰지 않고 보라 그라데이션 / 초록만 쓴다)
        string[] bigCrystals = { "PP_Crystal_Column_Gradient_01", "PP_Crystal_Column_Gradient_02", "PP_Crystal_Column_Gradient_03", "PP_Crystal_Column_Gradient_04" };
        var crystalTops = new List<Vector3>();
        for (int i = 0; i < 6; i++)
        {
            float ang = i * 60f + R(-8f, 8f); float rad = i == 0 ? 0f : R(1.8f, 3.4f);
            Vector2 p = Center + new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * rad;
            float sc = i == 0 ? R(3.0f, 3.6f) : R(1.8f, 2.8f);
            var go = Place(altar, bigCrystals[i % bigCrystals.Length], new Vector3(p.x, topY, p.y), R(0f, 360f), sc, true, new Vector3(R(-6f, 6f), 0f, R(-6f, 6f)));
            if (go != null) Recolor(go, i % 2 == 0 ? crystalPurple : crystalCyan);
            if (go != null) { var b = new Bounds(go.transform.position, Vector3.zero); foreach (var rr in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(rr.bounds); crystalTops.Add(new Vector3(b.center.x, b.max.y, b.center.z)); }
        }
        Vector3 crown = new Vector3(Center.x, topY + 6f, Center.y);
        if (crystalTops.Count > 0) { float maxY = topY; foreach (var t in crystalTops) maxY = Mathf.Max(maxY, t.y); crown = new Vector3(Center.x, maxY - 1.0f, Center.y); }
        AddLight(altar, "CrystalLight_Core", crown, new Color(0.7f, 0.4f, 1f), 14f, 46f);
        AddLight(altar, "CrystalLight_Low", new Vector3(Center.x, topY + 1.5f, Center.y), new Color(0.3f, 0.9f, 1f), 7f, 24f);
        var sparkle = Load("FX_Sparkles_Small_01");
        if (sparkle != null) { var m = (GameObject)PrefabUtility.InstantiatePrefab(sparkle, altar); m.transform.position = new Vector3(Center.x, topY + 2.5f, Center.y); m.name = "FX_AltarSparkles"; }

        // 네 귀퉁이 화로 (맵에 있는 횃불을 복제: 불꽃과 불빛이 같이 따라온다)
        GameObject torchSrc = null; float bestH = 1e9f;
        var torchesRoot = GameObject.Find("Torches");
        if (torchesRoot != null)
            foreach (Transform tt in torchesRoot.transform)
            {
                if (Vector2.Distance(new Vector2(tt.position.x, tt.position.z), Center) > 70f) continue;
                var rs = tt.GetComponentsInChildren<MeshRenderer>(); if (rs.Length == 0) continue;
                var bb = rs[0].bounds; foreach (var rr in rs) bb.Encapsulate(rr.bounds);
                if (bb.size.y > 0.3f && bb.size.y < bestH) { bestH = bb.size.y; torchSrc = tt.gameObject; }
            }
        if (torchSrc == null && torchesRoot != null && torchesRoot.transform.childCount > 0) torchSrc = torchesRoot.transform.GetChild(0).gameObject;
        if (torchSrc != null)
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = Polar(8.0f, i * 90f + 45f);
                if (!Ground(p.x, p.y, out Vector3 g)) continue;
                var t = Object.Instantiate(torchSrc, altar, true);
                t.name = "AltarBrazier_" + i;
                t.transform.position = new Vector3(p.x, baseY + 0.9f, p.y);
                t.transform.localScale = torchSrc.transform.lossyScale * 0.9f;
                placed++;
            }

        // 3) 기둥 10개 → 가운데 수정으로 에너지 줄기
        var beams = grp("Beams");
        var pillars = GameObject.Find("Pillars");
        if (pillars != null)
        {
            int bi = 0;
            foreach (Transform pl in pillars.transform)
            {
                var go = new GameObject("Beam_" + bi); go.transform.SetParent(beams, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true; lr.positionCount = 14; lr.widthMultiplier = 0.14f; lr.numCapVertices = 4;
                lr.sharedMaterial = beamMat; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
                var grad = new Gradient();
                grad.SetKeys(new[] { new GradientColorKey(new Color(0.55f, 0.95f, 1f), 0f), new GradientColorKey(new Color(0.85f, 0.45f, 1f), 1f) },
                             new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.9f, 1f) });
                lr.colorGradient = grad;
                Vector3 a = pl.position + Vector3.up * 11.4f, b = crown;
                for (int k = 0; k < lr.positionCount; k++)
                {
                    float t = k / (float)(lr.positionCount - 1);
                    Vector3 pt = Vector3.Lerp(a, b, t);
                    pt.y += Mathf.Sin(t * Mathf.PI) * 2.0f;   // 위로 살짝 호를 그린다
                    lr.SetPosition(k, pt);
                }
                go.AddComponent<CaveGlowPulse>().Configure(2.4f, 0.35f, bi * 0.7f);
                bi++;
            }
        }

        // 4) 바닥을 채우는 장식
        var props = grp("Props");
        var exclusion = new List<(float r0, float r1)> { (0f, 17f), (23.5f, 29.5f), (38.5f, 45f) };
        bool Allowed(Vector2 p)
        {
            float r = Vector2.Distance(p, Center);
            if (r > 51f) return false;
            foreach (var e in exclusion) if (r > e.r0 && r < e.r1) return false;
            if (Mathf.Abs(p.x - Center.x) < 6.5f) return false;   // 남북 통로는 비워 둔다
            return true;
        }
        System.Func<float, float, Vector3?> pick = (rmin, rmax) =>
        {
            for (int t = 0; t < 60; t++)
            {
                float rad = Mathf.Sqrt(R(rmin * rmin, rmax * rmax)), ang = R(0f, 360f);
                Vector2 p = Polar(rad, ang);
                if (!Allowed(p)) continue;
                if (Ground(p.x, p.y, out Vector3 g)) return g;
            }
            return null;
        };

        void Scatter(string label, string[] names, int count, float smin, float smax, float rmin, float rmax, bool noCol, System.Action<GameObject, Vector3> extra = null, Vector3? tilt = null)
        {
            var g = new GameObject(label).transform; g.SetParent(props, false);
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3? p = pick(rmin, rmax);
                if (p == null) continue;
                var go = Place(g, names[rng.Next(names.Length)], p.Value + Vector3.down * 0.05f, R(0f, 360f), R(smin, smax), noCol, tilt.HasValue ? new Vector3(R(-tilt.Value.x, tilt.Value.x), 0f, R(-tilt.Value.z, tilt.Value.z)) : (Vector3?)null);
                if (go != null) { n++; extra?.Invoke(go, p.Value); }
            }
            sb.AppendLine(label + ": " + n + "/" + count);
        }

        string[] stalag = { "PP_Stalagmite_01", "PP_Stalagmite_02", "PP_Stalagmite_03", "PP_Stalagmite_04", "PP_Stalagmite_05" };
        string[] gradient = { "PP_Crystal_Gradient_01", "PP_Crystal_Gradient_02", "PP_Crystal_Gradient_03", "PP_Crystal_Gradient_04", "PP_Crystal_Gradient_05", "PP_Crystal_Gradient_06", "PP_Crystal_Gradient_07", "PP_Crystal_Column_Gradient_01", "PP_Crystal_Column_Gradient_02", "PP_Crystal_Column_Gradient_03" };
        string[] greens = { "PP_Crystal_Cluster_01_Green", "PP_Crystal_Cluster_02_Green", "PP_Crystal_Cluster_03_Green", "PP_Crystal_Cluster_04_Green", "PP_Crystal_Column_01_Green", "PP_Crystal_Column_02_Green" };
        string[] rocks = { "PP_Rock_Moss_01", "PP_Rock_Moss_02", "PP_Rock_Moss_03", "PP_Rock_Moss_04", "PP_Rock_Pile_Moss_01", "PP_Rock_Pile_Moss_03", "PP_Rock_Pile_Moss_05", "PP_Rock_Menhir_Moss_01", "PP_Rock_Menhir_Moss_02" };
        string[] mush = { "PP_Mushrooms_Brown_01", "PP_Mushrooms_Brown_02", "PP_Mushrooms_Brown_03", "PP_Mushrooms_Brown_04", "PP_Mushrooms_Stone_Brown_01", "PP_Mushrooms_Stone_Brown_02", "PP_Mushroom_Long_Dark_Brown_01", "PP_Mushroom_Long_Dark_Brown_03" };
        string[] ruins = { "PP_Pillar_Stone_01", "PP_Pillar_Stone_03", "PP_Pillar_Stone_05", "PP_Pillar_Stone_07" };

        Scatter("Stalagmites", stalag, 34, 0.8f, 1.9f, 17f, 50f, false);
        Scatter("Rocks", rocks, 34, 0.8f, 1.6f, 17f, 50f, false);
        Scatter("Mushrooms", mush, 30, 0.9f, 1.8f, 17f, 50f, true);

        // 수정 무더기: 일부에는 빛을 단다
        int lit = 0;
        Scatter("Crystals_Purple", gradient, 26, 0.9f, 2.0f, 17f, 50f, true, (go, p) =>
        {
            bool cyan = rng.NextDouble() < 0.35;
            Recolor(go, cyan ? crystalCyan : crystalPurple);
            if (lit < 12 && rng.NextDouble() < 0.5)
            {
                lit++;
                AddLight(go.transform, "CrystalGlow", p + Vector3.up * 1.6f, cyan ? new Color(0.35f, 0.8f, 1f) : new Color(0.65f, 0.4f, 1f), 3.2f, 13f);
            }
        });
        Scatter("Crystals_Green", greens, 12, 0.9f, 1.7f, 20f, 50f, true, (go, p) =>
        {
            if (rng.NextDouble() < 0.5) AddLight(go.transform, "CrystalGlow", p + Vector3.up * 1.4f, new Color(0.3f, 1f, 0.6f), 2.8f, 11f);
        });

        // 무너진 기둥 (옆으로 쓰러뜨림)
        Scatter("BrokenPillars", ruins, 7, 0.9f, 1.1f, 18f, 46f, false, null, new Vector3(0f, 0f, 0f));
        foreach (Transform t in props.Find("BrokenPillars")) t.rotation = Quaternion.Euler(R(78f, 92f), R(0f, 360f), 0f);

        // 석상 두 개: 통로 양옆
        foreach (int side in new[] { -1, 1 })
        {
            Vector2 p = new Vector2(Center.x + side * 10.5f, Center.y + 33f);
            if (Ground(p.x, p.y, out Vector3 g)) Place(props, side < 0 ? "PP_Stone_Statue_01" : "PP_Stone_Statue_02", g, side < 0 ? 160f : 200f, 1.6f);
        }
        // 보물상자 몇 개 (장식)
        string[] chests = { "PP_Treasure_Chest_03_Gold", "PP_Treasure_Chest_02_Silver", "PP_Treasure_Chest_04_Blue" };
        for (int i = 0; i < 3; i++)
        {
            Vector3? p = pick(30f, 36f);
            if (p != null) Place(props, chests[i], p.Value, R(0f, 360f), 1.2f);
        }

        // 5) 분위기: 낮은 안개 + 반딧불
        var atmos = grp("Atmosphere");
        for (int i = 0; i < 7; i++)
        {
            Vector2 p = Polar(R(6f, 44f), i * 51f + R(-20f, 20f));
            if (!Ground(p.x, p.y, out Vector3 g)) continue;
            var pf = Load("FX_Fireflies_01"); if (pf == null) continue;
            var f = (GameObject)PrefabUtility.InstantiatePrefab(pf, atmos); f.transform.position = g + Vector3.up * 1.5f; f.name = "FX_Fireflies_" + i;
        }
        // 가장자리 은은한 바닥 조명
        for (int i = 0; i < 6; i++)
        {
            Vector2 p = Polar(33f, i * 60f + 30f);
            if (Ground(p.x, p.y, out Vector3 g)) AddLight(atmos, "RingLight_" + i, g + Vector3.up * 2.5f, i % 2 == 0 ? new Color(0.45f, 0.5f, 1f) : new Color(0.8f, 0.4f, 1f), 4f, 20f);
        }

        // 원래 있던 짙은 흰 안개(바닥/기둥 모양 연기)가 광장을 가려서 옅게 만든다 (알파를 정해진 값으로 맞추므로 여러 번 실행해도 같다)
        var effects = fm.transform.Find("Effects");
        int thinned = 0;
        if (effects != null)
            foreach (Transform e in effects)
            {
                float alpha = e.name.StartsWith("FX_Fog_Big") ? 0.18f : e.name.StartsWith("FX_Fog_") ? 0.07f : -1f;
                if (alpha < 0f) continue;
                foreach (var ps in e.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var m = ps.main; Color c0 = m.startColor.color;
                    m.startColor = new Color(c0.r, c0.g, c0.b, alpha);
                    thinned++;
                }
            }
        sb.AppendLine("옅게 만든 안개 파티클 " + thinned + "개");

        EditorUtility.SetDirty(root.gameObject);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        sb.AppendLine("총 배치 " + placed + "개, 바닥 높이 " + baseY.ToString("0.00"));
        return sb.ToString();
    }
}
#endif
