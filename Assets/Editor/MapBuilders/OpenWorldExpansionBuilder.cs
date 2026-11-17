#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using CraftingSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 시작 지역(구매 에셋) 북쪽과 동쪽의 빈 곳에 새 땅 3구역을 깐다 (Map/10_Expansion).
///
///  A 푸른 초원 (북쪽, 시작 지역 바로 위)   : 밝은 연두 들판, 자작나무 숲, 연못, 버려진 야영지 → 나무 / 약초
///  B 바위 고원 (북동쪽, 채석장 산맥 아래)  : 회색 바위 땅, 큰 암반, 선돌 원형 유적, 광산 입구 → 돌 / 구리·철·금 광맥
///  C 안개 늪   (동쪽, 호숫가)              : 짙은 이끼색 땅, 웅덩이, 고사목, 큰 버섯, 독약초, 무너진 오두막, 안개 → 독약초 / 기름
///
///  - 시작 지역 북쪽 끝(x -40~50)은 원래 허공으로 열려 있어서 그대로 이어 붙인다. 동쪽은 지하 동굴 지대(x 170~360) 너머에 땅을 깔고
///    북쪽 길로 돌아 들어가게 한다. 트럭 길(평평하게 다진 길)이 시작 지역 → 초원 → 고원 → 늪으로 이어진다.
///  - 바닥은 PurePoly 타일(23m)을 깔고 정점을 위치 기반 노이즈로 들어 올려 언덕을 만든다 (이웃 타일 경계가 정확히 맞는다).
///    색은 팔레트의 같은 줄 안에서만 바꿔서(초원=연두 줄, 고원=회색 줄, 늪=짙은 녹색 줄) 경계 보간이 다른 색으로 새지 않는다.
///  - 바깥 경계: 허공 쪽은 산 + 보이지 않는 벽, 동굴 쪽은 안쪽 바위 + 벽, 호수 쪽은 물가 바위 + 벽.
///  - 다시 실행하면 이전에 만든 것을 지우고 새로 만든다 (같은 seed면 같은 결과).
///  - 채집물(나무/돌/약초)은 마지막에 GatherablePropInstaller.Install(false), 트럭 통과 레이어는 TruckPassSetup.Apply로 맞춘다.
/// </summary>
public static partial class OpenWorldExpansionBuilder
{
    const string RootName = "10_Expansion";
    const string AssetDir = "Assets/Scenes/MiningMapAssets/Expansion";
    const string TileDir = AssetDir + "/Tiles";

    // 타일 격자 (타일 하나 23m)
    const float GX0 = -120f, GZ0 = -345f, Step = 23f;
    const int Cols = 33, Rows = 28;
    public const float BaseY = 1.0f;          // 시작 지역 바닥(약 1.2~1.6)보다 살짝 낮게: 겹치는 곳은 기존 바닥이 위에 보인다

    // 구역 경계
    public const float NorthZ = 100f;         // A/B 남쪽 끝
    public const float SplitX = 170f;         // A | B
    public const float SwampX = 380f;         // C 서쪽 끝 (그 서쪽은 지하 동굴)
    public const float EastX = 640f;          // 동쪽 끝 (그 너머는 호수)

    // 점유 격자 (5m)
    const float OX0 = -160f, OZ0 = -380f, OStep = 5f;
    const int OW = 170, OH = 140;
    enum Occ : byte { Empty, Water, Cave, Ground, High }
    static Occ[,] _occ;
    static float[,] _distExisting;

    static Dictionary<string, string> _paths;
    static System.Random _rng;
    static Transform _root;
    static readonly List<Vector3> _taken = new List<Vector3>();   // (x, z, 반경)
    static readonly List<Vector2> _ponds = new List<Vector2>();

    // 트럭 길: 시작 지역 북쪽 끝 → 초원 → 고원 → 늪
    static readonly Vector2[] Road =
    {
        new Vector2(5f, 96f), new Vector2(8f, 150f), new Vector2(40f, 200f), new Vector2(120f, 222f),
        new Vector2(230f, 214f), new Vector2(340f, 190f), new Vector2(450f, 175f), new Vector2(505f, 120f),
        new Vector2(500f, 30f), new Vector2(470f, -70f), new Vector2(500f, -170f), new Vector2(470f, -270f),
    };

    // ================================================================ 입구

    [MenuItem("Tools/Last Truck/Map: Build Open World Expansion")]
    static void Menu() { Debug.Log("[OpenWorldExpansion]\n" + Build(20261117)); }

    [MenuItem("Tools/Last Truck/Map: Bake Expansion NavMesh")]
    static void MenuBake() { Debug.Log("[OpenWorldExpansion] " + BakeNavMesh()); }

    /// <summary>
    /// 새 땅에만 내비메시를 따로 굽는다 (Map 전체를 다시 구우면 Unity가 크래시했다 - 폐허 도시 지형까지 함께 계산되면서).
    /// 시작 지역과 맞닿는 북쪽 통로에는 넓은 NavMeshLink를 놓아 좀비가 오갈 수 있게 한다.
    /// 주의: Map의 NavMeshSurface는 다시 굽지 말 것 (이 땅이 두 번 들어가고, 크래시 위험).
    /// </summary>
    public static string BakeNavMesh()
    {
        var map = GameObject.Find("Map");
        var root = map != null ? map.transform.Find(RootName) : null;
        if (root == null) return "확장 땅이 없어요";
        var mapSurface = map.GetComponent<Unity.AI.Navigation.NavMeshSurface>();

        var surface = root.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
        if (surface == null) surface = root.gameObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
        surface.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        if (mapSurface != null)
        {
            surface.agentTypeID = mapSurface.agentTypeID;
            surface.layerMask = mapSurface.layerMask;
            surface.overrideVoxelSize = mapSurface.overrideVoxelSize;
            surface.voxelSize = mapSurface.voxelSize;
            surface.overrideTileSize = mapSurface.overrideTileSize;
            surface.tileSize = mapSurface.tileSize;
        }
        surface.BuildNavMesh();
        if (surface.navMeshData == null) return "bake failed";

        string path = AssetDir + "/NavMesh-Expansion.asset";
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(surface.navMeshData, path);
        EditorUtility.SetDirty(surface);

        // 시작 지역 ↔ 초원 통로 연결
        var links = root.Find("NavLinks");
        if (links != null) Object.DestroyImmediate(links.gameObject);
        links = new GameObject("NavLinks").transform;
        links.SetParent(root, false);
        var go = new GameObject("Link_StartToMeadow");
        go.transform.SetParent(links, false);
        go.transform.position = new Vector3(5f, 2f, 108f);
        var link = go.AddComponent<Unity.AI.Navigation.NavMeshLink>();
        link.agentTypeID = surface.agentTypeID;
        link.startPoint = new Vector3(0f, 0f, -14f);   // 시작 지역 쪽
        link.endPoint = new Vector3(0f, 0f, 14f);      // 초원 쪽
        link.width = 70f;
        link.bidirectional = true;
        link.UpdateLink();

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        var b = surface.navMeshData.sourceBounds;
        return "expansion navmesh baked: " + b.min.ToString("F0") + ".." + b.max.ToString("F0") + " link width 70";
    }

    [MenuItem("Tools/Last Truck/Map: Remove Open World Expansion")]
    static void MenuRemove() { Debug.Log("[OpenWorldExpansion] " + Remove()); }

    public static string Remove()
    {
        var map = GameObject.Find("Map");
        var old = map != null ? map.transform.Find(RootName) : null;
        if (old != null) Object.DestroyImmediate(old.gameObject);
        RestoreRemoved();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "removed";
    }

    public static string Build(int seed)
    {
        if (Application.isPlaying) return "플레이 중에는 만들 수 없어요";
        var sb = new StringBuilder();
        _rng = new System.Random(seed);
        _taken.Clear(); _ponds.Clear();
        _mountAcc = 0f;
        _mineEntrance = null; _mineEntrance2 = null;
        ExpansionMineCave.BuiltPlans.Clear();

        var map = GameObject.Find("Map");
        if (map == null) return "Map 없음";
        var old = map.transform.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        RestoreRemoved();
        Physics.SyncTransforms();

        if (!AssetDatabase.IsValidFolder("Assets/Scenes/MiningMapAssets")) AssetDatabase.CreateFolder("Assets/Scenes", "MiningMapAssets");
        if (!AssetDatabase.IsValidFolder(AssetDir)) AssetDatabase.CreateFolder("Assets/Scenes/MiningMapAssets", "Expansion");
        if (AssetDatabase.IsValidFolder(TileDir)) AssetDatabase.DeleteAsset(TileDir);
        AssetDatabase.CreateFolder(AssetDir, "Tiles");

        _root = new GameObject(RootName).transform;
        _root.SetParent(map.transform, false);

        // 1) 지금 무엇이 있는지 조사
        SampleOccupancy();
        sb.AppendLine(ClearPassage());

        // 2) 연못 자리
        PickPonds();

        // 3) 바닥 타일 (메시 에셋 수백 개를 한 번에 쓴다)
        bool[,] placed;
        AssetDatabase.StartAssetEditing();
        try { placed = BuildTiles(sb); }
        finally { AssetDatabase.StopAssetEditing(); }
        Physics.SyncTransforms();

        // 4) 경계
        BuildBoundaries(placed, sb);

        // 5) 구역별 꾸미기
        DecorateMeadow(sb);
        DecorateHighland(sb);
        DecorateSwamp(sb);
        DecorateRoad(sb);
        Physics.SyncTransforms();

        // 6) 채집물 / 트럭 통과
        sb.AppendLine(GatherablePropInstaller.Install(false));
        int passed = TruckPassSetup.Apply(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        sb.AppendLine("truck-pass colliders=" + passed);

        // 7) 광산 입구 뒤로 이어지는 지하 동굴 두 곳 (트럭 통과 설정 뒤에 만들어서 동굴 레이어/충돌체가 바뀌지 않게 한다)
        sb.AppendLine(ExpansionMineCave.Build(_root, _mineEntrance, seed + 7, 1));
        if (_mineEntrance2 != null) sb.AppendLine(ExpansionMineCave.Build(_root, _mineEntrance2, seed + 8, 2, _mine2Side));
        else sb.AppendLine("두 번째 광산 입구를 놓을 자리를 못 찾았어요");

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        LastResult = sb.ToString();
        Debug.Log("[OpenWorldExpansion]\n" + LastResult);
        return LastResult;
    }

    /// <summary>마지막 Build 결과 (오래 걸려서 호출한 쪽이 기다리지 못했을 때 읽는다).</summary>
    public static string LastResult = "";

    // ================================================================ 조사

    static void SampleOccupancy()
    {
        _occ = new Occ[OW, OH];
        for (int i = 0; i < OW; i++)
            for (int j = 0; j < OH; j++)
            {
                float x = OX0 + i * OStep, z = OZ0 + j * OStep;
                _occ[i, j] = Classify(x, z);
            }

        // 기존 땅/바위/동굴까지의 거리 (이 근처는 평평하게 이어 붙인다)
        _distExisting = new float[OW, OH];
        const float INF = 1e6f;
        for (int i = 0; i < OW; i++)
            for (int j = 0; j < OH; j++)
                _distExisting[i, j] = (_occ[i, j] == Occ.Ground || _occ[i, j] == Occ.High || _occ[i, j] == Occ.Cave) ? 0f : INF;
        // 두 번 훑는 거리 변환 (대각선 근사)
        float d1 = OStep, d2 = OStep * 1.4142f;
        for (int j = 0; j < OH; j++)
            for (int i = 0; i < OW; i++)
            {
                float v = _distExisting[i, j];
                if (i > 0) v = Mathf.Min(v, _distExisting[i - 1, j] + d1);
                if (j > 0) v = Mathf.Min(v, _distExisting[i, j - 1] + d1);
                if (i > 0 && j > 0) v = Mathf.Min(v, _distExisting[i - 1, j - 1] + d2);
                if (i < OW - 1 && j > 0) v = Mathf.Min(v, _distExisting[i + 1, j - 1] + d2);
                _distExisting[i, j] = v;
            }
        for (int j = OH - 1; j >= 0; j--)
            for (int i = OW - 1; i >= 0; i--)
            {
                float v = _distExisting[i, j];
                if (i < OW - 1) v = Mathf.Min(v, _distExisting[i + 1, j] + d1);
                if (j < OH - 1) v = Mathf.Min(v, _distExisting[i, j + 1] + d1);
                if (i < OW - 1 && j < OH - 1) v = Mathf.Min(v, _distExisting[i + 1, j + 1] + d2);
                if (i > 0 && j < OH - 1) v = Mathf.Min(v, _distExisting[i - 1, j + 1] + d2);
                _distExisting[i, j] = v;
            }
    }

    static Occ Classify(float x, float z)
    {
        if (!Physics.Raycast(new Vector3(x, 300f, z), Vector3.down, out RaycastHit h, 600f, ~0, QueryTriggerInteraction.Ignore))
            return Occ.Empty;
        var t = h.collider.transform;
        if (t.IsChildOf(_root)) return Occ.Empty;
        if (h.collider.name == "Plane" && t.parent != null && t.parent.name == "Models") return Occ.Water;
        float y = h.point.y;
        if (y < -2f) return Occ.Cave;
        return y < 6f ? Occ.Ground : Occ.High;
    }

    static Occ OccAt(float x, float z)
    {
        int i = Mathf.RoundToInt((x - OX0) / OStep), j = Mathf.RoundToInt((z - OZ0) / OStep);
        if (i < 0 || j < 0 || i >= OW || j >= OH) return Occ.Empty;
        return _occ[i, j];
    }

    static float DistExisting(float x, float z)
    {
        float fi = (x - OX0) / OStep, fj = (z - OZ0) / OStep;
        int i = Mathf.Clamp(Mathf.FloorToInt(fi), 0, OW - 2), j = Mathf.Clamp(Mathf.FloorToInt(fj), 0, OH - 2);
        float tx = Mathf.Clamp01(fi - i), tz = Mathf.Clamp01(fj - j);
        float a = Mathf.Lerp(_distExisting[i, j], _distExisting[i + 1, j], tx);
        float b = Mathf.Lerp(_distExisting[i, j + 1], _distExisting[i + 1, j + 1], tx);
        return Mathf.Min(Mathf.Lerp(a, b, tz), 1000f);
    }

    // ================================================================ 통로 (시작 지역 → 초원)

    const string RemovedName = "_RemovedForExpansion";

    /// <summary>시작 지역 북쪽 끝 통로 위에 놓인 작은 바위/덤불을 비활성 묶음으로 옮긴다 (지우지 않는다 → Remove로 되돌릴 수 있다).</summary>
    static string ClearPassage()
    {
        var box = new Bounds(new Vector3(8f, 10f, 112f), new Vector3(70f, 40f, 34f));
        var map = GameObject.Find("Map");
        var bin = new GameObject(RemovedName).transform;
        bin.SetParent(map.transform, false);
        bin.gameObject.SetActive(false);
        int moved = 0;
        foreach (string group in new[] { "02_Nature/Vegetation", "02_Nature/Stones&Rocks", "02_Nature/Mushrooms" })
        {
            var g = map.transform.Find(group);
            if (g == null) continue;
            var list = new List<Transform>();
            foreach (Transform c in g)
            {
                var r = c.GetComponentInChildren<Renderer>();
                if (r != null && r.bounds.Intersects(box)) list.Add(c);
            }
            foreach (var c in list)
            {
                var tag = new GameObject("from:" + group);   // 원래 자리를 기억한다
                tag.transform.SetParent(bin, false);
                c.SetParent(tag.transform, true);
                moved++;
            }
        }
        if (moved == 0) Object.DestroyImmediate(bin.gameObject);
        return "passage cleared: " + moved + " props moved to " + RemovedName;
    }

    static void RestoreRemoved()
    {
        var map = GameObject.Find("Map");
        if (map == null) return;
        var bin = map.transform.Find(RemovedName);
        if (bin == null) return;
        var tags = new List<Transform>();
        foreach (Transform t in bin) tags.Add(t);
        foreach (var tag in tags)
        {
            string group = tag.name.Substring("from:".Length);
            var g = map.transform.Find(group);
            var kids = new List<Transform>();
            foreach (Transform c in tag) kids.Add(c);
            foreach (var c in kids) c.SetParent(g != null ? g : map.transform, true);
        }
        Object.DestroyImmediate(bin.gameObject);
    }

    // ================================================================ 지형

    static float Sm(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    static float N(float x, float z, float f, float o = 0f) => Mathf.PerlinNoise((x + 1000f + o) * f, (z + 1000f - o) * f);

    /// <summary>구역 가중치 (경계에서 부드럽게 섞인다).</summary>
    static void Weights(float x, float z, out float wA, out float wB, out float wC)
    {
        float jitter = (N(x, z, 0.02f, 77f) - 0.5f) * 50f;
        float east = Sm((x + jitter - (SplitX - 25f)) / 50f);
        float south = Sm(((NorthZ + 10f) - z + jitter * 0.5f) / 40f) * Sm((x - (SwampX - 40f)) / 40f);   // 늪은 동굴 지대 동쪽에만
        wC = east * south;
        wB = east * (1f - south);
        wA = 1f - east;
    }

    public enum Biome { Meadow, Highland, Swamp }

    static Biome BiomeAt(float x, float z)
    {
        Weights(x, z, out float a, out float b, out float c);
        if (c >= a && c >= b) return Biome.Swamp;
        return b >= a ? Biome.Highland : Biome.Meadow;
    }

    static float RoadDist(float x, float z)
    {
        float best = 1e9f;
        var p = new Vector2(x, z);
        for (int i = 0; i < Road.Length - 1; i++)
        {
            Vector2 a = Road[i], b = Road[i + 1], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    static float PondDist(float x, float z)
    {
        float best = 1e9f;
        foreach (var c in _ponds) best = Mathf.Min(best, Vector2.Distance(new Vector2(x, z), c));
        return best;
    }

    /// <summary>땅 높이 (월드 xz만으로 정해져서 이웃 타일 경계가 정확히 맞는다).</summary>
    public static float Height(float x, float z)
    {
        Weights(x, z, out float wA, out float wB, out float wC);
        // 초원: 넓게 구르는 언덕
        // (Mathf.PerlinNoise는 가끔 0보다 아주 조금 작은 값을 돌려준다. 음수의 소수 거듭제곱은 NaN이라 타일 메시에 NaN 정점이 생겨
        //  충돌체/내비메시 굽기가 실패하거나 크래시했다 → 0 이상으로 자른다)
        float hA = Mathf.Pow(Mathf.Max(0f, N(x, z, 0.011f)), 1.4f) * 7f + (N(x, z, 0.035f, 11f) - 0.5f) * 1.6f;
        // 고원: 더 높고 굵은 둔덕 + 잔 요철
        float hB = Mathf.Pow(Mathf.Max(0f, N(x, z, 0.009f, 33f)), 1.6f) * 12f + Mathf.Abs(N(x, z, 0.03f, 5f) - 0.5f) * 4f;
        // 늪: 거의 평평하고 낮은 물결
        float hC = N(x, z, 0.025f, 51f) * 1.4f;
        float h = wA * hA + wB * hB + wC * hC;

        // 기존 땅 / 트럭 길 / 연못 근처는 평평하게
        float mask = Sm((DistExisting(x, z) - 6f) / 40f);
        mask = Mathf.Min(mask, Sm((RoadDist(x, z) - 5f) / 14f));
        mask = Mathf.Min(mask, Sm((PondDist(x, z) - 13f) / 16f));
        if (float.IsNaN(h) || float.IsInfinity(h)) h = 0f;   // 안전장치: 어떤 경우에도 NaN 높이는 내보내지 않는다
        return BaseY + Mathf.Max(0f, h) * mask;
    }

    static float U(int col) => (col * 32 + 16) / 256f;
    static float V(int row, float shade) => 1f - (row * 32 + 2 + Mathf.Clamp01(shade) * 27f) / 256f;

    /// <summary>바닥 색 (팔레트 UV). 구역마다 팔레트의 한 줄 안에서만 고른다.</summary>
    static Vector2 GroundUV(Biome biome, float x, float z, float h)
    {
        if (biome == Biome.Swamp && GroveGround(x, z, out Vector2 groveUv)) return groveUv;   // 독버섯 군락 안은 청록빛 땅
        float n1 = N(x, z, 0.05f, 3f), n2 = N(x, z, 0.12f, 9f);
        float lift = Mathf.Clamp01((h - BaseY) / 8f);
        bool road = RoadDist(x, z) < 4.5f;
        switch (biome)
        {
            case Biome.Meadow:
            {
                if (road) return new Vector2(U(5), V(1, 0.55f));                       // 밟힌 풀길
                float t = n1 * 0.8f + lift * 0.5f + (n2 - 0.5f) * 0.3f;
                int col = t > 0.66f ? 5 : t > 0.16f ? 6 : 7;                            // 언덕 위 연두 / 들판 녹색 / 움푹한 곳 짙은 녹색
                return new Vector2(U(col), V(1, 0.12f + n2 * 0.35f));
            }
            case Biome.Highland:
            {
                if (road) return new Vector2(U(5), V(0, 0.05f));                       // 자갈길
                float t = n1 * 0.7f + lift * 0.6f + (n2 - 0.5f) * 0.3f;
                int col = t > 0.62f ? 5 : 6;                                            // 짙은 회색 바위 땅 (눈밭처럼 보이지 않게)
                return new Vector2(U(col), V(0, col == 5 ? 0.25f + n2 * 0.5f : 0.05f + n2 * 0.3f));
            }
            default:
            {
                if (road) return new Vector2(U(6), V(1, 0.75f));                       // 이끼 낀 둑길
                float t = n1 * 0.9f + (n2 - 0.5f) * 0.4f;
                int col = t > 0.72f ? 6 : 7;
                return new Vector2(U(col), V(1, col == 6 ? 0.7f + n2 * 0.25f : 0.5f + n2 * 0.45f));   // 짙고 축축한 이끼색
            }
        }
    }

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

    static Transform Group(string name)
    {
        var t = _root.Find(name);
        if (t != null) return t;
        var g = new GameObject(name);
        g.transform.SetParent(_root, false);
        return g.transform;
    }

    static float R01() => (float)_rng.NextDouble();
    static float Rng(float a, float b) => Mathf.Lerp(a, b, R01());

    // ---------------------------------------------------------------- 연못

    static void PickPonds()
    {
        // 초원 2개, 늪 8개 (트럭 길과 경계에서 떨어진 곳)
        TryPonds(2, -80f, 150f, 135f, 255f);
        TryPonds(8, SwampX + 30f, EastX - 40f, -320f, 70f);
    }

    static void TryPonds(int count, float x0, float x1, float z0, float z1)
    {
        int made = 0;
        for (int t = 0; t < 400 && made < count; t++)
        {
            // 연못은 타일 한 칸에 맞춰 놓는다
            float x = Rng(x0, x1), z = Rng(z0, z1);
            int i = Mathf.FloorToInt((x - GX0) / Step), j = Mathf.FloorToInt((z - GZ0) / Step);
            var c = new Vector2(GX0 + i * Step + Step * 0.5f, GZ0 + j * Step + Step * 0.5f);
            if (RoadDist(c.x, c.y) < 30f) continue;
            if (InGrove(c.x, c.y, 26f)) continue;   // 독버섯 군락 둘레에는 연못을 두지 않는다
            bool ok = true;
            foreach (var p in _ponds) if (Vector2.Distance(p, c) < 50f) { ok = false; break; }
            if (!ok) continue;
            if (DistExisting(c.x, c.y) < 30f) continue;
            _ponds.Add(c);
            made++;
        }
    }

    static bool IsPondSlot(Vector2 c)
    {
        foreach (var p in _ponds) if (Vector2.Distance(p, c) < 1f) return true;
        return false;
    }

    // ---------------------------------------------------------------- 타일

    static bool InZone(float cx, float cz)
    {
        if (cx > EastX + 5f || cx < GX0) return false;
        if (cz >= NorthZ - 2f && cz <= 300f) return true;          // A, B
        return cx >= SwampX && cz >= -350f;                         // C
    }

    static bool[,] BuildTiles(StringBuilder sb)
    {
        var placed = new bool[Cols, Rows];
        string[] meadow = { "PP_Meadow_01", "PP_Meadow_02", "PP_Meadow_03", "PP_Meadow_04" };
        string[] ground = { "PP_Ground_01", "PP_Ground_02", "PP_Ground_03", "PP_Ground_04" };
        var gTiles = Group("Ground");
        int count = 0, skipCave = 0, skipLand = 0;

        for (int j = 0; j < Rows; j++)
            for (int i = 0; i < Cols; i++)
            {
                float cx = GX0 + i * Step + Step * 0.5f, cz = GZ0 + j * Step + Step * 0.5f;
                if (!InZone(cx, cz)) continue;

                // 타일 자리에 무엇이 있는가: 동굴이 조금이라도 있으면 덮지 않는다 / 이미 땅이면 건너뛴다
                int cave = 0, land = 0, total = 0;
                for (int a = 0; a < 5; a++)
                    for (int b = 0; b < 5; b++)
                    {
                        var o = OccAt(cx - Step * 0.45f + a * Step * 0.225f, cz - Step * 0.45f + b * Step * 0.225f);
                        total++;
                        if (o == Occ.Cave) cave++;
                        else if (o == Occ.Ground || o == Occ.High) land++;
                    }
                if (cave > 0) { skipCave++; continue; }
                if (land > total * 0.88f) { skipLand++; continue; }

                var c = new Vector2(cx, cz);
                Biome biome = BiomeAt(cx, cz);
                bool pond = IsPondSlot(c);
                string prefab = pond
                    ? (biome == Biome.Meadow ? "PP_Meadow_Lake_0" + (1 + _rng.Next(4)) : "PP_Lake_0" + (1 + _rng.Next(4)))
                    : (biome == Biome.Meadow ? meadow[_rng.Next(4)] : ground[_rng.Next(4)]);
                var pf = P(prefab);
                if (pf == null) continue;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, gTiles);
                inst.name = "EXP_Tile_" + biome + "_" + i + "_" + j;
                inst.transform.position = new Vector3(cx, BaseY + (((i + j) & 1) == 0 ? 0f : 0.01f), cz);
                inst.transform.rotation = Quaternion.Euler(0f, 90f * _rng.Next(4), 0f);
                inst.tag = "Ground";
                ShapeTile(inst, biome, !pond, ++count);
                placed[i, j] = true;
            }
        sb.AppendLine($"tiles={count} skipped(cave)={skipCave} skipped(existing land)={skipLand} ponds={_ponds.Count}");
        return placed;
    }

    /// <summary>타일 메시를 복사해서 언덕 모양으로 들어 올리고 색을 바꾼 뒤 에셋으로 저장한다.</summary>
    static void ShapeTile(GameObject inst, Biome biome, bool deform, int index)
    {
        var mf = inst.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        Mesh m = Object.Instantiate(mf.sharedMesh);
        Vector3[] v = m.vertices; Vector3[] nr = m.normals; Vector2[] uv = m.uv;
        Quaternion rot = inst.transform.rotation, inv = Quaternion.Inverse(rot);
        for (int k = 0; k < v.Length; k++)
        {
            Vector3 wp = inst.transform.TransformPoint(v[k]);
            float h = deform ? Height(wp.x, wp.z) - BaseY : 0f;
            v[k].y += h;
            if (deform)
            {
                float gx = Height(wp.x + 0.6f, wp.z) - Height(wp.x - 0.6f, wp.z);
                float gz = Height(wp.x, wp.z + 0.6f) - Height(wp.x, wp.z - 0.6f);
                Vector3 nw = rot * nr[k];
                nw = new Vector3(nw.x - gx * 0.8f * nw.y, nw.y, nw.z - gz * 0.8f * nw.y).normalized;
                nr[k] = inv * nw;
            }
            uv[k] = GroundUV(biome, wp.x, wp.z, BaseY + h);
        }
        m.vertices = v; m.normals = nr; m.uv = uv; m.RecalculateBounds();
        m.name = "ExpTile_" + index.ToString("D4");
        AssetDatabase.CreateAsset(m, TileDir + "/" + m.name + ".asset");
        mf.sharedMesh = m;
        var mc = inst.GetComponent<MeshCollider>();
        if (mc != null) mc.sharedMesh = m;
        else inst.AddComponent<MeshCollider>().sharedMesh = m;
    }

    // ================================================================ 경계

    enum Edge { None, Void, Cave, Water }

    static Edge EdgeKind(float x, float z)
    {
        switch (OccAt(x, z))
        {
            case Occ.Ground: case Occ.High: return Edge.None;   // 기존 땅/바위산과 맞닿음
            case Occ.Water: return Edge.Water;
            case Occ.Cave: return Edge.Cave;
            default: return Edge.Void;
        }
    }

    static bool CaveBeyond(Vector3 p, Vector3 outward)
    {
        for (float d = 5f; d <= 50f; d += 5f)
        {
            Vector3 q = p + outward * d;
            if (OccAt(q.x, q.z) == Occ.Cave) return true;
        }
        return false;
    }

    static void BuildBoundaries(bool[,] placed, StringBuilder sb)
    {
        var gWall = Group("Boundary");
        var gMount = Group("BoundaryMountains");
        var gRock = Group("Stones&Rocks");
        int walls = 0, mountains = 0, rocks = 0;
        const int Sub = 6;   // 타일 한 변을 6토막으로 나눠서 판정

        for (int j = 0; j < Rows; j++)
            for (int i = 0; i < Cols; i++)
            {
                if (!placed[i, j]) continue;
                float cx = GX0 + i * Step + Step * 0.5f, cz = GZ0 + j * Step + Step * 0.5f;
                // 네 방향: (di, dj) 바깥 방향
                int[,] dirs = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
                for (int d = 0; d < 4; d++)
                {
                    int di = dirs[d, 0], dj = dirs[d, 1];
                    int ni = i + di, nj = j + dj;
                    if (ni >= 0 && nj >= 0 && ni < Cols && nj < Rows && placed[ni, nj]) continue;

                    Vector3 outward = new Vector3(di, 0f, dj);
                    Vector3 along = new Vector3(-dj, 0f, di);
                    Vector3 edgeCenter = new Vector3(cx, 0f, cz) + outward * (Step * 0.5f);

                    // 토막마다 바깥 3m 지점을 보고 같은 종류끼리 이어 붙인다
                    Edge runKind = Edge.None; int runStart = 0;
                    for (int s = 0; s <= Sub; s++)
                    {
                        Edge k = Edge.None;
                        if (s < Sub)
                        {
                            float t = (s + 0.5f) / Sub - 0.5f;
                            Vector3 p = edgeCenter + along * (t * Step) + outward * 3f;
                            k = EdgeKind(p.x, p.z);
                            if (k == Edge.Void && CaveBeyond(p, outward)) k = Edge.Cave;   // 산을 놓으면 동굴 위에 떠 버린다
                        }
                        if (k != runKind || s == Sub)
                        {
                            if (runKind != Edge.None && s > runStart)
                            {
                                float t0 = (float)runStart / Sub - 0.5f, t1 = (float)s / Sub - 0.5f;
                                Vector3 a = edgeCenter + along * (t0 * Step), b = edgeCenter + along * (t1 * Step);
                                AddWall(gWall, a, b, outward); walls++;
                                Vector3 mid = (a + b) * 0.5f; float len = Vector3.Distance(a, b);
                                if (runKind == Edge.Void) mountains += AddMountains(gMount, mid, along, outward, len);
                                else if (runKind == Edge.Cave) rocks += AddCaveRocks(gRock, mid, along, outward, len);
                                else rocks += AddShoreRocks(gRock, mid, along, outward, len);
                            }
                            runKind = k; runStart = s;
                        }
                    }
                }
            }
        sb.AppendLine($"boundary walls={walls} mountains={mountains} edge rocks={rocks}");
    }

    static void AddWall(Transform parent, Vector3 a, Vector3 b, Vector3 outward)
    {
        var go = new GameObject("Wall");
        go.transform.SetParent(parent, false);
        Vector3 mid = (a + b) * 0.5f + outward * 1.2f;
        float len = Vector3.Distance(a, b) + 2.5f;   // 모서리끼리 겹쳐서 틈이 없게
        go.transform.position = new Vector3(mid.x, BaseY + 12f, mid.z);
        go.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(len, 40f, 2f);
    }

    static float _mountAcc;

    static int AddMountains(Transform parent, Vector3 mid, Vector3 along, Vector3 outward, float len)
    {
        // 허공 쪽: 30m마다 산 하나 (산이 겹쳐서 벽처럼 보인다)
        _mountAcc += len;
        if (_mountAcc < 12f) return 0;   // 짧은 틈(산맥 사이 골짜기)에도 산을 세워 허공이 안 보이게
        _mountAcc = 0f;
        string[] names = { "PP_Mountain_01", "PP_Mountain_02" };
        var pf = P(names[_rng.Next(2)]);
        if (pf == null) return 0;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        float yaw = Quaternion.LookRotation(along).eulerAngles.y + Rng(-15f, 15f);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * Rng(0.75f, 1.05f);
        Vector3 p = mid + outward * Rng(18f, 24f);
        go.transform.position = new Vector3(p.x, BaseY - 1.5f, p.z);
        return 1;
    }

    static int AddCaveRocks(Transform parent, Vector3 mid, Vector3 along, Vector3 outward, float len)
    {
        // 동굴 쪽: 안쪽 가장자리에 이끼 암반을 세워서 낭떠러지를 가린다
        int n = Mathf.Max(1, Mathf.RoundToInt(len / 14f)), made = 0;
        for (int k = 0; k < n; k++)
        {
            float t = (k + 0.5f) / n - 0.5f;
            Vector3 p = mid + along * (t * len) - outward * 5f;
            var go = PutOnGround("PP_Rock_Plateau_Moss_" + (1 + _rng.Next(14)).ToString("D2"), parent, p.x, p.z, Rng(0.35f, 0.55f), 1.0f, 0.2f, false);
            if (go != null) made++;
        }
        return made;
    }

    static int AddShoreRocks(Transform parent, Vector3 mid, Vector3 along, Vector3 outward, float len)
    {
        // 호숫가: 군데군데 큰 바위 (호수 건너 폐허 도시가 보이도록 촘촘하지 않게)
        int made = 0;
        if (R01() < 0.55f)
        {
            Vector3 p = mid + along * Rng(-len * 0.4f, len * 0.4f) - outward * 2f;
            string n = R01() < 0.5f ? "PP_Rock_Brown_Moss_0" + (1 + _rng.Next(5)) : "PP_Rock_Pile_Moss_" + (1 + _rng.Next(12)).ToString("D2");
            if (PutOnGround(n, parent, p.x, p.z, Rng(1.6f, 2.8f), 0.4f, 0.4f, false) != null) made++;
        }
        return made;
    }

    // ================================================================ 놓기 도우미

    /// <summary>새로 깐 바닥 위의 높이/기울기.</summary>
    static bool Ground(float x, float z, out float y, out Vector3 normal)
    {
        y = 0f; normal = Vector3.up;
        float best = float.MaxValue; bool ok = false;
        foreach (var h in Physics.RaycastAll(new Vector3(x, 200f, z), Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.name.StartsWith("EXP_Tile") && h.distance < best) { best = h.distance; y = h.point.y; normal = h.normal; ok = true; }
        return ok;
    }

    static bool Free(float x, float z, float radius, float roadClear)
    {
        if (roadClear > 0f && RoadDist(x, z) < roadClear + radius) return false;
        foreach (var t in _taken) if ((new Vector2(t.x - x, t.y - z)).sqrMagnitude < (t.z + radius) * (t.z + radius)) return false;
        return true;
    }

    static GameObject PutOnGround(string prefab, Transform parent, float x, float z, float scale, float sink, float tilt, bool register, float radius = 0f)
    {
        var pf = P(prefab);
        if (pf == null) return null;
        if (!Ground(x, z, out float y, out Vector3 n)) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        Vector3 nn = Vector3.Lerp(Vector3.up, n, tilt).normalized;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, nn) * Quaternion.Euler(0f, R01() * 360f, 0f);
        go.transform.localScale = Vector3.one * scale;
        go.transform.position = new Vector3(x, y - sink, z);
        if (register) _taken.Add(new Vector3(x, z, radius));
        return go;
    }

    /// <summary>구역 안의 빈자리 하나 (트럭 길·다른 물체·연못과 떨어진 곳).</summary>
    static bool PickSpot(Biome biome, float radius, float roadClear, out float x, out float z, float x0 = -999f, float x1 = 999f, float z0 = -999f, float z1 = 999f)
    {
        for (int t = 0; t < 60; t++)
        {
            x = Rng(Mathf.Max(x0, GX0), Mathf.Min(x1, EastX)); z = Rng(Mathf.Max(z0, -345f), Mathf.Min(z1, 290f));
            if (BiomeAt(x, z) != biome) continue;
            if (PondDist(x, z) < 15f + radius) continue;
            if (!Free(x, z, radius, roadClear)) continue;
            if (!Ground(x, z, out _, out _)) continue;
            return true;
        }
        x = z = 0f;
        return false;
    }

    static int Scatter(Biome biome, Transform parent, System.Func<string> name, int count, float sMin, float sMax, float radius, float roadClear, float sink, float tilt)
    {
        int made = 0;
        for (int k = 0; k < count; k++)
        {
            if (!PickSpot(biome, radius, roadClear, out float x, out float z)) continue;
            if (PutOnGround(name(), parent, x, z, Rng(sMin, sMax), sink, tilt, true, radius) != null) made++;
        }
        return made;
    }

    static string Pick(params string[] names) => names[_rng.Next(names.Length)];
    static string Num(string prefix, int max) => prefix + (1 + _rng.Next(max)).ToString("D2");

    /// <summary>채집 트리거를 직접 단다 (약초/독약초처럼 GatherablePropInstaller 규칙과 다른 것).</summary>
    static void MakeGatherable(GameObject go, string label, string itemId, int amount, float hold)
    {
        var r = go.GetComponentInChildren<Renderer>();
        if (r == null) return;
        foreach (var col in go.GetComponentsInChildren<Collider>()) if (!col.isTrigger) Object.DestroyImmediate(col);   // 풀은 걸리지 않게
        Bounds b = r.bounds;
        var t = new GameObject(GatherablePropInstaller.TriggerName);
        t.transform.SetParent(go.transform, true);
        t.transform.rotation = Quaternion.identity; t.layer = 4;
        t.transform.position = b.center;
        Vector3 ls = t.transform.lossyScale;
        float s = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
        var sc = t.AddComponent<SphereCollider>(); sc.isTrigger = true;
        sc.radius = (Mathf.Max(b.extents.x, b.extents.z) + 0.3f) / s;
        t.AddComponent<GatherableProp>().Configure(label, itemId, amount, hold, go);
    }

    static Material TintedMaterial(string assetName, Color tint)
    {
        string path = AssetDir + "/" + assetName + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/PurePoly/Mining_Pack/Materials/PP_Standard_Material.mat");
            m = new Material(src) { name = assetName };
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Recolor(GameObject go, Material m)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
        }
    }

    // ================================================================ A 푸른 초원

    static void DecorateMeadow(StringBuilder sb)
    {
        var veg = Group("Vegetation"); var rocks = Group("Stones&Rocks"); var props = Group("Props");
        Biome B = Biome.Meadow;

        // 랜드마크 먼저 (넓은 빈자리가 필요하다)
        int camp = BuildCamp(props, veg);

        // 자작나무 숲 (몇 군데 모여서)
        int trees = 0;
        for (int g = 0; g < 9; g++)
        {
            if (!PickSpot(B, 6f, 14f, out float gx, out float gz)) continue;
            int n = 10 + _rng.Next(8);
            for (int k = 0; k < n; k++)
            {
                float x = gx + Rng(-22f, 22f), z = gz + Rng(-22f, 22f);
                if (BiomeAt(x, z) != B || !Free(x, z, 3f, 9f) || PondDist(x, z) < 15f) continue;
                if (PutOnGround(Num("PP_Fantasy_Birch_Tree_", 10), veg, x, z, Rng(1.0f, 1.6f), 0.2f, 0.15f, true, 3f) != null) trees++;
            }
        }
        // 드문드문 큰 나무
        trees += Scatter(B, veg, () => Num("PP_Tree_", 8), 55, 1.5f, 2.2f, 4f, 10f, 0.2f, 0.1f);
        trees += Scatter(B, veg, () => Num("PP_Fir_Tree_", 10), 20, 1.3f, 1.9f, 3.5f, 10f, 0.2f, 0.1f);

        // 풀 덤불 (일부는 설치기가 약초로 뽑는다) + 확실한 약초 군락
        int bushes = 0;
        for (int c = 0; c < 90; c++)
        {
            if (!PickSpot(B, 1f, 6f, out float cx, out float cz)) continue;
            int k = 2 + _rng.Next(4);
            for (int q = 0; q < k; q++)
            {
                float x = cx + Rng(-4f, 4f), z = cz + Rng(-4f, 4f);
                if (PutOnGround("PP_Grass_0" + (1 + _rng.Next(6)), veg, x, z, Rng(1.8f, 2.6f), 0.05f, 0.5f, false) != null) bushes++;
            }
        }
        int herbs = 0;
        for (int c = 0; c < 22; c++)
        {
            if (!PickSpot(B, 2f, 8f, out float cx, out float cz)) continue;
            int k = 2 + _rng.Next(3);
            for (int q = 0; q < k; q++)
            {
                var go = PutOnGround("PP_Grass_0" + (1 + _rng.Next(6)), veg, cx + Rng(-3f, 3f), cz + Rng(-3f, 3f), Rng(2.2f, 2.8f), 0.05f, 0.5f, false);
                if (go == null) continue;
                MakeGatherable(go, "약초", ItemIds.Herb, 1, GatherablePropInstaller.HerbHoldSeconds);
                herbs++;
            }
        }
        // 이끼 돌
        int r = Scatter(B, rocks, () => Pick("PP_Rock_Moss_0" + (1 + _rng.Next(7)), "PP_Rock_Pile_Moss_" + (1 + _rng.Next(12)).ToString("D2"), "PP_Rock_Brown_Moss_0" + (1 + _rng.Next(5))),
            40, 0.8f, 2.0f, 2.5f, 8f, 0.3f, 0.6f);

        sb.AppendLine($"meadow: trees={trees} bushes={bushes} herbs={herbs} rocks={r} camp props={camp}");
    }

    static int BuildCamp(Transform props, Transform veg)
    {
        if (!PickSpot(Biome.Meadow, 18f, 22f, out float cx, out float cz)) return 0;
        int n = 0;
        _taken.Add(new Vector3(cx, cz, 18f));
        if (PutOnGround("PP_Campfire_01", props, cx, cz, 2.6f, 0.05f, 0.2f, false) != null) n++;
        for (int k = 0; k < 3; k++)
        {
            float a = k * 2.1f + Rng(0f, 0.4f);
            var t = PutOnGround("PP_Tent_0" + (1 + _rng.Next(4)), props, cx + Mathf.Cos(a) * 8f, cz + Mathf.Sin(a) * 8f, 2.8f, 0.05f, 0.2f, false);
            if (t != null) { t.transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Cos(a), 0f, -Mathf.Sin(a))); n++; }
        }
        if (PutOnGround("PP_Well_01", props, cx + 12f, cz - 6f, 2.6f, 0.05f, 0.1f, false) != null) n++;
        for (int k = 0; k < 2; k++) if (PutOnGround("PP_Log_Pile_0" + (1 + _rng.Next(7)), props, cx + Rng(-13f, 13f), cz + Rng(-13f, 13f), 2.4f, 0.05f, 0.3f, false) != null) n++;
        for (int k = 0; k < 6; k++)
            if (PutOnGround(Pick("PP_Barrel_01", "PP_Barrel_02", "PP_Crate_Wooden_01", "PP_Crate_Wooden_02", "PP_Bucket_01"), props, cx + Rng(-11f, 11f), cz + Rng(-11f, 11f), Rng(2.4f, 3f), 0.05f, 0.4f, false) != null) n++;
        // 나무 울타리 반 바퀴
        for (int k = 0; k < 7; k++)
        {
            float a = Mathf.PI * (0.15f + k * 0.12f);
            var f = PutOnGround(Pick("PP_Wooden_Fence_01", "PP_Wooden_Fence_02", "PP_Wooden_Fence_03"), props, cx + Mathf.Cos(a) * 16f, cz + Mathf.Sin(a) * 16f, 2.6f, 0.05f, 0.3f, false);
            if (f != null) { f.transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a))); n++; }
        }
        return n;
    }

    // ================================================================ B 바위 고원

    static void DecorateHighland(StringBuilder sb)
    {
        var rocks = Group("Stones&Rocks"); var veg = Group("Vegetation"); var props = Group("Props"); var ores = Group("Crystals&Ores&Veins");
        Biome B = Biome.Highland;

        int circle = BuildStoneCircle(rocks);
        int mine = BuildMineEntrance(props);
        mine += BuildSecondMineEntrance(props);

        // 큰 암반 (이 구역의 얼굴)
        int mesas = 0;
        for (int k = 0; k < 40 && mesas < 9; k++)
        {
            if (!PickSpot(B, 20f, 22f, out float x, out float z)) continue;
            string n = R01() < 0.5f ? "PP_Rock_Plateau_0" + (1 + _rng.Next(7)) : "PP_Rock_Plateau_Moss_" + (1 + _rng.Next(14)).ToString("D2");
            if (PutOnGround(n, rocks, x, z, Rng(0.45f, 0.75f), 2f, 0.1f, true, 20f) != null) mesas++;
        }
        int menhir = Scatter(B, rocks, () => Pick("PP_Rock_Menhir_0" + (1 + _rng.Next(4)), "PP_Rock_Menhir_Moss_0" + (1 + _rng.Next(4)), "PP_Rock_Column_0" + (1 + _rng.Next(5))),
            26, 1.6f, 2.8f, 3f, 9f, 0.3f, 0.1f);
        int small = Scatter(B, rocks, () => Pick("PP_Rock_0" + (1 + _rng.Next(7)), "PP_Rock_Pile_" + (1 + _rng.Next(12)).ToString("D2"), "PP_Pebbles_" + (1 + _rng.Next(11)).ToString("D2")),
            90, 0.8f, 2.2f, 2f, 6f, 0.3f, 0.6f);
        int firs = Scatter(B, veg, () => Num("PP_Fir_Tree_", 10), 70, 1.3f, 2.0f, 3.5f, 9f, 0.2f, 0.1f);

        // 광맥: 구리 20 / 철 16 / 금 9 / 다이아몬드 4 (회색 고원은 광물 지대라 늘렸다. 동굴 두 곳에는 따로 더 있다)
        int nodes = 0;
        nodes += PlaceOres(ores, B, "copper_ore", "구리 광맥", 2, 8, 3, 5, 1.2f, 180f, 360f, 20);
        nodes += PlaceOres(ores, B, "iron_ore", "철 광맥", 3, 6, 4, 7, 1.8f, 300f, 600f, 16);
        nodes += PlaceOres(ores, B, "platinum_ore", "금 광맥", 4, 3, 3, 4, 3.5f, 420f, 900f, 9);
        nodes += PlaceOres(ores, B, "diamond", "다이아몬드 원석", 5, 1, 5, 6, 5f, 1200f, 1800f, 4);

        sb.AppendLine($"highland: mesas={mesas} menhirs={menhir} rocks={small} firs={firs} ore nodes={nodes} stone circle={circle} mine camp={mine}");
    }

    static int PlaceOres(Transform parent, Biome biome, string itemId, string label, int tier, int amount, int minHits, int maxHits, float hold, float minRegen, float maxRegen, int count)
    {
        var item = ItemCatalog.GetOrCreate().GetItem(itemId);
        if (item == null) return 0;
        int made = 0;
        for (int k = 0; k < count; k++)
        {
            if (!PickSpot(biome, 5f, 8f, out float x, out float z)) continue;
            if (!Ground(x, z, out float y, out _)) continue;
            var node = new GameObject(label.Replace(" ", "") + "_node");
            node.layer = 4;
            node.transform.SetParent(parent, false);
            node.transform.position = new Vector3(x, y - 0.2f, z);
            var sc = node.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 3.2f;
            var srn = node.AddComponent<SpecialResourceNode>();
            srn.Configure(label, item, amount, tier, minHits, maxHits, minRegen, maxRegen, "", hold);
            int seed = Mathf.RoundToInt(x * 13f + z * 7f);
            if (!OreCrystalConverter.Convert(srn, itemId, seed)) { Object.DestroyImmediate(node); continue; }
            foreach (Transform c in node.transform)
                foreach (var tr in c.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 0;
            EditorUtility.SetDirty(srn);
            _taken.Add(new Vector3(x, z, 5f));
            made++;
        }
        return made;
    }

    static int BuildStoneCircle(Transform parent)
    {
        if (!PickSpot(Biome.Highland, 16f, 20f, out float cx, out float cz)) return 0;
        _taken.Add(new Vector3(cx, cz, 16f));
        int n = 0;
        for (int k = 0; k < 9; k++)
        {
            float a = k * Mathf.PI * 2f / 9f;
            var s = PutOnGround("PP_Rune_Stone_" + (1 + _rng.Next(12)).ToString("D2"), parent, cx + Mathf.Cos(a) * 10f, cz + Mathf.Sin(a) * 10f, Rng(2.6f, 3.4f), 0.3f, 0f, false);
            if (s != null) { s.transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Cos(a), 0f, -Mathf.Sin(a))); n++; }
        }
        if (PutOnGround("PP_Pillar_Stone_Rune_0" + (1 + _rng.Next(3)), parent, cx, cz, 3.2f, 0.3f, 0f, false) != null) n++;
        return n;
    }

    static Transform _mineEntrance;   // 이번 빌드에서 놓은 광산 입구 (뒤로 동굴을 판다 - ExpansionMineCave)

    static int BuildMineEntrance(Transform parent)
    {
        // 북쪽 산맥 앞 (고원 위쪽 가장자리)에 버려진 광산 입구 + 레일 + 수레
        for (int t = 0; t < 40; t++)
        {
            float x = Rng(SplitX + 60f, EastX - 80f), z = Rng(235f, 262f);
            if (BiomeAt(x, z) != Biome.Highland || !Free(x, z, 14f, 10f) || !Ground(x, z, out _, out _)) continue;
            _mineEntrance = PlaceMineEntranceAt(parent, x, z, out int n);
            return n;
        }
        return 0;
    }

    /// <summary>광산 입구(문 틀 + 앞쪽 레일/수레/상자)를 한 곳에 놓는다. 입구는 -Z(남쪽)를 향하고 안쪽은 +Z다.</summary>
    static Transform PlaceMineEntranceAt(Transform parent, float x, float z, out int n)
    {
        _taken.Add(new Vector3(x, z, 14f));
        n = 0;
        Transform ent = null;
        var e = PutOnGround("PP_Main_Entrance_01_Wooden", parent, x, z, 2.4f, 0.2f, 0f, false);
        if (e != null) { e.transform.rotation = Quaternion.Euler(0f, 180f, 0f); n++; ent = e.transform; }
        for (int k = 0; k < 3; k++)
        {
            var r = PutOnGround("PP_Rail_Straight_0" + (1 + _rng.Next(5)), parent, x, z - 8f - k * 5.6f, 1.4f, 0.05f, 0.2f, false);
            if (r != null) { r.transform.rotation = Quaternion.Euler(0f, 0f, 0f); n++; }
        }
        if (PutOnGround("PP_Mine_Cart_0" + (1 + _rng.Next(9)), parent, x + 0.2f, z - 14f, 2.2f, 0f, 0.2f, false) != null) n++;
        for (int k = 0; k < 5; k++)
            if (PutOnGround(Pick("PP_Crate_Wooden_01", "PP_Crate_Wooden_03", "PP_Barrel_03", "PP_Dynamite_Bundle_01", "PP_Log_Pile_02"), parent, x + Rng(-10f, 10f), z - Rng(6f, 18f), Rng(2.2f, 2.8f), 0.05f, 0.3f, false) != null) n++;
        return ent;
    }

    // 두 번째 광산 입구 (수정 동굴). 첫 동굴과 겹치지 않고, 산/경계 벽/트럭 길에서 떨어진 고원 남쪽에 놓는다.
    static Transform _mineEntrance2;
    static float _mine2Side = 1f;

    static int BuildSecondMineEntrance(Transform parent)
    {
        if (_mineEntrance == null) return 0;
        // 첫 동굴 경로 (겹침 검사용)
        float f1 = 4f;
        if (Ground(_mineEntrance.position.x, _mineEntrance.position.z - 3f, out float gy1, out _)) f1 = gy1 + 0.15f;
        Vector3 door1 = _mineEntrance.position + Vector3.forward * 2.7f;
        float s1 = _mineEntrance.position.x < 400f ? 1f : -1f;
        var plan1 = ExpansionMineCave.Plan(door1, f1, s1, 1);

        for (int t = 0; t < 250; t++)
        {
            float x = Rng(190f, 600f), z = Rng(165f, 205f);
            if (BiomeAt(x, z) != Biome.Highland || !Free(x, z, 14f, 10f) || !Ground(x, z, out float gy, out _)) continue;
            if (!Ground(x, z - 3f, out float gf, out _)) continue;
            Vector3 door = new Vector3(x, gy, z) + Vector3.forward * 2.7f;
            foreach (float side in new[] { 1f, -1f })
            {
                var plan2 = ExpansionMineCave.Plan(door, gf + 0.15f, side, 2);
                if (!ExpansionMineCave.Fits(plan2, plan1, out string why)) continue;
                bool roadOk = true;
                foreach (var st in plan2)
                    if (st.pos.y > -9f && RoadDist(st.pos.x, st.pos.z) < st.hw + 10f) { roadOk = false; break; }
                if (!roadOk) continue;
                _mineEntrance2 = PlaceMineEntranceAt(parent, x, z, out int n);
                _mine2Side = side;
                return n;
            }
        }
        return 0;
    }

    // ================================================================ C 안개 늪

    static void DecorateSwamp(StringBuilder sb)
    {
        var veg = Group("Vegetation"); var mush = Group("Mushrooms"); var rocks = Group("Stones&Rocks"); var props = Group("Props"); var fx = Group("FX");
        Biome B = Biome.Swamp;

        // 독버섯 군락 (신비로운 숲): 자리를 먼저 차지해서 다른 장식이 겹치지 않게 한다
        _taken.Add(new Vector3(GroveCenter.x, GroveCenter.y, GroveRadius));
        sb.AppendLine(BuildGrove());

        int shacks = 0;
        for (int k = 0; k < 2; k++) if (BuildShack(props)) shacks++;
        int oil = BuildOilSeep(props);

        int dead = Scatter(B, veg, () => Num("PP_Leafless_Tree_", 11), 110, 1.0f, 1.8f, 3f, 9f, 0.3f, 0.25f);
        int firs = Scatter(B, veg, () => Num("PP_Fir_Tree_", 10), 26, 1.4f, 2.2f, 3.5f, 9f, 0.2f, 0.1f);
        int roots = Scatter(B, veg, () => Num("PP_Tree_Root_", 10), 30, 1.2f, 2.0f, 3f, 7f, 0.2f, 0.6f);

        // 큰 버섯 군락 (늪의 얼굴)
        int bigMush = 0;
        for (int g = 0; g < 9; g++)
        {
            if (!PickSpot(B, 8f, 12f, out float gx, out float gz)) continue;
            _taken.Add(new Vector3(gx, gz, 8f));
            int n = 3 + _rng.Next(4);
            for (int k = 0; k < n; k++)
            {
                string nm = R01() < 0.5f ? Num("PP_Mushroom_Long_Dark_Brown_", 11) : Num("PP_Mushroom_Dark_Brown_", 10);
                if (PutOnGround(nm, mush, gx + Rng(-6f, 6f), gz + Rng(-6f, 6f), Rng(3.5f, 7f), 0.1f, 0.2f, false) != null) bigMush++;
            }
        }
        int smallMush = Scatter(B, mush, () => Pick("PP_Mushrooms_Brown_0" + (1 + _rng.Next(7)), "PP_Mushrooms_Stone_Brown_0" + (1 + _rng.Next(3))), 50, 1.6f, 2.6f, 1.5f, 5f, 0.05f, 0.5f);
        int mossRocks = Scatter(B, rocks, () => Pick("PP_Rock_Moss_0" + (1 + _rng.Next(7)), "PP_Rock_Pile_Moss_" + (1 + _rng.Next(12)).ToString("D2"), "PP_Rock_Pile_Moss_Brown_0" + (1 + _rng.Next(5))),
            45, 0.8f, 2.0f, 2.5f, 7f, 0.3f, 0.6f);

        // 독약초 (보라빛 덤불) 군락
        var poisonMat = TintedMaterial("PP_PoisonHerb_Material", new Color(0.72f, 0.42f, 0.95f));
        int poison = 0;
        for (int c = 0; c < 20; c++)
        {
            if (!PickSpot(B, 2f, 7f, out float cx, out float cz)) continue;
            int k = 2 + _rng.Next(3);
            for (int q = 0; q < k; q++)
            {
                var go = PutOnGround("PP_Grass_0" + (1 + _rng.Next(6)), veg, cx + Rng(-3f, 3f), cz + Rng(-3f, 3f), Rng(2.2f, 2.8f), 0.05f, 0.5f, false);
                if (go == null) continue;
                Recolor(go, poisonMat);
                MakeGatherable(go, "독약초", ItemIds.PoisonHerb, 1, GatherablePropInstaller.HerbHoldSeconds);
                poison++;
            }
        }
        // 들풀
        int weeds = 0;
        for (int c = 0; c < 60; c++)
        {
            if (!PickSpot(B, 1f, 5f, out float cx, out float cz)) continue;
            for (int q = 0; q < 3; q++)
                if (PutOnGround(Num("PP_Grass_Single_", 10), veg, cx + Rng(-4f, 4f), cz + Rng(-4f, 4f), Rng(1.6f, 2.6f), 0.05f, 0.5f, false) != null) weeds++;
        }

        // 안개
        int fog = 0;
        for (int k = 0; k < 12; k++)
        {
            var fogPf = P(R01() < 0.5f ? "FX_Fog_Big_0" + (1 + _rng.Next(4)) : "FX_Fog_0" + (1 + _rng.Next(7)));
            if (fogPf == null) continue;
            if (!PickSpot(B, 0f, 0f, out float x, out float z)) continue;
            Ground(x, z, out float y, out _);
            var f = (GameObject)PrefabUtility.InstantiatePrefab(fogPf, fx);
            f.transform.position = new Vector3(x, y + 0.5f, z);
            f.transform.localScale = Vector3.one * 2f;
            fog++;
        }

        // 호숫가 배 두 척 (물 위)
        int boats = 0;
        for (int k = 0; k < 2; k++)
        {
            var pf = P("PP_Boat_0" + (1 + k));
            if (pf == null) continue;
            var b = (GameObject)PrefabUtility.InstantiatePrefab(pf, props);
            b.transform.position = new Vector3(EastX + 10f + k * 6f, -1.7f, Rng(-200f, 40f));
            b.transform.rotation = Quaternion.Euler(0f, Rng(0f, 360f), 0f);
            b.transform.localScale = Vector3.one * 3f;
            boats++;
        }
        sb.AppendLine($"swamp: dead trees={dead} firs={firs} roots={roots} big mushrooms={bigMush} small mushrooms={smallMush} moss rocks={mossRocks} poison herbs={poison} weeds={weeds} shacks={shacks} oil nodes={oil} fog={fog} boats={boats}");
    }

    static bool BuildShack(Transform parent)
    {
        if (!PickSpot(Biome.Swamp, 12f, 14f, out float x, out float z)) return false;
        if (!Ground(x, z, out float y, out _)) return false;
        _taken.Add(new Vector3(x, z, 12f));
        var root = new GameObject("SunkenShack"); root.transform.SetParent(parent);
        root.transform.position = new Vector3(x, y - 0.4f, z);
        root.transform.rotation = Quaternion.Euler(Rng(-3f, 3f), Rng(0f, 360f), Rng(-4f, 4f));   // 늪에 가라앉아 기울었다
        float sc = 3.4f, w = 3f * sc;
        string[] walls = { "PP_Wooden_Wall_01", "PP_Wooden_Wall_02", "PP_Wooden_Wall_03", "PP_Wooden_Wall_Window_01", "PP_Wooden_Wall_Window_02" };
        var floor = P("PP_Wooden_Floor_01");
        if (floor != null) { var f = (GameObject)PrefabUtility.InstantiatePrefab(floor, root.transform); f.transform.localScale = Vector3.one * sc; f.transform.localPosition = Vector3.zero; f.transform.localRotation = Quaternion.identity; }
        Vector3[] pos = { new Vector3(0, 0, w / 2f), new Vector3(0, 0, -w / 2f), new Vector3(w / 2f, 0, 0), new Vector3(-w / 2f, 0, 0) };
        float[] rot = { 0f, 180f, 90f, -90f };
        for (int i = 0; i < 4; i++)
        {
            if (R01() < 0.3f) continue;
            var pf = P(walls[_rng.Next(walls.Length)]); if (pf == null) continue;
            var wobj = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
            wobj.transform.localScale = Vector3.one * sc; wobj.transform.localPosition = pos[i];
            wobj.transform.localRotation = Quaternion.Euler(Rng(-4f, 4f), rot[i], Rng(-4f, 4f));
        }
        var roof = P("PP_Blacksmith_Roof_05") ?? P("PP_Blacksmith_Roof_02");
        if (roof != null && R01() < 0.6f) { var r = (GameObject)PrefabUtility.InstantiatePrefab(roof, root.transform); r.transform.localScale = Vector3.one * sc; r.transform.localPosition = new Vector3(0, w * 0.9f, 0); r.transform.localRotation = Quaternion.Euler(14f, 10f, 8f); }
        return true;
    }

    static int BuildOilSeep(Transform parent)
    {
        var oilMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/MiningMapAssets/Desert/PP_Oil_Material.mat");
        var oilItem = ItemCatalog.GetOrCreate().GetItem(ItemIds.Oil);
        if (oilMat == null || oilItem == null) return 0;
        if (!PickSpot(Biome.Swamp, 14f, 14f, out float cx, out float cz)) return 0;
        _taken.Add(new Vector3(cx, cz, 14f));
        var field = new GameObject("SwampOilSeep"); field.transform.SetParent(parent);
        field.transform.position = new Vector3(cx, BaseY, cz);
        int nodes = 0;
        for (int p = 0; p < 3; p++)
        {
            float a = p * 2.1f + Rng(0f, 0.5f), rr = p == 0 ? 0f : Rng(6f, 9f);
            float px = cx + Mathf.Cos(a) * rr, pz = cz + Mathf.Sin(a) * rr;
            if (!Ground(px, pz, out float py, out _)) continue;
            float radius = Rng(2.2f, 3.2f);
            var pool = new GameObject("OilNode_Swamp_" + (p + 1)); pool.transform.SetParent(field.transform);
            pool.transform.position = new Vector3(px, py + 0.05f, pz);
            int seg = 20;
            var verts = new List<Vector3> { Vector3.zero }; var uvs = new List<Vector2> { new Vector2(0.02f, 0.02f) };
            float ph = R01() * 6.28f;
            for (int i = 0; i < seg; i++)
            {
                float ang = i / (float)seg * 6.283f, r = radius * (1f + 0.22f * Mathf.Sin(3f * ang + ph));
                verts.Add(new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r)); uvs.Add(new Vector2(0.02f, 0.02f));
            }
            var tris = new List<int>();
            for (int i = 0; i < seg; i++) { tris.Add(0); tris.Add(1 + (i + 1) % seg); tris.Add(1 + i); }
            var m = new Mesh { name = pool.name };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            var nrm = new Vector3[verts.Count]; for (int i = 0; i < nrm.Length; i++) nrm[i] = Vector3.up;
            m.normals = nrm; m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, TileDir + "/" + m.name + ".asset");
            pool.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = pool.AddComponent<MeshRenderer>(); mr.sharedMaterial = oilMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var col = pool.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = radius * 0.75f + 1.2f; col.center = new Vector3(0, 0.6f, 0);
            pool.layer = 4;
            var node = pool.AddComponent<SpecialResourceNode>();
            node.Configure("기름", oilItem, 5, 0, 3, 5, 180f, 360f, ItemIds.CopperHandPump, 1.5f, null, 0f, null, 0f);
            EditorUtility.SetDirty(node);
            nodes++;
        }
        for (int k = 0; k < 5; k++)
            PutOnGround(Pick("PP_Barrel_01", "PP_Barrel_02", "PP_Barrel_04"), parent, cx + Rng(-12f, 12f), cz + Rng(-12f, 12f), Rng(2.4f, 3f), 0.1f, 0.4f, false);
        return nodes;
    }

    // ================================================================ 트럭 길

    static void DecorateRoad(StringBuilder sb)
    {
        var props = Group("Props");
        int signs = 0, lamps = 0;
        // 갈림/구역 입구마다 표지판
        foreach (int idx in new[] { 1, 4, 7, 9 })
        {
            Vector2 p = Road[idx], q = Road[Mathf.Min(idx + 1, Road.Length - 1)];
            Vector2 side = new Vector2(-(q - p).y, (q - p).x).normalized * 6.5f;
            var s = PutOnGround(Pick("PP_Signpost_01", "PP_Signpost_02"), props, p.x + side.x, p.y + side.y, 2.6f, 0.05f, 0f, false);
            if (s != null) signs++;
        }
        // 길가 가로등 (밤길 표시)
        float acc = 0f;
        for (int i = 0; i < Road.Length - 1; i++)
        {
            Vector2 a = Road[i], b = Road[i + 1];
            float len = Vector2.Distance(a, b);
            Vector2 dir = (b - a) / len, side = new Vector2(-dir.y, dir.x) * 6.5f;
            for (float d = 0f; d < len; d += 5f)
            {
                acc += 5f;
                if (acc < 55f) continue;
                acc = 0f;
                Vector2 p = a + dir * d + side * ((lamps & 1) == 0 ? 1f : -1f);
                if (i == 0 && d < 30f) continue;   // 시작 지역 경계 바로 앞은 비운다
                if (PutOnGround("PP_Street_Lights_01", props, p.x, p.y, 2.4f, 0.05f, 0f, false) != null) lamps++;
            }
        }
        sb.AppendLine($"road: signposts={signs} lamps={lamps}");
    }
}
#endif
