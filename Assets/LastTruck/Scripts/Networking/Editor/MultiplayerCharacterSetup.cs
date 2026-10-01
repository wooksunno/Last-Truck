using UnityEditor;
using UnityEditor.SceneManagement;

namespace LastTruck.Networking.EditorTools
{
    /// <summary>
    /// 메뉴 "LastTruck > Multiplayer > 0. 캐릭터 멀티플레이 전체 설치 (1~3 한 번에)".
    /// 1. 네트워크 프리팹 생성(빠진 것만) → 2. 초상화 전체 촬영(저장된 구도 설정) → 3. Menu 씬에 캐릭터 선택 UI 설치.
    /// 각 단계는 메뉴에서 따로 다시 실행할 수 있다.
    /// </summary>
    public static class MultiplayerCharacterSetup
    {
        private const string DialogTitle = "Last Truck - 캐릭터 멀티플레이 설치";

        [MenuItem("LastTruck/Multiplayer/0. 캐릭터 멀티플레이 전체 설치 (1~3 한 번에)", priority = 100)]
        private static void RunAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(DialogTitle, "플레이 모드에서는 실행할 수 없습니다.", "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string step1 = NetworkCharacterBuilder.Build(false);
            string step2 = CharacterPortraitCapture.CaptureAll(CharacterPortraitCapture.LoadSettings());
            string step3 = CharacterSelectInstaller.Install();

            UnityEngine.Debug.Log($"[MultiplayerCharacterSetup]\n[1] {step1}\n[2] {step2}\n[3] {step3}");
            EditorUtility.DisplayDialog(DialogTitle,
                "[1] 네트워크 프리팹 (캐릭터/몬스터/게임 상태)\n" + step1 + "\n\n[2] 초상화\n" + step2 + "\n\n[3] 캐릭터 선택 UI\n" + step3 +
                "\n\n(전체 내용은 콘솔에도 남겼습니다)",
                "확인");
        }
    }
}
