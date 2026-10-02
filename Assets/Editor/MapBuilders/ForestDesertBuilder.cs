#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using CraftingSystem;
using UnityEditor;
using UnityEngine;

public static class ForestDesertBuilder
{
    const string AssetDir = "Assets/Scenes/MiningMapAssets/Desert";
    const string TileDir = AssetDir + "/DesertTiles";

    public static float X0 = -505f, Step = 23.0f, BaseY = 1.4f;
    public static int Cols = 14, Rows = 12;
    public static float Z0 = 604f;

    // oil field centers (world x,z)
    public static Vector2[] Fields = { new Vector2(-405f, 700f), new Vector2(-290f, 735f), new Vector2(-455f, 805f), new Vector2(-250f, 830f) };

    static Dictionary<string, string> _paths;
    static Material _desertMat;

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

    static float Sm(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    static float FieldFlat(float x, float z)
    {
        float d = 1e9f;
        foreach (var f in Fields) d = Mathf.Min(d, Vector2.Distance(new Vector2(x, z), f));
        return Sm((d - 16f) / 14f);
    }

    // dune height (>= 0) at world position
    public static float Dune(float x, float z)
    {
        float f = Sm((z - 598f) / 45f) * FieldFlat(x, z);
        if (f <= 0f) return 0f;
        float X = x + 900f, Z = z + 300f;
        float warp = Mathf.PerlinNoise(X * 0.010f, Z * 0.010f) * 5f;
        float wave = Mathf.Sin((X * 0.55f + Z * 0.83f) * 0.040f + warp);
        float crest = Mathf.Pow(wave * 0.5f + 0.5f, 1.7f) * 4.6f;
        float mound = Mathf.PerlinNoise(X * 0.006f + 7f, Z * 0.006f + 3f) * 5.5f;
        float ripple = Mathf.PerlinNoise(X * 0.11f, Z * 0.11f) * 0.45f;
        return (crest * 1.9f + mound * 1.3f + ripple) * f;
    }

    static Material EnsureMaterials(out Material oil)
    {
        _desertMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDir + "/PP_Desert_Material.mat");
        oil = AssetDatabase.LoadAssetAtPath<Material>(AssetDir + "/PP_Oil_Material.mat");
        if (oil == null)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PurePoly/Mining_Pack/Prefabs/Environment/PP_Ground_01.prefab").GetComponent<MeshRenderer>().sharedMaterial;
            oil = new Material(src) { name = "PP_Oil_Material" };
            oil.SetColor("_BaseColor", new Color(0.04f, 0.035f, 0.035f, 1f));
            oil.SetFloat("_Smoothness", 0.92f);
            oil.SetFloat("_Metallic", 0.25f);
            AssetDatabase.CreateAsset(oil, AssetDir + "/PP_Oil_Material.mat");
        }
        return _desertMat;
    }

    static void ApplyDesert(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = _desertMat;
            r.sharedMaterials = mats;
        }
    }

    static bool Ground(float x, float z, out float y, out Vector3 normal)
    {
        y = 0; normal = Vector3.up;
        float best = float.MaxValue; bool ok = false;
        foreach (var h in Physics.RaycastAll(new Vector3(x, 120f, z), Vector3.down, 200f))
            if (h.collider.name.StartsWith("PP_Ground") && h.distance < best) { best = h.distance; y = h.point.y; normal = h.normal; ok = true; }
        return ok;
    }

    static Transform Group(Transform root, string name)
    {
        var t = root.Find(name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
        var g = new GameObject(name); g.transform.SetParent(root);
        return g.transform;
    }

    static GameObject Put(string prefab, Transform parent, float x, float z, float scale, float sink, float tilt, bool desertMat, System.Random rng)
    {
        var pf = P(prefab);
        if (pf == null) return null;
        if (!Ground(x, z, out float y, out Vector3 n)) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        Vector3 nn = Vector3.Lerp(Vector3.up, n, tilt).normalized;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, nn) * Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
        go.transform.localScale = Vector3.one * scale;
        go.transform.position = new Vector3(x, y - sink, z);
        if (desertMat) ApplyDesert(go);
        return go;
    }

    public static string Build(int seed)
    {
        var sb = new StringBuilder();
        var rng = new System.Random(seed);
        Material oilMat;
        EnsureMaterials(out oilMat);
        if (!AssetDatabase.IsValidFolder(TileDir)) AssetDatabase.CreateFolder(AssetDir, "DesertTiles");

        var old = GameObject.Find("ForestDesert"); if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("ForestDesert").transform;
        var gTiles = Group(root, "Tiles"); var gWalls = Group(root, "Walls"); var gRocks = Group(root, "Rocks");
        var gDeco = Group(root, "Decor"); var gOil = Group(root, "OilFields"); var gRuins = Group(root, "Ruins"); var gRail = Group(root, "Rails");

        // ---------- ground tiles ----------
        string[] groundNames = { "PP_Ground_01", "PP_Ground_02", "PP_Ground_03", "PP_Ground_04" };
        string[] pathNames = { "PP_Ground_Path_01", "PP_Ground_Path_02", "PP_Ground_Path_03", "PP_Ground_Path_04" };
        int count = 0;
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                string name = groundNames[rng.Next(4)];
                var pf = P(name);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, gTiles);
                inst.transform.position = new Vector3(X0 + c * Step, BaseY + (((c + r) & 1) == 0 ? 0f : 0.01f), Z0 + r * Step);
                float yaw = 90f * rng.Next(4);
                inst.transform.rotation = Quaternion.Euler(0, yaw, 0);
                ApplyDesert(inst);

                var mf = inst.GetComponent<MeshFilter>();
                Mesh src = pf.GetComponent<MeshFilter>().sharedMesh;
                Mesh m = Object.Instantiate(src);
                Vector3[] v = m.vertices; Vector3[] nr = m.normals; Vector2[] uv = m.uv;
                Quaternion rot = inst.transform.rotation, inv = Quaternion.Inverse(rot);
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 wp = inst.transform.TransformPoint(v[i]);
                    float h = Dune(wp.x, wp.z);
                    v[i].y += h;
                    float gx = Dune(wp.x + 0.6f, wp.z) - Dune(wp.x - 0.6f, wp.z);
                    float gz = Dune(wp.x, wp.z + 0.6f) - Dune(wp.x, wp.z - 0.6f);
                    Vector3 nw = rot * nr[i];
                    nw = new Vector3(nw.x - gx * 0.8f * nw.y, nw.y, nw.z - gz * 0.8f * nw.y).normalized;
                    nr[i] = inv * nw;
                    // sand colour ramp along one palette row: light crest -> sand -> orange -> brown
                    float n1 = Mathf.PerlinNoise((wp.x + 900f) * 0.02f, (wp.z + 300f) * 0.02f);
                    float n2 = Mathf.PerlinNoise((wp.x + 100f) * 0.07f, (wp.z + 700f) * 0.07f);
                    float tcol = Mathf.Clamp01(h / 8f) * 0.55f + (n1 - 0.5f) * 0.9f + (n2 - 0.5f) * 0.25f; // -.. +
                    float u = tcol > 0.28f ? 0.518f : tcol > -0.12f ? 0.643f : tcol > -0.34f ? 0.768f : 0.893f;
                    if (Mathf.Abs(uv[i].x - 0.89f) < 0.02f && Mathf.Abs(uv[i].y - 0.49f) < 0.02f) uv[i] = new Vector2(u, 0.580f);
                }
                m.vertices = v; m.normals = nr; m.uv = uv; m.RecalculateBounds();
                count++;
                m.name = "Dune_" + count.ToString("D3");
                AssetDatabase.CreateAsset(m, TileDir + "/Dune_" + count.ToString("D3") + ".asset");
                mf.sharedMesh = m;
                var mc = inst.GetComponent<MeshCollider>(); if (mc != null) mc.sharedMesh = m;
            }
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        sb.AppendLine("tiles=" + count);

        float minX = X0 - 12f, maxX = X0 + (Cols - 1) * Step + 12f, minZ = Z0 - 12f, maxZ = Z0 + (Rows - 1) * Step + 12f;

        // ---------- canyon walls ----------
        string[] mountains = { "PP_Mountain_01", "PP_Mountain_02", "PP_Mountain_03" };
        int wallCount = 0;
        for (float z = minZ + 20f; z <= maxZ + 40f; z += 34f)
        {
            foreach (float x in new[] { minX - 22f, maxX + 22f })
            {
                var o = PlaceWall(mountains[rng.Next(3)], gWalls, x, z, 90f + (float)(rng.NextDouble() * 20 - 10), 1.6f + (float)rng.NextDouble() * 0.5f, rng);
                if (o != null) wallCount++;
            }
        }
        for (float x = minX - 40f; x <= maxX + 40f; x += 34f)
        {
            var o = PlaceWall(mountains[rng.Next(3)], gWalls, x, maxZ + 24f, (float)(rng.NextDouble() * 20 - 10), 1.6f + (float)rng.NextDouble() * 0.5f, rng);
            if (o != null) wallCount++;
        }
        sb.AppendLine("wall pieces=" + wallCount);

        // ---------- mesas / rock outcrops ----------
        string[] plateaus = { "PP_Rock_Plateau_01", "PP_Rock_Plateau_02", "PP_Rock_Plateau_03", "PP_Rock_Plateau_04", "PP_Rock_Plateau_06", "PP_Rock_Plateau_07" };
        int mesas = 0, tries = 0;
        var taken = new List<Vector3>();
        foreach (var f in Fields) taken.Add(new Vector3(f.x, f.y, 34f));
        taken.Add(new Vector3(ForestMineBuilder.ExitX, ForestMineBuilder.ExitZ + 30f, 40f));
        System.Func<float, float, float, bool> free = (x, z, rad) => { foreach (var t in taken) if (Vector2.Distance(new Vector2(x, z), new Vector2(t.x, t.y)) < t.z + rad) return false; return true; };
        while (mesas < 6 && tries < 300)
        {
            tries++;
            float x = Mathf.Lerp(minX + 40f, maxX - 40f, (float)rng.NextDouble()), z = Mathf.Lerp(minZ + 80f, maxZ - 40f, (float)rng.NextDouble());
            if (!free(x, z, 30f)) continue;
            var o = Put(plateaus[mesas % plateaus.Length], gRocks, x, z, 0.8f + (float)rng.NextDouble() * 0.5f, 2.5f, 0.1f, true, rng);
            if (o == null) continue;
            taken.Add(new Vector3(x, z, 30f)); mesas++;
        }
        sb.AppendLine("mesas=" + mesas);

        // smaller rocks, cacti, dead trees, dry grass
        var rockNames = new List<string>();
        for (int i = 1; i <= 7; i++) rockNames.Add("PP_Rock_" + i.ToString("D2"));
        for (int i = 1; i <= 5; i++) rockNames.Add("PP_Rock_Brown_" + i.ToString("D2"));
        for (int i = 1; i <= 5; i++) rockNames.Add("PP_Rock_Pile_" + i.ToString("D2"));
        int rockCount = 0, nc = 0, nt = 0, ng = 0;
        for (int i = 0; i < 110; i++)
        {
            float x = Mathf.Lerp(minX + 10f, maxX - 10f, (float)rng.NextDouble()), z = Mathf.Lerp(minZ + 55f, maxZ - 10f, (float)rng.NextDouble());
            if (!free(x, z, 6f)) continue;
            var o = Put(rockNames[rng.Next(rockNames.Count)], gRocks, x, z, 0.7f + (float)rng.NextDouble() * 1.6f, 0.3f, 0.6f, true, rng);
            if (o != null) rockCount++;
        }
        for (int i = 0; i < 95; i++)
        {
            float x = Mathf.Lerp(minX + 10f, maxX - 10f, (float)rng.NextDouble()), z = Mathf.Lerp(minZ + 55f, maxZ - 10f, (float)rng.NextDouble());
            if (!free(x, z, 4f)) continue;
            string cn = "PP_Cactus_" + (1 + rng.Next(23)).ToString("D2");
            var o = Put(cn, gDeco, x, z, 1.8f + (float)rng.NextDouble() * 2.2f, 0.1f, 0.3f, false, rng);
            if (o != null) nc++;
        }
        for (int i = 0; i < 26; i++)
        {
            float x = Mathf.Lerp(minX + 10f, maxX - 10f, (float)rng.NextDouble()), z = Mathf.Lerp(minZ + 70f, maxZ - 10f, (float)rng.NextDouble());
            if (!free(x, z, 8f)) continue;
            var o = Put("PP_Leafless_Tree_" + (1 + rng.Next(11)).ToString("D2"), gDeco, x, z, 0.8f + (float)rng.NextDouble() * 0.7f, 0.2f, 0.3f, false, rng);
            if (o != null) nt++;
        }
        for (int c = 0; c < 70; c++)
        {
            float cx = Mathf.Lerp(minX + 10f, maxX - 10f, (float)rng.NextDouble()), cz = Mathf.Lerp(minZ + 55f, maxZ - 10f, (float)rng.NextDouble());
            int k = 2 + rng.Next(4);
            for (int j = 0; j < k; j++)
            {
                float x = cx + (float)(rng.NextDouble() * 2 - 1) * 4f, z = cz + (float)(rng.NextDouble() * 2 - 1) * 4f;
                string gn = rng.Next(2) == 0 ? "PP_Grass_0" + (1 + rng.Next(6)) : "PP_Grass_Single_" + (1 + rng.Next(10)).ToString("D2");
                var o = Put(gn, gDeco, x, z, 1.6f + (float)rng.NextDouble() * 1.4f, 0.05f, 0.5f, true, rng);
                if (o != null) ng++;
            }
        }
        sb.AppendLine($"rocks={rockCount} cacti={nc} deadTrees={nt} dryGrass={ng}");

        // ---------- oil fields ----------
        var catalog = ItemCatalog.GetOrCreate();
        ItemData oilItem = catalog.GetItem(ItemIds.Oil);
        int nodes = 0;
        for (int fi = 0; fi < Fields.Length; fi++)
        {
            var field = new GameObject("OilField_" + (fi + 1)); field.transform.SetParent(gOil);
            Vector2 fc = Fields[fi];
            field.transform.position = new Vector3(fc.x, BaseY, fc.y);
            int pools = 3 + rng.Next(3);
            for (int p = 0; p < pools; p++)
            {
                float a = (p / (float)pools) * 6.283f + (float)rng.NextDouble() * 0.6f;
                float rr = p == 0 ? 0f : 6f + (float)rng.NextDouble() * 4f;
                float px = fc.x + Mathf.Cos(a) * rr, pz = fc.y + Mathf.Sin(a) * rr;
                if (!Ground(px, pz, out float py, out Vector3 pn)) continue;
                float radius = 2.4f + (float)rng.NextDouble() * 1.4f;
                var pool = MakePool("OilNode_" + (fi + 1) + "_" + (p + 1), field.transform, new Vector3(px, py + 0.04f, pz), radius, oilMat, rng);
                var col = pool.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = radius * 0.75f + 1.2f; col.center = new Vector3(0, 0.6f, 0);
                pool.layer = 4;
                var node = pool.AddComponent<SpecialResourceNode>();
                node.Configure("기름", oilItem, 5, 0, 3, 5, 180f, 360f, ItemIds.CopperHandPump, 1.5f, null, 0f, null, 0f);
                EditorUtility.SetDirty(node);
                nodes++;
            }
            // props around the field: barrels, crates, buckets, log fence, derrick
            string[] barrels = { "PP_Barrel_01", "PP_Barrel_02", "PP_Barrel_03", "PP_Barrel_04" };
            for (int i = 0; i < 9; i++)
            {
                float a = (float)rng.NextDouble() * 6.283f, rr = 11f + (float)rng.NextDouble() * 7f;
                float x = fc.x + Mathf.Cos(a) * rr, z = fc.y + Mathf.Sin(a) * rr;
                string nm = rng.Next(3) == 0 ? "PP_Crate_Wooden_0" + (1 + rng.Next(3)) : barrels[rng.Next(4)];
                Put(nm, gRuins, x, z, 2.6f + (float)rng.NextDouble() * 0.8f, 0.05f, 0.4f, false, rng);
            }
            BuildDerrick(gRuins, fc.x + 14f, fc.y - 6f, rng);
            BuildShack(gRuins, fc.x - 16f, fc.y + 10f, (float)rng.NextDouble() * 360f, fi % 2 == 0, rng);
        }
        sb.AppendLine("oil nodes=" + nodes);

        // ---------- rail line from the mine exit into the first oil field ----------
        var railPf = P("PP_Rail_Straight_Long");
        int nrl = 0;
        if (railPf != null)
        {
            var rb = railPf.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            bool alongX = rb.size.x > rb.size.z; float len = alongX ? rb.size.x : rb.size.z;
            Vector3 a = new Vector3(ForestMineBuilder.ExitX, 0, ForestMineBuilder.ExitZ - 6f);
            Vector3 b = new Vector3(Fields[0].x + 8f, 0, Fields[0].y - 14f);
            // two legs: north then west/east
            Vector3 mid = new Vector3(a.x, 0, b.z);
            nrl += LayRail(railPf, gRail, a, mid, len * 1.3f, alongX, 1.3f);
            nrl += LayRail(railPf, gRail, mid, b, len * 1.3f, alongX, 1.3f);
        }
        sb.AppendLine("rail pieces=" + nrl);
        Physics.SyncTransforms();
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    static int LayRail(GameObject pf, Transform parent, Vector3 a, Vector3 b, float segLen, bool alongX, float scale)
    {
        int n = Mathf.Max(1, Mathf.FloorToInt(Vector3.Distance(a, b) / segLen));
        int placed = 0;
        Vector3 dir = (b - a).normalized;
        for (int i = 0; i < n; i++)
        {
            Vector3 p0 = a + dir * (i * segLen), p1 = a + dir * ((i + 1) * segLen);
            if (!Ground(p0.x, p0.z, out float y0, out _) || !Ground(p1.x, p1.z, out float y1, out _)) continue;
            Vector3 q0 = new Vector3(p0.x, y0, p0.z), q1 = new Vector3(p1.x, y1, p1.z);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            Quaternion q = Quaternion.LookRotation((q1 - q0).normalized, Vector3.up);
            if (alongX) q *= Quaternion.Euler(0, 90f, 0);
            go.transform.rotation = q; go.transform.localScale = Vector3.one * scale;
            go.transform.position = (q0 + q1) * 0.5f + Vector3.up * 0.05f;
            placed++;
        }
        return placed;
    }

    static GameObject PlaceWall(string prefab, Transform parent, float x, float z, float yaw, float scale, System.Random rng)
    {
        var pf = P(prefab); if (pf == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        go.transform.localScale = Vector3.one * scale;
        go.transform.position = new Vector3(x, BaseY - 0.5f, z);
        ApplyDesert(go);
        return go;
    }

    static GameObject MakePool(string name, Transform parent, Vector3 pos, float radius, Material mat, System.Random rng)
    {
        var go = new GameObject(name); go.transform.SetParent(parent);
        go.transform.position = pos;
        int seg = 22;
        var verts = new List<Vector3> { Vector3.zero };
        var uvs = new List<Vector2> { new Vector2(0.02f, 0.02f) };
        float ph1 = (float)rng.NextDouble() * 6.28f, ph2 = (float)rng.NextDouble() * 6.28f;
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * 6.283f;
            float rr = radius * (1f + 0.22f * Mathf.Sin(3f * a + ph1) + 0.12f * Mathf.Sin(5f * a + ph2));
            verts.Add(new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr)); uvs.Add(new Vector2(0.02f, 0.02f));
        }
        var tris = new List<int>();
        for (int i = 0; i < seg; i++) { tris.Add(0); tris.Add(1 + (i + 1) % seg); tris.Add(1 + i); }
        var m = new Mesh { name = name };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        var nrm = new Vector3[verts.Count]; for (int i = 0; i < nrm.Length; i++) nrm[i] = Vector3.up;
        m.normals = nrm; m.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static void BuildDerrick(Transform parent, float x, float z, System.Random rng)
    {
        if (!Ground(x, z, out float y, out _)) return;
        var root = new GameObject("Derrick"); root.transform.SetParent(parent); root.transform.position = new Vector3(x, y, z);
        float half = 4.5f, height = 3.0f; // beam prefab is 6 tall, scaled
        var leg = P("PP_Beam_Wooden_05") ?? P("PP_Beam_Wooden_01");
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                if (leg == null) break;
                var b = (GameObject)PrefabUtility.InstantiatePrefab(leg, root.transform);
                b.transform.localScale = Vector3.one * 3.0f;
                b.transform.position = new Vector3(x + sx * half, y, z + sz * half);
                b.transform.rotation = Quaternion.Euler(-sz * 4f, 0, sx * 4f);
            }
        var cross = P("PP_Beam_Wooden_Short_01");
        if (cross != null)
            for (int lvl = 1; lvl <= 2; lvl++)
                foreach (var side in new[] { 0, 1 })
                {
                    var b = (GameObject)PrefabUtility.InstantiatePrefab(cross, root.transform);
                    b.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
                    float yy = y + lvl * 5.5f;
                    b.transform.rotation = Quaternion.Euler(90, side * 90f, 0);
                    b.transform.position = new Vector3(x, yy, z + (side == 0 ? half * (1.1f - lvl * 0.08f) : 0f)) + (side == 1 ? new Vector3(half * (1.1f - lvl * 0.08f), 0, 0) : Vector3.zero);
                }
        var top = P("PP_Roof_Top_02") ?? P("PP_Roof_Top_01");
        if (top != null) { var t = (GameObject)PrefabUtility.InstantiatePrefab(top, root.transform); t.transform.localScale = Vector3.one * 3f; t.transform.position = new Vector3(x, y + 17f, z); }
        var barrel = P("PP_Barrel_01");
        if (barrel != null) for (int i = 0; i < 3; i++) { var bb = (GameObject)PrefabUtility.InstantiatePrefab(barrel, root.transform); bb.transform.localScale = Vector3.one * 2.8f; bb.transform.position = new Vector3(x + (i - 1) * 2.2f, y, z + 7.5f); }
    }

    static void BuildShack(Transform parent, float x, float z, float yaw, bool withRoof, System.Random rng)
    {
        if (!Ground(x, z, out float y, out _)) return;
        var root = new GameObject("RuinedShack"); root.transform.SetParent(parent);
        root.transform.position = new Vector3(x, y, z); root.transform.rotation = Quaternion.Euler(0, yaw, 0);
        float sc = 3.4f, w = 3f * sc;
        string[] walls = { "PP_Wooden_Wall_01", "PP_Wooden_Wall_02", "PP_Wooden_Wall_03", "PP_Wooden_Wall_Window_01", "PP_Wooden_Wall_Window_02" };
        var floor = P("PP_Wooden_Floor_01");
        if (floor != null) { var f = (GameObject)PrefabUtility.InstantiatePrefab(floor, root.transform); f.transform.localScale = Vector3.one * sc; f.transform.localPosition = Vector3.zero; f.transform.localRotation = Quaternion.identity; }
        Vector3[] pos = { new Vector3(0, 0, w / 2f), new Vector3(0, 0, -w / 2f), new Vector3(w / 2f, 0, 0), new Vector3(-w / 2f, 0, 0) };
        float[] rot = { 0f, 180f, 90f, -90f };
        for (int i = 0; i < 4; i++)
        {
            if (rng.NextDouble() < 0.28) continue; // ruined: some walls are missing
            var pf = P(walls[rng.Next(walls.Length)]); if (pf == null) continue;
            var wobj = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
            wobj.transform.localScale = Vector3.one * sc; wobj.transform.localPosition = pos[i]; wobj.transform.localRotation = Quaternion.Euler((float)(rng.NextDouble() * 6 - 3), rot[i], (float)(rng.NextDouble() * 6 - 3));
        }
        if (withRoof)
        {
            var roof = P("PP_Blacksmith_Roof_05") ?? P("PP_Blacksmith_Roof_02");
            if (roof != null) { var r = (GameObject)PrefabUtility.InstantiatePrefab(roof, root.transform); r.transform.localScale = Vector3.one * sc; r.transform.localPosition = new Vector3(0, w * 0.95f, 0); r.transform.localRotation = Quaternion.Euler(8f, 10f, 5f); }
        }
        var table = P("PP_Wooden_Table_01");
        if (table != null) { var t = (GameObject)PrefabUtility.InstantiatePrefab(table, root.transform); t.transform.localScale = Vector3.one * 3f; t.transform.localPosition = new Vector3(0.5f, 0.2f, -0.5f); }
    }
}
#endif
