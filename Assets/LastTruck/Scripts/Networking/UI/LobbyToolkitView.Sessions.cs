using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck.Networking
{
    /// <summary>
    /// LobbyToolkitView: 방 목록 화면 (+ 방 만들기 / 비밀번호 입력 모달, 코드로 참가, 재접속).
    /// 재접속할 수 있는 게임이 있으면: 그 방이 맨 위(초록), 남은 시간 안내, 다른 방 / 방 만들기 / 코드로 참가는 막힌다.
    /// </summary>
    public partial class LobbyToolkitView
    {
        #region 방 목록 화면

        private ScrollView _sessionScroll;
        private Label _sessionEmpty, _reconnectBanner, _codeError;
        private Button _createBtn, _codeJoinBtn, _rejoinBtn, _abandonBtn, _refreshBtn;
        private VisualElement _rejoinBox;
        private readonly TextField[] _codeCells = new TextField[LobbyRules.RoomCodeLength];
        private readonly List<SessionRowUi> _sessionRows = new List<SessionRowUi>();
        private bool _rejoinMode;

        private class SessionRowUi
        {
            public Button root;
            public VisualElement icon;
            public Label name, state, count;
            public SessionInfo session;
            public bool rejoin;
        }

        private VisualElement BuildListScreen()
        {
            var screen = Box("mn-screen");
            screen.Add(Box("mn-shade"));
            var center = Box("mn-center");
            screen.Add(center);
            var two = Box("mn-two");
            center.Add(two);

            // 왼쪽: 방 목록
            var left = Plate("mn-left");
            _refreshBtn = Btn("새로고침", RefreshSessionList, null, "refresh");
            _refreshBtn.style.height = 50;
            _refreshBtn.style.fontSize = 20;
            left.Add(Header("방 목록", "SESSIONS", _refreshBtn));
            left.Add(Tape());
            _reconnectBanner = L("", "mn-banner");
            _reconnectBanner.enableRichText = true;
            left.Add(_reconnectBanner);

            var head = Box("mn-list-head");
            head.Add(L("", "mn-col-icon"));
            head.Add(L("방 이름", "mn-col-name"));
            head.Add(L("상태", "mn-col-state"));
            head.Add(L("인원", "mn-col-count"));
            left.Add(head);

            _sessionScroll = new ScrollView(ScrollViewMode.Vertical);
            _sessionScroll.AddToClassList("mn-scroll");
            left.Add(_sessionScroll);
            _sessionEmpty = L("아직 열린 방이 없어요.\n오른쪽 [방 만들기]로 첫 방을 열어 보세요!", "mn-empty");
            _sessionScroll.Add(_sessionEmpty);
            two.Add(left);

            // 오른쪽: 방 만들기 / 코드로 참가 / 재접속 / 타이틀로
            var right = Plate("mn-right");
            _createBtn = Btn("방 만들기", OpenCreateRoomPanel, "primary", null, true);
            _createBtn.style.height = 84;
            _createBtn.style.fontSize = 32;
            right.Add(_createBtn);

            var codeTitle = L("코드로 참가", "mn-h2", FontKind.Heavy);
            codeTitle.style.marginTop = 40;
            right.Add(codeTitle);
            var codeRow = Box("mn-code-row");
            for (int i = 0; i < _codeCells.Length; i++)
            {
                int index = i;
                var cell = new TextField { maxLength = 1 };
                cell.AddToClassList("mn-code-cell");
                SetFont(cell, FontKind.Stencil);
                cell.RegisterValueChangedCallback(e => OnCodeCellChanged(index, e.newValue));
                cell.RegisterCallback<KeyDownEvent>(e => OnCodeCellKey(index, e), TrickleDown.TrickleDown);
                _codeCells[i] = cell;
                codeRow.Add(cell);
            }
            right.Add(codeRow);
            _codeJoinBtn = Btn("참가", JoinByCode);
            _codeJoinBtn.style.marginTop = 18;
            right.Add(_codeJoinBtn);
            _codeError = L("", "mn-toast mn-orange");
            right.Add(_codeError);

            _rejoinBox = Box(null);
            _rejoinBox.Add(Box("mn-sep"));
            _rejoinBtn = Btn("다시 들어가기", RejoinClicked, "green", "rejoin");
            _rejoinBtn.style.height = 70;
            _rejoinBtn.style.fontSize = 26;
            _rejoinBox.Add(_rejoinBtn);
            _abandonBtn = Btn("재접속 포기", AbandonClicked);
            _abandonBtn.style.height = 48;
            _abandonBtn.style.fontSize = 18;
            _abandonBtn.style.marginTop = 10;
            _rejoinBox.Add(_abandonBtn);
            right.Add(_rejoinBox);

            right.Add(Grow());
            right.Add(Btn("타이틀로", () => ShowScreen(MenuScreen.Title, false), null, "back"));
            two.Add(right);
            return screen;
        }

        private void RefreshSessionList()
        {
            if (_launcher == null) return;
            if (_launcher.State == LauncherState.InLobby || _launcher.State == LauncherState.Offline) _launcher.EnterLobby();
        }

        private void OnSessionListUpdated(IReadOnlyList<SessionInfo> sessions)
        {
            if (_sessionScroll == null) return;
            sessions ??= Array.Empty<SessionInfo>();
            _rejoinMode = _launcher != null && _launcher.HasReconnect;

            // 재접속할 방이 맨 위 → 들어갈 수 있는 방 → 나머지. 같은 순서 안에서는 이름순.
            List<SessionInfo> sorted = sessions
                .Where(s => s != null)
                .OrderByDescending(GameLauncher.CanRejoin)
                .ThenByDescending(GameLauncher.IsJoinable)
                .ThenBy(GameLauncher.ReadTitle)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                if (i >= _sessionRows.Count) _sessionRows.Add(MakeSessionRow());
                BindSessionRow(_sessionRows[i], sorted[i]);
                Show(_sessionRows[i].root, true);
            }
            for (int i = sorted.Count; i < _sessionRows.Count; i++) Show(_sessionRows[i].root, false);

            Show(_sessionEmpty, sorted.Count == 0);
            _sessionEmpty.text = _launcher != null && _launcher.State == LauncherState.ConnectingLobby
                ? "서버에 접속하는 중..."
                : _launcher != null && _launcher.State == LauncherState.Offline
                    ? "서버에 연결되어 있지 않아요.\n[새로고침]으로 다시 접속해 보세요."
                    : "아직 열린 방이 없어요.\n오른쪽 [방 만들기]로 첫 방을 열어 보세요!";
            RefreshListButtons();
            UpdateReconnectBanner();
        }

        private SessionRowUi MakeSessionRow()
        {
            var r = new SessionRowUi();
            r.root = new Button(() => OnSessionRowClicked(r)) { text = string.Empty, focusable = false };
            r.root.AddToClassList("mn-srow");
            var iconCol = Box("mn-col-icon");
            r.icon = Box("mn-row-icon");
            iconCol.Add(r.icon);
            r.root.Add(iconCol);
            var nameCol = Box("mn-col-name");
            r.name = L("", "mn-srow-name", FontKind.Heavy);
            nameCol.Add(r.name);
            r.root.Add(nameCol);
            r.state = L("", "mn-col-state");
            r.count = L("", "mn-col-count", FontKind.Heavy);
            r.root.Add(r.state);
            r.root.Add(r.count);
            _sessionScroll.Add(r.root);
            return r;
        }

        private void BindSessionRow(SessionRowUi r, SessionInfo session)
        {
            r.session = session;
            r.rejoin = GameLauncher.CanRejoin(session);
            bool inGame = GameLauncher.IsInGame(session);
            bool joinable = GameLauncher.IsJoinable(session) && (!_rejoinMode || r.rejoin);

            r.name.text = Plain(GameLauncher.ReadTitle(session));
            r.count.text = $"{session.PlayerCount} / {session.MaxPlayers}";

            Texture2D icon = r.rejoin ? Art("icon_rejoin") : GameLauncher.HasPassword(session) ? Art("icon_lock") : null;
            r.icon.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
            r.icon.EnableInClassList("mn-row-icon--green", r.rejoin);

            r.state.RemoveFromClassList("mn-green");
            if (r.rejoin) { r.state.text = "재접속 가능"; r.state.AddToClassList("mn-green"); }
            else if (inGame) r.state.text = "게임 중";
            else if (GameLauncher.IsFull(session)) r.state.text = "인원 마감";
            else r.state.text = "대기 중";

            r.root.EnableInClassList("mn-srow--rejoin", r.rejoin);
            r.root.EnableInClassList("mn-srow--off", !joinable);
            r.root.SetEnabled(joinable);
        }

        private void OnSessionRowClicked(SessionRowUi r)
        {
            if (r == null || r.session == null || _launcher == null || _launcher.State != LauncherState.InLobby) return;
            if (r.rejoin) { _launcher.RejoinLastSession(); return; }
            TryJoin(r.session);
        }

        /// <summary>목록 클릭 / 코드로 참가 공통: 막힌 방이면 이유, 비밀번호 방이면 비밀번호 창.</summary>
        private void TryJoin(SessionInfo session)
        {
            if (_launcher == null || _launcher.State != LauncherState.InLobby || session == null) return;
            if (GameLauncher.CanRejoin(session)) { _launcher.RejoinLastSession(); return; }
            if (_launcher.HasReconnect)
            {
                SystemUI.Alert("방 참가", "진행 중이던 게임이 있어요.\n다시 들어가거나 [재접속 포기]를 누른 뒤 다른 방에 들어갈 수 있어요.");
                return;
            }
            if (!GameLauncher.IsJoinable(session))
            {
                SystemUI.Alert("방 참가", GameLauncher.IsInGame(session) ? "이미 게임이 시작된 방이에요." : "방이 가득 찼어요.");
                return;
            }
            if (GameLauncher.HasPassword(session)) OpenPasswordPanel(session);
            else _launcher.JoinSession(session, null);
        }

        private void RefreshListButtons()
        {
            if (_createBtn == null || _launcher == null) return;
            bool inLobby = _launcher.State == LauncherState.InLobby;
            bool rejoin = _launcher.HasReconnect;
            _createBtn.SetEnabled(inLobby && !rejoin);
            _codeJoinBtn.SetEnabled(inLobby && !rejoin && ReadCodeCells().Length == LobbyRules.RoomCodeLength);
            foreach (TextField cell in _codeCells) cell.SetEnabled(!rejoin);
            _codeCells[0].parent.style.opacity = rejoin ? 0.4f : 1f;
            Show(_rejoinBox, rejoin);
            _rejoinBtn.SetEnabled(inLobby);
            _refreshBtn.SetEnabled(inLobby || _launcher.State == LauncherState.Offline);
        }

        private int _bannerSecond = -1;

        private void UpdateReconnectBanner()
        {
            if (_reconnectBanner == null || _launcher == null) return;
            bool rejoin = _launcher.HasReconnect;
            if (rejoin != _rejoinMode)
            {
                // 3분이 지났거나 포기했다 → 목록을 다시 그려 다른 방을 연다
                OnSessionListUpdated(_launcher.Sessions);
                return;
            }
            Show(_reconnectBanner, rejoin);
            if (!rejoin) { _bannerSecond = -1; return; }
            int left = Mathf.CeilToInt(_launcher.ReconnectSecondsLeft);
            if (left == _bannerSecond) return;
            _bannerSecond = left;
            _reconnectBanner.text =
                $"진행 중이던 게임 <b>{Plain(_launcher.ReconnectTitle)}</b>이(가) 있어요. <b>{left / 60}분 {left % 60:00}초</b> 안에 다시 들어가면 이어서 할 수 있어요. 그동안 다른 방에는 들어갈 수 없어요.";
        }

        private void RejoinClicked()
        {
            if (_launcher != null && _launcher.State == LauncherState.InLobby) _launcher.RejoinLastSession();
        }

        private void AbandonClicked()
        {
            if (_launcher == null || !_launcher.HasReconnect) return;
            SystemUI.Confirm("재접속 포기",
                "진행 중이던 게임으로 돌아갈 수 없게 돼요.\n(내 캐릭터의 가방은 트럭으로 옮겨집니다)\n포기할까요?",
                () => _launcher.AbandonReconnect(), null, "포기", "취소");
        }

        #endregion

        #region 코드로 참가 (6칸)

        private bool _codeEditing;

        private void OnCodeCellChanged(int index, string value)
        {
            if (_codeEditing) return;
            _codeEditing = true;
            string clean = GameLauncher.NormalizeCode(value);
            if (clean.Length > 1)
            {
                // 붙여넣기: 뒤 칸으로 나눠 넣는다
                for (int i = 0; i < clean.Length && index + i < _codeCells.Length; i++)
                    _codeCells[index + i].SetValueWithoutNotify(clean[i].ToString());
                int next = Mathf.Min(index + clean.Length, _codeCells.Length - 1);
                _codeCells[next].Focus();
            }
            else
            {
                _codeCells[index].SetValueWithoutNotify(clean);
                if (clean.Length == 1 && index + 1 < _codeCells.Length) _codeCells[index + 1].Focus();
            }
            _codeEditing = false;
            _codeError.RemoveFromClassList("mn-toast--show");
            RefreshListButtons();
        }

        private void OnCodeCellKey(int index, KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Backspace && string.IsNullOrEmpty(_codeCells[index].value) && index > 0)
            {
                _codeCells[index - 1].SetValueWithoutNotify(string.Empty);
                _codeCells[index - 1].Focus();
                RefreshListButtons();
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                JoinByCode();
            }
            else if ((e.ctrlKey || e.commandKey) && e.keyCode == KeyCode.V)
            {
                // 붙여넣기 단축키: 클립보드의 코드를 첫 칸부터 넣는다
                string clip = GameLauncher.NormalizeCode(GUIUtility.systemCopyBuffer);
                if (clip.Length > 0)
                {
                    e.StopImmediatePropagation();
                    for (int i = 0; i < _codeCells.Length; i++)
                        _codeCells[i].SetValueWithoutNotify(i < clip.Length ? clip[i].ToString() : string.Empty);
                    _codeCells[Mathf.Min(clip.Length, _codeCells.Length - 1)].Focus();
                    RefreshListButtons();
                }
            }
        }

        private string ReadCodeCells()
        {
            string code = string.Empty;
            foreach (TextField cell in _codeCells) code += cell.value ?? string.Empty;
            return GameLauncher.NormalizeCode(code);
        }

        private void JoinByCode()
        {
            if (_launcher == null || _launcher.State != LauncherState.InLobby || _launcher.HasReconnect) return;
            string code = ReadCodeCells();
            if (code.Length != LobbyRules.RoomCodeLength)
            {
                Flash(_codeError, $"코드 {LobbyRules.RoomCodeLength}자리를 모두 입력해 주세요.", 2.5f);
                return;
            }
            SessionInfo session = _launcher.FindSessionByCode(code);
            if (session == null)
            {
                Flash(_codeError, "그 코드의 방을 찾지 못했어요.", 2.5f);
                return;
            }
            foreach (TextField cell in _codeCells) cell.SetValueWithoutNotify(string.Empty);
            RefreshListButtons();
            TryJoin(session);
        }

        #endregion

        #region 비밀번호 입력

        private VisualElement _pwDim;
        private Label _pwRoomTitle;
        private TextField _joinPwField;
        private SessionInfo _pendingPasswordSession;

        private void BuildPasswordModal()
        {
            _pwDim = Modal(out VisualElement w, "mn-modal");
            w.Add(Header("비밀번호 입력", "LOCKED"));
            w.Add(Tape());
            _pwRoomTitle = L("", "mn-h2", FontKind.Heavy);
            w.Add(_pwRoomTitle);
            _joinPwField = Field(null, LobbyRules.PasswordMaxLength);
            _joinPwField.style.marginTop = 16;
            _joinPwField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) OnPasswordConfirmClicked();
            }, TrickleDown.TrickleDown);
            w.Add(_joinPwField);
            w.Add(L("방장에게 받은 비밀번호를 입력하세요.", "mn-hint"));
            var b = Box("mn-modal-buttons");
            b.Add(Btn("취소", ClosePasswordPanel));
            b.Add(Btn("참가", OnPasswordConfirmClicked, "primary"));
            w.Add(b);
        }

        private void OpenPasswordPanel(SessionInfo session)
        {
            _pendingPasswordSession = session;
            _pwRoomTitle.text = Plain(GameLauncher.ReadTitle(session));
            _joinPwField.SetValueWithoutNotify(string.Empty);
            Show(_pwDim, true);
            _pwDim.BringToFront();
            _joinPwField.schedule.Execute(() => _joinPwField.Focus()).StartingIn(30);
        }

        private void OnPasswordConfirmClicked()
        {
            if (!IsShown(_pwDim)) return;
            string password = (_joinPwField.value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(password))
            {
                SystemUI.Alert("비밀번호", "비밀번호를 입력해 주세요.");
                return;
            }
            SessionInfo session = _pendingPasswordSession;
            ClosePasswordPanel();
            if (session != null && _launcher != null) _launcher.JoinSession(session, password);
        }

        private void ClosePasswordPanel()
        {
            _pendingPasswordSession = null;
            Show(_pwDim, false);
        }

        #endregion

        #region 방 만들기

        private VisualElement _createDim, _createPwRow;
        private TextField _titleField, _createPwField;
        private Label _maxPlayersLabel, _createPwHint;
        private Button _minusBtn, _plusBtn, _publicBtn, _privateBtn;
        private int _createMaxPlayers = LobbyRules.DefaultMaxPlayers;
        private bool _createPrivate = LobbyRules.DefaultPrivate;

        private void BuildCreateModal()
        {
            _createDim = Modal(out VisualElement m, "mn-modal");
            m.Add(Header("방 만들기", "NEW ROOM"));
            m.Add(Tape());

            var r1 = Box("mn-form-row");
            r1.Add(L("방 이름", "mn-form-label"));
            _titleField = Field(null, LobbyRules.TitleMaxLength);
            _titleField.style.flexGrow = 1;
            r1.Add(_titleField);
            m.Add(r1);

            var r2 = Box("mn-form-row");
            r2.Add(L("최대 인원", "mn-form-label"));
            _minusBtn = Btn("-", () => ChangeMaxPlayers(-1), null, null, false, "mn-cycler-btn");
            _maxPlayersLabel = L("4", "mn-cycler-value", FontKind.Heavy);
            _maxPlayersLabel.style.flexGrow = 0;
            _maxPlayersLabel.style.width = 120;
            _plusBtn = Btn("+", () => ChangeMaxPlayers(+1), null, null, false, "mn-cycler-btn");
            r2.Add(_minusBtn);
            r2.Add(_maxPlayersLabel);
            r2.Add(_plusBtn);
            m.Add(r2);

            var r3 = Box("mn-form-row");
            r3.Add(L("공개 설정", "mn-form-label"));
            _publicBtn = Btn("공개", () => { _createPrivate = false; RefreshPasswordFieldState(); });
            _privateBtn = Btn("비공개", () => { _createPrivate = true; RefreshPasswordFieldState(); }, null, "lock");
            _publicBtn.style.width = 170;
            _privateBtn.style.width = 190;
            _privateBtn.style.marginLeft = 10;
            r3.Add(_publicBtn);
            r3.Add(_privateBtn);
            m.Add(r3);

            _createPwRow = Box("mn-form-row");
            _createPwRow.Add(L("비밀번호", "mn-form-label"));
            _createPwField = Field(null, LobbyRules.PasswordMaxLength);
            _createPwField.style.flexGrow = 1;
            _createPwRow.Add(_createPwField);
            m.Add(_createPwRow);
            _createPwHint = L("방에 들어가면 비밀번호와 방 코드를 복사해서 친구에게 보낼 수 있어요.", "mn-hint");
            _createPwHint.style.marginLeft = 150;
            m.Add(_createPwHint);

            var b = Box("mn-modal-buttons");
            b.Add(Btn("취소", () => Show(_createDim, false)));
            b.Add(Btn("만들기", OnCreateConfirmClicked, "primary"));
            m.Add(b);
        }

        private void OpenCreateRoomPanel()
        {
            if (_launcher == null || _launcher.State != LauncherState.InLobby || _launcher.HasReconnect) return;
            _titleField.SetValueWithoutNotify(LobbyRules.DefaultRoomTitle(PlayerProfile.Nickname));
            _createMaxPlayers = LobbyRules.DefaultMaxPlayers;
            _createPrivate = LobbyRules.DefaultPrivate;
            _createPwField.SetValueWithoutNotify(GeneratePassword());
            RefreshMaxPlayers();
            RefreshPasswordFieldState();
            Show(_createDim, true);
            _createDim.BringToFront();
        }

        private static string GeneratePassword()
        {
            var chars = new char[LobbyRules.GeneratedPasswordLength];
            for (int i = 0; i < chars.Length; i++) chars[i] = (char)('0' + UnityEngine.Random.Range(0, 10));
            return new string(chars);
        }

        private void ChangeMaxPlayers(int delta)
        {
            _createMaxPlayers = Mathf.Clamp(_createMaxPlayers + delta, LobbyRules.MinPlayers, LobbyRules.MaxPlayersLimit);
            RefreshMaxPlayers();
        }

        private void RefreshMaxPlayers()
        {
            _maxPlayersLabel.text = _createMaxPlayers + "명";
            _minusBtn.SetEnabled(_createMaxPlayers > LobbyRules.MinPlayers);
            _plusBtn.SetEnabled(_createMaxPlayers < LobbyRules.MaxPlayersLimit);
        }

        private void RefreshPasswordFieldState()
        {
            _publicBtn.EnableInClassList("mn-btn--on", !_createPrivate);
            _privateBtn.EnableInClassList("mn-btn--on", _createPrivate);
            _createPwField.SetEnabled(_createPrivate);
            _createPwRow.style.opacity = _createPrivate ? 1f : 0.4f;
            Show(_createPwHint, _createPrivate);
            if (_createPrivate && string.IsNullOrWhiteSpace(_createPwField.value))
                _createPwField.SetValueWithoutNotify(GeneratePassword());
        }

        private void OnCreateConfirmClicked()
        {
            string title = (_titleField.value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(title)) title = LobbyRules.DefaultRoomTitle(PlayerProfile.Nickname);
            if (title.Length > LobbyRules.TitleMaxLength) title = title.Substring(0, LobbyRules.TitleMaxLength);

            string password = null;
            if (_createPrivate)
            {
                password = (_createPwField.value ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(password))
                {
                    SystemUI.Alert("방 만들기", "비공개 방은 비밀번호를 입력해야 합니다.");
                    return;
                }
            }
            Show(_createDim, false);
            _launcher.CreateSession(title, _createMaxPlayers, _createPrivate, password);
        }

        #endregion
    }
}
