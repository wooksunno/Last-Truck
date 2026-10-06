#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ForestRoundCaveBuilder
{
    public const float XM = -502f;
    public const float ZM = 40f;
    public const float FY = 0.9f;
    public static float HX = -586f;
    public static float HallR = 50f;
    public static float HallH = 30f;

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

    public static string ClearOldHalf()
    {
        var sb = new StringBuilder();
        string[] groups = { "Cave", "Cave Props", "Terrain_Stone", "Stones&Rocks", "Vegetation", "Crystals&Ores&Veins", "Coins", "Props", "Mushrooms", "Rails&Mine Carts", "Runes", "Bridges", "Stalactite&Stalagmite&Stalagnate", "Lighting", "FX" };
        string[] keep = { "PP_Stone_Cave_Entrance_03 (1)", "PP_Mine_Entrance_Wooden_01 (1)", "PP_Mine_Entrance_Wooden_03 (1)" };
        var del = new List<GameObject>();
        foreach (var g in groups)
        {
            var go = GameObject.Find(g); if (go == null) continue;
            foreach (Transform t in go.transform)
            {
                var p = t.position;
                if (p.x > -495f || p.x < -1000f || p.z < -230f || p.z > 270f) continue;
                if (System.Array.IndexOf(keep, t.name) >= 0) continue;
                del.Add(t.gameObject);
            }
        }
        var seals = GameObject.Find("TunnelCutSeals");
        if (seals != null) foreach (Transform t in seals.transform) if (t.position.z < 300f) del.Add(t.gameObject);
        foreach (var d in del) Object.DestroyImmediate(d);
        sb.AppendLine("removed old tunnel objects=" + del.Count);
        return sb.ToString();
    }

    public static string BuildMass()
    {
        var sb = new StringBuilder();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        float hx = HX, hr = HallR, hh = HallH, fy = FY;

        System.Func<float, float, float, float> voidD = (x, y, z) =>
        {
            float dx = (x - hx) / hr, dy = (y - fy) / hh, dz = (z - ZM) / hr;
            float hall = (Mathf.Sqrt(dx * dx + dy * dy + dz * dz) - 1f) * Mathf.Min(hr, hh * 1.25f);
            hall = Mathf.Max(hall, fy - y);
            hall += 1.2f * N3(x, y, z, 0.07f) * 2f + 0.5f * N3(x, y, z, 0.2f) * 2f;
            // passage: circular cross-section tube along -x
            float pcy = fy + 6.3f;
            float tdx = Mathf.Clamp(x, hx + 28f, XM + 4f);
            float pass = Mathf.Sqrt((x - tdx) * (x - tdx) + (y - pcy) * (y - pcy) + (z - ZM) * (z - ZM)) - 6.8f;
            pass = Mathf.Max(pass, fy - y);
            pass += 0.35f * N3(x, y, z, 0.15f) * 2f;
            // oculus in the dome apex
            float ocu = Mathf.Max(Mathf.Sqrt((x - hx) * (x - hx) + (z - ZM) * (z - ZM)) - 6.5f, (fy + 14f) - y);
            return Mathf.Min(hall, Mathf.Min(pass, ocu));
        };
        System.Func<float, float, float, float> rockD = (x, y, z) =>
        {
            float dx = (x - hx) / 88f, dy = (y - (fy - 6f)) / 56f, dz = (z - ZM) / 84f;
            float mass = (Mathf.Sqrt(dx * dx + dy * dy + dz * dz) - 1f) * 56f;
            mass += 3.5f * N3(x, y, z, 0.03f) * 2f + 1.6f * N3(x, y, z, 0.08f) * 2f;
            mass = Mathf.Max(mass, (fy - 6f) - y);
            return Mathf.Max(mass, -voidD(x, y, z));
        };

        float cs = 2.4f;
        Vector3 org = new Vector3(-690f, fy - 8f, -58f);
        int nx = Mathf.CeilToInt(205f / cs), ny = Mathf.CeilToInt(68f / cs), nz = Mathf.CeilToInt(190f / cs);
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
            bool inside = voidD(cen.x, cen.y, cen.z) < 2.2f;
            Vector2 uv;
            if (inside) uv = fn.y > 0.7f ? uvDirt : uvGrey;
            else uv = fn.y > 0.8f ? uvGrass : uvGrey;
            long kx = Mathf.FloorToInt((cen.x + 1500f) / cell), ky = Mathf.FloorToInt((cen.y + 100f) / cell), kz = Mathf.FloorToInt((cen.z + 1500f) / cell);
            long key = (kx * 1000L + ky) * 1000L + kz;
            if (!bV.ContainsKey(key)) { bV[key] = new List<Vector3>(); bN[key] = new List<Vector3>(); bU[key] = new List<Vector2>(); }
            bV[key].Add(a); bV[key].Add(b); bV[key].Add(c);
            bN[key].Add(n0); bN[key].Add(n1); bN[key].Add(n2);
            bU[key].Add(uv); bU[key].Add(uv); bU[key].Add(uv);
        }
        var old = GameObject.Find("ForestRoundCave"); if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("ForestRoundCave");
        var parent = new GameObject("Shell"); parent.transform.SetParent(root.transform);
        var mat = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PurePoly/Mining_Pack/Prefabs/Cave/PP_Cave_Tube_04.prefab").GetComponent<MeshRenderer>().sharedMaterial;
        string path = "Assets/Scenes/MiningMapAssets/ForestRoundCave.asset";
        if (System.IO.File.Exists(path)) AssetDatabase.DeleteAsset(path);
        int n = 0; bool first = true;
        foreach (var kv in bV)
        {
            var vv = kv.Value; int cnt = vv.Count;
            var idxs = new int[cnt]; for (int q = 0; q < cnt; q++) idxs[q] = q;
            var cm = new Mesh { name = "RoundCave_" + n };
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

    static bool CastShell(Vector3 o, Vector3 d, float dist, out RaycastHit best)
    {
        best = default; float bd = float.MaxValue; bool found = false;
        var shell = GameObject.Find("ForestRoundCave");
        foreach (var h in Physics.RaycastAll(o, d, dist))
        {
            if (h.collider.isTrigger || !h.collider.transform.IsChildOf(shell.transform)) continue;
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

    public static string Dress(int seed)
    {
        var sb = new StringBuilder();
        var rng = new System.Random(seed);
        System.Func<float, float, float> Rr = (a, b) => a + (b - a) * (float)rng.NextDouble();
        Physics.SyncTransforms();
        var root = GameObject.Find("ForestRoundCave").transform;
        foreach (var nm in new[] { "Torches", "Pillars", "Effects", "Lights" }) { var o = root.Find(nm); if (o != null) Object.DestroyImmediate(o.gameObject); }
        var gT = new GameObject("Torches").transform; gT.SetParent(root);
        var gP = new GameObject("Pillars").transform; gP.SetParent(root);
        var gE = new GameObject("Effects").transform; gE.SetParent(root);
        var gL = new GameObject("Lights").transform; gL.SetParent(root);

        float hx = HX, fy = FY;
        Vector3 C = new Vector3(hx, fy, ZM);
        Color warm = new Color(1f, 0.62f, 0.25f), blue = new Color(0.35f, 0.6f, 1f), pale = new Color(0.82f, 0.9f, 1f);

        // ---- wall torch ring ----
        string[] torchNames = { "PP_Torch_Standing_01", "PP_Torch_Standing_03", "PP_Torch_Standing_04", "PP_Torch_Standing_06" };
        string[] fxNames = { "FX_Fire_Torch_01", "FX_Fire_Torch_02", "FX_Fire_Torch_03" };
        int nTorch = 0;
        int ring = 20;
        for (int i = 0; i < ring; i++)
        {
            float a = i / (float)ring * 360f + 9f;
            Vector3 d = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad));
            if (d.x > 0.86f) continue; // passage side
            if (!CastShell(C + Vector3.up * 5f, d, 90f, out var hw)) continue;
            Vector3 tp = hw.point - d * 2.0f;
            if (!CastShell(new Vector3(tp.x, fy + 6f, tp.z), Vector3.down, 20f, out var hf)) continue;
            var torch = Spawn(torchNames[rng.Next(torchNames.Length)], gT, hf.point, Quaternion.Euler(0, Rr(0, 360), 0), Vector3.one * 2.6f);
            if (torch == null) continue;
            Vector3 tip = hf.point + Vector3.up * 5.3f;
            Spawn(fxNames[rng.Next(fxNames.Length)], torch.transform, tip, Quaternion.identity, Vector3.one * 2.0f);
            AddLight(torch.transform, "TorchLight", tip + Vector3.up * 0.8f, warm, 7f, 30f);
            nTorch++;
        }
        // passage torches
        foreach (float px in new[] { -516f, -532f, -548f })
            foreach (float sg in new[] { -1f, 1f })
            {
                if (!CastShell(new Vector3(px, fy + 5f, ZM), new Vector3(0, 0, sg), 20f, out var hw)) continue;
                Vector3 tp = hw.point - new Vector3(0, 0, sg) * 1.8f;
                if (!CastShell(new Vector3(tp.x, fy + 6f, tp.z), Vector3.down, 20f, out var hf)) continue;
                var torch = Spawn("PP_Torch_Standing_03", gT, hf.point, Quaternion.identity, Vector3.one * 2.4f);
                if (torch == null) continue;
                Vector3 tip = hf.point + Vector3.up * 4.9f;
                Spawn("FX_Fire_Torch_02", torch.transform, tip, Quaternion.identity, Vector3.one * 1.8f);
                AddLight(torch.transform, "TorchLight", tip + Vector3.up * 0.7f, warm, 6f, 26f);
                nTorch++;
            }
        sb.AppendLine("torches=" + nTorch);

        // ---- rune pillar circle ----
        string[] pillars = { "PP_Pillar_Stone_Rune_01", "PP_Pillar_Stone_Rune_02", "PP_Pillar_Stone_Rune_03" };
        int nPil = 0;
        for (int i = 0; i < 8; i++)
        {
            float a = (i / 8f) * 360f + 22.5f;
            Vector3 pos = C + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad)) * 31f;
            if (!CastShell(new Vector3(pos.x, fy + 8f, pos.z), Vector3.down, 20f, out var hf)) continue;
            var pl = Spawn(pillars[i % pillars.Length], gP, hf.point - Vector3.up * 0.1f, Quaternion.Euler(0, -a + 90f, 0), Vector3.one * 2.4f);
            if (pl == null) continue;
            Vector3 top = hf.point + Vector3.up * 11f;
            Spawn("FX_Glow_0" + (1 + rng.Next(4)), gE, top, Quaternion.identity, Vector3.one * 3f);
            AddLight(gL, "PillarGlow_" + i, hf.point + Vector3.up * 7f, blue, 4.5f, 26f);
            nPil++;
        }
        sb.AppendLine("pillars=" + nPil);

        // ---- central magic circle ----
        float cfy = fy;
        if (CastShell(new Vector3(C.x, fy + 8f, C.z), Vector3.down, 20f, out var hc)) cfy = hc.point.y;
        Vector3 cc = new Vector3(C.x, cfy, C.z);
        Spawn("FX_Mystic Ring_Large_01", gE, cc + Vector3.up * 0.25f, Quaternion.identity, Vector3.one * 5f);
        Spawn("FX_Mystic Ring_Large_02", gE, cc + Vector3.up * 0.3f, Quaternion.identity, Vector3.one * 8f);
        Spawn("FX_Mystic Ring_Large_03", gE, cc + Vector3.up * 0.35f, Quaternion.identity, Vector3.one * 11f);
        Spawn("FX_Mystic_Shine_01", gE, cc + Vector3.up * 3.5f, Quaternion.identity, Vector3.one * 2.2f);
        Spawn("FX_Magic_Fire_01", gE, cc + Vector3.up * 0.4f, Quaternion.identity, Vector3.one * 2.4f);
        AddLight(gL, "CenterGlow", cc + Vector3.up * 5f, blue, 9f, 55f);
        AddLight(gL, "CenterWarm", cc + Vector3.up * 9f, warm, 3.5f, 70f);

        // ---- light shaft through the oculus ----
        float apexY = fy + 34f;
        var shaft = AddLight(gL, "OculusSpot", new Vector3(hx, apexY + 8f, ZM), pale, 28f, 80f, LightType.Spot);
        shaft.transform.rotation = Quaternion.Euler(90f, 0, 0); shaft.spotAngle = 26f; shaft.innerSpotAngle = 12f;
        for (int i = 0; i < 6; i++)
            Spawn(i % 2 == 0 ? "FX_Fog_02" : "FX_Fog_06", gE, new Vector3(hx + Rr(-2f, 2f), fy + 4f + i * 5f, ZM + Rr(-2f, 2f)), Quaternion.identity, Vector3.one * 0.8f);
        for (int i = 0; i < 3; i++)
            Spawn("FX_Mystic_Shine_01", gE, new Vector3(hx + Rr(-3f, 3f), fy + 12f + i * 7f, ZM + Rr(-3f, 3f)), Quaternion.identity, Vector3.one * 1.3f);
        sb.AppendLine("light shaft + fog + shine placed");

        // ---- floor fog + drifting glow ----
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * 6.283f; float r = Rr(14f, 36f);
            Spawn("FX_Fog_Big_0" + (1 + i % 3), gE, new Vector3(hx + Mathf.Cos(a) * r, fy + 1.2f, ZM + Mathf.Sin(a) * r), Quaternion.identity, Vector3.one * 0.7f);
        }
        for (int i = 0; i < 14; i++)
        {
            float a = Rr(0, 6.283f), r = Rr(5f, 42f);
            Spawn("FX_Glow_0" + (1 + rng.Next(4)), gE, new Vector3(hx + Mathf.Cos(a) * r, fy + Rr(2f, 14f), ZM + Mathf.Sin(a) * r), Quaternion.identity, Vector3.one * 3.5f);
        }
        // candles near the pillars' base for warm accents
        for (int i = 0; i < 8; i++)
        {
            float a = (i / 8f) * 360f + 22.5f;
            Vector3 pos = C + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad)) * 27f;
            if (CastShell(new Vector3(pos.x, fy + 8f, pos.z), Vector3.down, 20f, out var hf))
                Spawn("FX_Candle_Light_01", gE, hf.point + Vector3.up * 0.3f, Quaternion.identity, Vector3.one * 2f);
        }
        // fill lights
        for (int i = 0; i < 4; i++)
        {
            float a = i / 4f * 6.283f + 0.7f;
            AddLight(gL, "Fill_" + i, new Vector3(hx + Mathf.Cos(a) * 24f, fy + 12f, ZM + Mathf.Sin(a) * 24f), new Color(1f, 0.75f, 0.5f), 4f, 55f);
        }
        AddLight(gL, "Fill_Passage", new Vector3(-532f, fy + 8f, ZM), new Color(1f, 0.7f, 0.4f), 4f, 40f);
        Physics.SyncTransforms();
        return sb.ToString();
    }
}
#endif
