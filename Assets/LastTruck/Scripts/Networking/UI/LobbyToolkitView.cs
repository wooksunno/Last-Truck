using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace LastTruck.Networking
{
    /// <summary>
    /// Menu 씬 화면 전체 (UI Toolkit). 녹슨 금속 / 포스트아포칼립스 스타일.
    ///  - 타이틀: 로고 + [플레이] [설정] [제작진] [게임 종료], 황혼의 사막 배경(5겹, 살짝 흔들림 + 먼지 + 로고 빛)
    ///  - 방 목록: 플레이를 누르면 바로. 방 만들기 / 코드로 참가(6칸) / 재접속(있으면 맨 위 + 다른 방 막힘)
    ///  - 대기실: 방 이름(방장은 눌러서 바꾸기), 참가자 4칸, 방 코드/비밀번호 복사, 캐릭터 변경, 나가기 + 시작/준비
    ///  - 설정 창(소리/화면/조작키/닉네임), 제작진, 처음 실행 때 닉네임 정하기, 캐릭터 선택
    /// 네트워크 처리는 전부 GameLauncher에 맡기고 여기서는 화면 전환 / 표시 / 버튼 → GameLauncher 호출만 한다.
    /// 로딩 창 / 알림 팝업은 그대로 SystemUI(uGUI, 이 화면 위에 그려짐)가 맡는다.
    /// LobbyUIController.Awake가 만들고, 만들어지면 예전 uGUI 캔버스를 끈다. 메뉴 이미지가 없으면 예전 화면을 그대로 쓴다.
    /// 파일: 이 파일(공통/배경) + .Title / .Sessions / .Room / .Settings / .Character
    /// </summary>
    public partial class LobbyToolkitView : MonoBehaviour
    {
        #region 상수 / 상태

        private const string PanelSettingsPath = "UI/LastTruckHudPanelSettings";
        private const string UssPath = "UI/LastTruckMenu";
        private const string MenuArtPath = "UI/Menu/";
        private const string BodyFontPath = "UI/Fonts/Pretendard-Bold";
        private const string HeavyFontPath = "UI/Fonts/Pretendard-ExtraBold";
        private const string StencilFontPath = "UI/Fonts/BlackOpsOne-Regular";
        private const float RoomRefreshInterval = 0.5f;

        public static LobbyToolkitView Instance { get; private set; }

        /// <summary>한 번이라도 타이틀에서 플레이를 눌렀으면, 게임을 마치고 메뉴로 돌아왔을 때 바로 방 목록을 보여 준다.</summary>
        private static bool _passedTitle;

        private static FontAsset _bodyFont, _heavyFont, _stencilFont;

        private GameLauncher _launcher;
        private VisualElement _root;

        private enum MenuScreen { Title, List, Room }
        private MenuScreen _screen = MenuScreen.Title;
        private VisualElement _titleScreen, _listScreen, _roomScreen;

        private enum FontKind { Body, Heavy, Stencil }

        #endregion

        // 방 만들기
        private TextField _titleField, _createPwField;
        private Label _maxPlayersLabel, _privateLabel, _createPwHint;
        private Button _minusBtn, _plusBtn, _privateBtn;
        private VisualElement _createPwRow, _privateIcon;
        private int _createMaxPlayers = LobbyRules.DefaultMaxPlayers;
        private bool _createPrivate = LobbyRules.DefaultPrivate;

        #region 생성 / 생명주기

        public static LobbyToolkitView TryCreate()
        {
            PanelSettings baseSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            StyleSheet uss = Resources.Load<StyleSheet>(UssPath);
            Texture2D panel = Resources.Load<Texture2D>(MenuArtPath + "panel");
            if (baseSettings == null || uss == null || panel == null)
            {
                Debug.LogWarning("[LobbyToolkitView] 메뉴 UI 에셋(PanelSettings / LastTruckMenu.uss / UI/Menu 이미지)이 없어 예전 로비 화면을 그대로 씁니다.");
                return null;
            }

            // 기준 1920x1080을 화면 안에 다 보이게(Expand) 맞춘다. 화면이 더 길쭉하면 남는 쪽은 배경이 채운다.
            PanelSettings settings = Instantiate(baseSettings);
            settings.name = "LastTruckMenuPanelSettings";
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.Expand;
            settings.sortingOrder = 0;

            var go = new GameObject("UIDocument_Menu");
            var view = go.AddComponent<LobbyToolkitView>();
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            doc.sortingOrder = 0;
            view.Build(doc.rootVisualElement, uss);
            Instance = view;
            GameLauncher.MenuControlsLobbyConnect = true;
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
            GameSettings.CancelPreview();
        }

        private void Start()
        {
            _launcher = GameLauncher.Instance;
            if (_launcher == null)
            {
                Debug.LogError("[LobbyToolkitView] 씬에 GameLauncher가 없습니다.");
                return;
            }

            _launcher.StateChanged += OnLauncherStateChanged;
            _launcher.SessionListUpdated += OnSessionListUpdated;
            LobbyPlayerEntry.Changed += OnEntriesChanged;

            LauncherState state = _launcher.State;
            if (state == LauncherState.InRoom || state == LauncherState.StartingGame) ShowScreen(MenuScreen.Room, true);
            else if (_passedTitle || _launcher.HasReconnect) ShowScreen(MenuScreen.List, true);
            else ShowScreen(MenuScreen.Title, true);

            // 타이틀을 보는 동안 뒤에서 조용히 로비에 접속해 둔다 (플레이를 누르면 바로 방 목록).
            if (state == LauncherState.Offline) _launcher.EnterLobbyQuietly();

            OnLauncherStateChanged(_launcher.State);
            OnSessionListUpdated(_launcher.Sessions);

            if (_screen == MenuScreen.Title && !PlayerProfile.HasSavedNickname) OpenNicknamePopup();
        }

        private void Update()
        {
            AnimateBackground();
            if (_launcher == null) return;

            UpdateKeyCapture();
            UpdateToasts();

            if (Input.GetKeyDown(KeyCode.Escape) && !_captureJustEnded) HandleEscape();
            _captureJustEnded = false;

            if (_screen == MenuScreen.List) UpdateReconnectBanner();
            if (_screen == MenuScreen.Room)
            {
                _refreshTimer += Time.unscaledDeltaTime;
                if (_refreshTimer >= RoomRefreshInterval)
                {
                    _refreshTimer = 0f;
                    RefreshRoom();
                }
            }
        }

        /// <summary>ESC: 맨 위에 열린 것부터 닫는다.</summary>
        private void HandleEscape()
        {
            if (IsShown(_nickDim)) return;   // 처음 닉네임은 정해야 닫힌다
            if (IsShown(_settingsDim)) { CloseSettings(); return; }
            if (IsCharacterSelectOpen) { CloseCharacterSelect(); return; }
            if (IsShown(_creditsDim)) { Show(_creditsDim, false); return; }
            if (IsShown(_pwDim)) { ClosePasswordPanel(); return; }
            if (IsShown(_createDim)) { Show(_createDim, false); return; }
            if (_renaming) { EndRename(false); return; }
            if (_screen == MenuScreen.List && (_launcher.State == LauncherState.InLobby || _launcher.State == LauncherState.Offline))
                ShowScreen(MenuScreen.Title, false);
        }

        #endregion

        #region 화면 만들기 (공통)

        private void Build(VisualElement root, StyleSheet uss)
        {
            _root = root;
            root.styleSheets.Add(uss);

            root.AddToClassList("mn-root");
            LoadFonts();
            if (_bodyFont != null) root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(_bodyFont));

            root.AddToClassList("lt-root");
            // 원신풍 스킨이면 판이 남색, 버튼이 크림색이라 글씨 색을 반대로 맞춘다 (USS .lt-poly)
            if (_skin.polyTheme) root.AddToClassList("lt-poly");

            BuildBackground();

            _titleScreen = BuildTitleScreen();
            _listScreen = BuildListScreen();
            _roomScreen = BuildRoomScreen();
            root.Add(_titleScreen);
            root.Add(_listScreen);
            root.Add(_roomScreen);

            var version = L($"v{Application.version} · Last Truck Team", "mn-version");
            root.Add(version);

            BuildCreateModal();
            BuildPasswordModal();
            BuildCreditsModal();
            BuildCharacterSelect();
            BuildSettingsWindow();
            BuildNicknamePopup();

            Show(_titleScreen, false);
            Show(_listScreen, false);
            Show(_roomScreen, false);
        }

        private static void LoadFonts()
        {
            if (_bodyFont == null) _bodyFont = MakeFont(BodyFontPath);
            if (_heavyFont == null) _heavyFont = MakeFont(HeavyFontPath) ?? _bodyFont;
            if (_stencilFont == null)

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
            _changeCharBtn = Btn("캐릭터 변경", OpenCharacterSelect, "lb-btn--character", "user");
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
            _privateBtn = Btn("비공개", () => { _createPrivate = !_createPrivate; RefreshPasswordFieldState(); }, "lb-btn--privacy", "lock_locked");
            _privateIcon = _privateBtn.Q<VisualElement>(className: "lb-btn-icon");
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
                _stencilFont = MakeFont(StencilFontPath) ?? _heavyFont;
                // 스텐실 글꼴에는 한글이 없다 → 한글은 Pretendard로 그린다.
                if (_stencilFont != null && _stencilFont != _heavyFont && _heavyFont != null)
                    _stencilFont.fallbackFontAssetTable = new List<FontAsset> { _heavyFont };
            }
        }

        private static FontAsset MakeFont(string path)
        {
            Font font = Resources.Load<Font>(path);
            if (font == null)
            {
                Debug.LogWarning($"[LobbyToolkitView] 글꼴 '{path}'을 찾지 못했습니다.");
                return null;
            }
            return FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
        }

        private void ShowScreen(MenuScreen screen, bool instant)
        {
            if (screen != MenuScreen.Title) _passedTitle = true;
            _screen = screen;
            FadeTo(_titleScreen, screen == MenuScreen.Title, instant);
            FadeTo(_listScreen, screen == MenuScreen.List, instant);
            FadeTo(_roomScreen, screen == MenuScreen.Room, instant);
            if (screen != MenuScreen.Room && _renaming) EndRename(false);
            if (screen == MenuScreen.List) OnSessionListUpdated(_launcher != null ? _launcher.Sessions : null);
            if (screen == MenuScreen.Room) { RefreshRoom(); RefreshMyCharacter(); }
        }

        private static void FadeTo(VisualElement e, bool on, bool instant)
        {
            if (e == null) return;
            if (on)
            {
                bool wasShown = IsShown(e);
                Show(e, true);
                if (!wasShown && !instant)
                {
                    e.AddToClassList("mn-screen--hidden");
                    e.schedule.Execute(() => e.RemoveFromClassList("mn-screen--hidden")).StartingIn(16);
                }
                else e.RemoveFromClassList("mn-screen--hidden");
            }
            else Show(e, false);
        }

        #endregion

        #region 배경 (5겹 + 먼지 + 로고 빛)

        private readonly VisualElement[] _bgLayers = new VisualElement[5];
        private static readonly float[] BgSway = { 4f, 9f, 16f, 26f, 0f };   // 겹마다 흔들리는 폭(px) - 가까울수록 크게
        private VisualElement _dustLayer;
        private readonly List<Dust> _dust = new List<Dust>();
        private Vector2 _mouseParallax;

        private class Dust
        {
            public VisualElement e;
            public float x, y, speed, wobble, phase, size, alpha;
        }

        private void BuildBackground()
        {
            var bg = Box("mn-bg");
            string[] names = { "sky", "far", "mid", "ground", "fx" };
            for (int i = 0; i < names.Length; i++)
            {
                var layer = Box("mn-bg-layer");
                layer.AddToClassList("mn-bg-" + names[i]);
                layer.pickingMode = PickingMode.Ignore;
                bg.Add(layer);
                _bgLayers[i] = layer;
                if (i == 3)
                {
                    // 먼지는 땅 겹 앞, 비네트(fx) 뒤에 날린다
                    _dustLayer = Box("mn-fill");
                    _dustLayer.pickingMode = PickingMode.Ignore;
                    bg.Add(_dustLayer);
                }
            }
            bg.pickingMode = PickingMode.Ignore;
            _root.Add(bg);

            var rng = new System.Random(7);
            for (int i = 0; i < 46; i++)
            {
                var d = new Dust
                {
                    e = Box("mn-dust"),
                    x = (float)rng.NextDouble(),
                    y = 0.45f + (float)rng.NextDouble() * 0.55f,
                    speed = 0.004f + (float)rng.NextDouble() * 0.014f,
                    wobble = 4f + (float)rng.NextDouble() * 14f,
                    phase = (float)rng.NextDouble() * 6.28f,
                    size = 2f + (float)rng.NextDouble() * 4f,
                    alpha = 0.15f + (float)rng.NextDouble() * 0.45f,
                };
                d.e.pickingMode = PickingMode.Ignore;
                d.e.style.width = d.size;
                d.e.style.height = d.size;
                _dustLayer.Add(d.e);
                _dust.Add(d);
            }
        }

        private void AnimateBackground()
        {
            float t = Time.unscaledTime;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            // 마우스 위치를 따라 아주 조금 움직인다 (가까운 겹일수록 많이)
            Vector2 mouse = Input.mousePosition;
            Vector2 target = new Vector2(
                Screen.width > 0 ? Mathf.Clamp(mouse.x / Screen.width, 0f, 1f) - 0.5f : 0f,
                Screen.height > 0 ? Mathf.Clamp(mouse.y / Screen.height, 0f, 1f) - 0.5f : 0f);
            _mouseParallax = Vector2.Lerp(_mouseParallax, target, 1f - Mathf.Exp(-3f * dt));

            for (int i = 0; i < _bgLayers.Length; i++)
            {
                if (_bgLayers[i] == null || BgSway[i] <= 0f) continue;
                float amp = BgSway[i];
                float x = Mathf.Sin(t * 0.13f + i * 0.7f) * amp * 0.5f - _mouseParallax.x * amp;
                float y = Mathf.Sin(t * 0.21f + i * 1.3f) * amp * 0.18f + _mouseParallax.y * amp * 0.35f;
                _bgLayers[i].style.translate = new Translate(x, y);
            }

            float w = _dustLayer != null ? _dustLayer.resolvedStyle.width : 0f;
            float h = _dustLayer != null ? _dustLayer.resolvedStyle.height : 0f;
            if (w > 1f && h > 1f)
            {
                foreach (Dust d in _dust)
                {
                    d.x -= d.speed * dt;
                    if (d.x < -0.02f) { d.x = 1.02f; d.y = 0.45f + UnityEngine.Random.value * 0.55f; }
                    float px = d.x * w;
                    float py = d.y * h + Mathf.Sin(t * 0.9f + d.phase) * d.wobble;
                    d.e.style.left = px;
                    d.e.style.top = py;
                    d.e.style.opacity = d.alpha * (0.6f + 0.4f * Mathf.Sin(t * 1.7f + d.phase));
                }
            }

            if (_logoGlow != null) _logoGlow.style.opacity = 0.45f + 0.35f * (0.5f + 0.5f * Mathf.Sin(t * 1.6f));
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
            e.AddToClassList("mn-row");
            return e;
        }

        private static VisualElement Grow() => Box("mn-grow");
        private static VisualElement Icon(string name, string cls)
        {
            Sprite sprite = UiIcons.Get(name);
            if (sprite == null) return null;
            var icon = Box(cls);
            icon.pickingMode = PickingMode.Ignore;
            icon.style.backgroundImage = new StyleBackground(sprite);
            return icon;
        }

        private static VisualElement Grow() => Box("lb-grow");

        private static void SetFont(VisualElement e, FontKind kind)
        {
            FontAsset f = kind == FontKind.Stencil ? _stencilFont : kind == FontKind.Heavy ? _heavyFont : _bodyFont;
            if (f != null) e.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(f));
        }

        /// <summary>글자. kind: 본문(Pretendard Bold) / 굵게(ExtraBold) / 스텐실(Black Ops One).</summary>
        private static Label L(string text, string cls, FontKind kind = FontKind.Body)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("mn-text");
            if (!string.IsNullOrEmpty(cls))
                foreach (string c in cls.Split(' ')) l.AddToClassList(c);
            SetFont(l, kind);
            return l;
        }

        /// <summary>금속 판 + 네 귀퉁이 볼트.</summary>
        private static VisualElement Plate(string cls, bool dark = false)
        {
            var p = Box("mn-plate");
            if (dark) p.AddToClassList("mn-plate--dark");
            if (!string.IsNullOrEmpty(cls))
                foreach (string c in cls.Split(' ')) p.AddToClassList(c);
            foreach (string corner in new[] { "tl", "tr", "bl", "br" })
            {
                var b = Box("mn-bolt");
                b.AddToClassList("mn-bolt--" + corner);
                b.pickingMode = PickingMode.Ignore;
                p.Add(b);
            }
            return p;
        }

        /// <summary>판 위쪽 제목줄: "한글 제목  ENGLISH" (+ 오른쪽 요소).</summary>
        private static VisualElement Header(string title, string english, VisualElement right = null)
        {
            var row = Row(null);
            row.Add(L(title, "mn-h1", FontKind.Heavy));
            if (!string.IsNullOrEmpty(english)) row.Add(L(english, "mn-h1-en", FontKind.Stencil));
            row.Add(Grow());
            if (right != null) row.Add(right);
            return row;
        }

        private static VisualElement Tape() => Box("mn-tape");

        private static Texture2D Art(string name) => Resources.Load<Texture2D>(MenuArtPath + name);

        private static VisualElement Icon(string name, string cls)
        {
            var i = Box(cls);
            i.pickingMode = PickingMode.Ignore;
            Texture2D tex = Art("icon_" + name);
            if (tex != null) i.style.backgroundImage = new StyleBackground(tex);
            return i;
        }

        /// <summary>금속 버튼. variant: null / "primary" / "green" / "red". stripe = 왼쪽 해저드 줄무늬.</summary>
        private static Button Btn(string text, Action onClick, string variant = null, string icon = null, bool stripe = false, string cls = null)
        {
            // focusable = false: 키 설정 중 Space/Enter가 마지막으로 누른 버튼을 다시 누르지 않게
            var b = new Button(() => onClick?.Invoke()) { text = string.Empty, focusable = false };
            b.AddToClassList("mn-btn");
            if (!string.IsNullOrEmpty(variant)) b.AddToClassList("mn-btn--" + variant);
            if (!string.IsNullOrEmpty(cls))
                foreach (string c in cls.Split(' ')) b.AddToClassList(c);
            if (stripe)
            {
                var s = Box("mn-stripe");
                s.pickingMode = PickingMode.Ignore;
                b.Add(s);
                b.AddToClassList("mn-btn--left");
            }
            if (!string.IsNullOrEmpty(icon)) b.Add(Icon(icon, "mn-btn-icon"));
            if (!string.IsNullOrEmpty(text)) b.Add(L(text, "mn-btn-text", FontKind.Heavy));
            else b.AddToClassList("mn-btn--icononly");
            var b = new Button(() => onClick?.Invoke()) { text = "" };
            b.AddToClassList("lb-btn");
            if (!string.IsNullOrEmpty(cls)) b.AddToClassList(cls);
            b.userData = sprite != null ? sprite : _skin.buttonBrown;
            UiToolkitSkin.Slice(b, (Sprite)b.userData, 0.5f, ButtonTint((Sprite)b.userData));
            b.EnableInClassList("lb-btn--cream", (Sprite)b.userData == _skin.buttonBrown);
            var image = Icon(icon, "lb-btn-icon");
            if (image != null) b.Add(image);
            b.Add(L(text, "lb-btn-text"));
            return b;
        }

        private static void SetText(Button b, string text)
        {
            Label l = b.Q<Label>(className: "mn-btn-text");
            if (l != null) l.text = text;

            b.SetEnabled(on);
            b.EnableInClassList("lb-btn--off", !on);
            Sprite normal = b.userData as Sprite ?? _skin.buttonBrown;
            Sprite s = on ? normal : (_skin.buttonGrey != null ? _skin.buttonGrey : normal);
            UiToolkitSkin.Slice(b, s, 0.5f, ButtonTint(s));
            b.EnableInClassList("lb-btn--cream", s == _skin.buttonBrown);
        }

        private static TextField Field(string cls, int maxLength)
        {
            var f = new TextField { maxLength = maxLength };
            f.AddToClassList("mn-field");
            if (!string.IsNullOrEmpty(cls))
                foreach (string c in cls.Split(' ')) f.AddToClassList(c);
            SetFont(f, FontKind.Heavy);
            return f;
        }

        /// <summary>어두운 배경 + 가운데 창 (모달).</summary>
        private VisualElement Modal(out VisualElement window, string windowCls)
        {
            var dim = Box("mn-modal-dim");
            window = Plate(windowCls);
            dim.Add(window);
            _root.Add(dim);
            Show(dim, false);
            return dim;
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

        private static string Plain(string value) => LobbyRules.SafeText(value);

        #endregion

        #region 알림 글자 (복사했어요 등)

        private readonly List<(Label label, float until)> _toasts = new List<(Label, float)>();

        /// <summary>label을 잠깐 보여 준다 (CSS 전환으로 흐려진다).</summary>
        private void Flash(Label label, string text, float seconds = 1.6f)
        {
            if (label == null) return;
            label.text = text;
            label.AddToClassList("mn-toast--show");
            _toasts.RemoveAll(t => t.label == label);
            _toasts.Add((label, Time.unscaledTime + seconds));
        }

        private void UpdateToasts()
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < _toasts[i].until) continue;
                _toasts[i].label.RemoveFromClassList("mn-toast--show");
                _toasts.RemoveAt(i);
            }
        }

        private static void CopyToClipboard(string text)
        {
            GUIUtility.systemCopyBuffer = text ?? string.Empty;
        }

        #endregion

        #region 상태에 따른 화면 전환

        private void OnLauncherStateChanged(LauncherState state)
        {
            bool inRoom = state == LauncherState.InRoom || state == LauncherState.StartingGame;
            bool inList = state == LauncherState.Offline || state == LauncherState.ConnectingLobby
                          || state == LauncherState.InLobby || state == LauncherState.CreatingSession
                          || state == LauncherState.JoiningSession;

            if (state != LauncherState.InRoom && IsCharacterSelectOpen) CloseCharacterSelect();

            if (state != LauncherState.InLobby)
            {
                Show(_createDim, false);
                ClosePasswordPanel();
            }

            // Leaving / InGame 동안에는 화면을 그대로 둔다 (로딩 창이 위를 덮고 있음).
            if (inRoom)
            {
                CloseSettings();
                Show(_creditsDim, false);
                if (_screen != MenuScreen.Room) ShowScreen(MenuScreen.Room, false);
            }
            else if (inList && _screen == MenuScreen.Room)
            {
                ShowScreen(MenuScreen.List, false);
            }

            // 방 목록에서 접속을 기다리던 중이면 로딩 창은 GameLauncher가 닫는다.
            if (state == LauncherState.InLobby && _waitingForLobby)
            {
                _waitingForLobby = false;
                SystemUI.HideLoading();
            }
            if (state == LauncherState.Offline && _waitingForLobby)
            {
                _waitingForLobby = false;
                SystemUI.HideLoading();
            }

            RefreshListButtons();
            if (_screen == MenuScreen.Room) RefreshRoom();
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
            var occupancy = Row("lb-srow-occupancy");
            occupancy.pickingMode = PickingMode.Ignore;
            var users = Icon("users", "lb-srow-people");
            if (users != null) occupancy.Add(users);
            occupancy.Add(r.count);
            r.root.Add(r.lockIcon);
            r.root.Add(r.title);
            r.root.Add(r.status);
            r.root.Add(occupancy);
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
            if (_privateIcon != null)
                _privateIcon.style.backgroundImage = new StyleBackground(UiIcons.Get(_createPrivate ? "lock_locked" : "lock_open"));
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
                : (string.IsNullOrWhiteSpace(data.description)
                    ? "<color=#9AA3B2>(아직 설명이 없습니다)</color>"
                    : LobbyRules.SafeText(data.description))
                  + (string.IsNullOrEmpty(data.AbilitySummary) ? string.Empty : "\n\n" + data.AbilitySummary);   // 고유 능력 설명

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
