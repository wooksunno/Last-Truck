#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ForestMineDresser
{
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

    static Transform _tubes;

    static bool CastTube(Vector3 o, Vector3 d, float dist, out RaycastHit best)
    {
        best = default;
        float bd = float.MaxValue; bool found = false;
        foreach (var h in Physics.RaycastAll(o, d, dist))
        {
            if (h.collider.isTrigger || !h.collider.transform.IsChildOf(_tubes)) continue;
            if (h.distance < bd) { bd = h.distance; best = h; found = true; }
        }
        return found;
    }

    static GameObject Spawn(string prefab, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var pf = P(prefab);
        if (pf == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.position = pos; go.transform.rotation = rot; go.transform.localScale = scale;
        return go;
    }

    static Transform Group(Transform root, string name)
    {
        var t = root.Find(name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
        var g = new GameObject(name); g.transform.SetParent(root);
        return g.transform;
    }

    static float MeshMinY(string prefab, out float maxY)
    {
        var pf = P(prefab); maxY = 0f;
        if (pf == null) return 0f;
        var mf = pf.GetComponentInChildren<MeshFilter>();
        if (mf == null) return 0f;
        maxY = mf.sharedMesh.bounds.max.y;
        return mf.sharedMesh.bounds.min.y;
    }

    public static string Dress(int seed)
    {
        var sb = new StringBuilder();
        var rng = new System.Random(seed);
        System.Func<float, float, float> Rr = (a, b) => a + (b - a) * (float)rng.NextDouble();
        var mine = GameObject.Find("ForestMine").transform;
        _tubes = mine.Find("Tubes");
        Physics.SyncTransforms();
        var gTorch = Group(mine, "Torches"); var gSup = Group(mine, "Supports"); var gRail = Group(mine, "Rails");
        var gStal = Group(mine, "Stalactites"); var gCry = Group(mine, "Crystals"); var gFrame = Group(mine, "Frames");
        var gLight = Group(mine, "FillLights"); var gMisc = Group(mine, "Misc");

        string[] torchNames = { "PP_Torch_Standing_01", "PP_Torch_Standing_03", "PP_Torch_Standing_04", "PP_Torch_Standing_06", "PP_Torch_Standing_07" };
        string[] fxNames = { "FX_Fire_Torch_01", "FX_Fire_Torch_02", "FX_Fire_Torch_03", "FX_Fire_Torch_04" };
        string[] supports = { "PP_Mine_Wooden_Support_03", "PP_Mine_Wooden_Support_04", "PP_Mine_Wooden_Support_05" };
        string[] stalactites = new string[10]; for (int i = 0; i < 10; i++) stalactites[i] = "PP_Stalactite_" + (i + 1).ToString("D2");
        string[] stalagmites = new string[8]; for (int i = 0; i < 8; i++) stalagmites[i] = "PP_Stalagmite_" + (i + 1).ToString("D2");
        string[] crystalCols = { "Red", "Blue", "Green", "Gold" };

        int nTorch = 0, nSup = 0, nRail = 0, nStal = 0, nCry = 0, nChest = 0;

        // rail prefab length
        string railName = "PP_Rail_Straight_Long";
        var railPf = P(railName);
        float railLen = 8f; bool railAlongX = false;
        if (railPf != null)
        {
            var rm = railPf.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            railAlongX = rm.size.x > rm.size.z;
            railLen = railAlongX ? rm.size.x : rm.size.z;
        }
        sb.AppendLine($"rail prefab {railName} len={railLen:F1} alongX={railAlongX}");

        foreach (Transform t in _tubes)
        {
            string nm = t.name.Substring(0, 15);
            var ports = ForestMineBuilder.Defs[nm];
            int np = ports.Length / 2;
            var wp = new Vector3[np];
            for (int p = 0; p < np; p++) wp[p] = t.TransformPoint(ports[p * 2]);
            var segs = new List<Vector3[]>();
            if (np == 1) segs.Add(new[] { wp[0], wp[0] - t.TransformDirection(ports[1]) * 34f });
            else if (np == 2) segs.Add(new[] { wp[0], wp[1] });
            else
            {
                Vector3 c = Vector3.zero; foreach (var w in wp) c += w; c /= np;
                foreach (var w in wp) segs.Add(new[] { c, w });
            }
            bool hall = nm == "PP_Cave_Tube_09" || nm == "PP_Cave_Tube_20";
            bool isStub = nm == "PP_Cave_Tube_12";
            float cy = t.position.y;

            foreach (var seg in segs)
            {
                Vector3 a = seg[0], b = seg[1];
                float len = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
                Vector3 dir = new Vector3(b.x - a.x, 0, b.z - a.z).normalized;
                Vector3 lat = Vector3.Cross(Vector3.up, dir);

                // torches on both walls
                float step = hall ? 20f : 17f;
                for (float d = 8f; d < len - 5f; d += step)
                {
                    Vector3 pc = a + dir * d; pc.y = cy;
                    foreach (float sgn in new[] { -1f, 1f })
                    {
                        if (!CastTube(pc, lat * sgn, 45f, out var hw)) continue;
                        Vector3 tp = hw.point - lat * sgn * 1.8f;
                        if (!CastTube(new Vector3(tp.x, cy + 2f, tp.z), Vector3.down, 40f, out var hf)) continue;
                        var torch = Spawn(torchNames[rng.Next(torchNames.Length)], gTorch, hf.point, Quaternion.Euler(0, Rr(0, 360), 0), Vector3.one * 2.4f);
                        if (torch == null) continue;
                        Vector3 tip = hf.point + Vector3.up * 4.9f;
                        var fx = Spawn(fxNames[rng.Next(fxNames.Length)], torch.transform, tip, Quaternion.identity, Vector3.one * 1.8f);
                        var lg = new GameObject("TorchLight"); lg.transform.SetParent(torch.transform); lg.transform.position = tip + Vector3.up * 0.8f;
                        var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.62f, 0.25f); l.intensity = 7f; l.range = 32f; l.shadows = LightShadows.None;
                        nTorch++;
                    }
                }

                // wooden support frames across the tunnel
                if (!isStub)
                {
                    float sstep = hall ? 28f : 24f;
                    for (float d = 12f; d < len - 8f; d += sstep)
                    {
                        Vector3 pc = a + dir * d; pc.y = cy;
                        if (!CastTube(new Vector3(pc.x, cy + 3f, pc.z), Vector3.down, 40f, out var hf)) continue;
                        var s = Spawn(supports[rng.Next(supports.Length)], gSup, hf.point, Quaternion.LookRotation(dir, Vector3.up), new Vector3(2.7f, 2.3f, 2.3f));
                        if (s != null) nSup++;
                    }
                }

                // rails along floor center line (corridor pieces only)
                if (!hall && !isStub && railPf != null && np <= 2)
                {
                    int n = Mathf.Max(1, Mathf.FloorToInt((len - 6f) / railLen));
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 p0 = a + dir * (3f + i * railLen), p1 = a + dir * (3f + (i + 1) * railLen);
                        if (!CastTube(new Vector3(p0.x, cy + 3f, p0.z), Vector3.down, 40f, out var h0)) continue;
                        if (!CastTube(new Vector3(p1.x, cy + 3f, p1.z), Vector3.down, 40f, out var h1)) continue;
                        Vector3 mid = (h0.point + h1.point) * 0.5f;
                        Quaternion q = Quaternion.LookRotation((h1.point - h0.point).normalized, Vector3.up);
                        if (railAlongX) q *= Quaternion.Euler(0, 90f, 0);
                        var r = Spawn(railName, gRail, mid + Vector3.up * 0.05f, q, Vector3.one * 1.3f);
                        if (r != null) nRail++;
                    }
                }

                // stalactites + stalagmites
                int count = hall ? 14 : (isStub ? 4 : 7);
                for (int i = 0; i < count; i++)
                {
                    float d = Rr(2f, Mathf.Max(3f, len - 2f));
                    float off = Rr(-0.55f, 0.55f) * (hall ? 28f : 15f);
                    Vector3 pc = a + dir * d + lat * off; pc.y = cy;
                    if (CastTube(pc, Vector3.up, 40f, out var hc) && rng.NextDouble() < 0.7)
                    {
                        string sn = stalactites[rng.Next(stalactites.Length)];
                        float maxY; float minY = MeshMinY(sn, out maxY);
                        float sc = Rr(1.6f, 3.6f);
                        var o = Spawn(sn, gStal, new Vector3(hc.point.x, hc.point.y - maxY * sc + 0.4f, hc.point.z), Quaternion.Euler(0, Rr(0, 360), 0), Vector3.one * sc);
                        if (o != null) nStal++;
                    }
                    if (CastTube(new Vector3(pc.x, cy + 4f, pc.z), Vector3.down, 40f, out var hg) && rng.NextDouble() < 0.5)
                    {
                        string sn = stalagmites[rng.Next(stalagmites.Length)];
                        float maxY; float minY = MeshMinY(sn, out maxY);
                        float sc = Rr(1.6f, 3.4f);
                        var o = Spawn(sn, gStal, new Vector3(hg.point.x, hg.point.y - minY * sc - 0.1f, hg.point.z), Quaternion.Euler(0, Rr(0, 360), 0), Vector3.one * sc);
                        if (o != null) nStal++;
                    }
                }

                // crystals
                int cc = hall ? 5 : (isStub ? 3 : 2);
                for (int i = 0; i < cc; i++)
                {
                    float d = Rr(3f, Mathf.Max(4f, len - 3f));
                    Vector3 pc = a + dir * d; pc.y = cy;
                    float sgn = rng.Next(2) == 0 ? -1f : 1f;
                    if (!CastTube(pc, lat * sgn, 50f, out var hw)) continue;
                    string col = crystalCols[rng.Next(crystalCols.Length)];
                    string cn = "PP_Crystal_Cluster_0" + (1 + rng.Next(6)) + "_" + col;
                    var cobj = Spawn(cn, gCry, hw.point - hw.normal * 0.4f, Quaternion.FromToRotation(Vector3.up, hw.normal) * Quaternion.Euler(0, Rr(0, 360), 0), Vector3.one * Rr(1.8f, 3.4f));
                    if (cobj == null) continue;
                    nCry++;
                    if (rng.NextDouble() < 0.5)
                    {
                        var lg = new GameObject("CrystalGlow"); lg.transform.SetParent(cobj.transform); lg.transform.position = hw.point + hw.normal * 3f;
                        var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 20f; l.intensity = 3f; l.shadows = LightShadows.None;
                        l.color = col == "Red" ? new Color(1f, 0.3f, 0.3f) : col == "Blue" ? new Color(0.3f, 0.55f, 1f) : col == "Green" ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.85f, 0.3f);
                    }
                }

                // dead-end rewards
                if (isStub)
                {
                    Vector3 end = b; end.y = cy;
                    if (CastTube(new Vector3(end.x, cy + 3f, end.z), Vector3.down, 40f, out var hf))
                    {
                        var chest = Spawn("PP_Treasure_Chest_0" + (1 + rng.Next(4)) + "_" + (rng.Next(2) == 0 ? "Gold" : "Silver"), gMisc, hf.point, Quaternion.LookRotation(-dir, Vector3.up), Vector3.one * 2.4f);
                        if (chest != null) nChest++;
                    }
                }
            }

            // fill lights
            Vector3 center = Vector3.zero; foreach (var w in wp) center += w; center /= np; center.y = cy + 6f;
            var fl = new GameObject("Fill_" + t.name); fl.transform.SetParent(gLight); fl.transform.position = center;
            var fll = fl.AddComponent<Light>(); fll.type = LightType.Point; fll.color = new Color(1f, 0.72f, 0.45f); fll.intensity = hall ? 4.5f : 3f; fll.range = hall ? 70f : 48f; fll.shadows = LightShadows.None;
        }

        // entrance frame
        float ez = 178f;
        for (int ei = 0; ei < ForestMineBuilder.EntranceX.Length; ei++)
        {
            float ex = ForestMineBuilder.EntranceX[ei];
            float sc = ei == 0 ? 1f : 0.9f;
            Spawn("PP_Mine_Entrance_Wooden_01", gFrame, new Vector3(ex, ForestMineBuilder.Fy, ez), Quaternion.identity, new Vector3(3.1f * sc, 2.7f * sc, 2.7f));
            if (ei == 0) Spawn("PP_Mine_Entrance_Wooden_03", gFrame, new Vector3(ex, ForestMineBuilder.Fy, ez + 14f), Quaternion.identity, new Vector3(3.0f, 2.6f, 2.6f));
        }
        // entrance torches outside
        var torchSpots = new List<float>();
        foreach (float ex in ForestMineBuilder.EntranceX) { torchSpots.Add(ex - 22f); torchSpots.Add(ex + 22f); }
        foreach (float tx in torchSpots)
        {
            var torch = Spawn("PP_Torch_Standing_03", gTorch, new Vector3(tx, 1.6f, 170f), Quaternion.identity, Vector3.one * 2.6f);
            if (torch != null)
            {
                Vector3 tip = torch.transform.position + Vector3.up * 5.3f;
                Spawn("FX_Fire_Torch_02", torch.transform, tip, Quaternion.identity, Vector3.one * 2f);
                var lg = new GameObject("TorchLight"); lg.transform.SetParent(torch.transform); lg.transform.position = tip + Vector3.up;
                var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.62f, 0.25f); l.intensity = 6f; l.range = 30f; l.shadows = LightShadows.None;
                nTorch++;
            }
        }
        sb.AppendLine($"torches={nTorch} supports={nSup} rails={nRail} stalactites/mites={nStal} crystals={nCry} chests={nChest}");
        Physics.SyncTransforms();
        return sb.ToString();
    }
}
#endif
