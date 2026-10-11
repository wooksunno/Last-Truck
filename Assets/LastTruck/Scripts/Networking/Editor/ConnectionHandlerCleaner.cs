using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastTruck.Networking.EditorTools
{
    /// <summary>
    /// 플레이를 멈추는 순간 포톤이 접속을 시도하면 ConnectionHandler 오브젝트가 DontDestroyOnLoad 없이
    /// 열려 있는 씬(Menu 등)에 남는다. 편집 모드로 돌아오면(그리고 씬을 열 때) 자동으로 지운다.
    /// 씬에 저장되면 다음 플레이 때 포톤 경고가 뜨므로 저장 전에 치우는 용도.
    /// </summary>
    [InitializeOnLoad]
    internal static class ConnectionHandlerCleaner
    {
        #region 설정

        private const string ObjectName = "ConnectionHandler";
        private const double WatchSeconds = 5.0; // 종료 직후 늦게 생기는 경우까지 잠깐 지켜본다

        private static double _watchUntil;

        #endregion

        #region 등록

        static ConnectionHandlerCleaner()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneOpened += (scene, mode) => CleanScene(scene);
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;

            CleanAll();
            _watchUntil = EditorApplication.timeSinceStartup + WatchSeconds;
            EditorApplication.update -= Watch;
            EditorApplication.update += Watch;
        }

        private static void Watch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup > _watchUntil)
            {
                EditorApplication.update -= Watch;
                return;
            }
            CleanAll();
        }

        #endregion

        #region 정리

        private static void CleanAll()
        {
            if (EditorApplication.isPlaying) return;
            for (int i = 0; i < SceneManager.sceneCount; i++) CleanScene(SceneManager.GetSceneAt(i));
        }

        private static void CleanScene(Scene scene)
        {
            if (EditorApplication.isPlaying || !scene.IsValid() || !scene.isLoaded) return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == null || !IsConnectionHandler(root)) continue;
                Debug.Log($"[ConnectionHandlerCleaner] 플레이 종료 때 '{scene.name}' 씬에 남은 {ObjectName} 오브젝트를 지웠습니다.");
                Object.DestroyImmediate(root);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static bool IsConnectionHandler(GameObject go)
        {
            if (go.name != ObjectName) return false;
            foreach (MonoBehaviour behaviour in go.GetComponents<MonoBehaviour>())
            {
                if (behaviour != null && behaviour.GetType().Name == ObjectName) return true;
            }
            // 스크립트가 빠진 채로 저장된 경우(컴포넌트 없음)도 지운다.
            return go.GetComponents<Component>().Length <= 1 && go.transform.childCount == 0;
        }

        #endregion
    }
}
