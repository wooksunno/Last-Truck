#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using CraftingSystem;
using LastTruck;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Truck_.fbx(군용 6륜 트럭)를 기존 트럭(Truck.prefab) 모델로 교체한다. 이동/물리 스크립트는 그대로 두고 눈에 보이는 부분만 바꾼다.
///
///  1) FBX의 통짜 메쉬를 부품별 메쉬로 쪼갠다 (몸체 / 범퍼 / 뒷문 / 적재함 아치 5개 / 바퀴 6개 / 문 / 사이드미러).
///     크기(×0.7)와 방향(앞 = +Z)을 메쉬에 미리 반영해서 저장한다 → Assets/LastTruck/TruckModel/
///  2) Truck.prefab에서 옛 몸체/타이어/문/계기판을 지우고 새 부품을 "Model" 아래에 붙인다.
///     바퀴 콜라이더 4개(앞 2 + 뒤 2)는 그대로 쓰고, 6륜의 나머지 바퀴는 TruckWheelFollower로 눈에만 보이게 돌린다.
///  3) 떨어지는 부품에 TruckDetachablePart(내구도 기준 비율 등)를, 트럭 루트에 TruckDamageVisuals를 붙인다.
///
/// 다시 실행해도 안전하다 (Model/ 이하를 지우고 새로 만든다). 메뉴: Tools/Last Truck/Truck: 군용 트럭 모델로 교체
/// </summary>
public static class TruckModelSetup
{
    const string FbxPath = "Assets/LastTruck/Truck_.fbx";
    const string AtlasPath = "Assets/LastTruck/Truck_Atlas_3.png";
    const string OldMaterialPath = "Assets/LastTruck/DrivableCars/Material/Color.mat";
    const string OutDir = "Assets/LastTruck/TruckModel";
    const string PrefabPath = "Assets/LastTruck/Character/Prefabs/Truck.prefab";

    const float FbxUnit = 900f;      // FBX 루트 자식의 스케일 (메쉬 정점 × 900 = 모델 미터)
    const float Scale = 0.7f;        // 기존 트럭(길이 약 6.1m)과 비슷한 크기로
    public const float GroundY = -0.23f;   // Model을 트럭 루트 기준 이만큼 내린다 (바퀴 바닥이 땅에 닿게)

    // ---------------------------------------------------------------- 메쉬 쪼개기

    class Island
    {
        public List<int> tris = new List<int>();   // 삼각형 시작 인덱스(triangles 배열 기준)
        public Bounds bounds;                      // 모델 미터 단위 (정점 × 900)
        bool _init;
        public void Add(Vector3 p) { if (!_init) { bounds = new Bounds(p, Vector3.zero); _init = true; } else bounds.Encapsulate(p); }
    }

    static List<Island> FindIslands(Mesh mesh)
    {
        Vector3[] v = mesh.vertices;
        int[] t = mesh.triangles;

        var weld = new Dictionary<Vector3Int, int>();
        var rep = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(v[i].x * FbxUnit * 1000f), Mathf.RoundToInt(v[i].y * FbxUnit * 1000f), Mathf.RoundToInt(v[i].z * FbxUnit * 1000f));
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
            for (int k = 0; k < 3; k++) isl.Add(v[t[i + k]] * FbxUnit);
        }
        return new List<Island>(byRoot.Values);
    }

    /// <summary>모델 좌표(앞 = +X, 왼쪽 = +Z) → 트럭 좌표(앞 = +Z, 오른쪽 = +X) + 크기 조정.</summary>
    static Vector3 Bake(Vector3 modelPos) => new Vector3(-modelPos.z, modelPos.y, modelPos.x) * Scale;

    /// <summary>삼각형 시작 인덱스 목록으로 새 메쉬를 만든다. pivotModel은 모델 좌표 기준 새 원점.</summary>
    static Mesh Extract(Mesh src, IEnumerable<int> triStarts, Vector3 pivotModel, string name)
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
                    nv.Add(Bake(v[old] * FbxUnit - pivotModel));
                    Vector3 nm = n != null && n.Length > old ? n[old] : Vector3.up;
                    nn.Add(new Vector3(-nm.z, nm.y, nm.x).normalized);
                    nuv.Add(uv != null && uv.Length > old ? uv[old] : Vector2.zero);
                }
                nt.Add(idx);
            }
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(nv);
        mesh.SetNormals(nn);
        mesh.SetUVs(0, nuv);
        mesh.SetTriangles(nt, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

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

    static Mesh SourceMesh(GameObject fbx, string childName)
    {
        Transform t = fbx.transform.Find(childName);
        return t != null ? t.GetComponent<MeshFilter>().sharedMesh : null;
    }

    // ---------------------------------------------------------------- 만들 부품 목록

    class PartInfo
    {
        public string name;
        public Mesh mesh;
        public Vector3 position;            // Model 기준 위치 (바퀴는 허브 중심, 나머지는 0)
        public bool wheel;
    }

    static List<PartInfo> BuildParts(GameObject fbx, StringBuilder log)
    {
        var parts = new List<PartInfo>();

        // 1) 본체 메쉬: 범퍼 / 뒷문 / 적재함 아치를 떼어 내고 나머지가 몸체
        Mesh bodySrc = SourceMesh(fbx, "Truck");
        var islands = FindIslands(bodySrc);
        var hoops = new List<Island>();
        Island bumper = null, tailgate = null;
        var rest = new List<int>();
        foreach (Island isl in islands)
        {
            Vector3 c = isl.bounds.center, s = isl.bounds.size;
            bool isHoop = s.x < 0.2f && s.y > 1.4f && s.z > 2.5f && c.y > 2.5f;
            bool isBumper = c.x > 4.0f && s.z > 3.0f && s.y < 0.6f;
            bool isTailgate = c.x < -3.5f && s.z > 2.8f && s.y > 0.7f && s.x < 0.3f;
            if (isHoop) hoops.Add(isl);
            else if (isBumper && bumper == null) bumper = isl;
            else if (isTailgate && tailgate == null) tailgate = isl;
            else rest.AddRange(isl.tris);
        }
        hoops.Sort((a, b) => b.bounds.center.x.CompareTo(a.bounds.center.x));   // 앞(운전석 쪽)부터 뒤로
        log.AppendLine("본체 섬 " + islands.Count + "개 → 아치 " + hoops.Count + ", 범퍼 " + (bumper != null) + ", 뒷문 " + (tailgate != null));

        parts.Add(new PartInfo { name = "Body", mesh = Save(Extract(bodySrc, rest, Vector3.zero, "Truck_Body")) });
        if (bumper != null) parts.Add(new PartInfo { name = "Bumper", mesh = Save(Extract(bodySrc, bumper.tris, Vector3.zero, "Truck_Bumper")) });
        if (tailgate != null) parts.Add(new PartInfo { name = "Tailgate", mesh = Save(Extract(bodySrc, tailgate.tris, Vector3.zero, "Truck_Tailgate")) });
        for (int i = 0; i < hoops.Count; i++)
            parts.Add(new PartInfo { name = "Hoop_" + (i + 1), mesh = Save(Extract(bodySrc, hoops[i].tris, Vector3.zero, "Truck_Hoop_" + (i + 1))) });

        // 2) 문 / 사이드미러: 통째로 (왼쪽 = 모델 +Z = 트럭 -X)
        foreach (var (child, partName) in new[] { ("Truck_Door_Left", "Door_L"), ("Truck_Door_Right", "Door_R"), ("Truck_Mirror_Left", "Mirror_L"), ("Truck_Mirror_Right", "Mirror_R") })
        {
            Mesh m = SourceMesh(fbx, child);
            if (m == null) { log.AppendLine("!! 메쉬 없음: " + child); continue; }
            var all = new List<int>();
            for (int i = 0; i < m.triangles.Length; i += 3) all.Add(i);
            parts.Add(new PartInfo { name = partName, mesh = Save(Extract(m, all, Vector3.zero, "Truck_" + partName)) });
        }

        // 3) 바퀴: 한 메쉬에 들어 있는 두 바퀴를 차축 위치로 갈라서 바퀴마다 메쉬를 만든다 (허브 중심이 원점)
        var axles = new Dictionary<string, float[]>
        {
            { "Truck_Wheel_Front_Left", new[] { 3.16f } }, { "Truck_Wheel_Front_Right", new[] { 3.16f } },
            { "Truck_Wheel_Back_Left", new[] { -1.55f, -3.14f } }, { "Truck_Wheel_Back_Right", new[] { -1.55f, -3.14f } },
        };
        foreach (var kv in axles)
        {
            Mesh m = SourceMesh(fbx, kv.Key);
            if (m == null) { log.AppendLine("!! 메쉬 없음: " + kv.Key); continue; }
            var wheelIslands = FindIslands(m);
            var groups = new List<Island>[kv.Value.Length];
            for (int i = 0; i < groups.Length; i++) groups[i] = new List<Island>();
            foreach (Island isl in wheelIslands)
            {
                int best = 0;
                for (int i = 1; i < kv.Value.Length; i++)
                    if (Mathf.Abs(isl.bounds.center.x - kv.Value[i]) < Mathf.Abs(isl.bounds.center.x - kv.Value[best])) best = i;
                groups[best].Add(isl);
            }

            for (int g = 0; g < groups.Length; g++)
            {
                if (groups[g].Count == 0) continue;
                // 가장 큰(삼각형 많은) 섬 = 타이어. 그 중심이 회전 중심.
                Island tire = groups[g][0];
                foreach (Island isl in groups[g]) if (isl.tris.Count > tire.tris.Count) tire = isl;
                Vector3 pivot = tire.bounds.center;
                var tris = new List<int>();
                foreach (Island isl in groups[g]) tris.AddRange(isl.tris);

                // 이름은 실제 위치로 짓는다 (FBX의 Left/Right 이름은 앞바퀴와 뒷바퀴가 서로 반대라 믿을 수 없다)
                Vector3 baked = Bake(pivot);
                string side = baked.x < 0f ? "L" : "R";
                string axle = kv.Value[g] > 0f ? "F" : (g == 0 ? "M" : "R");
                string wheelName = "Wheel_" + axle + "_" + side;
                parts.Add(new PartInfo { name = wheelName, mesh = Save(Extract(m, tris, pivot, "Truck_" + wheelName)), position = baked, wheel = true });
            }
        }

        foreach (PartInfo p in parts)
            log.AppendLine("  " + p.name + (p.wheel ? " @ " + p.position.ToString("F2") : "") + " verts=" + p.mesh.vertexCount);
        return parts;
    }

    static Material BuildMaterial()
    {
        string path = OutDir + "/Truck_Military.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var template = AssetDatabase.LoadAssetAtPath<Material>(OldMaterialPath);
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        if (mat == null)
        {
            // 월드의 다른 물체와 같은 만화풍 셰이더(ToonWorld)로 맞춘다: 기존 트럭 머티리얼을 복사해서 텍스처만 바꾼다
            mat = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = "Truck_Military";
            AssetDatabase.CreateAsset(mat, path);
        }
        if (atlas != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", atlas);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", atlas);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------------------------------------------------------------- 부품별 떨어지는 방식

    static void ConfigureDetach(TruckDetachablePart d, string name)
    {
        switch (name)
        {
            case "Mirror_R": d.detachBelow = 0.88f; d.debrisMass = 4f;  d.kickLocal = new Vector3(3.5f, 2.5f, -1f);  d.spin = 8f; break;
            case "Mirror_L": d.detachBelow = 0.80f; d.debrisMass = 4f;  d.kickLocal = new Vector3(-3.5f, 2.5f, -1f); d.spin = 8f; break;
            case "Bumper":   d.detachBelow = 0.70f; d.debrisMass = 60f; d.kickLocal = new Vector3(0.5f, 1.5f, 3f);   d.spin = 2f; break;
            case "Door_L":   d.detachBelow = 0.60f; d.debrisMass = 40f; d.kickLocal = new Vector3(-4f, 3f, -1.5f);   d.spin = 3f; break;
            case "Door_R":   d.detachBelow = 0.50f; d.debrisMass = 40f; d.kickLocal = new Vector3(4f, 3f, -1.5f);    d.spin = 3f; break;
            case "Tailgate": d.detachBelow = 0.55f; d.debrisMass = 50f; d.kickLocal = new Vector3(0f, 2f, -4f);      d.spin = 2f; break;
            case "Hoop_1":   d.detachBelow = 0.44f; d.debrisMass = 15f; d.kickLocal = new Vector3(3.5f, 4f, 0f);     d.spin = 3f; break;
            case "Hoop_2":   d.detachBelow = 0.36f; d.debrisMass = 15f; d.kickLocal = new Vector3(-3.5f, 4f, 0f);    d.spin = 3f; break;
            case "Hoop_3":   d.detachBelow = 0.28f; d.debrisMass = 15f; d.kickLocal = new Vector3(3f, 4.5f, 0f);     d.spin = 3f; break;
            case "Hoop_4":   d.detachBelow = 0.20f; d.debrisMass = 15f; d.kickLocal = new Vector3(-3f, 4.5f, 0f);    d.spin = 3f; break;
            case "Hoop_5":   d.detachBelow = 0.12f; d.debrisMass = 15f; d.kickLocal = new Vector3(3f, 5f, 0f);      d.spin = 3f; break;
        }
    }

    static bool IsDetachable(string name) =>
        name.StartsWith("Door") || name.StartsWith("Mirror") || name.StartsWith("Hoop") || name == "Bumper" || name == "Tailgate";

    // ---------------------------------------------------------------- 프리팹 수정

    public static string Run()
    {
        var log = new StringBuilder();
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbx == null) return "FBX를 찾지 못했어요: " + FbxPath;
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/LastTruck", "TruckModel");

        List<PartInfo> parts = BuildParts(fbx, log);
        Material material = BuildMaterial();
        AssetDatabase.SaveAssets();

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            // 옛 모델 제거: 루트 몸체 메쉬, 타이어/문/계기판 (바퀴 콜라이더 묶음 "Collider" 아래는 건드리지 않는다)
            DestroyComponent<MeshRenderer>(root);
            DestroyComponent<MeshFilter>(root);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform c = root.transform.GetChild(i);
                if (c.name.StartsWith("Pick Up_7") || c.name == "Model")
                {
                    log.AppendLine("삭제: " + c.name);
                    Object.DestroyImmediate(c.gameObject);
                }
            }

            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localPosition = new Vector3(0f, GroundY, 0f);

            var made = new Dictionary<string, Transform>();
            foreach (PartInfo p in parts)
            {
                var go = new GameObject(p.name);
                go.transform.SetParent(model, false);
                go.transform.localPosition = p.position;
                go.AddComponent<MeshFilter>().sharedMesh = p.mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                if (IsDetachable(p.name)) ConfigureDetach(go.AddComponent<TruckDetachablePart>(), p.name);
                made[p.name] = go.transform;
            }

            // 바퀴 콜라이더: 앞 2개는 앞차축에, 뒤 2개는 중간·뒤 차축의 가운데에 (물리는 기존과 같은 4륜)
            Transform colliderRoot = root.transform.Find("Collider");
            var tm = root.GetComponent<TruckMove>();
            WheelCollider fl = null, fr = null, bl = null, br = null;
            foreach (WheelCollider wc in colliderRoot.GetComponentsInChildren<WheelCollider>(true))
            {
                string n = wc.name;
                if (n.EndsWith("FL")) fl = wc; else if (n.EndsWith("FR")) fr = wc;
                else if (n.EndsWith("BL") || n.EndsWith("RL")) bl = wc; else if (n.EndsWith("BR") || n.EndsWith("RR")) br = wc;
            }
            if (fl == null || fr == null || bl == null || br == null)
                return log + "\n!! 바퀴 콜라이더(FL/FR/BL/BR)를 찾지 못했어요. 프리팹이 이미 바뀌었는지 확인하세요.";

            float y = fl.transform.localPosition.y;   // 서스펜션 높이는 기존 값 그대로
            Vector3 pFL = made["Wheel_F_L"].localPosition, pFR = made["Wheel_F_R"].localPosition;
            Vector3 pML = made["Wheel_M_L"].localPosition, pRL = made["Wheel_R_L"].localPosition;
            Vector3 pMR = made["Wheel_M_R"].localPosition, pRR = made["Wheel_R_R"].localPosition;
            float rearZ = (pML.z + pRL.z) * 0.5f;
            fl.transform.localPosition = new Vector3(pFL.x, y, pFL.z);
            fr.transform.localPosition = new Vector3(pFR.x, y, pFR.z);
            bl.transform.localPosition = new Vector3(pML.x, y, rearZ);
            br.transform.localPosition = new Vector3(pMR.x, y, rearZ);
            fl.name = "Wheel FL"; fr.name = "Wheel FR"; bl.name = "Wheel RL"; br.name = "Wheel RR";

            // 바퀴 6개 모두 TruckWheelFollower가 콜라이더를 따라 돌린다. (TruckMove.Ani_Wheels는 운전 중에만 모델을 움직여서,
            // 내리거나 멈춘 뒤에는 앞바퀴가 마지막 방향/높이로 굳어 이상하게 보였다 → 항상 따라가게 한다)
            for (int i = 0; i < tm.wheels.Count; i++)
            {
                TruckMove.Wheel w = tm.wheels[i];
                w.wheelModel = null;
                tm.wheels[i] = w;
            }
            AddFollower(made["Wheel_F_L"], fl, Vector3.zero);
            AddFollower(made["Wheel_F_R"], fr, Vector3.zero);
            AddFollower(made["Wheel_M_L"], bl, new Vector3(0f, 0f, pML.z - rearZ));
            AddFollower(made["Wheel_R_L"], bl, new Vector3(0f, 0f, pRL.z - rearZ));
            AddFollower(made["Wheel_M_R"], br, new Vector3(0f, 0f, pMR.z - rearZ));
            AddFollower(made["Wheel_R_R"], br, new Vector3(0f, 0f, pRR.z - rearZ));

            // 몸체 충돌 상자: 높이는 기존 그대로, 가로/세로 길이만 새 모델에 맞춘다
            var box = root.GetComponent<BoxCollider>();
            if (box != null)
            {
                Bounds b = new Bounds();
                bool any = false;
                foreach (var kv in made)
                {
                    if (kv.Key.StartsWith("Wheel") || kv.Key.StartsWith("Mirror") || kv.Key.StartsWith("Hoop")) continue;
                    Bounds mb = kv.Value.GetComponent<MeshFilter>().sharedMesh.bounds;
                    if (!any) { b = mb; any = true; } else b.Encapsulate(mb);
                }
                box.center = new Vector3(0f, box.center.y, b.center.z);
                box.size = new Vector3(Mathf.Min(b.size.x, 2.6f), box.size.y, b.size.z);
                log.AppendLine("BoxCollider center=" + box.center + " size=" + box.size);
            }

            var obstacle = root.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle != null && box != null) { obstacle.center = box.center; obstacle.size = box.size; }

            if (root.GetComponent<TruckDamageVisuals>() == null) root.AddComponent<TruckDamageVisuals>();

            // 부착물(포탑/엔진/전면/장갑)이 붙는 자리. 위치를 옮기면 장착 모델도 같이 옮겨진다 (트럭 기준 좌표).
            AddMount(root.transform, "Mount_Roof", new Vector3(0f, 2.05f, 0.85f));        // 운전석 지붕 가운데: 포탑
            AddMount(root.transform, "Mount_Front", new Vector3(0f, 0.55f, 3.12f));       // 범퍼 앞: 강철가시 범퍼 / 분쇄 휠
            AddMount(root.transform, "Mount_Bed", new Vector3(0f, 1.2f, -1.7f));          // 적재함 가운데: 자동 포탑
            AddMount(root.transform, "Mount_Engine", new Vector3(0f, 1.32f, 2.1f));       // 본네트 위: 엔진 흡기구
            AddMount(root.transform, "Mount_ArmorL", new Vector3(-0.97f, 1.25f, 1.08f));  // 왼쪽 문 바깥: 장갑판
            AddMount(root.transform, "Mount_ArmorR", new Vector3(0.97f, 1.25f, 1.08f));   // 오른쪽 문 바깥: 장갑판

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        log.AppendLine("완료: " + PrefabPath);
        return log.ToString();
    }

    static void AddMount(Transform root, string name, Vector3 localPosition)
    {
        Transform t = root.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = localPosition;   // 이미 있으면 사용자가 옮겨 둔 위치를 그대로 둔다
        }
    }

    static void AddFollower(Transform wheel, WheelCollider source, Vector3 offset)
    {
        var f = wheel.gameObject.AddComponent<TruckWheelFollower>();
        f.source = source;
        f.localOffset = offset;
    }

    static void DestroyComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null) Object.DestroyImmediate(c);
    }

    [MenuItem("Tools/Last Truck/Truck: 군용 트럭 모델로 교체")]
    static void Menu() { Debug.Log("[TruckModelSetup]\n" + Run()); }
}
#endif
