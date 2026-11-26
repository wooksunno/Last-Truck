using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// ESC 설정 메뉴 (UI Toolkit). 게임 중 ESC → 이 창 → 다시 ESC 또는 [계속하기]로 닫는다.
    ///  - 배경음 / 효과음 크기 (BgmDirector.Volume, Audio.Sfx.Volume — 각자 PlayerPrefs에 저장된다)
    ///  - 마우스 감도 (카메라 회전 입력 배율, PlayerPrefs "mouse_sens")
    ///  - 화면 모드 (전체 화면 / 창 모드), 해상도
    ///  - [게임 나가기]: 기존 나가기 확인 팝업(GameLauncher.RequestLeaveFromGame)을 띄운다
    /// 열려 있는 동안은 GameUIController.IsPopupOpen이 true가 되어 공격/채집/트럭 창 등 게임 입력이 막히고, 이동도 멈춘다.
    /// 멀티플레이에서는 게임 시간이 멈추지 않는다 (혼자 할 때도 같은 규칙: 메뉴를 열어도 밤은 다가온다).
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        private const string OverlaySettingsPath = "UI/LastTruckOverlayPanelSettings";
        private const string UssPath = "UI/LastTruckHud";
        public const string MouseSensKey = "mouse_sens";

        private static SettingsMenu _instance;

        /// <summary>설정 메뉴가 열려 있는가.</summary>
        public static bool IsOpen => _instance != null && _instance._open;

        public static void Toggle()
        {
            Ensure();
            if (_instance._open) _instance.Close(); else _instance.Open();
        }

        private static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("SettingsMenu");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SettingsMenu>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; _sens = -1f; }

        // ------------------------------------------------------------------ 마우스 감도

        private static float _sens = -1f;

        /// <summary>마우스 감도 배율 (0.3 ~ 2.5, 기본 1).</summary>
        public static float MouseSensitivity
        {
            get
            {
                if (_sens < 0f)
                {
                    try { _sens = PlayerPrefs.GetFloat(MouseSensKey, 1f); } catch { _sens = 1f; }
                }
                return _sens;
            }
            set
            {
                _sens = Mathf.Clamp(value, 0.3f, 2.5f);
                try { PlayerPrefs.SetFloat(MouseSensKey, _sens); } catch { }
                ApplyMouseSensitivity(true);
            }
        }

        private static Unity.Cinemachine.CinemachineInputAxisController _axisController;
        private static readonly Dictionary<string, float> BaseGains = new Dictionary<string, float>();
        private static float _appliedSens = -1f;

        /// <summary>카메라 회전 입력(Look Orbit X/Y)의 세기를 감도에 맞춘다. 카메라가 새로 생기면 원래 값을 기억해 두고 배율만 곱한다.</summary>
        private static void ApplyMouseSensitivity(bool force)
        {
            if (_axisController == null)
            {
                _axisController = FindFirstObjectByType<Unity.Cinemachine.CinemachineInputAxisController>(FindObjectsInactive.Include);
                if (_axisController == null) return;
                BaseGains.Clear();
                foreach (var c in _axisController.Controllers)
                    if (c != null && c.Name != null && c.Name.StartsWith("Look")) BaseGains[c.Name] = c.Input.Gain;
                force = true;
            }
            float sens = MouseSensitivity;
            if (!force && Mathf.Approximately(sens, _appliedSens)) return;
            foreach (var c in _axisController.Controllers)
                if (c != null && c.Name != null && BaseGains.TryGetValue(c.Name, out float g)) c.Input.Gain = g * sens;
            _appliedSens = sens;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // 감도는 메뉴를 한 번도 열지 않아도 적용돼야 하므로 미리 만들어 둔다 (창은 처음 열 때 만든다)
            Ensure();
        }

        // ------------------------------------------------------------------ 화면

        private bool _built, _open;
        private VisualElement _root, _panel;
        private StyleFontDefinition _font;
        private LastTruckUiSkin _skin;

        private Slider01 _bgm, _sfx, _mouse;
        private Label _modeValue, _resValue;
        private readonly List<Vector2Int> _resolutions = new List<Vector2Int>();
        private int _resIndex;
        private float _nextCameraCheck;

        private void Update()
        {
            // 카메라가 새로 만들어지면(씬 이동, 재접속) 감도를 다시 입힌다
            if (Time.unscaledTime >= _nextCameraCheck)
            {
                _nextCameraCheck = Time.unscaledTime + 1f;
                ApplyMouseSensitivity(false);
            }
        }

        private void Open()
        {
            if (!_built) Build();
            if (!_built) return;
            _open = true;
            Refresh();
            _root.style.display = DisplayStyle.Flex;
            SetPlayerUiOpen(true);
            LastTruck.Audio.Sfx.Play("ui_open", 0.6f, 1f, 0.1f);
        }

        private void Close()
        {
            if (!_open) return;
            _open = false;
            if (_root != null) _root.style.display = DisplayStyle.None;
            SetPlayerUiOpen(false);
        }

        /// <summary>내 캐릭터 이동을 멈추고 커서를 풀어 준다 (트럭 창과 같은 방식). 트럭 창이 열려 있으면 그 상태를 건드리지 않는다.</summary>
        private static void SetPlayerUiOpen(bool open)
        {
            GameObject player = CraftingSystem.GatherRules.Player;
            if (player == null) return;
            var move = player.GetComponent<PlayerMove>();
            if (move == null) return;
            if (!open && CraftingSystem.GameUIController.Instance != null && CraftingSystem.GameUIController.Instance.IsOwnPopupOpen) return;
            move.IsUIOpen = open;
        }

        private void Build()
        {
            _skin = LastTruckUiSkin.Get();
            PanelSettings settings = Resources.Load<PanelSettings>(OverlaySettingsPath);
            StyleSheet uss = Resources.Load<StyleSheet>(UssPath);
            if (settings == null) { Debug.LogWarning("[SettingsMenu] 패널 설정이 없어 설정 메뉴를 만들 수 없습니다."); return; }

            StyleFontDefinition? shared = LastTruckHud.SharedFont;
            _font = shared.HasValue ? shared.Value
                : (_skin != null && _skin.font != null ? new StyleFontDefinition(_skin.font) : new StyleFontDefinition(StyleKeyword.Null));

            var docGo = new GameObject("UIDocument_Settings");
            docGo.transform.SetParent(transform, false);
            var doc = docGo.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            doc.sortingOrder = 50;   // 같은 오버레이 패널 안에서 토스트보다 앞

            VisualElement docRoot = doc.rootVisualElement;
            if (uss != null) docRoot.styleSheets.Add(uss);
            docRoot.pickingMode = PickingMode.Ignore;
            // 화면 전체를 덮는 어두운 막: 뒤 화면 클릭을 막고, 메뉴에 눈이 가게 한다
            _root = new VisualElement { name = "SettingsDim", pickingMode = PickingMode.Position };
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.style.backgroundColor = new Color(0.02f, 0.02f, 0.04f, 0.6f);
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            docRoot.Add(_root);

            _panel = new VisualElement();
            _panel.style.width = 660;
            _panel.style.paddingLeft = 46; _panel.style.paddingRight = 46;
            _panel.style.paddingTop = 34; _panel.style.paddingBottom = 34;
            if (_skin != null && _skin.panelBrownDark != null) LastTruckHud.Skin(_panel, _skin.panelBrownDark, 0.5f, new Color(0.7f, 0.6f, 0.55f, 0.98f));
            else _panel.style.backgroundColor = new Color(0.16f, 0.11f, 0.08f, 0.97f);
            _root.Add(_panel);

            Label title = MakeLabel("설정", 40, new Color(1f, 0.8f, 0.34f), true);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 22;
            _panel.Add(title);

            _panel.Add(SectionLabel("소리"));
            _bgm = AddSliderRow("배경음", v => LastTruck.Audio.BgmDirector.Volume = v);
            _sfx = AddSliderRow("효과음", v => { LastTruck.Audio.Sfx.Volume = v; LastTruck.Audio.Sfx.Play("ui_click", 0.8f, 1f, 0.12f); });

            _panel.Add(SectionLabel("조작"));
            _mouse = AddSliderRow("마우스 감도", v => MouseSensitivity = Mathf.Lerp(0.3f, 2.5f, v), v => Mathf.Lerp(0.3f, 2.5f, v).ToString("0.0") + "배");

            _panel.Add(SectionLabel("화면"));
            _modeValue = AddSelectorRow("화면 모드", () => StepMode(), () => StepMode());
            _resValue = AddSelectorRow("해상도", () => StepResolution(-1), () => StepResolution(1));

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.Center;
            buttons.style.marginTop = 30;
            buttons.Add(MakeButton("계속하기", 220, false, Close));
            buttons.Add(MakeButton("게임 나가기", 220, true, LeaveGame));
            _panel.Add(buttons);

            Label hint = MakeLabel("ESC: 닫기 · 설정은 자동으로 저장돼요", 17, new Color(0.78f, 0.72f, 0.62f), false);
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginTop = 16;
            _panel.Add(hint);

            _root.style.display = DisplayStyle.None;
            _built = true;
        }

        private void LeaveGame()
        {
            Close();
            Networking.GameLauncher launcher = Networking.GameLauncher.Instance;
            if (launcher != null) launcher.RequestLeaveFromGame();
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        private void Refresh()
        {
            _bgm.SetValue(LastTruck.Audio.BgmDirector.Volume);
            _sfx.SetValue(LastTruck.Audio.Sfx.Volume);
            _mouse.SetValue(Mathf.InverseLerp(0.3f, 2.5f, MouseSensitivity));

            _resolutions.Clear();
            foreach (Resolution r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x >= 1024 && !_resolutions.Contains(v)) _resolutions.Add(v);
            }
            var cur = new Vector2Int(Screen.width, Screen.height);
            if (!_resolutions.Contains(cur)) _resolutions.Add(cur);
            _resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            _resIndex = _resolutions.IndexOf(cur);
            UpdateScreenLabels();
        }

        private void UpdateScreenLabels()
        {
            bool windowed = Screen.fullScreenMode == FullScreenMode.Windowed;
            _modeValue.text = windowed ? "창 모드" : "전체 화면";
            Vector2Int r = _resIndex >= 0 && _resIndex < _resolutions.Count ? _resolutions[_resIndex] : new Vector2Int(Screen.width, Screen.height);
            _resValue.text = r.x + " × " + r.y;
        }

        private void StepMode()
        {
            bool toWindowed = Screen.fullScreenMode != FullScreenMode.Windowed;
            Vector2Int r = _resIndex >= 0 && _resIndex < _resolutions.Count ? _resolutions[_resIndex] : new Vector2Int(Screen.width, Screen.height);
            if (toWindowed)
            {
                // 창 모드는 모니터보다 조금 작게 (제목 표시줄이 화면 밖으로 나가지 않게)
                Resolution native = Screen.currentResolution;
                if (r.x >= native.width || r.y >= native.height) r = new Vector2Int(Mathf.RoundToInt(native.width * 0.8f), Mathf.RoundToInt(native.height * 0.8f));
                Screen.SetResolution(r.x, r.y, FullScreenMode.Windowed);
            }
            else
            {
                Resolution native = Screen.currentResolution;
                Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
                r = new Vector2Int(native.width, native.height);
            }
            if (!_resolutions.Contains(r)) { _resolutions.Add(r); _resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y)); }
            _resIndex = _resolutions.IndexOf(r);
            _modeValue.text = toWindowed ? "창 모드" : "전체 화면";
            _resValue.text = r.x + " × " + r.y;
        }

        private void StepResolution(int dir)
        {
            if (_resolutions.Count == 0) return;
            _resIndex = Mathf.Clamp((_resIndex < 0 ? 0 : _resIndex) + dir, 0, _resolutions.Count - 1);
            Vector2Int r = _resolutions[_resIndex];
            Screen.SetResolution(r.x, r.y, Screen.fullScreenMode);
            UpdateScreenLabels();
        }

        // ------------------------------------------------------------------ 부품

        private Label MakeLabel(string text, int size, Color color, bool bold)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.unityFontDefinition = _font;
            l.style.fontSize = size;
            l.style.color = color;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.unityTextOutlineWidth = 1.0f;
            l.style.unityTextOutlineColor = new Color(0.15f, 0.09f, 0.03f, 0.95f);
            l.style.marginLeft = 0; l.style.marginRight = 0; l.style.paddingLeft = 0; l.style.paddingRight = 0;
            return l;
        }

        private VisualElement SectionLabel(string text)
        {
            Label l = MakeLabel(text, 20, new Color(1f, 0.8f, 0.34f), true);
            l.style.marginTop = 10;
            l.style.marginBottom = 4;
            l.style.borderBottomWidth = 1;
            l.style.borderBottomColor = new Color(1f, 0.8f, 0.34f, 0.25f);
            l.style.paddingBottom = 3;
            return l;
        }

        private VisualElement MakeRow(string name)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 46;
            Label l = MakeLabel(name, 22, new Color(0.97f, 0.93f, 0.82f), false);
            l.style.width = 190;
            row.Add(l);
            _panel.Add(row);
            return row;
        }

        private Slider01 AddSliderRow(string name, System.Action<float> onChange, System.Func<float, string> format = null)
        {
            VisualElement row = MakeRow(name);
            var s = new Slider01(this, onChange, format ?? (v => Mathf.RoundToInt(v * 100f) + "%"));
            row.Add(s.Element);
            return s;
        }

        private Label AddSelectorRow(string name, System.Action prev, System.Action next)
        {
            VisualElement row = MakeRow(name);
            row.Add(MakeButton("◀", 52, false, prev));
            Label value = MakeLabel("", 22, new Color(0.97f, 0.93f, 0.82f), true);
            value.style.width = 220;
            value.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(value);
            row.Add(MakeButton("▶", 52, false, next));
            return value;
        }

        private VisualElement MakeButton(string text, float width, bool red, System.Action onClick)
        {
            var b = new VisualElement();
            b.style.width = width;
            b.style.height = 46;
            b.style.marginLeft = 6; b.style.marginRight = 6;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            Sprite sprite = _skin != null ? (red ? _skin.buttonRed : _skin.buttonBrown) : null;
            Color tint = new Color(1f, 1f, 1f, 1f);
            if (sprite != null) LastTruckHud.Skin(b, sprite, 0.5f, tint);
            else b.style.backgroundColor = red ? new Color(0.6f, 0.2f, 0.15f) : new Color(0.45f, 0.3f, 0.18f);
            // 갈색 버튼은 밝은 바탕이라 글자를 진한 갈색으로 (밝은 글자는 안 읽혔다)
            Label l = MakeLabel(text, 21, red || sprite == null ? new Color(0.97f, 0.93f, 0.82f) : new Color(0.26f, 0.15f, 0.06f), true);
            if (!red && sprite != null) l.style.unityTextOutlineWidth = 0f;
            b.Add(l);
            b.RegisterCallback<PointerEnterEvent>(_ => b.style.scale = new Scale(new Vector3(1.05f, 1.05f, 1f)));
            b.RegisterCallback<PointerLeaveEvent>(_ => b.style.scale = new Scale(Vector3.one));
            b.RegisterCallback<ClickEvent>(_ =>
            {
                LastTruck.Audio.Sfx.Play("ui_click", 0.8f, 1f, 0.05f);
                onClick?.Invoke();
            });
            return b;
        }

        /// <summary>0~1 막대 슬라이더: 막대를 누르거나 끌어서 값을 바꾼다.</summary>
        private class Slider01
        {
            public readonly VisualElement Element;
            private readonly VisualElement _track, _fill, _knob;
            private readonly Label _value;
            private readonly System.Action<float> _onChange;
            private readonly System.Func<float, string> _format;
            private float _v;
            private bool _drag;

            public Slider01(SettingsMenu owner, System.Action<float> onChange, System.Func<float, string> format)
            {
                _onChange = onChange;
                _format = format;
                Element = new VisualElement();
                Element.style.flexDirection = FlexDirection.Row;
                Element.style.alignItems = Align.Center;
                Element.style.flexGrow = 1;

                _track = new VisualElement();
                _track.style.width = 290;
                _track.style.height = 34;          // 누르기 쉬운 높이 (그려지는 막대는 아래 bar)
                _track.style.justifyContent = Justify.Center;
                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.style.height = 16;
                bar.style.backgroundColor = new Color(0.08f, 0.05f, 0.03f, 0.9f);
                bar.style.borderTopLeftRadius = 8; bar.style.borderTopRightRadius = 8; bar.style.borderBottomLeftRadius = 8; bar.style.borderBottomRightRadius = 8;
                bar.style.borderLeftWidth = 1; bar.style.borderRightWidth = 1; bar.style.borderTopWidth = 1; bar.style.borderBottomWidth = 1;
                Color edge = new Color(0.55f, 0.4f, 0.24f, 0.9f);
                bar.style.borderLeftColor = edge; bar.style.borderRightColor = edge; bar.style.borderTopColor = edge; bar.style.borderBottomColor = edge;
                _fill = new VisualElement { pickingMode = PickingMode.Ignore };
                _fill.style.height = Length.Percent(100);
                _fill.style.backgroundColor = new Color(1f, 0.74f, 0.3f, 1f);
                _fill.style.borderTopLeftRadius = 7; _fill.style.borderBottomLeftRadius = 7; _fill.style.borderTopRightRadius = 7; _fill.style.borderBottomRightRadius = 7;
                bar.Add(_fill);
                _track.Add(bar);
                _knob = new VisualElement { pickingMode = PickingMode.Ignore };
                _knob.style.position = Position.Absolute;
                _knob.style.width = 24; _knob.style.height = 24; _knob.style.top = 5;
                _knob.style.backgroundColor = new Color(0.98f, 0.93f, 0.8f);
                _knob.style.borderTopLeftRadius = 12; _knob.style.borderTopRightRadius = 12; _knob.style.borderBottomLeftRadius = 12; _knob.style.borderBottomRightRadius = 12;
                _knob.style.borderLeftWidth = 2; _knob.style.borderRightWidth = 2; _knob.style.borderTopWidth = 2; _knob.style.borderBottomWidth = 2;
                Color kEdge = new Color(0.35f, 0.22f, 0.1f);
                _knob.style.borderLeftColor = kEdge; _knob.style.borderRightColor = kEdge; _knob.style.borderTopColor = kEdge; _knob.style.borderBottomColor = kEdge;
                _track.Add(_knob);
                Element.Add(_track);

                _value = owner.MakeLabel("", 21, new Color(0.97f, 0.93f, 0.82f), true);
                _value.style.width = 80;
                _value.style.marginLeft = 14;
                _value.style.unityTextAlign = TextAnchor.MiddleRight;
                Element.Add(_value);

                _track.RegisterCallback<PointerDownEvent>(e => { _drag = true; _track.CapturePointer(e.pointerId); FromPointer(e.localPosition.x); });
                _track.RegisterCallback<PointerMoveEvent>(e => { if (_drag) FromPointer(e.localPosition.x); });
                _track.RegisterCallback<PointerUpEvent>(e => { _drag = false; _track.ReleasePointer(e.pointerId); });
                _track.RegisterCallback<PointerCaptureOutEvent>(_ => _drag = false);
            }

            private void FromPointer(float x)
            {
                float w = _track.resolvedStyle.width;
                if (w <= 1f) return;
                float v = Mathf.Clamp01(x / w);
                v = Mathf.Round(v * 20f) / 20f;   // 5% 단위
                if (Mathf.Approximately(v, _v)) return;
                SetValue(v);
                _onChange?.Invoke(v);
            }

            public void SetValue(float v)
            {
                _v = Mathf.Clamp01(v);
                _fill.style.width = Length.Percent(_v * 100f);
                _knob.style.left = Length.Percent(_v * 100f);
                _knob.style.translate = new Translate(-12, 0);
                _value.text = _format(_v);
            }
        }
    }
}
