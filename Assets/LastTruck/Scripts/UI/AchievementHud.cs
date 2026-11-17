using System.Collections.Generic;
using CraftingSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 도전과제 화면 (성장 임무 ProgressMissions를 도전과제처럼 보여 준다). LastTruckHud가 만들고 매 프레임 Tick을 부른다.
    ///
    ///  [달성 알림] 위쪽 가운데: 달성하는 순간 금테 배너가 위에서 튀어 내려오고, 받은 보상을 아이콘으로 보여 준 뒤 사라진다.
    ///              연달아 달성하면 차례로 하나씩 나온다.
    ///  전체 목록은 트럭 창의 "도전과제" 탭(도감 아래)에 있다 (TruckStorageView.ShowAchievements).
    /// </summary>
    public class AchievementHud
    {
        private static readonly Color Gold = new Color(1f, 0.8f, 0.34f, 1f);
        private static readonly Color Green = new Color(0.45f, 1f, 0.62f, 1f);
        private static readonly Color Cream = new Color(0.97f, 0.93f, 0.82f, 1f);
        private static readonly Color Outline = new Color(0.15f, 0.09f, 0.03f, 0.95f);

        private readonly StyleFontDefinition _font;
        private readonly Sprite _panelSprite;

        // 달성 알림
        private VisualElement _popup, _popupRewards, _popupShine;
        private Label _popupTitle;
        private readonly Queue<int> _queue = new Queue<int>();
        private float _popupTime = -1f;   // 지금 알림이 시작된 뒤 지난 시간 (-1 = 없음)
        private const float PopIn = 0.4f, PopHold = 4.2f, PopOut = 0.45f;

        public AchievementHud(StyleFontDefinition font, Sprite panelSprite)
        {
            _font = font;
            _panelSprite = panelSprite;
        }

        public void Build(VisualElement root)
        {
            BuildPopup(root);
            ProgressMissions.Completed += OnCompleted;
        }

        private void OnCompleted(int index) { _queue.Enqueue(index); }

        // ================================================================ 달성 알림

        private void BuildPopup(VisualElement root)
        {
            // 가운데 정렬용 틀 (화면 너비 전체, 위쪽)
            var holder = new VisualElement { pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.left = 0; holder.style.right = 0; holder.style.top = 150;
            holder.style.alignItems = Align.Center;

            _popup = new VisualElement { pickingMode = PickingMode.Ignore };
            _popup.style.width = 640;
            _popup.style.flexDirection = FlexDirection.Row;
            _popup.style.alignItems = Align.Center;
            _popup.style.paddingLeft = 22; _popup.style.paddingRight = 26;
            _popup.style.paddingTop = 16; _popup.style.paddingBottom = 16;
            _popup.style.backgroundColor = new Color(0.1f, 0.075f, 0.05f, 0.96f);
            Radius(_popup, 18);
            Border(_popup, 4, Gold);
            _popup.style.overflow = Overflow.Hidden;

            // 배너 위를 가로지르는 빛줄기
            _popupShine = new VisualElement { pickingMode = PickingMode.Ignore };
            _popupShine.style.position = Position.Absolute;
            _popupShine.style.top = -20; _popupShine.style.bottom = -20;
            _popupShine.style.width = 90;
            _popupShine.style.backgroundColor = new Color(1f, 0.95f, 0.75f, 0.22f);
            _popupShine.style.rotate = new Rotate(18f);
            _popup.Add(_popupShine);

            VisualElement medal = Medal(96f, Green, "check", 54f);
            medal.style.marginRight = 20;
            _popup.Add(medal);

            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.style.flexGrow = 1; col.style.flexShrink = 1;
            col.Add(Text("도전과제 달성!", 21, Gold));
            _popupTitle = Text("", 33, Cream);
            _popupTitle.style.whiteSpace = WhiteSpace.Normal;
            _popupTitle.style.marginBottom = 6;
            col.Add(_popupTitle);

            var rewardRow = Row();
            Label tag = Text("보상", 18, Gold);
            tag.style.marginRight = 10;
            rewardRow.Add(tag);
            _popupRewards = Row();
            _popupRewards.style.flexWrap = Wrap.Wrap;
            _popupRewards.style.flexShrink = 1;
            rewardRow.Add(_popupRewards);
            col.Add(rewardRow);
            _popup.Add(col);

            _popup.style.opacity = 0f;
            _popup.style.display = DisplayStyle.None;
            holder.Add(_popup);
            root.Add(holder);
        }

        private void StartPopup(int index)
        {
            ProgressMissions.Info info = ProgressMissions.GetInfo(index);
            _popupTitle.text = info.title;
            FillRewards(_popupRewards, info.rewards, 40f, 20, true);
            _popupTime = 0f;
            _popup.style.display = DisplayStyle.Flex;
        }

        private void TickPopup(float dt)
        {
            if (_popupTime < 0f)
            {
                if (_queue.Count > 0) StartPopup(_queue.Dequeue());
                return;
            }

            _popupTime += dt;
            float total = PopIn + PopHold + PopOut;
            if (_popupTime >= total)
            {
                _popupTime = -1f;
                _popup.style.opacity = 0f;
                _popup.style.display = DisplayStyle.None;
                return;
            }

            float y, scale, alpha;
            if (_popupTime < PopIn)
            {
                float t = _popupTime / PopIn;
                float e = EaseOutBack(t);
                y = Mathf.LerpUnclamped(-90f, 0f, e);
                scale = Mathf.LerpUnclamped(0.7f, 1f, e);
                alpha = Mathf.Clamp01(t * 2.5f);
            }
            else if (_popupTime < PopIn + PopHold)
            {
                y = 0f; scale = 1f; alpha = 1f;
            }
            else
            {
                float t = (_popupTime - PopIn - PopHold) / PopOut;
                y = Mathf.Lerp(0f, -40f, t * t);
                scale = 1f;
                alpha = 1f - t;
            }
            _popup.style.translate = new Translate(0f, y);
            _popup.style.scale = new Scale(new Vector3(scale, scale, 1f));
            _popup.style.opacity = alpha;

            // 빛줄기: 나타난 직후 왼쪽에서 오른쪽으로 한 번 지나간다
            float sweep = Mathf.Clamp01((_popupTime - PopIn * 0.6f) / 0.9f);
            _popupShine.style.left = Mathf.Lerp(-140f, 700f, sweep);
        }

        // ================================================================ 갱신

        public void Tick()
        {
            if (_popup == null) return;
            if (!ProgressMissions.Active || Networking.NetworkGameState.IsGameOver)
            {
                // 게임 화면이 아니면 알림을 지운다
                _queue.Clear();
                if (_popupTime >= 0f) { _popupTime = -1f; _popup.style.opacity = 0f; _popup.style.display = DisplayStyle.None; }
                return;
            }
            TickPopup(Time.unscaledDeltaTime);
        }

        // ================================================================ 부품

        /// <summary>보상 아이콘들 (아이콘 + x개수, withNames면 이름도).</summary>
        private void FillRewards(VisualElement row, ProgressMissions.RewardInfo[] rewards, float iconSize, int fontSize, bool withNames)
        {
            row.Clear();
            if (rewards == null) return;
            foreach (ProgressMissions.RewardInfo r in rewards)
            {
                var chip = Row();
                chip.style.marginRight = 10;
                if (r.item.icon != null)
                {
                    var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                    icon.style.width = iconSize; icon.style.height = iconSize;
                    icon.style.backgroundImage = new StyleBackground(r.item.icon);
                    icon.style.marginRight = 4;
                    icon.style.flexShrink = 0;
                    chip.Add(icon);
                }
                string text = withNames || r.item.icon == null ? r.item.itemName + " x" + r.count : "x" + r.count;
                chip.Add(Text(text, fontSize, Cream));
                row.Add(chip);
            }
        }

        /// <summary>동그란 메달: 색 테두리 + 어두운 바탕 + 가운데 그림.</summary>
        private VisualElement Medal(float size, Color color, string iconName, float iconSize)
        {
            var medal = new VisualElement { pickingMode = PickingMode.Ignore };
            medal.style.width = size; medal.style.height = size;
            medal.style.flexShrink = 0;
            medal.style.alignItems = Align.Center;
            medal.style.justifyContent = Justify.Center;
            medal.style.backgroundColor = new Color(color.r * 0.22f, color.g * 0.2f, color.b * 0.16f, 1f);
            Radius(medal, size * 0.5f);
            Border(medal, Mathf.Max(2f, size * 0.07f), color);

            Sprite sprite = UiIcons.Get(iconName);
            if (sprite != null)
            {
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.style.width = iconSize; icon.style.height = iconSize;
                icon.style.backgroundImage = new StyleBackground(sprite);
                icon.style.unityBackgroundImageTintColor = color;
                medal.Add(icon);
            }
            else
            {
                medal.Add(Text(iconName == "check" ? "V" : iconName == "lock" ? "?" : "★", Mathf.RoundToInt(iconSize * 0.8f), color));
            }
            return medal;
        }

        private Label Text(string text, int size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.unityFontDefinition = _font;
            l.style.fontSize = size;
            l.style.color = color;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.unityTextOutlineWidth = Mathf.Clamp(size * 0.05f, 0.8f, 1.6f);
            l.style.unityTextOutlineColor = Outline;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            return l;
        }

        private static VisualElement Row()
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems = Align.Center;
            return r;
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

        private static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
