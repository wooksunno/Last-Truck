using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastTruck.Networking
{
    /// <summary>
    /// 게임 씬 legacy UI Text 폰트 교체.
    ///  - 씬에 배치된 Text 대부분이 유니티 기본 폰트(Arial/LegacyRuntime)를 쓰는데, 이 폰트엔 한글이 없어서
    ///    운영체제 폰트로 대신 그려지며 작은 크기에서 뭉개지고 흐릿해 보인다.
    ///  - 씬이 로드될 때 기본 폰트를 쓰는 Text만 골라 Pretendard(NetworkPrefabRegistry.uiFont)로 바꾼다.
    ///    (팀원이 일부러 다른 폰트를 넣은 Text는 건드리지 않는다)
    ///  - GameUIController가 코드로 만드는 Text도 LegacyFont를 사용한다.
    /// </summary>
    public static class InGameFonts
    {
        #region 조회

        /// <summary>게임 UI용 한글 폰트 (없으면 null → 기존 폰트 유지).</summary>
        public static Font LegacyFont
        {
            get
            {
                NetworkPrefabRegistry registry = NetworkPrefabRegistry.Instance;
                return registry != null ? registry.uiFont : null;
            }
        }

        private static bool IsBuiltinFont(Font font)
        {
            if (font == null) return true;
            string name = font.name;
            return name == "Arial" || name == "LegacyRuntime" || name == "LegacySans";
        }

        #endregion

        #region 씬 로드 시 교체

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyToLoadedScenes();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToLoadedScenes();

        /// <summary>현재 로드된 모든 기본 폰트 Text를 교체한다. 코드로 나중에 만든 Text에도 불러도 된다.</summary>
        public static void ApplyToLoadedScenes()
        {
            Font font = LegacyFont;
            if (font == null) return;

            Text[] texts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Text text in texts)
            {
                if (text != null && IsBuiltinFont(text.font)) text.font = font;
            }
        }

        #endregion
    }
}
