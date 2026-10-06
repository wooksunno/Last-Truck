#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class WalkCell
{
    public Vector3 p;
    public int comp = -1;
    public float width;
}

public static class TunnelFix
{
    static readonly string[] Groups = { "Cave", "Cave Props", "Terrain_Stone", "Stones&Rocks", "Vegetation", "Crystals&Ores&Veins", "Coins", "Props", "Mushrooms", "Rails&Mine Carts", "Runes", "Bridges", "Stalactite&Stalagmite&Stalagnate", "Lighting", "FX" };

    static bool IsStructName(string n)
    {
        return n.StartsWith("PP_Cave") || n.StartsWith("PP_Mountain") || n.StartsWith("PP_Stone_Cave") || n.StartsWith("PP_Stone_Ground") || n.StartsWith("PP_Cliff") || n.StartsWith("PP_Rock_Plateau") || n.StartsWith("PP_Ground");
    }

    static bool IsClutterName(string n)
    {
        return n.StartsWith("PP_Stalag") || n.StartsWith("PP_Crystal") || n.StartsWith("PP_Stone_Crystal") || n.StartsWith("PP_Stones_Crystals") || n.StartsWith("PP_Rock_Veins") || n.StartsWith("PP_Rock_Gem")
            || n.StartsWith("PP_Mushroom") || n.StartsWith("PP_Stone_Veins") || n.StartsWith("PP_Gemstone") || n.StartsWith("PP_Stone_Plain") || n.StartsWith("PP_Rock_0") || n.StartsWith("PP_Rock_Brown") || n.StartsWith("PP_Rock_Moss")
            || n.StartsWith("PP_Rock_Pile") || n.StartsWith("PP_Rock_Column") || n.StartsWith("PP_Pebbles") || n.StartsWith("PP_Treasure") || n.StartsWith("PP_Coin");
    }

    public static List<WalkCell> ScanCells(float xMin, float xMax, float zMin, float zMax, float cs = 3f)
    {
        Physics.SyncTransforms();
        var cells = new List<WalkCell>();
        for (float z = zMin + cs * 0.5f; z < zMax; z += cs)
            for (float x = xMin + cs * 0.5f; x < xMax; x += cs)
            {
                var hits = Physics.RaycastAll(new Vector3(x, 30f, z), Vector3.down, 70f);
                foreach (var h in hits)
                {
                    if (h.collider.isTrigger || h.normal.y < 0.55f) continue;
                    string n = h.collider.name;
                    if (!(n.StartsWith("PP_Cave") || n.StartsWith("PP_Stone_Ground") || n.StartsWith("PP_Rock_Plateau"))) continue;
                    Vector3 o = h.point + Vector3.up * 0.15f; RaycastHit up;
                    if (Physics.SphereCast(o, 0.5f, Vector3.up, out up, 4.5f, ~0, QueryTriggerInteraction.Ignore) && IsStructName(up.collider.name)) continue;
                    if (!Physics.Raycast(o, Vector3.up, 60f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    cells.Add(new WalkCell { p = h.point });
                }
            }
        return cells;
    }

    public static void Components(List<WalkCell> cells, float cs = 3f)
    {
        var map = new Dictionary<long, List<int>>();
        for (int i = 0; i < cells.Count; i++)
        {
            long key = ((long)Mathf.RoundToInt(cells[i].p.x / cs) * 100000L) + Mathf.RoundToInt(cells[i].p.z / cs);
            if (!map.ContainsKey(key)) map[key] = new List<int>(); map[key].Add(i);
        }
        int nc = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].comp >= 0) continue;
            var qu = new Queue<int>(); qu.Enqueue(i); cells[i].comp = nc;
            while (qu.Count > 0)
            {
                int c = qu.Dequeue();
                int cx = Mathf.RoundToInt(cells[c].p.x / cs), cz = Mathf.RoundToInt(cells[c].p.z / cs);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        List<int> l; if (!map.TryGetValue(((long)(cx + dx) * 100000L) + (cz + dz), out l)) continue;
                        foreach (int j in l) if (cells[j].comp < 0 && Mathf.Abs(cells[j].p.y - cells[c].p.y) <= 2.1f) { cells[j].comp = nc; qu.Enqueue(j); }
                    }
            }
            nc++;
        }
    }

    public static string ClearClutter(List<WalkCell> cells, float minWidth, int minCompSize, bool apply)
    {
        var sb = new StringBuilder();
        var compSize = new Dictionary<int, int>();
        foreach (var c in cells) { if (!compSize.ContainsKey(c.comp)) compSize[c.comp] = 0; compSize[c.comp]++; }
        var victims = new HashSet<Transform>();
        int narrow = 0, narrowAfterClutter = 0;
        foreach (var c in cells)
        {
            if (compSize[c.comp] < minCompSize) continue;
            float w = Width(c.p);
            c.width = w;
            if (w >= minWidth) continue;
            narrow++;
            var cols = Physics.OverlapSphere(c.p + Vector3.up * 2.2f, 3.2f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in cols)
            {
                string n = col.gameObject.name;
                if (!IsClutterName(n)) continue;
                var t = col.transform;
                while (t.parent != null && System.Array.IndexOf(Groups, t.parent.name) < 0) t = t.parent;
                if (t.parent != null && System.Array.IndexOf(Groups, t.parent.name) >= 0) victims.Add(t);
            }
        }
        sb.AppendLine($"narrow cells={narrow}, clutter objects in them={victims.Count}");
        if (apply)
        {
            foreach (var v in victims) if (v != null) Object.DestroyImmediate(v.gameObject);
            Physics.SyncTransforms();
            foreach (var c in cells)
            {
                if (compSize[c.comp] < minCompSize) continue;
                if (Width(c.p) < minWidth) narrowAfterClutter++;
            }
            sb.AppendLine("narrow cells after clearing=" + narrowAfterClutter);
        }
        return sb.ToString();
    }

    public static float Width(Vector3 p)
    {
        const int dirs = 16; float[] d = new float[dirs];
        for (int a = 0; a < dirs; a++)
        {
            float ang = a * Mathf.PI * 2f / dirs; Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
            RaycastHit h; float dd = 40f;
            if (Physics.Raycast(p + Vector3.up * 1.4f, dir, out h, 40f, ~0, QueryTriggerInteraction.Ignore)) dd = h.distance;
            d[a] = dd;
        }
        float w = 1e9f;
        for (int a = 0; a < dirs / 2; a++) w = Mathf.Min(w, d[a] + d[a + dirs / 2]);
        return w;
    }
}
#endif
