using System.Collections.Generic;
using UnityEngine;
using Combat;

namespace CraftingSystem
{
    /// <summary>
    /// Demo_01 씬에 플레이어/트럭/3종 가공시설/UI를 자동 구성한다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CraftingSceneBootstrap : MonoBehaviour
    {
        public const string TruckObjectName = "PP_Horse_Cart_Used_02";
        private const string BonfireMeshPath = "Assets/Models/FBX format/campfire-pit.fbx";
        private const string CutterMeshPath = "Assets/Models/FBX format/workbench-grind.fbx";
        private const string RollerPressMeshPath = "Assets/Models/FBX format/workbench-anvil.fbx";
        private const string TreeMeshPath = "Assets/Low Poly Environment Starter Kit/Prefabs/URP/Trees/Tree 1.prefab";
        private const string RockMeshPath = "Assets/Low Poly Environment Starter Kit/Prefabs/URP/Rocks/Rock 1.prefab";
        private const string IronOreMeshPath = "Assets/PurePoly/Mining_Free_Assets/Prefabs/PP_Rock_Moss_04.prefab";
        private const string CopperOreMeshPath = "Assets/PurePoly/Mining_Free_Assets/Prefabs/PP_Gemstone_07_Copper.prefab";


        [SerializeField] private bool setupOnAwake = true;
        [SerializeField] private bool createFacilitiesIfMissing = true;

        private void Awake()
        {
            if (setupOnAwake)
                SetupScene();
        }

        [ContextMenu("Setup Crafting Scene")]
        public void SetupScene()
        {
            ItemCatalog catalog = ItemCatalog.GetOrCreate();

            PlayerInventory player = EnsurePlayer();
            TruckStation truck = EnsureTruck(catalog);
            Vector3 origin = truck != null ? truck.transform.position : Vector3.zero;
            // 데모 버전: 월드에 시설물을 자동 생성하지 않는다. 가공은 트럭 가공 탭에서 바로 처리된다.
            // EnsureFacilities(catalog, origin);
            EnsureResourceNodes(catalog, origin);
            EnsureClickInteractor(player);
            EnsureWeaponController(player);
            EnsureTrainingDummy(player.transform.position);
            EnsureUI(player);

            Debug.Log("[CraftingSceneBootstrap] 씬 셋업 완료.");
        }

        private static PlayerInventory EnsurePlayer()
        {
            PlayerInventory existing = FindFirstObjectByType<PlayerInventory>();
            if (existing != null)
                return existing;

            var go = new GameObject("Player");
            return go.AddComponent<PlayerInventory>();
        }

private static void EnsureWeaponController(PlayerInventory player)
        {
            if (player == null)
                return;

            if (player.GetComponent<WeaponController>() == null)
                player.gameObject.AddComponent<WeaponController>();
        }

        private static void EnsureTrainingDummy(Vector3 playerPosition)
        {
            if (GameObject.Find("TrainingDummy") != null)
                return;

            Vector3 pos = playerPosition + new Vector3(2.5f, 0f, 2.5f);

            var root = new GameObject("TrainingDummy");
            root.transform.position = pos;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            SetColor(body, new Color(0.65f, 0.5f, 0.3f));

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            head.transform.localScale = Vector3.one * 0.5f;
            SetColor(head, new Color(0.85f, 0.72f, 0.5f));

            GameObject arms = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arms.name = "Arms";
            arms.transform.SetParent(root.transform, false);
            arms.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            arms.transform.localScale = new Vector3(1.6f, 0.15f, 0.15f);
            SetColor(arms, new Color(0.5f, 0.35f, 0.2f));

            root.AddComponent<Damageable>();
        }


private static TruckStation EnsureTruck(ItemCatalog catalog)
        {
            TruckStation existing = FindFirstObjectByType<TruckStation>();
            if (existing != null)
            {
                TruckCraftingManager existingMgr = existing.CraftingManager;
                if (existingMgr == null)
                    existingMgr = existing.gameObject.AddComponent<TruckCraftingManager>();
                existingMgr.LoadAssemblyRecipesFromCatalog();
                return existing;
            }

            GameObject truckGo = GameObject.Find(TruckObjectName);
            if (truckGo == null)
            {
                // 경로 검색
                Transform[] all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
                foreach (Transform t in all)
                {
                    if (t.name == TruckObjectName ||
                        t.name.Equals(TruckObjectName, System.StringComparison.OrdinalIgnoreCase) ||
                        t.name.ToLowerInvariant().Contains("horse_cart"))
                    {
                        truckGo = t.gameObject;
                        break;
                    }
                }
            }

            if (truckGo == null)
            {
                truckGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                truckGo.name = TruckObjectName;
                truckGo.transform.position = new Vector3(2.5f, 0.5f, 0f);
                truckGo.transform.localScale = new Vector3(2.2f, 1.2f, 3.2f);
                SetColor(truckGo, new Color(0.45f, 0.3f, 0.18f));
                Debug.LogWarning("[Bootstrap] 마차 오브젝트를 찾지 못해 임시 트럭 큐브를 생성했습니다.");
            }

            EnsureCollider(truckGo);

            TruckInventory inv = truckGo.GetComponent<TruckInventory>();
            if (inv == null)
                inv = truckGo.AddComponent<TruckInventory>();

            TruckCraftingManager craft = truckGo.GetComponent<TruckCraftingManager>();
            if (craft == null)
                craft = truckGo.AddComponent<TruckCraftingManager>();

            TruckStation station = truckGo.GetComponent<TruckStation>();
            if (station == null)
                station = truckGo.AddComponent<TruckStation>();

            craft.LoadAssemblyRecipesFromCatalog();
            return station;
        }

private void EnsureFacilities(ItemCatalog catalog, Vector3 origin)
        {
            if (!createFacilitiesIfMissing)
                return;

            EnsureFacility(
                "Facility_Bonfire",
                "모닥불",
                FacilityType.Campfire,
                origin + new Vector3(-3.5f, 0f, 2.5f),
                new Color(0.95f, 0.35f, 0.12f),
                catalog,
                BonfireMeshPath);

            EnsureFacility(
                "Facility_Cutter",
                "절삭기",
                FacilityType.PrecisionCutter,
                origin + new Vector3(0.5f, 0f, 3.5f),
                new Color(0.2f, 0.55f, 0.85f),
                catalog,
                CutterMeshPath);

            EnsureFacility(
                "Facility_RollerPress",
                "롤러 프레스기",
                FacilityType.RollerPress,
                origin + new Vector3(4.5f, 0f, 2.5f),
                new Color(0.55f, 0.75f, 0.25f),
                catalog,
                RollerPressMeshPath);

            EnsureFacility(
                "Facility_Grindstone",
                "숫돌 연마대",
                FacilityType.Grindstone,
                origin + new Vector3(-3.5f, 0f, -1.5f),
                new Color(0.5f, 0.5f, 0.55f),
                catalog,
                null);

            EnsureFacility(
                "Facility_ChemicalRefinery",
                "화학 정제탑",
                FacilityType.ChemicalRefinery,
                origin + new Vector3(0.5f, 0f, -3.5f),
                new Color(0.35f, 0.75f, 0.45f),
                catalog,
                null);

            EnsureFacility(
                "Facility_SuperheatedFurnace",
                "초고온 용광로",
                FacilityType.SuperheatedFurnace,
                origin + new Vector3(4.5f, 0f, -1.5f),
                new Color(0.95f, 0.55f, 0.1f),
                catalog,
                null);

            EnsureFacility(
                "Facility_LeatherTanningRack",
                "가죽 무두질 건조대",
                FacilityType.LeatherTanningRack,
                origin + new Vector3(7f, 0f, 1f),
                new Color(0.6f, 0.45f, 0.3f),
                catalog,
                null);
        }

private static void EnsureFacility(
            string objectName,
            string displayName,
            FacilityType type,
            Vector3 position,
            Color color,
            ItemCatalog catalog,
            string meshAssetPath)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null)
            {
                GameObject meshSource = null;
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(meshAssetPath))
                    meshSource = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(meshAssetPath);
#endif
                if (meshSource != null)
                {
                    go = (GameObject)UnityEngine.Object.Instantiate(meshSource);
                    go.name = objectName;
                    go.transform.position = position;
                    NormalizeScale(go, position, FacilityTargetSize);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = objectName;
                    go.transform.position = position + new Vector3(0f, 0.625f, 0f);
                    go.transform.localScale = Vector3.one * 1.25f;
                    SetColor(go, color);
                }
            }

            EnsureCollider(go);

            ProcessingFacility facility = go.GetComponent<ProcessingFacility>();
            if (facility == null)
                facility = go.AddComponent<ProcessingFacility>();

            facility.Configure(type, displayName, catalog.GetRecipesForFacility(type));

            // 간단한 라벨용 자식 텍스트는 생략 (월드 클릭 + UI 제목으로 구분)
        }

        private const float FacilityTargetSize = 1.4f;

        private static void NormalizeScale(GameObject go, Vector3 groundPosition, float targetSize)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float maxDim = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (maxDim > 0.0001f)
            {
                float scale = targetSize / maxDim;
                go.transform.localScale = Vector3.one * scale;
            }

            renderers = go.GetComponentsInChildren<Renderer>();
            Bounds scaledBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                scaledBounds.Encapsulate(renderers[i].bounds);

            float liftToGround = go.transform.position.y - scaledBounds.min.y;
            go.transform.position = new Vector3(groundPosition.x, groundPosition.y + liftToGround, groundPosition.z);
        }

        private void EnsureResourceNodes(ItemCatalog catalog, Vector3 origin)
        {
            if (!createFacilitiesIfMissing)
                return;

            // 도구 등급: 맨손 0 < 돌곡괭이 1 < 구리곡괭이 2 < 철제곡괭이 3
            EnsureResourceNode(
                "Resource_Wood", "나무", ItemIds.Wood, 1, 0f, 0,
                origin + new Vector3(-6f, 0f, -1.5f), 1.8f, TreeMeshPath, catalog);

            EnsureResourceNode(
                "Resource_Stone", "돌", ItemIds.Stone, 1, 0f, 1,
                origin + new Vector3(-8f, 0f, 0.5f), 1.2f, RockMeshPath, catalog);

            // Copper/Iron은 맵 절차 생성(MapGenerator) 구역에서만 채집된다 — 트럭 근처 고정 데모 노드는 생성하지 않는다.
        }

        private static void EnsureResourceNode(
            string objectName,
            string displayName,
            string outputItemId,
            int amount,
            float gatherSeconds,
            int requiredTier,
            Vector3 position,
            float targetSize,
            string meshAssetPath,
            ItemCatalog catalog)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null)
            {
                GameObject meshSource = null;
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(meshAssetPath))
                    meshSource = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(meshAssetPath);
#endif
                if (meshSource != null)
                {
                    go = (GameObject)UnityEngine.Object.Instantiate(meshSource);
                    go.name = objectName;
                    go.transform.position = position;
                    NormalizeScale(go, position, targetSize);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = objectName;
                    go.transform.position = position + new Vector3(0f, targetSize * 0.5f, 0f);
                    go.transform.localScale = Vector3.one * targetSize;
                }
            }

            EnsureCollider(go);
            go.layer = 4; // PlayerInteract.interactLayer(Water)와 일치 — E키 근처 상호작용이 감지하도록

            ResourceNode node = go.GetComponent<ResourceNode>();
            if (node == null)
                node = go.AddComponent<ResourceNode>();

            ItemData outputItem = catalog.GetItem(outputItemId);
            node.Configure(displayName, outputItem, amount, gatherSeconds, requiredTier);
        }

        private static void EnsureClickInteractor(PlayerInventory player)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = FindFirstObjectByType<Camera>();
            }

            if (cam == null)
            {
                Debug.LogError("[Bootstrap] Camera를 찾지 못했습니다.");
                return;
            }

            WorldClickInteractor interactor = cam.GetComponent<WorldClickInteractor>();
            if (interactor == null)
                interactor = cam.gameObject.AddComponent<WorldClickInteractor>();

            interactor.PlayerInventory = player;
        }

        private static void EnsureUI(PlayerInventory player)
        {
            GameUIController ui = FindFirstObjectByType<GameUIController>();
            if (ui == null)
            {
                var go = new GameObject("GameUI");
                ui = go.AddComponent<GameUIController>();
            }

            ui.Initialize(player);
        }

private static void EnsureCollider(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col is MeshCollider meshCollider)
            {
                if (meshCollider.sharedMesh != null)
                {
                    meshCollider.convex = true;
                    return;
                }

                // 일부 에셋(예: Low Poly Environment Starter Kit)은 루트에 메쉬가
                // 비어있는 MeshCollider를 붙인 채로 배포된다. 그대로 두면 레이캐스트가
                // 통과해버리므로 아래에서 실제 렌더러 기준 박스 콜라이더로 보완한다.
            }
            else if (col != null)
            {
                return;
            }

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                go.AddComponent<BoxCollider>();
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(bounds.center);
            Vector3 lossy = go.transform.lossyScale;
            box.size = new Vector3(
                lossy.x != 0f ? bounds.size.x / lossy.x : bounds.size.x,
                lossy.y != 0f ? bounds.size.y / lossy.y : bounds.size.y,
                lossy.z != 0f ? bounds.size.z / lossy.z : bounds.size.z);
        }

        private static void SetColor(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                        ?? Shader.Find("Standard")
                                        ?? Shader.Find("Diffuse"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            renderer.sharedMaterial = mat;
        }
    }
}
