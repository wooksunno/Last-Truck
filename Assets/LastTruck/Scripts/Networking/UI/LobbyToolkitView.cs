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
            return b;
        }

        private static void SetText(Button b, string text)
        {
            Label l = b.Q<Label>(className: "mn-btn-text");
            if (l != null) l.text = text;
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
    }
}
