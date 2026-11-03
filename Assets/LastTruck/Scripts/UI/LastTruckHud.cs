using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// UI Toolkit으로 그린 게임 HUD (글자 대신 그림을 쓴다).
    ///  - 내 체력: 캐릭터 머리 위 (하트 그림 + 막대)
    ///  - 스태미나: 오른쪽 위 판, 미니맵 아래 (번개 그림 + 막대)
    ///  - 낮/밤 시계: 미니맵 왼쪽 반원 눈금판 (DayNightDial)
    ///  - 트럭 내구도: 위쪽 가운데 강철 판 (트럭 그림 + 막대)
    ///  - 안내 토스트: 아래쪽 가운데
    ///
    /// - 씬에 따로 둘 것이 없다: 게임이 시작되면 자동으로 만들어진다 (LastTruckUiSkin / PanelSettings / USS는 Resources/UI).
    /// - 판의 모양은 Kenney UI Pack(Adventure) 9-slice 스프라이트, 막대는 USS의 둥근 사각형이다.
    /// - 예전 uGUI 체력 막대(PlayerHPUI)는 이 HUD가 대신하므로 숨긴다. 에셋이 하나라도 없으면 아무것도 바꾸지 않고 예전 UI가 그대로 보인다.
    /// - HUD는 마우스를 가로채지 않는다 (pickingMode = Ignore).
    /// </summary>
    public class LastTruckHud : MonoBehaviour
    {
        private const string HudSettingsPath = "UI/LastTruckHudPanelSettings";
        private const string OverlaySettingsPath = "UI/LastTruckOverlayPanelSettings";
        private const string UssPath = "UI/LastTruckHud";

        private const float OverheadHeight = 2.95f;   // 캐릭터 발 기준 머리 위 높이(m)

        private static LastTruckHud _instance;
        public static bool IsReady => _instance != null && _instance._built;

        /// <summary>HUD가 쓰는 글꼴 정의 (다른 UI Toolkit 화면도 같은 글꼴을 쓴다). 준비 전이면 null.</summary>
        public static StyleFontDefinition? SharedFont => IsReady ? _instance._fontDef : (StyleFontDefinition?)null;

        private bool _built;
        private LastTruckUiSkin _skin;
        private FontAsset _sdfFont;
        private StyleFontDefinition _fontDef;

        // 내 상태
        private PlayerStats _stats;
        private Renderer[] _bodyRenderers;
        private TruckInformation _truck;
        private float _nextResolve;
        private bool _legacyHidden;

        // 요소
        private VisualElement _hudRoot;
        private VisualElement _statusPlate;
        private VisualElement _hpOverhead;
        private VisualElement _hpFill, _spFill, _truckFill;
        private Label _spValue, _truckValue;
        private VisualElement _truckPlate, _spRow;
        private DayNightDial _dial;
        private VisualElement _toast;
        private Label _toastText;
        private float _toastUntil;
        private float _spAlpha = 1f;

        // ------------------------------------------------------------------ 시작

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("LastTruckHud");
            DontDestroyOnLoad(go);
            go.AddComponent<LastTruckHud>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Build()
        {
            _skin = LastTruckUiSkin.Get();
            PanelSettings hudSettings = Resources.Load<PanelSettings>(HudSettingsPath);
            PanelSettings overlaySettings = Resources.Load<PanelSettings>(OverlaySettingsPath);
            StyleSheet uss = Resources.Load<StyleSheet>(UssPath);
            if (_skin == null || hudSettings == null || overlaySettings == null || uss == null || _skin.panelBrownDark == null || _skin.panelGreyBolts == null)
            {
                Debug.LogWarning("[LastTruckHud] UI 스킨 에셋이 없어 예전 HUD를 그대로 씁니다. 메뉴 'LastTruck > UI > UI 스킨 만들기'를 실행하세요.");
                return;
            }

            // SDF 글꼴: 굵게/외곽선 같은 글자 효과가 SDF에서만 제대로 나온다 (한글은 처음 쓸 때 하나씩 채워진다)
            if (_skin.font != null)
                _sdfFont = FontAsset.CreateFontAsset(_skin.font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            _fontDef = _sdfFont != null
                ? new StyleFontDefinition(FontDefinition.FromSDFFont(_sdfFont))
                : (_skin.font != null ? new StyleFontDefinition(_skin.font) : new StyleFontDefinition(StyleKeyword.Null));

            // HUD 문서 (팝업 뒤)
            _hudRoot = CreateDocument("Hud", hudSettings, uss);
            BuildStatusPlate(_hudRoot);
            BuildTruckPlate(_hudRoot);
            BuildHpOverhead(_hudRoot);
            BuildDial(_hudRoot);

            // 토스트 문서 (팝업 앞)
            VisualElement toastRoot = CreateDocument("Toast", overlaySettings, uss);
            BuildToast(toastRoot);

            _built = true;
        }

        private VisualElement CreateDocument(string name, PanelSettings settings, StyleSheet uss)
        {
            var go = new GameObject("UIDocument_" + name);
            go.transform.SetParent(transform, false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;

            VisualElement root = doc.rootVisualElement;
            root.AddToClassList("lt-root");
            root.pickingMode = PickingMode.Ignore;
            root.styleSheets.Add(uss);
            return root;
        }

        // ------------------------------------------------------------------ 부품

        /// <summary>스프라이트를 9-slice 배경으로 입힌다. 모서리 두께는 scale배로 줄여서 보여 준다.</summary>
        private static void Skin(VisualElement e, Sprite sprite, float scale = 0.5f, Color? tint = null)
        {
            if (sprite == null) return;
            e.style.backgroundImage = new StyleBackground(sprite);
            if (tint.HasValue) e.style.unityBackgroundImageTintColor = tint.Value;
            e.style.unitySliceLeft = Mathf.RoundToInt(sprite.border.x);
            e.style.unitySliceBottom = Mathf.RoundToInt(sprite.border.y);
            e.style.unitySliceRight = Mathf.RoundToInt(sprite.border.z);
            e.style.unitySliceTop = Mathf.RoundToInt(sprite.border.w);
            e.style.unitySliceScale = scale;
        }

        private Label MakeLabel(string text, string cls)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("lt-text");
            if (!string.IsNullOrEmpty(cls)) l.AddToClassList(cls);
            l.style.unityFontDefinition = _fontDef;
            return l;
        }

        /// <summary>그림 요소 (그림이 없으면 null).</summary>
        private static VisualElement MakeIcon(string name, float size)
        {
            Sprite s = UiIcons.Get(name);
            if (s == null) return null;
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.width = size;
            e.style.height = size;
            e.style.backgroundImage = new StyleBackground(s);
            e.style.flexShrink = 0;
            return e;
        }

        /// <summary>한 줄: [그림 이름표] [막대(+값)]. 그림이 없으면 fallbackTag 글자. fillClass는 막대 색 USS 클래스.</summary>
        private VisualElement MakeBarRow(string iconName, string fallbackTag, string fillClass, bool showValue, out VisualElement fill, out Label value)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("lt-row");

            VisualElement icon = MakeIcon(iconName, 32f);
            if (icon != null)
            {
                icon.style.marginRight = 10;
                row.Add(icon);
            }
            else
            {
                row.Add(MakeLabel(fallbackTag, "lt-tag"));
            }

            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("lt-track");
            fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("lt-fill");
            if (!string.IsNullOrEmpty(fillClass)) fill.AddToClassList(fillClass);
            track.Add(fill);

            var shine = new VisualElement { pickingMode = PickingMode.Ignore };
            shine.AddToClassList("lt-shine");
            track.Add(shine);

            value = MakeLabel("", "lt-value");
            if (showValue) track.Add(value);

            row.Add(track);
            return row;
        }

        private void BuildStatusPlate(VisualElement root)
        {
            // 스태미나 판 (체력은 캐릭터 머리 위로 옮겼다)
            _statusPlate = new VisualElement { pickingMode = PickingMode.Ignore };
            _statusPlate.AddToClassList("lt-status-plate");
            _statusPlate.style.width = 226;
            _statusPlate.style.top = 280;
            _statusPlate.style.paddingTop = 12;
            _statusPlate.style.paddingBottom = 10;
            Skin(_statusPlate, _skin.panelBrownDark, 0.5f, new Color(0.74f, 0.62f, 0.55f, 1f));

            _spRow = MakeBarRow("bolt", "SP", "lt-fill-sp", false, out _spFill, out _spValue);
            _statusPlate.Add(_spRow);

            _statusPlate.style.display = DisplayStyle.None;
            root.Add(_statusPlate);
        }

        private void BuildTruckPlate(VisualElement root)
        {
            _truckPlate = new VisualElement { pickingMode = PickingMode.Ignore };
            _truckPlate.AddToClassList("lt-truck-plate");
            Skin(_truckPlate, _skin.panelGreyBolts);
            // 위쪽 가운데는 캐릭터 머리 위 체력과 겹치므로 왼쪽 위로 옮긴다
            _truckPlate.style.left = 30;
            _truckPlate.style.translate = new Translate(0, 0);
            _truckPlate.style.width = 420;

            _truckPlate.Add(MakeBarRow("truck", "트럭", "lt-fill-truck", true, out _truckFill, out _truckValue));
            _truckPlate.style.display = DisplayStyle.None;
            root.Add(_truckPlate);
        }

        /// <summary>캐릭터 머리 위에 따라다니는 체력: 하트 그림 + 막대 (글자 없음).</summary>
        private void BuildHpOverhead(VisualElement root)
        {
            _hpOverhead = new VisualElement { pickingMode = PickingMode.Ignore };
            _hpOverhead.style.position = Position.Absolute;
            _hpOverhead.style.flexDirection = FlexDirection.Row;
            _hpOverhead.style.alignItems = Align.Center;
            _hpOverhead.style.width = 168;
            _hpOverhead.style.height = 34;
            _hpOverhead.style.display = DisplayStyle.None;

            VisualElement heart = MakeIcon("heart", 34f);
            if (heart != null)
            {
                heart.style.marginRight = 4;
                _hpOverhead.Add(heart);
            }

            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("lt-track");
            track.style.height = 20;
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius = track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 10;
            _hpFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _hpFill.AddToClassList("lt-fill");
            _hpFill.AddToClassList("lt-fill-hp");
            track.Add(_hpFill);
            var shine = new VisualElement { pickingMode = PickingMode.Ignore };
            shine.AddToClassList("lt-shine");
            track.Add(shine);
            _hpOverhead.Add(track);

            root.Add(_hpOverhead);
        }

        private void BuildDial(VisualElement root)
        {
            _dial = new DayNightDial(_fontDef);
            _dial.style.position = Position.Absolute;
            _dial.style.right = 36 + 220 + 18;   // 미니맵(오른쪽 36, 폭 220) 왼쪽
            _dial.style.top = 36;
            _dial.style.display = DisplayStyle.None;
            root.Add(_dial);
        }

        private void BuildToast(VisualElement root)
        {
            _toast = new VisualElement { pickingMode = PickingMode.Ignore };
            _toast.AddToClassList("lt-toast");
            Skin(_toast, _skin.panelBrownDark, 0.5f, new Color(0.7f, 0.58f, 0.5f, 1f));
            _toastText = MakeLabel("", "lt-toast-text");
            _toast.Add(_toastText);
            _toast.style.opacity = 0f;
            root.Add(_toast);
        }

        // ------------------------------------------------------------------ 외부에서 부르는 함수

        /// <summary>아래쪽 가운데에 잠깐 문구를 띄운다. HUD가 준비되지 않았으면 false (호출한 쪽이 예전 방식으로 보여 준다).</summary>
        public static bool TryShowToast(string message, float seconds)
        {
            if (!IsReady || string.IsNullOrEmpty(message)) return false;
            _instance._toastText.text = message;
            _instance._toast.style.opacity = 1f;
            _instance._toastUntil = Time.unscaledTime + seconds;
            return true;
        }

        // ------------------------------------------------------------------ 갱신

        private void Update()
        {
            if (!_built) return;

            if (Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.5f;
                Resolve();
            }

            UpdateStatus();
            UpdateHpOverhead();
            UpdateDial();
            UpdateTruck();
            UpdateToast();
        }

        private void Resolve()
        {
            if (_stats == null)
            {
                var local = LastTruck.Networking.NetworkPlayer.Local;
                if (local != null)
                    _stats = local.GetComponentInChildren<PlayerStats>(true);
                if (_stats == null && !LastTruck.Networking.GameLauncher.IsOnlineSession)
                    _stats = FindFirstObjectByType<PlayerStats>();

                if (_stats != null)
                {
                    var list = new System.Collections.Generic.List<Renderer>();
                    foreach (Renderer r in _stats.GetComponentsInChildren<Renderer>(true))
                        if (r is SkinnedMeshRenderer || r is MeshRenderer) list.Add(r);
                    _bodyRenderers = list.ToArray();
                }
            }

            if (_truck == null)
                _truck = FindFirstObjectByType<TruckInformation>();

            // 예전 체력 막대(PlayerHPUI)는 이 HUD가 대신한다
            if (_stats != null && !_legacyHidden)
            {
                PlayerHPUI legacy = FindFirstObjectByType<PlayerHPUI>(FindObjectsInactive.Include);
                if (legacy != null && legacy.transform.parent != null)
                    legacy.transform.parent.gameObject.SetActive(false);
                _legacyHidden = true;
            }
        }

        private static void SetFill(VisualElement fill, float ratio)
        {
            fill.style.width = new Length(Mathf.Clamp01(ratio) * 100f, LengthUnit.Percent);
        }

        private void UpdateStatus()
        {
            bool show = _stats != null;
            _statusPlate.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            PlayerStamina st = PlayerStamina.Instance;
            if (st == null) return;

            float sr = st.Max > 0f ? st.Current / st.Max : 0f;
            SetFill(_spFill, sr);
            _spFill.EnableInClassList("lt-fill-tired", st.Exhausted);

            // 가득 차 있고 안 달릴 때는 스태미나 판이 흐려진다
            float target = (sr >= 0.999f && !PlayerStamina.Sprinting) ? 0.45f : 1f;
            _spAlpha = Mathf.MoveTowards(_spAlpha, target, Time.unscaledDeltaTime * 3f);
            _statusPlate.style.opacity = _spAlpha;
        }

        private void UpdateHpOverhead()
        {
            Camera cam = Camera.main;
            if (_stats == null || cam == null || _hudRoot == null || _hudRoot.panel == null)
            {
                _hpOverhead.style.display = DisplayStyle.None;
                return;
            }

            // 머리 위: 캐릭터 몸(렌더러)의 맨 위 + 조금. 렌더러를 못 찾으면 발 위치 기준 고정 높이.
            Vector3 world = _stats.transform.position + Vector3.up * OverheadHeight;
            if (_bodyRenderers != null && _bodyRenderers.Length > 0)
            {
                float top = float.MinValue;
                foreach (Renderer r in _bodyRenderers)
                    if (r != null && r.enabled && r.gameObject.activeInHierarchy) top = Mathf.Max(top, r.bounds.max.y);
                if (top > float.MinValue)
                    world = new Vector3(_stats.transform.position.x, top + 0.15f, _stats.transform.position.z);
            }
            Vector3 viewport = cam.WorldToViewportPoint(world);
            if (viewport.z <= 0f)
            {
                _hpOverhead.style.display = DisplayStyle.None;
                return;
            }

            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(_hudRoot.panel, world, cam);
            _hpOverhead.style.display = DisplayStyle.Flex;
            _hpOverhead.style.left = p.x - 84f;
            _hpOverhead.style.top = Mathf.Max(6f, p.y - 34f);   // 카메라가 가까워 머리가 화면 위로 나가도 막대는 보이게

            float max = Mathf.Max(1f, _stats.MaxHealth);
            float ratio = Mathf.Clamp01(_stats.currentHealth / max);
            SetFill(_hpFill, ratio);
            // 체력이 25% 아래면 막대가 깜빡인다
            _hpFill.style.opacity = ratio < 0.25f ? 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) : 1f;
        }

        private void UpdateDial()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.IsWaitingForNetworkStart)
            {
                _dial.style.display = DisplayStyle.None;
                return;
            }
            _dial.style.display = DisplayStyle.Flex;

            if (gm.CurrentPhase == GameManager.GamePhase.Day)
            {
                float total = gm.DayDurationSeconds;
                float progress = total > 0f ? 1f - Mathf.Clamp01(gm.DayTimeRemaining / total) : 0f;
                int secs = Mathf.CeilToInt(Mathf.Max(0f, gm.DayTimeRemaining));
                _dial.SetState(progress, false, (secs / 60).ToString("00") + ":" + (secs % 60).ToString("00"), gm.CurrentCycle);
            }
            else
            {
                int quota = gm.NightMonsterQuota;
                float progress = quota > 0 ? Mathf.Clamp01((float)gm.NightMonstersKilled / quota) : 0f;
                _dial.SetState(progress, true, gm.NightMonstersKilled + "/" + quota, gm.CurrentCycle);
            }
        }

        private void UpdateTruck()
        {
            bool show = _truck != null;
            _truckPlate.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            float max = Mathf.Max(1f, _truck.MaxDurability);
            float cur = _truck.CurrentDurability;
            float ratio = Mathf.Clamp01(cur / max);
            SetFill(_truckFill, ratio);
            _truckValue.text = _truck.IsBroken ? "!" : Mathf.CeilToInt(cur) + " / " + Mathf.CeilToInt(max);

            // 많이 부서질수록 초록 → 노랑 → 빨강, 25% 아래에서는 깜빡임
            Color c = ratio > 0.5f
                ? Color.Lerp(new Color(0.95f, 0.78f, 0.22f), new Color(0.36f, 0.80f, 0.34f), (ratio - 0.5f) * 2f)
                : Color.Lerp(new Color(0.92f, 0.26f, 0.22f), new Color(0.95f, 0.78f, 0.22f), ratio * 2f);
            _truckFill.style.backgroundColor = c;
            _truckFill.style.opacity = ratio < 0.25f ? 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) : 1f;
        }

        private void UpdateToast()
        {
            float left = _toastUntil - Time.unscaledTime;
            if (left <= 0f)
            {
                if (_toast.resolvedStyle.opacity > 0f) _toast.style.opacity = 0f;
                return;
            }
            _toast.style.opacity = Mathf.Clamp01(left / 0.4f);
        }
    }
}
