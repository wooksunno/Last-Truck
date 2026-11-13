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
        }

        // ---- 제작 탭 ----

        public class CraftCard
        {
            public ItemData item;
            public bool canCraft;
            public bool selected;
            public bool installed;
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
            string hint = c.item == null ? "" : c.item.itemName + (c.installed ? "  (이미 설치됨)" : c.canCraft ? "  (지금 만들 수 있어요)" : "  (재료가 부족해요)");
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
                string count = ing.have + " / " + ing.need + (ing.originalNeed > ing.need ? "  (원래 " + ing.originalNeed + ")" : "");
                row.Add(MakeLabel(count, enough ? "tw-ing-count tw-ing-count--ok" : "tw-ing-count tw-ing-count--short"));
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
            UiToolkitSkin.Slice(make, m.canCraft ? _skin.buttonBrown : _skin.buttonGrey, 0.5f,
                m.canCraft ? new Color(0.50f, 0.88f, 0.56f, 1f) : new Color(0.55f, 0.55f, 0.60f, 1f));
            make.Add(MakeLabel(m.craftLabel ?? "제작하기", "tw-make-text"));
            buttons.Add(make);
            d.Add(buttons);
        }

        // ---- 가공 탭 ----

        private class ProcSlotEl { public VisualElement fill; public Label timer; }
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
            listHead.Add(MakeLabel((m.title ?? "") + "  ·  " + (m.listTitle ?? "가공할 재료"), "tw-proc-title"));
            listPanel.Add(listHead);
            _gridScroll = new ScrollView(ScrollViewMode.Vertical);
            _gridScroll.AddToClassList("tw-scroll");
            _gridScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            var grid = new VisualElement();
            grid.AddToClassList("tw-grid");
            foreach (var c in m.cards) grid.Add(MakeProcessCard(c, m.accent));
            if (m.cards.Count == 0) grid.Add(MakeLabel("이 설비로 가공할 수 있는 재료가 없어요", "tw-empty"));
            _gridScroll.Add(grid);
            listPanel.Add(_gridScroll);
            body.Add(listPanel);

            // 오른쪽: 가공 칸
            var right = new VisualElement();
            right.AddToClassList("tw-proc-right");
            UiToolkitSkin.Slice(right, _skin.panelBrown, 0.4f, new Color(0.55f, 0.45f, 0.38f, 1f));
            body.Add(right);

            right.Add(MakeLabel(m.slotHeader ?? "가공 칸", "tw-detail-need"));
            var slotGrid = new VisualElement();
            slotGrid.AddToClassList("tw-proc-slots");
            if (m.slots != null)
                foreach (var sl in m.slots) slotGrid.Add(MakeProcessSlot(sl));
            right.Add(slotGrid);

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
            string hint = (c.input != null ? c.input.itemName + " ×" + c.inCount : "?") + "  →  " + (c.output != null ? c.output.itemName + " ×" + c.outCount : "?")
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

        private VisualElement MakeProcessSlot(CraftingSystem.TruckProcessView.SlotInfo sl)
        {
            var b = new Button(() => sl.onClick?.Invoke()) { text = "" };
            b.AddToClassList("tw-pslot");
            var el = new ProcSlotEl();
            Color tint = sl.state == 2 ? new Color(0.70f, 0.95f, 0.62f, 1f) : (sl.state == 1 ? UiSkinUtil.SlotFilled : UiSkinUtil.SlotNormal);
            UiToolkitSkin.Slice(b, _skin.panelBrownDark, 0.35f, tint);
            if (sl.sameSelected) b.AddToClassList("tw-pslot--same");

            if (sl.state == 0)
            {
                b.Add(MakeLabel("+", "tw-pslot-plus"));
                b.Add(MakeLabel("빈 칸", "tw-pslot-text"));
            }
            else if (sl.state == 1)
            {
                var row = Row("tw-pcard-row");
                row.pickingMode = PickingMode.Ignore;
                row.Add(ItemIconWithCount(sl.input, 0, "tw-pslot-icon"));
                row.Add(MakeLabel("→", "tw-pcard-arrow"));
                row.Add(ItemIconWithCount(sl.output, 0, "tw-pslot-icon"));
                b.Add(row);
                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.AddToClassList("tw-pslot-bar");
                el.fill = new VisualElement { pickingMode = PickingMode.Ignore };
                el.fill.AddToClassList("tw-pslot-fill");
                el.fill.style.width = Length.Percent(Mathf.Clamp01(sl.progress) * 100f);
                bar.Add(el.fill);
                b.Add(bar);
                el.timer = MakeLabel(sl.timerText ?? "", "tw-pslot-text");
                b.Add(el.timer);
                if (sl.queued > 1) b.Add(MakeLabel("대기 ×" + sl.queued, "tw-pslot-queue"));
                if (sl.readyCount > 0) b.Add(MakeLabel("완성 ×" + sl.readyCount, "tw-pslot-ready"));
            }
            else
            {
                b.AddToClassList("tw-pslot--done");
                b.Add(ItemIconWithCount(sl.output, 0, "tw-pslot-icon tw-pslot-icon--big"));
                b.Add(MakeLabel("받기 ×" + Mathf.Max(1, sl.readyCount), "tw-pslot-claim"));
            }

            string hint = sl.state == 0 ? "빈 칸: 왼쪽에서 고른 재료를 넣어요"
                : sl.state == 2 ? "다 됐어요! 누르면 결과물을 받아요"
                : sl.sameSelected ? "같은 재료를 1개 더 넣어요" : "가공 중: 재료를 고르지 않고 누르면 남은 재료를 돌려받아요";
            b.RegisterCallback<PointerEnterEvent>(_ => SetHint(hint));
            _procSlots.Add(el);
            return b;
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
            b.style.backgroundColor = t.selected ? t.color : new Color(t.color.r * 0.55f, t.color.g * 0.55f, t.color.b * 0.55f, 0.95f);
            if (t.icon != null)
            {
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("tw-tab-icon");
                icon.style.backgroundImage = new StyleBackground(t.icon);
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

            Button ok = SmallButton("꺼내기", () => m.onWithdraw?.Invoke(value));
            ok.AddToClassList("tw-btn--ok");
            row.Add(ok);
            row.Add(SmallButton("취소", () => m.onWithdrawCancel?.Invoke()));

            // 마우스 휠로도 개수를 바꾼다
            row.RegisterCallback<WheelEvent>(e => { set(value + (e.delta.y < 0 ? 1 : -1)); e.StopPropagation(); });
            return row;
        }

        private Button SmallButton(string text, Action onClick)
        {
            var b = new Button(() => onClick?.Invoke()) { text = "" };
            b.AddToClassList("tw-btn");
            UiToolkitSkin.Slice(b, _skin.buttonBrown, 0.5f, new Color(0.86f, 0.70f, 0.52f, 1f));
            b.Add(MakeLabel(text, "tw-btn-text"));
            return b;
        }

        private void SetHint(string text)
        {
            if (_hoverLabel != null && !string.IsNullOrEmpty(text)) _hoverLabel.text = text;
        }
    }
}
