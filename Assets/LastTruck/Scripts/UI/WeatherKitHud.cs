using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 날씨 키트 (개발/테스트용 패널). F4로 열고 닫고, 맑음 F5 / 안개 F6 / 비 F7 / 폭풍우 F8 / 자동 F9 (버튼을 눌러도 된다).
    /// 개발 빌드(에디터 포함)에서만 만들어진다. 정식 빌드에서는 Debug.isDebugBuild가 꺼져 있어 나타나지 않는다.
    /// 고른 날씨는 내 화면에만 적용된다 (멀티플레이에서 다른 사람에게 보내지 않는다).
    /// </summary>
    public class WeatherKitHud
    {
        private static readonly Color Gold = new Color(1f, 0.8f, 0.34f, 1f);
        private static readonly Color Cream = new Color(0.97f, 0.93f, 0.82f, 1f);

        private static readonly KeyCode[] Keys = { KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8, KeyCode.F9 };
        private static readonly string[] Names = { "맑음", "안개", "비", "폭풍우", "자동" };

        private readonly StyleFontDefinition _font;
        private VisualElement _panel;
        private readonly VisualElement[] _buttons = new VisualElement[5];
        private bool _open;

        public static bool Enabled { get { return Debug.isDebugBuild; } }

        public WeatherKitHud(StyleFontDefinition font) { _font = font; }

        public void Build(VisualElement root)
        {
            if (!Enabled) return;

            _panel = new VisualElement();   // 버튼을 누를 수 있도록 picking은 기본값
            _panel.style.position = Position.Absolute;
            _panel.style.left = 26; _panel.style.bottom = 26;
            _panel.style.paddingLeft = 16; _panel.style.paddingRight = 16;
            _panel.style.paddingTop = 12; _panel.style.paddingBottom = 14;
            _panel.style.backgroundColor = new Color(0.08f, 0.07f, 0.06f, 0.92f);
            Radius(_panel, 12);
            Border(_panel, 2, new Color(1f, 0.8f, 0.34f, 0.5f));
            _panel.style.display = DisplayStyle.None;

            var title = Text("날씨 키트  (F4 닫기)", 19, Gold);
            title.style.marginBottom = 8;
            _panel.Add(title);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            for (int i = 0; i < Names.Length; i++)
            {
                int index = i;
                var b = new VisualElement();
                b.style.alignItems = Align.Center;
                b.style.paddingLeft = 12; b.style.paddingRight = 12;
                b.style.paddingTop = 6; b.style.paddingBottom = 6;
                b.style.marginRight = i < Names.Length - 1 ? 6 : 0;
                Radius(b, 10);
                Border(b, 2, new Color(1f, 1f, 1f, 0.2f));
                b.Add(Text(Names[i], 21, Cream));
                var key = Text(Keys[i].ToString(), 15, new Color(1f, 1f, 1f, 0.55f));
                b.Add(key);
                b.RegisterCallback<ClickEvent>(e => Pick(index));
                row.Add(b);
                _buttons[i] = b;
            }
            _panel.Add(row);
            root.Add(_panel);
        }

        private static void Pick(int index)
        {
            switch (index)
            {
                case 0: WeatherDirector.SetOverride(WeatherKind.Clear); break;
                case 1: WeatherDirector.SetOverride(WeatherKind.Fog); break;
                case 2: WeatherDirector.SetOverride(WeatherKind.Rain); break;
                case 3: WeatherDirector.SetOverride(WeatherKind.Storm); break;
                default: WeatherDirector.SetOverride(null); break;
            }
        }

        public void Tick()
        {
            if (_panel == null) return;

            if (Input.GetKeyDown(KeyCode.F4)) _open = !_open;
            for (int i = 0; i < Keys.Length; i++)
                if (Input.GetKeyDown(Keys[i])) Pick(i);   // 패널이 닫혀 있어도 단축키는 동작한다

            _panel.style.display = _open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_open) return;

            int current = WeatherDirector.Override.HasValue ? (int)WeatherDirector.Override.Value : 4;
            for (int i = 0; i < _buttons.Length; i++)
            {
                bool on = i == current;
                _buttons[i].style.backgroundColor = on ? new Color(1f, 0.8f, 0.34f, 0.3f) : new Color(1f, 1f, 1f, 0.05f);
                Color border = on ? Gold : new Color(1f, 1f, 1f, 0.2f);
                _buttons[i].style.borderTopColor = _buttons[i].style.borderBottomColor = _buttons[i].style.borderLeftColor = _buttons[i].style.borderRightColor = border;
            }
        }

        private Label Text(string text, float size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("lt-text");
            l.style.unityFontDefinition = _font;
            l.style.fontSize = size;
            l.style.color = color;
            return l;
        }

        private static void Radius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        private static void Border(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
        }
    }
}
