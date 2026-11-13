using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 채집 대상 이름표 + E키를 꾹 누를 때의 원형 게이지 (UI Toolkit). 계산은 CraftingSystem.GatherProgressUI가 하고 값만 받는다.
    ///  - 이름표: 대상 위(화면 좌표)에 나무 판 모양으로, 이름(색) + 부가 설명.
    ///  - 원형 게이지: 이름표 바로 아래 (이름표가 없으면 화면 가운데 조금 아래 - 먹기/덫 설치).
    /// </summary>
    public class GatherHudView : MonoBehaviour
    {
        private const string PanelSettingsPath = "UI/LastTruckHudPanelSettings";
        private const string UssPath = "UI/LastTruckGather";

        private VisualElement _root, _plate, _gauge;
        private Label _name, _sub, _percent;
        private RingGauge _ring;
        private Vector2 _platePos;
        private bool _platePosValid;
        private bool _labelShown;

        public static GatherHudView Create()
        {
            LastTruckUiSkin skin = LastTruckUiSkin.Get();
            PanelSettings settings = Resources.Load<PanelSettings>(PanelSettingsPath);
            StyleSheet uss = Resources.Load<StyleSheet>(UssPath);
            if (skin == null || settings == null || uss == null) return null;

            var go = new GameObject("UIDocument_GatherHud");
            DontDestroyOnLoad(go);
            var view = go.AddComponent<GatherHudView>();
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            doc.sortingOrder = 3;
            view.Build(doc.rootVisualElement, uss, skin);
            return view;
        }

        private void Build(VisualElement root, StyleSheet uss, LastTruckUiSkin skin)
        {
            _root = root;
            root.styleSheets.Add(uss);
            root.AddToClassList("lt-root");
            root.pickingMode = PickingMode.Ignore;
            StyleFontDefinition? font = LastTruckHud.SharedFont;

            _plate = new VisualElement { pickingMode = PickingMode.Ignore };
            _plate.AddToClassList("gh-plate");
            UiToolkitSkin.Slice(_plate, skin.panelBrownDark, 0.45f, new Color(0.70f, 0.58f, 0.50f, 1f));
            _name = MakeLabel("", "gh-name", font);
            _sub = MakeLabel("", "gh-sub", font);
            _plate.Add(_name);
            _plate.Add(_sub);
            _plate.style.display = DisplayStyle.None;
            root.Add(_plate);

            _gauge = new VisualElement { pickingMode = PickingMode.Ignore };
            _gauge.AddToClassList("gh-gauge");
            _ring = new RingGauge { pickingMode = PickingMode.Ignore };
            _ring.AddToClassList("gh-ring");
            _gauge.Add(_ring);
            _percent = MakeLabel("", "gh-percent", font);
            _gauge.Add(_percent);
            _gauge.style.display = DisplayStyle.None;
            root.Add(_gauge);
        }

        private static Label MakeLabel(string text, string cls, StyleFontDefinition? font)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("lt-text");
            l.AddToClassList(cls);
            if (font.HasValue) l.style.unityFontDefinition = font.Value;
            return l;
        }

        /// <summary>화면 좌표(픽셀, 아래가 0) → 이 패널 좌표 (위가 0).</summary>
        private Vector2 ToPanel(Vector2 screen)
        {
            if (_root.panel == null) return screen;
            return RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
        }

        /// <summary>이름표. screenAnchor = 대상 머리 위 지점의 화면 좌표.</summary>
        public void SetLabel(bool show, string name, Color nameColor, string sub, Vector2 screenAnchor)
        {
            _labelShown = show;
            _plate.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) { _platePosValid = false; return; }

            _name.text = name;
            _name.style.color = nameColor;
            bool hasSub = !string.IsNullOrEmpty(sub);
            _sub.text = hasSub ? sub : "";
            _sub.style.display = hasSub ? DisplayStyle.Flex : DisplayStyle.None;

            Vector2 p = ToPanel(screenAnchor);
            Rect panelRect = _root.layout;
            float w = Mathf.Max(110f, _plate.layout.width), h = Mathf.Max(40f, _plate.layout.height);
            Vector2 target = new Vector2(p.x - w * 0.5f, p.y - h - 8f);
            if (panelRect.width > 1f)
            {
                target.x = Mathf.Clamp(target.x, 8f, panelRect.width - w - 8f);
                target.y = Mathf.Clamp(target.y, 8f, panelRect.height - h - 80f);
            }
            _platePos = _platePosValid ? Vector2.Lerp(_platePos, target, 1f - Mathf.Exp(-18f * Time.deltaTime)) : target;
            _platePosValid = true;
            _plate.style.left = _platePos.x;
            _plate.style.top = _platePos.y;
        }

        /// <summary>원형 게이지 (progress 0~1).</summary>
        public void SetGauge(bool show, float progress)
        {
            _gauge.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            _ring.Progress = Mathf.Clamp01(progress);
            _percent.text = Mathf.RoundToInt(progress * 100f) + "%";

            const float size = 64f;
            Rect panelRect = _root.layout;
            Vector2 pos;
            if (_labelShown && _platePosValid)
            {
                float w = Mathf.Max(110f, _plate.layout.width), h = Mathf.Max(40f, _plate.layout.height);
                pos = new Vector2(_platePos.x + w * 0.5f - size * 0.5f, _platePos.y + h + 6f);
            }
            else
            {
                pos = new Vector2(panelRect.width * 0.5f - size * 0.5f, panelRect.height * 0.5f + 90f - size * 0.5f);
            }
            _gauge.style.left = pos.x;
            _gauge.style.top = pos.y;
        }

        /// <summary>둥근 진행 고리 (위에서 시계 방향으로 찬다).</summary>
        private class RingGauge : VisualElement
        {
            private float _progress;
            public float Progress
            {
                get => _progress;
                set { if (!Mathf.Approximately(_progress, value)) { _progress = value; MarkDirtyRepaint(); } }
            }

            public RingGauge() { generateVisualContent += Draw; }

            private void Draw(MeshGenerationContext ctx)
            {
                Rect r = contentRect;
                Vector2 c = r.center;
                float radius = Mathf.Min(r.width, r.height) * 0.5f - 6f;
                var p = ctx.painter2D;

                p.lineWidth = 9f;
                p.lineCap = LineCap.Round;
                p.strokeColor = new Color(0.12f, 0.08f, 0.04f, 0.85f);
                p.BeginPath();
                p.Arc(c, radius, 0f, 360f);
                p.Stroke();

                if (_progress <= 0.001f) return;
                p.lineWidth = 6f;
                p.strokeColor = Color.Lerp(new Color(1f, 0.78f, 0.25f), new Color(0.55f, 0.92f, 0.40f), _progress);
                p.BeginPath();
                p.Arc(c, radius, -90f, -90f + 360f * _progress);
                p.Stroke();
            }
        }
    }
}
