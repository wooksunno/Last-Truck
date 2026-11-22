#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using CraftingSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// "숲 광산(ForestMine)" 둥근 동굴 광장을 "원시인들이 살던 야영지"로 꾸민다. (예전 버전은 룬 마법진/수정 제단이었다 → 게임과 안 맞아 교체)
///  - 가운데: 돌로 두른 큰 모닥불 + 요리용 삼각대와 냄비 + 통나무 의자
///  - 둘레: 가죽 천막(티피) 8채, 토템 기둥과 선돌(원래 룬 기둥 10개 자리), 가죽 말리는 틀, 창 걸이, 뼈 무더기, 항아리/자루, 작은 화덕
///  - 벽: 손바닥 자국·사냥꾼·짐승을 그린 동굴 벽화
///  - 채집: 구리/철/금 광맥, 돌 무더기, 장작 더미, 말리는 가죽(젖은 가죽) — 바깥 둘레와 남북 통로
///  - 보물상자 6개 (나무 3 / 철 2 / 황금 1, TreasureChest: E 꾹 → 열기, 20분 뒤 다시 채워짐)
///  - 원래 있던 신비 연출(파란 기둥 불빛, 마법 고리/빛 이펙트)은 지우지 않고 끈다(SetActive false). 룬 기둥도 끄고 그 자리에 토템/선돌을 세운다.
/// "ForestMine/Makeover" 아래에 모으며, 다시 실행하면 지난번 것을 지우고 새로 만든다.
/// </summary>
public static class CaveArenaMakeover
{
    const string GroupName = "Makeover";
    const string ArtDir = "Assets/Art/CaveCamp";
    static readonly Vector2 Center = new Vector2(-355f, 372f);
    const float CorridorHalf = 15f;            // 남북 통로 반폭 (x -372 ~ -338)
    const int NodeLayer = 4;                   // Water = PlayerInteract.interactLayer

    static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();
    static readonly List<(Vector2 p, float r)> Occupied = new List<(Vector2, float)>();
    static readonly List<string> Missing = new List<string>();
    static System.Random rng;
    static int placed;

    // ───────────── 기본 도우미 ─────────────

    static GameObject Load(string name)
    {
        if (PrefabCache.TryGetValue(name, out var g)) return g;
        GameObject found = null;
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name) { found = AssetDatabase.LoadAssetAtPath<GameObject>(path); break; }
        }
        if (found == null && !Missing.Contains(name)) Missing.Add(name);
        PrefabCache[name] = found;
        return found;
    }

    static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static T Pick<T>(T[] arr) => arr[rng.Next(arr.Length)];

    // 동굴 바닥(Shell_*)의 높이. 소품 위가 아니라 진짜 바닥만 잡는다.
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
    static float Yaw(Vector3 from, Vector2 to) { Vector3 d = new Vector3(to.x - from.x, 0f, to.y - from.z); return Quaternion.LookRotation(d.sqrMagnitude < 0.001f ? Vector3.forward : d).eulerAngles.y; }

    static bool InCorridorMouth(float angDeg)
    {
        float a = Mathf.Repeat(angDeg, 360f);
        return Mathf.Abs(Mathf.DeltaAngle(a, 90f)) < 16f || Mathf.Abs(Mathf.DeltaAngle(a, 270f)) < 16f;
    }

    // 자리가 비었는가: 이번에 놓은 것들과의 간격 + 원래 있던 물체(기둥/횃불 등)와 겹치지 않는지
    static bool Free(Vector3 g, float r)
    {
        Vector2 p = new Vector2(g.x, g.z);
        foreach (var o in Occupied) if (Vector2.Distance(o.p, p) < o.r + r) return false;
        foreach (var c in Physics.OverlapSphere(g + Vector3.up * (r + 0.3f), r, ~0, QueryTriggerInteraction.Ignore))
            if (!c.name.StartsWith("Shell_")) return false;
        return true;
    }
    static void Claim(Vector3 g, float r) => Occupied.Add((new Vector2(g.x, g.z), r));

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

    static Light AddLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range, float flicker = 0.16f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        if (flicker > 0f) go.AddComponent<CaveGlowPulse>().Configure(R(7f, 10f), flicker, R(0f, 6f));
        return l;
    }

    static GameObject Fx(Transform parent, string prefab, Vector3 pos, float scale, bool smoke = false)
    {
        var pf = Load(prefab); if (pf == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.position = pos;
        go.transform.localScale = pf.transform.localScale * scale;
        // 동굴 안이라 연기가 화면을 덮지 않게: 기본은 연기를 끈다
        if (!smoke) foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) if (ps.name.Contains("Smoke")) ps.gameObject.SetActive(false);
        return go;
    }

    // ───────────── 재질 / 기본 도형 ─────────────

    static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
    static Material M(string name, Color c, Color? emission = null)
    {
        if (Mats.TryGetValue(name, out var m)) return m;
        string path = ArtDir + "/" + name + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("LastTruck/ToonWorld")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c);
        if (emission.HasValue) { m.SetColor("_EmissionColor", emission.Value); m.EnableKeyword("_EMISSION"); }
        else m.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(m);
        Mats[name] = m;
        return m;
    }

    static Material Wood => M("CampWood", new Color(0.5f, 0.33f, 0.2f));
    static Material DarkWood => M("CampDarkWood", new Color(0.3f, 0.2f, 0.13f));
    static Material Ochre => M("CampOchre", new Color(0.66f, 0.27f, 0.14f));
    static Material Chalk => M("CampChalk", new Color(0.9f, 0.86f, 0.76f));
    static Material Charcoal => M("CampCharcoal", new Color(0.08f, 0.07f, 0.07f));
    static Material Bone => M("CampBone", new Color(0.88f, 0.84f, 0.72f));
    static Material Rope => M("CampRope", new Color(0.68f, 0.57f, 0.38f));
    static Material Flint => M("CampFlint", new Color(0.35f, 0.36f, 0.4f));
    static Material[] Hides => new[] { M("CampHideTan", new Color(0.78f, 0.6f, 0.4f)), M("CampHideLight", new Color(0.86f, 0.74f, 0.54f)), M("CampHideBrown", new Color(0.58f, 0.4f, 0.26f)) };

    static Transform Prim(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Material mat, Quaternion? rot = null, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    // a→b 를 잇는 막대(원기둥)
    static Transform Stick(Transform parent, string name, Vector3 a, Vector3 b, float thick, Material mat)
    {
        var t = Prim(parent, PrimitiveType.Cylinder, name, (a + b) * 0.5f, new Vector3(thick, Vector3.Distance(a, b) * 0.5f, thick), mat);
        t.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        return t;
    }

    static Transform Group(Transform parent, string name, Vector3 pos, float yaw)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = pos;
        t.rotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    // ───────────── 메쉬: 원뿔(티피), 둘레 띠 ─────────────

    static Mesh SaveMesh(Mesh m, string name)
    {
        string path = ArtDir + "/" + name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { EditorUtility.CopySerialized(m, old); return old; }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // 높이 y0~y1 사이의 깎인 원뿔(면마다 따로 꼭짓점: 각진 로우폴리). r(y) = rBase * (1 - y)  (단위 원뿔: 바닥 반지름 1, 높이 1)
    static Mesh Frustum(string name, int sides, float y0, float y1, float grow)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
            float r0 = (1f - y0) * grow, r1 = (1f - y1) * grow;
            Vector3 p00 = new Vector3(Mathf.Cos(a0) * r0, y0, Mathf.Sin(a0) * r0), p01 = new Vector3(Mathf.Cos(a1) * r0, y0, Mathf.Sin(a1) * r0);
            Vector3 p10 = new Vector3(Mathf.Cos(a0) * r1, y1, Mathf.Sin(a0) * r1), p11 = new Vector3(Mathf.Cos(a1) * r1, y1, Mathf.Sin(a1) * r1);
            int b = v.Count;
            v.Add(p00); v.Add(p01); v.Add(p10); v.Add(p11);
            t.Add(b); t.Add(b + 2); t.Add(b + 1);
            if (y1 < 0.999f) { t.Add(b + 1); t.Add(b + 2); t.Add(b + 3); }
        }
        var m = new Mesh { name = name };
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return SaveMesh(m, name);
    }

    // ───────────── 동굴 벽화 텍스처 ─────────────

    static Texture2D PaintingTexture(int variant)
    {
        string path = ArtDir + "/CavePainting_" + variant + ".png";
        const int W = 1024, H = 512;
        var px = new Color32[W * H];
        var rr = new System.Random(500 + variant);
        float Rr(float a, float b) => a + (float)rr.NextDouble() * (b - a);
        Color32 ochre = new Color32(150, 52, 28, 255), dark = new Color32(30, 22, 20, 255), yellow = new Color32(176, 120, 48, 255), white = new Color32(214, 200, 176, 255);

        void Dot(float cx, float cy, float rad, Color32 c)
        {
            int x0 = Mathf.FloorToInt(cx - rad), x1 = Mathf.CeilToInt(cx + rad), y0 = Mathf.FloorToInt(cy - rad), y1 = Mathf.CeilToInt(cy + rad);
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(H - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(W - 1, x1); x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d > rad) continue;
                    if (rr.NextDouble() < 0.12) continue;   // 거친 바위에 칠한 듯 군데군데 빈다
                    px[y * W + x] = c;
                }
        }
        void Line(float x0, float y0, float x1, float y1, float th, Color32 c)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) / 1.5f) + 1;
            for (int s = 0; s <= steps; s++) { float k = s / (float)steps; Dot(Mathf.Lerp(x0, x1, k), Mathf.Lerp(y0, y1, k), th * Rr(0.85f, 1.1f), c); }
        }
        void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            for (int y = Mathf.Max(0, (int)(cy - ry)); y <= Mathf.Min(H - 1, (int)(cy + ry)); y++)
                for (int x = Mathf.Max(0, (int)(cx - rx)); x <= Mathf.Min(W - 1, (int)(cx + rx)); x++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    if (dx * dx + dy * dy > 1f || rr.NextDouble() < 0.1) continue;
                    px[y * W + x] = c;
                }
        }
        void Hand(float cx, float cy, float s, Color32 c)
        {
            Ellipse(cx, cy, 22 * s, 26 * s, c);
            float[] ang = { 150f, 110f, 90f, 70f, 45f };
            float[] len = { 30f, 44f, 50f, 44f, 34f };
            for (int i = 0; i < 5; i++)
            {
                float a = ang[i] * Mathf.Deg2Rad;
                float bx = cx + Mathf.Cos(a) * 18 * s, by = cy + Mathf.Sin(a) * 20 * s;
                Line(bx, by, bx + Mathf.Cos(a) * len[i] * s, by + Mathf.Sin(a) * len[i] * s, 6.5f * s, c);
            }
        }
        void Animal(float cx, float cy, float s, bool antlers, bool big, Color32 c)
        {
            float bw = (big ? 85 : 60) * s, bh = (big ? 40 : 26) * s;
            Ellipse(cx, cy, bw, bh, c);
            Ellipse(cx + bw * 0.95f, cy + bh * 0.55f, bh * 0.55f, bh * 0.45f, c);         // 머리
            foreach (float lx in new[] { -0.6f, -0.35f, 0.4f, 0.65f })
                Line(cx + lx * bw, cy - bh * 0.6f, cx + lx * bw + Rr(-6, 6) * s, cy - bh * 0.6f - (big ? 46 : 40) * s, 5 * s, c);
            Line(cx - bw, cy + bh * 0.2f, cx - bw - 18 * s, cy - 8 * s, 4 * s, c);            // 꼬리
            if (antlers)
            {
                float hx = cx + bw * 1.05f, hy = cy + bh;
                Line(hx, hy, hx - 12 * s, hy + 38 * s, 3.5f * s, c); Line(hx - 6 * s, hy + 20 * s, hx - 24 * s, hy + 30 * s, 3f * s, c);
                Line(hx + 6 * s, hy, hx + 16 * s, hy + 36 * s, 3.5f * s, c); Line(hx + 11 * s, hy + 18 * s, hx + 28 * s, hy + 26 * s, 3f * s, c);
            }
            if (big) Line(cx + bw * 1.3f, cy + bh * 0.3f, cx + bw * 1.55f, cy - bh * 0.6f, 6 * s, white);   // 엄니
        }
        void Hunter(float cx, float cy, float s, Color32 c)
        {
            Dot(cx, cy + 62 * s, 9 * s, c);                                  // 머리
            Line(cx, cy + 52 * s, cx, cy + 16 * s, 5 * s, c);                // 몸
            Line(cx, cy + 16 * s, cx - 14 * s, cy - 14 * s, 4.5f * s, c);    // 다리
            Line(cx, cy + 16 * s, cx + 14 * s, cy - 14 * s, 4.5f * s, c);
            Line(cx, cy + 44 * s, cx + 20 * s, cy + 56 * s, 4 * s, c);       // 팔
            Line(cx - 30 * s, cy + 34 * s, cx + 54 * s, cy + 74 * s, 3 * s, c);   // 창
            Line(cx + 54 * s, cy + 74 * s, cx + 44 * s, cy + 66 * s, 5 * s, c);
        }

        // 구성: 짐승 몇 마리 + 사냥꾼 무리 + 손바닥 자국 + 점 줄 + 해(소용돌이)
        int animals = 2 + variant % 2;
        for (int i = 0; i < animals; i++)
            Animal(Rr(180, 840), Rr(200, 330), Rr(0.8f, 1.2f), rr.NextDouble() < 0.5, i == 0 && variant != 1, rr.NextDouble() < 0.7 ? ochre : dark);
        int hunters = 3 + rr.Next(3);
        float hx0 = Rr(80, 400);
        for (int i = 0; i < hunters; i++) Hunter(hx0 + i * Rr(55, 75), Rr(60, 110), Rr(0.8f, 1.05f), dark);
        for (int i = 0; i < 4 + rr.Next(4); i++) Hand(Rr(60, 960), Rr(380, 470), Rr(0.7f, 1.0f), rr.NextDouble() < 0.75 ? ochre : yellow);
        float dy = Rr(150, 180);
        for (int i = 0; i < 14; i++) Dot(Rr(600, 980) * 0 + 620 + i * 24, dy + Mathf.Sin(i * 0.9f) * 8, 6, ochre);
        float sx = Rr(820, 950), sy = Rr(400, 450);
        for (int k = 0; k < 120; k++) { float a = k * 0.18f; float rad = 3 + k * 0.32f; Dot(sx + Mathf.Cos(a) * rad, sy + Mathf.Sin(a) * rad, 3.5f, yellow); }

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, true);
        tex.SetPixels32(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true; imp.maxTextureSize = 1024;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material PaintingMat(int variant)
    {
        string path = ArtDir + "/CavePainting_" + variant + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetTexture("_BaseMap", PaintingTexture(variant));
        mat.SetColor("_BaseColor", new Color(0.9f, 0.9f, 0.9f, 1f));
        mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", 0.45f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Smoothness", 0f); mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Cull", 0f);
        mat.renderQueue = 2450;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ───────────── 조립품 ─────────────

    static void Teepee(Transform parent, Vector3 g, float yaw, float radius, float height, Mesh cone, Mesh band, int style)
    {
        var t = Group(parent, "Teepee", g, yaw);
        var hides = Hides;
        var body = new GameObject("Cover"); body.transform.SetParent(t, false);
        body.transform.localScale = new Vector3(radius, height, radius);
        body.AddComponent<MeshFilter>().sharedMesh = cone;
        body.AddComponent<MeshRenderer>().sharedMaterial = hides[style % hides.Length];
        var mc = body.AddComponent<MeshCollider>(); mc.sharedMesh = cone; mc.convex = true;
        // 칠한 띠 두 줄
        foreach (var (mesh, mat) in new[] { (band, style % 2 == 0 ? Ochre : Charcoal) })
        {
            var b = new GameObject("Band"); b.transform.SetParent(t, false);
            b.transform.localScale = new Vector3(radius, height, radius);
            b.AddComponent<MeshFilter>().sharedMesh = mesh;
            b.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
        // 꼭대기 위로 삐져나온 장대
        Vector3 apex = new Vector3(0f, height, 0f);
        for (int i = 0; i < 6; i++)
        {
            float a = (i * 60f + 15f) * Mathf.Deg2Rad;
            Vector3 foot = new Vector3(Mathf.Cos(a) * radius * 0.92f, 0f, Mathf.Sin(a) * radius * 0.92f);
            Vector3 dir = (apex - foot).normalized;
            Stick(t, "Pole", foot + dir * height * 0.55f, apex + dir * 1.1f, 0.09f, DarkWood);
        }
        // 입구 (앞쪽 = +z): 어두운 삼각 틈 + 걷어 올린 가죽
        float slope = Mathf.Atan2(radius, height) * Mathf.Rad2Deg;
        Prim(t, PrimitiveType.Cube, "Door", new Vector3(0f, height * 0.17f, radius * 0.83f), new Vector3(radius * 0.42f, height * 0.34f, 0.06f), Charcoal, Quaternion.Euler(-slope, 0f, 0f));
        Prim(t, PrimitiveType.Cube, "Flap", new Vector3(radius * 0.32f, height * 0.26f, radius * 0.76f), new Vector3(radius * 0.22f, height * 0.3f, 0.05f), hides[(style + 1) % hides.Length], Quaternion.Euler(-slope, 20f, -8f));
        // 그려 넣은 무늬 (해/짐승 대신 굵은 점 세 개)
        for (int i = 0; i < 3; i++)
            Prim(t, PrimitiveType.Sphere, "PaintDot", new Vector3((i - 1) * radius * 0.22f, height * 0.42f, radius * 0.6f), new Vector3(0.24f, 0.24f, 0.08f), style % 2 == 0 ? Chalk : Ochre, Quaternion.Euler(-slope, 0f, 0f));
        placed++;
    }

    static void TotemPole(Transform parent, Vector3 g, float yaw, int seed, Transform lightParent)
    {
        var t = Group(parent, "TotemPole", g, yaw);
        var cap = t.gameObject.AddComponent<CapsuleCollider>(); cap.radius = 0.7f; cap.height = 6.5f; cap.center = new Vector3(0f, 3.25f, 0f);
        float y = 0f;
        for (int i = 0; i < 4; i++)
        {
            float h = i == 0 ? 1.4f : 1.15f;
            Prim(t, PrimitiveType.Cylinder, "Log", new Vector3(0f, y + h * 0.5f, 0f), new Vector3(1.05f, h * 0.5f, 1.05f), (i + seed) % 2 == 0 ? Wood : DarkWood);
            // 얼굴: 눈 두 개, 입, 코
            float fy = y + h * 0.58f;
            foreach (float ex in new[] { -0.2f, 0.2f })
            {
                Prim(t, PrimitiveType.Cube, "Eye", new Vector3(ex, fy, 0.5f), new Vector3(0.22f, 0.14f, 0.08f), Chalk);
                Prim(t, PrimitiveType.Cube, "Pupil", new Vector3(ex, fy, 0.54f), new Vector3(0.08f, 0.1f, 0.04f), Charcoal);
            }
            Prim(t, PrimitiveType.Cube, "Mouth", new Vector3(0f, y + h * 0.22f, 0.5f), new Vector3(0.42f, 0.12f, 0.08f), (i + seed) % 2 == 0 ? Ochre : Chalk);
            Prim(t, PrimitiveType.Cube, "Nose", new Vector3(0f, y + h * 0.42f, 0.58f), new Vector3(0.12f, 0.2f, 0.18f), (i + seed) % 3 == 0 ? Ochre : DarkWood);
            y += h;
        }
        // 날개 판 + 꼭대기 불 그릇
        Prim(t, PrimitiveType.Cube, "Wings", new Vector3(0f, y - 0.35f, 0f), new Vector3(3.0f, 0.32f, 0.18f), Ochre, Quaternion.Euler(0f, 0f, 0f));
        Prim(t, PrimitiveType.Cube, "WingTipL", new Vector3(-1.45f, y - 0.2f, 0f), new Vector3(0.3f, 0.5f, 0.2f), Chalk);
        Prim(t, PrimitiveType.Cube, "WingTipR", new Vector3(1.45f, y - 0.2f, 0f), new Vector3(0.3f, 0.5f, 0.2f), Chalk);
        Prim(t, PrimitiveType.Cylinder, "FireBowl", new Vector3(0f, y + 0.18f, 0f), new Vector3(0.9f, 0.18f, 0.9f), Flint);
        Fx(t, "FX_Campfire_01", t.TransformPoint(new Vector3(0f, y + 0.3f, 0f)), 0.55f);
        AddLight(lightParent, "TotemFire", t.TransformPoint(new Vector3(0f, y + 1.0f, 0f)), new Color(1f, 0.55f, 0.24f), 3.2f, 22f, 0.18f);
        placed++;
    }

    static void BoneProp(Transform parent, Vector3 pos, float yaw, float len)
    {
        var b = Group(parent, "Bone", pos, yaw);
        b.rotation = Quaternion.Euler(R(-6f, 6f), yaw, 90f);
        Prim(b, PrimitiveType.Cylinder, "Shaft", Vector3.zero, new Vector3(0.12f, len * 0.5f, 0.12f), Bone);
        foreach (float s in new[] { -1f, 1f })
        {
            Prim(b, PrimitiveType.Sphere, "Knob", new Vector3(0.06f, s * len * 0.5f, 0f), new Vector3(0.2f, 0.2f, 0.2f), Bone);
            Prim(b, PrimitiveType.Sphere, "Knob", new Vector3(-0.06f, s * len * 0.5f, 0f), new Vector3(0.2f, 0.2f, 0.2f), Bone);
        }
    }

    static void BonePile(Transform parent, Vector3 g)
    {
        var t = Group(parent, "BonePile", g, R(0f, 360f));
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = t.TransformPoint(new Vector3(R(-0.8f, 0.8f), 0.08f + i * 0.05f, R(-0.6f, 0.6f)));
            BoneProp(t, p, R(0f, 360f), R(0.7f, 1.3f));
        }
        var skull = Place(t, "SM_Gen_Prop_Skull_01", t.TransformPoint(new Vector3(0.3f, 0.05f, 0.1f)), R(0f, 360f), 2.4f, true);
        if (skull == null) Prim(t, PrimitiveType.Sphere, "Skull", new Vector3(0.3f, 0.2f, 0.1f), new Vector3(0.4f, 0.32f, 0.45f), Bone);
    }

    static void SpearRack(Transform parent, Vector3 g, float yaw)
    {
        var t = Group(parent, "SpearRack", g, yaw);
        foreach (float x in new[] { -0.9f, 0.9f })
        {
            Stick(t, "Leg", new Vector3(x, 0f, -0.35f), new Vector3(x, 1.5f, 0f), 0.08f, DarkWood);
            Stick(t, "Leg", new Vector3(x, 0f, 0.35f), new Vector3(x, 1.5f, 0f), 0.08f, DarkWood);
        }
        Stick(t, "Bar", new Vector3(-1.05f, 1.45f, 0f), new Vector3(1.05f, 1.45f, 0f), 0.08f, Wood);
        for (int i = 0; i < 4; i++)
        {
            float x = -0.6f + i * 0.4f;
            Vector3 foot = new Vector3(x + R(-0.05f, 0.05f), 0f, 0.55f), tip = new Vector3(x, 2.4f, -0.12f);
            Stick(t, "Spear", foot, tip, 0.05f, Wood);
            var head = Prim(t, PrimitiveType.Cube, "SpearHead", tip + (tip - foot).normalized * 0.14f, new Vector3(0.12f, 0.12f, 0.03f), Flint);
            head.localRotation = Quaternion.FromToRotation(Vector3.up, (tip - foot).normalized) * Quaternion.Euler(0f, 0f, 45f);
        }
        placed++;
    }

    static Transform DryingRack(Transform parent, Vector3 g, float yaw)
    {
        var t = Group(parent, "DryingRack", g, yaw);
        foreach (float x in new[] { -1.1f, 1.1f })
            Stick(t, "Post", new Vector3(x, 0f, 0f), new Vector3(x * 0.96f, 1.9f, 0f), 0.1f, DarkWood);
        Stick(t, "TopBar", new Vector3(-1.25f, 1.82f, 0f), new Vector3(1.25f, 1.82f, 0f), 0.08f, Wood);
        Stick(t, "LowBar", new Vector3(-1.1f, 0.5f, 0f), new Vector3(1.1f, 0.5f, 0f), 0.07f, Wood);
        var hides = Hides;
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -0.5f : 0.55f;
            Prim(t, PrimitiveType.Cube, "Hide", new Vector3(x, 1.15f, 0.02f), new Vector3(0.85f, 1.15f, 0.04f), hides[(i + 2) % hides.Length], Quaternion.Euler(0f, 0f, R(-4f, 4f)));
            Prim(t, PrimitiveType.Cube, "HideEdge", new Vector3(x, 1.15f, 0.045f), new Vector3(0.6f, 0.85f, 0.02f), hides[i % hides.Length]);
            foreach (float dx in new[] { -0.32f, 0.32f })
                Stick(t, "Tie", new Vector3(x + dx, 1.82f, 0f), new Vector3(x + dx, 1.7f, 0.02f), 0.025f, Rope);
        }
        placed++;
        return t;
    }

    static void LogSeat(Transform parent, Vector3 g, float yaw)
    {
        var go = Place(parent, rng.NextDouble() < 0.5 ? "SM_Gen_Env_Log_01" : "SM_Gen_Env_Log_02", g + Vector3.up * 0.05f, yaw, 0.85f);
        if (go == null) Stick(parent, "LogSeat", g + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.3f, -1.6f), g + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.3f, 1.6f), 0.6f, Wood);
    }

    // ───────────── 채집 노드 / 보물상자 ─────────────

    static SpecialResourceNode Node(Transform parent, string name, Vector3 pos, string itemId, string label, int amount, int tier, int minHits, int maxHits, float hold, float regenMin, float regenMax, float radius, float centerY)
    {
        var node = new GameObject(name + "_node");
        node.layer = NodeLayer;
        node.transform.SetParent(parent, false);
        node.transform.position = pos;
        var sc = node.AddComponent<SphereCollider>();
        sc.isTrigger = true; sc.radius = radius; sc.center = new Vector3(0f, centerY, 0f);
        var srn = node.AddComponent<SpecialResourceNode>();
        srn.Configure(label, ItemCatalog.GetOrCreate().GetItem(itemId), amount, tier, minHits, maxHits, regenMin, regenMax, "", hold);
        EditorUtility.SetDirty(srn);
        placed++;
        return srn;
    }

    static int Ore(Transform parent, Vector3 g, string color)
    {
        string[] kinds = { "PP_Crystal_Column_01_", "PP_Crystal_Column_02_", "PP_Crystal_Cluster_01_", "PP_Crystal_Cluster_02_", "PP_Crystal_Cluster_03_" };
        string pf = Pick(kinds) + color;
        if (Load(pf) == null) pf = "PP_Crystal_Column_01_" + color;
        string id = color == "Copper" ? ItemIds.CopperOre : color == "Iron" ? ItemIds.IronOre : ItemIds.PlatinumOre;
        string label = color == "Copper" ? "구리 광맥" : color == "Iron" ? "철 광맥" : "금 광맥";
        int tier = color == "Copper" ? 2 : color == "Iron" ? 3 : 4;
        int amt = color == "Copper" ? 8 : color == "Iron" ? 6 : 3;
        float hold = color == "Copper" ? 1.2f : color == "Iron" ? 1.8f : 3.5f;
        var srn = Node(parent, pf, g, id, label, amt, tier, 3, 5, hold, color == "Gold" ? 420f : 180f, color == "Gold" ? 900f : 420f, 2.2f, 0.9f);
        float sc = pf.Contains("Cluster") ? R(2.2f, 3.0f) : R(1.0f, 1.4f);
        var model = Place(srn.transform, pf, g, R(0f, 360f), sc);
        if (model != null && color == "Copper") OreCrystalConverter.ApplyCopperMaterial(model.transform);
        return 1;
    }

    static int StonePile(Transform parent, Vector3 g)
    {
        string pf = "PP_Rock_Pile_0" + (1 + rng.Next(9));
        var srn = Node(parent, pf, g, ItemIds.Stone, "돌 무더기", 8, 1, 2, 3, 1.0f, 120f, 240f, 2.0f, 0.6f);
        Place(srn.transform, pf, g, R(0f, 360f), R(0.36f, 0.45f));
        return 1;
    }

    static int WoodPile(Transform parent, Vector3 g, float yaw)
    {
        string pf = "PP_Log_Pile_0" + (1 + rng.Next(7));
        var srn = Node(parent, pf, g, ItemIds.Wood, "장작 더미", 8, 0, 2, 3, 0.8f, 120f, 240f, 1.8f, 0.4f);
        Place(srn.transform, pf, g, yaw, R(1.3f, 1.6f));
        return 1;
    }

    static int HideRack(Transform parent, Vector3 g, float yaw)
    {
        var srn = Node(parent, "HideRack", g, ItemIds.WetHide, "말리는 가죽", 1, 0, 1, 2, 1.0f, 300f, 600f, 1.8f, 1.0f);
        DryingRack(srn.transform, g, yaw);
        return 1;
    }

    static void Chest(Transform parent, Vector3 g, float yaw, TreasureChest.Tier tier)
    {
        string pf = tier == TreasureChest.Tier.Gold ? "PP_Treasure_Chest_03_Gold" : tier == TreasureChest.Tier.Iron ? "PP_Treasure_Chest_04_Iron" : "PP_Treasure_Chest_01_Copper";
        var root = new GameObject("TreasureChest_" + tier);
        root.layer = NodeLayer;
        root.transform.SetParent(parent, false);
        root.transform.position = g;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var sc = root.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 1.7f; sc.center = new Vector3(0f, 0.5f, 0f);
        var model = Place(root.transform, pf, g, yaw, 1.15f);
        Transform lid = null;
        if (model != null) foreach (var tr in model.GetComponentsInChildren<Transform>(true)) if (tr != model.transform && tr.name.Contains("Lid")) { lid = tr; break; }
        root.AddComponent<TreasureChest>().Configure(tier, lid, 20f);
        // 금 상자는 은은하게 빛난다
        if (tier == TreasureChest.Tier.Gold) AddLight(root.transform, "ChestGlow", g + Vector3.up * 1.2f, new Color(1f, 0.8f, 0.35f), 1.6f, 6f, 0.1f);
    }

    // ───────────── 본체 ─────────────

    [MenuItem("Tools/Last Truck/Map: Decorate Forest Mine Camp")]
    static void Menu() { Debug.Log("[CaveArenaMakeover]\n" + Run()); }

    public static string Run()
    {
        var sb = new StringBuilder();
        var fm = GameObject.Find("ForestMine");
        if (fm == null) return "ForestMine 을 찾을 수 없습니다";
        var old = fm.transform.Find(GroupName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder(ArtDir)) AssetDatabase.CreateFolder("Assets/Art", "CaveCamp");

        rng = new System.Random(20261121);
        placed = 0; Occupied.Clear(); Missing.Clear(); Mats.Clear();
        Physics.SyncTransforms();

        // 0) 원래 있던 신비 연출을 끈다 (지우지 않음)
        int off = 0;
        var pillars = fm.transform.Find("Pillars");
        var pillarSpots = new List<(Vector3 pos, float yaw)>();
        if (pillars != null)
            foreach (Transform p in pillars) { pillarSpots.Add((p.position, p.eulerAngles.y)); if (p.gameObject.activeSelf) { p.gameObject.SetActive(false); off++; } }
        var lightsRoot = fm.transform.Find("Lights");
        if (lightsRoot != null)
            foreach (Transform l in lightsRoot)
                if ((l.name.StartsWith("PillarGlow") || l.name == "CenterGlow") && l.gameObject.activeSelf) { l.gameObject.SetActive(false); off++; }
        var effects = fm.transform.Find("Effects");
        int thinned = 0;
        if (effects != null)
            foreach (Transform e in effects)
            {
                if ((e.name.StartsWith("FX_Mystic") || e.name.StartsWith("FX_Magic") || e.name.StartsWith("FX_Glow")) && e.gameObject.activeSelf) { e.gameObject.SetActive(false); off++; }
                // 짙은 흰 안개는 옅게 (값을 정해 두므로 여러 번 실행해도 같다)
                float alpha = e.name.StartsWith("FX_Fog_Big") ? 0.1f : e.name.StartsWith("FX_Fog_") ? 0.05f : -1f;
                if (alpha < 0f) continue;
                foreach (var ps in e.GetComponentsInChildren<ParticleSystem>(true)) { var m = ps.main; Color c0 = m.startColor.color; m.startColor = new Color(c0.r, c0.g, c0.b, alpha); thinned++; }
            }
        Physics.SyncTransforms();
        sb.AppendLine($"끈 신비 연출/룬 기둥: {off}개, 옅게 만든 안개: {thinned}");

        if (!Ground(Center.x, Center.y, out Vector3 cg)) return "광장 바닥을 찾지 못했습니다";

        var root = new GameObject(GroupName).transform;
        root.SetParent(fm.transform, false);
        Transform grp(string n) { var t = new GameObject(n).transform; t.SetParent(root, false); return t; }
        var lights = grp("Lights");

        // 1) 가운데 큰 모닥불
        var hearth = grp("Hearth");
        Claim(cg, 7.5f);
        string[] ringRocks = { "PP_Rock_01", "PP_Rock_02", "PP_Rock_03" };
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f;
            Vector2 p = Polar(2.6f, a);
            if (Ground(p.x, p.y, out Vector3 g)) Place(hearth, Pick(ringRocks), g, R(0f, 360f), R(0.32f, 0.42f), true);
        }
        Place(hearth, "PP_Campfire_01", cg, 0f, 2.1f, true);
        Fx(hearth, "FX_Campfire_01", cg + Vector3.up * 0.1f, 1.45f);
        Fx(hearth, "FX_Embers_01", cg + Vector3.up * 0.6f, 1.3f);
        AddLight(lights, "BonfireLight", cg + Vector3.up * 2.2f, new Color(1f, 0.55f, 0.22f), 9f, 42f, 0.2f);
        // 요리용 삼각대 + 냄비
        {
            var tri = Group(hearth, "CookTripod", cg, 20f);
            Vector3 apex = new Vector3(0f, 3.4f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f) * Mathf.Deg2Rad;
                Stick(tri, "Leg", new Vector3(Mathf.Cos(a) * 2.3f, 0f, Mathf.Sin(a) * 2.3f), apex + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * -0.25f + Vector3.up * 0.4f, 0.11f, DarkWood);
            }
            Stick(tri, "Rope", apex, new Vector3(0f, 1.75f, 0f), 0.03f, Rope);
            var pot = Place(tri, "SM_Gen_Prop_Pot_02", tri.TransformPoint(new Vector3(0f, 1.05f, 0f)), 0f, 2.0f, true);
            if (pot == null) Prim(tri, PrimitiveType.Sphere, "Pot", new Vector3(0f, 1.4f, 0f), new Vector3(0.8f, 0.7f, 0.8f), Charcoal);
        }
        // 통나무 의자
        for (int i = 0; i < 7; i++)
        {
            float a = i * (360f / 7f) + 10f;
            Vector2 p = Polar(6.4f, a);
            if (Ground(p.x, p.y, out Vector3 g)) LogSeat(hearth, g, a);   // 둘레 방향으로 눕힌다
        }

        // 2) 가죽 천막 8채 (입구가 모닥불 쪽)
        var camp = grp("Camp");
        Mesh cone = Frustum("TeepeeCone", 10, 0f, 1f, 1f);
        Mesh band = Frustum("TeepeeBand", 10, 0.56f, 0.66f, 1.015f);
        int tents = 0;
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f + 22.5f + R(-6f, 6f);
            if (InCorridorMouth(a)) a += 18f;
            float rad = R(21f, 27f);
            Vector2 p = Polar(rad, a);
            if (!Ground(p.x, p.y, out Vector3 g)) continue;
            float big = i == 2 ? 1.3f : R(0.9f, 1.1f);   // 하나는 우두머리 천막 (크게)
            if (!Free(g, 3.2f * big)) continue;
            Claim(g, 3.4f * big);
            Teepee(camp, g, Yaw(g, Center), 2.7f * big, 5.4f * big, cone, band, i);
            tents++;
        }
        sb.AppendLine("천막: " + tents);

        // 3) 토템 기둥 / 선돌 (원래 룬 기둥 10개 자리)
        var totems = grp("Totems");
        string[] menhirs = { "PP_Rock_Menhir_Moss_01", "PP_Rock_Menhir_Moss_02", "PP_Rock_Menhir_Moss_03", "PP_Rock_Menhir_01", "PP_Rock_Menhir_02" };
        int ti = 0;
        foreach (var (pos, _) in pillarSpots)
        {
            Vector3 g = Ground(pos.x, pos.z, out Vector3 gg) ? gg : pos;
            Claim(g, 2.5f);
            if (ti % 2 == 0) TotemPole(totems, g, Yaw(g, Center), ti, lights);
            else Place(totems, Pick(menhirs), g, R(0f, 360f), R(0.5f, 0.62f));
            ti++;
        }

        // 4) 천막 사이 생활 소품: 작은 화덕, 창 걸이, 뼈 무더기, 항아리/자루
        var life = grp("CampLife");
        System.Func<float, float, float, Vector3?> spot = (rmin, rmax, clear) =>
        {
            for (int t = 0; t < 80; t++)
            {
                float a = R(0f, 360f);
                if (InCorridorMouth(a)) continue;
                Vector2 p = Polar(Mathf.Sqrt(R(rmin * rmin, rmax * rmax)), a);
                if (!Ground(p.x, p.y, out Vector3 g)) continue;
                if (!Free(g, clear)) continue;
                Claim(g, clear);
                return g;
            }
            return null;
        };
        for (int i = 0; i < 3; i++)
        {
            var g = spot(28f, 36f, 2.5f); if (g == null) continue;
            Place(life, "PP_Campfire_02", g.Value, R(0f, 360f), 1.6f, true);
            Fx(life, "FX_Campfire_01", g.Value + Vector3.up * 0.05f, 0.7f);
            AddLight(lights, "PitLight", g.Value + Vector3.up * 1.4f, new Color(1f, 0.58f, 0.26f), 3.5f, 18f, 0.22f);
        }
        for (int i = 0; i < 4; i++) { var g = spot(14f, 32f, 1.8f); if (g != null) SpearRack(life, g.Value, Yaw(g.Value, Center) + 90f); }
        for (int i = 0; i < 6; i++) { var g = spot(14f, 44f, 1.4f); if (g != null) BonePile(life, g.Value); }
        string[] pots = { "SM_Gen_Prop_Pot_01", "SM_Gen_Prop_Pot_03", "SM_Gen_Prop_Pot_04", "SM_Gen_Prop_Pot_05", "SM_Gen_Prop_Sack_01", "SM_Gen_Prop_Sack_03", "SM_Gen_Prop_Sack_Stack_01" };
        for (int i = 0; i < 8; i++)
        {
            var g = spot(15f, 34f, 1.3f); if (g == null) continue;
            var cl = Group(life, "Stores", g.Value, R(0f, 360f));
            int n = 2 + rng.Next(3);
            for (int k = 0; k < n; k++) Place(cl, Pick(pots), cl.TransformPoint(new Vector3(R(-0.6f, 0.6f), 0f, R(-0.6f, 0.6f))), R(0f, 360f), R(1.6f, 2.2f), true);
        }
        // 입구 두 곳에 큰 짐승 머리뼈 (통로 양옆)
        foreach (float side in new[] { -1f, 1f })
            foreach (float zSide in new[] { -1f, 1f })
            {
                Vector2 p = new Vector2(Center.x + side * 13.5f, Center.y + zSide * 78f);
                if (!Ground(p.x, p.y, out Vector3 g)) continue;
                var skull = Place(life, "PP_Cave_Skull_01", g + Vector3.down * 0.3f, zSide > 0 ? 180f + side * 25f : side * -25f, 0.055f, true);
                if (skull != null) Claim(g, 3f);
            }

        // 5) 동굴 벽화
        var art = grp("CavePaintings");
        var paintMats = new[] { PaintingMat(0), PaintingMat(1), PaintingMat(2) };
        int paintings = 0;
        foreach (float a in new[] { 18f, 55f, 125f, 162f, 198f, 235f, 305f, 342f })
        {
            Vector2 dir2 = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
            Vector3 dir = new Vector3(dir2.x, 0f, dir2.y);
            Vector3 origin = new Vector3(Center.x, cg.y + 4.5f, Center.y) + dir * 50f;
            if (!Physics.Raycast(origin, dir, out RaycastHit hit, 70f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!hit.collider.name.StartsWith("Shell_")) continue;
            Vector3 n = hit.normal; n.y = 0f; if (n.sqrMagnitude < 0.01f) n = -dir; n.Normalize();
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "CavePainting";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(art, false);
            q.transform.position = hit.point + n * 0.35f;
            q.transform.rotation = Quaternion.LookRotation(-n);
            q.transform.localScale = new Vector3(9f, 4.5f, 1f);
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = paintMats[paintings % paintMats.Length];
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            paintings++;
        }
        sb.AppendLine("벽화: " + paintings);

        // 6) 채집: 바깥 둘레(벽 쪽) + 남북 통로
        var gather = grp("Gatherables");
        int ores = 0, stones = 0, woods = 0, hides = 0;
        string[] oreColors = { "Copper", "Copper", "Copper", "Iron", "Iron", "Copper", "Iron", "Gold", "Copper", "Iron", "Gold", "Iron", "Copper" };
        foreach (string c in oreColors) { var g = spot(60f, 76f, 2.6f); if (g != null) ores += Ore(gather, g.Value, c); }
        for (int i = 0; i < 7; i++) { var g = spot(52f, 76f, 2.2f); if (g != null) stones += StonePile(gather, g.Value); }
        for (int i = 0; i < 5; i++) { var g = spot(24f, 46f, 2.0f); if (g != null) woods += WoodPile(gather, g.Value, R(0f, 360f)); }
        for (int i = 0; i < 4; i++) { var g = spot(15f, 30f, 2.0f); if (g != null) hides += HideRack(gather, g.Value, Yaw(g.Value, Center) + 90f); }

        // 통로: 벽 쪽(가운데 길은 비운다)
        System.Func<float, float, float, Vector3?> corridorSpot = (z0, z1, clear) =>
        {
            for (int t = 0; t < 60; t++)
            {
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float x = Center.x + side * R(9.5f, CorridorHalf - 2f), z = R(z0, z1);
                if (!Ground(x, z, out Vector3 g)) continue;
                if (!Free(g, clear)) continue;
                Claim(g, clear);
                return g;
            }
            return null;
        };
        foreach (var (z0, z1) in new[] { (465f, 560f), (195f, 280f) })
        {
            foreach (string c in new[] { "Copper", "Iron", "Copper", "Iron", "Gold" }) { var g = corridorSpot(z0, z1, 2.6f); if (g != null) ores += Ore(gather, g.Value, c); }
            for (int i = 0; i < 2; i++) { var g = corridorSpot(z0, z1, 2.2f); if (g != null) stones += StonePile(gather, g.Value); }
        }
        sb.AppendLine($"채집: 광맥 {ores}, 돌 무더기 {stones}, 장작 {woods}, 말리는 가죽 {hides}");

        // 7) 보물상자 6개
        var chests = grp("TreasureChests");
        int chestCount = 0;
        void PutChest(Vector3? g, TreasureChest.Tier tier) { if (g == null) return; Chest(chests, g.Value, Yaw(g.Value, Center), tier); chestCount++; }
        PutChest(spot(27f, 31f, 1.6f), TreasureChest.Tier.Wood);   // 천막 뒤
        PutChest(spot(30f, 36f, 1.6f), TreasureChest.Tier.Wood);
        PutChest(spot(64f, 74f, 1.6f), TreasureChest.Tier.Iron);   // 벽 쪽
        PutChest(spot(64f, 74f, 1.6f), TreasureChest.Tier.Wood);
        PutChest(corridorSpot(540f, 570f, 1.6f), TreasureChest.Tier.Gold);   // 북쪽 통로 끝
        PutChest(corridorSpot(200f, 225f, 1.6f), TreasureChest.Tier.Iron);   // 남쪽 통로 끝
        sb.AppendLine("보물상자: " + chestCount);

        // 8) 바깥 둘레 자연 장식 (석순, 이끼 바위, 버섯)
        var nature = grp("Nature");
        string[] stalag = { "PP_Stalagmite_01", "PP_Stalagmite_02", "PP_Stalagmite_03", "PP_Stalagmite_04", "PP_Stalagmite_05" };
        string[] rocks = { "PP_Rock_Moss_01", "PP_Rock_Moss_02", "PP_Rock_Moss_03", "PP_Rock_Moss_04", "PP_Rock_Pile_Moss_01", "PP_Rock_Pile_Moss_03" };
        string[] mush = { "PP_Mushrooms_Brown_01", "PP_Mushrooms_Brown_02", "PP_Mushrooms_Brown_03", "PP_Mushrooms_Stone_Brown_01", "PP_Mushroom_Long_Dark_Brown_01", "PP_Mushroom_Long_Dark_Brown_03" };
        for (int i = 0; i < 22; i++) { var g = spot(48f, 80f, 1.8f); if (g != null) Place(nature, Pick(stalag), g.Value + Vector3.down * 0.05f, R(0f, 360f), R(0.8f, 1.7f)); }
        for (int i = 0; i < 18; i++) { var g = spot(44f, 80f, 1.8f); if (g != null) Place(nature, Pick(rocks), g.Value + Vector3.down * 0.05f, R(0f, 360f), R(0.7f, 1.3f)); }
        for (int i = 0; i < 24; i++) { var g = spot(38f, 80f, 1.0f); if (g != null) Place(nature, Pick(mush), g.Value, R(0f, 360f), R(0.9f, 1.6f), true); }

        EditorUtility.SetDirty(root.gameObject);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        sb.AppendLine("총 배치 " + placed + "개, 바닥 높이 " + cg.y.ToString("0.00"));
        if (Missing.Count > 0) sb.AppendLine("없는 프리팹: " + string.Join(", ", Missing));
        return sb.ToString();
    }
}
#endif
