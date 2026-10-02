#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ForestMineBuilder
{
    public const float S = 1.9f;
    public const float Fy = 1.5f;
    public const float Xm = -360f;

    public static float ExitX, ExitZ;
    public static float PivotY = 11.4f;
    public static float[] EntranceEndZ = { 178f };
    public static float[] EntranceX = { -360f };
    public static float[] EntranceR = { 21f };
    public static float FrontSlope = 0.7f;
    public static float FrontZ = 177f;
    public static float HoleRadius = 21f;

    static readonly Vector3 pZ = new Vector3(0, 0, 1), mZ = new Vector3(0, 0, -1), pX = new Vector3(1, 0, 0), mX = new Vector3(-1, 0, 0);

    public static readonly Dictionary<string, Vector3[]> Defs = new Dictionary<string, Vector3[]>
    {
        { "PP_Cave_Tube_01", new[] { new Vector3(-0.1f, 0f, 1.3f), pZ, new Vector3(0f, 0.2f, -30.4f), mZ } },
        { "PP_Cave_Tube_02", new[] { new Vector3(-0.3f, 0.2f, 1.3f), pZ, new Vector3(-0.2f, 0.5f, -30.4f), mZ } },
        { "PP_Cave_Tube_10", new[] { new Vector3(0f, 0f, 1.2f), pZ, new Vector3(0.1f, -0.3f, -28f), mZ } },
        { "PP_Cave_Tube_15", new[] { new Vector3(0f, 0f, 2.1f), pZ, new Vector3(-3.1f, 0.3f, -34.4f), mZ } },
        { "PP_Cave_Tube_12", new[] { new Vector3(-0.3f, -0.1f, 1.7f), pZ } },
        { "PP_Cave_Tube_03", new[] { new Vector3(0.5f, -0.1f, 2.0f), pZ, new Vector3(21.5f, 0.4f, -29.2f), pX } },
        { "PP_Cave_Tube_05", new[] { new Vector3(0f, 0f, 1.0f), pZ, new Vector3(-20.3f, 0.7f, -21.0f), mX } },
        { "PP_Cave_Tube_16", new[] { new Vector3(0.9f, 0f, 0.9f), pZ, new Vector3(-24.6f, -0.5f, -24.9f), mX } },
        { "PP_Cave_Tube_17", new[] { new Vector3(0f, 0f, 0.4f), pZ, new Vector3(-19.7f, 0.8f, -29.5f), mX } },
        { "PP_Cave_Tube_07", new[] { new Vector3(0f, 0f, 0.5f), pZ, new Vector3(0.1f, 0.3f, -41.4f), mZ, new Vector3(-24.4f, 0f, -19.9f), mX } },
        { "PP_Cave_Tube_06", new[] { new Vector3(0f, 0f, 1.7f), pZ, new Vector3(0.1f, 0.3f, -43.5f), mZ, new Vector3(-22.5f, 0.2f, -22.4f), mX, new Vector3(22.8f, -0.1f, -22.3f), pX } },
        { "PP_Cave_Tube_09", new[] { new Vector3(0.1f, 0.2f, 1.2f), pZ, new Vector3(0.2f, -0.1f, -28f), mZ } },
        { "PP_Cave_Tube_20", new[] { new Vector3(0f, 0f, 0.2f), pZ, new Vector3(0.1f, 0.4f, -29f), mZ } },
    };

    static float N3(float x, float y, float z, float s)
    {
        float a = Mathf.PerlinNoise(x * s + 1011f, y * s + 1005f);
        float b = Mathf.PerlinNoise(y * s + 1007f, z * s + 1003f);
        float c = Mathf.PerlinNoise(z * s + 1019f, x * s + 1023f);
        return (a + b + c) / 3f - 0.5f;
    }

    public static string BuildMass()
    {
        var sb = new StringBuilder();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var caps = new List<float[]>();
        var tubesRoot = GameObject.Find("ForestMine").transform.Find("Tubes");
        foreach (Transform t in tubesRoot)
        {
            string nm = t.name.Substring(0, 15);
            var ports = Defs[nm];
            int np = ports.Length / 2;
            var wp = new Vector3[np];
            for (int p = 0; p < np; p++) wp[p] = t.TransformPoint(ports[p * 2]);
            bool hall = nm == "PP_Cave_Tube_09" || nm == "PP_Cave_Tube_20";
            float rad = hall ? 64f : 44f;
            if (np == 1)
            {
                Vector3 n = t.TransformDirection(ports[1]);
                Vector3 end = wp[0] - n * 36f;
                caps.Add(new[] { wp[0].x, wp[0].y, wp[0].z, end.x, end.y, end.z, rad });
            }
            else if (np == 2) caps.Add(new[] { wp[0].x, wp[0].y, wp[0].z, wp[1].x, wp[1].y, wp[1].z, rad });
            else
            {
                Vector3 c = Vector3.zero; foreach (var w in wp) c += w; c /= np;
                foreach (var w in wp) caps.Add(new[] { c.x, c.y, c.z, w.x, w.y, w.z, rad });
            }
        }
        float pivotY = PivotY;
        caps.Add(new[] { -505f, pivotY, 212f, -222f, pivotY, 212f, 50f });

        bool hasExit = false; float exX = 0f, exZ = 0f;
        foreach (Transform t in tubesRoot)
        {
            if (!t.name.EndsWith("_exit")) continue;
            var ports = Defs[t.name.Substring(0, 15)];
            Vector3 ep = t.TransformPoint(ports[2]);
            hasExit = true; exX = ep.x; exZ = ep.z;
        }
        ExitX = exX; ExitZ = exZ;
        float frontSlope = FrontSlope, frontZ = FrontZ, holeR = HoleRadius;

        System.Func<float, float, float, float> massD = (x, y, z) =>
        {
            float best = 1e9f;
            for (int i = 0; i < caps.Count; i++)
            {
                var c = caps[i];
                Vector3 a = new Vector3(c[0], c[1] * 1.25f, c[2]), b = new Vector3(c[3], c[4] * 1.25f, c[5]);
                Vector3 p = new Vector3(x, y * 1.25f, z);
                Vector3 ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                float d = (p - (a + ab * t)).magnitude - c[6];
                if (d < best) best = d;
            }
            best += 5f * N3(x, y, z, 0.025f) * 2f + 2.5f * N3(x, y, z, 0.07f) * 2f + 1.0f * N3(x, y, z, 0.16f) * 2f;
            best = Mathf.Max(best, (Fy - 6f) - y);
            float sideT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 65f, Mathf.Abs(x - Xm)));
            float slopeX = Mathf.Lerp(frontSlope, 0.12f, sideT);
            float frontX = Mathf.Lerp(frontZ, 172.5f, sideT);
            float front = frontX + slopeX * (y - Fy) + 5f * N3(x, y * 0.3f, 0f, 0.06f) * 2f * (1f - sideT * 0.6f) - z;
            best = Mathf.Max(best, front);
            if (hasExit)
            {
                float back = z - (exZ + 10f - 0.6f * (y - Fy) + 4f * N3(x, y * 0.3f, 0f, 0.07f) * 2f * 0f);
                best = Mathf.Max(best, back);
                Vector3 xa = new Vector3(exX, pivotY * 1.35f, exZ - 60f), xb = new Vector3(exX, pivotY * 1.35f, exZ - 5f);
                Vector3 xp = new Vector3(x, y * 1.35f, z);
                Vector3 xab = xb - xa; float xt = Mathf.Clamp01(Vector3.Dot(xp - xa, xab) / xab.sqrMagnitude);
                float xhole = Mathf.Max((xp - (xa + xab * xt)).magnitude - 21f, (Fy - 0.4f) - y);
                best = Mathf.Max(best, -xhole);
                float xslab = Mathf.Max(Mathf.Abs(x - exX) - 24f, Mathf.Max(Mathf.Abs(y - (Fy - 3.15f)) - 3f, Mathf.Abs(z - (exZ + 6f)) - 20f));
                if (xslab < best) best = xslab;
            }
            for (int ei = 0; ei < EntranceX.Length; ei++)
            {
                float ex = EntranceX[ei], er = EntranceR[ei];
                Vector3 pa = new Vector3(ex, pivotY * 1.35f, 150f), pb = new Vector3(ex, pivotY * 1.35f, EntranceEndZ[ei]);
                Vector3 pp = new Vector3(x, y * 1.35f, z);
                Vector3 pab = pb - pa; float ht = Mathf.Clamp01(Vector3.Dot(pp - pa, pab) / pab.sqrMagnitude);
                float hole = (pp - (pa + pab * ht)).magnitude - er;
                hole = Mathf.Max(hole, (Fy - 0.4f) - y);
                best = Mathf.Max(best, -hole);
            }
            for (int ei = 0; ei < EntranceX.Length; ei++)
            {
                float ex = EntranceX[ei];
                float slab = Mathf.Max(Mathf.Abs(x - ex) - (EntranceR[ei] + 3f), Mathf.Max(Mathf.Abs(y - (Fy - 3.15f)) - 3f, Mathf.Abs(z - 178f) - 13f));
                if (slab < best) best = slab;
            }
            return best;
        };

        float cs = 3.0f;
        Vector3 org = new Vector3(-565f, Fy - 9f, 160f);
        int nx = Mathf.CeilToInt(370f / cs), ny = Mathf.CeilToInt(96f / cs), nz = Mathf.CeilToInt(500f / cs);
        int sx = nx + 1, sy = ny + 1, sz = nz + 1;
        float[] F = new float[sx * sy * sz];
        for (int k = 0; k < sz; k++)
            for (int j = 0; j < sy; j++)
                for (int i = 0; i < sx; i++)
                    F[(k * sy + j) * sx + i] = massD(org.x + i * cs, org.y + j * cs, org.z + k * cs);

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
        var mv = new List<Vector3>(); var mn = new List<Vector3>(); var mu = new List<Vector2>(); var mi = new List<int>();
        float e0 = 0.8f; int flipped = 0;
        for (int t = 0; t < triCount; t++)
        {
            Vector3 a = verts[tri[t * 3]], b = verts[tri[t * 3 + 1]], c = verts[tri[t * 3 + 2]];
            Vector3 cr = Vector3.Cross(b - a, c - a);
            if (cr.sqrMagnitude < 1e-6f) continue;
            Vector3 cen = (a + b + c) / 3f;
            Vector3 grad = new Vector3(massD(cen.x + e0, cen.y, cen.z) - massD(cen.x - e0, cen.y, cen.z), massD(cen.x, cen.y + e0, cen.z) - massD(cen.x, cen.y - e0, cen.z), massD(cen.x, cen.y, cen.z + e0) - massD(cen.x, cen.y, cen.z - e0));
            if (Vector3.Dot(cr, grad) < 0f) { Vector3 tmp = b; b = c; c = tmp; cr = -cr; flipped++; }
            Vector3 nrm = cr.normalized;
            bool apron = (Mathf.Abs(cen.x - EntranceX[0]) < 25f && cen.z > 160f && cen.z < 195f && nrm.y > 0.6f && cen.y < Fy + 0.6f)
                         || (hasExit && Mathf.Abs(cen.x - exX) < 25f && cen.z > exZ - 12f && cen.z < exZ + 30f && nrm.y > 0.6f && cen.y < Fy + 0.6f);
            Vector2 uv = apron ? uvDirt : (nrm.y > 0.86f ? uvGrass : uvGrey);
            int bi = mv.Count; mv.Add(a); mv.Add(b); mv.Add(c); mn.Add(nrm); mn.Add(nrm); mn.Add(nrm); mu.Add(uv); mu.Add(uv); mu.Add(uv); mi.Add(bi); mi.Add(bi + 1); mi.Add(bi + 2);
        }
        var oldM = GameObject.Find("ForestMineMass");
        Mesh m;
        GameObject go;
        if (oldM != null) { go = oldM; m = go.GetComponent<MeshFilter>().sharedMesh; m.Clear(); }
        else
        {
            go = new GameObject("ForestMineMass"); go.transform.SetParent(GameObject.Find("ForestMine").transform);
            m = new Mesh { name = "ForestMineMass" };
            AssetDatabase.CreateAsset(m, "Assets/Scenes/MiningMapAssets/ForestMineMass.asset");
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PurePoly/Mining_Pack/Prefabs/Cave/PP_Cave_Tube_04.prefab").GetComponent<MeshRenderer>().sharedMaterial;
            go.AddComponent<MeshCollider>();
        }
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(mv); m.SetNormals(mn); m.SetUVs(0, mu); m.SetTriangles(mi, 0); m.RecalculateBounds();
        EditorUtility.SetDirty(m);
        var mc = go.GetComponent<MeshCollider>(); mc.sharedMesh = null; mc.sharedMesh = m;
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        sb.AppendLine("mass tris=" + (mi.Count / 3) + " flipped=" + flipped + " t=" + sw.ElapsedMilliseconds + "ms");
        return sb.ToString();
    }
}
#endif
