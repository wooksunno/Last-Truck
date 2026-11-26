using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 확장 맵 땅 타일 이음매 수리.
///
/// 문제: 타일 프리팹(PP_Meadow_0x / PP_Ground_0x)은 정점이 성기고(가장자리 정점 간격 수 m) 원래 높낮이도 있다.
///       이웃 타일끼리 정점 위치가 다르고 무작위로 회전해 놓이므로, 경계에서 높이가 서로 달라(8cm ~ 2m) 틈새로 빛이 새거나 절벽처럼 보이는 "구멍"이 생겼다.
/// 수리:
///  1) 프리팹 자체의 높낮이는 경계에서 3.5m 안쪽까지 서서히 0으로 줄인다.
///  2) 경계 4m 안쪽까지 높이를 "타일 네 모서리 높이로 만든 평면"으로 서서히 맞춘다. 이웃 타일은 같은 모서리를 공유하므로 경계선이 완전히 같은 직선이 된다.
///  3) 남는 아주 작은 틈은 타일 바깥 테두리에 아래로 늘어뜨린 "치마(skirt)"로 가린다 (같은 색/노멀이라 티가 안 난다).
/// </summary>
public static partial class OpenWorldExpansionBuilder
{
    const string StitchedTag = "_stitched";
    const string PlanarTag = "_planar";
    const string ClampTag = "_clamped";
    const float SkirtDepth = 0.9f;
    const float HalfTile = 11.5f;           // 한 칸(23m)의 절반
    const float PlaneBlendWidth = 4f;       // 경계에서 이만큼 안쪽까지 평면으로 맞춘다

    /// <summary>
    /// cur = 지형 높이가 더해진 타일 메쉬, orig = 프리팹 원본 메쉬 (정점 순서 동일).
    /// l2w = 타일 오브젝트의 로컬→월드, cellMin = 이 타일이 차지하는 칸의 월드 최소 (x0, z0), corner(a,b) = 칸 모서리 (a,b ∈ {0,1}) 의 월드 높이.
    /// 이미 처리된 메쉬(이름 끝 _planar)는 건너뛴다.
    /// </summary>
    public static bool StitchMesh(Mesh cur, Mesh orig, Matrix4x4 l2w, Vector2 cellMin, Func<int, int, float> corner)
    {
        if (cur == null || orig == null || cur.name.EndsWith(ClampTag)) return false;
        int n = orig.vertexCount;
        if (cur.vertexCount < n || cur.subMeshCount != 1) return false;
        if (cur.colors.Length > 0 || cur.uv2.Length > 0) return false;

        float c00 = corner(0, 0), c10 = corner(1, 0), c01 = corner(0, 1), c11 = corner(1, 1);
        if (float.IsNaN(c00) || float.IsNaN(c10) || float.IsNaN(c01) || float.IsNaN(c11)) return false;

        Vector4[] tanAll = cur.tangents;
        Vector3[] vAll = cur.vertices, nrAll = cur.normals, o = orig.vertices;
        Vector2[] uvAll = cur.uv;

        // 앞 n개(원래 정점)만 쓴다. 이전에 붙인 치마가 있으면 버리고 다시 만든다.
        var v = new Vector3[n]; var nr = new Vector3[n]; var uv = new Vector2[n];
        Array.Copy(vAll, v, n); Array.Copy(nrAll, nr, n); Array.Copy(uvAll, uv, n);
        Vector4[] tan = tanAll.Length >= n ? new Vector4[n] : new Vector4[0];
        if (tan.Length > 0) Array.Copy(tanAll, tan, n);

        Bounds b = orig.bounds;
        float ex = b.extents.x, ez = b.extents.z;
        bool alreadyRelief = cur.name.Contains(StitchedTag);
        bool alreadyPlane = cur.name.Contains(PlanarTag);

        // 1) 경계 근처 프리팹 높낮이 제거 (한 번만)
        if (!alreadyRelief)
            for (int k = 0; k < n; k++)
            {
                float h = v[k].y - o[k].y;
                float ed = Mathf.Min(ex - Mathf.Abs(o[k].x - b.center.x), ez - Mathf.Abs(o[k].z - b.center.z));
                v[k].y = h + o[k].y * Sm((ed - 0.4f) / 3.5f);
            }

        // 2) 경계 4m 안쪽까지 네 모서리 평면에 맞춘다
        float ty = l2w.GetColumn(3).y;
        float cx = cellMin.x + HalfTile, cz = cellMin.y + HalfTile;
        if (!alreadyPlane)
            for (int k = 0; k < n; k++)
            {
                Vector3 w = l2w.MultiplyPoint3x4(v[k]);
                float edge = HalfTile - Mathf.Max(Mathf.Abs(w.x - cx), Mathf.Abs(w.z - cz));   // 칸 경계까지 거리 (바깥으로 삐져나오면 음수)
                float wb = 1f - Sm(edge / PlaneBlendWidth);
                if (wb <= 0f) continue;
                float tx = (w.x - cellMin.x) / (2f * HalfTile), tz = (w.z - cellMin.y) / (2f * HalfTile);
                float plane = Mathf.Lerp(Mathf.Lerp(c00, c10, tx), Mathf.Lerp(c01, c11, tx), tz);
                v[k].y = Mathf.Lerp(v[k].y, plane - ty, wb);
            }

        // 2-b) 칸 밖으로 삐져나온 부분(타일이 칸보다 0.36m 넓다)을 경계선 위로 눌러 붙인다.
        //      이웃 타일과 겹치는 띠가 없어져서, 거의 같은 높이의 두 면이 깜빡이는 줄무늬(z-fighting)가 사라진다.
        Matrix4x4 w2l = l2w.inverse;
        for (int k = 0; k < n; k++)
        {
            Vector3 w = l2w.MultiplyPoint3x4(v[k]);
            float cwx = Mathf.Clamp(w.x, cellMin.x, cellMin.x + 2f * HalfTile);
            float cwz = Mathf.Clamp(w.z, cellMin.y, cellMin.y + 2f * HalfTile);
            if (Mathf.Approximately(cwx, w.x) && Mathf.Approximately(cwz, w.z)) continue;
            float tx = (cwx - cellMin.x) / (2f * HalfTile), tz = (cwz - cellMin.y) / (2f * HalfTile);
            float plane = Mathf.Lerp(Mathf.Lerp(c00, c10, tx), Mathf.Lerp(c01, c11, tx), tz);
            v[k] = w2l.MultiplyPoint3x4(new Vector3(cwx, plane, cwz));
        }

        // 3) 바깥 테두리 모서리(한 삼각형에만 속한 모서리)에 치마를 붙인다
        int[] trisAll = cur.GetTriangles(0);
        var tris = new List<int>(trisAll.Length);
        for (int t = 0; t < trisAll.Length; t += 3)
            if (trisAll[t] < n && trisAll[t + 1] < n && trisAll[t + 2] < n) { tris.Add(trisAll[t]); tris.Add(trisAll[t + 1]); tris.Add(trisAll[t + 2]); }

        var edgeCount = new Dictionary<long, int>();
        for (int t = 0; t < tris.Count; t += 3)
            for (int e = 0; e < 3; e++)
            {
                long key = EdgeKey(o, tris[t + e], tris[t + (e + 1) % 3]);
                edgeCount.TryGetValue(key, out int cnt);
                edgeCount[key] = cnt + 1;
            }

        var verts = new List<Vector3>(v);
        var norms = new List<Vector3>(nr);
        var uvs = new List<Vector2>(uv);
        var tans = new List<Vector4>(tan);
        var triList = new List<int>(tris);
        const float eps = 0.2f;
        for (int t = 0; t < tris.Count; t += 3)
            for (int e = 0; e < 3; e++)
            {
                int ia = tris[t + e], ib = tris[t + (e + 1) % 3];
                if (edgeCount[EdgeKey(o, ia, ib)] != 1) continue;
                bool onX = Mathf.Abs(Mathf.Abs(o[ia].x - b.center.x) - ex) < eps && Mathf.Abs(Mathf.Abs(o[ib].x - b.center.x) - ex) < eps && Mathf.Sign(o[ia].x - b.center.x) == Mathf.Sign(o[ib].x - b.center.x);
                bool onZ = Mathf.Abs(Mathf.Abs(o[ia].z - b.center.z) - ez) < eps && Mathf.Abs(Mathf.Abs(o[ib].z - b.center.z) - ez) < eps && Mathf.Sign(o[ia].z - b.center.z) == Mathf.Sign(o[ib].z - b.center.z);
                if (!onX && !onZ) continue;   // 동굴 구멍 둘레는 제외

                Vector3 A = v[ia], B = v[ib];
                Vector3 outward = new Vector3(-(B.z - A.z), 0f, B.x - A.x);   // 위에서 볼 때 시계 방향 삼각형의 바깥쪽 (왼쪽)
                int baseIdx = verts.Count;
                verts.Add(A); verts.Add(B);
                verts.Add(A + Vector3.down * SkirtDepth); verts.Add(B + Vector3.down * SkirtDepth);
                norms.Add(nr[ia]); norms.Add(nr[ib]); norms.Add(nr[ia]); norms.Add(nr[ib]);
                uvs.Add(uv[ia]); uvs.Add(uv[ib]); uvs.Add(uv[ia]); uvs.Add(uv[ib]);
                if (tan.Length > 0) { tans.Add(tan[ia]); tans.Add(tan[ib]); tans.Add(tan[ia]); tans.Add(tan[ib]); }
                int a = baseIdx, bb = baseIdx + 1, a2 = baseIdx + 2, b2 = baseIdx + 3;
                Vector3 nrm = Vector3.Cross(verts[bb] - verts[a], verts[b2] - verts[a]);
                if (Vector3.Dot(nrm, outward) > 0f) { triList.Add(a); triList.Add(bb); triList.Add(b2); triList.Add(a); triList.Add(b2); triList.Add(a2); }
                else { triList.Add(a); triList.Add(b2); triList.Add(bb); triList.Add(a); triList.Add(a2); triList.Add(b2); }
            }

        string baseName = cur.name.Replace(StitchedTag, "").Replace(PlanarTag, "");
        cur.Clear();
        cur.SetVertices(verts);
        cur.SetNormals(norms);
        cur.SetUVs(0, uvs);
        if (tan.Length > 0) cur.SetTangents(tans);
        cur.SetTriangles(triList, 0);
        cur.RecalculateBounds();
        cur.name = baseName + StitchedTag + PlanarTag + ClampTag;
        return true;
    }

    static long EdgeKey(Vector3[] o, int a, int c)
    {
        long qa = Quant(o[a]), qc = Quant(o[c]);
        return qa < qc ? (qa << 32) ^ qc : (qc << 32) ^ qa;
    }

    static long Quant(Vector3 p)
    {
        // 위치(x,z)를 1mm 단위로 묶어서 같은 자리의 복제 정점이 같은 키가 되게 한다
        long ix = Mathf.RoundToInt(p.x * 1000f) & 0xFFFFF;
        long iz = Mathf.RoundToInt(p.z * 1000f) & 0xFFFFF;
        return (ix << 20) | iz;
    }

    /// <summary>
    /// 이미 만들어진 타일(씬 + 메쉬 에셋)에 같은 수리를 적용하고, 땅이 바뀐 만큼 근처 소품을 같이 내리거나 올린다 (다시 빌드하지 않는다).
    /// 칸 모서리 높이는 지금 타일들이 그 점에서 가진 높이의 평균을 쓴다.
    /// </summary>
    public static string StitchExistingTiles()
    {
        var root = GameObject.Find("Map/10_Expansion/Ground");
        if (root == null) return "no Ground root";

        // 소품: 바꾸기 전에 발밑 땅 높이를 기록한다
        var props = new List<Transform>();
        foreach (string g in new[] { "Vegetation", "Stones&Rocks", "Props", "Crystals&Ores&Veins", "Mushrooms" })
        {
            var grp = GameObject.Find("Map/10_Expansion/" + g);
            if (grp == null) continue;
            foreach (Transform c in grp.transform) props.Add(c);
        }
        var oldY = new float[props.Count];
        for (int i = 0; i < props.Count; i++) oldY[i] = GroundYAt(props[i].position, root.transform);

        // 칸 모서리 높이 (월드 격자점 → 평균 높이)
        var cornerY = new Dictionary<long, float>();
        float CornerAt(int a, int bz)
        {
            long key = ((long)a << 32) | (uint)bz;
            if (cornerY.TryGetValue(key, out float y)) return y;
            float x = GX0 + a * Step, z = GZ0 + bz * Step;
            var hits = Physics.RaycastAll(new Vector3(x, 150f, z), Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore);
            float sum = 0f; int cnt = 0;
            foreach (var h in hits)
                if (h.collider.name.StartsWith("EXP_Tile") && h.collider.transform.IsChildOf(root.transform)) { sum += h.point.y; cnt++; }
            y = cnt > 0 ? sum / cnt : float.NaN;
            cornerY[key] = y;
            return y;
        }
        // 모든 모서리를 미리 읽어 둔다 (타일을 바꾸기 시작하면 값이 달라지므로)
        var tilesInfo = new List<(Transform t, int i, int j)>();
        foreach (Transform t in root.transform)
        {
            var p = t.name.Split('_');
            if (p.Length < 5 || !int.TryParse(p[3], out int i) || !int.TryParse(p[4], out int j)) continue;
            tilesInfo.Add((t, i, j));
            CornerAt(i, j); CornerAt(i + 1, j); CornerAt(i, j + 1); CornerAt(i + 1, j + 1);
        }

        int done = 0, skipped = 0;
        foreach (var (t, i, j) in tilesInfo)
        {
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            var smf = src != null ? src.GetComponent<MeshFilter>() : null;
            if (smf == null || smf.sharedMesh == null) { skipped++; continue; }
            int ci = i, cj = j;
            Transform tt = t; Mesh tm = mf.sharedMesh; int nOrig = smf.sharedMesh.vertexCount;
            bool ok = StitchMesh(mf.sharedMesh, smf.sharedMesh, t.localToWorldMatrix, new Vector2(GX0 + i * Step, GZ0 + j * Step),
                                 (a, b) =>
                                 {
                                     float y = CornerAt(ci + a, cj + b);
                                     // 모서리 아래에 타일이 하나도 없으면(동굴 구멍 옆) 이 타일의 가장 가까운 정점 높이를 쓴다
                                     return float.IsNaN(y) ? NearestVertexWorldY(tt, tm, nOrig, GX0 + (ci + a) * Step, GZ0 + (cj + b) * Step) : y;
                                 });
            if (ok)
            {
                EditorUtility.SetDirty(mf.sharedMesh);
                var mc = t.GetComponent<MeshCollider>();
                if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = mf.sharedMesh; }
                done++;
            }
            else skipped++;
        }
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();

        int moved = 0;
        for (int i = 0; i < props.Count; i++)
        {
            if (props[i] == null || float.IsNaN(oldY[i])) continue;
            float ny = GroundYAt(props[i].position, root.transform);
            if (float.IsNaN(ny)) continue;
            float d = ny - oldY[i];
            if (Mathf.Abs(d) > 0.02f && Mathf.Abs(d) < 3f)
            {
                var p = props[i].position; p.y += d; props[i].position = p; moved++;
                EditorUtility.SetDirty(props[i]);
            }
        }
        return "stitched=" + done + " skipped=" + skipped + " propsAdjusted=" + moved + "/" + props.Count;
    }

    // ================================================================ 구멍 메우기

    const string GapDir = "Assets/Scenes/MiningMapAssets/Expansion/Gap";

    /// <summary>
    /// 확장 맵 안의 진짜 빈 곳(위에서 쏘아도 아무 충돌체도 없는 자리: 시작 지역 산과 확장 타일 사이 틈 등)을 땅 조각으로 메운다.
    /// 타일 경계선 바로 위(두께 0)는 빈 곳으로 세지 않는다. 낭떠러지 협곡(스왐프 서쪽 가장자리)은 땅이 아니라 보이는 바닥(충돌체 없음)만 깐다.
    /// 다시 실행해도 안전하다 (GapFill 오브젝트를 지우고 새로 만든다).
    /// </summary>
    public static string FillGaps()
    {
        var exp = GameObject.Find("Map/10_Expansion");
        if (exp == null) return "no 10_Expansion";
        var oldFill = exp.transform.Find("GapFill");
        if (oldFill != null) UnityEngine.Object.DestroyImmediate(oldFill.gameObject);
        var fillRoot = new GameObject("GapFill").transform;
        fillRoot.SetParent(exp.transform, false);
        var tileRoot = exp.transform.Find("Ground");
        var ground = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/MiningMapAssets/Ground/PP_Standard_Ground.mat");
        if (!AssetDatabase.IsValidFolder(GapDir)) AssetDatabase.CreateFolder("Assets/Scenes/MiningMapAssets/Expansion", "Gap");
        Physics.SyncTransforms();

        // 빈 곳 수집 (1m 격자)
        var voids = new List<Vector2>();
        for (float x = -119.3f; x < 641f; x += 1f)
            for (float z = -344.7f; z < 299f; z += 1f)
            {
                bool inZone = (z >= 96f && z <= 300f && x >= -120f) || (x >= 378f && z >= -350f);
                if (!inZone) continue;
                if (x > 360f && x < 392f && z > -72f && z < 10f) continue;   // 협곡은 따로
                // 표본 위치(…​.7)는 타일 경계선(정수 좌표)에서 0.3m 떨어져 있어서, 경계선 위 두께 0 틈은 자연히 피한다. 아주 가까운 것(<0.1m)만 제외.
                float mx = ((x + 120f) % 23f + 23f) % 23f, mz = ((z + 345f) % 23f + 23f) % 23f;
                if (mx < 0.1f || mx > 22.9f || mz < 0.1f || mz > 22.9f) continue;
                if (!Physics.Raycast(new Vector3(x, 150f, z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore)) voids.Add(new Vector2(x, z));
            }

        // 이어진 덩어리로 묶기
        var used = new bool[voids.Count];
        int patches = 0, cells = 0;
        for (int i = 0; i < voids.Count; i++)
        {
            if (used[i]) continue;
            var cluster = new List<Vector2>();
            var q = new Queue<int>(); q.Enqueue(i); used[i] = true;
            while (q.Count > 0)
            {
                int a = q.Dequeue(); cluster.Add(voids[a]);
                for (int b = 0; b < voids.Count; b++)
                    if (!used[b] && (voids[a] - voids[b]).sqrMagnitude < 2.1f * 2.1f) { used[b] = true; q.Enqueue(b); }
            }
            if (cluster.Count < 3) continue;
            cells += BuildGapPatch(cluster, fillRoot, tileRoot, ground, ++patches);
        }

        // 협곡 바닥: 하늘색 빈 공간이 비치지 않게 어두운 돌바닥을 깊이 깔아 둔다 (충돌체 없음 → 떨어지는 동작은 그대로)
        var floor = new GameObject("ChasmFloor");
        floor.transform.SetParent(fillRoot, false);
        floor.transform.position = new Vector3(378f, -22f, -30f);
        var mesh = new Mesh { name = "ChasmFloor" };
        mesh.SetVertices(new[] { new Vector3(-16f, 0f, -48f), new Vector3(16f, 0f, -48f), new Vector3(16f, 0f, 48f), new Vector3(-16f, 0f, 48f) });
        mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
        Vector2 uvDark = new Vector2(U(3), V(7, 0.45f));   // 팔레트 7번 줄(청회색) 3번 칸의 어두운 쪽 = 짙은 청회색 돌바닥
        mesh.SetUVs(0, new[] { uvDark, uvDark, uvDark, uvDark });
        mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, GapDir + "/ChasmFloor.asset");
        floor.AddComponent<MeshFilter>().sharedMesh = mesh;
        var fr = floor.AddComponent<MeshRenderer>();
        fr.sharedMaterial = ground;
        fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fr.receiveShadows = false;

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(exp);
        return "gap patches=" + patches + " cells=" + cells + " voidSamples=" + voids.Count + " + chasm floor";
    }

    static int BuildGapPatch(List<Vector2> cluster, Transform parent, Transform tileRoot, Material mat, int index)
    {
        Vector2 mn = cluster[0], mx = cluster[0];
        foreach (var p in cluster) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
        float x0 = Mathf.Floor(mn.x) - 2f, z0 = Mathf.Floor(mn.y) - 2f;
        int nx = Mathf.CeilToInt(mx.x + 2f - x0), nz = Mathf.CeilToInt(mx.y + 2f - z0);
        var voidCell = new HashSet<long>();
        foreach (var p in cluster) voidCell.Add(((long)Mathf.FloorToInt(p.x - x0) << 32) | (uint)Mathf.FloorToInt(p.y - z0));

        var verts = new List<Vector3>((nx + 1) * (nz + 1));
        for (int a = 0; a <= nx; a++)
            for (int b = 0; b <= nz; b++)
            {
                float x = x0 + a, z = z0 + b;
                float y = TileYAt(tileRoot, x, z, out bool onTile);
                verts.Add(new Vector3(x, onTile ? y - 0.04f : y, z));   // 이미 땅이 있는 곳은 살짝 아래로 (기존 땅이 위에 보이게)
            }
        var tris = new List<int>();
        int used = 0;
        for (int a = 0; a < nx; a++)
            for (int b = 0; b < nz; b++)
            {
                bool near = false;
                for (int da = -2; da <= 2 && !near; da++)
                    for (int db = -2; db <= 2 && !near; db++)
                        if (voidCell.Contains(((long)(a + da) << 32) | (uint)(b + db))) near = true;
                if (!near) continue;
                int i00 = a * (nz + 1) + b, i01 = i00 + 1, i10 = i00 + (nz + 1), i11 = i10 + 1;
                tris.Add(i00); tris.Add(i01); tris.Add(i11);
                tris.Add(i00); tris.Add(i11); tris.Add(i10);
                used++;
            }
        var norms = new List<Vector3>(verts.Count); var uvs = new List<Vector2>(verts.Count);
        Vector2 uv = new Vector2(U(6), VT(1, 0.35f));   // 초원 연두색 (옆 타일 색과 비슷하게)
        for (int k = 0; k < verts.Count; k++) { norms.Add(Vector3.up); uvs.Add(uv); }
        var mesh = new Mesh { name = "GapFill_" + index.ToString("D2") };
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, GapDir + "/" + mesh.name + ".asset");

        var go = new GameObject(mesh.name);
        go.tag = "Ground";
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return used;
    }

    /// <summary>(x,z)의 땅 타일 높이. 그 자리에 타일이 없으면 가장 가까운 타일 높이 (onTile = false).</summary>
    static float TileYAt(Transform tileRoot, float x, float z, out bool onTile)
    {
        float y;
        if (TileHit(tileRoot, x, z, out y)) { onTile = true; return y; }
        onTile = false;
        for (float r = 1f; r <= 12f; r += 1f)
            for (int k = 0; k < 8; k++)
            {
                float ang = k * Mathf.PI / 4f;
                if (TileHit(tileRoot, x + Mathf.Cos(ang) * r, z + Mathf.Sin(ang) * r, out y)) return y;
            }
        return BaseY;
    }

    static bool TileHit(Transform tileRoot, float x, float z, out float y)
    {
        y = 0f;
        var hits = Physics.RaycastAll(new Vector3(x, 150f, z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
        bool found = false; float best = -1e9f;
        foreach (var h in hits)
            if (h.collider.name.StartsWith("EXP_Tile") && h.collider.transform.IsChildOf(tileRoot) && h.point.y > best) { best = h.point.y; found = true; }
        if (found) y = best;
        return found;
    }

    static float NearestVertexWorldY(Transform t, Mesh m, int n, float wx, float wz)
    {
        var v = m.vertices;
        float best = float.MaxValue, y = float.NaN;
        for (int k = 0; k < n && k < v.Length; k++)
        {
            Vector3 w = t.TransformPoint(v[k]);
            float d = (w.x - wx) * (w.x - wx) + (w.z - wz) * (w.z - wz);
            if (d < best) { best = d; y = w.y; }
        }
        return y;
    }

    // 땅 타일 콜라이더만 대상으로 위에서 내려 쏜 높이 (없으면 NaN)
    static float GroundYAt(Vector3 p, Transform tileRoot)
    {
        var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 60f, p.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NaN;
        foreach (var h in hits)
            if (h.collider.name.StartsWith("EXP_Tile") && h.collider.transform.IsChildOf(tileRoot))
            {
                if (float.IsNaN(best) || Mathf.Abs(h.point.y - p.y) < Mathf.Abs(best - p.y)) best = h.point.y;
            }
        return best;
    }
}
