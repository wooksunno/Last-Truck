using CraftingSystem;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 인게임 ESC 메뉴: 창이 아무것도 열려 있지 않을 때 ESC → 설정 메뉴(SettingsMenu) 열기, 열려 있으면 닫기.
    /// 게임 나가기는 설정 메뉴의 [게임 나가기] 버튼 → 기존 확인 팝업(GameLauncher.RequestLeaveFromGame).
    /// (인벤토리/제작 창이나 트럭 원형 메뉴가 열려 있을 때의 ESC는 기존처럼 그 창을 닫는다.)
    /// </summary>
    public class InGameMenu : MonoBehaviour
    {
        private bool _popupWasOpen;
        private bool _radialWasOpen;

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (ChatHud.ConsumedEscape) return; // Esc로 채팅창을 닫았다

            // 설정 메뉴가 열려 있으면 닫기만 한다
            if (SettingsMenu.IsOpen)
            {
                SettingsMenu.Toggle();
                return;
            }

            // 같은 프레임에 ESC로 제작 창/원형 메뉴가 닫히는 경우와 겹치지 않게 지난 프레임 상태도 본다.
            if (_popupWasOpen || IsGamePopupOpen || _radialWasOpen || TruckRadialMenu.IsOpen) return;

            SystemUI system = SystemUI.Instance;
            if (system != null && (system.IsPopupVisible || system.IsLoadingVisible)) return;
            if (NetworkGameState.IsGameOver) return;

            SettingsMenu.Toggle();
        }

        private void LateUpdate()
        {
            _popupWasOpen = IsGamePopupOpen;
            _radialWasOpen = TruckRadialMenu.IsOpen;
        }

        // 트럭 창 등 게임 창만 본다 (설정 메뉴는 위에서 따로 처리)
        private static bool IsGamePopupOpen => GameUIController.Instance != null && GameUIController.Instance.IsOwnPopupOpen;
    }
}
