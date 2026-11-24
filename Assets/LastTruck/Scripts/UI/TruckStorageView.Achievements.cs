using System;
using System.Collections.Generic;
using CraftingSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 트럭 창 "도전과제" 탭 (UI Toolkit, 도감 탭 바로 아래): 성장 임무(ProgressMissions) 전체 목록.
    /// 위 = 달성 수와 전체 진행 막대 + 내 무기 강화 단계, 아래 = 두 줄 목록
    /// (달성 = 초록 체크 / 진행 중 = 금색 + 진행 수 / 아직 = 자물쇠, 오른쪽에 보상 아이콘).
    /// </summary>
    public partial class TruckStorageView
    {
        public class AchievementsModel
        {
            public List<Tab> tabs = new List<Tab>();
            public Action onClose;
        }

        private static readonly Color AchGold = new Color(1f, 0.8f, 0.34f, 1f);
        private static readonly Color AchGreen = new Color(0.45f, 1f, 0.62f, 1f);
        private static readonly Color AchCream = new Color(0.97f, 0.93f, 0.82f, 1f);
        private static readonly Color AchDim = new Color(0.62f, 0.58f, 0.52f, 1f);

        public void ShowAchievements(AchievementsModel m)
        {
            VisualElement frame = BeginWindow("achievements", "도전과제", m.onClose);
            if (frame == null) return;

            int count = ProgressMissions.Count, cur = Mathf.Min(ProgressMissions.CurrentIndex, ProgressMissions.Count);

            // 머리: 달성 수 + 전체 진행 막대 + 무기 강화 단계
            var summary = new VisualElement { pickingMode = PickingMode.Ignore };
            summary.style.flexDirection = FlexDirection.Row;
            summary.style.alignItems = Align.Center;
            summary.style.paddingLeft = 18; summary.style.paddingRight = 18;
            summary.style.paddingTop = 12; summary.style.paddingBottom = 12;
            summary.style.marginBottom = 10;
            UiToolkitSkin.Slice(summary, _skin.panelBrownDark, 0.35f, new Color(0.40f, 0.32f, 0.28f, 1f));

            VisualElement medal = AchMedal(54f, AchGold, "star", 30f);
            medal.style.marginRight = 14;
            summary.Add(medal);

            Label done = AchText("달성 " + cur + " / " + count, 26, AchCream);
            done.style.marginRight = 18;
            summary.Add(done);

            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.style.flexGrow = 1; track.style.height = 18;
            track.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            AchRadius(track, 9);
            AchBorder(track, 2, new Color(1f, 0.8f, 0.34f, 0.45f));
            track.style.overflow = Overflow.Hidden;
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.style.position = Position.Absolute;
            fill.style.left = 0; fill.style.top = 0; fill.style.bottom = 0;
            fill.style.width = new Length(count > 0 ? (float)cur / count * 100f : 0f, LengthUnit.Percent);
            fill.style.backgroundColor = AchGold;
            AchRadius(fill, 9);
            track.Add(fill);
            summary.Add(track);

            Label gear = AchText(PlayerGear.WeaponSummary, 19, new Color(0.72f, 0.86f, 1f, 1f));
            gear.style.marginLeft = 18;
            summary.Add(gear);
            frame.Add(summary);

            // 목록 (두 줄)
            _gridScroll = new ScrollView(ScrollViewMode.Vertical);
            _gridScroll.AddToClassList("tw-scroll");
            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.style.flexDirection = FlexDirection.Row;

            int perColumn = Mathf.CeilToInt(count / 2f);
            for (int c = 0; c < 2; c++)
            {
                var column = new VisualElement { pickingMode = PickingMode.Ignore };
                column.style.flexGrow = 1; column.style.flexBasis = 0;
                column.style.marginRight = c == 0 ? 14 : 0;
                for (int i = c * perColumn; i < Mathf.Min(count, (c + 1) * perColumn); i++)
                    column.Add(AchRow(i, ProgressMissions.GetInfo(i)));
                body.Add(column);
            }
            _gridScroll.Add(body);
            frame.Add(_gridScroll);

            _hoverLabel = MakeLabel("도전과제를 달성하면 보상이 전부 트럭 보관함에 들어와요. 보관함에서 꺼내 쓰세요 (무기 강화 키트는 손에 들고 왼쪽 클릭을 꾹).", "tw-hint");
            frame.Add(_hoverLabel);
            EndWindow(m.tabs);
        }

        private VisualElement AchRow(int index, ProgressMissions.Info info)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 62;
            row.style.marginBottom = 6;
            row.style.paddingLeft = 10; row.style.paddingRight = 12;
            row.style.paddingTop = 6; row.style.paddingBottom = 6;
            AchRadius(row, 10);

            Color accent, titleColor; string icon;
            if (info.done) { accent = AchGreen; icon = "check"; titleColor = AchCream; row.style.backgroundColor = new Color(0.2f, 0.5f, 0.3f, 0.25f); }
            else if (info.isCurrent) { accent = AchGold; icon = "star"; titleColor = AchGold; row.style.backgroundColor = new Color(1f, 0.8f, 0.34f, 0.18f); AchBorder(row, 2, new Color(1f, 0.8f, 0.34f, 0.75f)); }
            else { accent = new Color(0.55f, 0.52f, 0.48f, 1f); icon = "lock"; titleColor = AchDim; row.style.backgroundColor = new Color(0f, 0f, 0f, 0.22f); }

            VisualElement medal = AchMedal(42f, accent, icon, 23f);
            medal.style.marginRight = 12;
            row.Add(medal);

            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.style.flexGrow = 1; texts.style.flexShrink = 1;
            Label title = AchText((index + 1).ToString("00") + "  " + info.title, 21, titleColor);
            texts.Add(title);
            // 진행 중인 것만 할 일(힌트)을 보여 준다
            if (info.isCurrent)
            {
                Label hint = AchText(info.hint + "   (" + info.current + " / " + info.goal + ")", 15, AchCream);
                hint.style.whiteSpace = WhiteSpace.Normal;
                texts.Add(hint);
            }
            row.Add(texts);

            var rewards = new VisualElement { pickingMode = PickingMode.Ignore };
            rewards.style.flexDirection = FlexDirection.Row;
            rewards.style.alignItems = Align.Center;
            rewards.style.flexShrink = 0;
            rewards.style.opacity = info.done ? 0.45f : 1f;   // 이미 받은 보상은 흐리게
            if (info.rewards != null)
                foreach (ProgressMissions.RewardInfo r in info.rewards)
                {
                    if (r.item.icon != null)
                    {
                        var ic = new VisualElement { pickingMode = PickingMode.Ignore };
                        ic.style.width = 32; ic.style.height = 32;
                        ic.style.marginLeft = 8; ic.style.marginRight = 3;
                        ic.style.backgroundImage = new StyleBackground(r.item.icon);
                        rewards.Add(ic);
                        rewards.Add(AchText("x" + r.count, 16, AchCream));
                    }
                    else
                    {
                        Label name = AchText(r.item.itemName + " x" + r.count, 16, AchCream);
                        name.style.marginLeft = 8;
                        rewards.Add(name);
                    }
                }
            row.Add(rewards);
            return row;
        }

        private Label AchText(string text, int size, Color color)
        {
            Label l = MakeLabel(text, "");
            l.style.fontSize = size;
            l.style.color = color;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            return l;
        }

        private static VisualElement AchMedal(float size, Color color, string iconName, float iconSize)
        {
            var medal = new VisualElement { pickingMode = PickingMode.Ignore };
            medal.style.width = size; medal.style.height = size;
            medal.style.flexShrink = 0;
            medal.style.alignItems = Align.Center;
            medal.style.justifyContent = Justify.Center;
            medal.style.backgroundColor = new Color(color.r * 0.22f, color.g * 0.2f, color.b * 0.16f, 1f);
            AchRadius(medal, size * 0.5f);
            AchBorder(medal, Mathf.Max(2f, size * 0.07f), color);
            Sprite sprite = UiIcons.Get(iconName);
            if (sprite != null)
            {
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.style.width = iconSize; icon.style.height = iconSize;
                icon.style.backgroundImage = new StyleBackground(sprite);
                icon.style.unityBackgroundImageTintColor = color;
                medal.Add(icon);
            }
            return medal;
        }

        private static void AchRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        private static void AchBorder(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
        }
    }
}
