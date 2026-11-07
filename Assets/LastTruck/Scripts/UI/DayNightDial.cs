using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 낮/밤 시계: 반원 모양 눈금판. 해/달이 왼쪽(새벽)에서 오른쪽(해질녘)으로 호를 따라 움직인다.
    ///  - 낮: 호가 금색으로 차오르고(낮 진행), 가운데 숫자는 남은 시간(분:초)
    ///  - 밤: 호가 푸른색으로 바뀌고(처치 진행), 가운데 숫자는 처치 수/목표 수
    ///  - 아래쪽 그림 + 숫자 = 몇 번째 낮/밤인지
    /// 호는 UI Toolkit Painter2D로 그리고, 해/달 마커와 숫자는 자식 요소다.
    /// </summary>
    public class DayNightDial : VisualElement
    {
        public const float Width = 250f;
        public const float Height = 138f;

        private const float Radius = 116f;
        private const float TrackRadius = 92f;
        private const float TrackWidth = 16f;
        private static readonly Vector2 Center = new Vector2(Width * 0.5f, 126f);

        private static readonly Color PlateColor = new Color(0.11f, 0.065f, 0.03f, 0.88f);
        private static readonly Color RimColor = new Color(0.78f, 0.58f, 0.34f, 1f);
        private static readonly Color EmptyColor = new Color(0.30f, 0.21f, 0.13f, 1f);
        private static readonly Color DayFill = new Color(1f, 0.80f, 0.28f, 1f);
        private static readonly Color NightFill = new Color(0.46f, 0.62f, 0.96f, 1f);
        private static readonly Color NightEmpty = new Color(0.17f, 0.20f, 0.34f, 1f);

        private float _progress;
        private bool _night;

        private readonly VisualElement _marker;
        private readonly VisualElement _phaseIcon;
        private readonly Label _big;
        private readonly Label _cycle;
        private readonly Sprite _sun, _moon;

        public DayNightDial(StyleFontDefinition font)
        {
            style.width = Width;
            style.height = Height;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;

            _sun = UiIcons.Get("sun");
            _moon = UiIcons.Get("moon");

            // 해/달 마커
            _marker = new VisualElement { pickingMode = PickingMode.Ignore };
            _marker.style.position = Position.Absolute;
            _marker.style.width = 44;
            _marker.style.height = 44;
            Add(_marker);

            // 가운데 큰 숫자
            _big = MakeLabel(font, 34);
            _big.style.position = Position.Absolute;
            _big.style.left = 0;
            _big.style.right = 0;
            _big.style.top = 56;
            Add(_big);

            // 아래쪽 작은 줄: 그림 + 회차 숫자
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.position = Position.Absolute;
            row.style.left = 0;
            row.style.right = 0;
            row.style.top = 96;
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            row.style.alignItems = Align.Center;
            _phaseIcon = new VisualElement { pickingMode = PickingMode.Ignore };
            _phaseIcon.style.width = 26;
            _phaseIcon.style.height = 26;
            _phaseIcon.style.marginRight = 6;
            row.Add(_phaseIcon);
            _cycle = MakeLabel(font, 22);
            row.Add(_cycle);
            Add(row);

            SetState(0f, false, "", 1);
        }

        private static Label MakeLabel(StyleFontDefinition font, int size)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.AddToClassList("lt-text");
            l.style.fontSize = size;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.unityFontDefinition = font;
            return l;
        }

        /// <summary>progress 0~1(호를 따라 간 정도), night 여부, 가운데 숫자 글자, 회차.</summary>
        public void SetState(float progress, bool night, string bigText, int cycle)
        {
            progress = Mathf.Clamp01(progress);
            bool changed = !Mathf.Approximately(progress, _progress) || night != _night;
            _progress = progress;
            _night = night;

            if (_big.text != bigText) _big.text = bigText;
            string c = cycle.ToString();
            if (_cycle.text != c) _cycle.text = c;

            Sprite phase = night ? _moon : _sun;
            if (phase != null)
            {
                _phaseIcon.style.backgroundImage = new StyleBackground(phase);
                _marker.style.backgroundImage = new StyleBackground(phase);
            }

            // 마커 위치: 왼쪽(180도)에서 위를 지나 오른쪽(360도)으로
            float a = Mathf.Deg2Rad * (180f + 180f * progress);
            float mx = Center.x + TrackRadius * Mathf.Cos(a) - 22f;
            float my = Center.y + TrackRadius * Mathf.Sin(a) - 22f;
            _marker.style.left = mx;
            _marker.style.top = my;

            if (changed) MarkDirtyRepaint();
        }

        private void Paint(MeshGenerationContext ctx)
        {
            Painter2D p = ctx.painter2D;

            // 반원 바탕
            p.fillColor = PlateColor;
            p.BeginPath();
            p.MoveTo(new Vector2(Center.x - Radius, Center.y));
            p.Arc(Center, Radius, Angle.Degrees(180f), Angle.Degrees(360f), ArcDirection.Clockwise);
            p.ClosePath();
            p.Fill();

            // 테두리
            p.strokeColor = RimColor;
            p.lineWidth = 4f;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(Center.x - Radius, Center.y));
            p.Arc(Center, Radius, Angle.Degrees(180f), Angle.Degrees(360f), ArcDirection.Clockwise);
            p.LineTo(new Vector2(Center.x - Radius, Center.y));
            p.Stroke();

            // 빈 호
            p.lineWidth = TrackWidth;
            p.strokeColor = _night ? NightEmpty : EmptyColor;
            p.BeginPath();
            p.Arc(Center, TrackRadius, Angle.Degrees(180f), Angle.Degrees(360f), ArcDirection.Clockwise);
            p.Stroke();

            // 지나온 호
            if (_progress > 0.003f)
            {
                p.strokeColor = _night ? NightFill : DayFill;
                p.BeginPath();
                p.Arc(Center, TrackRadius, Angle.Degrees(180f), Angle.Degrees(180f + 180f * _progress), ArcDirection.Clockwise);
                p.Stroke();
            }

            // 눈금 (0/25/50/75/100%)
            p.strokeColor = new Color(1f, 0.92f, 0.75f, 0.55f);
            p.lineWidth = 3f;
            for (int i = 0; i <= 4; i++)
            {
                float a = Mathf.Deg2Rad * (180f + 45f * i);
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.BeginPath();
                p.MoveTo(Center + d * (TrackRadius + TrackWidth * 0.5f + 3f));
                p.LineTo(Center + d * (TrackRadius + TrackWidth * 0.5f + 11f));
                p.Stroke();
            }
        }
    }
}
