using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck.Networking
{
    /// <summary>
    /// LobbyToolkitView: 설정 창. 탭 = 소리 / 화면 / 조작키 / 닉네임, 아래쪽 = [기본값](지금 탭) [적용](전부 저장), 오른쪽 위 ✕(저장 안 하고 닫기).
    ///  - 소리: 슬라이더를 움직이면 바로 들리고(미리듣기), [적용]을 눌러야 저장된다.
    ///  - 조작키: 키 버튼을 누른 뒤 새 키(마우스 버튼 포함)를 누른다. Esc = 취소, 이미 쓰는 키면 두 행동의 키를 맞바꾼다.
    /// 값은 GameSettings / KeyBindings / PlayerProfile이 PlayerPrefs에 저장한다.
    /// </summary>
    public partial class LobbyToolkitView
    {
        #region 상태

        private VisualElement _settingsDim;
        private readonly VisualElement[] _tabPages = new VisualElement[4];
        private readonly Button[] _tabButtons = new Button[4];
        private int _tab;
        private Label _setFootText, _setToast;
        private Button _defaultsBtn;

        private GameSettings.Values _draft;
        private KeyCode[] _draftKeys;

        // 소리
        private Slider _masterSlider, _sfxSlider, _bgmSlider;
        private Label _masterValue, _sfxValue, _bgmValue;

        // 화면
        private Label _resValue, _modeValue, _qualityValue, _fpsValue;
        private Button _fpsPrev, _fpsNext, _vsyncOff, _vsyncOn;
        private List<Vector2Int> _resOptions;

        // 조작키
        private readonly Dictionary<GameAction, (VisualElement row, Button button)> _keyRows = new Dictionary<GameAction, (VisualElement, Button)>();
        private GameAction? _capturing;
        private int _captureFrame;
        private bool _captureJustEnded;
        private static KeyCode[] _allKeys;

        // 닉네임
        private TextField _nickField;
        private Label _nickHint;

        private static readonly string[] TabNames = { "소리", "화면", "조작키", "닉네임" };

        #endregion

        #region 만들기

        private void BuildSettingsWindow()
        {
            _settingsDim = Modal(out VisualElement w, "mn-settings");
            w.Add(Header("설정", "SETTINGS", Btn(null, CloseSettings, null, "close", false, "mn-copy--small")));
            w.Add(Tape());

            var tabs = Box("mn-tabs");
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                _tabButtons[i] = Btn(TabNames[i], () => SelectTab(index), null, null, false, "mn-tab");
                tabs.Add(_tabButtons[i]);
            }
            w.Add(tabs);

            var body = Box("mn-set-body");
            _tabPages[0] = BuildSoundPage();
            _tabPages[1] = BuildDisplayPage();
            _tabPages[2] = BuildKeysPage();
            _tabPages[3] = BuildNicknamePage();
            foreach (VisualElement page in _tabPages) body.Add(page);
            w.Add(body);

            var foot = Box("mn-set-foot");
            var footText = Box("mn-set-foot-text");
            _setFootText = L("", "mn-small");
            _setFootText.style.opacity = 0.75f;
            footText.Add(_setFootText);
            _setToast = L("", "mn-toast");
            footText.Add(_setToast);
            foot.Add(footText);
            _defaultsBtn = Btn("기본값", ResetCurrentTab);
            foot.Add(_defaultsBtn);
            foot.Add(Btn("적용", ApplySettings, "primary"));
            w.Add(foot);
        }

        private VisualElement BuildSoundPage()
        {
            var page = Box(null);
            page.Add(SliderRow("전체 볼륨", out _masterSlider, out _masterValue));
            page.Add(SliderRow("효과음", out _sfxSlider, out _sfxValue));
            page.Add(SliderRow("배경음", out _bgmSlider, out _bgmValue));
            foreach (Slider s in new[] { _masterSlider, _sfxSlider, _bgmSlider })
                s.RegisterValueChangedCallback(_ => OnVolumeChanged());
            return page;
        }

        private VisualElement SliderRow(string label, out Slider slider, out Label value)
        {
            var row = Box("mn-set-row");
            row.AddToClassList("mn-well");
            row.Add(L(label, "mn-set-label", FontKind.Heavy));
            slider = new Slider(0f, 100f);
            slider.AddToClassList("mn-slider");
            row.Add(slider);
            value = L("100", "mn-set-value", FontKind.Heavy);
            row.Add(value);
            return row;
        }

        private VisualElement BuildDisplayPage()
        {
            var page = Box(null);
            page.Add(CyclerRow("해상도", out _resValue, () => StepResolution(-1), () => StepResolution(+1), out _, out _));
            page.Add(CyclerRow("화면 모드", out _modeValue, () => StepMode(-1), () => StepMode(+1), out _, out _));
            page.Add(CyclerRow("그래픽 품질", out _qualityValue, () => StepQuality(-1), () => StepQuality(+1), out _, out _));
            page.Add(CyclerRow("FPS 제한", out _fpsValue, () => StepFps(-1), () => StepFps(+1), out _fpsPrev, out _fpsNext));

            var vs = Box("mn-set-row");
            vs.AddToClassList("mn-well");
            vs.Add(L("수직 동기화", "mn-set-label", FontKind.Heavy));
            _vsyncOff = Btn("끄기", () => { _draft.vsync = false; RefreshDisplayPage(); });
            _vsyncOn = Btn("켜기", () => { _draft.vsync = true; RefreshDisplayPage(); });
            _vsyncOff.style.width = 160;
            _vsyncOn.style.width = 160;
            _vsyncOn.style.marginLeft = 10;
            vs.Add(_vsyncOff);
            vs.Add(_vsyncOn);
            vs.Add(Grow());
            vs.Add(L("켜면 FPS 제한 대신 모니터 주사율을 따라요", "mn-small"));
            page.Add(vs);
            return page;
        }

        private VisualElement CyclerRow(string label, out Label value, Action prev, Action next, out Button prevBtn, out Button nextBtn)
        {
            var row = Box("mn-set-row");
            row.AddToClassList("mn-well");
            row.Add(L(label, "mn-set-label", FontKind.Heavy));
            var c = Box("mn-cycler");
            prevBtn = Btn("◀", prev, null, null, false, "mn-cycler-btn");
            value = L("", "mn-cycler-value", FontKind.Heavy);
            nextBtn = Btn("▶", next, null, null, false, "mn-cycler-btn");
            c.Add(prevBtn);
            c.Add(value);
            c.Add(nextBtn);
            row.Add(c);
            return row;
        }

        private VisualElement BuildKeysPage()
        {
            var page = Box("mn-keys");
            var left = Box("mn-keycol");
            left.AddToClassList("mn-well");
            var right = Box("mn-keycol");
            right.AddToClassList("mn-well");
            right.AddToClassList("mn-keycol--right");
            page.Add(left);
            page.Add(right);

            int i = 0;
            foreach (KeyBindings.ActionInfo info in KeyBindings.Rebindable)
            {
                GameAction action = info.Action;
                var row = Box("mn-keyrow");
                if (i % 2 == 1) row.AddToClassList("mn-keyrow--alt");
                row.Add(L(info.Label, "mn-keyrow-label"));
                var btn = Btn("", () => BeginCapture(action), null, null, false, "mn-keybtn");
                btn.Add(L("", "mn-btn-text", FontKind.Heavy));
                btn.RemoveFromClassList("mn-btn--icononly");
                row.Add(btn);
                (i < 9 ? left : right).Add(row);
                _keyRows[action] = (row, btn);
                i++;
            }
            return page;
        }

        private VisualElement BuildNicknamePage()
        {
            var page = Box(null);
            var row = Box("mn-set-row");
            row.AddToClassList("mn-well");
            row.style.height = 110;
            row.Add(L("닉네임", "mn-set-label", FontKind.Heavy));
            _nickField = Field(null, LobbyRules.NicknameMaxLength);
            _nickField.style.flexGrow = 1;
            row.Add(_nickField);
            page.Add(row);
            _nickHint = L("", "mn-hint");
            _nickHint.style.marginLeft = 24;
            page.Add(_nickHint);
            return page;
        }

        #endregion

        #region 열기 / 닫기 / 탭

        public void OpenSettings()
        {
            _draft = GameSettings.Current;
            _draftKeys = KeyBindings.Snapshot();
            _resOptions = GameSettings.ResolutionOptions();
            _nickField.SetValueWithoutNotify(PlayerProfile.Nickname);
            bool canRename = _launcher == null || (_launcher.State != LauncherState.InRoom && _launcher.State != LauncherState.StartingGame);
            _nickField.SetEnabled(canRename);
            _nickHint.text = canRename
                ? $"{LobbyRules.NicknameMinLength}~{LobbyRules.NicknameMaxLength}자 · 방에 들어가면 다른 사람에게 보이는 이름이에요"
                : "방 안에서는 닉네임을 바꿀 수 없어요. 방을 나간 뒤 바꿔 주세요.";
            RefreshSoundPage();
            RefreshDisplayPage();
            RefreshKeysPage();
            SelectTab(0);
            Show(_settingsDim, true);
            _settingsDim.BringToFront();
        }

        public void CloseSettings()
        {
            if (!IsShown(_settingsDim)) return;
            CancelCapture();
            GameSettings.CancelPreview();
            Show(_settingsDim, false);
        }

        private void SelectTab(int index)
        {
            CancelCapture();
            _tab = Mathf.Clamp(index, 0, _tabPages.Length - 1);
            for (int i = 0; i < _tabPages.Length; i++)
            {
                Show(_tabPages[i], i == _tab);
                _tabButtons[i].EnableInClassList("mn-btn--on", i == _tab);
            }
            _defaultsBtn.SetEnabled(_tab != 3);
            switch (_tab)
            {
                case 0: _setFootText.text = "슬라이더를 움직이면 바로 들려요. [적용]을 눌러야 저장돼요."; break;
                case 1: _setFootText.text = "해상도와 화면 모드는 실행 파일(빌드)에서 적용돼요."; break;
                case 2: _setFootText.text = "키 버튼을 누른 뒤 새 키를 누르세요 · Esc: 취소 · 이미 쓰는 키면 두 키가 서로 바뀌어요 · 공격(마우스 왼쪽) / ESC / F3는 고정"; break;
                default: _setFootText.text = "[적용]을 누르면 저장돼요."; break;
            }
        }

        private void ResetCurrentTab()
        {
            GameSettings.Values d = GameSettings.Defaults();
            switch (_tab)
            {
                case 0:
                    _draft.master = d.master;
                    _draft.sfx = d.sfx;
                    _draft.bgm = d.bgm;
                    RefreshSoundPage();
                    OnVolumeChanged();
                    break;
                case 1:
                    _draft.width = d.width;
                    _draft.height = d.height;
                    _draft.mode = d.mode;
                    _draft.quality = d.quality;
                    _draft.fpsLimit = d.fpsLimit;
                    _draft.vsync = d.vsync;
                    RefreshDisplayPage();
                    break;
                case 2:
                    CancelCapture();
                    _draftKeys = KeyBindings.DefaultSnapshot();
                    RefreshKeysPage();
                    break;
            }
            Flash(_setToast, "기본값으로 되돌렸어요. [적용]을 눌러 저장하세요.", 2.5f);
        }

        private void ApplySettings()
        {
            CancelCapture();
            string nick = _nickField.value;
            if (_nickField.enabledSelf && PlayerProfile.Clean(nick) != PlayerProfile.Nickname)
            {
                if (!PlayerProfile.TrySetNickname(nick, out string error))
                {
                    SelectTab(3);
                    Flash(_setToast, error, 3f);
                    return;
                }
            }
            GameSettings.Commit(_draft);
            KeyBindings.Apply(_draftKeys);
            _draft = GameSettings.Current;
            Flash(_setToast, "설정을 저장했어요.");
        }

        #endregion

        #region 소리

        private void RefreshSoundPage()
        {
            _masterSlider.SetValueWithoutNotify(_draft.master * 100f);
            _sfxSlider.SetValueWithoutNotify(_draft.sfx * 100f);
            _bgmSlider.SetValueWithoutNotify(_draft.bgm * 100f);
            RefreshVolumeLabels();
        }

        private void RefreshVolumeLabels()
        {
            _masterValue.text = Mathf.RoundToInt(_masterSlider.value).ToString();
            _sfxValue.text = Mathf.RoundToInt(_sfxSlider.value).ToString();
            _bgmValue.text = Mathf.RoundToInt(_bgmSlider.value).ToString();
        }

        private void OnVolumeChanged()
        {
            _draft.master = Mathf.Round(_masterSlider.value) / 100f;
            _draft.sfx = Mathf.Round(_sfxSlider.value) / 100f;
            _draft.bgm = Mathf.Round(_bgmSlider.value) / 100f;
            RefreshVolumeLabels();
            GameSettings.PreviewAudio(_draft.master, _draft.sfx, _draft.bgm);
        }

        #endregion

        #region 화면

        private void RefreshDisplayPage()
        {
            _resValue.text = $"{_draft.width} x {_draft.height}";
            _modeValue.text = GameSettings.ModeName(_draft.mode);
            _qualityValue.text = GameSettings.QualityName(_draft.quality);
            _fpsValue.text = _draft.vsync ? "수직 동기화 사용 중" : GameSettings.FpsName(_draft.fpsLimit);
            _fpsPrev.SetEnabled(!_draft.vsync);
            _fpsNext.SetEnabled(!_draft.vsync);
            _vsyncOff.EnableInClassList("mn-btn--on", !_draft.vsync);
            _vsyncOn.EnableInClassList("mn-btn--on", _draft.vsync);
        }

        private void StepResolution(int dir)
        {
            if (_resOptions == null || _resOptions.Count == 0) return;
            int index = _resOptions.IndexOf(new Vector2Int(_draft.width, _draft.height));
            index = index < 0 ? 0 : (index + dir + _resOptions.Count) % _resOptions.Count;
            _draft.width = _resOptions[index].x;
            _draft.height = _resOptions[index].y;
            RefreshDisplayPage();
        }

        private void StepMode(int dir)
        {
            FullScreenMode[] modes = GameSettings.ModeOptions;
            int index = Array.IndexOf(modes, _draft.mode);
            index = index < 0 ? 0 : (index + dir + modes.Length) % modes.Length;
            _draft.mode = modes[index];
            RefreshDisplayPage();
        }

        private void StepQuality(int dir)
        {
            int count = QualitySettings.names.Length;
            if (count == 0) return;
            _draft.quality = (_draft.quality + dir + count) % count;
            RefreshDisplayPage();
        }

        private void StepFps(int dir)
        {
            int[] options = GameSettings.FpsOptions;
            int index = Array.IndexOf(options, _draft.fpsLimit);
            index = index < 0 ? 1 : (index + dir + options.Length) % options.Length;
            _draft.fpsLimit = options[index];
            RefreshDisplayPage();
        }

        #endregion

        #region 조작키

        private void RefreshKeysPage()
        {
            foreach (KeyValuePair<GameAction, (VisualElement row, Button button)> pair in _keyRows)
            {
                bool waiting = _capturing == pair.Key;
                pair.Value.row.EnableInClassList("mn-keyrow--wait", waiting);
                pair.Value.button.EnableInClassList("mn-keybtn--wait", waiting);
                SetText(pair.Value.button, waiting ? "키를 누르세요..." : KeyBindings.DisplayName(_draftKeys[(int)pair.Key]));
            }
        }

        private void BeginCapture(GameAction action)
        {
            _capturing = action;
            _captureFrame = Time.frameCount;
            foreach (var pair in _keyRows) pair.Value.row.RemoveFromClassList("mn-keyrow--flash");
            RefreshKeysPage();
        }

        private void CancelCapture()
        {
            if (_capturing == null) return;
            _capturing = null;
            RefreshKeysPage();
        }

        private void UpdateKeyCapture()
        {
            if (_capturing == null || Time.frameCount <= _captureFrame || !IsShown(_settingsDim)) return;
            if (!Input.anyKeyDown) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _captureJustEnded = true;   // 이번 Esc로 설정 창까지 닫히지 않게
                CancelCapture();
                return;
            }

            if (_allKeys == null)
            {
                var list = new List<KeyCode>();
                foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
                    if (KeyBindings.IsBindable(k) || k == KeyCode.F3 || k == KeyCode.Mouse0) list.Add(k);
                _allKeys = list.ToArray();
            }

            foreach (KeyCode key in _allKeys)
            {
                if (!Input.GetKeyDown(key)) continue;
                if (key == KeyCode.Mouse0) return;   // 왼쪽 클릭 = 다른 곳을 누름 (공격 고정 키라 쓸 수 없음)
                GameAction action = _capturing.Value;
                if (!KeyBindings.IsBindable(key))
                {
                    Flash(_setToast, $"{KeyBindings.DisplayName(key)} 키는 쓸 수 없어요.", 2f);
                    return;
                }
                GameAction? swapped = KeyBindings.Assign(_draftKeys, action, key);
                _capturing = null;
                RefreshKeysPage();
                if (swapped.HasValue && _keyRows.TryGetValue(swapped.Value, out var other))
                {
                    other.row.AddToClassList("mn-keyrow--flash");
                    Flash(_setToast, $"'{KeyBindings.InfoOf(swapped.Value).Label}'와(과) 키를 서로 바꿨어요.", 2.5f);
                }
                return;
            }
        }

        #endregion
    }
}
