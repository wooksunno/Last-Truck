using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck.Networking
{
    /// <summary>LobbyToolkitView: 타이틀 화면 / 제작진 / 처음 실행 때 닉네임 정하기.</summary>
    public partial class LobbyToolkitView
    {
        #region 타이틀

        private Label _logoGlow;
        private Button _playBtn;

        private VisualElement BuildTitleScreen()
        {
            var screen = Box("mn-screen");
            var col = Box("mn-title-col");
            screen.Add(col);

            var logoBox = Box("mn-logo-box");
            _logoGlow = L("LAST TRUCK", "mn-logo mn-logo-glow", FontKind.Stencil);
            logoBox.Add(_logoGlow);
            logoBox.Add(L("LAST TRUCK", "mn-logo", FontKind.Stencil));
            col.Add(logoBox);
            col.Add(L("마지막 트럭을 지켜라", "mn-subtitle", FontKind.Heavy));

            _playBtn = Btn("플레이", OnPlayClicked, "primary", null, true, "mn-title-btn mn-title-btn--play");
            col.Add(_playBtn);
            col.Add(Btn("설정", () => OpenSettings(), null, null, true, "mn-title-btn"));
            col.Add(Btn("제작진", () => Show(_creditsDim, true), null, null, true, "mn-title-btn"));
            col.Add(Btn("게임 종료", QuitGame, null, null, true, "mn-title-btn"));
            return screen;
        }

        private bool _waitingForLobby;

        private void OnPlayClicked()
        {
            if (_launcher == null) return;
            ShowScreen(MenuScreen.List, false);
            switch (_launcher.State)
            {
                case LauncherState.Offline:
                    _launcher.EnterLobby();   // 조용한 접속이 실패했었다 → 이번에는 로딩 창 + 실패 알림과 함께 다시
                    break;
                case LauncherState.ConnectingLobby:
                    _waitingForLobby = true;
                    SystemUI.ShowLoading("서버에 접속하는 중...");
                    break;
            }
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion

        #region 제작진

        private VisualElement _creditsDim;

        private void BuildCreditsModal()
        {
            _creditsDim = Modal(out VisualElement w, "mn-credits");
            w.Add(Header("제작진", "CREDITS", Btn(null, () => Show(_creditsDim, false), null, "close", false, "mn-copy--small")));
            w.Add(Tape());
            w.Add(L("LAST TRUCK", "mn-h1", FontKind.Stencil));
            w.Add(L("Last Truck Team · 캡스톤 디자인", "mn-credit-line"));
            w.Add(L("기획 · 프로그래밍 · 아트 · 사운드", "mn-credit-line mn-dimtext"));
            w.Add(L("(제작진 목록은 준비 중입니다)", "mn-credit-line mn-dimtext"));
            w.Add(L("글꼴: Pretendard, Black Ops One (SIL Open Font License)", "mn-hint"));
            w.Add(L("네트워크: Photon Fusion 2", "mn-hint"));
        }

        #endregion

        #region 처음 실행: 닉네임 정하기

        private VisualElement _nickDim;
        private TextField _firstNickField;
        private Label _firstNickError;

        private void BuildNicknamePopup()
        {
            _nickDim = Modal(out VisualElement w, "mn-modal");
            w.Add(Header("닉네임 정하기", "WHO ARE YOU"));
            w.Add(Tape());
            w.Add(L("방에 들어가면 다른 사람에게 보이는 이름이에요. 나중에 설정에서 바꿀 수 있어요.", "mn-label"));
            _firstNickField = Field(null, LobbyRules.NicknameMaxLength);
            _firstNickField.style.marginTop = 18;
            _firstNickField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) ConfirmFirstNickname();
            }, TrickleDown.TrickleDown);
            w.Add(_firstNickField);
            _firstNickError = L($"{LobbyRules.NicknameMinLength}~{LobbyRules.NicknameMaxLength}자", "mn-hint");
            w.Add(_firstNickError);
            var buttons = Box("mn-modal-buttons");
            buttons.Add(Btn("확인", ConfirmFirstNickname, "primary"));
            w.Add(buttons);
        }

        private void OpenNicknamePopup()
        {
            _firstNickField.SetValueWithoutNotify(PlayerProfile.Nickname);
            _firstNickError.text = $"{LobbyRules.NicknameMinLength}~{LobbyRules.NicknameMaxLength}자";
            _firstNickError.RemoveFromClassList("mn-orange");
            Show(_nickDim, true);
            _nickDim.BringToFront();
            _firstNickField.schedule.Execute(() => _firstNickField.Focus()).StartingIn(50);
        }

        private void ConfirmFirstNickname()
        {
            if (!PlayerProfile.TrySetNickname(_firstNickField.value, out string error))
            {
                _firstNickError.text = error;
                _firstNickError.AddToClassList("mn-orange");
                return;
            }
            Show(_nickDim, false);
        }

        #endregion
    }
}
