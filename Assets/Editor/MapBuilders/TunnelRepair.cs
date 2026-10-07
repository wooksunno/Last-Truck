#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class TunnelRepairResult
{
    public float vs, X0, Y0, Z0;
    public int nx, ny, nz;
    public bool[,,] str, clut, sky, flooded;
    public int[,] floorJ;
    public List<Vector3Int> holeCells = new List<Vector3Int>();
    public List<Vector3Int> holeNeighbors = new List<Vector3Int>();
    public int voidColumns;
    public Vector3 World(int i, int j, int k) { return new Vector3(X0 + (i + 0.5f) * vs, Y0 + (j + 0.5f) * vs, Z0 + (k + 0.5f) * vs); }
}

public static class TunnelRepair
{
    static readonly string[] StructPrefix = { "PP_Cave_Tube", "PP_Cave_Wall", "PP_Cave_Small", "PP_Cave_Skull", "PP_Mountain", "PP_Stone_Cave", "PP_Stone_Ground", "PP_Cliff", "PP_Rock_Plateau", "PP_Ground", "PP_Stone Wall" };

    static bool IsStruct(string n)
    {
        foreach (var p in StructPrefix) if (n.StartsWith(p)) return true;
        return false;
    }

    public static TunnelRepairResult Analyze(Vector3 min, Vector3 max, Vector3 start, float maxXInterior, float vs = 1.5f)
    {
        Physics.SyncTransforms();
        var R = new TunnelRepairResult();
        R.vs = vs; R.X0 = min.x; R.Y0 = min.y; R.Z0 = min.z;
        R.nx = Mathf.CeilToInt((max.x - min.x) / vs); R.ny = Mathf.CeilToInt((max.y - min.y) / vs); R.nz = Mathf.CeilToInt((max.z - min.z) / vs);
        int nx = R.nx, ny = R.ny, nz = R.nz;
        R.str = new bool[nx, ny, nz]; R.clut = new bool[nx, ny, nz]; R.sky = new bool[nx, ny, nz]; R.flooded = new bool[nx, ny, nz];
        var buf = new Collider[24];
        var cache = new Dictionary<int, bool>();
        float rad = vs * 0.62f;
        for (int k = 0; k < nz; k++)
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int c = Physics.OverlapSphereNonAlloc(R.World(i, j, k), rad, buf, ~0, QueryTriggerInteraction.Ignore);
                    bool s = false, o = false;
                    for (int q = 0; q < c; q++)
                    {
                        int id = buf[q].GetInstanceID(); bool isS;
                        if (!cache.TryGetValue(id, out isS)) { isS = IsStruct(buf[q].gameObject.name); cache[id] = isS; }
                        if (isS) s = true; else o = true;
                    }
                    R.str[i, j, k] = s; R.clut[i, j, k] = o;
                }
        for (int k = 0; k < nz; k++)
            for (int i = 0; i < nx; i++) { bool any = false; for (int j = ny - 1; j >= 0; j--) { if (R.str[i, j, k]) any = true; R.sky[i, j, k] = !any; } }

        int si = Mathf.FloorToInt((start.x - R.X0) / vs), sj = Mathf.FloorToInt((start.y - R.Y0) / vs), sk = Mathf.FloorToInt((start.z - R.Z0) / vs);
        var q2 = new Queue<int>();
        if (!R.str[si, sj, sk] && !R.sky[si, sj, sk]) { R.flooded[si, sj, sk] = true; q2.Enqueue((si * ny + sj) * nz + sk); }
        int[] dx = { 1, -1, 0, 0, 0, 0 }, dy = { 0, 0, 1, -1, 0, 0 }, dz = { 0, 0, 0, 0, 1, -1 };
        int maxI = Mathf.FloorToInt((maxXInterior - R.X0) / vs);
        var bottomCols = new HashSet<int>();
        while (q2.Count > 0)
        {
            int cc = q2.Dequeue(); int k = cc % nz; int j = (cc / nz) % ny; int i = cc / (nz * ny);
            for (int d = 0; d < 6; d++)
            {
                int ni = i + dx[d], nj = j + dy[d], nk = k + dz[d];
                if (nj < 0) { bottomCols.Add(i * nz + k); continue; }
                if (ni < 0 || nk < 0 || ni >= nx || nk >= nz || nj >= ny) { R.holeCells.Add(new Vector3Int(i, j, k)); R.holeNeighbors.Add(new Vector3Int(i, j, k)); continue; }
                if (ni > maxI) continue;
                if (R.str[ni, nj, nk] || R.flooded[ni, nj, nk]) continue;
                if (R.sky[ni, nj, nk]) { R.holeCells.Add(new Vector3Int(i, j, k)); R.holeNeighbors.Add(new Vector3Int(ni, nj, nk)); continue; }
                R.flooded[ni, nj, nk] = true; q2.Enqueue((ni * ny + nj) * nz + nk);
            }
        }
        R.floorJ = new int[nx, nz];
        for (int k = 0; k < nz; k++)
            for (int i = 0; i < nx; i++)
            {
                R.floorJ[i, k] = -1;
                bool bottomHit = bottomCols.Contains(i * nz + k);
                int lowest = -1;
                for (int j = 0; j < ny; j++) if (R.flooded[i, j, k]) { lowest = j; break; }
                if (lowest < 0) continue;
                if (lowest == 0 || !R.str[i, lowest - 1, k]) { if (lowest == 0 || bottomHit) { R.floorJ[i, k] = -2; R.voidColumns++; } else R.floorJ[i, k] = lowest; }
                else R.floorJ[i, k] = lowest;
            }
        return R;
    }

    public static string Summary(TunnelRepairResult R)
    {
        int fl = 0; foreach (var b in R.flooded) if (b) fl++;
        var sb = new StringBuilder();
        sb.AppendLine($"grid {R.nx}x{R.ny}x{R.nz} flooded={fl} holeContacts={R.holeCells.Count} voidColumns={R.voidColumns}");
        System.Func<IEnumerable<Vector3>, string> cluster = pts =>
        {
            var cells = new Dictionary<string, int>();
            foreach (var p in pts) { string key = "(" + (Mathf.Round(p.x / 8f) * 8).ToString("F0") + "," + (Mathf.Round(p.y / 6f) * 6).ToString("F0") + "," + (Mathf.Round(p.z / 8f) * 8).ToString("F0") + ")"; if (!cells.ContainsKey(key)) cells[key] = 0; cells[key]++; }
            var l = new List<string>(cells.Keys); l.Sort((a, b) => cells[b].CompareTo(cells[a]));
            var s = ""; for (int i = 0; i < Mathf.Min(40, l.Count); i++) s += l[i] + "x" + cells[l[i]] + " ";
            return s;
        };
        var hp = new List<Vector3>(); foreach (var c in R.holeCells) hp.Add(R.World(c.x, c.y, c.z));
        sb.AppendLine("hole spots: " + cluster(hp));
        var vp = new List<Vector3>();
        for (int k = 0; k < R.nz; k++) for (int i = 0; i < R.nx; i++) if (R.floorJ[i, k] == -2) vp.Add(R.World(i, 0, k));
        sb.AppendLine("void-floor columns (x,_,z): " + cluster(vp));
        return sb.ToString();
    }
}
#endif
