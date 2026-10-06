#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// "Quarry-Chasm" east of the desert, built only from existing prefabs.
//  - round, off-centre spiral crater (Genshin chasm map) made of stepped benches (quarry photos)
//  - PP_Ground tiles on an 11.5 m grid for every bench floor, PP_Cave_Wall_Even for every bench cliff
//  - PP_Bridge_05 ramps follow a spiral, lift bays share one side, wooden scaffolds / ladders on the cliffs
//  - pink trees (palette-swapped material), layered tilted slab outcrops on the rim, fog FX in the depth
public static class QuarryBuilder
{
    const string AssetDir = "Assets/Scenes/MiningMapAssets/Quarry";
    const string DesertMatPath = "Assets/Scenes/MiningMapAssets/Desert/PP_Desert_Material.mat";

    public static Vector2 C = new Vector2(1f, 742f);
    public const float T = 11.5f;           // bench tile size
    public const float D = 9f;              // height of one bench
    public const int Levels = 6;     // 0 = plateau ... 5 = main quarry floor, 6 = deeper central pit
    public const int Floor = 5;
    const int N0 = -17, N1 = 20, K0 = -15, K1 = 14;   // fine grid (2 x 2 per desert tile)
    const float RX = 148f, RZ = 134f;
    public static float LV(int i) { return 1.4f - D * i; }

    static int[,] lv = new int[N1 - N0 + 1, K1 - K0 + 1];
    static int[,] comp, road, gateOf, taken;

    class Gate
    {
        public int nA, kA, nB, kB, cliff, upper; public bool lift, mine; public float width;
        public Vector2 b, down;
    }
    static List<Gate> gates = new List<Gate>();

    static Dictionary<string, string> _paths;
    static Material _desertMat, _stoneMat, _pinkMat, _mineStone, _mineFloor;
    static Material[] _dirt = new Material[4];

    static int L(int n, int k) { if (n < N0 || n > N1 || k < K0 || k > K1) return 0; return lv[n - N0, k - K0]; }
    static Vector2 Center(int n, int k) { return new Vector2(C.x + (n + 0.5f) * T, C.y + (k + 0.5f) * T); }
    static float S01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    static readonly int[] DX = { 1, -1, 0, 0 }, DK = { 0, 0, 1, -1 };

    static GameObject P(string name)
    {
        if (_paths == null)
        {
            _paths = new Dictionary<string, string>();
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/PurePoly/Mining_Pack" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                _paths[System.IO.Path.GetFileNameWithoutExtension(p)] = p;
            }
        }
        return _paths.ContainsKey(name) ? AssetDatabase.LoadAssetAtPath<GameObject>(_paths[name]) : null;
    }

    static void ApplyMat(GameObject go, Material m)
    {
        if (m == null) return;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
        {
            var a = r.sharedMaterials;
            for (int i = 0; i < a.Length; i++) a[i] = m;
            r.sharedMaterials = a;
        }
    }

    static Material TintedCopy(string name, Material src, Color tint, float smooth)
    {
        string path = AssetDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(src) { name = name }; AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", tint); m.SetFloat("_Smoothness", smooth); EditorUtility.SetDirty(m);
        return m;
    }

    // palette copy with the greens turned pink (used for the trees)
    static Material PinkMaterial(Material srcMat)
    {
        string orig = "Assets/PurePoly/Mining_Pack/Textures/PP_Color_Palette.png", np = AssetDir + "/PP_Color_Palette_Pink.png";
        var tex = new Texture2D(2, 2); tex.LoadImage(System.IO.File.ReadAllBytes(orig));
        var px = tex.GetPixels();
        for (int i = 0; i < px.Length; i++)
        {
            float h, s, v; Color.RGBToHSV(px[i], out h, out s, out v);
            if (h > 0.16f && h < 0.47f && s > 0.22f)
            {
                h = 0.94f + (h - 0.3f) * 0.08f; if (h > 1f) h -= 1f;
                px[i] = Color.HSVToRGB(h, Mathf.Min(1f, s * 0.62f), Mathf.Min(1f, v * 1.45f + 0.2f));
            }
        }
        tex.SetPixels(px); tex.Apply();
        System.IO.File.WriteAllBytes(np, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(np, ImportAssetOptions.ForceUpdate);
        var so = AssetImporter.GetAtPath(orig) as TextureImporter; var dst = AssetImporter.GetAtPath(np) as TextureImporter;
        if (so != null && dst != null) { dst.filterMode = so.filterMode; dst.mipmapEnabled = so.mipmapEnabled; dst.wrapMode = so.wrapMode; dst.textureCompression = so.textureCompression; dst.sRGBTexture = so.sRGBTexture; dst.SaveAndReimport(); }
        string mp = AssetDir + "/PP_Pink_Material.mat"; var pm = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (pm == null) { pm = new Material(srcMat) { name = "PP_Pink_Material" }; AssetDatabase.CreateAsset(pm, mp); }
        pm.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(np)); EditorUtility.SetDirty(pm);
        return pm;
    }

    static Transform Grp(Transform root, string name) { var g = new GameObject(name); g.transform.SetParent(root); return g.transform; }

    // ---------- level map ----------
    // A stair block: steps of straight rectangular benches that get shorter and narrower towards the centre (tile-centre coordinates).
    static readonly float[] StairFrom = { -143.75f, -97.75f, -63.25f, -28.75f };
    static readonly float[] StairTo = { -109.25f, -74.75f, -40.25f, -17.25f };
    static readonly float[] StairHalf = { 51.75f, 40.25f, 28.75f, 17.25f };

    static int StairLevel(Vector2 rel)
    {
        for (int s = 0; s < 2; s++)
        {
            float along = s == 0 ? rel.x : rel.y - 11.5f, lat = s == 0 ? rel.y : rel.x - 69f;   // stair 0 comes in from the west, stair 1 from the south (shifted east so the two never overlap)
            for (int i = 0; i < 4; i++)
                if (along >= StairFrom[i] - 0.1f && along <= StairTo[i] + 0.1f && Mathf.Abs(lat) <= StairHalf[i] + 0.1f) return i + 1;
        }
        return 0;
    }

    static void GenMap(System.Random rng)
    {
        float s1 = rng.Next(1000), s2 = rng.Next(1000);
        float[] thr = { 0.98f, 0.76f, 0.56f, 0.38f, 0.22f };      // stair benches get narrower towards the centre (0.22, 0.20, 0.18, 0.16 of the radius)
        for (int k = K0; k <= K1; k++)
            for (int n = N0; n <= N1; n++)
            {
                Vector2 c = Center(n, k);
                float dx = (c.x - C.x) / RX, dz = (c.y - C.y) / RZ;
                float u = Mathf.Sqrt(dx * dx + dz * dz);
                float ang = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
                float j = Mathf.PerlinNoise(n * 0.18f + s1, k * 0.18f + s2) - 0.5f;
                // two stair wedges (west = entrance, south = second way down); each is wide at the rim and narrows towards the centre
                int l = 0; float f0 = u + j * 0.05f;
                l = f0 <= 0.86f ? Floor : (f0 <= 0.99f ? 1 : 0);                          // sheer drop all round, with a rim ledge (road) in front of the plateau
                int sl = StairLevel(c - C); if (sl > 0) l = sl;                           // the two straight stair blocks cut into the rim
                // deepest part: a trench along the foot of the east cliff (the mine entrance sits at its end)
                if (Mathf.Abs(ang) < 27f + j * 22f && u > 0.50f && f0 <= 0.86f) l = Levels;
                lv[n - N0, k - K0] = l;
            }
        for (int pass = 0; pass < 2; pass++)
            for (int k = K0; k <= K1; k++)
                for (int n = N0; n <= N1; n++)
                {
                    int me = L(n, k); int same = 0; var nb = new List<int>();
                    for (int d = 0; d < 4; d++) { int o = L(n + DX[d], k + DK[d]); nb.Add(o); if (o == me) same++; }
                    if (same == 0) { nb.Sort(); lv[n - N0, k - K0] = nb[1]; }
                }
        // every level region must touch the level above it (otherwise there would be nowhere to put a ramp)
        for (int it = 0; it < 10; it++)
        {
            Components(); bool ch = false;
            int nComp = 0; foreach (int cc in comp) nComp = Mathf.Max(nComp, cc + 1);
            var okc = new bool[nComp]; var lvlOf = new int[nComp];
            for (int k = K0; k <= K1; k++)
                for (int n = N0; n <= N1; n++)
                {
                    int me = L(n, k), id = CompOf(n, k); lvlOf[id] = me;
                    if (me == 0) { okc[id] = true; continue; }
                    for (int d = 0; d < 4; d++) { int a = n + DX[d], b = k + DK[d]; if (a >= N0 && a <= N1 && b >= K0 && b <= K1 && L(a, b) == me - 1) okc[id] = true; }
                }
            for (int k = K0; k <= K1; k++)
                for (int n = N0; n <= N1; n++)
                { int id = CompOf(n, k); if (!okc[id]) { lv[n - N0, k - K0] = lvlOf[id] - 1; ch = true; } }
            if (!ch) break;
        }
    }

    static void Components()
    {
        int W = N1 - N0 + 1, H = K1 - K0 + 1; comp = new int[W, H];
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++) comp[i, j] = -1;
        int id = 0;
        for (int k = K0; k <= K1; k++)
            for (int n = N0; n <= N1; n++)
            {
                if (comp[n - N0, k - K0] >= 0) continue;
                var q = new Queue<Vector2Int>(); q.Enqueue(new Vector2Int(n, k)); comp[n - N0, k - K0] = id; int lvl = L(n, k);
                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    for (int d = 0; d < 4; d++)
                    {
                        int a = c.x + DX[d], b = c.y + DK[d];
                        if (a < N0 || a > N1 || b < K0 || b > K1) continue;
                        if (comp[a - N0, b - K0] >= 0 || L(a, b) != lvl) continue;
                        comp[a - N0, b - K0] = id; q.Enqueue(new Vector2Int(a, b));
                    }
                }
                id++;
            }
    }
    static int CompOf(int n, int k) { return comp[n - N0, k - K0]; }

    // ---------- gates ----------
    static Gate MakeGate(int nA, int kA, int nB, int kB, bool lift)
    {
        var g = new Gate { nA = nA, kA = kA, nB = nB, kB = kB, cliff = L(nB, kB), upper = L(nA, kA), lift = lift, width = lift ? 10.5f : 6f };
        Vector2 a = Center(nA, kA), b = Center(nB, kB);
        g.b = (a + b) * 0.5f; g.down = (b - a).normalized;
        int dn = nB - nA, dk = kB - kA;
        foreach (int s in new[] { 0, 1 })
        {
            int n2 = nB + dn * s, k2 = kB + dk * s;
            for (int l = -1; l <= 1; l++) { int n3 = n2 + (dk != 0 ? l : 0), k3 = k2 + (dn != 0 ? l : 0); if (n3 >= N0 && n3 <= N1 && k3 >= K0 && k3 <= K1) taken[n3 - N0, k3 - K0] = 1; }
        }
        gateOf[nB - N0, kB - K0] = gates.Count; gates.Add(g);
        return g;
    }

    static float EdgeAngle(Vector2 p) { return Mathf.Atan2((p.y - C.y) / RZ, (p.x - C.x) / RX); }
    static float AngDiff(float a, float b) { return Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg)) * Mathf.Deg2Rad; }

    // all valid (A,B) edges for transition into 'lvl' (B has level lvl, A lvl-1, 2 tiles of free room behind B)
    static List<Vector4> Candidates(int lvl, bool anyComp, bool[] reached, int onlyComp)
    {
        var cand = new List<Vector4>();
        for (int k = K0; k <= K1; k++)
            for (int n = N0; n <= N1; n++)
            {
                if (L(n, k) != lvl || taken[n - N0, k - K0] == 1) continue;
                if (onlyComp >= 0 && CompOf(n, k) != onlyComp) continue;
                for (int d = 0; d < 4; d++)
                {
                    int a = n + DX[d], b = k + DK[d];
                    if (a < N0 || a > N1 || b < K0 || b > K1 || L(a, b) != lvl - 1) continue;
                    if (!reached[CompOf(a, b)]) continue;
                    int n2 = n - DX[d], k2 = k - DK[d];                  // tile behind B (away from A)
                    if (L(n2, k2) != lvl || taken[n2 - N0, k2 - K0] == 1) continue;
                    cand.Add(new Vector4(a, b, n, k));
                }
            }
        return cand;
    }

    static Vector4 PickByAngle(List<Vector4> cand, float target, float minSep, System.Random rng, out bool ok)
    {
        ok = false; Vector4 best = Vector4.zero; float bs = 1e9f;
        foreach (var cd in cand)
        {
            Vector2 e = (Center((int)cd.x, (int)cd.y) + Center((int)cd.z, (int)cd.w)) * 0.5f;
            float md = 1e9f; foreach (var g in gates) md = Mathf.Min(md, Vector2.Distance(e, g.b));
            if (md < minSep) continue;
            float s = AngDiff(EdgeAngle(e), target) + (float)rng.NextDouble() * 0.05f;
            if (s < bs) { bs = s; best = cd; ok = true; }
        }
        return best;
    }

    static Vector4 PickByPoint(List<Vector4> cand, Vector2 target, float minSep, System.Random rng, out bool ok)
    {
        ok = false; Vector4 best = Vector4.zero; float bs = 1e9f;
        foreach (var cd in cand)
        {
            Vector2 e = (Center((int)cd.x, (int)cd.y) + Center((int)cd.z, (int)cd.w)) * 0.5f;
            float md = 1e9f; foreach (var g in gates) md = Mathf.Min(md, Vector2.Distance(e, g.b));
            if (md < minSep) continue;
            float s = Vector2.Distance(e, target) + (float)rng.NextDouble() * 0.5f;
            if (s < bs) { bs = s; best = cd; ok = true; }
        }
        return best;
    }

    static void PlanGates(System.Random rng, StringBuilder sb)
    {
        gates.Clear(); int W = N1 - N0 + 1, H = K1 - K0 + 1; gateOf = new int[W, H]; taken = new int[W, H];
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++) gateOf[i, j] = -1;
        int nComp = 0; foreach (int c in comp) nComp = Mathf.Max(nComp, c + 1);
        var reached = new bool[nComp]; var compLevel = new int[nComp];
        for (int k = K0; k <= K1; k++) for (int n = N0; n <= N1; n++) { int c = CompOf(n, k); compLevel[c] = L(n, k); if (L(n, k) == 0) reached[c] = true; }
        // ramps down the two stair blocks: one per step, zig-zagging from side to side like the switchbacks in the quarry photo
        float[] stairX = { -149.5f, -103.5f, -69f, -34.5f, -11.5f };      // where each ramp starts along the stair (step boundary)
        float[] stairOff = { 0f, 28f, -22f, 16f, -9f };                   // side offset of each ramp
        for (int st = 0; st < 2; st++)
            for (int lvl = 1; lvl <= Floor; lvl++)
            {
                Vector2 sdir = st == 0 ? new Vector2(1f, 0f) : new Vector2(0f, 1f), slat = new Vector2(-sdir.y, sdir.x);
                Vector2 tgt = C + sdir * (stairX[lvl - 1] + (st == 0 ? 0f : 11.5f)) + slat * stairOff[lvl - 1] + (st == 0 ? Vector2.zero : new Vector2(69f, 0f));
                bool ok; var cand = Candidates(lvl, true, reached, -1);
                var pick = PickByPoint(cand, tgt, 18f, rng, out ok);
                if (!ok) { sb.AppendLine("WARNING: no stair ramp for step " + lvl + " of stair " + st); continue; }
                MakeGate((int)pick.x, (int)pick.y, (int)pick.z, (int)pick.w, false);
                reached[CompOf((int)pick.z, (int)pick.w)] = true;
            }
        // anything still unreachable (e.g. the deepest trench)
        for (int lvl = 1; lvl <= Levels; lvl++)
            for (int c = 0; c < nComp; c++)
            {
                if (compLevel[c] != lvl || reached[c]) continue;
                bool ok; var cand = Candidates(lvl, false, reached, c);
                float target = Mathf.PI + ((lvl % 2 == 0) ? 0.5f : -0.5f);          // switchback down the west side
                var pick = PickByAngle(cand, target, 30f, rng, out ok);
                if (!ok) pick = PickByAngle(cand, target, 0f, rng, out ok);
                if (!ok) { sb.AppendLine("WARNING: no ramp possible for component " + c + " (level " + lvl + ")"); continue; }
                MakeGate((int)pick.x, (int)pick.y, (int)pick.z, (int)pick.w, false);
                reached[c] = true;
            }
        // a second route into the deepest trench
        {
            bool ok; var cand = Candidates(Levels, true, reached, -1);
            var pick = PickByAngle(cand, 0.6f, 28f, rng, out ok);
            if (ok) MakeGate((int)pick.x, (int)pick.y, (int)pick.z, (int)pick.w, false);
        }
        // freight lifts: on the sheer cliffs (rim ledge -> main floor), one north and one north-east; pass 0 is the mine gate
        for (int pass = 0; pass < 3; pass++)
        {
            float target = pass == 0 ? 0f : pass == 1 ? Mathf.PI * 0.5f : 0.95f;
            int needLevel = pass == 0 ? Levels : Floor;                      // pass 0 = mine gate at the deepest trench
            var cand = new List<Vector4>();
            for (int k = K0; k <= K1; k++)
                for (int n = N0; n <= N1; n++)
                {
                    if (L(n, k) != needLevel || taken[n - N0, k - K0] == 1) continue;
                    for (int d = 0; d < 4; d++)
                    {
                        int a = n + DX[d], b = k + DK[d];
                        if (a < N0 || a > N1 || b < K0 || b > K1 || L(a, b) != 1) continue;      // upper side = the rim ledge
                        int n2 = n - DX[d], k2 = k - DK[d];
                        if (L(n2, k2) != needLevel || taken[n2 - N0, k2 - K0] == 1) continue;
                        cand.Add(new Vector4(a, b, n, k));
                    }
                }
            bool ok; var pick = PickByAngle(cand, target, 40f, rng, out ok);
            if (!ok) { if (pass == 0) sb.AppendLine("WARNING: no place for the mine gate"); continue; }
            var gg = MakeGate((int)pick.x, (int)pick.y, (int)pick.z, (int)pick.w, pass != 0);
            if (pass == 0) { gg.mine = true; gg.width = 14f; }
        }
    }

    static List<Vector2Int> TilePath(Vector2Int s, Vector2Int t)
    {
        var prev = new Dictionary<Vector2Int, Vector2Int>(); var q = new Queue<Vector2Int>(); q.Enqueue(s); prev[s] = s; int lvl = L(s.x, s.y);
        while (q.Count > 0)
        {
            var c = q.Dequeue(); if (c == t) break;
            for (int d = 0; d < 4; d++)
            {
                var nx = new Vector2Int(c.x + DX[d], c.y + DK[d]);
                if (prev.ContainsKey(nx) || nx.x < N0 || nx.x > N1 || nx.y < K0 || nx.y > K1 || L(nx.x, nx.y) != lvl) continue;
                prev[nx] = c; q.Enqueue(nx);
            }
        }
        var path = new List<Vector2Int>(); if (!prev.ContainsKey(t)) return path;
        var cur = t; while (cur != s) { path.Add(cur); cur = prev[cur]; }
        path.Add(s); return path;
    }

    static void PlanRoads()
    {
        road = new int[N1 - N0 + 1, K1 - K0 + 1];
        // the outermost row of the rim ledge is a road that runs all the way round
        for (int k = K0; k <= K1; k++)
            for (int n = N0; n <= N1; n++)
            {
                if (L(n, k) != 1) continue;
                for (int d = 0; d < 4; d++) if (L(n + DX[d], k + DK[d]) == 0) { road[n - N0, k - K0] = 1; break; }
            }
        foreach (var gr in gates)
        {
            if (gr.lift || gr.mine) continue;
            var landing = new Vector2Int(gr.nB, gr.kB);
            foreach (var g2 in gates)
            {
                if (L(g2.nA, g2.kA) != gr.cliff || CompOf(g2.nA, g2.kA) != CompOf(landing.x, landing.y)) continue;
                foreach (var p in TilePath(landing, new Vector2Int(g2.nA, g2.kA))) road[p.x - N0, p.y - K0] = 1;
            }
        }
    }

    // ---------- plateau tile (mesh copy bent by the desert dune function, desert colour ramp) ----------
    static void PlateauTile(Transform parent, GameObject pf, Vector3 pos, float yaw, float scale, int id)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        inst.transform.position = pos; inst.transform.rotation = Quaternion.Euler(0, yaw, 0); inst.transform.localScale = new Vector3(scale, 1f, scale);
        ApplyMat(inst, _desertMat);
        var mf = inst.GetComponent<MeshFilter>();
        Mesh m = Object.Instantiate(pf.GetComponent<MeshFilter>().sharedMesh);
        Vector3[] v = m.vertices; Vector3[] nr = m.normals; Vector2[] uv = m.uv;
        Quaternion rot = inst.transform.rotation, inv = Quaternion.Inverse(rot);
        System.Func<float, float, float> hf = (x, z) => ForestDesertBuilder.Dune(x, z) * (1f - S01((x + 195f) / 35f));
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 wp = inst.transform.TransformPoint(v[i]);
            float h = hf(wp.x, wp.z);
            v[i].y += h;
            float gx = hf(wp.x + 0.6f, wp.z) - hf(wp.x - 0.6f, wp.z), gz = hf(wp.x, wp.z + 0.6f) - hf(wp.x, wp.z - 0.6f);
            Vector3 nw = rot * nr[i];
            nw = new Vector3(nw.x - gx * 0.8f * nw.y, nw.y, nw.z - gz * 0.8f * nw.y).normalized;
            nr[i] = inv * nw;
            float n1 = Mathf.PerlinNoise((wp.x + 900f) * 0.02f, (wp.z + 300f) * 0.02f);
            float n2 = Mathf.PerlinNoise((wp.x + 100f) * 0.07f, (wp.z + 700f) * 0.07f);
            float tcol = Mathf.Clamp01(h / 8f) * 0.55f + (n1 - 0.5f) * 0.9f + (n2 - 0.5f) * 0.25f;
            float u = tcol > 0.28f ? 0.518f : tcol > -0.12f ? 0.643f : tcol > -0.34f ? 0.768f : 0.893f;
            if (Mathf.Abs(uv[i].x - 0.89f) < 0.02f && Mathf.Abs(uv[i].y - 0.49f) < 0.02f) uv[i] = new Vector2(u, 0.580f);
        }
        m.vertices = v; m.normals = nr; m.uv = uv; m.RecalculateBounds();
        m.name = "PlateauTile_" + id.ToString("D3");
        AssetDatabase.CreateAsset(m, AssetDir + "/" + m.name + ".asset");
        mf.sharedMesh = m;
        var mc = inst.GetComponent<MeshCollider>(); if (mc != null) mc.sharedMesh = m;
    }

    // ---------- cliffs ----------
    static GameObject WallPiece(Transform parent, string name, Vector2 target, float yawDeg, float len, float baseY, float sy)
    {
        var pf = P(name); if (pf == null) return null;
        float sx = len / 14.4f;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        Quaternion R = Quaternion.Euler(0, yawDeg, 0);
        go.transform.rotation = R; go.transform.localScale = new Vector3(sx, sy, 1.15f);
        go.transform.position = new Vector3(target.x, baseY, target.y) + R * new Vector3(6.7f * sx, 0f, 0f);
        ApplyMat(go, _stoneMat);
        return go;
    }

    static string WallName(System.Random rng)
    {
        if (rng.NextDouble() < 0.12) return "PP_Cave_Wall_Even_Veins_0" + (1 + rng.Next(5)) + (rng.Next(2) == 0 ? "_Iron" : "_Copper");
        return "PP_Cave_Wall_Even_0" + (1 + rng.Next(5));
    }

    static List<Vector4> wallEdges = new List<Vector4>();   // nA,kA,nB,kB of every non-gate single-bench cliff edge
    static List<Vector4> highEdges = new List<Vector4>();   // same for sheer cliffs (several walls tall)

    static int BuildWalls(Transform parent, System.Random rng)
    {
        int count = 0; wallEdges.Clear(); highEdges.Clear();
        for (int k = K0; k <= K1; k++)
            for (int n = N0; n <= N1; n++)
            {
                int lb = L(n, k); if (lb == 0) continue;
                for (int d = 0; d < 4; d++)
                {
                    int a = n + DX[d], b = k + DK[d];
                    int la = L(a, b); if (la >= lb) continue;
                    int drop = lb - la;                                         // number of 9 m wall layers stacked on this edge
                    Vector2 cA = Center(a, b), cB = Center(n, k);
                    Vector2 down = (cB - cA).normalized, mid = (cA + cB) * 0.5f, along = new Vector2(-down.y, down.x);
                    float yaw = Mathf.Atan2(down.x, down.y) * Mathf.Rad2Deg;
                    Gate gate = null; if (gateOf[n - N0, k - K0] >= 0) { var cg = gates[gateOf[n - N0, k - K0]]; if (cg.nA == a && cg.kA == b) gate = cg; }
                    if (gate == null) { if (drop == 1) wallEdges.Add(new Vector4(a, b, n, k)); else highEdges.Add(new Vector4(a, b, n, k)); }
                    var spans = new List<Vector2>(); float half = T * 0.5f + 0.9f;
                    var fullSpans = new List<Vector2> { new Vector2(-half, half) };
                    if (gate == null) spans.Add(new Vector2(-half, half));
                    else { spans.Add(new Vector2(-half, -gate.width * 0.5f)); spans.Add(new Vector2(gate.width * 0.5f, half)); }
                    for (int layer = 0; layer < drop; layer++)
                    {
                        float baseY = LV(lb) - 0.3f + D * layer;
                        bool open = gate != null && (!gate.mine || layer < 2);        // the mine gate is a 2-wall-high opening with a lintel above
                        foreach (var sp in (open ? spans : fullSpans))
                        {
                            float len = sp.y - sp.x; if (len < 1.5f) continue;
                            int pc = Mathf.Max(1, Mathf.CeilToInt(len / 14f)); float seg = len / pc;
                            for (int j = 0; j < pc; j++)
                            {
                                Vector2 pt = mid + along * (sp.x + (j + 0.5f) * seg) - down * 0.7f;
                                if (WallPiece(parent, WallName(rng), pt, yaw, seg, baseY, 0.97f) != null) count++;
                            }
                        }
                        if (gate != null && !gate.mine) WallPiece(parent, WallName(rng), mid - down * 1.9f, yaw, gate.width + 1.2f, baseY, 0.93f);
                    }
                }
            }
        return count;
    }

    // ---------- helpers ----------
    static bool Ground(float x, float z, out float y, out Vector3 normal, float fromY = 200f)
    {
        y = 0f; normal = Vector3.up; float best = float.MaxValue; bool ok = false;
        foreach (var h in Physics.RaycastAll(new Vector3(x, fromY, z), Vector3.down, 400f))
            if (h.collider.name.StartsWith("PP_Ground") && h.distance < best) { best = h.distance; y = h.point.y; normal = h.normal; ok = true; }
        return ok;
    }

    static float TopOf(int level, float x, float z)
    {
        float y; Vector3 n; if (Ground(x, z, out y, out n)) return y; return LV(level) + 0.1f;
    }

    static GameObject Put(string prefab, Transform parent, float x, float z, float scale, float sink, float tilt, Material mat, System.Random rng, float yaw = -1f, float minN = 0f)
    {
        var pf = P(prefab); if (pf == null) return null;
        float y; Vector3 n; if (!Ground(x, z, out y, out n)) return null;
        if (n.y < minN) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        Vector3 nn = Vector3.Lerp(Vector3.up, n, tilt).normalized;
        float yw = yaw >= 0f ? yaw : (float)rng.NextDouble() * 360f;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, nn) * Quaternion.Euler(0, yw, 0);
        go.transform.localScale = Vector3.one * scale;
        go.transform.position = new Vector3(x, y - sink, z);
        if (mat != null) ApplyMat(go, mat);
        return go;
    }

    static void BuildRamp(Transform parent, Gate g)
    {
        int i = g.cliff; Vector2 down = g.down, b = g.b;
        float yTop = TopOf(i - 1, b.x - down.x * 3f, b.y - down.y * 3f);
        float yBot = LV(i) + 0.1f;
        float run = 19.5f, rise = yTop - yBot - 0.05f;
        float phi = Mathf.Atan2(rise, run), Ls = Mathf.Sqrt(run * run + rise * rise);
        float segLen = Ls / 2f, sx = segLen * 1.16f / 12.36f;
        float yawU = Mathf.Atan2(down.y, -down.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0, yawU, 0) * Quaternion.Euler(0, 0, phi * Mathf.Rad2Deg);
        Vector3 top = new Vector3(b.x - down.x * 0.4f, yTop - 0.05f, b.y - down.y * 0.4f);
        Vector3 dirDown = new Vector3(down.x * Mathf.Cos(phi), -Mathf.Sin(phi), down.y * Mathf.Cos(phi));
        var root = new GameObject("Ramp_L" + i + "_" + g.nB + "_" + g.kB); root.transform.SetParent(parent);
        var pf = P("PP_Bridge_05");
        for (int k = 0; k < 2; k++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
            go.transform.rotation = rot; go.transform.localScale = new Vector3(sx, 1f, 1.6f);
            go.transform.position = top + dirDown * (segLen * k) + Vector3.down * (0.02f * k);
        }
        var sup = P("PP_Mine_Wooden_Support_06");
        foreach (float t in new[] { 0.28f, 0.55f, 0.82f })
        {
            Vector3 p = top + dirDown * (Ls * t);
            float h = p.y - LV(i) - 0.45f; if (h < 1.5f) continue;
            var s = (GameObject)PrefabUtility.InstantiatePrefab(sup, root.transform);
            s.transform.rotation = Quaternion.Euler(0, yawU, 0); s.transform.localScale = new Vector3(1f, h / 10.4f, 0.56f);
            s.transform.position = new Vector3(p.x, LV(i) + 0.1f, p.z);
        }
        var tp = P("PP_Torch_Standing_01");
        if (tp != null)
            foreach (float sd in new[] { -1f, 1f })
            {
                Vector3 p = new Vector3(b.x + down.x * (run + 1.5f) + (-down.y) * sd * 3.2f, 0, b.y + down.y * (run + 1.5f) + down.x * sd * 3.2f);
                float gy; Vector3 gn;
                if (Ground(p.x, p.z, out gy, out gn)) { var tt = (GameObject)PrefabUtility.InstantiatePrefab(tp, root.transform); tt.transform.position = new Vector3(p.x, gy, p.z); tt.transform.localScale = Vector3.one * 2.2f; }
            }
    }

    static void BuildLift(Transform parent, Gate g)
    {
        int i = g.cliff; Vector2 down = g.down, b = g.b;
        float yUp = TopOf(g.upper, b.x - down.x * 3f, b.y - down.y * 3f);
        float yLow = LV(i) + 0.1f;
        float H = yUp - yLow;
        float yawD = Mathf.Atan2(down.x, down.y) * Mathf.Rad2Deg;
        Quaternion Rf = Quaternion.Euler(0, yawD, 0);
        Vector3 across = new Vector3(down.y, 0, -down.x);
        var root = new GameObject("Lift_L" + i + "_" + g.nB + "_" + g.kB); root.transform.SetParent(parent);
        var floor = P("PP_Wooden_Floor_01");
        System.Func<Vector3, float, Transform, GameObject> tile = (centre, y, par) =>
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(floor, par);
            go.transform.rotation = Rf;
            go.transform.position = new Vector3(centre.x, y, centre.z) + Rf * new Vector3(1.5f, 0f, 1.5f);
            return go;
        };
        var deck = new GameObject("Deck"); deck.transform.SetParent(root.transform);
        for (int c = -1; c <= 1; c++)
            tile(new Vector3(b.x, 0, b.y) + new Vector3(down.x, 0, down.y) * 1.5f + across * (3f * c), yUp + 0.02f, deck.transform);
        var sup = P("PP_Mine_Wooden_Support_06");
        // posts are stacked 10.4 m at a time so a lift can span any number of benches
        System.Action<Vector3, float, float, string> pole = (basePos, height, zScale, nm) =>
        {
            float y0 = basePos.y, rem = height;
            while (rem > 0.2f)
            {
                float seg = Mathf.Min(10.4f, rem);
                var po = (GameObject)PrefabUtility.InstantiatePrefab(sup, root.transform);
                po.name = nm; po.transform.rotation = Rf; po.transform.localScale = new Vector3(1f, seg / 10.4f, zScale);
                po.transform.position = new Vector3(basePos.x, y0, basePos.z);
                y0 += seg; rem -= seg;
            }
        };
        pole(new Vector3(b.x + down.x * 3.0f, yLow, b.y + down.y * 3.0f), H - 0.55f, 1f, "DeckSupport");
        float towerH = H + 3.2f;
        foreach (float sd in new[] { -4.4f, 4.4f })
            pole(new Vector3(b.x + down.x * 9.6f + across.x * sd, yLow, b.y + down.y * 9.6f + across.z * sd), towerH, 0.12f, "TowerFrame");
        var plat = new GameObject("Platform"); plat.transform.SetParent(root.transform);
        for (int d = 0; d < 2; d++)
            for (int c = -1; c <= 1; c++)
                tile(new Vector3(b.x, 0, b.y) + new Vector3(down.x, 0, down.y) * (4.5f + 3f * d) + across * (3f * c), yLow + 0.04f, plat.transform);
        Vector3 pc = new Vector3(b.x, 0, b.y) + new Vector3(down.x, 0, down.y) * 6f;
        var stopB = new GameObject("StopBottom"); stopB.transform.SetParent(root.transform); stopB.transform.position = new Vector3(pc.x, yLow + 0.1f, pc.z);
        var stopT = new GameObject("StopTop"); stopT.transform.SetParent(root.transform); stopT.transform.position = new Vector3(pc.x, yUp + 0.1f, pc.z);
        var lg = new GameObject("LiftLight"); lg.transform.SetParent(root.transform); lg.transform.position = new Vector3(pc.x, yLow + 6f, pc.z);
        var li = lg.AddComponent<Light>(); li.type = LightType.Point; li.color = new Color(1f, 0.72f, 0.4f); li.intensity = 7f; li.range = 18f; li.shadows = LightShadows.None;
    }

    // wooden scaffold with a ladder, hung on a cliff face (decoration, like the plank scaffolds in the chasm screenshot)
    static void BuildScaffold(Transform parent, Vector4 e, System.Random rng)
    {
        int lb = L((int)e.z, (int)e.w);
        Vector2 cA = Center((int)e.x, (int)e.y), cB = Center((int)e.z, (int)e.w);
        Vector2 down = (cB - cA).normalized, mid = (cA + cB) * 0.5f, across = new Vector2(down.y, -down.x);
        float yawD = Mathf.Atan2(down.x, down.y) * Mathf.Rad2Deg; Quaternion Rf = Quaternion.Euler(0, yawD, 0);
        float baseY = LV(lb) + 0.1f, midY = baseY + D * 0.5f;
        var root = new GameObject("Scaffold_L" + lb); root.transform.SetParent(parent);
        var sup = P("PP_Mine_Wooden_Support_06"); var floor = P("PP_Wooden_Floor_01"); var ladder = P("PP_Ladder_New_01"); var fence = P("PP_Log_Fence_01");
        foreach (float sd in new[] { -2.7f, 2.7f })
        {
            Vector2 p = mid + down * 3.4f + across * sd;
            var s = (GameObject)PrefabUtility.InstantiatePrefab(sup, root.transform);
            s.transform.rotation = Rf; s.transform.localScale = new Vector3(1f, (midY - baseY) / 10.4f, 0.12f); s.transform.position = new Vector3(p.x, baseY, p.y);
        }
        for (int c = -1; c <= 1; c += 2)
        {
            Vector2 cen = mid + down * 1.5f + across * (1.5f * c);
            var f = (GameObject)PrefabUtility.InstantiatePrefab(floor, root.transform);
            f.transform.rotation = Rf; f.transform.position = new Vector3(cen.x, midY, cen.y) + Rf * new Vector3(1.5f, 0f, 1.5f);
        }
        if (ladder != null)
            for (int j = 0; j < 2; j++)
            {
                var l = (GameObject)PrefabUtility.InstantiatePrefab(ladder, root.transform);
                l.transform.rotation = Rf * Quaternion.Euler(-8f, 0f, 0f); l.transform.localScale = Vector3.one * 1.05f;
                Vector2 p = mid + down * (3.4f + j * 0.2f) + across * 0f;
                l.transform.position = new Vector3(p.x, baseY + j * 4.3f, p.y);
            }
        if (fence != null)
            for (int c = -2; c <= 2; c++)
            {
                Vector2 p = mid + down * 3.2f + across * (c * 1.4f);
                var fe = (GameObject)PrefabUtility.InstantiatePrefab(fence, root.transform);
                fe.transform.rotation = Rf * Quaternion.Euler(0, 90f, 0); fe.transform.localScale = Vector3.one * 1.15f; fe.transform.position = new Vector3(p.x, midY + 0.05f, p.y);
            }
    }

    static void SetCaveLayer(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 8;           // like the other cave pieces: not lit by the sun
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask = 2;
    }

    // Copies the user's underground cave (the (B) half that was brought to the desert) and re-attaches its entrance to the mine gate,
    // so the deepest bench leads straight into it. Everything is rigidly turned 180 degrees about the old doorway and moved down.
    static string AttachCave(Transform parent, Gate g)
    {
        var old = GameObject.Find("QuarryCave"); if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("QuarryCave"); root.transform.SetParent(parent);
        Vector3 E = new Vector3(-518f, 2.7f, 815f);          // old doorway (rigid-transform pivot of the (B) half)
        const float mouthOffset = 4.5f;                       // doorway plane to cliff line
        Vector2 fwd = -g.down;
        float theta = Mathf.Atan2(fwd.y, -fwd.x) * Mathf.Rad2Deg;                    // turns the cave interior (-x) onto 'fwd'
        Quaternion R = Quaternion.Euler(0, theta, 0);
        float yMouth = LV(g.cliff) + 0.1f, floorAtDoor = caveFloorAtDoor;
        Vector3 G0 = new Vector3(g.b.x + fwd.x * mouthOffset, 0f, g.b.y + fwd.y * mouthOffset);
        string[] groups = { "Cave", "Cave Props", "Terrain_Stone", "Stones&Rocks", "Vegetation", "Crystals&Ores&Veins", "Coins", "Props", "Mushrooms", "Rails&Mine Carts", "Runes", "Bridges", "Stalactite&Stalagmite&Stalagnate", "Lighting", "FX" };
        int n = 0;
        foreach (var gn in groups)
        {
            var src = GameObject.Find(gn); if (src == null) continue;
            var dst = new GameObject(gn).transform; dst.SetParent(root.transform);
            var list = new List<Transform>(); foreach (Transform t in src.transform) if (t.name.Contains("(B)") && t.position.x < E.x + 1.5f) list.Add(t);   // only the cave interior, not the desert side outside the doorway
            foreach (var t in list)
            {
                var c = Object.Instantiate(t.gameObject, dst); c.name = t.name;
                Vector3 rel = t.position - E; Vector3 flat = R * new Vector3(rel.x, 0f, rel.z);
                c.transform.position = new Vector3(G0.x + flat.x, yMouth + (t.position.y - floorAtDoor), G0.z + flat.z);
                c.transform.rotation = R * t.rotation; c.transform.localScale = t.localScale; n++;
            }
        }
        // threshold planks over the seam between the bench tile and the cave floor
        var thr = P("PP_Wooden_Floor_01");
        if (thr != null)
            for (int ci = -3; ci <= 3; ci++)
                for (int ui = 0; ui < 3; ui++)
                {
                    var f = (GameObject)PrefabUtility.InstantiatePrefab(thr, root.transform); f.name = "Threshold";
                    Vector2 p = new Vector2(G0.x, G0.z) - fwd * (6f - 3f * ui) + new Vector2(fwd.y, -fwd.x) * (3f * ci);
                    f.transform.rotation = Quaternion.Euler(0, theta, 0);
                    f.transform.position = new Vector3(p.x, yMouth + 0.45f, p.y) + f.transform.rotation * new Vector3(1.5f, 0f, 1.5f);
                }
        return "cave copy: " + n + " objects, mouth at (" + g.b.x.ToString("F0") + "," + g.b.y.ToString("F0") + ") floor y=" + yMouth.ToString("F1") + " theta=" + theta.ToString("F0");
    }

    static float caveFloorAtDoor = 3.0f;

    static bool NearGate(float x, float z, float r)
    {
        foreach (var g in gates)
        {
            Vector2 a = g.b - g.down * 16f, c = g.b + g.down * 20f;
            for (float t = 0f; t <= 1f; t += 0.25f) { Vector2 q = Vector2.Lerp(a, c, t); if ((q.x - x) * (q.x - x) + (q.y - z) * (q.y - z) < r * r) return true; }
        }
        return false;
    }

    static bool NearPit(int n, int k, int r)
    {
        for (int a = -r; a <= r; a++) for (int b = -r; b <= r; b++) if (L(n + a, k + b) > 0) return true;
        return false;
    }

    static Vector2 Rnd(System.Random rng, float r) { return new Vector2((float)(rng.NextDouble() * 2 - 1) * r, (float)(rng.NextDouble() * 2 - 1) * r); }

    public static string Build(int seed)
    {
        var sb = new StringBuilder(); var rng = new System.Random(seed);
        _desertMat = AssetDatabase.LoadAssetAtPath<Material>(DesertMatPath);
        if (!AssetDatabase.IsValidFolder(AssetDir)) AssetDatabase.CreateFolder("Assets/Scenes/MiningMapAssets", "Quarry");
        var srcMat = P("PP_Cave_Wall_Even_01").GetComponentInChildren<MeshRenderer>().sharedMaterial;
        _stoneMat = TintedCopy("PP_Quarry_Stone", srcMat, new Color(2.4f, 2.45f, 2.6f, 1f), 0.1f);
        _dirt[0] = TintedCopy("PP_Quarry_Dirt", srcMat, new Color(1.8f, 1.55f, 1.35f, 1f), 0.05f);
        _dirt[1] = TintedCopy("PP_Quarry_Dust", srcMat, new Color(2.3f, 2.05f, 1.75f, 1f), 0.05f);
        _dirt[2] = TintedCopy("PP_Quarry_Clay", srcMat, new Color(1.65f, 1.3f, 1.1f, 1f), 0.05f);
        _dirt[3] = TintedCopy("PP_Quarry_Gravel", srcMat, new Color(1.75f, 1.75f, 1.8f, 1f), 0.08f);
        _mineStone = TintedCopy("PP_Mine_Stone", srcMat, new Color(1.5f, 1.45f, 1.4f, 1f), 0.1f);
        _mineFloor = TintedCopy("PP_Mine_Floor", srcMat, new Color(1.2f, 1.05f, 0.9f, 1f), 0.05f);
        _pinkMat = PinkMaterial(P("PP_Tree_01").GetComponentInChildren<MeshRenderer>().sharedMaterial);

        var oldQ = GameObject.Find("Quarry"); if (oldQ != null) Object.DestroyImmediate(oldQ);
        var dw = GameObject.Find("ForestDesert/Walls");
        if (dw != null) { var kill = new List<GameObject>(); foreach (Transform w in dw.transform) if (w.position.x > -200f) kill.Add(w.gameObject); foreach (var kg in kill) Object.DestroyImmediate(kg); }
        foreach (var f in System.IO.Directory.GetFiles(System.IO.Path.GetFullPath(AssetDir), "PlateauTile_*.asset")) { System.IO.File.Delete(f); if (System.IO.File.Exists(f + ".meta")) System.IO.File.Delete(f + ".meta"); }
        AssetDatabase.Refresh();

        var root = new GameObject("Quarry").transform;
        var gPlat = Grp(root, "PlateauTiles"); var gBench = Grp(root, "BenchTiles"); var gWall = Grp(root, "CliffWalls"); var gRamp = Grp(root, "Ramps"); var gLift = Grp(root, "Lifts");
        var gScaf = Grp(root, "Scaffolds"); var gMtn = Grp(root, "Mountains"); var gRock = Grp(root, "Rocks"); var gSlab = Grp(root, "RimSlabs"); var gDeco = Grp(root, "Decor");
        var gTree = Grp(root, "PinkTrees"); var gCamp = Grp(root, "Camps"); var gFx = Grp(root, "Mist"); var gLight = Grp(root, "Lights");

        GenMap(rng); Components(); PlanGates(rng, sb); PlanRoads();
        var cnt = new int[Levels + 1]; for (int k = K0; k <= K1; k++) for (int n = N0; n <= N1; n++) cnt[L(n, k)]++;
        sb.AppendLine("tiles per level: " + string.Join(",", System.Array.ConvertAll(cnt, q => q.ToString())) + "; gates=" + gates.Count);

        // ---- tiles ----
        string[] grounds = { "PP_Ground_01", "PP_Ground_02", "PP_Ground_03", "PP_Ground_04" };
        string[] paths = { "PP_Ground_Path_01", "PP_Ground_Path_02", "PP_Ground_Path_03", "PP_Ground_Path_04" };
        string[] beds = { "PP_Ground_Riverbed_01", "PP_Ground_Riverbed_02", "PP_Ground_Riverbed_03", "PP_Ground_Riverbed_04" };
        int plate = 0, bench = 0, roadTiles = 0;
        // plateau: whole 23 m tiles where all four fine cells are plateau, 11.5 m tiles around the crater edge
        for (int j = -7; j <= 7; j++)
            for (int m = -8; m <= 10; m++)
            {
                int f0n = 2 * m - 1, f0k = 2 * j - 1;
                bool all = L(f0n, f0k) == 0 && L(f0n + 1, f0k) == 0 && L(f0n, f0k + 1) == 0 && L(f0n + 1, f0k + 1) == 0;
                if (all)
                {
                    Vector2 c = new Vector2(C.x + 23f * m, C.y + 23f * j);
                    PlateauTile(gPlat, P(grounds[rng.Next(4)]), new Vector3(c.x, 1.4f + (((m + j) & 1) == 0 ? 0f : 0.01f), c.y), 90f * rng.Next(4), 1f, ++plate);
                }
                else
                    for (int a = 0; a < 2; a++) for (int bq = 0; bq < 2; bq++)
                        {
                            int n = f0n + a, k = f0k + bq; if (L(n, k) != 0) continue;
                            Vector2 c = Center(n, k);
                            PlateauTile(gPlat, P(grounds[rng.Next(4)]), new Vector3(c.x, 1.4f + (((n + k) & 1) == 0 ? 0f : 0.01f), c.y), 90f * rng.Next(4), 0.5f, ++plate);
                        }
            }
        for (int k = K0; k <= K1; k++)
            for (int n = N0; n <= N1; n++)
            {
                int l = L(n, k); if (l == 0) continue;
                Vector2 c = Center(n, k);
                bool isRoad = road[n - N0, k - K0] == 1;
                float zone = Mathf.PerlinNoise(n * 0.17f + 11f, k * 0.17f + 5f);
                int mi = zone < 0.28f ? 2 : zone < 0.55f ? 0 : zone < 0.8f ? 1 : 3;
                string nm = isRoad ? grounds[rng.Next(4)] : (mi == 3 && rng.NextDouble() < 0.6 ? beds[rng.Next(4)] : grounds[rng.Next(4)]);
                var pf = P(nm) ?? P(grounds[0]);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, gBench);
                inst.transform.position = new Vector3(c.x, LV(l), c.y); inst.transform.rotation = Quaternion.Euler(0, 90f * rng.Next(4), 0); inst.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
                ApplyMat(inst, isRoad ? _dirt[1] : _dirt[mi]); bench++; if (isRoad) roadTiles++;
            }
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        sb.AppendLine("plateau tiles=" + plate + " bench tiles=" + bench + " road tiles=" + roadTiles);

        int walls = BuildWalls(gWall, rng);
        sb.AppendLine("cliff wall pieces=" + walls);
        Physics.SyncTransforms();

        var gMine = Grp(root, "UndergroundMine");
        foreach (var g in gates) { if (g.mine) sb.AppendLine(AttachCave(gMine, g)); else if (g.lift) BuildLift(gLift, g); else BuildRamp(gRamp, g); }
        Physics.SyncTransforms();

        // scaffolds on cliff edges that are free of gates
        int nScaf = 0;
        for (int tries = 0; tries < 400 && nScaf < 12 && wallEdges.Count > 0; tries++)
        {
            var e = wallEdges[rng.Next(wallEdges.Count)];
            Vector2 mid = (Center((int)e.x, (int)e.y) + Center((int)e.z, (int)e.w)) * 0.5f;
            if (NearGate(mid.x, mid.y, 26f)) continue;
            Vector2 dn = (Center((int)e.z, (int)e.w) - Center((int)e.x, (int)e.y)).normalized;
            if (L((int)e.z + (int)dn.x, (int)e.w + (int)dn.y) != L((int)e.z, (int)e.w)) continue;   // needs room in front
            BuildScaffold(gScaf, e, rng); nScaf++;
        }
        Physics.SyncTransforms();

        // ---- enclosing mountains (slightly tilted like layered slabs) ----
        string[] mts = { "PP_Mountain_01", "PP_Mountain_02", "PP_Mountain_03" };
        // the mountains are up to ~190 m deep, so their centres sit far enough out not to cover the plateau
        float eastX = C.x + 330f, northZ = C.y + 262f, southZ = C.y - 262f;
        int nmt = 0;
        System.Action<float, float, float> mtn = (x, z, yaw) =>
        {
            var pfm = P(mts[rng.Next(3)]); var go = (GameObject)PrefabUtility.InstantiatePrefab(pfm, gMtn);
            go.transform.rotation = Quaternion.Euler((float)(rng.NextDouble() * 6 - 3), yaw + (float)(rng.NextDouble() * 16 - 8), (float)(rng.NextDouble() * 6 - 3));
            go.transform.localScale = Vector3.one * (2.8f + (float)rng.NextDouble() * 0.8f);
            go.transform.position = new Vector3(x, 0.9f, z); ApplyMat(go, _desertMat); nmt++;
        };
        for (float z = southZ; z <= northZ + 1f; z += 78f) mtn(eastX, z, 90f);
        for (float x = -150f; x <= eastX + 1f; x += 78f) { mtn(x, northZ, 0f); mtn(x, southZ, 180f); }
        // second, even higher row behind the first
        for (float z = southZ - 20f; z <= northZ + 21f; z += 95f) mtn(eastX + 110f, z, 90f);
        for (float x = -120f; x <= eastX + 110f; x += 95f) { mtn(x, northZ + 110f, 0f); mtn(x, southZ - 110f, 180f); }

        // ---- safety fences along the top of the sheer cliffs ----
        int nFenceTop = 0; var fencePf = P("PP_Log_Fence_01");
        if (fencePf != null)
            foreach (var he in highEdges)
            {
                Vector2 hA = Center((int)he.x, (int)he.y), hB = Center((int)he.z, (int)he.w), hd = (hB - hA).normalized, hm = (hA + hB) * 0.5f, ha = new Vector2(-hd.y, hd.x);
                float fyaw = ((Mathf.Atan2(-ha.y, ha.x) * Mathf.Rad2Deg) % 360f + 360f) % 360f;
                for (int q = -2; q <= 2; q++)
                {
                    if (rng.NextDouble() < 0.18) continue;                       // gaps, so it looks rustic
                    Vector2 fp = hm - hd * 2.4f + ha * (q * 2.2f);
                    if (Put("PP_Log_Fence_0" + (1 + rng.Next(4)), gDeco, fp.x, fp.y, 1.7f, 0.05f, 0.1f, null, rng, fyaw) != null) nFenceTop++;
                }
            }

        // ---- rim: layered slab outcrops (stacked, tilted PP_Cliff pieces in the desert material) ----
        string[] cliffs = { "PP_Cliff_01", "PP_Cliff_02", "PP_Cliff_03" };
        int nSlab = 0;
        for (int tries = 0; tries < 500 && nSlab < 46; tries++)
        {
            int n = rng.Next(N0, N1 + 1), k = rng.Next(K0, K1 + 1);
            if (L(n, k) != 0 || !NearPit(n, k, 2) || NearPit(n, k, 0)) continue;
            Vector2 c = Center(n, k) + Rnd(rng, 4f);
            if (NearGate(c.x, c.y, 16f)) continue;
            float tiltA = (float)(rng.NextDouble() * 10 - 5), tiltB = (float)(rng.NextDouble() * 10 - 5); float sc = 1.3f + (float)rng.NextDouble() * 1.2f;
            Vector2 rd = (c - C).normalized; float tyaw = Mathf.Atan2(rd.x, rd.y) * Mathf.Rad2Deg + 90f + (float)(rng.NextDouble() * 20 - 10);
            int layers = 1 + rng.Next(3);
            for (int ly = 0; ly < layers; ly++)
            {
                var s = Put(cliffs[rng.Next(3)], gSlab, c.x + (float)(rng.NextDouble() * 2 - 1) * 2f, c.y + (float)(rng.NextDouble() * 2 - 1) * 2f, 1f, 0.8f, 0f, _desertMat, rng, 0f);
                if (s == null) continue;
                float fall = 1f - ly * 0.15f;
                s.transform.localScale = new Vector3(sc * 1.9f * fall, sc * 0.62f, sc * 1.15f * fall);
                s.transform.rotation = Quaternion.Euler(tiltA, ((tyaw + ly * 12f) % 360f + 360f) % 360f, tiltB);
                s.transform.position += Vector3.up * (ly * 5.2f * sc * 0.62f);
                nSlab++;
            }
        }

        // ---- plateau decor ----
        var rocks = new List<string>();
        for (int i = 1; i <= 7; i++) rocks.Add("PP_Rock_" + i.ToString("D2"));
        for (int i = 1; i <= 5; i++) rocks.Add("PP_Rock_Brown_" + i.ToString("D2"));
        for (int i = 1; i <= 5; i++) rocks.Add("PP_Rock_Pile_" + i.ToString("D2"));
        string[] pinkTrees = { "PP_Tree_01", "PP_Tree_02", "PP_Tree_03", "PP_Tree_05", "PP_Tree_07", "PP_Fantasy_Birch_Tree_01", "PP_Fantasy_Birch_Tree_03", "PP_Fantasy_Birch_Tree_05", "PP_Fantasy_Birch_Tree_08" };
        int nr = 0, nc = 0, nt = 0, ngr = 0;
        for (int i = 0; i < 520; i++)
        {
            int n = rng.Next(N0, N1 + 1), k = rng.Next(K0, K1 + 1);
            if (L(n, k) != 0) continue;
            Vector2 c = Center(n, k) + Rnd(rng, 5.5f); bool rim = NearPit(n, k, 2);
            if (NearGate(c.x, c.y, 14f)) continue;
            double r = rng.NextDouble();
            if (rim && r < 0.35) { if (Put(pinkTrees[rng.Next(pinkTrees.Length)], gTree, c.x, c.y, 0.9f + (float)rng.NextDouble() * 0.7f, 0.2f, 0.2f, _pinkMat, rng, -1f, 0.9f) != null) nt++; }
            else if (rim && r < 0.6) { string gn = rng.Next(2) == 0 ? "PP_Grass_0" + (1 + rng.Next(6)) : "PP_Grass_Single_" + (1 + rng.Next(10)).ToString("D2"); if (Put(gn, gDeco, c.x, c.y, 1.8f + (float)rng.NextDouble() * 1.4f, 0.05f, 0.5f, _desertMat, rng, -1f, 0.85f) != null) ngr++; }
            else if (r < 0.75) { if (Put(rocks[rng.Next(rocks.Count)], gRock, c.x, c.y, 0.8f + (float)rng.NextDouble() * 1.8f, 0.3f, 0.6f, null, rng, -1f, 0.85f) != null) nr++; }
            else if (r < 0.82) { if (Put(pinkTrees[rng.Next(pinkTrees.Length)], gTree, c.x, c.y, 0.9f + (float)rng.NextDouble() * 0.6f, 0.2f, 0.2f, _pinkMat, rng, -1f, 0.9f) != null) nt++; }
            else if (Put("PP_Cactus_" + (1 + rng.Next(23)).ToString("D2"), gDeco, c.x, c.y, 1.8f + (float)rng.NextDouble() * 2.2f, 0.1f, 0.3f, null, rng, -1f, 0.9f) != null) nc++;
        }
        string[] mesas = { "PP_Rock_Plateau_01", "PP_Rock_Plateau_02", "PP_Rock_Plateau_03", "PP_Rock_Plateau_04" };
        int mesa = 0;
        for (int tries = 0; tries < 80 && mesa < 3; tries++)
        {
            int n = rng.Next(N0, N1 + 1), k = rng.Next(K0 + 4, K1 - 3); if (L(n, k) != 0 || NearPit(n, k, 5) || n < -12) continue;
            Vector2 c = Center(n, k); if (Put(mesas[mesa % 4], gRock, c.x, c.y, 0.55f, 2.5f, 0.1f, _desertMat, rng) != null) mesa++;
        }

        // ---- bench decor ----
        string[] stones = { "PP_Stone_Plain_01", "PP_Stone_Plain_02", "PP_Stone_Plain_03" };
        string[] props = { "PP_Barrel_01", "PP_Barrel_02", "PP_Barrel_03", "PP_Crate_Wooden_01", "PP_Crate_Wooden_02", "PP_Crate_Wooden_03", "PP_Wheelbarrow_New_01", "PP_Wheelbarrow_Used_01", "PP_Mine_Cart_01", "PP_Mine_Cart_03" };
        string[] tents = { "PP_Tent_01", "PP_Tent_02", "PP_Tent_03", "PP_Tent_04" };
        string[] peb = { "PP_Pebbles_01", "PP_Pebbles_02", "PP_Pebbles_03", "PP_Pebbles_04", "PP_Pebbles_05", "PP_Pebbles_06" };
        int ns = 0, nCamp = 0, nProps = 0, nPeb = 0, nBTree = 0;
        for (int k = K0; k <= K1; k++)
            for (int n = N0; n <= N1; n++)
            {
                int l = L(n, k); if (l == 0) continue;
                Vector2 c = Center(n, k);
                if (rng.NextDouble() < 0.28)
                {
                    int cb = 1 + rng.Next(3); Vector2 bp = c + Rnd(rng, 3f);
                    if (!NearGate(bp.x, bp.y, 14f))
                        for (int j = 0; j < cb; j++)
                        {
                            Vector2 q = bp + Rnd(rng, 3.5f);
                            var s = Put(stones[rng.Next(3)], gRock, q.x, q.y, 2.2f + (float)rng.NextDouble() * 1.8f, 0.2f, 0.2f, _dirt[3], rng, -1f, 0.9f);
                            if (s != null) ns++;
                        }
                }
                for (int j = 0; j < 2; j++) { Vector2 q = c + Rnd(rng, 5.5f); if (Put(peb[rng.Next(peb.Length)], gDeco, q.x, q.y, 2f + (float)rng.NextDouble() * 1.5f, 0.05f, 0.5f, null, rng, -1f, 0.85f) != null) nPeb++; }
                if (rng.NextDouble() < 0.1)
                {
                    Vector2 q = c + Rnd(rng, 4f);
                    string pn = rng.Next(3) == 0 ? "PP_Pillar_Stone_0" + (1 + rng.Next(9)) : rocks[rng.Next(rocks.Count)];
                    if (!NearGate(q.x, q.y, 12f) && Put(pn, gRock, q.x, q.y, 1.2f + (float)rng.NextDouble() * 1.5f, 0.2f, 0.3f, null, rng, -1f, 0.9f) != null) ns++;
                }
                if (l <= 2 && rng.NextDouble() < 0.05)
                {
                    Vector2 q = c + Rnd(rng, 4f);
                    if (!NearGate(q.x, q.y, 14f) && Put(pinkTrees[rng.Next(pinkTrees.Length)], gTree, q.x, q.y, 0.8f + (float)rng.NextDouble() * 0.5f, 0.2f, 0.2f, _pinkMat, rng, -1f, 0.9f) != null) nBTree++;
                }
                if (rng.NextDouble() < 0.035)
                {
                    Vector2 cp = c + Rnd(rng, 2f);
                    if (NearGate(cp.x, cp.y, 18f)) continue;
                    var cr = new GameObject("Camp_L" + l); cr.transform.SetParent(gCamp);
                    float by = (float)rng.NextDouble() * 360f;
                    for (int i = 0; i < 2; i++) { float a = by + i * 150f; if (Put(tents[rng.Next(4)], cr.transform, cp.x + Mathf.Cos(a * Mathf.Deg2Rad) * 6f, cp.y + Mathf.Sin(a * Mathf.Deg2Rad) * 6f, 1.6f, 0.05f, 0.2f, null, rng, (a + 90f) % 360f, 0.9f) != null) nProps++; }
                    for (int i = 0; i < 7; i++) { float a = (float)rng.NextDouble() * 6.283f, rr = 2f + (float)rng.NextDouble() * 5f; if (Put(props[rng.Next(props.Length)], cr.transform, cp.x + Mathf.Cos(a) * rr, cp.y + Mathf.Sin(a) * rr, 2.2f + (float)rng.NextDouble() * 0.6f, 0.05f, 0.2f, null, rng, -1f, 0.9f) != null) nProps++; }
                    if (Put("PP_Torch_Standing_01", cr.transform, cp.x, cp.y, 2.4f, 0.05f, 0.1f, null, rng) != null) nProps++;
                    if (Put("PP_Signpost_01", cr.transform, cp.x + 3f, cp.y - 3f, 1.8f, 0.05f, 0.1f, null, rng) != null) nProps++;
                    float gy; Vector3 gn;
                    if (Ground(cp.x, cp.y, out gy, out gn))
                    {
                        var lg = new GameObject("CampLight"); lg.transform.SetParent(gLight); lg.transform.position = new Vector3(cp.x, gy + 3.2f, cp.y);
                        var li = lg.AddComponent<Light>(); li.type = LightType.Point; li.color = new Color(1f, 0.62f, 0.28f); li.intensity = 6f; li.range = 24f; li.shadows = LightShadows.None;
                    }
                    nCamp++;
                }
            }
        // rubble at the foot of cliffs, breaks up the stair-stepped outline
        int nFoot = 0;
        foreach (var e in wallEdges)
        {
            if (rng.NextDouble() > 0.4) continue;
            Vector2 cA = Center((int)e.x, (int)e.y), cB = Center((int)e.z, (int)e.w), dn = (cB - cA).normalized, mid = (cA + cB) * 0.5f, ac = new Vector2(dn.y, -dn.x);
            Vector2 q = mid + dn * 3f + ac * ((float)(rng.NextDouble() * 2 - 1) * 4.5f);
            if (NearGate(q.x, q.y, 10f)) continue;
            if (Put(rocks[rng.Next(rocks.Count)], gRock, q.x, q.y, 1.4f + (float)rng.NextDouble() * 1.6f, 0.3f, 0.5f, null, rng, -1f, 0.85f) != null) nFoot++;
        }
        // rails on the deepest bench
        var railPf = P("PP_Rail_Straight_Long"); int nRail = 0;
        if (railPf != null)
        {
            var rb = railPf.GetComponentInChildren<MeshFilter>().sharedMesh.bounds; bool alongX = rb.size.x > rb.size.z; float len = (alongX ? rb.size.x : rb.size.z) * 1.3f;
            for (int k = K0; k <= K1 && nRail < 6; k++)
                for (int n = N0; n <= N1 && nRail < 6; n++)
                    if (L(n, k) == Floor && L(n + 1, k) == Floor && L(n - 1, k) == Floor && L(n, k + 1) == Floor && L(n, k - 1) == Floor && !NearGate(Center(n, k).x, Center(n, k).y, 14f))
                        for (int j = 0; j < 2 && nRail < 6; j++)
                        {
                            Vector2 c = Center(n, k); float x = c.x - len * 0.5f + j * len, z = c.y; float gy; Vector3 gn;
                            if (!Ground(x, z, out gy, out gn)) continue;
                            var rl = (GameObject)PrefabUtility.InstantiatePrefab(railPf, gDeco);
                            rl.transform.rotation = alongX ? Quaternion.identity : Quaternion.Euler(0, 90f, 0); rl.transform.localScale = Vector3.one * 1.3f; rl.transform.position = new Vector3(x, gy + 0.05f, z); nRail++;
                        }
        }

        // ---- mist in the depth (FX_Fog prefabs) + cold glow ----
        int nFog = 0;
        string[] fogBig = { "FX_Fog_Big_01", "FX_Fog_Big_02", "FX_Fog_Big_03", "FX_Fog_Big_04" };
        string[] fogSm = { "FX_Fog_01", "FX_Fog_02", "FX_Fog_03", "FX_Fog_04" };
        for (int tries = 0; tries < 900 && nFog < 30; tries++)
        {
            int n = rng.Next(N0, N1 + 1), k = rng.Next(K0, K1 + 1); int l = L(n, k); if (l < 2) continue;
            bool big = l >= 4 && rng.NextDouble() < 0.7;
            Vector2 c = Center(n, k); float gy; Vector3 gn; if (!Ground(c.x, c.y, out gy, out gn)) continue;
            var pf = P(big ? fogBig[rng.Next(4)] : fogSm[rng.Next(4)]); if (pf == null) { foreach (var g in AssetDatabase.FindAssets((big ? "FX_Fog_Big_01" : "FX_Fog_01") + " t:Prefab", new[] { "Assets/PurePoly" })) pf = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)); }
            if (pf == null) break;
            var fx = (GameObject)PrefabUtility.InstantiatePrefab(pf, gFx); fx.transform.position = new Vector3(c.x, gy + 1f, c.y); nFog++;
        }
        foreach (var bt in new[] { Vector2.zero })
        {
            // cold light at the deepest tiles
            var deep = new List<Vector2>(); for (int k = K0; k <= K1; k++) for (int n = N0; n <= N1; n++) if (L(n, k) == Levels) deep.Add(Center(n, k));
            for (int i = 0; i < Mathf.Min(4, deep.Count); i++)
            {
                var p = deep[(i * deep.Count) / 4]; float gy; Vector3 gn; if (!Ground(p.x, p.y, out gy, out gn)) continue;
                var lg = new GameObject("DeepGlow"); lg.transform.SetParent(gLight); lg.transform.position = new Vector3(p.x, gy + 8f, p.y);
                var li = lg.AddComponent<Light>(); li.type = LightType.Point; li.color = new Color(0.55f, 0.7f, 1f); li.intensity = 14f; li.range = 55f; li.shadows = LightShadows.None;
            }
        }

        sb.AppendLine($"scaffolds={nScaf} rim slabs={nSlab} plateau rocks={nr} cacti={nc} pink trees={nt}+{nBTree} grass={ngr} mesas={mesa} | bench stones={ns} pebbles={nPeb} camps={nCamp} props={nProps} foot rubble={nFoot} rails={nRail} fog={nFog} mountains={nmt} top fences={nFenceTop} highEdges={highEdges.Count}");
        Physics.SyncTransforms();
        AssetDatabase.SaveAssets();
        sb.AppendLine(Map());
        return sb.ToString();
    }

    public static string Map()
    {
        var sb = new StringBuilder("level map (rows z+ at top, cols x+; g=ramp landing, L=lift landing):\n");
        for (int k = K1; k >= K0; k--)
        {
            for (int n = N0; n <= N1; n++)
            {
                char ch = (char)('0' + L(n, k));
                if (gateOf != null && gateOf[n - N0, k - K0] >= 0) ch = gates[gateOf[n - N0, k - K0]].lift ? 'L' : 'g';
                sb.Append(ch);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string WalkTest()
    {
        Physics.SyncTransforms(); var sb = new StringBuilder();
        foreach (var g in gates)
        {
            if (g.lift || g.mine) continue;
            float run = 0, worst = 0, prevY = float.NaN, ms = 0; float yFirst = 0, yLast = 0;
            for (float t = -4f; t <= 22f; t += 0.1f)
            {
                Vector2 q = g.b + g.down * t; bool onBridge = false; float y = float.NaN, bd = 1e9f;
                foreach (var h in Physics.RaycastAll(new Vector3(q.x, 60f, q.y), Vector3.down, 150f)) { if (h.collider.isTrigger || h.normal.y < 0.45f) continue; if (h.collider.name.StartsWith("PP_Bridge")) onBridge = true; if (h.distance < bd) { bd = h.distance; y = h.point.y; } }
                if (t > 0.3f && t < 19.3f) { if (!onBridge) { run += 0.1f; worst = Mathf.Max(worst, run); } else run = 0; }
                if (t < 0.05f) yFirst = y; yLast = y;
                if (!float.IsNaN(prevY) && onBridge) ms = Mathf.Max(ms, Mathf.Abs(y - prevY)); if (onBridge) prevY = y;
            }
            sb.AppendLine("ramp L" + g.cliff + " tile(" + g.nB + "," + g.kB + "): widest gap " + worst.ToString("F1") + " step " + ms.ToString("F2") + " start y " + yFirst.ToString("F1") + " end y " + yLast.ToString("F1"));
        }
        return sb.ToString();
    }
}
#endif
