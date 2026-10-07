#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ForestMineRoundBuilder
{
    public const float X = -355f;
    public const float FY = 1.5f;
    public const float FrontZ = 177f;
    public const float ExitZ = 570f;
    public static float HZ = 372f;
    public static float HallR = 88f;
    public static float HallH = 46f;
    public static float PassR = 17f;

    static Dictionary<string, string> _paths;
    static GameObject P(string name)
    {
        if (_paths == null)
        {
            _paths = new Dictionary<string, string>();
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/PurePoly/Mining_Pack/Prefabs" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                _paths[System.IO.Path.GetFileNameWithoutExtension(p)] = p;
            }
        }
        return _paths.ContainsKey(name) ? AssetDatabase.LoadAssetAtPath<GameObject>(_paths[name]) : null;
    }

    static float N3(float x, float y, float z, float s)
    {
        float a = Mathf.PerlinNoise(x * s + 1011f, y * s + 1005f);
        float b = Mathf.PerlinNoise(y * s + 1007f, z * s + 1003f);
        float c = Mathf.PerlinNoise(z * s + 1019f, x * s + 1023f);
        return (a + b + c) / 3f - 0.5f;
    }

    public static string ClearOld()
    {
        var mine = GameObject.Find("ForestMine");
        var sb = new StringBuilder();
        var del = new List<GameObject>();
        foreach (Transform t in mine.transform) if (t.name != "Frames") del.Add(t.gameObject);
        foreach (var d in del) Object.DestroyImmediate(d);
        sb.AppendLine("cleared old mine children=" + del.Count);
        return sb.ToString();
    }

    public static string BuildShell()
    {
        var sb = new StringBuilder();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        float x0 = X, fy = FY, hz = HZ, hr = HallR, hh = HallH, pr = PassR;

        System.Func<float, float, float, float> voidD = (x, y, z) =>
        {
            float dx = (x - x0) / hr, dy = (y - fy) / hh, dz = (z - hz) / hr;
            float hall = (Mathf.Sqrt(dx * dx + dy * dy + dz * dz) - 1f) * Mathf.Min(hr, hh * 1.3f);
            hall = Mathf.Max(hall, fy - y);
            hall += 1.6f * N3(x, y, z, 0.05f) * 2f + 0.6f * N3(x, y, z, 0.16f) * 2f;
            // passage: elliptical cross-section tube along z, full length
            float py = (y - (fy + 3f)) * 0.72f;
            float pass = Mathf.Sqrt((x - x0) * (x - x0) + py * py) - pr;
            pass = Mathf.Max(pass, fy - y);
            pass += 0.5f * N3(x, y, z, 0.12f) * 2f;
            pass = Mathf.Max(pass, Mathf.Max((FrontZ - 6f) - z - 0f, -1e9f)); // allow open at front (handled by plane)
            pass = Mathf.Max(pass, -1e9f);
            float ocu = Mathf.Max(Mathf.Sqrt((x - x0) * (x - x0) + (z - hz) * (z - hz)) - 8f, (fy + 30f) - y);
            return Mathf.Min(hall, Mathf.Min(pass, ocu));
        };
        System.Func<float, float, float, float> rockD = (x, y, z) =>
        {
            float dx = (x - x0) / 120f, dy = (y - (fy - 8f)) / 72f, dz = (z - hz) / 118f;
            float dome = (Mathf.Sqrt(dx * dx + dy * dy + dz * dz) - 1f) * 72f;
            // hull along the passages
            float hy = (y - (fy + 4f)) * 0.78f;
            float hull = Mathf.Sqrt((x - x0) * (x - x0) + hy * hy) - 38f;
            float m = Mathf.Min(dome, hull);
            m += 3.5f * N3(x, y, z, 0.03f) * 2f + 1.5f * N3(x, y, z, 0.085f) * 2f;
            m = Mathf.Max(m, (fy - 6f) - y);
            // front / back clip planes
            float sideT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 62f, Mathf.Abs(x - x0)));
            float front = Mathf.Lerp(FrontZ, 172.5f, sideT) + Mathf.Lerp(0.7f, 0.12f, sideT) * (y - fy) - z;
            float back = z - (ExitZ + 10f - 0.6f * (y - fy));
            m = Mathf.Max(m, Mathf.Max(front, back));
            float r = Mathf.Max(m, -voidD(x, y, z));
            // entrance and exit aprons
            float slabF = Mathf.Max(Mathf.Abs(x - x0) - 24f, Mathf.Max(Mathf.Abs(y - (fy - 3.15f)) - 3f, Mathf.Abs(z - 178f) - 13f));
            float slabE = Mathf.Max(Mathf.Abs(x - x0) - 24f, Mathf.Max(Mathf.Abs(y - (fy - 3.15f)) - 3f, Mathf.Abs(z - (ExitZ + 6f)) - 20f));
            return Mathf.Min(r, Mathf.Min(slabF, slabE));
        };

        float cs = 3.0f;
        Vector3 org = new Vector3(x0 - 140f, fy - 9f, 160f);
        int nx = Mathf.CeilToInt(280f / cs), ny = Mathf.CeilToInt(80f / cs), nz = Mathf.CeilToInt(450f / cs);
        int sx = nx + 1, sy = ny + 1, sz = nz + 1;
        float[] F = new float[sx * sy * sz];
        for (int k = 0; k < sz; k++)
            for (int j = 0; j < sy; j++)
                for (int i = 0; i < sx; i++)
                    F[(k * sy + j) * sx + i] = rockD(org.x + i * cs, org.y + j * cs, org.z + k * cs);

        int[] cellVert = new int[nx * ny * nz];
        for (int i = 0; i < cellVert.Length; i++) cellVert[i] = -1;
        var verts = new List<Vector3>();
        int[,] edgeA = { { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 }, { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
        float[] cv = new float[8];
        for (int k = 0; k < nz; k++)
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int mask = 0;
                    for (int c = 0; c < 8; c++) { cv[c] = F[((k + ((c >> 2) & 1)) * sy + (j + ((c >> 1) & 1))) * sx + (i + (c & 1))]; if (cv[c] < 0f) mask |= 1 << c; }
                    if (mask == 0 || mask == 255) continue;
                    Vector3 sum = Vector3.zero; int cnt = 0;
                    for (int e = 0; e < 12; e++)
                    {
                        int a = edgeA[e, 0], b = edgeA[e, 1];
                        if ((cv[a] < 0f) == (cv[b] < 0f)) continue;
                        float t = cv[a] / (cv[a] - cv[b]);
                        sum += Vector3.Lerp(new Vector3(i + (a & 1), j + ((a >> 1) & 1), k + ((a >> 2) & 1)), new Vector3(i + (b & 1), j + ((b >> 1) & 1), k + ((b >> 2) & 1)), t); cnt++;
                    }
                    cellVert[(k * ny + j) * nx + i] = verts.Count;
                    verts.Add(org + (sum / cnt) * cs);
                }
        var tri = new List<int>();
        for (int k = 1; k < nz; k++)
            for (int j = 1; j < ny; j++)
                for (int i = 1; i < nx; i++)
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int i1 = i + (axis == 0 ? 1 : 0), j1 = j + (axis == 1 ? 1 : 0), k1 = k + (axis == 2 ? 1 : 0);
                        if (i1 > nx || j1 > ny || k1 > nz) continue;
                        if ((F[(k * sy + j) * sx + i] < 0f) == (F[(k1 * sy + j1) * sx + i1] < 0f)) continue;
                        int[] c4 = new int[4];
                        if (axis == 0) { c4[0] = cellVert[((k - 1) * ny + (j - 1)) * nx + i]; c4[1] = cellVert[((k - 1) * ny + j) * nx + i]; c4[2] = cellVert[(k * ny + j) * nx + i]; c4[3] = cellVert[(k * ny + (j - 1)) * nx + i]; }
                        else if (axis == 1) { c4[0] = cellVert[((k - 1) * ny + j) * nx + (i - 1)]; c4[1] = cellVert[(k * ny + j) * nx + (i - 1)]; c4[2] = cellVert[(k * ny + j) * nx + i]; c4[3] = cellVert[((k - 1) * ny + j) * nx + i]; }
                        else { c4[0] = cellVert[(k * ny + (j - 1)) * nx + (i - 1)]; c4[1] = cellVert[(k * ny + j) * nx + (i - 1)]; c4[2] = cellVert[(k * ny + j) * nx + i]; c4[3] = cellVert[(k * ny + (j - 1)) * nx + i]; }
                        if (c4[0] < 0 || c4[1] < 0 || c4[2] < 0 || c4[3] < 0) continue;
                        tri.Add(c4[0]); tri.Add(c4[1]); tri.Add(c4[2]); tri.Add(c4[0]); tri.Add(c4[2]); tri.Add(c4[3]);
                    }
        int triCount = tri.Count / 3;
        Vector2 uvGrey = new Vector2(0.68f, 0.96f), uvGrass = new Vector2(0.852f, 0.813f), uvDirt = new Vector2(0.578f, 0.422f);
        const float cell = 20f;
        var bV = new Dictionary<long, List<Vector3>>(); var bN = new Dictionary<long, List<Vector3>>(); var bU = new Dictionary<long, List<Vector2>>();
        float e0 = 0.9f; int flipped = 0;
        System.Func<Vector3, Vector3> grad = c => new Vector3(
            rockD(c.x + e0, c.y, c.z) - rockD(c.x - e0, c.y, c.z),
            rockD(c.x, c.y + e0, c.z) - rockD(c.x, c.y - e0, c.z),
            rockD(c.x, c.y, c.z + e0) - rockD(c.x, c.y, c.z - e0));
        for (int t = 0; t < triCount; t++)
        {
            Vector3 a = verts[tri[t * 3]], b = verts[tri[t * 3 + 1]], c = verts[tri[t * 3 + 2]];
            Vector3 cr = Vector3.Cross(b - a, c - a);
            if (cr.sqrMagnitude < 1e-6f) continue;
            Vector3 cen = (a + b + c) / 3f;
            Vector3 gc = grad(cen);
            if (Vector3.Dot(cr, gc) < 0f) { Vector3 tmp = b; b = c; c = tmp; flipped++; }
            Vector3 n0 = grad(a).normalized, n1 = grad(b).normalized, n2 = grad(c).normalized;
            Vector3 fn = gc.normalized;
            bool inside = voidD(cen.x, cen.y, cen.z) < 2.4f;
            bool apron = Mathf.Abs(cen.x - x0) < 25f && fn.y > 0.6f && cen.y < fy + 0.6f && ((cen.z > 160f && cen.z < 192f) || (cen.z > ExitZ - 12f && cen.z < ExitZ + 30f));
            Vector2 uv = (inside || apron) ? (fn.y > 0.7f ? uvDirt : uvGrey) : (fn.y > 0.8f ? uvGrass : uvGrey);
            long kx = Mathf.FloorToInt((cen.x + 1500f) / cell), ky = Mathf.FloorToInt((cen.y + 100f) / cell), kz = Mathf.FloorToInt((cen.z + 1500f) / cell);
            long key = (kx * 1000L + ky) * 1000L + kz;
            if (!bV.ContainsKey(key)) { bV[key] = new List<Vector3>(); bN[key] = new List<Vector3>(); bU[key] = new List<Vector2>(); }
            bV[key].Add(a); bV[key].Add(b); bV[key].Add(c);
            bN[key].Add(n0); bN[key].Add(n1); bN[key].Add(n2);
            bU[key].Add(uv); bU[key].Add(uv); bU[key].Add(uv);
        }
        var mine = GameObject.Find("ForestMine").transform;
        var old = mine.Find("RoundShell"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var parent = new GameObject("RoundShell"); parent.transform.SetParent(mine);
        var mat = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PurePoly/Mining_Pack/Prefabs/Cave/PP_Cave_Tube_04.prefab").GetComponent<MeshRenderer>().sharedMaterial;
        string path = "Assets/Scenes/MiningMapAssets/ForestMineRound.asset";
        if (System.IO.File.Exists(path)) AssetDatabase.DeleteAsset(path);
        int n = 0; bool first = true;
        foreach (var kv in bV)
        {
            var vv = kv.Value; int cnt = vv.Count;
            var idxs = new int[cnt]; for (int q = 0; q < cnt; q++) idxs[q] = q;
            var cm = new Mesh { name = "MineRound_" + n };
            cm.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            cm.SetVertices(vv); cm.SetNormals(bN[kv.Key]); cm.SetUVs(0, bU[kv.Key]); cm.SetTriangles(idxs, 0); cm.RecalculateBounds();
            if (first) { AssetDatabase.CreateAsset(cm, path); first = false; } else AssetDatabase.AddObjectToAsset(cm, path);
            var go = new GameObject("Shell_" + n); go.transform.SetParent(parent.transform);
            go.AddComponent<MeshFilter>().sharedMesh = cm; go.AddComponent<MeshRenderer>().sharedMaterial = mat; go.AddComponent<MeshCollider>().sharedMesh = cm;
            n++;
        }
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        sb.AppendLine($"shell tris={triCount} flipped={flipped} chunks={n} t={sw.ElapsedMilliseconds}ms");
        return sb.ToString();
    }

    static bool Cast(Vector3 o, Vector3 d, float dist, out RaycastHit best)
    {
        best = default; float bd = float.MaxValue; bool found = false;
        var shell = GameObject.Find("ForestMine").transform.Find("RoundShell");
        foreach (var h in Physics.RaycastAll(o, d, dist))
        {
            if (h.collider.isTrigger || !h.collider.transform.IsChildOf(shell)) continue;
            if (h.distance < bd) { bd = h.distance; best = h; found = true; }
        }
        return found;
    }

    static GameObject Spawn(string prefab, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var pf = P(prefab); if (pf == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.position = pos; go.transform.rotation = rot; go.transform.localScale = scale;
        return go;
    }

    static Light AddLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range, LightType type = LightType.Point)
    {
        var g = new GameObject(name); g.transform.SetParent(parent); g.transform.position = pos;
        var l = g.AddComponent<Light>(); l.type = type; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        return l;
    }

    static Transform Group(Transform root, string name)
    {
        var t = root.Find(name); if (t != null) Object.DestroyImmediate(t.gameObject);
        var g = new GameObject(name); g.transform.SetParent(root); return g.transform;
    }

    public static string Dress(int seed)
    {
        var sb = new StringBuilder();
        var rng = new System.Random(seed);
        System.Func<float, float, float> Rr = (a, b) => a + (b - a) * (float)rng.NextDouble();
        Physics.SyncTransforms();
        var mine = GameObject.Find("ForestMine").transform;
        var gT = Group(mine, "Torches"); var gP = Group(mine, "Pillars"); var gE = Group(mine, "Effects"); var gL = Group(mine, "Lights"); var gF = Group(mine, "ExitFrames");
        float x0 = X, fy = FY, hz = HZ;
        Vector3 C = new Vector3(x0, fy, hz);
        Color warm = new Color(1f, 0.62f, 0.25f), blue = new Color(0.35f, 0.6f, 1f), pale = new Color(0.82f, 0.9f, 1f);
        string[] torchNames = { "PP_Torch_Standing_01", "PP_Torch_Standing_03", "PP_Torch_Standing_04", "PP_Torch_Standing_06", "PP_Torch_Standing_07" };
        string[] fxNames = { "FX_Fire_Torch_01", "FX_Fire_Torch_02", "FX_Fire_Torch_03" };
        int nTorch = 0;
        System.Func<Vector3, float, float, bool> torchAt = (pos, range, inten) =>
        {
            var torch = Spawn(torchNames[rng.Next(torchNames.Length)], gT, pos, Quaternion.Euler(0, Rr(0, 360), 0), Vector3.one * 2.6f);
            if (torch == null) return false;
            Vector3 tip = pos + Vector3.up * 5.3f;
            Spawn(fxNames[rng.Next(fxNames.Length)], torch.transform, tip, Quaternion.identity, Vector3.one * 2.0f);
            AddLight(torch.transform, "TorchLight", tip + Vector3.up * 0.8f, warm, inten, range);
            return true;
        };

        // hall wall ring
        int ring = 26;
        for (int i = 0; i < ring; i++)
        {
            float a = i / (float)ring * 360f + 6f;
            Vector3 d = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad));
            if (Mathf.Abs(d.x) < 0.2f) continue; // passage directions (north/south)
            if (!Cast(C + Vector3.up * 5f, d, 140f, out var hw)) continue;
            Vector3 tp = hw.point - d * 2.2f;
            if (!Cast(new Vector3(tp.x, fy + 8f, tp.z), Vector3.down, 24f, out var hf)) continue;
            if (torchAt(hf.point, 32f, 7f)) nTorch++;
        }
        // passage torches every 24 units, both walls
        foreach (var span in new[] { new Vector2(190f, 280f), new Vector2(464f, 560f) })
            for (float z = span.x; z <= span.y; z += 24f)
                foreach (float sg in new[] { -1f, 1f })
                {
                    if (!Cast(new Vector3(x0, fy + 6f, z), new Vector3(sg, 0, 0), 40f, out var hw)) continue;
                    Vector3 tp = hw.point - new Vector3(sg, 0, 0) * 2f;
                    if (!Cast(new Vector3(tp.x, fy + 8f, tp.z), Vector3.down, 24f, out var hf)) continue;
                    if (torchAt(hf.point, 28f, 6.5f)) nTorch++;
                }
        // outside entrance + exit torches
        foreach (float sx in new[] { -22f, 22f })
        {
            torchAt(new Vector3(x0 + sx, 1.6f, 170f), 30f, 6f); nTorch++;
            if (Cast(new Vector3(x0 + sx, 40f, ExitZ + 14f), Vector3.down, 80f, out var he)) { torchAt(he.point, 30f, 6f); nTorch++; }
        }
        sb.AppendLine("torches=" + nTorch);

        // exit frames facing the desert
        Spawn("PP_Mine_Entrance_Wooden_01", gF, new Vector3(x0, fy, ExitZ + 8f), Quaternion.Euler(0, 180f, 0), new Vector3(3.1f, 2.7f, 2.7f));

        // rune pillar circle
        string[] pillars = { "PP_Pillar_Stone_Rune_01", "PP_Pillar_Stone_Rune_02", "PP_Pillar_Stone_Rune_03" };
        int nPil = 0;
        for (int i = 0; i < 10; i++)
        {
            float a = (i / 10f) * 360f + 18f;
            Vector3 pos = C + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad)) * 54f;
            if (!Cast(new Vector3(pos.x, fy + 10f, pos.z), Vector3.down, 30f, out var hf)) continue;
            var pl = Spawn(pillars[i % pillars.Length], gP, hf.point - Vector3.up * 0.1f, Quaternion.Euler(0, -a + 90f, 0), Vector3.one * 3.4f);
            if (pl == null) continue;
            Spawn("FX_Glow_0" + (1 + rng.Next(4)), gE, hf.point + Vector3.up * 15f, Quaternion.identity, Vector3.one * 4f);
            AddLight(gL, "PillarGlow_" + i, hf.point + Vector3.up * 10f, blue, 5f, 36f);
            nPil++;
        }
        sb.AppendLine("pillars=" + nPil);

        // center magic circle
        float cfy = fy;
        if (Cast(new Vector3(C.x, fy + 10f, C.z), Vector3.down, 30f, out var hc)) cfy = hc.point.y;
        Vector3 cc = new Vector3(C.x, cfy, C.z);
        Spawn("FX_Mystic Ring_Large_01", gE, cc + Vector3.up * 0.25f, Quaternion.identity, Vector3.one * 8f);
        Spawn("FX_Mystic Ring_Large_02", gE, cc + Vector3.up * 0.3f, Quaternion.identity, Vector3.one * 13f);
        Spawn("FX_Mystic Ring_Large_03", gE, cc + Vector3.up * 0.35f, Quaternion.identity, Vector3.one * 19f);
        Spawn("FX_Mystic_Shine_01", gE, cc + Vector3.up * 5f, Quaternion.identity, Vector3.one * 3.2f);
        Spawn("FX_Magic_Fire_01", gE, cc + Vector3.up * 0.4f, Quaternion.identity, Vector3.one * 3.6f);
        AddLight(gL, "CenterGlow", cc + Vector3.up * 7f, blue, 14f, 85f);
        AddLight(gL, "CenterWarm", cc + Vector3.up * 14f, warm, 6f, 110f);

        // light shaft through oculus
        float apexY = fy + 54f;
        var shaft = AddLight(gL, "OculusSpot", new Vector3(x0, apexY, hz), pale, 30f, 100f, LightType.Spot);
        shaft.transform.rotation = Quaternion.Euler(90f, 0, 0); shaft.spotAngle = 24f; shaft.innerSpotAngle = 10f;
        for (int i = 0; i < 8; i++)
            Spawn(i % 2 == 0 ? "FX_Fog_02" : "FX_Fog_06", gE, new Vector3(x0 + Rr(-3f, 3f), fy + 5f + i * 6.5f, hz + Rr(-3f, 3f)), Quaternion.identity, Vector3.one * 1.1f);
        for (int i = 0; i < 4; i++)
            Spawn("FX_Mystic_Shine_01", gE, new Vector3(x0 + Rr(-4f, 4f), fy + 14f + i * 9f, hz + Rr(-4f, 4f)), Quaternion.identity, Vector3.one * 1.6f);

        // floor fog + drifting glow + candles
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * 6.283f; float r = Rr(18f, 62f);
            Spawn("FX_Fog_Big_0" + (1 + i % 3), gE, new Vector3(x0 + Mathf.Cos(a) * r, fy + 1.2f, hz + Mathf.Sin(a) * r), Quaternion.identity, Vector3.one * 0.55f);
        }
        for (int i = 0; i < 24; i++)
        {
            float a = Rr(0, 6.283f), r = Rr(6f, 74f);
            Spawn("FX_Glow_0" + (1 + rng.Next(4)), gE, new Vector3(x0 + Mathf.Cos(a) * r, fy + Rr(2f, 22f), hz + Mathf.Sin(a) * r), Quaternion.identity, Vector3.one * 4f);
        }
        for (int i = 0; i < 10; i++)
        {
            float a = (i / 10f) * 360f + 18f;
            Vector3 pos = C + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad)) * 48f;
            if (Cast(new Vector3(pos.x, fy + 10f, pos.z), Vector3.down, 30f, out var hf))
                Spawn("FX_Candle_Light_01", gE, hf.point + Vector3.up * 0.3f, Quaternion.identity, Vector3.one * 2.4f);
        }
        // fill lights (hall + passages)
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * 6.283f + 0.5f;
            AddLight(gL, "Fill_" + i, new Vector3(x0 + Mathf.Cos(a) * 40f, fy + 16f, hz + Mathf.Sin(a) * 40f), new Color(1f, 0.75f, 0.5f), 10f, 90f);
        }
        foreach (float z in new[] { 205f, 240f, 275f, 470f, 510f, 545f })
            AddLight(gL, "FillPass_" + z, new Vector3(x0, fy + 12f, z), new Color(1f, 0.7f, 0.4f), 5f, 50f);
        Physics.SyncTransforms();
        return sb.ToString();
    }
}
#endif
