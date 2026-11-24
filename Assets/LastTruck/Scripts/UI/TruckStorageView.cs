using System;
using System.Collections.Generic;
using CraftingSystem;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// Tab 트럭 거점 창의 "보관함" 탭 (UI Toolkit). 값과 동작은 GameUIController가 Model로 넘겨 준다.
    ///  - 창은 왼쪽에 두어 오른쪽 HUD(미니맵/시계/스태미나/인벤토리 핫바)를 가리지 않는다.
    ///  - 오른쪽 책갈피 탭(보관함/제작/가공/도감/강화), 분류 칩, 정렬, 보관함 칸, 꺼낼 개수 고르기.
    /// 스킨/패널 설정이 없으면 만들지 않는다 (그러면 예전 uGUI 창이 그대로 쓰인다).
    /// </summary>
    public partial class TruckStorageView : MonoBehaviour
    {
        private const string PanelSettingsPath = "UI/LastTruckHudPanelSettings";
        private const string UssPath = "UI/LastTruckWindow";

        public class Chip
        {
            public Sprite icon;
            public string text;
            public bool selected;
            public Color accent;
            public string tooltip;
            public Action onClick;
        }

        public class Slot
        {
            public ItemData item;
            public int count;
            public float durability01 = 1f;
            public Action onClick;
        }

        public class Tab
        {
            public Sprite icon;
            public string label;
            public Color color;
            public bool selected;
            public bool locked;
            public Action onClick;
        }

        public class Model
        {
            public string title;
            public List<Tab> tabs = new List<Tab>();
            public List<Chip> categories = new List<Chip>();
            public List<Chip> sorts = new List<Chip>();
            public List<Slot> slots = new List<Slot>();
            public string emptyText;
            public Action onClose;
            public string mode = "storage";   // 같은 화면을 다시 그릴 때 스크롤을 이어 가는 기준 (가죽 파우치는 "pouch")
            public string hint;               // 아래 안내 줄 (비우면 트럭 보관함 안내)
            public bool compact;              // 작은 창 (가죽 파우치)

            // 꺼낼 개수 고르기 (item이 null이면 숨김)
            public ItemData withdrawItem;
            public int withdrawMax;
            public int withdrawDefault = 1;
            public Action<int> onWithdraw;
            public Action onWithdrawCancel;
            // 과학자 광물 변환 버튼 (onConvert가 null이면 버튼을 그리지 않는다)
            public string convertLabel;
            public Action onConvert;
            public bool convertEnabled = true;      // false면 회색(재료 부족/밤). 눌러도 이유만 알려준다
            public string convertDisabledReason;    // 비활성일 때 아래 안내 줄에 보여줄 이유

            // 칸 위 동작 줄 (예: 보관함 "재료 모두 넣기", 배낭 "가죽 덧대기"). onAction이 null이면 줄을 그리지 않는다
            public string actionInfo;               // 왼쪽 설명 (예: "배낭 4/6칸")
            public string actionLabel;
            public Action onAction;
            public bool actionEnabled = true;
            public string actionHint;               // 버튼에 마우스를 올리면 아래 안내 줄에
            public string action2Label;             // 두 번째 버튼 (없으면 null)
            public Action onAction2;
            public string action2Hint;
        }

        // ---- 제작 탭 ----

        public class CraftCard
        {
            public ItemData item;
            public bool canCraft;
            public bool selected;
            public bool installed;
            public bool discounted;   // 고유 능력(대장장이/요리사)으로 재료가 줄어드는 레시피
            public Action onClick;
        }

        public class CraftIngredient
        {
            public ItemData item;
            public int have;
            public int need;
            public int originalNeed;   // 능력으로 줄었으면 원래 개수 (같으면 need와 같다)
        }

        public class CraftModel
        {
            public string title;
            public List<Tab> tabs = new List<Tab>();
            public List<Chip> categories = new List<Chip>();
            public Color accent = Color.white;
            public List<CraftCard> cards = new List<CraftCard>();
            public Action onClose;

            // 오른쪽 상세 (item이 null이면 "고르세요" 안내)
            public ItemData item;
            public string subtitle;
            public List<CraftIngredient> ingredients = new List<CraftIngredient>();
            public bool canCraft;
            public string craftLabel;
            public Action onCraft;
            public Action onCodex;
            public string discountBadge;   // 고유 능력 할인 배지 문구 (비어 있으면 표시 안 함)
        }

        // ---- 고유 능력 할인 표시 (대장장이 화로 색) ----
        private static readonly Color ForgeOrange = new Color(1f, 0.52f, 0.16f, 1f);
        private static readonly Color ForgeGold = new Color(1f, 0.84f, 0.29f, 1f);

        private static void SetBorder(VisualElement e, Color color, float width, float radius)
        {
            e.style.borderTopWidth = width; e.style.borderBottomWidth = width; e.style.borderLeftWidth = width; e.style.borderRightWidth = width;
            SetBorderColor(e, color);
            e.style.borderTopLeftRadius = radius; e.style.borderTopRightRadius = radius;
            e.style.borderBottomLeftRadius = radius; e.style.borderBottomRightRadius = radius;
        }

        private static void SetBorderColor(VisualElement e, Color color)
        {
            e.style.borderTopColor = color; e.style.borderBottomColor = color; e.style.borderLeftColor = color; e.style.borderRightColor = color;
        }

        /// <summary>작은 주황 알약 모양 표시 ("할인", "-1").</summary>
        private Label MakeDiscountPill(string text)
        {
            Label pill = MakeLabel(text, "tw-ability-pill");
            pill.pickingMode = PickingMode.Ignore;
            pill.style.backgroundColor = ForgeOrange;
            pill.style.color = Color.white;
            pill.style.fontSize = 12;
            pill.style.unityFontStyleAndWeight = FontStyle.Bold;
            pill.style.unityTextAlign = TextAnchor.MiddleCenter;
            pill.style.paddingLeft = 6; pill.style.paddingRight = 6; pill.style.paddingTop = 1; pill.style.paddingBottom = 1;
            pill.style.marginLeft = 6;
            SetBorder(pill, ForgeGold, 1f, 8f);
            return pill;
        }

        /// <summary>상세 위쪽 배지: 망치 아이콘 + 능력 이름 + 절약한 개수. 테두리가 화로처럼 은은하게 깜빡인다.</summary>
        private VisualElement MakeAbilityBanner(string text)
        {
            var banner = new VisualElement { pickingMode = PickingMode.Ignore };
            banner.AddToClassList("tw-ability-banner");
            banner.style.flexDirection = FlexDirection.Row;
            banner.style.alignItems = Align.Center;
            banner.style.marginTop = 6; banner.style.marginBottom = 6;
            banner.style.paddingLeft = 10; banner.style.paddingRight = 10; banner.style.paddingTop = 6; banner.style.paddingBottom = 6;
            banner.style.backgroundColor = new Color(ForgeOrange.r, ForgeOrange.g, ForgeOrange.b, 0.2f);
            SetBorder(banner, ForgeOrange, 2f, 8f);

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = 22; icon.style.height = 22; icon.style.marginRight = 8; icon.style.flexShrink = 0;
            Sprite hammer = UiIcons.Get("hammer");
            if (hammer != null)
            {
                icon.style.backgroundImage = new StyleBackground(hammer);
                icon.style.unityBackgroundImageTintColor = ForgeGold;
                banner.Add(icon);
            }

            Label label = MakeLabel(text, "tw-ability-banner-text");
            label.style.color = ForgeGold;
            label.style.fontSize = 15;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.flexShrink = 1;
            label.style.whiteSpace = WhiteSpace.Normal;
            banner.Add(label);

            // 은은한 깜빡임 (창이 닫히면 요소가 패널에서 빠지면서 같이 멈춘다)
            banner.schedule.Execute(() =>
            {
                float t = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 4f);
                SetBorderColor(banner, Color.Lerp(ForgeOrange, ForgeGold, t));
                banner.style.backgroundColor = new Color(ForgeOrange.r, ForgeOrange.g, ForgeOrange.b, 0.12f + 0.14f * t);
            }).Every(50);
            return banner;
        }

        private VisualElement _root, _window;
        private ScrollView _gridScroll;
        private LastTruckUiSkin _skin;
        private StyleFontDefinition _font;
        private FontAsset _ownFont;
        private Label _hoverLabel;
        private float _scrollY;
        private string _mode;
        public string Mode => IsVisible ? _mode : null;

        public bool IsVisible => _window != null && _window.style.display == DisplayStyle.Flex;

        public static TruckStorageView Create()
        {
            LastTruckUiSkin skin = LastTruckUiSkin.Get();
            PanelSettings settings = Resources.Load<PanelSettings>(PanelSettingsPath);
            StyleSheet uss = Resources.Load<StyleSheet>(UssPath);
            if (skin == null || settings == null || uss == null || skin.panelBrownDark == null)
                return null;

            var go = new GameObject("UIDocument_TruckStorage");
            DontDestroyOnLoad(go);
            var view = go.AddComponent<TruckStorageView>();
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            doc.sortingOrder = 20;   // HUD 위, 인벤토리 핫바(5)보다도 위
            view._skin = skin;
            view.Init(doc.rootVisualElement, uss);
            return view;
        }

        private void OnDestroy()
        {
            if (_ownFont != null) Destroy(_ownFont);
        }

        private void Init(VisualElement root, StyleSheet uss)
        {
            _root = root;
            root.styleSheets.Add(uss);
            root.AddToClassList("lt-root");
            root.pickingMode = PickingMode.Ignore;

            if (LastTruckHud.SharedFont.HasValue)
                _font = LastTruckHud.SharedFont.Value;
            else if (_skin.font != null)
            {
                _ownFont = FontAsset.CreateFontAsset(_skin.font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                _font = _ownFont != null ? new StyleFontDefinition(FontDefinition.FromSDFFont(_ownFont)) : new StyleFontDefinition(_skin.font);
            }
        }

        public void Hide()
        {
            ProcessDiorama.Hide();
            if (_window != null) _window.style.display = DisplayStyle.None;
        }

        /// <summary>창을 (다시) 그린다. 스크롤 위치는 유지한다.</summary>
        public void Show(Model m)
        {
            VisualElement frame = BeginWindow(m.mode ?? "storage", m.title, m.onClose);
            if (frame == null) return;
            if (m.compact) _window.AddToClassList("tw-window--compact");

            // 도구 줄: 분류 칩 ........ 정렬 칩
            var toolbar = Row("tw-toolbar");
            foreach (Chip c in m.categories) toolbar.Add(MakeChip(c, "tw-chip"));
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            toolbar.Add(spacer);
            foreach (Chip c in m.sorts) toolbar.Add(MakeChip(c, "tw-chip tw-chip--sort"));
            if (m.categories.Count > 0 || m.sorts.Count > 0) frame.Add(toolbar);   // 가죽 파우치처럼 분류가 없으면 빈 줄을 두지 않는다

            // 동작 줄
            if (m.onAction != null || m.onAction2 != null)
            {
                var act = Row("tw-toolbar");
                if (!string.IsNullOrEmpty(m.actionInfo))
                {
                    Label info = MakeLabel(m.actionInfo, "tw-hint");
                    info.style.marginTop = 0; info.style.marginBottom = 0;
                    info.style.unityTextAlign = TextAnchor.MiddleLeft;
                    act.Add(info);
                }
                var actSpacer = new VisualElement { pickingMode = PickingMode.Ignore };
                actSpacer.style.flexGrow = 1;
                act.Add(actSpacer);
                if (m.onAction2 != null)
                {
                    Button b2 = SmallButton(m.action2Label ?? "", () => m.onAction2?.Invoke());
                    if (!string.IsNullOrEmpty(m.action2Hint)) b2.RegisterCallback<PointerEnterEvent>(_ => SetHint(m.action2Hint));
                    act.Add(b2);
                }
                if (m.onAction != null)
                {
                    Button b = SmallButton(m.actionLabel ?? "", () => m.onAction?.Invoke());
                    if (m.actionEnabled) b.AddToClassList("tw-btn--ok");
                    else b.style.opacity = 0.55f;
                    if (!string.IsNullOrEmpty(m.actionHint)) b.RegisterCallback<PointerEnterEvent>(_ => SetHint(m.actionHint));
                    act.Add(b);
                }
                frame.Add(act);
            }

            // 꺼내기
            if (m.withdrawItem != null)
                frame.Add(MakeWithdraw(m));

            // 칸
            var body = new VisualElement();
            body.AddToClassList("tw-body");
            UiToolkitSkin.Slice(body, _skin.panelBrownDark, 0.35f, new Color(0.40f, 0.32f, 0.28f, 1f));
            _gridScroll = new ScrollView(ScrollViewMode.Vertical);
            _gridScroll.AddToClassList("tw-scroll");
            _gridScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            var grid = new VisualElement();
            grid.AddToClassList("tw-grid");
            foreach (Slot s in m.slots) grid.Add(MakeSlot(s));
            if (m.slots.Count == 0)
                grid.Add(MakeLabel(m.emptyText ?? "보관함이 비어 있어요", "tw-empty"));
            _gridScroll.Add(grid);
            body.Add(_gridScroll);
            frame.Add(body);

            // 아래: 칸에 마우스를 올리면 이름
            _hoverLabel = MakeLabel(m.hint ?? "칸을 누르면 인벤토리로 꺼내요. 인벤토리 칸을 누르면 보관함에 넣어요.", "tw-hint");
            frame.Add(_hoverLabel);

            EndWindow(m.tabs);
        }

        /// <summary>창 틀(제목 + 닫기)을 새로 만든다. 같은 화면(mode)을 다시 그리면 스크롤 위치를 이어 간다.</summary>
        private VisualElement BeginWindow(string mode, string title, Action onClose)
        {
            ProcessDiorama.Hide();   // 가공 화면이 아니면 3D 미니 장면 카메라를 끈다 (가공 화면이면 바로 다시 켠다)
            if (_root == null) return null;
            if (_gridScroll != null && _gridScroll.panel != null) _scrollY = _gridScroll.scrollOffset.y;
            if (mode != _mode) _scrollY = 0f;
            _mode = mode;
            _gridScroll = null;
            if (_window != null) _window.RemoveFromHierarchy();

            _window = new VisualElement();
            _window.AddToClassList("tw-window");
            _root.Add(_window);

            var frame = new VisualElement();
            frame.AddToClassList("tw-frame");
            UiToolkitSkin.Slice(frame, _skin.panelBrownDark, 0.5f, new Color(0.62f, 0.51f, 0.45f, 1f));
            _window.Add(frame);

            // 머리: 제목 + 닫기
            var header = Row("tw-header");
            header.Add(MakeLabel(title, "tw-title"));
            var close = new Button(() => onClose?.Invoke()) { text = "" };
            close.AddToClassList("tw-close");
            UiToolkitSkin.Slice(close, _skin.buttonRed != null ? _skin.buttonRed : _skin.buttonBrown, 0.5f, new Color(0.95f, 0.85f, 0.85f, 1f));
            Sprite x = UiIcons.Get("close");
            if (x != null)
            {
                var xi = new VisualElement { pickingMode = PickingMode.Ignore };
                xi.AddToClassList("tw-close-icon");
                xi.style.backgroundImage = new StyleBackground(x);
                close.Add(xi);
            }
            else close.Add(MakeLabel("X", "tw-btn-text"));
            header.Add(close);
            frame.Add(header);
            return frame;
        }

        /// <summary>오른쪽 책갈피 탭을 달고 창을 보인다.</summary>
        private void EndWindow(List<Tab> tabList)
        {
            var tabs = new VisualElement { pickingMode = PickingMode.Ignore };
            tabs.AddToClassList("tw-tabs");
            foreach (Tab t in tabList) tabs.Add(MakeTab(t));
            _window.Add(tabs);

            _window.style.display = DisplayStyle.Flex;
            float keep = _scrollY;
            ScrollView scroll = _gridScroll;
            if (scroll != null)
                scroll.schedule.Execute(() => { if (scroll.panel != null) scroll.scrollOffset = new Vector2(0f, keep); });
        }

        /// <summary>제작 탭: 위 분류 칩 → 왼쪽 만들 수 있는 아이템 카드 → 오른쪽 필요 재료와 제작 버튼.</summary>
        public void ShowCraft(CraftModel m)
        {
            VisualElement frame = BeginWindow("craft", m.title, m.onClose);
            if (frame == null) return;

            var toolbar = Row("tw-toolbar");
            foreach (Chip c in m.categories) toolbar.Add(MakeChip(c, "tw-chip tw-chip--cat"));
            frame.Add(toolbar);

            var body = Row("tw-craft-body");
            frame.Add(body);

            // 왼쪽: 카드
            var listPanel = new VisualElement();
            listPanel.AddToClassList("tw-craft-list");
            UiToolkitSkin.Slice(listPanel, _skin.panelBrownDark, 0.35f, new Color(0.40f, 0.32f, 0.28f, 1f));
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("tw-craft-line");
            line.style.backgroundColor = m.accent;
            listPanel.Add(line);
            _gridScroll = new ScrollView(ScrollViewMode.Vertical);
            _gridScroll.AddToClassList("tw-scroll");
            _gridScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            var grid = new VisualElement();
            grid.AddToClassList("tw-grid");
            foreach (CraftCard c in m.cards) grid.Add(MakeCraftCard(c, m.accent));
            if (m.cards.Count == 0) grid.Add(MakeLabel("만들 수 있는 아이템이 없어요", "tw-empty"));
            _gridScroll.Add(grid);
            listPanel.Add(_gridScroll);
            body.Add(listPanel);

            // 오른쪽: 상세
            var detail = new VisualElement();
            detail.AddToClassList("tw-craft-detail");
            UiToolkitSkin.Slice(detail, _skin.panelBrown, 0.4f, new Color(0.55f, 0.45f, 0.38f, 1f));
            body.Add(detail);
            BuildCraftDetail(detail, m);

            _hoverLabel = MakeLabel("트럭 보관함과 내 인벤토리에 있는 재료로 만들어요. 초록 점 = 지금 만들 수 있음", "tw-hint");
            frame.Add(_hoverLabel);

            EndWindow(m.tabs);
        }

        private VisualElement MakeCraftCard(CraftCard c, Color accent)
        {
            var b = new Button(() => c.onClick?.Invoke()) { text = "" };
            b.AddToClassList("tw-card");
            if (c.selected) b.AddToClassList("tw-card--on");
            if (!c.canCraft) b.AddToClassList("tw-card--off");
            UiToolkitSkin.Slice(b, _skin.panelBrownDark, 0.35f, c.canCraft ? UiSkinUtil.SlotFilled : UiSkinUtil.SlotNormal);
            Color border = c.selected ? new Color(1f, 0.82f, 0.32f) : (c.canCraft ? new Color(accent.r, accent.g, accent.b, 0.7f) : new Color(1f, 1f, 1f, 0.08f));
            b.style.borderTopColor = border; b.style.borderBottomColor = border; b.style.borderLeftColor = border; b.style.borderRightColor = border;

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("tw-card-icon");
            if (c.item != null && c.item.icon != null) icon.style.backgroundImage = new StyleBackground(c.item.icon);
            b.Add(icon);
            b.Add(MakeLabel(c.item != null ? c.item.itemName : "?", "tw-card-name"));
            if (c.canCraft)
            {
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList("tw-card-dot");
                b.Add(dot);
            }
            if (c.installed) b.Add(MakeLabel("설치됨", "tw-card-tag"));
            if (c.discounted)
            {
                // 고유 능력 할인: 카드 왼쪽 위에 주황 "할인" 표시 + 테두리 금색
                Label tag = MakeDiscountPill("할인");
                tag.style.position = Position.Absolute;
                tag.style.top = 3; tag.style.left = 3; tag.style.marginLeft = 0;
                b.Add(tag);
                if (!c.selected) SetBorderColor(b, new Color(ForgeGold.r, ForgeGold.g, ForgeGold.b, c.canCraft ? 0.9f : 0.45f));
            }
            string hint = c.item == null ? "" : c.item.itemName + (c.installed ? "  (이미 설치됨)" : c.canCraft ? "  (지금 만들 수 있어요)" : "  (재료가 부족해요)")
                          + (c.discounted ? "  · 고유 능력 재료 할인" : "");
            b.RegisterCallback<PointerEnterEvent>(_ => SetHint(hint));
            return b;
        }

        private void BuildCraftDetail(VisualElement d, CraftModel m)
        {
            if (m.item == null)
            {
                d.Add(MakeLabel("왼쪽에서 만들 아이템을 고르세요", "tw-detail-empty"));
                return;
            }

            var head = Row("tw-detail-head");
            var big = new VisualElement { pickingMode = PickingMode.Ignore };
            big.AddToClassList("tw-detail-icon");
            UiToolkitSkin.Slice(big, _skin.panelBrownDark, 0.35f, new Color(0.45f, 0.36f, 0.31f, 1f));
            var bigIcon = new VisualElement { pickingMode = PickingMode.Ignore };
            bigIcon.AddToClassList("tw-detail-icon-img");
            if (m.item.icon != null) bigIcon.style.backgroundImage = new StyleBackground(m.item.icon);
            big.Add(bigIcon);
            head.Add(big);
            var titles = new VisualElement { pickingMode = PickingMode.Ignore };
            titles.Add(MakeLabel(m.item.itemName, "tw-detail-title"));
            titles.Add(MakeLabel(m.subtitle ?? "", "tw-detail-sub"));
            head.Add(titles);
            d.Add(head);

            if (!string.IsNullOrEmpty(m.discountBadge))
                d.Add(MakeAbilityBanner(m.discountBadge));

            d.Add(MakeLabel("필요 재료", "tw-detail-need"));
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("tw-ing-list");
            foreach (CraftIngredient ing in m.ingredients)
            {
                var row = Row("tw-ing");
                bool enough = ing.have >= ing.need;
                var ic = new VisualElement { pickingMode = PickingMode.Ignore };
                ic.AddToClassList("tw-ing-icon");
                if (ing.item.icon != null) ic.style.backgroundImage = new StyleBackground(ing.item.icon);
                row.Add(ic);
                row.Add(MakeLabel(ing.item.itemName, "tw-ing-name"));

                bool discounted = ing.originalNeed > ing.need;
                if (discounted)
                {
                    // 할인된 재료 줄: 금빛 바탕 + 왼쪽 주황 띠, 원래 개수는 취소선, 줄어든 개수는 금색 굵게, 오른쪽에 "-N"
                    row.style.backgroundColor = new Color(ForgeGold.r, ForgeGold.g, ForgeGold.b, 0.14f);
                    row.style.borderLeftWidth = 4;
                    row.style.borderLeftColor = ForgeOrange;
                    row.style.borderTopRightRadius = 6; row.style.borderBottomRightRadius = 6;
                    string original = ing.originalNeed.ToString(), reduced = ing.need.ToString();
                    row.RegisterCallback<PointerEnterEvent>(_ => SetHint($"고유 능력 할인: {original}개 → {reduced}개"));

                    // 개수: "보유 / 원래개수(주황 사선으로 그음) 줄어든개수"
                    string countCls = enough ? "tw-ing-count tw-ing-count--ok" : "tw-ing-count tw-ing-count--short";
                    var countRow = new VisualElement { pickingMode = PickingMode.Ignore };
                    countRow.style.flexDirection = FlexDirection.Row;
                    countRow.style.alignItems = Align.Center;
                    countRow.style.flexShrink = 0;
                    countRow.Add(MakeLabel(ing.have + " / ", countCls));

                    var originalBox = new VisualElement { pickingMode = PickingMode.Ignore };
                    originalBox.style.justifyContent = Justify.Center;
                    originalBox.style.marginRight = 6;
                    Label originalLabel = MakeLabel(original, countCls);
                    originalLabel.style.color = new Color(0.64f, 0.59f, 0.54f, 1f);
                    originalBox.Add(originalLabel);
                    Label slash = MakeLabel("/", "tw-ing-slash");   // 원래 개수 위에 사선
                    slash.style.position = Position.Absolute;
                    slash.style.left = 0; slash.style.right = 0; slash.style.top = -2; slash.style.bottom = 0;
                    slash.style.unityTextAlign = TextAnchor.MiddleCenter;
                    slash.style.color = ForgeOrange;
                    slash.style.fontSize = 24;
                    slash.style.unityFontStyleAndWeight = FontStyle.Bold;
                    originalBox.Add(slash);
                    countRow.Add(originalBox);

                    Label reducedLabel = MakeLabel(reduced, countCls);
                    reducedLabel.style.color = ForgeGold;
                    reducedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    countRow.Add(reducedLabel);
                    row.Add(countRow);
                }
                else
                {
                    string count = ing.have + " / " + ing.need;
                    row.Add(MakeLabel(count, enough ? "tw-ing-count tw-ing-count--ok" : "tw-ing-count tw-ing-count--short"));
                }
                if (!enough && OnMissingItem != null)
                {
                    row.Add(MakeLabel("도감 ›", "tw-missing-tag"));
                    MakeMissingLink(row, ing.item, true);
                }
                list.Add(row);
            }
            d.Add(list);

            var buttons = Row("tw-detail-buttons");
            Button codex = SmallButton("제작 링크 보기", () => m.onCodex?.Invoke());
            codex.AddToClassList("tw-btn--wide");
            buttons.Add(codex);
            var make = new Button(() => { if (m.canCraft) m.onCraft?.Invoke(); }) { text = "" };
            make.AddToClassList("tw-make");
            make.EnableInClassList("tw-make--off", !m.canCraft);
            bool discountedCraft = !string.IsNullOrEmpty(m.discountBadge);
            UiToolkitSkin.Slice(make, m.canCraft ? _skin.buttonBrown : _skin.buttonGrey, 0.5f,
                m.canCraft ? (discountedCraft ? ForgeGold : new Color(0.50f, 0.88f, 0.56f, 1f)) : new Color(0.55f, 0.55f, 0.60f, 1f));
            make.Add(MakeLabel(m.craftLabel ?? "제작하기", "tw-make-text"));
            buttons.Add(make);
            d.Add(buttons);
        }

        // ---- 가공 탭 ----

        private class ProcSlotEl
        {
            public VisualElement fill; public Label timer; public Action<float> onProgress;
            public VisualElement bigFill; public Label bigPercent, bigTimer;   // 큰 가공 화면 (고른 칸만)
        }
        private readonly List<ProcSlotEl> _procSlots = new List<ProcSlotEl>();

        /// <summary>가공 탭: 위 설비 칩 → 왼쪽 가공할 재료 카드 → 오른쪽 가공 칸(타이머/받기) + 칸 늘리기.</summary>
        public void ShowProcess(CraftingSystem.TruckProcessView.ProcessModel m, List<Tab> tabs, Action onClose)
        {
            VisualElement frame = BeginWindow("process", "가공", onClose);
            if (frame == null) return;
            _procSlots.Clear();

            // 설비 칩
            var toolbar = Row("tw-toolbar");
            if (m.tabs != null)
                foreach (var t in m.tabs)
                {
                    var captured = t;
                    toolbar.Add(MakeChip(new Chip { text = t.label, selected = t.selected, accent = t.color, onClick = () => captured.onClick?.Invoke() }, "tw-chip tw-chip--cat"));
                }
            frame.Add(toolbar);

            var body = Row("tw-craft-body");
            frame.Add(body);

            // 왼쪽: 가공할 재료
            var listPanel = new VisualElement();
            listPanel.AddToClassList("tw-craft-list");
            UiToolkitSkin.Slice(listPanel, _skin.panelBrownDark, 0.35f, new Color(0.40f, 0.32f, 0.28f, 1f));
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("tw-craft-line");
            line.style.backgroundColor = m.accent;
            listPanel.Add(line);
            var listHead = Row("tw-proc-head");
            if (m.titleIcon != null)
            {
                var ti = new VisualElement { pickingMode = PickingMode.Ignore };
                ti.AddToClassList("tw-proc-title-icon");
                ti.style.backgroundImage = new StyleBackground(m.titleIcon);
                listHead.Add(ti);
            }
            listHead.Add(MakeLabel(m.listTitle ?? "가공할 재료", "tw-proc-title"));
            listPanel.Add(listHead);
            // 가공법 고르기 화면이면 돌아가기 줄을 위에 둔다
            if (m.owned != null && m.pickTitle != null && m.pickCards != null)
            {
                var backRow = Row("tw-toolbar");
                backRow.Add(MakeChip(new Chip { text = "← 돌아가기", selected = false, accent = m.accent, onClick = () => m.onPickBack?.Invoke() }, "tw-chip tw-chip--cat"));
                backRow.Add(MakeLabel(m.pickTitle + "(으)로 무엇을 만들까요?", "tw-proc-title"));
                listPanel.Add(backRow);
            }
            _gridScroll = new ScrollView(ScrollViewMode.Vertical);
            _gridScroll.AddToClassList("tw-scroll");
            _gridScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            var grid = new VisualElement();
            grid.AddToClassList("tw-grid");
            if (m.owned != null && m.pickTitle != null && m.pickCards != null)
            {
                foreach (var c in m.pickCards) grid.Add(MakeProcessCard(c, m.accent));   // 누르면 바로 가공 칸에 들어간다
            }
            else if (m.owned != null)
            {
                foreach (var o in m.owned) grid.Add(MakeOwnedSlot(o, m.accent));
                if (m.owned.Count == 0) grid.Add(MakeLabel("가진 아이템이 없어요", "tw-empty"));
            }
            else
            {
                foreach (var c in m.cards) grid.Add(MakeProcessCard(c, m.accent));
                if (m.cards.Count == 0) grid.Add(MakeLabel("이 설비로 가공할 수 있는 재료가 없어요", "tw-empty"));
            }
            _gridScroll.Add(grid);
            listPanel.Add(_gridScroll);
            body.Add(listPanel);

            listPanel.AddToClassList("tw-proc-left");

            // 칸마다 갱신용 칸을 먼저 만들어 둔다 (타이머가 창을 다시 그리지 않고 값을 바꾼다)
            if (m.slots != null)
                foreach (var _ in m.slots) _procSlots.Add(new ProcSlotEl());
            var focusSlot = m.slots != null && m.focus >= 0 && m.focus < m.slots.Count ? m.slots[m.focus] : null;
            var focusEl = focusSlot != null ? _procSlots[m.focus] : null;

            // 가운데: 설비 장면 (가공 연출을 크게 + 위에 진행 막대 + 아래에 취소/받기 + 칸 선택 줄)
            var tilesBox = new VisualElement();
            tilesBox.AddToClassList("tw-scene-tiles");
            tilesBox.Add(MakeLabel(m.slotHeader ?? "가공 칸", "tw-scene-tiles-title"));
            var slotGrid = new VisualElement();
            slotGrid.AddToClassList("tw-proc-slots");
            if (m.slots != null)
                for (int i = 0; i < m.slots.Count; i++)
                    slotGrid.Add(MakeProcessTile(m.slots[i], i == m.focus, _procSlots[i]));
            tilesBox.Add(slotGrid);
            body.Add(MakeProcessScene(m, focusSlot, focusEl, tilesBox));

            // 오른쪽: 결과 예상 + 모두 받기 + 칸 늘리기
            var right = new VisualElement();
            right.AddToClassList("tw-proc-right");
            UiToolkitSkin.Slice(right, _skin.panelBrown, 0.4f, new Color(0.55f, 0.45f, 0.38f, 1f));
            body.Add(right);

            if (!string.IsNullOrEmpty(m.discountBadge))
                right.Add(MakeAbilityBanner(m.discountBadge));   // 요리사 등 고유 능력 재료 할인
            right.Add(MakeResultPanel(m, focusSlot, focusEl));

            // 큰 화면의 "받기"로 받을 수 있는 칸이 하나뿐이면 "모두 받기"는 필요 없다 (자리를 아낀다)
            bool focusClaimable = focusSlot != null && focusSlot.onClaim != null;
            if (m.onClaimAll != null && (m.readyTotal > 1 || (m.readyTotal == 1 && !focusClaimable)))
                right.Add(MakeClaimAll(m));

            if (!string.IsNullOrEmpty(m.message))
                right.Add(MakeLabel(m.message, "tw-proc-message"));

            if (m.upgrade != null)
                right.Add(MakeUpgrade(m.upgrade));

            _hoverLabel = MakeLabel(m.slotHint ?? m.listHint ?? "", "tw-hint");
            frame.Add(_hoverLabel);

            EndWindow(tabs);
        }

        /// <summary>매 프레임: i번 가공 칸의 남은 시간/진행도를 갱신한다 (창을 다시 그리지 않는다).</summary>
        public void SetProcessSlot(int i, string timerText, float progress)
        {
            if (i < 0 || i >= _procSlots.Count) return;
            ProcSlotEl e = _procSlots[i];
            if (e == null) return;
            if (e.timer != null) e.timer.text = timerText;
            if (e.fill != null) e.fill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
            if (e.bigFill != null) e.bigFill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
            if (e.bigPercent != null) e.bigPercent.text = Mathf.FloorToInt(Mathf.Clamp01(progress) * 100f) + "%";
            if (e.bigTimer != null) e.bigTimer.text = timerText;
            e.onProgress?.Invoke(progress);   // 가공 연출(구워질수록 갈색 등)에도 진행도를 알려 준다
        }

        private VisualElement MakeProcessCard(CraftingSystem.TruckProcessView.CardInfo c, Color accent)
        {
            var b = new Button(() => c.onClick?.Invoke()) { text = "" };
            b.AddToClassList("tw-pcard");
            if (c.selected) b.AddToClassList("tw-card--on");
            if (!c.can) b.AddToClassList("tw-card--off");
            UiToolkitSkin.Slice(b, _skin.panelBrownDark, 0.35f, c.can ? UiSkinUtil.SlotFilled : UiSkinUtil.SlotNormal);
            Color border = c.selected ? new Color(1f, 0.82f, 0.32f) : (c.can ? new Color(accent.r, accent.g, accent.b, 0.7f) : new Color(1f, 1f, 1f, 0.08f));
            b.style.borderTopColor = border; b.style.borderBottomColor = border; b.style.borderLeftColor = border; b.style.borderRightColor = border;

            var row = Row("tw-pcard-row");
            row.pickingMode = PickingMode.Ignore;
            row.Add(ItemIconWithCount(c.input, c.inCount, "tw-pcard-icon"));
            row.Add(MakeLabel("→", "tw-pcard-arrow"));
            row.Add(ItemIconWithCount(c.output, c.outCount, "tw-pcard-icon"));
            b.Add(row);
            b.Add(MakeLabel(c.output != null ? c.output.itemName : "?", "tw-card-name"));
            if (c.discounted)
            {
                // 고유 능력 할인: 카드 왼쪽 위에 주황 "할인" 표시 + 테두리 금색
                Label tag = MakeDiscountPill("할인");
                tag.style.position = Position.Absolute;
                tag.style.top = 3; tag.style.left = 3; tag.style.marginLeft = 0;
                b.Add(tag);
                if (!c.selected) SetBorderColor(b, new Color(ForgeGold.r, ForgeGold.g, ForgeGold.b, c.can ? 0.9f : 0.45f));
            }
            string recipeText = !string.IsNullOrEmpty(c.ingredientText)
                ? c.ingredientText
                : (c.input != null ? c.input.itemName + " ×" + c.inCount : "?");
            string hint = recipeText + "  →  " + (c.output != null ? c.output.itemName + " ×" + c.outCount : "?")
                          + (c.discounted ? "   · 고유 능력 재료 할인" : "")
                          + (c.can ? "" : "   (재료가 부족해요)") + (c.selected ? "   · 오른쪽 칸을 누르면 넣어요" : "");
            b.RegisterCallback<PointerEnterEvent>(_ => SetHint(hint));
            return b;
        }

        private VisualElement ItemIconWithCount(ItemData item, int count, string cls)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList(cls);
            if (item != null && item.icon != null) box.style.backgroundImage = new StyleBackground(item.icon);
            if (count > 1) box.Add(MakeLabel("×" + count, "tw-icon-count"));
            return box;
        }

        /// <summary>칸 선택 줄의 작은 칸: 빈 칸 "+", 가공 중이면 재료 아이콘 + 진행 막대, 완성이면 결과물 + 초록 테두리. 누르면 큰 화면에 보여 준다.</summary>
        private VisualElement MakeProcessTile(CraftingSystem.TruckProcessView.SlotInfo sl, bool focused, ProcSlotEl el)
        {
            var b = new Button(() => (sl.onFocus ?? sl.onClick)?.Invoke()) { text = "" };
            b.AddToClassList("tw-ptile");
            Color tint = sl.state == 2 ? new Color(0.70f, 0.95f, 0.62f, 1f) : (sl.state == 1 ? UiSkinUtil.SlotFilled : UiSkinUtil.SlotNormal);
            UiToolkitSkin.Slice(b, _skin.panelBrownDark, 0.35f, tint);
            if (focused) b.AddToClassList("tw-ptile--on");
            if (sl.sameSelected) b.AddToClassList("tw-ptile--same");
            if (sl.state == 2) b.AddToClassList("tw-ptile--done");

            if (sl.state == 0)
            {
                b.AddToClassList("tw-ptile--empty");
                b.Add(MakeLabel("+", "tw-ptile-plus"));
            }
            else
            {
                b.Add(ItemIconWithCount(sl.state == 2 ? sl.output : (sl.input ?? sl.inItem), 0, "tw-ptile-icon"));
                if (sl.state == 1)
                {
                    var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                    bar.AddToClassList("tw-ptile-bar");
                    el.fill = new VisualElement { pickingMode = PickingMode.Ignore };
                    el.fill.AddToClassList("tw-pslot-fill");
                    el.fill.style.width = Length.Percent(Mathf.Clamp01(sl.progress) * 100f);
                    bar.Add(el.fill);
                    b.Add(bar);
                }
                else b.Add(MakeLabel("✔", "tw-ptile-check"));
                // 칸에 든 개수 / 칸당 최대 (가공 대기 + 완성품)
                int inSlot = sl.queued + sl.readyCount;
                if (inSlot > 1 || sl.queued > 0) b.Add(MakeLabel(inSlot + "/" + CraftingSystem.TruckProcessingLocal.MaxPerSlot, "tw-ptile-queue"));
            }

            string hint = sl.state == 0 ? "빈 칸: 왼쪽에서 아이템을 누르면 여기에 들어가요"
                : sl.state == 2 ? "다 됐어요! 눌러서 큰 화면에서 받아요"
                : sl.sameSelected ? "같은 재료를 1개 더 넣어요" : "눌러서 이 칸의 가공 모습을 크게 봐요";
            b.RegisterCallback<PointerEnterEvent>(_ => SetHint(hint));
            return b;
        }

        private static VisualElement ProcessSlotStatus(Label label, string iconName, Color? tint = null)
        {
            var row = Row("tw-pslot-status");
            row.pickingMode = PickingMode.Ignore;
            Sprite sprite = UiIcons.Get(iconName);
            if (sprite != null)
            {
                var icon = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit,
                    tintColor = tint ?? Color.white, pickingMode = PickingMode.Ignore };
                icon.AddToClassList("tw-pslot-status-icon");
                row.Add(icon);
            }
            row.Add(label);
            return row;
        }

        private VisualElement MakeUpgrade(CraftingSystem.TruckProcessView.UpgradeInfo up)
        {
            var box = new VisualElement();
            box.AddToClassList("tw-upgrade");
            if (up.maxed)
            {
                box.Add(MakeLabel("가공 칸이 최대예요", "tw-upgrade-title"));
                return box;
            }
            box.Add(MakeLabel(up.title ?? "칸 늘리기", "tw-upgrade-title"));
            var costs = Row("tw-upgrade-costs");
            foreach (var c in up.costs)
            {
                var cell = Row("tw-upgrade-cost");
                cell.pickingMode = PickingMode.Ignore;
                cell.Add(ItemIconWithCount(c.item, 0, "tw-ing-icon"));
                cell.Add(MakeLabel(c.have + "/" + c.need, c.have >= c.need ? "tw-ing-count tw-ing-count--ok" : "tw-ing-count tw-ing-count--short"));
                MakeMissingLink(cell, c.item, c.have < c.need);
                costs.Add(cell);
            }
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            costs.Add(spacer);
            var btn = new Button(() => { if (up.canAfford) up.onClick?.Invoke(); }) { text = "" };
            btn.AddToClassList("tw-make");
            btn.AddToClassList("tw-upgrade-btn");
            btn.EnableInClassList("tw-make--off", !up.canAfford);
            UiToolkitSkin.Slice(btn, up.canAfford ? _skin.buttonBrown : _skin.buttonGrey, 0.5f,
                up.canAfford ? new Color(0.50f, 0.88f, 0.56f, 1f) : new Color(0.55f, 0.55f, 0.60f, 1f));
            btn.Add(MakeLabel(up.canAfford ? "칸 늘리기" : "재료 부족", "tw-make-text"));
            costs.Add(btn);
            box.Add(costs);
            return box;
        }

        // ------------------------------------------------------------------ 부품

        private static VisualElement Row(string cls)
        {
            var r = new VisualElement();
            r.AddToClassList(cls);
            return r;
        }

        private Label MakeLabel(string text, string cls)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("lt-text");
            foreach (string c in cls.Split(' ')) if (c.Length > 0) l.AddToClassList(c);
            l.style.unityFontDefinition = _font;
            return l;
        }

        private VisualElement MakeChip(Chip c, string cls)
        {
            var b = new Button(() => c.onClick?.Invoke()) { text = "" };
            foreach (string k in cls.Split(' ')) if (k.Length > 0) b.AddToClassList(k);
            Sprite bg = c.selected ? _skin.buttonBrown : _skin.buttonGrey;
            Color tint = c.selected ? Color.Lerp(new Color(1f, 0.92f, 0.78f), c.accent, 0.45f) : new Color(0.46f, 0.47f, 0.52f);
            UiToolkitSkin.Slice(b, bg, 0.5f, tint);
            if (c.selected) b.AddToClassList("tw-chip--on");
            if (c.icon != null)
            {
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("tw-chip-icon");
                icon.style.backgroundImage = new StyleBackground(c.icon);
                b.Add(icon);
            }
            if (!string.IsNullOrEmpty(c.text))
                b.Add(MakeLabel(c.text, c.selected ? "tw-chip-text tw-chip-text--on" : "tw-chip-text"));
            if (!string.IsNullOrEmpty(c.tooltip))
            {
                b.RegisterCallback<PointerEnterEvent>(_ => SetHint(c.tooltip));
            }
            return b;
        }

        private VisualElement MakeSlot(Slot s)
        {
            var b = new Button(() => s.onClick?.Invoke()) { text = "" };
            b.AddToClassList("tw-slot");
            UiToolkitSkin.Slice(b, _skin.panelBrownDark, 0.35f, UiSkinUtil.SlotFilled);

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("tw-slot-icon");
            if (s.item != null && s.item.icon != null) icon.style.backgroundImage = new StyleBackground(s.item.icon);
            b.Add(icon);

            if (s.item != null && s.item.icon == null)
                b.Add(MakeLabel(s.item.itemName.Length <= 2 ? s.item.itemName : s.item.itemName.Substring(0, 2), "tw-slot-letter"));
            if (s.count > 1)
                b.Add(MakeLabel(s.count.ToString(), "tw-slot-count"));
            b.Add(MakeLabel(s.item != null ? s.item.itemName : "", "tw-slot-name"));

            if (s.item != null && s.item.HasDurability)
            {
                var back = new VisualElement { pickingMode = PickingMode.Ignore };
                back.AddToClassList("tw-dur");
                var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.AddToClassList("tw-dur-fill");
                float r = Mathf.Clamp01(s.durability01);
                fill.style.width = Length.Percent(r * 100f);
                fill.style.backgroundColor = r > 0.5f
                    ? Color.Lerp(new Color(0.95f, 0.8f, 0.25f), new Color(0.45f, 0.85f, 0.35f), (r - 0.5f) * 2f)
                    : Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.95f, 0.8f, 0.25f), r * 2f);
                back.Add(fill);
                b.Add(back);
            }

            string hint = s.item != null ? s.item.itemName + "  ×" + s.count + (s.item.maxStack <= 1 ? "  (누르면 바로 꺼내기)" : "  (누르면 개수 고르기)") : "";
            b.RegisterCallback<PointerEnterEvent>(_ => SetHint(hint));
            return b;
        }

        private VisualElement MakeTab(Tab t)
        {
            var b = new Button(() => t.onClick?.Invoke()) { text = "" };
            b.AddToClassList("tw-tab");
            if (t.selected) b.AddToClassList("tw-tab--on");
            if (t.locked) b.AddToClassList("tw-tab--locked");
            bool poly = _skin != null && _skin.polyTheme;
            if (poly)
            {
                // 원신풍: 고른 탭 = 크림색 판 + 짙은 아이콘, 나머지 = 남색 판 + 금빛 아이콘. 탭마다의 색은 왼쪽 가는 띠로만 남긴다.
                b.style.backgroundColor = Color.clear;
                UiToolkitSkin.Slice(b, t.selected ? _skin.buttonBrown : _skin.panelBrownDark, 0.45f, Color.white);
                var stripe = new VisualElement { pickingMode = PickingMode.Ignore };
                stripe.style.position = Position.Absolute;
                stripe.style.left = 7; stripe.style.top = 12; stripe.style.bottom = 12; stripe.style.width = 4;
                stripe.style.borderTopLeftRadius = stripe.style.borderBottomLeftRadius = stripe.style.borderTopRightRadius = stripe.style.borderBottomRightRadius = 2;
                stripe.style.backgroundColor = new Color(t.color.r, t.color.g, t.color.b, t.selected ? 1f : 0.7f);
                b.Add(stripe);
            }
            else b.style.backgroundColor = t.selected ? t.color : new Color(t.color.r * 0.55f, t.color.g * 0.55f, t.color.b * 0.55f, 0.95f);
            if (t.icon != null)
            {
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("tw-tab-icon");
                icon.style.backgroundImage = new StyleBackground(t.icon);
                if (poly) icon.style.unityBackgroundImageTintColor = t.selected ? new Color(0.23f, 0.26f, 0.33f, 1f) : new Color(0.88f, 0.79f, 0.6f, 1f);
                b.Add(icon);
            }
            else b.Add(MakeLabel(t.label, "tw-tab-text"));
            b.RegisterCallback<PointerEnterEvent>(_ => SetHint(t.label + (t.locked ? "  (설비를 제작하면 열려요)" : "")));
            return b;
        }

        private VisualElement MakeWithdraw(Model m)
        {
            var row = Row("tw-withdraw");
            UiToolkitSkin.Slice(row, _skin.panelBrown, 0.4f, new Color(0.85f, 0.74f, 0.62f, 1f));

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("tw-withdraw-icon");
            if (m.withdrawItem.icon != null) icon.style.backgroundImage = new StyleBackground(m.withdrawItem.icon);
            row.Add(icon);
            row.Add(MakeLabel(m.withdrawItem.itemName + "\n보유 " + m.withdrawMax + "개", "tw-withdraw-name"));

            int max = Mathf.Max(1, m.withdrawMax);
            int value = Mathf.Clamp(m.withdrawDefault, 1, max);
            Label amount = MakeLabel(value.ToString(), "tw-amount");

            Action<int> set = v =>
            {
                value = Mathf.Clamp(v, 1, max);
                amount.text = value.ToString();
            };
            row.Add(SmallButton("-10", () => set(value - 10)));
            row.Add(SmallButton("-1", () => set(value - 1)));
            row.Add(amount);
            row.Add(SmallButton("+1", () => set(value + 1)));
            row.Add(SmallButton("+10", () => set(value + 10)));
            row.Add(SmallButton("전부", () => set(max)));

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            row.Add(spacer);

            if (m.onConvert != null)
            {
                Button convert = MakeConvertButton(string.IsNullOrEmpty(m.convertLabel) ? "변환" : m.convertLabel, () => m.onConvert?.Invoke(), m.convertEnabled);
                string convertHint = m.convertEnabled || string.IsNullOrEmpty(m.convertDisabledReason)
                    ? "과학자 능력: 광물 변환 (낮에만, 결과는 트럭 보관함으로)"
                    : "과학자 능력: 광물 변환 - " + m.convertDisabledReason;
                convert.RegisterCallback<PointerEnterEvent>(_ => SetHint(convertHint));
                row.Add(convert);
            }

            Button ok = SmallButton("꺼내기", () => m.onWithdraw?.Invoke(value));
            ok.AddToClassList("tw-btn--ok");
            row.Add(ok);
            row.Add(SmallButton("취소", () => m.onWithdrawCancel?.Invoke()));

            // 마우스 휠로도 개수를 바꾼다
            row.RegisterCallback<WheelEvent>(e => { set(value + (e.delta.y < 0 ? 1 : -1)); e.StopPropagation(); });
            return row;
        }

        // 과학자 능력 색 (실험실 느낌의 청록)
        private static readonly Color LabCyan = new Color(0.10f, 0.78f, 0.86f, 1f);
        private static readonly Color LabDeep = new Color(0.04f, 0.36f, 0.46f, 1f);
        private static readonly Color LabGlow = new Color(0.62f, 1f, 0.98f, 1f);

        /// <summary>
        /// 과학자 광물 변환 버튼: 갈색 버튼들 사이에서 눈에 띄도록 청록 바탕 + 깜빡이는 밝은 테두리.
        /// enabled=false(재료 부족/밤)면 회색으로 어둡게, 깜빡임 없이 그린다 (눌러도 이유 안내만 나온다).
        /// </summary>
        private Button MakeConvertButton(string text, Action onClick, bool enabled = true)
        {
            if (!enabled)
            {
                var off = new Button(() => onClick?.Invoke()) { text = "" };
                off.AddToClassList("tw-btn");
                off.style.backgroundImage = StyleKeyword.None;
                off.style.backgroundColor = new Color(0.20f, 0.21f, 0.22f, 1f);
                off.style.paddingLeft = 10; off.style.paddingRight = 10;
                off.style.marginLeft = 4; off.style.marginRight = 6;
                SetBorder(off, new Color(0.38f, 0.40f, 0.42f, 1f), 2f, 6f);

                Label offLabel = MakeLabel(text, "tw-btn-text");
                offLabel.style.color = new Color(0.55f, 0.56f, 0.58f, 1f);
                offLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                off.Add(offLabel);
                return off;
            }

            var b = new Button(() => onClick?.Invoke()) { text = "" };
            b.AddToClassList("tw-btn");
            b.style.backgroundImage = StyleKeyword.None;
            b.style.backgroundColor = LabDeep;
            b.style.paddingLeft = 10; b.style.paddingRight = 10;
            b.style.marginLeft = 4; b.style.marginRight = 6;
            SetBorder(b, LabCyan, 2f, 6f);

            Label label = MakeLabel(text, "tw-btn-text");
            label.style.color = Color.white;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextOutlineColor = new Color(0f, 0.12f, 0.16f, 1f);
            label.style.unityTextOutlineWidth = 0.15f;
            b.Add(label);

            bool hover = false;
            b.RegisterCallback<PointerEnterEvent>(_ => hover = true);
            b.RegisterCallback<PointerLeaveEvent>(_ => hover = false);

            // 테두리가 청록 ↔ 밝은 하늘색으로 깜빡이고, 마우스를 올리면 바탕이 밝아진다
            b.schedule.Execute(() =>
            {
                float t = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 4f);
                SetBorderColor(b, Color.Lerp(LabCyan, LabGlow, t));
                b.style.backgroundColor = hover ? Color.Lerp(LabDeep, LabCyan, 0.55f) : Color.Lerp(LabDeep, LabCyan, 0.12f * t);
            }).Every(50);
            return b;
        }

        private Button SmallButton(string text, Action onClick)
        {
            var b = new Button(() => onClick?.Invoke()) { text = "" };
            b.AddToClassList("tw-btn");
            UiToolkitSkin.Slice(b, _skin.buttonBrown, 0.5f, new Color(0.86f, 0.70f, 0.52f, 1f));
            b.Add(MakeLabel(text, "tw-btn-text"));
            return b;
        }

        /// <summary>모자란 재료를 누르면 그 아이템을 만드는 제작 도감으로 이동시키는 콜백 (GameUIController가 연결한다).</summary>
        public Action<ItemData> OnMissingItem;

        /// <summary>재료가 모자랄 때만: 눌러서 제작 도감으로 가는 링크로 만든다 (마우스를 올리면 안내 문구).</summary>
        private void MakeMissingLink(VisualElement el, ItemData item, bool missing)
        {
            if (!missing || item == null || el == null) return;
            el.pickingMode = PickingMode.Position;
            el.AddToClassList("tw-missing-link");
            el.RegisterCallback<PointerEnterEvent>(_ => SetHint(item.itemName + " — 눌러서 만드는 방법(제작 도감) 보기"));
            el.RegisterCallback<ClickEvent>(_ => OnMissingItem?.Invoke(item));
        }

        private void SetHint(string text)
        {
            if (_hoverLabel != null && !string.IsNullOrEmpty(text)) _hoverLabel.text = text;
        }
    }
}
