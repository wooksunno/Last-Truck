#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Truck_finish.fbx의 새 부품을 게임에서 쓰는 트럭(Truck.prefab)에 합친다.
/// 메뉴: Tools/Last Truck/Truck: Truck_finish 부품 합치기
///
///  - 몸체(+범퍼/뒷문/적재함 아치·가로 봉 = Truck_Stick1/2), 왼쪽/오른쪽 문, 왼쪽/오른쪽 사이드미러: 메쉬만 Truck_finish 것으로 바꾼다.
///    (Assets/LastTruck/TruckModel의 메쉬 파일을 덮어쓰므로 프리팹의 머티리얼/셰이더/떨어지는 부품 설정/위치는 그대로)
///  - 새로 추가: 앞 유리(Model/Window), 문 유리(Door_L/Window, Door_R/Window), 거울면(Mirror_L/Glass, Mirror_R/Glass)
///    유리는 TruckGlass, 거울면은 TruckMirror 머티리얼. 문/미러의 자식이라 부품이 떨어지면 같이 떨어진다.
///  - 바퀴는 건드리지 않는다.
///
/// 크기/방향 변환은 TruckModelSetup과 같다 (FBX 단위 ×900, 배율 0.95, 앞 = +Z).
/// 부품 위치는 FBX 안에서 몸체 기준으로 계산하므로, 블렌더에서 문을 옮겨 놓았어도 정확한 자리에 붙는다.
/// 다시 실행해도 안전하다 (메쉬를 덮어쓰고, 유리 오브젝트는 있으면 갱신한다).
/// </summary>
public static class TruckFinishMerge
{
    const string FbxPath = "Assets/LastTruck/Truck_finish.fbx";
    const string OutDir = "Assets/LastTruck/TruckModel";
    const string PrefabPath = "Assets/LastTruck/Character/Prefabs/Truck.prefab";
    const string GlassMaterialPath = OutDir + "/TruckGlass.mat";
    const string MirrorMaterialPath = OutDir + "/TruckMirror.mat";

    const float FbxUnit = 900f;
    const float Scale = 0.95f;

    [MenuItem("Tools/Last Truck/Truck: Truck_finish 부품 합치기")]
    static void Menu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Truck_finish 합치기", "플레이 모드에서는 실행할 수 없습니다.", "확인");
            return;
        }
        if (!EditorUtility.DisplayDialog("Truck_finish 합치기",
                "Truck.prefab의 몸체/문/사이드미러 메쉬를 Truck_finish 것으로 바꾸고 유리/거울면을 추가합니다.\n" +
                "머티리얼, 셰이더, 부품 설정은 그대로 둡니다.", "실행", "취소"))
            return;

        string log = Run();
        Debug.Log("[TruckFinishMerge]\n" + log);
        EditorUtility.DisplayDialog("Truck_finish 합치기", log.Length > 1800 ? log.Substring(0, 1800) + "\n…(나머지는 콘솔)" : log, "확인");
    }

    // ---------------------------------------------------------------- 좌표 변환 (TruckModelSetup과 같은 규칙)

    /// <summary>모델 좌표(앞 = +X, 왼쪽 = +Z) → 트럭 좌표(앞 = +Z, 오른쪽 = +X) + 크기 조정.</summary>
    static Vector3 Bake(Vector3 modelPos) => new Vector3(-modelPos.z, modelPos.y, modelPos.x) * Scale;

    /// <summary>FBX 부품 메쉬의 정점을 "몸체 메쉬 기준 모델 미터 좌표"로 바꾸는 함수.</summary>
    class Space
    {
        readonly Transform _part, _body;
        public Space(Transform part, Transform body) { _part = part; _body = body; }
        public Vector3 Point(Vector3 v) => _body.InverseTransformPoint(_part.TransformPoint(v)) * FbxUnit;
        public Vector3 Normal(Vector3 n) => _body.InverseTransformDirection(_part.TransformDirection(n)).normalized;
    }

    // ---------------------------------------------------------------- 섬(떨어진 덩어리) 찾기

    class Island
    {
        public List<int> tris = new List<int>();
        public Bounds bounds;
        bool _init;
        public void Add(Vector3 p) { if (!_init) { bounds = new Bounds(p, Vector3.zero); _init = true; } else bounds.Encapsulate(p); }
    }

    static List<Island> FindIslands(Mesh mesh, Space space)
    {
        Vector3[] v = mesh.vertices;
        int[] t = mesh.triangles;
        var p = new Vector3[v.Length];
        for (int i = 0; i < v.Length; i++) p[i] = space.Point(v[i]);

        var weld = new Dictionary<Vector3Int, int>();
        var rep = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(p[i].x * 1000f), Mathf.RoundToInt(p[i].y * 1000f), Mathf.RoundToInt(p[i].z * 1000f));
            if (!weld.TryGetValue(key, out int r)) { weld[key] = i; r = i; }
            rep[i] = r;
        }

        var parent = new int[v.Length];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }

        for (int i = 0; i < t.Length; i += 3)
        {
            Union(rep[t[i]], rep[t[i + 1]]);
            Union(rep[t[i + 1]], rep[t[i + 2]]);
        }

        var byRoot = new Dictionary<int, Island>();
        for (int i = 0; i < t.Length; i += 3)
        {
            int root = Find(rep[t[i]]);
            if (!byRoot.TryGetValue(root, out Island isl)) { isl = new Island(); byRoot[root] = isl; }
            isl.tris.Add(i);
            for (int k = 0; k < 3; k++) isl.Add(p[t[i + k]]);
        }
        return new List<Island>(byRoot.Values);
    }

    // ---------------------------------------------------------------- 메쉬 만들기 / 저장

    static List<int> AllTris(Mesh m)
    {
        var all = new List<int>();
        int n = m.triangles.Length;
        for (int i = 0; i < n; i += 3) all.Add(i);
        return all;
    }

    static Mesh Extract(Mesh src, IEnumerable<int> triStarts, Space space, string name)
    {
        Vector3[] v = src.vertices;
        Vector3[] n = src.normals;
        Vector2[] uv = src.uv;
        int[] t = src.triangles;

        var remap = new Dictionary<int, int>();
        var nv = new List<Vector3>();
        var nn = new List<Vector3>();
        var nuv = new List<Vector2>();
        var nt = new List<int>();

        foreach (int s in triStarts)
        {
            for (int k = 0; k < 3; k++)
            {
                int old = t[s + k];
                if (!remap.TryGetValue(old, out int idx))
                {
                    idx = nv.Count;
                    remap[old] = idx;
                    nv.Add(Bake(space.Point(v[old])));
                    Vector3 nm = space.Normal(n != null && n.Length > old ? n[old] : Vector3.up);
                    nn.Add(new Vector3(-nm.z, nm.y, nm.x).normalized);
                    nuv.Add(uv != null && uv.Length > old ? uv[old] : Vector2.zero);
                }
                nt.Add(idx);
            }
        }

        var mesh = new Mesh { name = name };
        if (nv.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(nv);
        mesh.SetNormals(nn);
        mesh.SetUVs(0, nuv);
        mesh.SetTriangles(nt, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    /// <summary>여러 조각을 한 메쉬로 합친다 (같은 머티리얼 하나).</summary>
    static Mesh Combine(List<Mesh> pieces, string name)
    {
        if (pieces.Count == 1) { pieces[0].name = name; return pieces[0]; }
        var ci = new CombineInstance[pieces.Count];
        int verts = 0;
        for (int i = 0; i < pieces.Count; i++) { ci[i] = new CombineInstance { mesh = pieces[i] }; verts += pieces[i].vertexCount; }
        var mesh = new Mesh { name = name };
        if (verts > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.CombineMeshes(ci, true, false);
        mesh.RecalculateBounds();
        foreach (Mesh p in pieces) Object.DestroyImmediate(p);
        return mesh;
    }

    /// <summary>모든 삼각형의 뒷면을 추가한다 (정점을 복사해 법선을 뒤집고, 감는 방향을 반대로).</summary>
    static Mesh DoubleSided(Mesh src)
    {
        Vector3[] v = src.vertices;
        Vector3[] n = src.normals;
        Vector2[] uv = src.uv;
        int[] t = src.triangles;
        int count = v.Length;

        var nv = new Vector3[count * 2];
        var nn = new Vector3[count * 2];
        var nuv = new Vector2[count * 2];
        for (int i = 0; i < count; i++)
        {
            nv[i] = nv[i + count] = v[i];
            Vector3 normal = n != null && n.Length == count ? n[i] : Vector3.up;
            nn[i] = normal;
            nn[i + count] = -normal;
            nuv[i] = nuv[i + count] = uv != null && uv.Length == count ? uv[i] : Vector2.zero;
        }

        var nt = new int[t.Length * 2];
        for (int i = 0; i < t.Length; i += 3)
        {
            nt[i] = t[i]; nt[i + 1] = t[i + 1]; nt[i + 2] = t[i + 2];
            nt[t.Length + i] = t[i] + count;          // 뒷면: 감는 방향 반대
            nt[t.Length + i + 1] = t[i + 2] + count;
            nt[t.Length + i + 2] = t[i + 1] + count;
        }

        var mesh = new Mesh { name = src.name };
        if (nv.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = nv;
        mesh.normals = nn;
        mesh.uv = nuv;
        mesh.triangles = nt;
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        Object.DestroyImmediate(src);
        return mesh;
    }

    /// <summary>같은 이름의 메쉬 파일이 있으면 내용만 덮어쓴다 (프리팹의 참조가 그대로 유지됨).</summary>
    static Mesh Save(Mesh mesh)
    {
        string path = OutDir + "/" + mesh.name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Transform FindChild(GameObject fbx, string name)
    {
        foreach (Transform t in fbx.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    static Mesh MeshOf(Transform t)
    {
        var f = t != null ? t.GetComponent<MeshFilter>() : null;
        return f != null ? f.sharedMesh : null;
    }

    // ---------------------------------------------------------------- 실행

    public static string Run()
    {
        var log = new StringBuilder();
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbx == null) return "FBX를 찾지 못했어요: " + FbxPath;

        var glassMat = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        var mirrorMat = AssetDatabase.LoadAssetAtPath<Material>(MirrorMaterialPath);
        if (glassMat == null || mirrorMat == null) return "유리/거울 머티리얼을 찾지 못했어요: " + GlassMaterialPath + ", " + MirrorMaterialPath;

        Transform body = FindChild(fbx, "Truck");
        if (MeshOf(body) == null) return "Truck_finish 안에서 몸체(Truck) 메쉬를 찾지 못했어요.";
        var bodySpace = new Space(body, body);

        // 1) 몸체: 범퍼 / 뒷문을 떼어 내고 나머지가 몸체.
        //    Truck_finish에서는 적재함 틀이 따로 빠져 있다: Truck_Stick1 = 세로 아치 5개, Truck_Stick2 = 가로 봉 3개
        //    → 아치는 예전처럼 Hoop_1~5(떨어지는 부품)로, 가로 봉은 몸체에 합친다.
        Mesh bodySrc = MeshOf(body);
        List<Island> islands = FindIslands(bodySrc, bodySpace);
        var hoops = new List<(Island isl, Mesh mesh, Space space)>();
        Island bumper = null, tailgate = null;
        var rest = new List<int>();
        foreach (Island isl in islands)
        {
            Vector3 c = isl.bounds.center, s = isl.bounds.size;
            bool isHoop = s.x < 0.2f && s.y > 1.4f && s.z > 2.5f && c.y > 2.5f;
            bool isBumper = c.x > 4.0f && s.z > 3.0f && s.y < 0.6f;
            bool isTailgate = c.x < -3.5f && s.z > 2.8f && s.y > 0.7f && s.x < 0.3f;
            if (isHoop) hoops.Add((isl, bodySrc, bodySpace));
            else if (isBumper && bumper == null) bumper = isl;
            else if (isTailgate && tailgate == null) tailgate = isl;
            else rest.AddRange(isl.tris);
        }

        var bodyPieces = new List<Mesh> { Extract(bodySrc, rest, bodySpace, "Truck_Body") };
        foreach (string stickName in new[] { "Truck_Stick1", "Truck_Stick2" })
        {
            Transform st = FindChild(fbx, stickName);
            Mesh sm = MeshOf(st);
            if (sm == null) continue;
            var sSpace = new Space(st, body);
            var sRest = new List<int>();
            foreach (Island isl in FindIslands(sm, sSpace))
            {
                Vector3 c = isl.bounds.center, s = isl.bounds.size;
                bool isHoop = s.x < 0.2f && s.y > 1.4f && s.z > 2.5f && c.y > 2.5f;
                if (isHoop) hoops.Add((isl, sm, sSpace));
                else sRest.AddRange(isl.tris);
            }
            if (sRest.Count > 0) bodyPieces.Add(Extract(sm, sRest, sSpace, stickName));
            log.AppendLine($"  {stickName}: 아치 {hoops.Count}개까지 찾음, 나머지는 몸체에 합침");
        }
        hoops.Sort((a, b) => b.isl.bounds.center.x.CompareTo(a.isl.bounds.center.x));   // 앞(운전석 쪽)부터 뒤로
        log.AppendLine($"몸체 덩어리 {islands.Count}개 → 아치 {hoops.Count}, 범퍼 {(bumper != null ? "O" : "X")}, 뒷문 {(tailgate != null ? "O" : "X")}");

        // 적재함 바닥/옆판처럼 한쪽 면만 있는 판은 반대쪽에서 보면 투명하게 사라진다 (블렌더에서는 양면으로 보여서 티가 안 남)
        // → 몸체는 앞/뒤 면을 모두 넣어 어느 쪽에서 봐도 보이게 한다.
        Save(DoubleSided(Combine(bodyPieces, "Truck_Body")));
        if (bumper != null) Save(Extract(bodySrc, bumper.tris, bodySpace, "Truck_Bumper"));
        else log.AppendLine("  ! 범퍼 덩어리를 못 찾아 기존 범퍼 메쉬를 그대로 둡니다.");
        if (tailgate != null) Save(Extract(bodySrc, tailgate.tris, bodySpace, "Truck_Tailgate"));
        else log.AppendLine("  ! 뒷문 덩어리를 못 찾아 기존 뒷문 메쉬를 그대로 둡니다.");
        for (int i = 0; i < hoops.Count && i < 5; i++)
            Save(Extract(hoops[i].mesh, hoops[i].isl.tris, hoops[i].space, "Truck_Hoop_" + (i + 1)));
        if (hoops.Count != 5) log.AppendLine($"  ! 적재함 아치가 {hoops.Count}개입니다 (기존 5개). 확인해 주세요.");

        // 2) 문 / 사이드미러 (+ 유리 / 거울면): FBX 안에서 몸체 기준 위치로 굽는다
        var extra = new Dictionary<string, Mesh>();   // 새로 붙일 유리/거울면 메쉬
        var map = new[]
        {
            ("Truck_Door_Left",   "Truck_Door_L",   "Truck_Door_Left_Window",  "Truck_Door_L_Window"),
            ("Truck_Door_Right",  "Truck_Door_R",   "Truck_Door_Right_Window", "Truck_Door_R_Window"),
            ("Truck_Mirror_Left", "Truck_Mirror_L", "Truck_Mirror_Left.001",   "Truck_Mirror_L_Glass"),
            ("Truck_Mirror_Right","Truck_Mirror_R", "Truck_Mirror_Right.001",  "Truck_Mirror_R_Glass"),
        };
        foreach (var (src, dst, glassSrc, glassDst) in map)
        {
            Transform t = FindChild(fbx, src);
            Mesh m = MeshOf(t);
            if (m == null) log.AppendLine("  ! 메쉬 없음: " + src);
            else { Save(Extract(m, AllTris(m), new Space(t, body), dst)); log.AppendLine("  교체: " + dst); }

            Transform g = FindChild(fbx, glassSrc);
            Mesh gm = MeshOf(g);
            if (gm == null) log.AppendLine("  ! 메쉬 없음: " + glassSrc);
            else extra[glassDst] = Save(Extract(gm, AllTris(gm), new Space(g, body), glassDst));
        }

        Transform windowT = FindChild(fbx, "Truck_Window");
        Mesh windowSrc = MeshOf(windowT);
        if (windowSrc == null) log.AppendLine("  ! 메쉬 없음: Truck_Window");
        else extra["Truck_Window"] = Save(Extract(windowSrc, AllTris(windowSrc), new Space(windowT, body), "Truck_Window"));

        AssetDatabase.SaveAssets();

        // 3) 프리팹: 유리 / 거울면 오브젝트 추가 (기존 부품의 설정은 건드리지 않는다)
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform model = root.transform.Find("Model");
            if (model == null) return log + "\n!! Truck.prefab에 Model이 없어요.";

            AddVisual(model, "Model", "Window", extra, "Truck_Window", glassMat, log);
            AddVisual(model.Find("Door_L"), "Door_L", "Window", extra, "Truck_Door_L_Window", glassMat, log);
            AddVisual(model.Find("Door_R"), "Door_R", "Window", extra, "Truck_Door_R_Window", glassMat, log);
            AddVisual(model.Find("Mirror_L"), "Mirror_L", "Glass", extra, "Truck_Mirror_L_Glass", mirrorMat, log);
            AddVisual(model.Find("Mirror_R"), "Mirror_R", "Glass", extra, "Truck_Mirror_R_Glass", mirrorMat, log);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        log.AppendLine("\n완료. 씬의 트럭(Truck 프리팹)에 자동으로 반영됩니다.");
        return log.ToString();
    }

    /// <summary>parent 아래에 childName 오브젝트를 만들거나 갱신해서 메쉬/머티리얼을 넣는다.</summary>
    static void AddVisual(Transform parent, string parentName, string childName, Dictionary<string, Mesh> meshes, string meshName, Material mat, StringBuilder log)
    {
        if (parent == null) { log.AppendLine($"  ! 프리팹에 {parentName}이(가) 없어 {meshName}을(를) 붙이지 못했어요."); return; }
        if (!meshes.TryGetValue(meshName, out Mesh mesh) || mesh == null) return;

        Transform child = parent.Find(childName);
        if (child == null)
        {
            child = new GameObject(childName).transform;
            child.SetParent(parent, false);
        }
        // 부품 메쉬는 모두 Model 원점 기준으로 구워져 있다 → 부모(문/미러)와 같은 자리에 둔다
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        child.gameObject.layer = parent.gameObject.layer;

        var mf = child.GetComponent<MeshFilter>();
        if (mf == null) mf = child.gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = child.GetComponent<MeshRenderer>();
        if (mr == null) mr = child.gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 유리/거울면은 그림자를 만들지 않는다
        mr.receiveShadows = false;

        log.AppendLine($"  추가: {parentName}/{childName} ({mat.name})");
    }
}
#endif
