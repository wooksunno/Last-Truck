#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using CraftingSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 바위 고원 광산 입구(PP_Main_Entrance_01_Wooden) 뒤로 이어지는 지하 동굴 (Map/10_Expansion/MineCave).
///
///  - 입구의 검은 판(Black_Front)과 뒤쪽 바위(Stone_Back)를 치우고, 문 안쪽에서 시작하는 터널을 직접 만든 메시로 판다.
///  - 경로: 문 → 짧은 평지 → 가파른 내리막 → 땅 밑(바닥 y -10) 긴 터널 → 첫 번째 큰 광석방 → 좁은 터널 → 막다른 보물방.
///    천장이 땅(y 약 1~3)과 산(바닥 y -0.5)보다 항상 아래에 있도록 바닥을 깊게 잡아서 다른 지형과 겹치지 않는다.
///  - 입구 쪽 지상에 드러나는 구간은 바깥면도 그려서(두 겹) 비어 보이지 않게 한다.
///  - 안에는 레일, 나무 버팀목, 횃불(불꽃+빛), 광맥(구리/철/금/다이아), 수정, 종유석, 상자, 광산 수레를 둔다.
///  - 입구 방향/위치는 입구 물체에서 읽는다. 호출은 OpenWorldExpansionBuilder.Build가 한다 (트럭 통과 설정 뒤에).
/// </summary>
public static class ExpansionMineCave
{
    public const string CaveName = "MineCave";
    const string MeshDir = "Assets/Scenes/MiningMapAssets/Expansion/Cave";

    struct K { public float u, v, y, hw, h; public K(float u, float v, float y, float hw, float h) { this.u = u; this.v = v; this.y = y; this.hw = hw; this.h = h; } }

    // 바닥 깊이 (땅 y 약 1~3, 산 바닥 -0.5 아래로)
    const float DeepY = -16f;

    static Dictionary<string, string> _paths;
    static System.Random _rng;

    /// <summary>마지막 Build의 경로 (x, 바닥 y, z, 반폭) / 높이 - 검사용.</summary>
    public static readonly List<Vector4> LastStations = new List<Vector4>();
    public static readonly List<float> LastHeights = new List<float>();

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
        return _paths.TryGetValue(name, out string path) ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
    }

    static float R01() => (float)_rng.NextDouble();
    static float Rng(float a, float b) => Mathf.Lerp(a, b, R01());
    static string Pick(params string[] names) => names[_rng.Next(names.Length)];

    static float U(int col) => (col * 32 + 16) / 256f;
    static float V(int row, float shade) => 1f - (row * 32 + 2 + Mathf.Clamp01(shade) * 27f) / 256f;

    // ------------------------------------------------------------ 경로 표본

    class Path
    {
        public readonly List<Vector3> pos = new List<Vector3>();   // 바닥 중심선
        public readonly List<float> hw = new List<float>(), h = new List<float>();
        public readonly List<Vector3> dir = new List<Vector3>();   // 진행 방향 (수평)
        public readonly List<Vector3> right = new List<Vector3>();
        public readonly List<float> dist = new List<float>();      // 누적 거리
        public int Count => pos.Count;
    }

    static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    static Path Sample(List<Vector3> pts, List<Vector2> sizes, float step)
    {
        // 촘촘한 곡선 점 → 같은 간격으로 다시 뽑는다
        var dense = new List<Vector3>(); var dsz = new List<Vector2>();
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
            Vector2 s0 = sizes[Mathf.Max(0, i - 1)], s1 = sizes[i], s2 = sizes[i + 1], s3 = sizes[Mathf.Min(sizes.Count - 1, i + 2)];
            int sub = 24;
            for (int k = 0; k < sub; k++)
            {
                float t = k / (float)sub;
                dense.Add(CR(p0, p1, p2, p3, t));
                Vector3 sv = CR(new Vector3(s0.x, s0.y, 0), new Vector3(s1.x, s1.y, 0), new Vector3(s2.x, s2.y, 0), new Vector3(s3.x, s3.y, 0), t);
                dsz.Add(new Vector2(sv.x, sv.y));
            }
        }
        dense.Add(pts[pts.Count - 1]); dsz.Add(sizes[sizes.Count - 1]);

        var path = new Path();
        float acc = 0f, next = 0f;
        path.pos.Add(dense[0]); path.hw.Add(dsz[0].x); path.h.Add(dsz[0].y); path.dist.Add(0f);
        next = step;
        for (int i = 1; i < dense.Count; i++)
        {
            float seg = Vector3.Distance(dense[i - 1], dense[i]);
            while (acc + seg >= next)
            {
                float t = seg > 0f ? (next - acc) / seg : 0f;
                path.pos.Add(Vector3.Lerp(dense[i - 1], dense[i], t));
                Vector2 sz = Vector2.Lerp(dsz[i - 1], dsz[i], t);
                path.hw.Add(sz.x); path.h.Add(sz.y); path.dist.Add(next);
                next += step;
            }
            acc += seg;
        }
        // 끝점
        if (Vector3.Distance(path.pos[path.pos.Count - 1], dense[dense.Count - 1]) > step * 0.3f)
        {
            path.pos.Add(dense[dense.Count - 1]); path.hw.Add(dsz[dsz.Count - 1].x); path.h.Add(dsz[dsz.Count - 1].y); path.dist.Add(acc);
        }

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 a = path.pos[Mathf.Max(0, i - 1)], b = path.pos[Mathf.Min(path.Count - 1, i + 1)];
            Vector3 d = b - a; d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) d = i > 0 ? path.dir[i - 1] : Vector3.forward;
            d.Normalize();
            path.dir.Add(d);
            path.right.Add(Vector3.Cross(Vector3.up, d).normalized);
        }
        return path;
    }

    // ------------------------------------------------------------ 메시

    static readonly Vector2[] Profile =
    {
        new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(1.08f, 0.35f), new Vector2(0.95f, 0.70f), new Vector2(0.55f, 0.93f),
        new Vector2(0f, 1f), new Vector2(-0.55f, 0.93f), new Vector2(-0.95f, 0.70f), new Vector2(-1.08f, 0.35f),
    };

    static float Noise3(Vector3 p, float f) =>
        (Mathf.PerlinNoise(p.x * f + 100f, p.z * f + 200f) + Mathf.PerlinNoise(p.y * f + 300f, p.x * f + 400f) + Mathf.PerlinNoise(p.z * f + 500f, p.y * f + 600f)) / 3f - 0.5f;

    /// <summary>경로를 따라 터널 메시(안쪽 면)를 만든다. outer=true면 바깥을 향한 같은 면 (땅 위로 드러나는 구간용).</summary>
    static void BuildMesh(Path path, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris, int fromStation, int toStation, bool outer)
    {
        int n = path.Count, R = Profile.Length;
        var ring = new Vector3[n][];
        for (int i = 0; i < n; i++)
        {
            ring[i] = new Vector3[R];
            float hw = path.hw[i], h = path.h[i];
            bool hall = hw > 10f;
            float amp = hall ? 1.4f : 0.7f;
            for (int j = 0; j < R; j++)
            {
                Vector2 pr = Profile[j];
                Vector3 local = path.right[i] * (pr.x * hw) + Vector3.up * (pr.y * h);
                Vector3 wp = path.pos[i] + local;
                if (j >= 2)   // 바닥(0,1번)은 평평하게 둔다
                {
                    Vector3 toCenter = (path.pos[i] + Vector3.up * (0.45f * h)) - wp;
                    Vector3 away = -toCenter.normalized;
                    float nz = Noise3(wp, 0.11f) * 2f * amp + Noise3(wp, 0.35f) * amp;
                    // 천장 가운데(5번)는 너무 흔들리지 않게
                    if (j == 5) nz *= 0.5f;
                    wp += away * nz;
                }
                if (outer)
                {
                    // 바위 껍질: 터널 중심에서 바깥으로 1.3m 밀어낸다 (바닥 모서리는 옆/아래로)
                    Vector3 cen = path.pos[i] + Vector3.up * (0.45f * h);
                    Vector3 dirOut = (wp - cen); if (dirOut.sqrMagnitude < 1e-6f) dirOut = Vector3.up;
                    wp += dirOut.normalized * 1.3f;
                }
                ring[i][j] = wp;
            }
        }

        for (int i = Mathf.Max(0, fromStation); i < Mathf.Min(n - 1, toStation); i++)
        {
            for (int j = 0; j < R; j++)
            {
                int j2 = (j + 1) % R;
                Vector3 a = ring[i][j], b = ring[i][j2], c = ring[i + 1][j2], d = ring[i + 1][j];
                // 이 칸의 안쪽 방향 (터널 중심 쪽)
                Vector3 mid = (a + b + c + d) * 0.25f;
                Vector3 center = (path.pos[i] + path.pos[i + 1]) * 0.5f + Vector3.up * (0.45f * path.h[i]);
                Vector3 inward = center - mid;
                float rock = Mathf.PerlinNoise(mid.x * 0.2f + 7f, mid.z * 0.2f + 3f);
                float shade = 0.12f + rock * 0.5f;
                int col = rock > 0.62f ? 5 : 6;
                Vector2 uv = new Vector2(U(col), V(0, shade));
                AddTri(verts, normals, uvs, tris, a, b, c, inward, uv, outer);
                AddTri(verts, normals, uvs, tris, a, c, d, inward, uv, outer);
            }
        }

        // 막다른 끝을 막는다 (안 막으면 끝에서 터널 밖 빈 곳이 보인다)
        if (!outer && toStation >= n - 1)
        {
            int last = n - 1;
            Vector3 cc = path.pos[last] + Vector3.up * (0.45f * path.h[last]);
            Vector3 backIn = -path.dir[last];
            Vector2 capUv = new Vector2(U(6), V(0, 0.2f));
            for (int j = 0; j < R; j++)
                AddTri(verts, normals, uvs, tris, ring[last][j], ring[last][(j + 1) % R], cc, backIn, capUv, false);
        }
    }

    static void AddTri(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 inward, Vector2 uv, bool outer)
    {
        Vector3 nrm = Vector3.Cross(b - a, c - a);
        if (nrm.sqrMagnitude < 1e-8f) return;   // 찌그러진 삼각형은 버린다
        nrm.Normalize();
        bool wantInward = !outer;
        bool pointsInward = Vector3.Dot(nrm, inward) > 0f;
        if (pointsInward != wantInward) { var t = b; b = c; c = t; nrm = -nrm; }
        int baseIndex = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        normals.Add(nrm); normals.Add(nrm); normals.Add(nrm);
        uvs.Add(uv); uvs.Add(uv); uvs.Add(uv);
        tris.Add(baseIndex); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
    }

    static Mesh MakeMesh(string name, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris)
    {
        var m = new Mesh { name = name };
        if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(verts); m.SetNormals(normals); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    // ------------------------------------------------------------ 입구

    static Transform FindChildContaining(Transform root, string part)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name.Contains(part)) return t;
        return null;
    }

    // ------------------------------------------------------------ 만들기

    /// <summary>경로 한 지점 (계획용).</summary>
    public struct Station { public Vector3 pos; public float hw, h; }

    /// <summary>입구 문 위치/바닥 높이/옆 방향에서 만들어질 동굴 경로를 미리 계산한다 (입구는 -Z를 향한다고 본다). 겹침 검사용.</summary>
    public static List<Station> Plan(Vector3 doorPoint, float floor0, float side, int variant)
    {
        Path p = MakePath(doorPoint, Vector3.forward, Vector3.right, floor0, side, variant);
        var list = new List<Station>(p.Count);
        for (int i = 0; i < p.Count; i++) list.Add(new Station { pos = p.pos[i], hw = p.hw[i], h = p.h[i] });
        return list;
    }

    /// <summary>
    /// 계획한 경로가 들어가도 되는가: 지도 안(고원) / 북쪽 경계 벽(z 300) 아래 / 이미 만든 동굴과 6m 이상 간격 / 산·경계 벽과 안 겹침.
    /// </summary>
    public static bool Fits(List<Station> plan, List<Station> others, out string why)
    {
        why = "";
        foreach (var s in plan)
        {
            if (s.pos.x - s.hw < 178f || s.pos.x + s.hw > 625f) { why = "지도 가로 범위 밖 x=" + s.pos.x.ToString("F0"); return false; }
            if (s.pos.z - s.hw < 112f || s.pos.z + s.hw > 289f) { why = "지도 세로 범위 밖 z=" + s.pos.z.ToString("F0"); return false; }
        }
        if (others != null)
            foreach (var s in plan)
                foreach (var o in others)
                {
                    float dx = s.pos.x - o.pos.x, dz = s.pos.z - o.pos.z;
                    float need = s.hw + o.hw + 6f;
                    if (dx * dx + dz * dz < need * need && Mathf.Abs(s.pos.y - o.pos.y) < 20f) { why = "다른 동굴과 겹침 (" + s.pos.x.ToString("F0") + "," + s.pos.z.ToString("F0") + ")"; return false; }
                }
        Physics.SyncTransforms();
        foreach (var s in plan)
            for (float f = -0.7f; f <= 0.71f; f += 0.35f)
                for (float yy = 1f; yy <= s.h * 0.85f; yy += 3f)
                {
                    // 단면 방향을 모르는 계획 단계라 x/z 둘 다 훑는다
                    foreach (var col in Physics.OverlapSphere(s.pos + Vector3.up * yy + new Vector3(f * s.hw, 0f, 0f), 0.6f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        string n = col.name;
                        if (n.StartsWith("PP_Mountain") || n == "Wall") { why = "산/경계 벽과 겹침 " + n + " (" + s.pos.x.ToString("F0") + "," + s.pos.z.ToString("F0") + ")"; return false; }
                    }
                    foreach (var col in Physics.OverlapSphere(s.pos + Vector3.up * yy + new Vector3(0f, 0f, f * s.hw), 0.6f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        string n = col.name;
                        if (n.StartsWith("PP_Mountain") || n == "Wall") { why = "산/경계 벽과 겹침 " + n + " (" + s.pos.x.ToString("F0") + "," + s.pos.z.ToString("F0") + ")"; return false; }
                    }
                }
        return true;
    }

    /// <summary>제어점으로 경로를 만든다. variant 1 = 큰 방 두 개의 긴 광산 / variant 2 = 수정 동굴 (남쪽으로 휘며 큰 방 두 개).</summary>
    static Path MakePath(Vector3 doorPoint, Vector3 F, Vector3 Rt, float floor0, float s, int variant)
    {
        Vector3 W(float u, float v) => doorPoint + F * v + Rt * (s * u);
        float descent = 0.43f;                                   // 내리막 기울기 (약 23도)
        float uStart = 4f;                                       // 내리막이 시작되는 옆 거리
        System.Func<float, float> RampY = u => Mathf.Max(DeepY, floor0 - descent * (u - uStart));
        float uEnd = uStart + (floor0 - DeepY) / descent;        // 내리막이 끝나는 옆 거리
        var K = new List<K>
        {
            new K(0f, -0.3f, floor0, 4.1f, 14.5f),                          // 문 (폭 8m)
            new K(0f, 4.5f, floor0, 4.1f, 13f),                             // 문틀(앞 바위) 안쪽까지는 문 폭 그대로
            new K(3f, 8.5f, floor0, 7f, 12f),                               // 들어와서 넓어진다
            new K(uStart + 8f, 8.5f, RampY(uStart + 8f), 7f, 12f),
            new K(uStart + (uEnd - uStart) * 0.5f, 8.5f, RampY(uStart + (uEnd - uStart) * 0.5f), 7f, 12f),
            new K(uEnd, 9f, DeepY, 7f, 12f),                                // 내리막 끝
        };
        if (variant == 1)
        {
            K.Add(new K(uEnd + 18f, 14f, DeepY, 7f, 12f));
            K.Add(new K(uEnd + 38f, 22f, DeepY, 9f, 12.5f));
            K.Add(new K(uEnd + 58f, 29f, DeepY, 19f, 14f));                  // 큰 광석방 A
            K.Add(new K(uEnd + 80f, 31f, DeepY, 15f, 13f));
            K.Add(new K(uEnd + 98f, 26f, DeepY, 7f, 12f));
            K.Add(new K(uEnd + 114f, 18f, DeepY, 7f, 12f));
            K.Add(new K(uEnd + 132f, 10f, DeepY, 11f, 12.5f));
            K.Add(new K(uEnd + 148f, 6f, DeepY, 15f, 13.5f));               // 막다른 보물방
            K.Add(new K(uEnd + 160f, 6f, DeepY, 14f, 13f));
        }
        else
        {
            // 수정 동굴: 내리막 뒤 남쪽(문 뒤쪽)으로 부드럽게 휘어 내려가며, 가운데 방과 막다른 방이 있다. 첫 방(큰 광석방)이 일찍 나온다.
            K.Add(new K(uEnd + 14f, 4f, DeepY, 7f, 12f));
            K.Add(new K(uEnd + 30f, -4f, DeepY, 9f, 12.5f));
            K.Add(new K(uEnd + 46f, -12f, DeepY, 17f, 14f));                // 수정 방 (큰 방 C)
            K.Add(new K(uEnd + 64f, -20f, DeepY, 15f, 13.5f));
            K.Add(new K(uEnd + 80f, -30f, DeepY, 7f, 12f));
            K.Add(new K(uEnd + 94f, -42f, DeepY, 7f, 12f));
            K.Add(new K(uEnd + 108f, -52f, DeepY, 10f, 12.5f));
            K.Add(new K(uEnd + 122f, -58f, DeepY, 17f, 14f));               // 막다른 방 D
            K.Add(new K(uEnd + 134f, -58f, DeepY, 15f, 13.5f));
        }
        var pts = new List<Vector3>(); var sizes = new List<Vector2>();
        foreach (var k in K) { Vector3 w = W(k.u, k.v); pts.Add(new Vector3(w.x, k.y, w.z)); sizes.Add(new Vector2(k.hw, k.h)); }
        return Sample(pts, sizes, 2.2f);
    }

    /// <summary>만들어 둔 동굴 경로 (다음 동굴과 겹치지 않게 비교하는 용도).</summary>
    public static readonly Dictionary<int, List<Station>> BuiltPlans = new Dictionary<int, List<Station>>();

    public static string Build(Transform expansionRoot, Transform entrance, int seed, int variant = 1, float sideSign = 0f)
    {
        var sb = new StringBuilder();
        if (entrance == null) return "광산 동굴: 입구 없음";
        _rng = new System.Random(seed);
        string caveName = variant == 1 ? CaveName : CaveName + variant;

        var old = expansionRoot.Find(caveName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/Scenes/MiningMapAssets/Expansion", "Cave");

        // 입구 방향 (문은 -forward 쪽을 향하고, 안쪽은 +forward 반대편)
        Vector3 F = -entrance.forward; F.y = 0f; F.Normalize();
        Vector3 Rt = Vector3.Cross(Vector3.up, F).normalized;
        var blackFront = FindChildContaining(entrance, "Black_Front");
        var stoneBack = FindChildContaining(entrance, "Stone_Back");
        Vector3 doorPoint = blackFront != null ? blackFront.GetComponent<Renderer>().bounds.center : entrance.position + F * 2.7f;
        float floor0;
        {
            // 문 앞 바닥 높이 (입구 앞 땅)
            floor0 = entrance.position.y + 0.2f;
            foreach (var hh in Physics.RaycastAll(entrance.position - F * 3f + Vector3.up * 40f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore))
                if (hh.collider.name.StartsWith("EXP_Tile")) { floor0 = hh.point.y + 0.15f; break; }
        }
        float s = sideSign != 0f ? sideSign : (entrance.position.x < 400f ? 1f : -1f);   // 더 넓은 쪽(동쪽/서쪽)으로 꺾는다

        // 문을 막고 있던 검은 판과 뒤쪽 바위를 치운다
        if (blackFront != null) blackFront.gameObject.SetActive(false);
        if (stoneBack != null) stoneBack.gameObject.SetActive(false);

        // ---- 경로 (u 옆 / v 앞 / y 바닥 / 반폭 / 높이)
        // 크기: 3인칭 카메라가 천장 안에 들어오도록 기존 동굴처럼 크게 판다 (예전에는 천장 6.5m라 카메라가 천장 밖으로 나가 벽에 막혀 보였다).
        //   터널 폭 14m / 높이 12m, 큰 방 폭 30~38m / 높이 14m. 바닥은 y -16이라 천장이 땅과 산 밑(y -2 이하)에 있다.
        // 경로: 문 안쪽에서 북쪽으로 8m 들어간 뒤 곧바로 옆(동/서)으로 꺾어 그 방향으로 내려간다 (절벽과 평행하게).
        Path path = MakePath(doorPoint, F, Rt, floor0, s, variant);
        sb.AppendLine($"cave{variant} path: {path.Count} stations, {path.dist[path.Count - 1]:F0}m, floor {floor0:F1} -> {DeepY}");
        LastStations.Clear();
        for (int i = 0; i < path.Count; i++) LastStations.Add(new Vector4(path.pos[i].x, path.pos[i].y, path.pos[i].z, path.hw[i]));
        LastHeights.Clear();
        for (int i = 0; i < path.Count; i++) LastHeights.Add(path.h[i]);
        var planList = new List<Station>();
        for (int i = 0; i < path.Count; i++) planList.Add(new Station { pos = path.pos[i], hw = path.hw[i], h = path.h[i] });
        BuiltPlans[variant] = planList;

        // ---- 지상 바닥 타일에 터널 구멍 (내리막 구간은 땅을 가로질러서, 구멍을 안 내면 땅이 터널 가운데를 가로막는다)
        int holeTris = CutGroundHoles(expansionRoot, path);
        sb.AppendLine("ground tile triangles removed for tunnel: " + holeTris);

        // ---- 터널이 지나는 땅 위의 나무/바위/풀을 치운다 (터널 껍질 속에 박혀 보이지 않게)
        int cleared = ClearPropsAbove(expansionRoot, path);
        sb.AppendLine("props cleared above tunnel: " + cleared);

        // ---- 메시
        var inV = new List<Vector3>(); var inN = new List<Vector3>(); var inUV = new List<Vector2>(); var inT = new List<int>();
        BuildMesh(path, inV, inN, inUV, inT, 0, path.Count - 1, false);
        var outV = new List<Vector3>(); var outN = new List<Vector3>(); var outUV = new List<Vector2>(); var outT = new List<int>();
        // 바깥면(바위 껍질)은 땅 위로 드러나는 앞쪽만 (천장이 땅보다 높은 구간). 안쪽 벽보다 1.3m 바깥에 둬서 타일 구멍 가장자리를 덮는다.
        int outerTo = 0;
        for (int i = 0; i < path.Count; i++) if (path.pos[i].y + path.h[i] > -1f) outerTo = i + 1;
        BuildMesh(path, outV, outN, outUV, outT, 0, Mathf.Min(path.Count - 1, outerTo + 1), true);

        string meshTag = variant == 1 ? "MineCave" : "MineCave" + variant;
        Mesh innerMesh = MakeMesh(meshTag + "_Inner", inV, inN, inUV, inT);
        Mesh outerMesh = MakeMesh(meshTag + "_Outer", outV, outN, outUV, outT);
        // NaN 검사 (충돌체/내비메시 굽기 크래시 방지)
        foreach (var v in innerMesh.vertices) if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)) return "광산 동굴: NaN 정점";
        AssetDatabase.CreateAsset(innerMesh, MeshDir + "/" + meshTag + "_Inner.asset");
        AssetDatabase.CreateAsset(outerMesh, MeshDir + "/" + meshTag + "_Outer.asset");

        // 재질 (바닥 타일과 같은 팔레트 재질)
        Material mat = null;
        var tilePf = P("PP_Ground_01");
        if (tilePf != null) { var tr = tilePf.GetComponentInChildren<Renderer>(); if (tr != null) mat = tr.sharedMaterial; }

        var root = new GameObject(caveName).transform;
        root.SetParent(expansionRoot, false);
        var zone = root.gameObject.AddComponent<LastTruck.ZoneMarker>();   // 들어가면 구역 이름이 뜬다 (지하에 있을 때만)
        zone.zoneName = variant == 1 ? "버려진 광산" : "수정 동굴";
        zone.hint = variant == 1 ? "횃불이 타오르는 지하 갱도. 광맥과 수정이 빛난다" : "푸른 수정이 빛나는 깊은 동굴. 금과 다이아몬드 광맥이 숨어 있다";
        zone.radius = 0f; zone.maxY = -3f;

        var inner = new GameObject("Inner"); inner.transform.SetParent(root, false);
        inner.layer = 8;   // 동굴 바닥 레이어 (낮에 햇빛/주변광을 줄여 동굴처럼 어둡게)
        inner.AddComponent<MeshFilter>().sharedMesh = innerMesh;
        var ir = inner.AddComponent<MeshRenderer>(); ir.sharedMaterial = mat;
        inner.AddComponent<MeshCollider>().sharedMesh = innerMesh;

        var outerGo = new GameObject("Outer"); outerGo.transform.SetParent(root, false);
        outerGo.AddComponent<MeshFilter>().sharedMesh = outerMesh;
        var orr = outerGo.AddComponent<MeshRenderer>(); orr.sharedMaterial = mat;

        Physics.SyncTransforms();

        // ---- 꾸미기
        var gTorch = new GameObject("Torches").transform; gTorch.SetParent(root, false);
        var gSup = new GameObject("Supports").transform; gSup.SetParent(root, false);
        var gRail = new GameObject("Rails").transform; gRail.SetParent(root, false);
        var gDeco = new GameObject("Deco").transform; gDeco.SetParent(root, false);
        var gOre = new GameObject("Ores").transform; gOre.SetParent(root, false);
        var gLight = new GameObject("Lights").transform; gLight.SetParent(root, false);

        int nTorch = 0, nSup = 0, nRail = 0, nDeco = 0, nCry = 0, nChest = 0;

        // 버팀목 크기: 터널 폭에 맞춘다
        string[] supports = { "PP_Mine_Wooden_Support_03", "PP_Mine_Wooden_Support_04", "PP_Mine_Wooden_Support_05" };
        var supBounds = new Dictionary<string, Bounds>();
        foreach (var sn in supports)
        {
            var pf = P(sn); if (pf == null) continue;
            var mf = pf.GetComponentInChildren<MeshFilter>(); if (mf != null) supBounds[sn] = mf.sharedMesh.bounds;
        }

        // 레일 길이
        string railName = "PP_Rail_Straight_Long";
        var railPf = P(railName);
        float railLen = 8f; bool railAlongX = false;
        if (railPf != null)
        {
            var rm = railPf.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            railAlongX = rm.size.x > rm.size.z;
            railLen = (railAlongX ? rm.size.x : rm.size.z) * 1.3f;
        }

        // 경로 위 거리에서 위치/방향
        float total = path.dist[path.Count - 1];
        System.Func<float, int> StationAt = d =>
        {
            int idx = 0;
            for (int i = 0; i < path.Count; i++) { if (path.dist[i] <= d) idx = i; else break; }
            return idx;
        };

        // 레일은 문 안쪽부터 내리막이 끝나는 곳까지만 (직선 레일 조각이 휘는 구간에서는 터널 가운데를 벗어나 길을 막는다)
        float railFrom = 3f;
        for (int i = 0; i < path.Count; i++) if (path.pos[i].y < floor0 - 0.5f) { railFrom = path.dist[i] + 4f; break; }   // 꺾이는 구간을 지나 내리막이 시작된 뒤부터
        float railTo = RampEndDistance(path, floor0) - 1f;
        if (railPf != null && railTo > railFrom)
        {
            for (float d = railFrom; d + railLen < railTo; d += railLen)
            {
                int i0 = StationAt(d), i1 = StationAt(d + railLen);
                Vector3 p0 = FloorAt(path, d), p1 = FloorAt(path, d + railLen);
                Vector3 mid = (p0 + p1) * 0.5f + Vector3.up * 0.06f;
                Quaternion q = Quaternion.LookRotation((p1 - p0).normalized, Vector3.up);
                if (railAlongX) q *= Quaternion.Euler(0, 90f, 0);
                var r = Spawn(railName, gRail, mid, q, Vector3.one * 1.3f);
                if (r != null) nRail++;
            }
        }

        // 버팀목 + 횃불 + 채움 빛
        for (float d = 10f; d < total - 6f; d += 16f)
        {
            int i = StationAt(d);
            if (path.hw[i] > 8.5f) continue;   // 큰 방 안은 버팀목이 어색하다
            // 급하게 꺾이는 곳은 건너뛴다 (프레임이 비스듬히 서서 길을 막는다)
            if (Vector3.Angle(path.dir[Mathf.Max(0, i - 3)], path.dir[Mathf.Min(path.Count - 1, i + 3)]) > 20f) continue;
            if (supBounds.Count > 0)
            {
                string sn = supports[_rng.Next(supports.Length)];
                if (!supBounds.ContainsKey(sn)) sn = new List<string>(supBounds.Keys)[0];
                var sbnd = supBounds[sn];
                float sx = Mathf.Clamp((path.hw[i] * 2f * 0.9f) / Mathf.Max(0.01f, sbnd.size.x), 0.5f, 9f);
                float sy = Mathf.Clamp((path.h[i] * 0.85f) / Mathf.Max(0.01f, sbnd.size.y), 0.5f, 9f);
                Vector3 fp = FloorAt(path, d);
                var s0 = Spawn(sn, gSup, fp - Vector3.up * (sbnd.min.y * sy), Quaternion.LookRotation(path.dir[i], Vector3.up), new Vector3(sx, sy, Mathf.Min(sx, sy) * 0.7f));
                if (s0 != null) nSup++;
            }
        }

        string[] torchNames = { "PP_Torch_Standing_01", "PP_Torch_Standing_03", "PP_Torch_Standing_04", "PP_Torch_Standing_06" };
        string[] fxNames = { "FX_Fire_Torch_01", "FX_Fire_Torch_02", "FX_Fire_Torch_03" };
        float sideFlip = 1f;
        for (float d = 6f; d < total - 4f; d += 12f)
        {
            int i = StationAt(d);
            float hw = path.hw[i];
            sideFlip = -sideFlip;
            // 문 가까운 앞쪽(지상)에는 횃불을 두지 않는다 (입구 바깥 장식과 겹침)
            if (path.pos[i].y > -1f && d < 8f) continue;
            Vector3 fp = FloorAt(path, d) + path.right[i] * (sideFlip * (hw - 1.8f));
            var torch = Spawn(Pick(torchNames), gTorch, fp, Quaternion.Euler(0, Rng(0, 360), 0), Vector3.one * 3.2f);
            if (torch == null) continue;
            Vector3 tip = fp + Vector3.up * 6.5f;
            Spawn(Pick(fxNames), torch.transform, tip, Quaternion.identity, Vector3.one * 2.4f);
            var lg = new GameObject("TorchLight"); lg.transform.SetParent(torch.transform, true); lg.transform.position = tip + Vector3.up * 1.0f;
            var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.62f, 0.25f); l.intensity = 9f; l.range = 34f; l.shadows = LightShadows.None;
            nTorch++;
        }
        for (float d = 4f; d < total - 2f; d += 16f)
        {
            int i = StationAt(d);
            bool bigRoom = path.hw[i] > 12f;
            var fl = new GameObject("Fill"); fl.transform.SetParent(gLight, false);
            fl.transform.position = path.pos[i] + Vector3.up * (path.h[i] * 0.7f);
            var fll = fl.AddComponent<Light>(); fll.type = LightType.Point; fll.color = variant == 2 ? new Color(0.55f, 0.72f, 1f) : new Color(1f, 0.72f, 0.45f);   // 수정 동굴은 푸른 빛
            fll.intensity = bigRoom ? 7f : 4.5f; fll.range = bigRoom ? 70f : 45f; fll.shadows = LightShadows.None;
        }

        // 종유석 / 석순 / 수정
        string[] crystalCols = { "Red", "Blue", "Green", "Gold" };
        for (float d = 12f; d < total - 3f; d += Rng(4f, 7f))
        {
            int i = StationAt(d);
            float hw = path.hw[i];
            bool hall = hw > 10f;
            Vector3 c = path.pos[i];
            // 바닥 돌무더기 (기존 동굴처럼 어두운 바위가 곳곳에)
            if (R01() < 0.55f && c.y < floor0 - 1f)
            {
                float sg = R01() < 0.5f ? -1f : 1f;
                Vector3 at = c + path.right[i] * (sg * (hw - Rng(2.5f, 5.5f)));
                var o = SpawnSized(Pick("PP_Rock_0" + (1 + _rng.Next(7)), "PP_Rock_Pile_" + (1 + _rng.Next(12)).ToString("D2")), gDeco, new Vector3(at.x, c.y - 0.3f, at.z), Quaternion.Euler(0, Rng(0, 360), 0), Rng(2.2f, hall ? 4.5f : 3.4f));
                if (o != null) nDeco++;
            }
            // 덩굴: 벽 가까이에서 늘어진다 (크기를 맞춰서 놓는다. 크기를 안 맞추면 40m짜리 덩굴이 터널을 가득 채운다)
            if (R01() < 0.3f)
            {
                float sg = R01() < 0.5f ? -1f : 1f;
                Vector3 at = c + path.right[i] * (sg * (hw * 0.9f));
                var o = SpawnSized("PP_Vine_0" + (1 + _rng.Next(8)), gDeco, new Vector3(at.x, c.y + path.h[i] * 0.55f, at.z), Quaternion.LookRotation(-path.right[i] * sg, Vector3.up), Rng(5f, 8f));
                if (o != null) nDeco++;
            }
            // 종유석: 천장에서 아래로
            if (R01() < 0.85f)
            {
                string sn = "PP_Stalactite_" + (1 + _rng.Next(10)).ToString("D2");
                var pf = P(sn);
                if (pf != null)
                {
                    float maxY = pf.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.max.y;
                    float sc = Rng(3.5f, hall ? 8f : 6f) / MaxDim(sn);
                    Vector3 at = c + path.right[i] * Rng(-hw * 0.6f, hw * 0.6f);
                    float ceilY = c.y + path.h[i] * 0.95f;
                    var o = Spawn(sn, gDeco, new Vector3(at.x, ceilY - maxY * sc + 0.4f, at.z), Quaternion.Euler(0, Rng(0, 360), 0), Vector3.one * sc);
                    if (o != null) nDeco++;
                }
            }
            // 석순: 바닥 가장자리
            if (R01() < 0.5f)
            {
                string sn = "PP_Stalagmite_" + (1 + _rng.Next(8)).ToString("D2");
                var pf = P(sn);
                if (pf != null)
                {
                    float minY = pf.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.min.y;
                    float sc = Rng(3f, 5.5f) / MaxDim(sn);
                    float sg = R01() < 0.5f ? -1f : 1f;
                    Vector3 at = c + path.right[i] * (sg * (hw - Rng(2.5f, 4.5f)));
                    var o = Spawn(sn, gDeco, new Vector3(at.x, c.y - minY * sc - 0.1f, at.z), Quaternion.Euler(0, Rng(0, 360), 0), Vector3.one * sc);
                    if (o != null) nDeco++;
                }
            }
            // 수정: 벽에 박힘 + 은은한 빛
            if (R01() < (hall ? 0.7f : 0.35f) + (variant == 2 ? 0.3f : 0f))
            {
                float sg = R01() < 0.5f ? -1f : 1f;
                string col = variant == 2 && R01() < 0.6f ? "Blue" : crystalCols[_rng.Next(crystalCols.Length)];
                string cn = "PP_Crystal_Cluster_0" + (1 + _rng.Next(6)) + "_" + col;
                Vector3 wallPos = c + path.right[i] * (sg * (hw * 1.0f)) + Vector3.up * Rng(0.4f, 3.5f);
                Vector3 normal = -path.right[i] * sg;
                var cobj = SpawnSized(cn, gDeco, wallPos - normal * 0.4f, Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0, Rng(0, 360), 0), Rng(2.6f, 4.4f));
                if (cobj != null)
                {
                    nCry++;
                    var lg = new GameObject("CrystalGlow"); lg.transform.SetParent(cobj.transform, true); lg.transform.position = wallPos + normal * 3.2f;
                    var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 20f; l.intensity = 3.5f; l.shadows = LightShadows.None;
                    l.color = col == "Red" ? new Color(1f, 0.3f, 0.3f) : col == "Blue" ? new Color(0.3f, 0.55f, 1f) : col == "Green" ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.85f, 0.3f);
                }
            }
        }

        // 내리막 끝 수레 + 상자/통 + 첫 방에 물건들
        {
            // 휘는 구간(내리막 끝 ~ +14m)을 지난 곳부터 수레/상자를 벽 쪽에 놓는다 (길 가운데는 비워 둔다)
            float dStart = Mathf.Min(total - 12f, RampEndDistance(path, floor0) + 14f);
            int ic = StationAt(dStart);
            var cart = Spawn("PP_Mine_Cart_0" + (1 + _rng.Next(9)), gDeco, FloorAt(path, dStart) + path.right[ic] * (path.hw[ic] - 1.6f), Quaternion.LookRotation(path.dir[ic], Vector3.up), Vector3.one * 2.2f);
            if (cart != null) nDeco++;
            for (int k = 0; k < 6; k++)
            {
                float d = Mathf.Clamp(dStart + Rng(2f, 22f), 3f, total - 3f);
                int i = StationAt(d);
                float sg = R01() < 0.5f ? -1f : 1f;
                Vector3 at = FloorAt(path, d) + path.right[i] * (sg * (path.hw[i] - Rng(1.2f, 2.0f)));
                var o = Spawn(Pick("PP_Crate_Wooden_01", "PP_Crate_Wooden_03", "PP_Barrel_03", "PP_Barrel_01", "PP_Dynamite_Bundle_01"), gDeco, at, Quaternion.Euler(0, Rng(0, 360), 0), Vector3.one * Rng(2.2f, 2.8f));
                if (o != null) nDeco++;
            }
        }

        // 광맥: 큰 방 A(첫 번째)와 막다른 방
        int nodes = 0;
        var hallA = new List<int>(); var hallB = new List<int>();
        bool inHall = false; int hallIndex = 0;
        for (int i = 0; i < path.Count; i++)
        {
            bool h = path.hw[i] > 12f;
            if (h && !inHall) { inHall = true; hallIndex++; }
            if (!h && inHall) inHall = false;
            if (h) (hallIndex == 1 ? hallA : hallB).Add(i);
        }
        if (variant == 1)
        {
            nodes += PlaceOres(gOre, path, hallA, "copper_ore", "구리 광맥", 2, 8, 3, 5, 1.2f, 180f, 360f, 5);
            nodes += PlaceOres(gOre, path, hallA, "iron_ore", "철 광맥", 3, 6, 4, 7, 1.8f, 300f, 600f, 4);
            nodes += PlaceOres(gOre, path, hallA, "platinum_ore", "금 광맥", 4, 3, 3, 4, 3.5f, 420f, 900f, 3);
            nodes += PlaceOres(gOre, path, hallB, "iron_ore", "철 광맥", 3, 6, 4, 7, 1.8f, 300f, 600f, 3);
            nodes += PlaceOres(gOre, path, hallB, "platinum_ore", "금 광맥", 4, 3, 3, 4, 3.5f, 420f, 900f, 2);
            nodes += PlaceOres(gOre, path, hallB, "diamond", "다이아몬드 원석", 5, 1, 5, 6, 5f, 1200f, 1800f, 1);
        }
        else
        {
            // 수정 동굴: 금과 다이아몬드가 더 많다 (깊이 들어가는 보상)
            nodes += PlaceOres(gOre, path, hallA, "copper_ore", "구리 광맥", 2, 8, 3, 5, 1.2f, 180f, 360f, 4);
            nodes += PlaceOres(gOre, path, hallA, "iron_ore", "철 광맥", 3, 6, 4, 7, 1.8f, 300f, 600f, 4);
            nodes += PlaceOres(gOre, path, hallA, "platinum_ore", "금 광맥", 4, 3, 3, 4, 3.5f, 420f, 900f, 4);
            nodes += PlaceOres(gOre, path, hallA, "diamond", "다이아몬드 원석", 5, 1, 5, 6, 5f, 1200f, 1800f, 1);
            nodes += PlaceOres(gOre, path, hallB, "iron_ore", "철 광맥", 3, 6, 4, 7, 1.8f, 300f, 600f, 3);
            nodes += PlaceOres(gOre, path, hallB, "platinum_ore", "금 광맥", 4, 3, 3, 4, 3.5f, 420f, 900f, 4);
            nodes += PlaceOres(gOre, path, hallB, "diamond", "다이아몬드 원석", 5, 1, 5, 6, 5f, 1200f, 1800f, 2);
        }

        // 막다른 방 보물 상자 (2개)
        for (int k = 0; k < 2; k++)
        {
            int i = path.Count - 3 - k * 3;
            if (i < 0) continue;
            Vector3 at = path.pos[i] + path.right[i] * (k == 0 ? -3f : 3f);
            var chest = Spawn("PP_Treasure_Chest_0" + (1 + _rng.Next(4)) + "_" + (k == 0 ? "Gold" : "Silver"), gDeco, at, Quaternion.LookRotation(-path.dir[i], Vector3.up), Vector3.one * 2.4f);
            if (chest != null) nChest++;
        }

        // 입구 안쪽에 수레/레일이 보이도록 입구 문 앞 레일도 이어 둔다 (바깥 레일은 입구 빌더가 놓았다)
        sb.AppendLine($"cave: rails={nRail} supports={nSup} torches={nTorch} deco={nDeco} crystals={nCry} chests={nChest} ore nodes={nodes} hallA stations={hallA.Count} hallB stations={hallB.Count}");
        Physics.SyncTransforms();
        return sb.ToString();
    }

    /// <summary>
    /// 터널이 지상 타일을 가로지르는 구간(바닥은 땅 아래, 천장은 땅 위)에서, 터널 발자국 안에 완전히 들어가는 타일 삼각형을 지운다.
    /// 조금 작게 잘라서(반폭의 90%) 구멍 가장자리가 터널 벽 바깥면 안쪽에 숨는다. 이미 지운 타일에 다시 해도 안전하다.
    /// </summary>
    static int CutGroundHoles(Transform expansionRoot, Path path)
    {
        Physics.SyncTransforms();
        var crossing = new List<int>();
        for (int i = 0; i < path.Count; i++)
        {
            float gy = float.NaN;
            foreach (var hh in Physics.RaycastAll(new Vector3(path.pos[i].x, 120f, path.pos[i].z), Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore))
                if (hh.collider.name.StartsWith("EXP_Tile")) { gy = hh.point.y; break; }
            if (float.IsNaN(gy)) continue;
            if (path.pos[i].y < gy + 0.3f && path.pos[i].y + path.h[i] > gy - 0.6f) crossing.Add(i);
        }
        if (crossing.Count == 0) return 0;

        var gTiles = expansionRoot.Find("Ground");
        if (gTiles == null) return 0;
        int removed = 0;
        foreach (Transform tile in gTiles)
        {
            var mf = tile.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            // 터널 근처 타일만
            Bounds wb = tile.GetComponent<Renderer>() != null ? tile.GetComponent<Renderer>().bounds : new Bounds(tile.position, Vector3.one * 24f);
            bool near = false;
            foreach (int i in crossing) { Vector3 p = path.pos[i]; if (p.x > wb.min.x - 6f && p.x < wb.max.x + 6f && p.z > wb.min.z - 6f && p.z < wb.max.z + 6f) { near = true; break; } }
            if (!near) continue;

            Mesh m = mf.sharedMesh;
            Vector3[] v = m.vertices;
            int[] t = m.triangles;
            var keep = new List<int>(t.Length);
            for (int k = 0; k < t.Length; k += 3)
            {
                // 꼭짓점 하나라도 터널 발자국 안(반폭 85%)에 있으면 지운다 (남은 조각이 터널 안으로 삐져나와 길/시야를 막지 않게).
                // 구멍은 바위 껍질(안쪽 벽보다 1.3m 바깥)이 덮는다.
                bool anyIn = false;
                for (int c = 0; c < 3 && !anyIn; c++)
                {
                    Vector3 w = tile.TransformPoint(v[t[k + c]]);
                    foreach (int i in crossing)
                    {
                        float dx = w.x - path.pos[i].x, dz = w.z - path.pos[i].z;
                        float r = path.hw[i] * 0.85f;
                        if (dx * dx + dz * dz <= r * r) { anyIn = true; break; }
                    }
                }
                if (anyIn) { removed++; continue; }
                keep.Add(t[k]); keep.Add(t[k + 1]); keep.Add(t[k + 2]);
            }
            if (keep.Count == t.Length) continue;
            m.SetTriangles(keep, 0);
            m.RecalculateBounds();
            EditorUtility.SetDirty(m);
            var mc = tile.GetComponent<MeshCollider>();
            if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = m; }
        }
        return removed;
    }

    /// <summary>땅 위로 드러나는 터널 구간(천장이 땅보다 높은 곳) 둘레의 나무/바위/풀 같은 소품 묶음을 지운다 (입구, 타일, 경계, 산, 동굴 자신은 제외).</summary>
    static int ClearPropsAbove(Transform expansionRoot, Path path)
    {
        Physics.SyncTransforms();
        var groups = new HashSet<string> { "Props", "Stones&Rocks", "Vegetation", "Crystals&Ores&Veins", "Mushrooms", "FX" };
        var toDestroy = new HashSet<GameObject>();
        for (int i = 0; i < path.Count; i++)
        {
            float gy = float.NaN;
            foreach (var hh in Physics.RaycastAll(new Vector3(path.pos[i].x, 120f, path.pos[i].z), Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore))
                if (hh.collider.name.StartsWith("EXP_Tile")) { gy = hh.point.y; break; }
            if (float.IsNaN(gy)) { gy = 1.5f; }
            if (path.pos[i].y + path.h[i] < gy - 0.5f) continue;   // 천장이 땅 밑이면 위에 있는 것들은 그대로 둔다
            Vector3 c = path.pos[i] + Vector3.up * (path.h[i] * 0.5f);
            foreach (var col in Physics.OverlapSphere(c, path.hw[i] + 3.5f, ~0, QueryTriggerInteraction.Collide))
            {
                Transform t = col.transform;
                // 소품 묶음의 가장 위 자식 (그룹 바로 아래)을 찾는다
                Transform top = t;
                while (top != null && top.parent != null && !(groups.Contains(top.parent.name) && top.parent.parent == expansionRoot)) top = top.parent;
                if (top == null || top.parent == null || !(groups.Contains(top.parent.name) && top.parent.parent == expansionRoot)) continue;
                if (top.name.StartsWith("PP_Main_Entrance")) continue;
                toDestroy.Add(top.gameObject);
            }
        }
        int n = 0;
        foreach (var go in toDestroy) { if (go != null) { Object.DestroyImmediate(go); n++; } }
        return n;
    }

    static float RampEndDistance(Path path, float floor0)
    {
        for (int i = 0; i < path.Count; i++) if (path.pos[i].y <= DeepY + 0.2f) return path.dist[i];
        return 40f;
    }

    static Vector3 FloorAt(Path path, float d)
    {
        d = Mathf.Clamp(d, 0f, path.dist[path.Count - 1]);
        int i = 0;
        for (int k = 0; k < path.Count - 1; k++) { if (path.dist[k + 1] >= d) { i = k; break; } i = k; }
        float t = Mathf.InverseLerp(path.dist[i], path.dist[Mathf.Min(path.Count - 1, i + 1)], d);
        return Vector3.Lerp(path.pos[i], path.pos[Mathf.Min(path.Count - 1, i + 1)], t);
    }

    static readonly Dictionary<string, float> _maxDim = new Dictionary<string, float>();

    /// <summary>프리팹 가장 긴 변(배율 1일 때, m).</summary>
    static float MaxDim(string prefab)
    {
        if (_maxDim.TryGetValue(prefab, out float v)) return v;
        var pf = P(prefab);
        float d = 1f;
        if (pf != null)
        {
            var rs = pf.GetComponentsInChildren<MeshFilter>();
            Bounds b = new Bounds(); bool first = true;
            foreach (var mf in rs)
            {
                if (mf.sharedMesh == null) continue;
                Vector3 sc = mf.transform.lossyScale;
                Bounds mb = mf.sharedMesh.bounds;
                Vector3 size = new Vector3(mb.size.x * Mathf.Abs(sc.x), mb.size.y * Mathf.Abs(sc.y), mb.size.z * Mathf.Abs(sc.z));
                if (first) { b = new Bounds(Vector3.zero, size); first = false; } else b.Encapsulate(new Bounds(Vector3.zero, size));
            }
            if (!first) d = Mathf.Max(0.1f, b.size.x, b.size.y, b.size.z);
        }
        _maxDim[prefab] = d;
        return d;
    }

    /// <summary>가장 긴 변이 targetSize(m)가 되도록 크기를 맞춰서 놓는다.</summary>
    static GameObject SpawnSized(string prefab, Transform parent, Vector3 pos, Quaternion rot, float targetSize)
    {
        float s = targetSize / MaxDim(prefab);
        return Spawn(prefab, parent, pos, rot, Vector3.one * s);
    }

    static GameObject Spawn(string prefab, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var pf = P(prefab);
        if (pf == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.position = pos; go.transform.rotation = rot; go.transform.localScale = scale;
        return go;
    }

    static int PlaceOres(Transform parent, Path path, List<int> stations, string itemId, string label, int tier, int amount, int minHits, int maxHits, float hold, float minRegen, float maxRegen, int count)
    {
        var item = ItemCatalog.GetOrCreate().GetItem(itemId);
        if (item == null || stations.Count == 0) return 0;
        int made = 0;
        for (int k = 0; k < count; k++)
        {
            int i = stations[_rng.Next(stations.Count)];
            float sg = R01() < 0.5f ? -1f : 1f;
            float off = path.hw[i] - Rng(3.5f, 6.5f);
            Vector3 at = path.pos[i] + path.right[i] * (sg * off);
            var node = new GameObject(label.Replace(" ", "") + "_node");
            node.layer = 4;
            node.transform.SetParent(parent, false);
            node.transform.position = new Vector3(at.x, at.y - 0.2f, at.z);
            var sc = node.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 3.2f;
            var srn = node.AddComponent<SpecialResourceNode>();
            srn.Configure(label, item, amount, tier, minHits, maxHits, minRegen, maxRegen, "", hold);
            int seed = Mathf.RoundToInt(at.x * 13f + at.z * 7f);
            if (!OreCrystalConverter.Convert(srn, itemId, seed)) { Object.DestroyImmediate(node); continue; }
            foreach (Transform c in node.transform)
                foreach (var tr in c.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 0;
            EditorUtility.SetDirty(srn);
            made++;
        }
        return made;
    }
}
#endif
