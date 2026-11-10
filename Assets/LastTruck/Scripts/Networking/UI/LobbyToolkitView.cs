using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace LastTruck.Networking
{
    /// <summary>
    /// Menu 씬의 로비 + 캐릭터 선택 화면 (UI Toolkit, Kenney 스킨).
    /// 예전 uGUI 화면(LobbyUIController / CharacterSelectUI)과 같은 일을 한다: 네트워크 처리는 전부 GameLauncher에 맡기고
    /// 여기서는 화면 전환 / 표시 / 버튼 → GameLauncher 호출만 다룬다.
    ///  - 방 목록: 닉네임, 방 만들기, 방 목록(입장 가능한 방 먼저)
    ///  - 방 만들기 / 비밀번호 입력: 모달
    ///  - 대기실: 내 캐릭터(초상화 + 캐릭터 변경), 참가자 목록, 준비/시작/나가기
    ///  - 캐릭터 선택: 전체 화면 (큰 초상화 + 설명 + 능력치 막대, 아래쪽에 모든 캐릭터)
    /// 로딩 패널/알림 팝업은 그대로 SystemUI(uGUI, 이 화면 위에 그려짐)가 담당한다.
    /// LobbyUIController.Awake가 만들고, 만들어지면 예전 uGUI 캔버스를 끈다. 스킨 에셋이 없으면 예전 화면을 그대로 쓴다.
    /// </summary>
    public class LobbyToolkitView : MonoBehaviour
    {
        private const string PanelSettingsPath = "UI/LastTruckHudPanelSettings";
        private const string UssPath = "UI/LastTruckLobby";
        private const float RoomRefreshInterval = 0.5f;

        // 트럭 창(TruckStorageView)과 같은 색: 바깥 판 / 안쪽 판 / 줄 / 버튼
        private static readonly Color FrameTint = new Color(0.62f, 0.51f, 0.45f, 1f);
        private static readonly Color InnerTint = new Color(0.40f, 0.32f, 0.28f, 1f);
        private static readonly Color RowTint = new Color(0.85f, 0.74f, 0.62f, 1f);
        private static readonly Color BrownButtonTint = new Color(0.86f, 0.70f, 0.52f, 1f);
        private static readonly Color RedButtonTint = new Color(0.95f, 0.85f, 0.85f, 1f);
        private static readonly Color GreyButtonTint = new Color(0.82f, 0.74f, 0.68f, 1f);   // 회색 버튼의 푸른 기를 빼서 갈색 판에 맞춘다

        public static LobbyToolkitView Instance { get; private set; }

        private static FontAsset _sdfFont;
        private StyleFontDefinition? _font;
        private LastTruckUiSkin _skin;
        private GameLauncher _launcher;

        private VisualElement _root, _listScreen, _roomScreen, _createModal, _pwModal, _charOverlay;

        // 방 목록
        private TextField _nickField;
        private Button _openCreateBtn;
        private Label _sessionCount;
        private ScrollView _sessionScroll;
        private Label _sessionEmpty;
        private readonly List<SessionRowUi> _sessionRows = new List<SessionRowUi>();

        // 방 만들기
        private TextField _titleField, _createPwField;
        private Label _maxPlayersLabel, _privateLabel, _createPwHint;
        private Button _minusBtn, _plusBtn, _privateBtn;
        private VisualElement _createPwRow;
        private int _createMaxPlayers = LobbyRules.DefaultMaxPlayers;
        private bool _createPrivate = LobbyRules.DefaultPrivate;

        // 비밀번호
        private Label _pwRoomTitle;
        private TextField _joinPwField;
        private SessionInfo _pendingPasswordSession;

        // 대기실
        private Label _roomTitle, _roomInfo, _roomPassword, _roomStatus, _myCharName;
        private VisualElement _myPortrait, _playerList;
        private Button _readyBtn, _startBtn, _leaveBtn, _changeCharBtn;
        private Label _readyLabel;
        private readonly List<PlayerRowUi> _playerRows = new List<PlayerRowUi>();
        private float _refreshTimer;

        // 캐릭터 선택
        private VisualElement _csPortrait, _csSlotRow;
        private Label _csName, _csDesc;
        private Button _csConfirm;
        private readonly List<VisualElement> _csFills = new List<VisualElement>();
        private readonly List<Label> _csValues = new List<Label>();
        private readonly List<SlotUi> _csSlots = new List<SlotUi>();
        private bool _csSlotsBuilt;
        private int _previewIndex;
        private int _requestedIndex = -1;

        private class SessionRowUi { public Button root; public VisualElement lockIcon; public Label title, status, count; public SessionInfo session; }
        private class PlayerRowUi { public VisualElement root, badge, portrait; public Label name, character, state; }
        private class SlotUi { public Button root; public int index; }

        public bool IsCharacterSelectOpen => _charOverlay != null && _charOverlay.style.display == DisplayStyle.Flex;

        #region 생성

        public static LobbyToolkitView TryCreate()
        {
            LastTruckUiSkin skin = LastTruckUiSkin.Get();
            PanelSettings settings = Resources.Load<PanelSettings>(PanelSettingsPath);
            StyleSheet uss = Resources.Load<StyleSheet>(UssPath);
            if (skin == null || settings == null || uss == null || skin.panelBrown == null || skin.buttonBrown == null)
            {
                Debug.LogWarning("[LobbyToolkitView] UI 스킨 에셋이 없어 예전 로비 화면을 그대로 씁니다.");
                return null;
            }

            var go = new GameObject("UIDocument_Lobby");
            var view = go.AddComponent<LobbyToolkitView>();
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            doc.sortingOrder = 0;
            view._skin = skin;
            view.Build(doc.rootVisualElement, uss);
            Instance = view;
            return view;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_launcher != null)
            {
                _launcher.StateChanged -= OnLauncherStateChanged;
                _launcher.SessionListUpdated -= OnSessionListUpdated;
            }
            LobbyPlayerEntry.Changed -= OnEntriesChanged;
        }

        private void Start()
        {
            _launcher = GameLauncher.Instance;
            if (_launcher == null)
            {
                Debug.LogError("[LobbyToolkitView] 씬에 GameLauncher가 없습니다.");
                return;
            }

            _nickField.SetValueWithoutNotify(PlayerProfile.Nickname);
            _launcher.StateChanged += OnLauncherStateChanged;
            _launcher.SessionListUpdated += OnSessionListUpdated;
            LobbyPlayerEntry.Changed += OnEntriesChanged;

            OnLauncherStateChanged(_launcher.State);
            OnSessionListUpdated(_launcher.Sessions);
        }

        private void Update()
        {
            if (_launcher == null) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsCharacterSelectOpen) CloseCharacterSelect();
                else if (IsShown(_pwModal)) ClosePasswordPanel();
                else if (IsShown(_createModal)) Show(_createModal, false);
            }

            if (!IsShown(_roomScreen)) return;
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer < RoomRefreshInterval) return;
            _refreshTimer = 0f;
            RefreshRoom();
        }

        #endregion

        #region 화면 만들기

        private void Build(VisualElement root, StyleSheet uss)
        {
            _root = root;
            root.styleSheets.Add(uss);
            root.AddToClassList("lt-root");

            if (_sdfFont == null && _skin.font != null)
                _sdfFont = FontAsset.CreateFontAsset(_skin.font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (_sdfFont != null) _font = new StyleFontDefinition(FontDefinition.FromSDFFont(_sdfFont));
            else if (_skin.font != null) _font = new StyleFontDefinition(_skin.font);
            if (_font.HasValue) root.style.unityFontDefinition = _font.Value;

            // 배경 + 제목
            var bg = Box("lb-bg");
            bg.Add(Box("lb-bg-glow"));
            bg.Add(Box("lb-bg-ground"));
            root.Add(bg);
            var titleBox = Box("lb-title-box");
            titleBox.Add(L("LAST TRUCK", "lb-title"));
            titleBox.Add(L("함께 트럭을 지키며 살아남으세요", "lb-subtitle"));
            root.Add(titleBox);

            BuildListScreen();
            BuildRoomScreen();
            BuildCreateModal();
            BuildPasswordModal();
            BuildCharacterSelect();
        }

        private void BuildListScreen()
        {
            _listScreen = Box("lb-screen");
            _root.Add(_listScreen);
            var panel = Panel("lb-panel", _skin.panelBrownDark, 0.5f, FrameTint);
            _listScreen.Add(panel);

            var head = Row("lb-head");
            head.Add(L("방 목록", "lb-h1"));
            head.Add(Grow());
            head.Add(L("닉네임", "lb-h2"));
            _nickField = Field("lb-nick", LobbyRules.NicknameMaxLength);
            _nickField.isDelayed = true;   // Enter / 포커스 해제 때만 값이 바뀐다
            _nickField.RegisterValueChangedCallback(e => OnNicknameEdited(e.newValue));
            head.Add(_nickField);
            _openCreateBtn = Btn("방 만들기", OpenCreateRoomPanel, "lb-btn--big", "plus");
            head.Add(_openCreateBtn);
            panel.Add(head);

            _sessionCount = L("방 0개", "lb-small");
            _sessionCount.style.marginBottom = 8;
            panel.Add(_sessionCount);

            var list = Panel("lb-list", _skin.panelBrownDark, 0.4f, InnerTint);
            _sessionScroll = new ScrollView(ScrollViewMode.Vertical);
            _sessionScroll.AddToClassList("lb-scroll");
            list.Add(_sessionScroll);
            _sessionEmpty = L("아직 열린 방이 없어요.\n[방 만들기]로 첫 방을 열어 보세요!", "lb-empty");
            list.Add(_sessionEmpty);
            panel.Add(list);
        }

        private void BuildRoomScreen()
        {
            _roomScreen = Box("lb-screen");
            _root.Add(_roomScreen);
            var panel = Panel("lb-panel", _skin.panelBrownDark, 0.5f, FrameTint);
            panel.AddToClassList("lb-panel--room");
            _roomScreen.Add(panel);

            // 왼쪽: 내 캐릭터
            var left = Panel("lb-room-left", _skin.panelBrownDark, 0.4f, InnerTint);
            left.Add(L("내 캐릭터", "lb-h2"));
            _myPortrait = Box("lb-my-portrait");
            left.Add(_myPortrait);
            _myCharName = L("", "lb-my-name");
            left.Add(_myCharName);
            _changeCharBtn = Btn("캐릭터 변경", OpenCharacterSelect, null, "upgrade");
            left.Add(_changeCharBtn);
            panel.Add(left);

            // 오른쪽: 방 정보 + 참가자
            var right = Box("lb-room-right");
            _roomTitle = L("", "lb-h1");
            right.Add(_roomTitle);
            _roomInfo = L("", "lb-small");
            right.Add(_roomInfo);
            _roomPassword = L("", "lb-password");
            right.Add(_roomPassword);

            _playerList = Panel("lb-plist", _skin.panelBrownDark, 0.4f, InnerTint);
            right.Add(_playerList);

            _roomStatus = L("", "lb-status");
            right.Add(_roomStatus);

            var buttons = Row("lb-room-buttons");
            _readyBtn = Btn("준비", OnReadyClicked, "lb-btn--big", "check");
            _readyLabel = _readyBtn.Q<Label>();
            _startBtn = Btn("게임 시작", () => { if (_launcher != null) _launcher.StartGameAsHost(); }, "lb-btn--big", "truck");
            _leaveBtn = Btn("나가기", () => { if (_launcher != null) _launcher.RequestLeaveSession(); }, "lb-btn--big", "close", _skin.buttonRed);
            buttons.Add(_readyBtn);
            buttons.Add(_startBtn);
            buttons.Add(_leaveBtn);
            right.Add(buttons);
            panel.Add(right);
        }

        private void BuildCreateModal()
        {
            _createModal = Box("lb-dim");
            _root.Add(_createModal);
            var m = Panel("lb-modal", _skin.panelBrownDark, 0.5f, FrameTint);
            _createModal.Add(m);
            m.Add(L("방 만들기", "lb-modal-title"));

            var r1 = Row("lb-form-row");
            r1.Add(L("방 제목", "lb-form-label"));
            _titleField = Field(null, LobbyRules.TitleMaxLength);
            _titleField.style.flexGrow = 1;
            r1.Add(_titleField);
            m.Add(r1);

            var r2 = Row("lb-form-row");
            r2.Add(L("최대 인원", "lb-form-label"));
            _minusBtn = Btn("-", () => ChangeMaxPlayers(-1), "lb-btn--small");
            _maxPlayersLabel = L("4", "lb-form-value");
            _plusBtn = Btn("+", () => ChangeMaxPlayers(+1), "lb-btn--small");
            r2.Add(_minusBtn);
            r2.Add(_maxPlayersLabel);
            r2.Add(_plusBtn);
            m.Add(r2);

            var r3 = Row("lb-form-row");
            r3.Add(L("공개 설정", "lb-form-label"));
            _privateBtn = Btn("비공개", () => { _createPrivate = !_createPrivate; RefreshPasswordFieldState(); }, null, "lock");
            _privateLabel = _privateBtn.Q<Label>();
            r3.Add(_privateBtn);
            m.Add(r3);

            _createPwRow = Row("lb-form-row");
            _createPwRow.Add(L("비밀번호", "lb-form-label"));
            _createPwField = Field(null, LobbyRules.PasswordMaxLength);
            _createPwField.style.flexGrow = 1;
            _createPwRow.Add(_createPwField);
            m.Add(_createPwRow);
            _createPwHint = L("함께할 친구에게 이 비밀번호를 알려 주세요.", "lb-hint");
            m.Add(_createPwHint);

            var b = Row("lb-modal-buttons");
            b.Add(Btn("만들기", OnCreateConfirmClicked, "lb-btn--big", "check"));
            b.Add(Btn("취소", () => Show(_createModal, false), "lb-btn--big", null, _skin.buttonGrey));
            m.Add(b);
            Show(_createModal, false);
        }

        private void BuildPasswordModal()
        {
            _pwModal = Box("lb-dim");
            _root.Add(_pwModal);
            var m = Panel("lb-modal", _skin.panelBrownDark, 0.5f, FrameTint);
            _pwModal.Add(m);
            m.Add(L("비밀번호 입력", "lb-modal-title"));
            _pwRoomTitle = L("", "lb-modal-msg");
            m.Add(_pwRoomTitle);
            _joinPwField = Field(null, LobbyRules.PasswordMaxLength);
            _joinPwField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) OnPasswordConfirmClicked();
            }, TrickleDown.TrickleDown);
            m.Add(_joinPwField);
            var b = Row("lb-modal-buttons");
            b.Add(Btn("참가", OnPasswordConfirmClicked, "lb-btn--big", "check"));
            b.Add(Btn("취소", ClosePasswordPanel, "lb-btn--big", null, _skin.buttonGrey));
            m.Add(b);
            Show(_pwModal, false);
        }

        private void BuildCharacterSelect()
        {
            _charOverlay = Box("cs-root");
            _root.Add(_charOverlay);

            var head = Row("cs-head");
            head.Add(L("캐릭터 선택", "cs-title"));
            head.Add(Btn("닫기", CloseCharacterSelect, null, "close", _skin.buttonGrey));
            _charOverlay.Add(head);

            var body = Row("cs-body");
            var preview = Panel("cs-preview", _skin.panelBrownDark, 0.45f, InnerTint);
            _csPortrait = Box("cs-portrait");
            preview.Add(_csPortrait);
            body.Add(preview);

            var info = Panel("cs-info", _skin.panelBrownDark, 0.5f, FrameTint);
            _csName = L("", "cs-name");
            _csDesc = L("", "cs-desc");
            info.Add(_csName);
            info.Add(_csDesc);
            foreach (string stat in CharacterSelectUI.StatLabels)
            {
                var row = Row("cs-stat");
                row.Add(L(stat, "cs-stat-label"));
                var bar = Box("cs-stat-bar");
                var fill = Box("cs-stat-fill");
                bar.Add(fill);
                row.Add(bar);
                var value = L("-", "cs-stat-value");
                row.Add(value);
                info.Add(row);
                _csFills.Add(fill);
                _csValues.Add(value);
            }
            body.Add(info);
            _charOverlay.Add(body);

            var bottom = Row("cs-bottom");
            var slotsPanel = Panel("cs-slots", _skin.panelBrownDark, 0.4f, InnerTint);
            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.style.flexGrow = 1;
            _csSlotRow = Row("cs-slot-row");
            scroll.Add(_csSlotRow);
            slotsPanel.Add(scroll);
            bottom.Add(slotsPanel);
            _csConfirm = Btn("선택", ConfirmCharacter, "lb-btn--big", "check");
            _csConfirm.AddToClassList("cs-confirm");
            bottom.Add(_csConfirm);
            _charOverlay.Add(bottom);

            Show(_charOverlay, false);
        }

        #endregion

        #region 작은 도우미

        private static VisualElement Box(string cls)
        {
            var e = new VisualElement();
            if (!string.IsNullOrEmpty(cls)) e.AddToClassList(cls);
            return e;
        }

        private static VisualElement Row(string cls)
        {
            var e = Box(cls);
            e.AddToClassList("lb-row");
            return e;
        }

        private static VisualElement Grow() => Box("lb-grow");

        private VisualElement Panel(string cls, Sprite sprite, float scale, Color? tint = null)
        {
            var e = Box(cls);
            UiToolkitSkin.Slice(e, sprite, scale, tint);
            return e;
        }

        private Label L(string text, string cls)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("lt-text");
            if (!string.IsNullOrEmpty(cls)) l.AddToClassList(cls);
            if (_font.HasValue) l.style.unityFontDefinition = _font.Value;
            return l;
        }

        private TextField Field(string cls, int maxLength)
        {
            var f = new TextField { maxLength = maxLength };
            f.AddToClassList("lb-field");
            if (!string.IsNullOrEmpty(cls)) f.AddToClassList(cls);
            if (_font.HasValue) f.style.unityFontDefinition = _font.Value;
            return f;
        }

        private Button Btn(string text, Action onClick, string cls = null, string icon = null, Sprite sprite = null)
        {
            var b = new Button(() => onClick?.Invoke()) { text = "" };
            b.AddToClassList("lb-btn");
            if (!string.IsNullOrEmpty(cls)) b.AddToClassList(cls);
            b.userData = sprite != null ? sprite : _skin.buttonBrown;
            UiToolkitSkin.Slice(b, (Sprite)b.userData, 0.5f, ButtonTint((Sprite)b.userData));
            if (!string.IsNullOrEmpty(icon))
            {
                Sprite s = UiIcons.Get(icon);
                if (s != null)
                {
                    var i = Box("lb-btn-icon");
                    i.pickingMode = PickingMode.Ignore;
                    i.style.backgroundImage = new StyleBackground(s);
                    b.Add(i);
                }
            }
            b.Add(L(text, "lb-btn-text"));
            return b;
        }

        /// <summary>버튼 켜고 끄기: 꺼지면 회색 판 + 반투명 + 클릭 막힘.</summary>
        private void SetInteractable(Button b, bool on)
        {
            b.SetEnabled(on);
            b.EnableInClassList("lb-btn--off", !on);
            Sprite normal = b.userData as Sprite ?? _skin.buttonBrown;
            Sprite s = on ? normal : (_skin.buttonGrey != null ? _skin.buttonGrey : normal);
            UiToolkitSkin.Slice(b, s, 0.5f, ButtonTint(s));
        }

        private Color ButtonTint(Sprite s)
        {
            if (s == _skin.buttonBrown) return BrownButtonTint;
            if (s == _skin.buttonRed) return RedButtonTint;
            if (s == _skin.buttonGrey) return GreyButtonTint;
            return Color.white;
        }

        private static void Show(VisualElement e, bool on)
        {
            if (e != null) e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static bool IsShown(VisualElement e) => e != null && e.style.display != DisplayStyle.None;

        private static void SetPortrait(VisualElement e, Sprite s)
        {
            e.style.backgroundImage = s != null ? new StyleBackground(s) : new StyleBackground(StyleKeyword.None);
        }

        #endregion

        #region 상태에 따른 화면 전환

        private void OnLauncherStateChanged(LauncherState state)
        {
            bool inRoom = state == LauncherState.InRoom || state == LauncherState.StartingGame;
            bool inList = state == LauncherState.Offline || state == LauncherState.ConnectingLobby
                          || state == LauncherState.InLobby || state == LauncherState.CreatingSession
                          || state == LauncherState.JoiningSession;

            // 캐릭터는 대기실에서만 바꿀 수 있다.
            if (state != LauncherState.InRoom && IsCharacterSelectOpen) CloseCharacterSelect();
            if (_changeCharBtn != null) SetInteractable(_changeCharBtn, state == LauncherState.InRoom);

            // Leaving / InGame 동안에는 현재 화면을 그대로 둔다 (로딩 패널이 위를 덮고 있음).
            if (!inRoom && !inList) return;

            Show(_roomScreen, inRoom);
            Show(_listScreen, inList);

            if (state != LauncherState.InLobby)
            {
                Show(_createModal, false);
                ClosePasswordPanel();
            }

            SetInteractable(_openCreateBtn, state == LauncherState.InLobby);
            if (inRoom)
            {
                RefreshRoom();
                RefreshMyCharacter();
            }
        }

        private void OnEntriesChanged()
        {
            RefreshRoom();
            RefreshMyCharacter();
        }

        #endregion

        #region 방 목록

        private void OnNicknameEdited(string value)
        {
            if (PlayerProfile.Clean(value) != PlayerProfile.Nickname && !PlayerProfile.TrySetNickname(value, out string error))
                SystemUI.Alert("닉네임", error);
            _nickField.SetValueWithoutNotify(PlayerProfile.Nickname);
        }

        private void OnSessionListUpdated(IReadOnlyList<SessionInfo> sessions)
        {
            List<SessionInfo> sorted = (sessions ?? (IReadOnlyList<SessionInfo>)Array.Empty<SessionInfo>())
                .OrderByDescending(GameLauncher.IsJoinable)
                .ThenBy(GameLauncher.ReadTitle)
                .ToList();

            // 줄을 재사용해서 깜빡임과 스크롤 초기화를 막는다.
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i >= _sessionRows.Count) _sessionRows.Add(MakeSessionRow());
                BindSessionRow(_sessionRows[i], sorted[i]);
                Show(_sessionRows[i].root, true);
            }
            for (int i = sorted.Count; i < _sessionRows.Count; i++) Show(_sessionRows[i].root, false);

            Show(_sessionEmpty, sorted.Count == 0);
            Show(_sessionScroll, sorted.Count > 0);
            int joinable = sorted.Count(GameLauncher.IsJoinable);
            _sessionCount.text = $"방 {sorted.Count}개 · 입장 가능 {joinable}개";
        }

        private SessionRowUi MakeSessionRow()
        {
            var r = new SessionRowUi();
            r.root = new Button(() => { if (r.session != null) OnSessionRowClicked(r.session); }) { text = "" };
            r.root.AddToClassList("lb-srow");
            UiToolkitSkin.Slice(r.root, _skin.panelBrown, 0.4f, RowTint);
            r.lockIcon = Box("lb-lock");
            r.lockIcon.pickingMode = PickingMode.Ignore;
            Sprite lockSprite = UiIcons.Get("lock");
            if (lockSprite != null) r.lockIcon.style.backgroundImage = new StyleBackground(lockSprite);
            r.title = L("", "lb-srow-title");
            r.title.AddToClassList("lt-dark");
            r.status = L("", "lb-srow-status");
            r.count = L("", "lb-srow-count");
            r.count.AddToClassList("lt-dark");
            r.root.Add(r.lockIcon);
            r.root.Add(r.title);
            r.root.Add(r.status);
            r.root.Add(r.count);
            _sessionScroll.Add(r.root);
            return r;
        }

        private void BindSessionRow(SessionRowUi r, SessionInfo session)
        {
            r.session = session;
            bool joinable = GameLauncher.IsJoinable(session);
            r.title.text = LobbyRules.SafeText(GameLauncher.ReadTitle(session));
            r.count.text = $"{session.PlayerCount} / {session.MaxPlayers}";
            r.lockIcon.style.visibility = GameLauncher.HasPassword(session) ? Visibility.Visible : Visibility.Hidden;

            if (!session.IsOpen) { r.status.text = "게임 중"; r.status.style.color = new Color(0.45f, 0.42f, 0.40f); }
            else if (GameLauncher.IsFull(session)) { r.status.text = "인원 마감"; r.status.style.color = new Color(0.45f, 0.42f, 0.40f); }
            else { r.status.text = "모집 중"; r.status.style.color = new Color(0.20f, 0.55f, 0.20f); }
            r.status.style.unityTextOutlineWidth = 0;

            r.root.EnableInClassList("lb-srow--off", !joinable);
        }

        private void OnSessionRowClicked(SessionInfo session)
        {
            if (_launcher == null || _launcher.State != LauncherState.InLobby) return;

            if (!GameLauncher.IsJoinable(session))
            {
                SystemUI.Alert("방 참가", "방이 가득 찼거나 이미 게임이 시작되었습니다.");
                return;
            }

            if (GameLauncher.HasPassword(session))
            {
                _pendingPasswordSession = session;
                _pwRoomTitle.text = LobbyRules.SafeText(GameLauncher.ReadTitle(session));
                _joinPwField.SetValueWithoutNotify(string.Empty);
                Show(_pwModal, true);
                _joinPwField.schedule.Execute(() => _joinPwField.Focus());
            }
            else
            {
                _launcher.JoinSession(session, null);
            }
        }

        #endregion

        #region 비밀번호 입력

        private void OnPasswordConfirmClicked()
        {
            if (!IsShown(_pwModal)) return;
            string password = (_joinPwField.value ?? "").Trim();
            if (string.IsNullOrEmpty(password))
            {
                SystemUI.Alert("비밀번호", "비밀번호를 입력해 주세요.");
                return;
            }

            SessionInfo session = _pendingPasswordSession;
            ClosePasswordPanel();
            if (session != null) _launcher.JoinSession(session, password);
        }

        private void ClosePasswordPanel()
        {
            _pendingPasswordSession = null;
            Show(_pwModal, false);
        }

        #endregion

        #region 방 만들기

        private void OpenCreateRoomPanel()
        {
            if (_launcher == null || _launcher.State != LauncherState.InLobby) return;

            _titleField.SetValueWithoutNotify(LobbyRules.DefaultRoomTitle(PlayerProfile.Nickname));
            _createMaxPlayers = LobbyRules.DefaultMaxPlayers;
            _createPrivate = LobbyRules.DefaultPrivate;
            _createPwField.SetValueWithoutNotify(GeneratePassword());

            RefreshMaxPlayers();
            RefreshPasswordFieldState();
            Show(_createModal, true);
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
            _maxPlayersLabel.text = _createMaxPlayers.ToString();
            SetInteractable(_minusBtn, _createMaxPlayers > LobbyRules.MinPlayers);
            SetInteractable(_plusBtn, _createMaxPlayers < LobbyRules.MaxPlayersLimit);
        }

        private void RefreshPasswordFieldState()
        {
            _privateLabel.text = _createPrivate ? "비공개" : "공개";
            _createPwField.SetEnabled(_createPrivate);
            _createPwRow.EnableInClassList("lb-field--off", !_createPrivate);
            Show(_createPwHint, _createPrivate);
            if (_createPrivate && string.IsNullOrWhiteSpace(_createPwField.value))
                _createPwField.SetValueWithoutNotify(GeneratePassword());
        }

        private void OnCreateConfirmClicked()
        {
            string title = (_titleField.value ?? "").Trim();
            if (string.IsNullOrEmpty(title)) title = LobbyRules.DefaultRoomTitle(PlayerProfile.Nickname);
            if (title.Length > LobbyRules.TitleMaxLength) title = title.Substring(0, LobbyRules.TitleMaxLength);

            string password = null;
            if (_createPrivate)
            {
                password = (_createPwField.value ?? "").Trim();
                if (string.IsNullOrEmpty(password))
                {
                    SystemUI.Alert("방 만들기", "비공개 방은 비밀번호를 입력해야 합니다.");
                    return;
                }
            }

            Show(_createModal, false);
            _launcher.CreateSession(title, _createMaxPlayers, _createPrivate, password);
        }

        #endregion

        #region 대기실

        private void OnReadyClicked()
        {
            LobbyPlayerEntry local = LobbyPlayerEntry.Local;
            if (local == null || local.IsHost) return;
            _launcher.SetLocalReady(!local.IsReady);
        }

        private void RefreshRoom()
        {
            if (_launcher == null || !IsShown(_roomScreen)) return;

            bool isHost = _launcher.IsHost;
            _roomTitle.text = LobbyRules.SafeText(_launcher.RoomTitle);
            _roomInfo.text = $"{_launcher.RoomPlayerCount} / {_launcher.RoomMaxPlayers}명 · {(_launcher.RoomIsPrivate ? "비공개 방" : "공개 방")}";

            string password = _launcher.HostPassword;
            bool showPw = isHost && !string.IsNullOrEmpty(password);
            Show(_roomPassword, showPw);
            if (showPw) _roomPassword.text = $"비밀번호: {LobbyRules.SafeText(password)}  (함께할 친구에게 알려주세요)";

            List<LobbyPlayerEntry> entries = LobbyPlayerEntry.All
                .Where(e => e != null && e.IsAlive)
                .OrderByDescending(e => (bool)e.IsHost)
                .ThenBy(e => e.Owner.PlayerId)
                .ToList();

            CharacterCatalog catalog = CharacterCatalog.Instance;
            for (int i = 0; i < entries.Count; i++)
            {
                if (i >= _playerRows.Count) _playerRows.Add(MakePlayerRow());
                PlayerRowUi row = _playerRows[i];
                LobbyPlayerEntry e = entries[i];
                bool host = e.IsHost, ready = e.IsReady;
                string safeName = LobbyRules.SafeText(e.DisplayName);
                row.name.text = e.IsLocal ? $"{safeName} <color=#E8913A>(나)</color>" : safeName;
                row.badge.style.visibility = host ? Visibility.Visible : Visibility.Hidden;
                SetPortrait(row.portrait, catalog != null ? catalog.GetPortrait(e.CharacterIndex) : null);
                row.character.text = catalog != null ? LobbyRules.SafeText(catalog.GetDisplayName(e.CharacterIndex)) : "";
                row.state.text = host ? "방장" : ready ? "준비 완료" : "대기 중";
                row.state.EnableInClassList("lb-hostmark", host);
                row.state.EnableInClassList("lb-ready", !host && ready);
                row.state.EnableInClassList("lb-waiting", !host && !ready);
                Show(row.root, true);
            }
            for (int i = entries.Count; i < _playerRows.Count; i++) Show(_playerRows[i].root, false);

            bool canInteract = _launcher.State == LauncherState.InRoom;
            Show(_startBtn, isHost);
            Show(_readyBtn, !isHost);
            SetInteractable(_leaveBtn, canInteract);

            if (isHost)
            {
                bool allReady = _launcher.AreAllPlayersReady();
                SetInteractable(_startBtn, canInteract && allReady);
                int others = entries.Count(e => !e.IsHost);
                int readyOthers = entries.Count(e => !e.IsHost && e.IsReady);
                _roomStatus.text = others == 0
                    ? "혼자 시작할 수 있습니다."
                    : allReady
                        ? "모든 참가자가 준비되었습니다. 게임을 시작하세요!"
                        : $"모든 참가자가 준비하면 시작할 수 있습니다. ({readyOthers}/{others})";
            }
            else
            {
                LobbyPlayerEntry local = LobbyPlayerEntry.Local;
                bool ready = local != null && local.IsReady;
                SetInteractable(_readyBtn, canInteract && local != null);
                _readyLabel.text = ready ? "준비 취소" : "준비";
                _roomStatus.text = ready ? "방장이 게임을 시작하기를 기다리는 중..." : "준비 버튼을 눌러 주세요.";
            }
        }

        private PlayerRowUi MakePlayerRow()
        {
            var r = new PlayerRowUi();
            r.root = Row("lb-prow");
            UiToolkitSkin.Slice(r.root, _skin.panelBrown, 0.4f, RowTint);
            r.badge = Box("lb-badge");
            Sprite star = UiIcons.Get("star");
            if (star != null) r.badge.style.backgroundImage = new StyleBackground(star);
            r.badge.style.unityBackgroundImageTintColor = new Color(1f, 0.77f, 0.25f);
            r.portrait = Box("lb-prow-portrait");
            r.name = L("", "lb-prow-name");
            r.name.AddToClassList("lt-dark");
            r.character = L("", "lb-prow-char");
            r.character.AddToClassList("lt-dark");
            r.state = L("", "lb-prow-state");
            r.root.Add(r.badge);
            r.root.Add(r.portrait);
            r.root.Add(r.name);
            r.root.Add(r.character);
            r.root.Add(r.state);
            _playerList.Add(r.root);
            return r;
        }

        #endregion

        #region 캐릭터 선택

        /// <summary>지금 내 캐릭터: 호스트가 확정한 값, 단 [선택] 직후 응답 전까지는 요청한 번호.</summary>
        private int CurrentIndex
        {
            get
            {
                LobbyPlayerEntry local = LobbyPlayerEntry.Local;
                if (local == null) return PlayerProfile.CharacterIndex;
                if (_requestedIndex >= 0 && local.CharacterIndex != _requestedIndex) return _requestedIndex;
                _requestedIndex = -1;
                return local.CharacterIndex;
            }
        }

        private void RefreshMyCharacter()
        {
            CharacterCatalog catalog = CharacterCatalog.Instance;
            int index = CurrentIndex;
            SetPortrait(_myPortrait, catalog != null ? catalog.GetPortrait(index) : null);
            _myCharName.text = catalog != null ? LobbyRules.SafeText(catalog.GetDisplayName(index)) : "캐릭터 목록 없음";
        }

        public void OpenCharacterSelect()
        {
            if (_launcher != null && _launcher.State != LauncherState.InRoom) return;
            BuildSlotsIfNeeded();
            Preview(CurrentIndex);
            Show(_charOverlay, true);
            _charOverlay.BringToFront();
        }

        public void CloseCharacterSelect() => Show(_charOverlay, false);

        private void ConfirmCharacter()
        {
            CharacterCatalog catalog = CharacterCatalog.Instance;
            if (catalog == null || catalog.Count == 0) return;
            if (_launcher != null && _launcher.State != LauncherState.InRoom) { CloseCharacterSelect(); return; }

            int index = catalog.ClampIndex(_previewIndex);
            PlayerProfile.CharacterIndex = index;
            LobbyPlayerEntry local = LobbyPlayerEntry.Local;
            if (local != null)
            {
                _requestedIndex = index;
                local.RPC_SetCharacter(index);
            }
            CloseCharacterSelect();
            RefreshMyCharacter();
        }

        private void BuildSlotsIfNeeded()
        {
            if (_csSlotsBuilt) return;
            _csSlotsBuilt = true;
            CharacterCatalog catalog = CharacterCatalog.Instance;
            int count = catalog != null ? catalog.Count : 0;
            Sprite check = UiIcons.Get("check");
            for (int i = 0; i < count; i++)
            {
                int index = i;
                CharacterStatsData data = catalog.Get(i);
                var slot = new SlotUi { index = i };
                slot.root = new Button(() => Preview(index)) { text = "" };
                slot.root.AddToClassList("cs-slot");
                UiToolkitSkin.Slice(slot.root, _skin.panelBrown, 0.4f, RowTint);
                var p = Box("cs-slot-portrait");
                p.pickingMode = PickingMode.Ignore;
                SetPortrait(p, data != null ? data.portrait : null);
                slot.root.Add(p);
                var n = L(data != null ? LobbyRules.SafeText(data.DisplayName) : "?", "cs-slot-name");
                n.AddToClassList("lt-dark");
                slot.root.Add(n);
                var c = Box("cs-slot-check");
                c.pickingMode = PickingMode.Ignore;
                if (check != null) c.style.backgroundImage = new StyleBackground(check);
                slot.root.Add(c);
                _csSlotRow.Add(slot.root);
                _csSlots.Add(slot);
            }
            if (count == 0) _csSlotRow.Add(L("캐릭터 목록이 비어 있습니다.", "lb-empty"));
        }

        private void Preview(int index)
        {
            CharacterCatalog catalog = CharacterCatalog.Instance;
            _previewIndex = catalog != null ? catalog.ClampIndex(index) : 0;
            CharacterStatsData data = catalog != null && catalog.Count > 0 ? catalog.Get(_previewIndex) : null;

            int current = CurrentIndex;
            foreach (SlotUi s in _csSlots)
            {
                s.root.EnableInClassList("cs-slot--preview", s.index == _previewIndex);
                s.root.EnableInClassList("cs-slot--equipped", s.index == current);
            }

            SetPortrait(_csPortrait, data != null ? data.portrait : null);
            _csName.text = data != null ? LobbyRules.SafeText(data.DisplayName) : "캐릭터 없음";
            _csDesc.text = data == null
                ? "캐릭터 목록이 비어 있습니다."
                : string.IsNullOrWhiteSpace(data.description)
                    ? "<color=#9AA3B2>(아직 설명이 없습니다)</color>"
                    : LobbyRules.SafeText(data.description);

            for (int i = 0; i < _csFills.Count; i++)
            {
                float value = data != null ? GetStat(data, i) : 0f;
                float max = 0.0001f;
                if (catalog != null)
                    foreach (CharacterStatsData other in catalog.Characters)
                        if (other != null) max = Mathf.Max(max, GetStat(other, i));
                _csFills[i].style.width = Length.Percent(Mathf.Clamp01(value / max) * 100f);
                _csValues[i].text = data != null ? value.ToString("0.##") : "-";
            }

            SetInteractable(_csConfirm, data != null);
        }

        private static float GetStat(CharacterStatsData data, int statIndex)
        {
            switch (statIndex)
            {
                case 0: return data.MaxHealth;
                case 1: return data.AttackPower;
                case 2: return data.MoveSpeed;
                case 3: return data.CostReduction;
                case 4: return data.HarvestSpeed;
                case 5: return data.RepairBonus;
                default: return 0f;
            }
        }

        #endregion
    }
}
