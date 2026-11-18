using System.Text;
using Fusion;
using UnityEditor;
using UnityEngine;

namespace LastTruck.Networking.EditorTools
{
    /// <summary>
    /// 몬스터 종류별 프리팹을 만든다: Assets/LastTruck/Multiplayer/Prefabs/Monsters/Monster_{종류}.prefab
    ///  - 기본 Monster_Network.prefab의 Variant (NetworkObject/NetworkMonster 등은 부모에서 이어받음).
    ///  - 루트 Scale, MonsterAppearance(종류/색/애니메이션 속도), ZombieVariant.AllowedBodies(Synty 몸 메시 목록)를
    ///    종류 표(MonsterCatalog)의 모습 값으로 처음 한 번 채운다. 이후에는 프리팹에서 직접 고치면 된다(모델 교체 가능).
    ///  - NetworkPrefabRegistry.monsterKindPrefabs에 종류 순서대로 연결한다.
    /// 메뉴 1(네트워크 프리팹 생성)도 없는 종류만 만들어 준다(이미 있으면 건드리지 않음).
    /// </summary>
    public static class MonsterKindPrefabBuilder
    {
        private const string DialogTitle = "몬스터 종류별 프리팹";

        #region 메뉴

        [MenuItem("LastTruck/Multiplayer/3. 몬스터 종류별 프리팹 만들기", priority = 103)]
        public static void BuildFromMenu()
        {
            NetworkPrefabRegistry registry = AssetDatabase.LoadAssetAtPath<NetworkPrefabRegistry>(NetworkGamePrefabBuilder.RegistryPath);
            if (registry == null || registry.monsterPrefab == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, "기본 몬스터 프리팹이 없습니다.\n먼저 메뉴 '1. 네트워크 프리팹 생성'을 실행하세요.", "확인");
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex(DialogTitle,
                "종류별 프리팹(Monster_Walker, Monster_Tank ...)을 만듭니다.\n\n이미 있는 프리팹은 어떻게 할까요?",
                "없는 것만 만들기", "취소", "모습 값을 표 기본값으로 다시 넣기");
            if (choice == 1) return;

            var log = new StringBuilder();
            Build(registry, choice == 2, log);
            AssetDatabase.SaveAssets();
            if (!EditorApplication.ExecuteMenuItem("Tools/Fusion/Rebuild Prefab Table"))
                log.AppendLine("\n※ 'Tools/Fusion/Rebuild Prefab Table'을 직접 한 번 실행해 주세요.");
            EditorUtility.DisplayDialog(DialogTitle, "완료했습니다.\n\n" + log, "확인");
        }

        #endregion

        #region 만들기

        public static void Build(NetworkPrefabRegistry registry, bool overwriteAppearance, StringBuilder log)
        {
            if (registry == null || registry.monsterPrefab == null) return;
            LobbyEditorUI.EnsureFolder(NetworkGamePrefabBuilder.MonsterPrefabDir);

            MonsterCatalog catalog = MonsterCatalog.Instance;
            int count = System.Enum.GetValues(typeof(MonsterKind)).Length;
            if (registry.monsterKindPrefabs == null || registry.monsterKindPrefabs.Length != count)
            {
                var resized = new NetworkObject[count];
                if (registry.monsterKindPrefabs != null)
                    for (int i = 0; i < Mathf.Min(count, registry.monsterKindPrefabs.Length); i++) resized[i] = registry.monsterKindPrefabs[i];
                registry.monsterKindPrefabs = resized;
            }

            GameObject baseAsset = registry.monsterPrefab.gameObject;
            foreach (MonsterKind kind in System.Enum.GetValues(typeof(MonsterKind)))
            {
                MonsterTypeData data = catalog.Get(kind);
                string path = $"{NetworkGamePrefabBuilder.MonsterPrefabDir}/Monster_{kind}.prefab";
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (existing == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(baseAsset);
                    try
                    {
                        instance.name = $"Monster_{kind}";
                        ApplyAppearance(instance, kind, data);
                        existing = PrefabUtility.SaveAsPrefabAsset(instance, path);
                        log.AppendLine($"- 새로 만듦: {path} ({data.displayName})");
                    }
                    finally
                    {
                        Object.DestroyImmediate(instance);
                    }
                }
                else if (overwriteAppearance)
                {
                    GameObject contents = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        ApplyAppearance(contents, kind, data);
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        log.AppendLine($"- 모습 값 다시 넣음: {path}");
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(contents);
                    }
                }

                NetworkObject networkObject = existing != null ? existing.GetComponent<NetworkObject>() : null;
                if (registry.monsterKindPrefabs[(int)kind] != networkObject)
                {
                    registry.monsterKindPrefabs[(int)kind] = networkObject;
                    log.AppendLine($"- 목록 연결: {kind} → {(networkObject != null ? networkObject.name : "(없음)")}");
                }
            }
            EditorUtility.SetDirty(registry);
        }

        /// <summary>종류 표의 모습 값(크기/색/애니메이션 속도/몸 메시 목록)을 프리팹에 넣는다.</summary>
        private static void ApplyAppearance(GameObject root, MonsterKind kind, MonsterTypeData data)
        {
            root.transform.localScale = Vector3.one * Mathf.Max(0.2f, data.scale);

            MonsterAppearance appearance = root.GetComponent<MonsterAppearance>();
            if (appearance == null) appearance = root.AddComponent<MonsterAppearance>();
            appearance.kind = kind;
            appearance.tint = data.tint;
            appearance.animationSpeed = Mathf.Max(0.1f, data.animationSpeed);
            EditorUtility.SetDirty(appearance);

            ZombieVariant variant = root.GetComponent<ZombieVariant>();
            if (variant == null) variant = root.AddComponent<ZombieVariant>();
            variant.AllowedBodies = data.bodies != null ? (string[])data.bodies.Clone() : new string[0];
            EditorUtility.SetDirty(variant);

            // 프리팹에서 바로 보이도록: 이 종류의 모델(목록의 첫 번째) 몸 메시만 켜고 나머지 Synty 몸 메시는 끈다.
            string main = data.bodies != null && data.bodies.Length > 0 ? data.bodies[0] : null;
            if (string.IsNullOrEmpty(main)) return;
            bool found = false;
            foreach (SkinnedMeshRenderer body in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (body == null || !body.name.StartsWith("Zombie_")) continue;
                if (body.name == main) found = true;
                body.gameObject.SetActive(body.name == main);
            }
            if (!found) Debug.LogWarning($"[MonsterKindPrefabBuilder] {kind}: 몸 메시 '{main}'을 찾지 못했습니다.");
        }

        #endregion
    }
}
