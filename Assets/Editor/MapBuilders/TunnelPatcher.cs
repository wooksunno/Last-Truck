#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TunnelPatcher
{
    static Material CaveMat()
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PurePoly/Mining_Pack/Prefabs/Cave/PP_Cave_Tube_04.prefab").GetComponent<MeshRenderer>().sharedMaterial;
    }

    class Chunked
    {
        public Dictionary<long, List<Vector3>> V = new Dictionary<long, List<Vector3>>();
        public Dictionary<long, List<Vector3>> N = new Dictionary<long, List<Vector3>>();
        public Dictionary<long, List<Vector2>> U = new Dictionary<long, List<Vector2>>();
        public void AddTri(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector2 uv)
        {
            Vector3 cen = (a + b + c) / 3f;
            long key = ((long)Mathf.FloorToInt((cen.x + 2000f) / 20f) * 1000L + Mathf.FloorToInt((cen.y + 200f) / 20f)) * 1000L + Mathf.FloorToInt((cen.z + 2000f) / 20f);
            if (!V.ContainsKey(key)) { V[key] = new List<Vector3>(); N[key] = new List<Vector3>(); U[key] = new List<Vector2>(); }
            V[key].Add(a); V[key].Add(b); V[key].Add(c); N[key].Add(na); N[key].Add(nb); N[key].Add(nc); U[key].Add(uv); U[key].Add(uv); U[key].Add(uv);
        }
        public int Emit(Transform parent, string namePrefix, string assetPath, Material mat)
        {
            int n = 0; bool first = !System.IO.File.Exists(assetPath);
            if (!first) AssetDatabase.DeleteAsset(assetPath);
            first = true;
            foreach (var kv in V)
            {
                var vv = kv.Value; var idx = new int[vv.Count]; for (int i = 0; i < idx.Length; i++) idx[i] = i;
                var m = new Mesh { name = namePrefix + "_" + n };
                m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(vv); m.SetNormals(N[kv.Key]); m.SetUVs(0, U[kv.Key]); m.SetTriangles(idx, 0); m.RecalculateBounds();
                if (first) { AssetDatabase.CreateAsset(m, assetPath); first = false; } else AssetDatabase.AddObjectToAsset(m, assetPath);
                var go = new GameObject(namePrefix + "_" + n); go.transform.SetParent(parent);
                go.AddComponent<MeshFilter>().sharedMesh = m; go.AddComponent<MeshRenderer>().sharedMaterial = mat; go.AddComponent<MeshCollider>().sharedMesh = m;
                n++;
            }
            return n;
        }
    }

    static void AddBox(Chunked ch, Vector3 center, Vector3 half, Vector2 uv)
    {
        Vector3[] c = new Vector3[8];
        for (int i = 0; i < 8; i++) c[i] = center + new Vector3(((i & 1) == 0 ? -1 : 1) * half.x, ((i & 2) == 0 ? -1 : 1) * half.y, ((i & 4) == 0 ? -1 : 1) * half.z);
        int[][] faces = { new[] { 1, 3, 7, 5 }, new[] { 0, 4, 6, 2 }, new[] { 2, 6, 7, 3 }, new[] { 0, 1, 5, 4 }, new[] { 4, 5, 7, 6 }, new[] { 0, 2, 3, 1 } };
        Vector3[] nrm = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        for (int f = 0; f < 6; f++)
        {
            var q = faces[f];
            Vector3 a = c[q[0]], b = c[q[1]], d = c[q[2]], e = c[q[3]];
            if (Vector3.Dot(Vector3.Cross(b - a, d - a), nrm[f]) < 0f) { ch.AddTri(a, d, b, nrm[f], nrm[f], nrm[f], uv); ch.AddTri(a, e, d, nrm[f], nrm[f], nrm[f], uv); }
            else { ch.AddTri(a, b, d, nrm[f], nrm[f], nrm[f], uv); ch.AddTri(a, d, e, nrm[f], nrm[f], nrm[f], uv); }
        }
    }

    static Transform Root(string name)
    {
        var r = GameObject.Find(name); if (r != null) Object.DestroyImmediate(r);
        return new GameObject(name).transform;
    }

    // -------------------- floor patches --------------------
    public static string PatchFloors(List<WalkCell> cells, string rootName, string assetPrefix, int minComp)
    {
        var sb = new StringBuilder();
        float cs = 3f;
        var comp = new Dictionary<int, int>(); foreach (var c in cells) { if (!comp.ContainsKey(c.comp)) comp[c.comp] = 0; comp[c.comp]++; }
        var good = new List<WalkCell>(); foreach (var c in cells) if (comp[c.comp] >= minComp) good.Add(c);
        var grid = new Dictionary<long, List<WalkCell>>();
        System.Func<int, int, long> K = (i, k) => (long)i * 100000L + k;
        foreach (var c in good) { long key = K(Mathf.RoundToInt(c.p.x / cs), Mathf.RoundToInt(c.p.z / cs)); if (!grid.ContainsKey(key)) grid[key] = new List<WalkCell>(); grid[key].Add(c); }
        var patches = new List<Vector3>(); // x,y,z of patch cell centres
        var seen = new HashSet<long>();
        // 1) closing: fill empty cells that have floor on opposite sides
        foreach (var kv in new List<KeyValuePair<long, List<WalkCell>>>(grid))
        {
            int ci = Mathf.RoundToInt(kv.Value[0].p.x / cs), ck = Mathf.RoundToInt(kv.Value[0].p.z / cs);
            for (int dx = -2; dx <= 2; dx++)
                for (int dz = -2; dz <= 2; dz++)
                {
                    int i = ci + dx, k = ck + dz; long key = K(i, k);
                    if (grid.ContainsKey(key) || seen.Contains(key)) continue;
                    seen.Add(key);
                    // gather neighbours in 5x5
                    var ys = new List<float>(); bool px = false, nxn = false, pz = false, nz = false;
                    for (int ax = -2; ax <= 2; ax++)
                        for (int az = -2; az <= 2; az++)
                        {
                            List<WalkCell> l; if (!grid.TryGetValue(K(i + ax, k + az), out l)) continue;
                            foreach (var w in l) ys.Add(w.p.y);
                            if (ax > 0) px = true; if (ax < 0) nxn = true; if (az > 0) pz = true; if (az < 0) nz = true;
                        }
                    if (ys.Count < 5) continue;
                    if (!((px && nxn) || (pz && nz))) continue;
                    ys.Sort(); float y = ys[ys.Count / 2];
                    if (ys[ys.Count - 1] - ys[0] > 3.2f) continue;
                    Vector3 pc = new Vector3(i * cs, y, k * cs);
                    if (!Physics.Raycast(pc + Vector3.up * 1.5f, Vector3.up, 40f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.CheckSphere(pc + Vector3.up * 1.9f, 1.0f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    patches.Add(pc);
                }
        }
        sb.AppendLine("floor-hole patch cells=" + patches.Count);
        // 2) bridges between components
        var comps = new Dictionary<int, List<WalkCell>>();
        foreach (var c in good) { if (!comps.ContainsKey(c.comp)) comps[c.comp] = new List<WalkCell>(); comps[c.comp].Add(c); }
        var ids = new List<int>(comps.Keys);
        int bridges = 0;
        for (int a = 0; a < ids.Count; a++)
            for (int b = a + 1; b < ids.Count; b++)
            {
                float best = 1e9f; WalkCell ca = null, cb = null;
                foreach (var x in comps[ids[a]]) foreach (var y in comps[ids[b]]) { float d = Vector3.Distance(x.p, y.p); if (d < best) { best = d; ca = x; cb = y; } }
                if (best > 32f || Mathf.Abs(ca.p.y - cb.p.y) > 9f) continue;
                int steps = Mathf.CeilToInt(best / 1.5f);
                Vector3 dir = (cb.p - ca.p); Vector3 flat = new Vector3(dir.x, 0, dir.z).normalized; Vector3 side = Vector3.Cross(Vector3.up, flat);
                for (int s = 0; s <= steps; s++)
                {
                    float t = (float)s / steps; Vector3 p = Vector3.Lerp(ca.p, cb.p, t);
                    for (float w = -4f; w <= 4f; w += 2f) patches.Add(p + side * w);
                }
                bridges++;
                sb.AppendLine($"bridge {ids[a]}-{ids[b]} len={best:F1} dy={cb.p.y - ca.p.y:F1} at ({ca.p.x:F0},{ca.p.z:F0})->({cb.p.x:F0},{cb.p.z:F0})");
            }
        // meshes
        var root = Root(rootName);
        var ch = new Chunked();
        var dirt = new Vector2(0.578f, 0.422f);
        foreach (var p in patches) AddBox(ch, new Vector3(p.x, p.y - 0.45f, p.z), new Vector3(1.6f, 0.5f, 1.6f), dirt);
        int n = ch.Emit(root, "Floor", "Assets/Scenes/MiningMapAssets/" + assetPrefix + "_Floor.asset", CaveMat());
        sb.AppendLine($"floor chunks={n} bridges={bridges}");
        return sb.ToString();
    }

    // -------------------- roof lids over open-top corridor cells --------------------
    public static string RoofLids(float xMin, float xMax, float zMin, float zMax, string rootName, string assetPrefix)
    {
        foreach (var n in new[] { rootName }) { var o = GameObject.Find(n); if (o != null) Object.DestroyImmediate(o); }
        Physics.SyncTransforms();
        var sb = new StringBuilder();
        float cs = 3f;
        var open = new List<Vector3>(); var ceilH = new Dictionary<long, float>();
        System.Func<int, int, long> K = (i, k) => (long)i * 100000L + k;
        for (float z = zMin + 1.5f; z < zMax; z += cs)
            for (float x = xMin + 1.5f; x < xMax; x += cs)
                foreach (var h in Physics.RaycastAll(new Vector3(x, 40f, z), Vector3.down, 80f))
                {
                    if (h.collider.isTrigger || h.normal.y < 0.55f || !h.collider.name.StartsWith("PP_Cave_Tube")) continue;
                    RaycastHit up; int ci = Mathf.FloorToInt((x - xMin) / cs), ck = Mathf.FloorToInt((z - zMin) / cs);
                    if (Physics.Raycast(h.point + Vector3.up * 0.15f, Vector3.up, out up, 60f, ~0, QueryTriggerInteraction.Ignore)) ceilH[K(ci, ck)] = up.distance; else open.Add(h.point);
                }
        sb.AppendLine("open-top cells=" + open.Count);
        if (open.Count == 0) return sb.ToString();
        var blobs = new List<Vector3>(); var seen = new HashSet<long>();
        System.Func<Vector3, long> Q = p => ((long)Mathf.RoundToInt(p.x / 2.6f) * 100000L + Mathf.RoundToInt(p.z / 2.6f)) * 1000L + Mathf.RoundToInt(p.y / 2.6f);
        foreach (var p in open)
        {
            int ci = Mathf.FloorToInt((p.x - xMin) / cs), ck = Mathf.FloorToInt((p.z - zMin) / cs);
            float sum = 0; int n = 0;
            for (int dx = -4; dx <= 4; dx++) for (int dz = -4; dz <= 4; dz++) { float v; if (ceilH.TryGetValue(K(ci + dx, ck + dz), out v) && v < 30f) { sum += v; n++; } }
            float ch = Mathf.Clamp(n > 0 ? sum / n : 15f, 9f, 20f);
            for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
            {
                Vector3 b = new Vector3(xMin + (ci + dx) * cs + 1.5f, p.y + ch + 3.4f, zMin + (ck + dz) * cs + 1.5f);
                if (seen.Add(Q(b))) blobs.Add(b);
            }
        }
        sb.AppendLine("lid blobs=" + blobs.Count);
        sb.AppendLine(BlobMesh(blobs, 4.2f, rootName, assetPrefix));
        return sb.ToString();
    }

    // -------------------- wall / roof plugs --------------------
    public static string PlugWalls(List<WalkCell> cells, string rootName, string assetPrefix, int minComp)
    {
        var sb = new StringBuilder();
        var comp = new Dictionary<int, int>(); foreach (var c in cells) { if (!comp.ContainsKey(c.comp)) comp[c.comp] = 0; comp[c.comp]++; }
        float[] heights = { 2f, 4.5f, 7f, 9.5f, 12f };
        const int ring = 24; const float range = 24f;
        var blobs = new HashSet<long>(); var blobList = new List<Vector3>();
        System.Func<Vector3, long> Q = p => ((long)Mathf.RoundToInt(p.x / 2.4f) * 100000L + Mathf.RoundToInt(p.z / 2.4f)) * 1000L + Mathf.RoundToInt(p.y / 2.4f);
        int holeRays = 0, roofRays = 0;
        foreach (var c in cells)
        {
            if (comp[c.comp] < minComp) continue;
            int near = 0; for (int a = 0; a < 8; a++) { float ang = a * Mathf.PI / 4f; if (Physics.Raycast(c.p + Vector3.up * 2f, new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)), 14f, ~0, QueryTriggerInteraction.Ignore)) near++; }
            if (near < 5) continue;
            for (int hi = 0; hi < heights.Length; hi++)
            {
                float[] dist = new float[ring]; Vector3 o = c.p + Vector3.up * heights[hi];
                for (int a = 0; a < ring; a++)
                {
                    float ang = a * Mathf.PI * 2f / ring; Vector3 d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)); RaycastHit h;
                    dist[a] = Physics.Raycast(o, d, out h, range, ~0, QueryTriggerInteraction.Ignore) ? h.distance : -1f;
                }
                for (int a = 0; a < ring; a++)
                {
                    if (dist[a] >= 0f) continue;
                    float ang = a * Mathf.PI * 2f / ring; Vector3 d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                    Vector3 ep = o + d * range;
                    if (Physics.Raycast(ep, Vector3.up, 80f, ~0, QueryTriggerInteraction.Ignore)) continue; // leads into another covered space (junction)
                    float fill = -1f;
                    for (int s = 1; s <= 4 && fill < 0f; s++) { float l = dist[(a - s + ring) % ring], r = dist[(a + s) % ring]; if (l >= 0f && r >= 0f) fill = (l + r) * 0.5f; else if (l >= 0f) fill = l; else if (r >= 0f) fill = r; }
                    if (fill < 0f) continue;
                    Vector3 p = o + d * (fill + 0.9f);
                    long key = Q(p); if (blobs.Add(key)) { blobList.Add(p); holeRays++; }
                }
            }
            // roof
            if (!Physics.Raycast(c.p + Vector3.up * 2f, Vector3.up, 60f, ~0, QueryTriggerInteraction.Ignore))
            {
                for (int s = 0; s < 3; s++)
                {
                    Vector3 p = c.p + Vector3.up * (13f + s * 2.2f); long key = Q(p); if (blobs.Add(key)) { blobList.Add(p); roofRays++; }
                }
            }
        }
        sb.AppendLine($"plug blobs wall={holeRays} roof={roofRays}");
        return sb.ToString() + BlobMesh(blobList, 3.4f, rootName, assetPrefix);
    }

    public static string BlobMesh(List<Vector3> blobList, float r0, string rootName, string assetPrefix, float cs = 1.6f)
    {
        var sb = new StringBuilder();
        if (blobList.Count == 0) return "";
        Vector3 mn = new Vector3(1e9f, 1e9f, 1e9f), mx = -mn;
        foreach (var p in blobList) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
        mn -= Vector3.one * (r0 + 3f); mx += Vector3.one * (r0 + 3f);
        var hash = new Dictionary<long, List<int>>();
        System.Func<float, int> hc = v => Mathf.FloorToInt(v / 6f);
        for (int i = 0; i < blobList.Count; i++) { var p = blobList[i]; long key = ((long)hc(p.x) * 100000L + hc(p.z)) * 1000L + hc(p.y); if (!hash.ContainsKey(key)) hash[key] = new List<int>(); hash[key].Add(i); }
        System.Func<float, float, float, float> field = (x, y, z) =>
        {
            float best = 1e9f; int cx = hc(x), cy = hc(y), cz = hc(z);
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) for (int dz = -1; dz <= 1; dz++)
            {
                List<int> l; if (!hash.TryGetValue(((long)(cx + dx) * 100000L + (cz + dz)) * 1000L + (cy + dy), out l)) continue;
                foreach (int i in l) { float d = (new Vector3(x, y, z) - blobList[i]).magnitude; if (d < best) best = d; }
            }
            return best - r0;
        };
        int nx = Mathf.CeilToInt((mx.x - mn.x) / cs), ny = Mathf.CeilToInt((mx.y - mn.y) / cs), nz = Mathf.CeilToInt((mx.z - mn.z) / cs);
        sb.AppendLine($"plug grid {nx}x{ny}x{nz}");
        int sx = nx + 1, sy = ny + 1, sz = nz + 1;
        float[] F = new float[sx * sy * sz];
        for (int k = 0; k < sz; k++) for (int j = 0; j < sy; j++) for (int i = 0; i < sx; i++) F[(k * sy + j) * sx + i] = field(mn.x + i * cs, mn.y + j * cs, mn.z + k * cs);
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
            cellVert[(k * ny + j) * nx + i] = verts.Count; verts.Add(mn + (sum / cnt) * cs);
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
        var root = Root(rootName);
        var chunk = new Chunked(); var grey = new Vector2(0.68f, 0.96f);
        float e0 = 0.6f;
        System.Func<Vector3, Vector3> grad = p => new Vector3(field(p.x + e0, p.y, p.z) - field(p.x - e0, p.y, p.z), field(p.x, p.y + e0, p.z) - field(p.x, p.y - e0, p.z), field(p.x, p.y, p.z + e0) - field(p.x, p.y, p.z - e0)).normalized;
        for (int t = 0; t < tri.Count / 3; t++)
        {
            Vector3 a = verts[tri[t * 3]], b = verts[tri[t * 3 + 1]], c = verts[tri[t * 3 + 2]];
            Vector3 cr = Vector3.Cross(b - a, c - a); if (cr.sqrMagnitude < 1e-6f) continue;
            Vector3 gc = grad((a + b + c) / 3f);
            if (Vector3.Dot(cr, gc) < 0f) { Vector3 tmp = b; b = c; c = tmp; }
            chunk.AddTri(a, b, c, grad(a), grad(b), grad(c), grey);
        }
        int n = chunk.Emit(root, "Plug", "Assets/Scenes/MiningMapAssets/" + assetPrefix + "_Plugs.asset", CaveMat());
        sb.AppendLine($"plug tris={tri.Count / 3} chunks={n}");
        return sb.ToString();
    }
}
#endif
