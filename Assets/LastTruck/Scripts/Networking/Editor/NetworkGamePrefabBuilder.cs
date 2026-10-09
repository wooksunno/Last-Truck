using System.Linq;
using System.Text;
using Fusion;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace LastTruck.Networking.EditorTools
{
    /// <summary>
    /// 게임 씬에서 호스트가 스폰하는 네트워크 프리팹(캐릭터 제외)을 만든다. 메뉴 1번이 같이 실행한다.
    ///
    ///  - Prefabs/NetworkGameState.prefab : NetworkObject + NetworkGameState (준비 대기, 낮/밤 동기화, 게임 오버)
    ///      + NetworkTruckInventory (트럭 인벤토리 실시간 동기화)
    ///      + NetworkTruck (트럭 탑승/운전/라이트), NetworkGathering (특산물 채집/고갈)
    ///  - Prefabs/Monsters/Monster_Network.prefab : 기존 Monster.prefab의 Variant
    ///      + NetworkObject + NetworkTransform(위치 동기화) + NetworkHealth(체력) + NetworkMonster(호스트 AI)
    ///    → 원본 Monster.prefab의 모양/크기/Damageable 값을 고치면 그대로 따라온다.
    ///  - Resources/NetworkPrefabRegistry.asset : 위 두 프리팹 + 게임 씬 UI 폰트를 연결 (GameLauncher, MonsterSpawner, InGameFonts가 읽는다)
    ///
    /// 이미 있으면 새로 만들지 않고 빠진 컴포넌트만 채운다.
    /// </summary>
    public static class NetworkGamePrefabBuilder
    {
        #region 경로

        public const string RegistryPath = LobbyEditorUI.Root + "/Resources/NetworkPrefabRegistry.asset";
        public const string GameStatePrefabPath = LobbyEditorUI.Root + "/Prefabs/NetworkGameState.prefab";
        public const string MonsterPrefabDir = LobbyEditorUI.Root + "/Prefabs/Monsters";
        public const string MonsterPrefabPath = MonsterPrefabDir + "/Monster_Network.prefab";
        private const string DefaultMonsterSourcePath = "Assets/Mr.No/Monster/Monster.prefab";

        #endregion

        #region 전체 흐름

        public static void Build(StringBuilder log)
        {
            LobbyEditorUI.EnsureFolder(LobbyEditorUI.Root + "/Resources");
            LobbyEditorUI.EnsureFolder(MonsterPrefabDir);

            NetworkPrefabRegistry registry = AssetDatabase.LoadAssetAtPath<NetworkPrefabRegistry>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<NetworkPrefabRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
                log.AppendLine($"- 네트워크 프리팹 목록 생성: {RegistryPath}");
            }

            registry.gameStatePrefab = EnsureGameStatePrefab(log);
            registry.monsterPrefab = EnsureMonsterPrefab(log);
            registry.uiFont = EnsureUIFont(log);
            registry.tmpFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(LobbyEditorUI.FontAssetPath);
            EditorUtility.SetDirty(registry);
        }

        #endregion

        #region 게임 상태

        private static NetworkObject EnsureGameStatePrefab(StringBuilder log)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(GameStatePrefabPath);
            if (existing == null)
            {
                var root = new GameObject("NetworkGameState");
                root.AddComponent<NetworkObject>();
                existing = PrefabUtility.SaveAsPrefabAsset(root, GameStatePrefabPath);
                Object.DestroyImmediate(root);
                log.AppendLine($"- 게임 상태 프리팹: {GameStatePrefabPath}");
            }

            // 필요한 컴포넌트 채우기: 진행 상태 + 트럭 인벤토리 동기화
            GameObject contents = PrefabUtility.LoadPrefabContents(GameStatePrefabPath);
            try
            {
                bool changed = false;
                if (contents.GetComponent<NetworkObject>() == null) { contents.AddComponent<NetworkObject>(); changed = true; }
                if (contents.GetComponent<NetworkGameState>() == null) { contents.AddComponent<NetworkGameState>(); changed = true; }
                if (contents.GetComponent<NetworkTruckInventory>() == null)
                {
                    contents.AddComponent<NetworkTruckInventory>();
                    changed = true;
                    log.AppendLine("- 게임 상태 프리팹: 트럭 인벤토리 동기화(NetworkTruckInventory) 추가");
                }
                if (contents.GetComponent<NetworkTruck>() == null)
                {
                    contents.AddComponent<NetworkTruck>();
                    changed = true;
                    log.AppendLine("- 게임 상태 프리팹: 트럭 탑승/운전 동기화(NetworkTruck) 추가");
                }
                if (contents.GetComponent<NetworkGathering>() == null)
                {
                    contents.AddComponent<NetworkGathering>();
                    changed = true;
                    log.AppendLine("- 게임 상태 프리팹: 특산물 채집 동기화(NetworkGathering) 추가");
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, GameStatePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            existing = AssetDatabase.LoadAssetAtPath<GameObject>(GameStatePrefabPath);
            return existing != null ? existing.GetComponent<NetworkObject>() : null;
        }

        #endregion

        #region 게임 씬 UI 폰트

        private const string UIFontPath = LobbyEditorUI.Root + "/Fonts/Pretendard-Regular.ttf";

        /// <summary>
        /// 게임 씬 legacy Text용 한글 폰트 (Pretendard ttf). 작은 글자도 또렷하게 힌팅 렌더링으로 맞춘다.
        /// </summary>
        private static Font EnsureUIFont(StringBuilder log)
        {
            var importer = AssetImporter.GetAtPath(UIFontPath) as TrueTypeFontImporter;
            if (importer == null)
            {
                log.AppendLine($"- UI 폰트를 찾지 못했습니다: {UIFontPath} (게임 씬 텍스트는 기본 폰트 유지)");
                return null;
            }

            if (importer.fontTextureCase != FontTextureCase.Dynamic || importer.fontRenderingMode != FontRenderingMode.HintedSmooth)
            {
                importer.fontTextureCase = FontTextureCase.Dynamic;
                importer.fontRenderingMode = FontRenderingMode.HintedSmooth;
                importer.includeFontData = true;
                importer.SaveAndReimport();
                log.AppendLine("- UI 폰트(Pretendard) 렌더링 설정: Dynamic + Hinted Smooth");
            }

            return AssetDatabase.LoadAssetAtPath<Font>(UIFontPath);
        }

        #endregion

        #region 몬스터

        private static NetworkObject EnsureMonsterPrefab(StringBuilder log)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath);
            if (existing == null)
            {
                GameObject source = FindMonsterSource();
                if (source == null)
                {
                    log.AppendLine("- 몬스터 원본 프리팹(MonsterAI가 붙은 프리팹)을 찾지 못해 네트워크 몬스터를 만들지 못했습니다.");
                    return null;
                }

                // 원본 몬스터의 Variant로 만든다 (원본을 고치면 따라온다).
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.name = "Monster_Network";
                existing = PrefabUtility.SaveAsPrefabAsset(instance, MonsterPrefabPath);
                Object.DestroyImmediate(instance);
                log.AppendLine($"- 네트워크 몬스터 프리팹: {MonsterPrefabPath} (원본: {source.name})");
            }

            // 필요한 컴포넌트 채우기 (이미 있으면 그대로)
            GameObject contents = PrefabUtility.LoadPrefabContents(MonsterPrefabPath);
            try
            {
                bool changed = false;
                if (contents.GetComponent<NetworkObject>() == null) { contents.AddComponent<NetworkObject>(); changed = true; }
                if (contents.GetComponent<NetworkTransform>() == null) { contents.AddComponent<NetworkTransform>(); changed = true; }
                if (contents.GetComponent<NavMeshAgent>() == null) { contents.AddComponent<NavMeshAgent>(); changed = true; }

                if (contents.GetComponent<NetworkHealth>() == null)
                {
                    NetworkHealth health = contents.AddComponent<NetworkHealth>();
                    var so = new SerializedObject(health);
                    so.FindProperty("isPlayer").boolValue = false;
                    so.FindProperty("despawnDelay").floatValue = 0.3f; // 사망 연출이 모든 화면에 뜰 만큼만 남겼다가 사라짐
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (contents.GetComponent<NetworkMonster>() == null) { contents.AddComponent<NetworkMonster>(); changed = true; }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, MonsterPrefabPath);
                    log.AppendLine("- 네트워크 몬스터: NetworkObject/NetworkTransform/NetworkHealth/NetworkMonster 추가");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            existing = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath);
            return existing != null ? existing.GetComponent<NetworkObject>() : null;
        }

        private static GameObject FindMonsterSource()
        {
            GameObject preferred = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultMonsterSourcePath);
            if (preferred != null && preferred.GetComponent<MonsterAI>() != null) return preferred;

            return AssetDatabase.FindAssets("t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith("Assets/") && !p.StartsWith(LobbyEditorUI.Root))
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .FirstOrDefault(p => p != null && p.GetComponent<MonsterAI>() != null && p.GetComponent<NetworkObject>() == null);
        }

        #endregion
    }
}
