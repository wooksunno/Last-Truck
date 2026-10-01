using System.Collections.Generic;
using System.Linq;
using System.Text;
using Combat;
using CraftingSystem;
using Fusion;
using UnityEditor;
using UnityEngine;

namespace LastTruck.Networking.EditorTools
{
    /// <summary>
    /// 메뉴 "LastTruck > Multiplayer > 1. 네트워크 프리팹 생성 (캐릭터/몬스터/게임 상태)".
    ///
    ///  1. 캐릭터 목록(CharacterCatalog)이 없으면 만들고, 비어 있으면 프로젝트의 CharacterStatsData를 모두 넣는다.
    ///  2. 캐릭터 데이터마다 원본 캐릭터 프리팹(modelPrefab)을 자동으로 찾아 연결한다
    ///     (Character 컴포넌트의 stats가 그 데이터인 프리팹).
    ///  3. 공통 기본 프리팹 NetworkPlayer_Base.prefab을 만든다.
    ///     = CharacterController + NetworkObject + NetworkCharacterController + NetworkPlayer + NetworkPlayerMovement
    ///       + 기존 게임플레이 컴포넌트(Character, PlayerMove, PlayerStats, PlayerInteract, PlayerInventory, WeaponController, PlayerAttack)
    ///     기존 Rigidbody/CapsuleCollider는 쓰지 않는다 (몬스터/다른 플레이어에게 밀리지 않게).
    ///  4. 캐릭터마다 기본 프리팹의 Variant(NetworkPlayer_Army 등)를 만들고 원본의 모델(메쉬+애니메이터)만 자식으로 넣는다.
    ///     → 스크립트를 고칠 땐 Base 하나만 고치면 모든 캐릭터에 적용된다.
    ///  5. 각 캐릭터 데이터의 networkPrefab에 연결한다.
    ///  6. 몬스터/게임 상태 네트워크 프리팹과 NetworkPrefabRegistry를 만든다 (NetworkGamePrefabBuilder).
    ///  7. Fusion 프리팹 테이블을 다시 만든다.
    ///
    /// 이미 있는 프리팹은 기본적으로 건드리지 않는다 (빠진 것만 만든다). "모두 다시 만들기"를 고르면 덮어쓴다.
    /// </summary>
    public static class NetworkCharacterBuilder
    {
        #region 메뉴 / 전체 흐름

        private const string DialogTitle = "Last Truck - 네트워크 캐릭터 프리팹";
        public const string CatalogPath = LobbyEditorUI.Root + "/Resources/CharacterCatalog.asset";
        public const string CharacterPrefabDir = LobbyEditorUI.Root + "/Prefabs/Characters";
        public const string BasePrefabPath = CharacterPrefabDir + "/NetworkPlayer_Base.prefab";

        [MenuItem("LastTruck/Multiplayer/1. 네트워크 프리팹 생성 (캐릭터/몬스터/게임 상태)", priority = 101)]
        private static void BuildMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(DialogTitle, "플레이 모드에서는 실행할 수 없습니다.", "확인");
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex(DialogTitle,
                "캐릭터 목록과 네트워크 캐릭터 프리팹을 만듭니다.\n\n" +
                "- 빠진 것만 만들기: 이미 있는 프리팹은 그대로 둡니다.\n" +
                "- 모두 다시 만들기: 기본 프리팹과 캐릭터별 프리팹을 새로 덮어씁니다\n  (프리팹을 손으로 고친 내용은 사라집니다).",
                "빠진 것만 만들기", "취소", "모두 다시 만들기");
            if (choice == 1) return;

            string log = Build(choice == 2);
            EditorUtility.DisplayDialog(DialogTitle, log, "확인");
        }

        /// <summary>전체 과정을 실행하고 결과 요약 문자열을 돌려준다.</summary>
        public static string Build(bool rebuildAll)
        {
            var log = new StringBuilder();
            try
            {
                EditorUtility.DisplayProgressBar(DialogTitle, "캐릭터 목록 준비", 0.1f);
                LobbyEditorUI.EnsureFolder(LobbyEditorUI.Root + "/Resources");
                LobbyEditorUI.EnsureFolder(CharacterPrefabDir);

                CharacterCatalog catalog = EnsureCatalog(log);
                List<CharacterStatsData> characters = catalog.Characters.Where(c => c != null).ToList();
                if (characters.Count == 0)
                {
                    return "캐릭터 데이터(CharacterStatsData)를 찾지 못했습니다.\n" +
                           "Create > LastTruck > CharacterStats Data로 먼저 만들어 주세요.";
                }

                EditorUtility.DisplayProgressBar(DialogTitle, "원본 캐릭터 프리팹 찾기", 0.25f);
                foreach (CharacterStatsData data in characters)
                {
                    if (data.modelPrefab != null) continue;
                    GameObject found = FindModelPrefab(data);
                    if (found != null)
                    {
                        data.modelPrefab = found;
                        EditorUtility.SetDirty(data);
                        log.AppendLine($"- {data.DisplayName}: 원본 프리팹 {found.name} 연결");
                    }
                    else
                    {
                        log.AppendLine($"- {data.DisplayName}: 원본 프리팹을 찾지 못함 (modelPrefab을 직접 넣어 주세요)");
                    }
                }

                List<GameObject> sources = characters.Select(c => c.modelPrefab).Where(p => p != null).ToList();
                GameObject baseSource = sources.FirstOrDefault();
                if (baseSource == null)
                {
                    return log + "\n원본 캐릭터 프리팹이 하나도 없어 네트워크 프리팹을 만들 수 없습니다.";
                }

                EditorUtility.DisplayProgressBar(DialogTitle, "공통 기본 프리팹 생성", 0.4f);
                GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
                if (basePrefab == null || rebuildAll)
                {
                    basePrefab = CreateBasePrefab(baseSource, sources);
                    log.AppendLine($"- 기본 프리팹 생성: {BasePrefabPath}");
                }
                else
                {
                    log.AppendLine("- 기본 프리팹: 이미 있어서 그대로 사용");
                    if (EnsureBaseComponents(BasePrefabPath)) log.AppendLine("  (빠진 동기화 컴포넌트 추가: 체력 / 무기 연출)");
                }

                for (int i = 0; i < characters.Count; i++)
                {
                    CharacterStatsData data = characters[i];
                    EditorUtility.DisplayProgressBar(DialogTitle, $"캐릭터 프리팹: {data.DisplayName}",
                        0.5f + 0.4f * i / Mathf.Max(1, characters.Count));
                    if (data.modelPrefab == null) continue;

                    string path = VariantPath(data);
                    GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    NetworkObject variant = existing != null ? existing.GetComponent<NetworkObject>() : null;

                    if (variant == null || rebuildAll)
                    {
                        variant = CreateVariant(basePrefab, data, path);
                        log.AppendLine($"- {data.DisplayName}: {path}");
                    }

                    if (data.networkPrefab != variant)
                    {
                        data.networkPrefab = variant;
                        EditorUtility.SetDirty(data);
                    }
                }

                EditorUtility.DisplayProgressBar(DialogTitle, "몬스터 / 게임 상태 프리팹", 0.9f);
                NetworkGamePrefabBuilder.Build(log);

                AssetDatabase.SaveAssets();

                EditorUtility.DisplayProgressBar(DialogTitle, "Fusion 프리팹 테이블 재빌드", 0.95f);
                if (!EditorApplication.ExecuteMenuItem("Tools/Fusion/Rebuild Prefab Table"))
                {
                    log.AppendLine("\n※ 'Tools/Fusion/Rebuild Prefab Table'을 직접 한 번 실행해 주세요.");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return "완료했습니다.\n\n" + log;
        }

        #endregion

        #region 캐릭터 목록

        public static CharacterCatalog EnsureCatalog(StringBuilder log)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            if (catalog == null)
            {
                LobbyEditorUI.EnsureFolder(LobbyEditorUI.Root + "/Resources");
                catalog = ScriptableObject.CreateInstance<CharacterCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                log?.AppendLine($"- 캐릭터 목록 생성: {CatalogPath}");
            }

            if (catalog.Count == 0)
            {
                List<CharacterStatsData> all = AssetDatabase.FindAssets("t:CharacterStatsData")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(p => p)
                    .Select(AssetDatabase.LoadAssetAtPath<CharacterStatsData>)
                    .Where(d => d != null)
                    .ToList();
                catalog.EditorSetCharacters(all);
                EditorUtility.SetDirty(catalog);
                log?.AppendLine($"- 캐릭터 목록에 {all.Count}명 등록: {string.Join(", ", all.Select(d => d.DisplayName))}");
            }
            return catalog;
        }

        private static GameObject FindModelPrefab(CharacterStatsData data)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(CharacterPrefabDir)) continue; // 우리가 만든 네트워크 프리팹은 제외
                if (!path.StartsWith("Assets/")) continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<NetworkObject>() != null) continue;

                Character character = prefab.GetComponent<Character>();
                if (character != null && character.stats == data) return prefab;
            }
            return null;
        }

        private static string VariantPath(CharacterStatsData data)
        {
            string id = data.name.Replace("CharacterData_", string.Empty);
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) id = id.Replace(c, '_');
            return $"{CharacterPrefabDir}/NetworkPlayer_{id}.prefab";
        }

        #endregion

        #region 기본 프리팹

        /// <summary>원본 프리팹에서 복사해 올 게임플레이 컴포넌트 (순서대로 붙인다).</summary>
        private static readonly System.Type[] GameplayComponents =
        {
            typeof(Character),
            typeof(PlayerMove),
            typeof(PlayerStats),
            typeof(PlayerInteract),
            typeof(PlayerInventory),
            typeof(WeaponController),
            typeof(PlayerAttack),
        };

        private static GameObject CreateBasePrefab(GameObject source, List<GameObject> allSources)
        {
            var root = new GameObject("NetworkPlayer_Base");
            root.tag = "Player";
            root.layer = source.layer;

            // 1) 이동: CharacterController + Fusion NetworkCharacterController
            var controller = root.AddComponent<CharacterController>();
            FitCharacterController(controller, source);

            root.AddComponent<NetworkObject>();
            var networkController = root.AddComponent<NetworkCharacterController>();
            networkController.gravity = -20f;
            networkController.jumpImpulse = 0f;
            networkController.acceleration = 100f;
            networkController.braking = 100f;
            networkController.maxSpeed = 6f;
            networkController.rotationSpeed = 20f;

            root.AddComponent<NetworkPlayer>();
            root.AddComponent<NetworkPlayerMovement>();
            AddPlayerHealth(root);
            root.AddComponent<NetworkWeaponVisuals>();

            // 2) 기존 게임플레이 컴포넌트: 그 컴포넌트를 가진 첫 번째 원본 캐릭터에서 값을 복사
            //    (예: 활/화살 프리팹이 연결된 WeaponController는 Army에만 있다)
            foreach (System.Type type in GameplayComponents)
            {
                Component added = root.GetComponent(type);
                if (added == null) added = root.AddComponent(type); // (Unity null 때문에 ?? 대신 if)
                GameObject owner = allSources.FirstOrDefault(s => s != null && s.GetComponent(type) != null);
                Component original = owner != null ? owner.GetComponent(type) : null;
                if (original == null || added == null) continue;

                EditorUtility.CopySerialized(original, added);
                ClearReferencesInto(added, AssetDatabase.GetAssetPath(owner));
            }

            // Character.stats는 캐릭터별 Variant에서 넣는다.
            Character character = root.GetComponent<Character>();
            if (character != null) character.stats = null;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BasePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>플레이어용 NetworkHealth (팀킬 금지, 사망 3초 뒤 사라짐).</summary>
        private static NetworkHealth AddPlayerHealth(GameObject root)
        {
            NetworkHealth health = root.AddComponent<NetworkHealth>();
            var so = new SerializedObject(health);
            so.FindProperty("isPlayer").boolValue = true;
            so.FindProperty("despawnDelay").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return health;
        }

        /// <summary>
        /// 예전에 만든 기본 프리팹에 새로 필요해진 컴포넌트를 채운다 (다시 만들지 않고 추가만).
        /// 추가한 것이 있으면 true.
        /// </summary>
        private static bool EnsureBaseComponents(string path)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                if (contents.GetComponent<NetworkHealth>() == null) { AddPlayerHealth(contents); changed = true; }
                if (contents.GetComponent<NetworkWeaponVisuals>() == null) { contents.AddComponent<NetworkWeaponVisuals>(); changed = true; }
                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, path);
                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>원본 프리팹 내부 오브젝트를 가리키는 참조는 끊는다 (그대로 두면 다른 에셋을 가리키게 됨).</summary>
        private static void ClearReferencesInto(Component component, string sourceAssetPath)
        {
            var so = new SerializedObject(component);
            SerializedProperty property = so.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (property.name == "m_Script") continue;
                Object value = property.objectReferenceValue;
                if (value == null) continue;
                if (AssetDatabase.GetAssetPath(value) == sourceAssetPath && !(value is ScriptableObject))
                {
                    property.objectReferenceValue = null;
                    changed = true;
                }
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>원본 모델의 렌더러 크기에 맞춰 CharacterController 캡슐 크기를 정한다.</summary>
        private static void FitCharacterController(CharacterController controller, GameObject source)
        {
            float height = 1.8f;
            float radius = 0.35f;
            float centerY = 0.9f;

            GameObject temp = Object.Instantiate(source);
            try
            {
                temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Renderer[] renderers = temp.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                    if (bounds.size.y > 0.2f)
                    {
                        height = Mathf.Clamp(bounds.size.y, 0.8f, 4f);
                        radius = Mathf.Clamp(bounds.size.z * 0.5f, 0.25f, 0.6f);
                        centerY = bounds.center.y;
                    }
                }
                else
                {
                    var capsule = source.GetComponent<CapsuleCollider>();
                    if (capsule != null)
                    {
                        height = capsule.height;
                        radius = capsule.radius;
                        centerY = capsule.center.y;
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }

            radius = Mathf.Min(radius, height * 0.5f - 0.01f);
            controller.height = height;
            controller.radius = radius;
            controller.center = new Vector3(0f, centerY + controller.skinWidth, 0f);
            controller.stepOffset = Mathf.Min(0.3f, height * 0.25f);
            controller.slopeLimit = 50f;
            controller.minMoveDistance = 0f;
        }

        #endregion

        #region 캐릭터별 Variant

        private static NetworkObject CreateVariant(GameObject basePrefab, CharacterStatsData data, string path)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = System.IO.Path.GetFileNameWithoutExtension(path);

            // 모델(메쉬 + 애니메이터)만 가져오고 게임플레이 스크립트/물리는 뺀다.
            var model = (GameObject)PrefabUtility.InstantiatePrefab(data.modelPrefab);
            if (model == null) model = Object.Instantiate(data.modelPrefab);
            if (PrefabUtility.IsPartOfPrefabInstance(model))
            {
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            StripToVisual(model);

            model.name = "Model";
            model.tag = "Untagged";
            model.transform.SetParent(instance.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;

            Character character = instance.GetComponent<Character>();
            if (character != null) character.stats = data;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return saved != null ? saved.GetComponent<NetworkObject>() : null;
        }

        /// <summary>모델에서 보이는 것(Transform/렌더러/애니메이터 등)만 남긴다.</summary>
        private static void StripToVisual(GameObject model)
        {
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }

            // 스크립트 → 물리 순서로 지운다. [RequireComponent]로 서로 묶인 스크립트는 한 번에 안 지워질 수 있어서
            // (예: WeaponController가 PlayerInventory를 요구) 다 없어질 때까지 몇 번 반복한다.
            for (int pass = 0; pass < 8; pass++)
            {
                MonoBehaviour[] remaining = model.GetComponentsInChildren<MonoBehaviour>(true);
                if (remaining.Length == 0) break;
                for (int i = remaining.Length - 1; i >= 0; i--)
                {
                    if (remaining[i] != null) Object.DestroyImmediate(remaining[i]);
                }
            }
            if (model.GetComponentsInChildren<MonoBehaviour>(true).Length > 0)
            {
                Debug.LogWarning($"[NetworkCharacterBuilder] {model.name} 모델에서 지우지 못한 스크립트가 있습니다. 프리팹의 Model 자식을 확인해 주세요.");
            }
            foreach (Joint joint in model.GetComponentsInChildren<Joint>(true))
            {
                Object.DestroyImmediate(joint);
            }
            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
            {
                Object.DestroyImmediate(body);
            }
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
        }

        #endregion
    }
}
