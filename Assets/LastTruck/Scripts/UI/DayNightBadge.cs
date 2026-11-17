using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 낮/밤 표시 (화면 위쪽 가운데의 작은 모래시계 판).
    ///  - 낮: 금빛 모래가 위에서 아래로 떨어지고(낮 진행), 숫자는 남은 시간(분:초). 해가 지기 30초 전에는 빨갛게 깜빡인다.
    ///  - 밤: 푸른 모래가 아래에 쌓이며(처치 진행), 숫자는 "처치 2 / 5".
    ///  - 아래쪽 가는 막대도 같은 진행을 보여 준다. 첫 줄은 "N일차 · 낮/밤".
    /// 모래시계는 UI Toolkit Painter2D로 그린다 (그림 에셋이 필요 없다).
    /// </summary>
    public class DayNightBadge : VisualElement
    {
        public const float Width = 270f;
        public const float Height = 66f;

        private static readonly Color PlateColor = new Color(0.11f, 0.065f, 0.03f, 0.9f);
        private static readonly Color RimColor = new Color(1f, 0.92f, 0.75f, 0.45f);
        private static readonly Color DaySand = new Color(1f, 0.80f, 0.28f, 1f);
        private static readonly Color NightSand = new Color(0.52f, 0.68f, 1f, 1f);
        private static readonly Color GlassColor = new Color(1f, 0.96f, 0.86f, 0.9f);
        private static readonly Color DayText = new Color(1f, 0.88f, 0.5f, 1f);
        private static readonly Color NightText = new Color(0.72f, 0.82f, 1f, 1f);
        private static readonly Color WarnText = new Color(1f, 0.38f, 0.32f, 1f);

        private readonly VisualElement _glass;
        private readonly Label _title;
        private readonly Label _big;
        private readonly VisualElement _barFill;

        private float _progress;
        private bool _night;

        public DayNightBadge(StyleFontDefinition font)
        {
            style.width = Width;
            style.height = Height;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 12; style.paddingRight = 16;
            style.backgroundColor = PlateColor;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 16;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 2;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = RimColor;
            pickingMode = PickingMode.Ignore;

            _glass = new VisualElement { pickingMode = PickingMode.Ignore };
            _glass.style.width = 40;
            _glass.style.height = 52;
            _glass.style.marginRight = 12;
            _glass.style.flexShrink = 0;
            _glass.generateVisualContent += PaintHourglass;
            Add(_glass);

            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.style.flexGrow = 1;
            column.style.justifyContent = Justify.Center;

            _title = MakeLabel(font, 17);
            _title.style.unityTextAlign = TextAnchor.MiddleLeft;
            column.Add(_title);

            _big = MakeLabel(font, 28);
            _big.style.unityTextAlign = TextAnchor.MiddleLeft;
            _big.style.marginTop = -2;
            column.Add(_big);

            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.height = 6;
            bar.style.marginTop = 2;
            bar.style.backgroundColor = new Color(0.30f, 0.21f, 0.13f, 1f);
            bar.style.borderTopLeftRadius = bar.style.borderTopRightRadius = bar.style.borderBottomLeftRadius = bar.style.borderBottomRightRadius = 3;
            bar.style.overflow = Overflow.Hidden;
            _barFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _barFill.style.position = Position.Absolute;
            _barFill.style.left = 0; _barFill.style.top = 0; _barFill.style.bottom = 0;
            _barFill.style.width = new Length(0f, LengthUnit.Percent);
            bar.Add(_barFill);
            column.Add(bar);

            Add(column);
            SetState(false, 1, 0f, "", false);
        }

        private static Label MakeLabel(StyleFontDefinition font, int size)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.AddToClassList("lt-text");
            l.style.fontSize = size;
            l.style.unityFontDefinition = font;
            return l;
        }

        /// <summary>night 여부, 몇 번째 낮/밤인지, 진행 0~1(낮=지난 시간, 밤=처치 비율), 큰 글자, 급한 상황(해지기 직전) 여부.</summary>
        public void SetState(bool night, int cycle, float progress, string bigText, bool warn)
        {
            progress = Mathf.Clamp01(progress);
            bool changed = night != _night || !Mathf.Approximately(progress, _progress);
            _night = night;
            _progress = progress;

            string title = cycle + "일차 · " + (night ? "밤" : "낮");
            if (_title.text != title) _title.text = title;
            if (_big.text != bigText) _big.text = bigText;

            Color text = warn ? Color.Lerp(DayText, WarnText, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)) : (night ? NightText : DayText);
            _title.style.color = text;
            _big.style.color = warn ? text : Color.white;

            _barFill.style.backgroundColor = night ? NightSand : DaySand;
            _barFill.style.width = new Length(progress * 100f, LengthUnit.Percent);

            if (changed) _glass.MarkDirtyRepaint();
        }

        private void PaintHourglass(MeshGenerationContext ctx)
        {
            Painter2D p = ctx.painter2D;
            Color sand = _night ? NightSand : DaySand;

            // 위 유리: 위가 넓고 아래가 좁은 삼각형, 아래 유리: 반대
            const float cx = 20f, topY = 8f, neckY = 26f, botY = 45f, halfW = 14f;

            // 위쪽 모래 (남은 양 = 1 - progress)
            float topFill = 1f - _progress;
            if (topFill > 0.01f)
            {
                float y = neckY - topFill * (neckY - topY);
                float w = 1f + (halfW - 1f) * topFill;
                p.fillColor = sand;
                p.BeginPath();
                p.MoveTo(new Vector2(cx - w, y)); p.LineTo(new Vector2(cx + w, y));
                p.LineTo(new Vector2(cx + 1f, neckY)); p.LineTo(new Vector2(cx - 1f, neckY));
                p.ClosePath(); p.Fill();
            }

            // 아래쪽 모래 (쌓인 양 = progress)
            if (_progress > 0.01f)
            {
                float y = botY - _progress * (botY - neckY);
                float w = 1f + (halfW - 1f) * ((y - neckY) / (botY - neckY));
                p.fillColor = sand;
                p.BeginPath();
                p.MoveTo(new Vector2(cx - w, y)); p.LineTo(new Vector2(cx + w, y));
                p.LineTo(new Vector2(cx + halfW, botY)); p.LineTo(new Vector2(cx - halfW, botY));
                p.ClosePath(); p.Fill();
            }

            // 떨어지는 모래줄 (진행 중일 때만)
            if (_progress > 0.01f && _progress < 0.99f)
            {
                p.strokeColor = sand;
                p.lineWidth = 1.6f;
                p.BeginPath();
                p.MoveTo(new Vector2(cx, neckY)); p.LineTo(new Vector2(cx, botY - _progress * (botY - neckY)));
                p.Stroke();
            }

            // 유리 윤곽 + 위아래 받침
            p.strokeColor = GlassColor;
            p.lineWidth = 2.4f;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(cx - halfW, topY)); p.LineTo(new Vector2(cx + halfW, topY));
            p.LineTo(new Vector2(cx + 1.5f, neckY)); p.LineTo(new Vector2(cx + halfW, botY));
            p.LineTo(new Vector2(cx - halfW, botY)); p.LineTo(new Vector2(cx - 1.5f, neckY));
            p.ClosePath();
            p.Stroke();

            p.lineWidth = 4f;
            p.BeginPath(); p.MoveTo(new Vector2(cx - halfW - 4f, topY - 2f)); p.LineTo(new Vector2(cx + halfW + 4f, topY - 2f)); p.Stroke();
            p.BeginPath(); p.MoveTo(new Vector2(cx - halfW - 4f, botY + 2f)); p.LineTo(new Vector2(cx + halfW + 4f, botY + 2f)); p.Stroke();
        }
    }
}
