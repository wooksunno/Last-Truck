using CraftingSystem;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 인게임 ESC 메뉴: 창이 아무것도 열려 있지 않을 때 ESC → "게임 나가기" 확인 팝업 → 방 목록으로.
    /// (인벤토리/제작 창이 열려 있을 때의 ESC는 기존처럼 그 창을 닫는다.)
    /// 방장이 나가면 게임이 끝나고 모두 방 목록으로 돌아간다 (팝업에 안내).
    /// </summary>
    public class InGameMenu : MonoBehaviour
    {
        private bool _popupWasOpen;

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            // 같은 프레임에 ESC로 제작 창이 닫히는 경우(WorldClickInteractor)와 겹치지 않게 지난 프레임 상태도 본다.
            if (_popupWasOpen || IsGamePopupOpen) return;

            SystemUI system = SystemUI.Instance;
            if (system != null && (system.IsPopupVisible || system.IsLoadingVisible)) return;
            if (NetworkGameState.IsGameOver) return;

            GameLauncher launcher = GameLauncher.Instance;
            if (launcher != null) launcher.RequestLeaveFromGame();
        }

        private void LateUpdate()
        {
            _popupWasOpen = IsGamePopupOpen;
        }

        private static bool IsGamePopupOpen => GameUIController.Instance != null && GameUIController.Instance.IsPopupOpen;
    }
}
