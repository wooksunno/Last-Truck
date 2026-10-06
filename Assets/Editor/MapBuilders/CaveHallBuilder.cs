#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CaveHallBuilder
{
    // hall
    public static Vector3 HallC = new Vector3(-684f, -11f, 62f);
    public static float HallR = 56f, HallH = 34f;
    // crater (deeper area) centre offset and shape
    public static Vector2 PitC = new Vector2(-696f, 64f);
    public static float RimR = 44f, BottomR = 8f, Depth = 14f;
    // passages: from hall (x0,y0,z) to branch wall (x1,y1,z)
    public struct Pass { public float z, x0, y0, x1, y1; }
    public static Pass NE = new Pass { z = 100f, x0 = -648f, y0 = -11f, x1 = -592f, y1 = -9f };
    public static Pass SE = new Pass { z = 20f, x0 = -650f, y0 = -11f, x1 = -590f, y1 = -12.5f };
    public static float PassR = 9f;
    public static float ClipX = -599f;

    static string[] Groups = { "Cave", "Cave Props", "Terrain_Stone", "Stones&Rocks", "Vegetation", "Crystals&Ores&Veins", "Coins", "Props", "Mushrooms", "Rails&Mine Carts", "Runes", "Bridges", "Stalactite&Stalagmite&Stalagnate", "Lighting", "FX" };

    static Dictionary<string, string> _paths;
    static GameObject P(string name)
    {
        if (_paths == null)
        {
            _paths = new Dictionary<string, string>();
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/PurePoly/Mining_Pack/Prefabs" }))
            { string p = AssetDatabase.GUIDToAssetPath(g); _paths[System.IO.Path.GetFileNameWithoutExtension(p)] = p; }
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

    public static float HallFloor(float x, float z)
    {
        float r = Vector2.Distance(new Vector2(x, z), PitC);
        float t = Mathf.Clamp01((RimR - r) / (RimR - BottomR));
        float s = t * t * (3f - 2f * t);
        t = Mathf.Lerp(t, s, 0.35f);
        return HallC.y - Depth * t;
    }

    static float PassFloor(Pass p, float x) { return Mathf.Lerp(p.y0, p.y1, Mathf.InverseLerp(p.x0, p.x1, x)); }

    // signed distance of a passage tube void (negative inside)
    static float PassVoid(Pass p, float x, float y, float z, float r)
    {
        float xc = Mathf.Clamp(x, p.x0, p.x1 + 6f);
        float fy = PassFloor(p, xc);
        float cy = fy + 6.5f;
        float dy = (y - cy) * 0.78f;
        float d = Mathf.Sqrt((x - xc) * (x - xc) * 0f + dy * dy + (z - p.z) * (z - p.z)) - r;
        // end caps: extend inside both ends
        if (x < p.x0 - 4f) d = Mathf.Max(d, p.x0 - 4f - x);
        d = Mathf.Max(d, PassFloor(p, Mathf.Clamp(x, p.x0, p.x1)) - y);
        return d;
    }

    public static string ClearOldHall()
    {
        var del = new List<GameObject>();
        foreach (var g in Groups)
        {
            var go = GameObject.Find(g); if (go == null) continue;
            foreach (Transform t in go.transform)
            {
                var p = t.position;
                if (p.x < -626f && p.x > -800f && p.z > -70f && p.z < 110f) del.Add(t.gameObject);
            }
        }
        foreach (var d in del) Object.DestroyImmediate(d);
        var old = GameObject.Find("CaveHallRebuild"); if (old != null) Object.DestroyImmediate(old);
        Physics.SyncTransforms();
        return "removed old hall objects=" + del.Count;
    }

    static bool IsStruct(string n)
    {
        return n.StartsWith("PP_Cave") || n.StartsWith("PP_Mountain") || n.StartsWith("PP_Stone_Cave") || n.StartsWith("PP_Stone_Ground") || n.StartsWith("PP_Cliff") || n.StartsWith("PP_Rock_Plateau") || n.StartsWith("PP_Stone Wall");
    }

    public static string CarveDoorway(Pass p, float xFrom, float xTo)
    {
        var sb = new StringBuilder();
        Physics.SyncTransforms();
        string folder = "Assets/Scenes/MiningMapAssets/CarvedMeshes";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Scenes/MiningMapAssets", "CarvedMeshes");
        float cy = p.y1 + 6.5f; float rz = PassR + 1.8f, ry = (PassR + 1.8f) / 0.78f;
        int carved = 0, tris = 0;
        foreach (var g in Groups)
        {
            var go = GameObject.Find(g); if (go == null) continue;
            foreach (Transform t in go.transform)
            {
                if (!IsStruct(t.name)) continue;
                var mf = t.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var r = t.GetComponent<MeshRenderer>(); if (r == null) continue;
                var b = r.bounds;
                if (b.max.x < xFrom || b.min.x > xTo || b.max.z < p.z - rz || b.min.z > p.z + rz || b.max.y < cy - ry || b.min.y > cy + ry) continue;
                Mesh src = mf.sharedMesh;
                var V = src.vertices; var T = src.triangles; var keep = new List<int>(); int removed = 0;
                for (int i = 0; i < T.Length; i += 3)
                {
                    Vector3 c = t.TransformPoint((V[T[i]] + V[T[i + 1]] + V[T[i + 2]]) / 3f);
                    bool inside = c.x >= xFrom && c.x <= xTo && ((c.z - p.z) * (c.z - p.z)) / (rz * rz) + ((c.y - cy) * (c.y - cy)) / (ry * ry) < 1f && c.y > PassFloor(p, c.x) - 0.5f;
                    if (inside) removed++; else { keep.Add(T[i]); keep.Add(T[i + 1]); keep.Add(T[i + 2]); }
                }
                if (removed == 0) continue;
                var m = Object.Instantiate(src); m.name = src.name + "_carved_" + t.GetInstanceID();
                m.SetTriangles(keep, 0); m.RecalculateBounds();
                AssetDatabase.CreateAsset(m, folder + "/" + m.name + ".asset");
                mf.sharedMesh = m;
                var mc = t.GetComponent<MeshCollider>(); if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = m; }
                carved++; tris += removed;
                sb.AppendLine($"carved {t.name}: removed {removed} tris");
            }
        }
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        sb.AppendLine($"doorway at z={p.z}: meshes carved={carved} tris={tris}");
        return sb.ToString();
    }

    public static string BuildShell()
    {
        var sb = new StringBuilder();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Vector3 hc = HallC; float hr = HallR, hh = HallH;
        System.Func<float, float, float, float> voidD = (x, y, z) =>
        {
            // hall: ellipsoid above the crater floor
            float dx = (x - hc.x) / hr, dy = (y - hc.y) / hh, dz = (z - hc.z) / hr;
            float hall = (Mathf.Sqrt(dx * dx + dy * dy + dz * dz) - 1f) * Mathf.Min(hr, hh * 1.3f);
            hall = Mathf.Max(hall, HallFloor(x, z) - y);
            hall += 1.4f * N3(x, y, z, 0.06f) * 2f + 0.5f * N3(x, y, z, 0.18f) * 2f;
            // east portal (funnel end -> hall)
            float px = Mathf.Clamp(x, -650f, -612f);
            float pf = Mathf.Lerp(-11f, -9f, Mathf.InverseLerp(-650f, -612f, px));
            float pdy = (y - (pf + 8f)) * 0.78f;
            float por = Mathf.Sqrt(pdy * pdy + (z - 68f) * (z - 68f)) - 13f;
            por = Mathf.Max(por, pf - y);
            float v = Mathf.Min(hall, por);
            v = Mathf.Min(v, PassVoid(NE, x, y, z, PassR) + 0.4f * N3(x, y, z, 0.14f) * 2f);
            v = Mathf.Min(v, PassVoid(SE, x, y, z, PassR) + 0.4f * N3(x, y, z, 0.14f) * 2f);
            // oculus
            float ocu = Mathf.Max(Mathf.Sqrt((x - hc.x) * (x - hc.x) + (z - hc.z) * (z - hc.z)) - 6f, (hc.y + 24f) - y);
            return Mathf.Min(v, ocu);
        };
        System.Func<float, float, float, float> massD = (x, y, z) =>
        {
            float dx = (x - hc.x) / (hr + 16f), dy = (y - (hc.y - 12f)) / 52f, dz = (z - hc.z) / (hr + 16f);
            float dome = (Mathf.Sqrt(dx * dx + dy * dy + dz * dz) - 1f) * 52f + 3.2f * N3(x, y, z, 0.03f) * 2f + 1.4f * N3(x, y, z, 0.08f) * 2f;
            // passage hulls (clipped at the existing branch wall)
            float h1 = PassHull(NE, x, y, z), h2 = PassHull(SE, x, y, z);
            float porx = Mathf.Clamp(x, -650f, -612f);
            float porHull = Mathf.Sqrt(((y - (-3f)) * 0.78f) * ((y - (-3f)) * 0.78f) + (z - 68f) * (z - 68f)) - 20f;
            porHull = Mathf.Max(porHull, Mathf.Max(x - (-612f), -660f - x));
            return Mathf.Min(Mathf.Min(dome, porHull), Mathf.Min(h1, h2));
        };
        System.Func<float, float, float, float> rockD = (x, y, z) => Mathf.Max(massD(x, y, z), -voidD(x, y, z));

        float cs = 2.0f;
        Vector3 org = new Vector3(-768f, -52f, -22f);
        int nx = Mathf.CeilToInt(176f / cs), ny = Mathf.CeilToInt(96f / cs), nz = Mathf.CeilToInt(160f / cs);
        int sx = nx + 1, sy = ny + 1, sz = nz + 1;
        float[] F = new float[sx * sy * sz];
        for (int k = 0; k < sz; k++) for (int j = 0; j < sy; j++) for (int i = 0; i < sx; i++) F[(k * sy + j) * sx + i] = rockD(org.x + i * cs, org.y + j * cs, org.z + k * cs);
        var cellVert = new int[nx * ny * nz]; for (int i = 0; i < cellVert.Length; i++) cellVert[i] = -1;
        var verts = new List<Vector3>();
        int[,] edgeA = { { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 }, { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
        float[] cv = new float[8];
        for (int k = 0; k < nz; k++) for (int j = 0; j < ny; j++) for (int i = 0; i < nx; i++)
        {
            int mask = 0;
            for (int c = 0; c < 8; c++) { cv[c] = F[((k + ((c >> 2) & 1)) * sy + (j + ((c >> 1) & 1))) * sx + (i + (c & 1))]; if (cv[c] < 0f) mask |= 1 << c; }
            if (mask == 0 || mask == 255) continue;
            Vector3 sum = Vector3.zero; int cnt = 0;
            for (int e = 0; e < 12; e++)
            {
                int a = edgeA[e, 0], b = edgeA[e, 1]; if ((cv[a] < 0f) == (cv[b] < 0f)) continue;
                float t = cv[a] / (cv[a] - cv[b]);
                sum += Vector3.Lerp(new Vector3(i + (a & 1), j + ((a >> 1) & 1), k + ((a >> 2) & 1)), new Vector3(i + (b & 1), j + ((b >> 1) & 1), k + ((b >> 2) & 1)), t); cnt++;
            }
            cellVert[(k * ny + j) * nx + i] = verts.Count; verts.Add(org + (sum / cnt) * cs);
        }
        var tri = new List<int>();
        for (int k = 1; k < nz; k++) for (int j = 1; j < ny; j++) for (int i = 1; i < nx; i++) for (int axis = 0; axis < 3; axis++)
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
        Vector2 grey = new Vector2(0.68f, 0.96f), dirt = new Vector2(0.578f, 0.422f);
        const float cell = 20f;
        var bV = new Dictionary<long, List<Vector3>>(); var bN = new Dictionary<long, List<Vector3>>(); var bU = new Dictionary<long, List<Vector2>>();
        float e0 = 0.8f;
        System.Func<Vector3, Vector3> grad = c => new Vector3(rockD(c.x + e0, c.y, c.z) - rockD(c.x - e0, c.y, c.z), rockD(c.x, c.y + e0, c.z) - rockD(c.x, c.y - e0, c.z), rockD(c.x, c.y, c.z + e0) - rockD(c.x, c.y, c.z - e0));
        for (int t = 0; t < tri.Count / 3; t++)
        {
            Vector3 a = verts[tri[t * 3]], b = verts[tri[t * 3 + 1]], c = verts[tri[t * 3 + 2]];
            Vector3 cr = Vector3.Cross(b - a, c - a); if (cr.sqrMagnitude < 1e-6f) continue;
            Vector3 cen = (a + b + c) / 3f; Vector3 gc = grad(cen);
            if (Vector3.Dot(cr, gc) < 0f) { Vector3 tmp = b; b = c; c = tmp; }
            Vector3 n0 = grad(a).normalized, n1 = grad(b).normalized, n2 = grad(c).normalized;
            bool inside = voidD(cen.x, cen.y, cen.z) < 2.2f;
            Vector2 uv = inside ? (gc.normalized.y > 0.6f ? dirt : grey) : (gc.normalized.y > 0.82f ? new Vector2(0.852f, 0.813f) : grey);
            long key = ((long)Mathf.FloorToInt((cen.x + 2000f) / cell) * 1000L + Mathf.FloorToInt((cen.y + 200f) / cell)) * 1000L + Mathf.FloorToInt((cen.z + 2000f) / cell);
            if (!bV.ContainsKey(key)) { bV[key] = new List<Vector3>(); bN[key] = new List<Vector3>(); bU[key] = new List<Vector2>(); }
            bV[key].Add(a); bV[key].Add(b); bV[key].Add(c); bN[key].Add(n0); bN[key].Add(n1); bN[key].Add(n2); bU[key].Add(uv); bU[key].Add(uv); bU[key].Add(uv);
        }
        var old = GameObject.Find("CaveHallRebuild"); if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("CaveHallRebuild");
        var shell = new GameObject("Shell"); shell.transform.SetParent(root.transform);
        var mat = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PurePoly/Mining_Pack/Prefabs/Cave/PP_Cave_Tube_04.prefab").GetComponent<MeshRenderer>().sharedMaterial;
        string path = "Assets/Scenes/MiningMapAssets/CaveHallShell.asset";
        if (System.IO.File.Exists(path)) AssetDatabase.DeleteAsset(path);
        int n = 0; bool first = true;
        foreach (var kv in bV)
        {
            var vv = kv.Value; var idx = new int[vv.Count]; for (int i = 0; i < idx.Length; i++) idx[i] = i;
            var m = new Mesh { name = "HallShell_" + n }; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(vv); m.SetNormals(bN[kv.Key]); m.SetUVs(0, bU[kv.Key]); m.SetTriangles(idx, 0); m.RecalculateBounds();
            if (first) { AssetDatabase.CreateAsset(m, path); first = false; } else AssetDatabase.AddObjectToAsset(m, path);
            var go = new GameObject("Shell_" + n); go.transform.SetParent(shell.transform);
            go.AddComponent<MeshFilter>().sharedMesh = m; go.AddComponent<MeshRenderer>().sharedMaterial = mat; go.AddComponent<MeshCollider>().sharedMesh = m; n++;
        }
        AssetDatabase.SaveAssets(); Physics.SyncTransforms();
        sb.AppendLine($"shell tris={tri.Count / 3} chunks={n} t={sw.ElapsedMilliseconds}ms");
        return sb.ToString();
    }

    static float PassHull(Pass p, float x, float y, float z)
    {
        float xc = Mathf.Clamp(x, p.x0, ClipX);
        float fy = PassFloor(p, xc); float cy = fy + 5f;
        float dy = (y - cy) * 0.8f;
        float d = Mathf.Sqrt(dy * dy + (z - p.z) * (z - p.z)) - 17f;
        d = Mathf.Max(d, x - ClipX);
        d = Mathf.Max(d, p.x0 - 2f - x);
        return d;
    }

    static GameObject Spawn(string prefab, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var pf = P(prefab); if (pf == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent); go.transform.position = pos; go.transform.rotation = rot; go.transform.localScale = scale; return go;
    }
    static Light AddLight(Transform parent, string name, Vector3 pos, Color c, float i, float r)
    {
        var g = new GameObject(name); g.transform.SetParent(parent); g.transform.position = pos;
        var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.intensity = i; l.range = r; l.shadows = LightShadows.None; return l;
    }
    static bool CastShell(Vector3 o, Vector3 d, float dist, out RaycastHit best)
    {
        best = default; float bd = float.MaxValue; bool f = false;
        var shell = GameObject.Find("CaveHallRebuild").transform.Find("Shell");
        foreach (var h in Physics.RaycastAll(o, d, dist)) { if (h.collider.isTrigger || !h.collider.transform.IsChildOf(shell)) continue; if (h.distance < bd) { bd = h.distance; best = h; f = true; } }
        return f;
    }

    public static string Dress(int seed)
    {
        var sb = new StringBuilder(); var rng = new System.Random(seed);
        System.Func<float, float, float> Rr = (a, b) => a + (b - a) * (float)rng.NextDouble();
        Physics.SyncTransforms();
        var root = GameObject.Find("CaveHallRebuild").transform;
        foreach (var nm in new[] { "Torches", "Effects", "Lights" }) { var o = root.Find(nm); if (o != null) Object.DestroyImmediate(o.gameObject); }
        var gT = new GameObject("Torches").transform; gT.SetParent(root); var gE = new GameObject("Effects").transform; gE.SetParent(root); var gL = new GameObject("Lights").transform; gL.SetParent(root);
        Color warm = new Color(1f, 0.62f, 0.25f), blue = new Color(0.35f, 0.6f, 1f);
        string[] torchNames = { "PP_Torch_Standing_01", "PP_Torch_Standing_03", "PP_Torch_Standing_04", "PP_Torch_Standing_06" };
        string[] fx = { "FX_Fire_Torch_01", "FX_Fire_Torch_02", "FX_Fire_Torch_03" };
        int nt = 0;
        System.Action<Vector3, float, float> torchAt = (pos, rg, it) =>
        {
            var t = Spawn(torchNames[rng.Next(torchNames.Length)], gT, pos, Quaternion.Euler(0, Rr(0, 360), 0), Vector3.one * 2.6f); if (t == null) return;
            Vector3 tip = pos + Vector3.up * 5.3f; Spawn(fx[rng.Next(fx.Length)], t.transform, tip, Quaternion.identity, Vector3.one * 2f);
            AddLight(t.transform, "TorchLight", tip + Vector3.up * 0.8f, warm, it, rg); nt++;
        };
        Vector3 C = new Vector3(HallC.x, HallC.y + 6f, HallC.z);
        // torches on the flat rim ring (r = 50 from hall centre) and along the crater
        for (int i = 0; i < 24; i++)
        {
            float a = i / 24f * 360f + 5f; Vector3 d = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad));
            if (!CastShell(C, d, 90f, out var hw)) continue;
            Vector3 tp = hw.point - d * 2.2f;
            if (!CastShell(new Vector3(tp.x, 30f, tp.z), Vector3.down, 90f, out var hf)) continue;
            torchAt(hf.point, 34f, 7f);
        }
        foreach (var pa in new[] { NE, SE })
            for (float x = pa.x0 + 6f; x < pa.x1 - 6f; x += 16f)
                foreach (float sg in new[] { -1f, 1f })
                {
                    if (!CastShell(new Vector3(x, PassFloor(pa, x) + 5f, pa.z), new Vector3(0, 0, sg), 14f, out var hw)) continue;
                    Vector3 tp = hw.point - new Vector3(0, 0, sg) * 1.8f;
                    if (!CastShell(new Vector3(tp.x, tp.y + 8f, tp.z), Vector3.down, 20f, out var hf)) continue;
                    torchAt(hf.point, 28f, 6.5f);
                }
        sb.AppendLine("torches=" + nt);
        // crater: blue glow at the bottom, rim candles
        float bf = HallFloor(PitC.x, PitC.y);
        Vector3 cb = new Vector3(PitC.x, bf, PitC.y);
        Spawn("FX_Mystic Ring_Large_01", gE, cb + Vector3.up * 0.25f, Quaternion.identity, Vector3.one * 7f);
        Spawn("FX_Mystic Ring_Large_02", gE, cb + Vector3.up * 0.3f, Quaternion.identity, Vector3.one * 11f);
        Spawn("FX_Mystic_Shine_01", gE, cb + Vector3.up * 5f, Quaternion.identity, Vector3.one * 3f);
        Spawn("FX_Magic_Fire_01", gE, cb + Vector3.up * 0.4f, Quaternion.identity, Vector3.one * 3f);
        AddLight(gL, "PitGlow", cb + Vector3.up * 6f, blue, 14f, 70f);
        for (int i = 0; i < 6; i++) Spawn(i % 2 == 0 ? "FX_Fog_02" : "FX_Fog_06", gE, new Vector3(HallC.x + Rr(-3, 3), HallC.y + 6f + i * 5f, HallC.z + Rr(-3, 3)), Quaternion.identity, Vector3.one * 0.9f);
        for (int i = 0; i < 16; i++) { float a = Rr(0, 6.283f), r = Rr(5f, 50f); Spawn("FX_Glow_0" + (1 + rng.Next(4)), gE, new Vector3(HallC.x + Mathf.Cos(a) * r, HallC.y + Rr(1f, 18f), HallC.z + Mathf.Sin(a) * r), Quaternion.identity, Vector3.one * 3.5f); }
        // oculus shaft
        var shaft = AddLight(gL, "OculusSpot", new Vector3(HallC.x, HallC.y + 40f, HallC.z), new Color(0.82f, 0.9f, 1f), 28f, 100f);
        shaft.type = LightType.Spot; shaft.transform.rotation = Quaternion.Euler(90, 0, 0); shaft.spotAngle = 24f; shaft.innerSpotAngle = 10f;
        for (int i = 0; i < 6; i++) { float a = i / 6f * 6.283f + 0.5f; AddLight(gL, "Fill_" + i, new Vector3(HallC.x + Mathf.Cos(a) * 38f, HallC.y + 14f, HallC.z + Mathf.Sin(a) * 38f), new Color(1f, 0.75f, 0.5f), 9f, 80f); }
        AddLight(gL, "Fill_NE", new Vector3(-620f, -2f, 100f), new Color(1f, 0.7f, 0.4f), 6f, 50f);
        AddLight(gL, "Fill_SE", new Vector3(-620f, -3f, 20f), new Color(1f, 0.7f, 0.4f), 6f, 50f);
        Physics.SyncTransforms();
        return sb.ToString();
    }
}
#endif
