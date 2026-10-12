using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static CraftingSystem.CraftUI;

namespace CraftingSystem
{
    /// <summary>
    /// 가공 화면(트럭 가공 / 현장 가공 시설 공용).
    /// 왼쪽: 가공할 재료 카드 목록 / 오른쪽: [재료 슬롯] → 진행(타이머·진행 막대·버튼) → [결과 슬롯].
    /// 화면에 표시할 값은 모두 ProcessModel로 받고, 매 프레임 바뀌는 값(타이머/진행 막대)은 Refs로 돌려준다.
    /// </summary>
    public class TruckProcessView
    {
        public class TabInfo { public string label; public Color color; public bool selected; public Action onClick; }

        public class CardInfo
        {
            public ItemData input, output;
            public int inCount, outCount;
            public bool can, selected;
            public Action onClick;
        }

        /// <summary>여러 칸 가공 화면의 칸 하나. state: 0 빈 칸, 1 가공 중, 2 완료(받기)</summary>
        public class SlotInfo
        {
            public int state;
            public ItemData input, output;
            public float progress;            // 가공 중일 때 0~1
            public string timerText;          // 가공 중 남은 시간
            public Action onClick;            // 빈 칸: 넣기 / 가공 중: 빼기 / 완료: 받기
        }

        public class CostLine { public ItemData item; public int need, have; }

        /// <summary>칸 늘리기 업그레이드 정보.</summary>
        public class UpgradeInfo
        {
            public string title;              // 예: "칸 늘리기 (2 → 3칸)"
            public List<CostLine> costs = new List<CostLine>();
            public bool canAfford, maxed;
            public Action onClick;
        }

        public class ProcessModel
        {
            public List<SlotInfo> slots;         // 있으면 "여러 칸" 화면(트럭 가공), 없으면 입력→결과 한 줄 화면(현장 시설)
            public string slotHeader;            // 예: "가공 칸 2 / 6"
            public string slotHint;              // 예: 선택한 재료 안내
            public UpgradeInfo upgrade;
            public string title;                 // 시설 이름
            public Sprite titleIcon;
            public Color accent = new Color(0.96f, 0.78f, 0.40f);
            public List<TabInfo> tabs;           // 트럭 가공일 때 시설 탭(없으면 null)
            public List<CardInfo> cards = new List<CardInfo>();
            public string listTitle = "가공할 재료";
            public string listHint;
            public ItemData inItem; public string inText;
            public Action onInputClick;          // 입력칸을 누르면 대기 중인 재료를 1개 빼낸다 (null이면 누를 수 없음)
            public ItemData outItem; public string outText;
            public bool outClaim; public Action onClaim;
            public string timerText; public float progress = -1f;   // progress < 0 이면 막대를 숨긴다
            public string statusText;
            public string message;
            public string buttonLabel; public bool buttonEnabled; public Action onButton;   // 현장 시설용 "가공하기" 버튼
        }

        public class SlotRef
        {
            public Text timer;
            public RectTransform fill;
        }

        public class Refs
        {
            public Text timer;
            public RectTransform fill;
            public List<SlotRef> slots = new List<SlotRef>();   // 여러 칸 화면: 칸마다 타이머/진행 막대
            public void SetProgress(float p)
            {
                if (fill == null) return;
                fill.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
            }

            /// <summary>매 프레임: i번 칸의 남은 시간/진행도를 갱신한다.</summary>
            public void SetSlot(int i, string timerText, float progress)
            {
                if (i < 0 || i >= slots.Count) return;
                SlotRef s = slots[i];
                if (s.timer != null) s.timer.text = timerText;
                if (s.fill != null) s.fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            }
        }

        public static Refs Build(RectTransform parent, float h, ProcessModel m, Action onBack)
        {
            var refs = new Refs();
            var root = Mk("ProcessRoot", parent);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h; le.minHeight = h; le.flexibleWidth = 1f;
            root.sizeDelta = new Vector2(0f, h);

            // 상단 바 (뒤로 가기가 있을 때만)
            float topH = 0f;
            if (onBack != null)
            {
                var bar = Mk("TopBar", root);
                bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1); bar.pivot = new Vector2(0.5f, 1);
                bar.anchoredPosition = Vector2.zero; bar.sizeDelta = new Vector2(0, 50);
                var back = MkButton(bar, "BackButton", "← 뒤로", BtnBrown, 18, () => onBack());
                var brt = (RectTransform)back.transform;
                brt.anchorMin = brt.anchorMax = new Vector2(0, 0.5f); brt.pivot = new Vector2(0, 0.5f);
                brt.anchoredPosition = new Vector2(2, 0); brt.sizeDelta = new Vector2(104, 40);
                var tt = Mk("Title", bar); tt.anchorMin = Vector2.zero; tt.anchorMax = Vector2.one; tt.offsetMin = new Vector2(124, 0); tt.offsetMax = Vector2.zero;
                Txt(tt, m.title, 22, FontStyle.Bold, Cream, TextAnchor.MiddleLeft);
                topH = 56f;
            }

            // 시설 탭 (트럭 가공)
            float bodyTop = topH;
            if (m.tabs != null && m.tabs.Count > 0)
            {
                var tabs = Mk("Tabs", root);
                tabs.anchorMin = new Vector2(0, 1); tabs.anchorMax = new Vector2(1, 1); tabs.pivot = new Vector2(0, 1);
                tabs.anchoredPosition = new Vector2(14, -topH); tabs.sizeDelta = new Vector2(-28, 48);
                var hl = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.childAlignment = TextAnchor.LowerLeft; hl.spacing = 6;
                hl.childControlWidth = false; hl.childControlHeight = false; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
                foreach (TabInfo t in m.tabs)
                {
                    float w = Mathf.Max(110f, t.label.Length * 17f + 30f);
                    TabInfo cap = t;
                    FolderTab(tabs, t.label, t.color, t.selected, w, () => cap.onClick?.Invoke());
                }
                bodyTop = topH + 46f;
            }

            // 본문
            var panel = Panel(root, "Body", PanelCol, true);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero; panel.offsetMax = new Vector2(0, -bodyTop);
            var line = Mk("TopLine", panel);
            line.anchorMin = new Vector2(0, 1); line.anchorMax = new Vector2(1, 1); line.pivot = new Vector2(0.5f, 1);
            line.sizeDelta = new Vector2(-40, 4); line.anchoredPosition = new Vector2(0, -2);
            Img(line, null, m.accent, false);

            BuildList(panel, m);
            BuildMachine(panel, m, refs);
            return refs;
        }

        static void BuildList(RectTransform panel, ProcessModel m)
        {
            var left = Mk("List", panel);
            left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0.56f, 1);
            left.offsetMin = new Vector2(18, 14); left.offsetMax = new Vector2(-8, -14);

            var head = Mk("Head", left);
            head.anchorMin = new Vector2(0, 1); head.anchorMax = new Vector2(1, 1); head.pivot = new Vector2(0, 1);
            head.anchoredPosition = Vector2.zero; head.sizeDelta = new Vector2(0, 34);
            Txt(head, m.listTitle, 22, FontStyle.Bold, Cream, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(m.listHint))
            {
                var hint = Mk("Hint", left);
                hint.anchorMin = new Vector2(0, 1); hint.anchorMax = new Vector2(1, 1); hint.pivot = new Vector2(0, 1);
                hint.anchoredPosition = new Vector2(0, -34); hint.sizeDelta = new Vector2(0, 26);
                Txt(hint, m.listHint, 15, FontStyle.Normal, Dim, TextAnchor.MiddleLeft);
            }

            var area = Mk("Area", left);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero; area.offsetMax = new Vector2(0, -68);
            RectTransform grid;
            NewScroll(area, false, true, 0, 0, 0, 0, out grid);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(150, 138); g.spacing = new Vector2(10, 10);
            g.padding = new RectOffset(4, 4, 6, 12);
            g.constraint = GridLayoutGroup.Constraint.Flexible; g.childAlignment = TextAnchor.UpperCenter;
            var csf = grid.gameObject.AddComponent<ContentSizeFitter>(); csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (m.cards.Count == 0)
            {
                var none = Mk("None", area); Stretch(none, 0, 0, 0, 0);
                Txt(none, "가공할 수 있는 재료가 없습니다", 20, FontStyle.Bold, Dim, TextAnchor.MiddleCenter);
            }

            foreach (CardInfo c in m.cards)
                BuildCard(grid, c, m.accent);
        }

        static void BuildCard(RectTransform grid, CardInfo c, Color accent, float scale = 1f)
        {
            var card = Mk("Card_" + (c.input != null ? c.input.itemID : "x"), grid);
            var bg = Img(card, Round, c.can ? new Color(0.26f, 0.20f, 0.13f, 1f) : CardCol, true, true);
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = bg;
            var cb = btn.colors; cb.highlightedColor = CardHover; cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); btn.colors = cb;
            Action act = c.onClick;
            btn.onClick.AddListener(() => act?.Invoke());

            if (Ring != null)
            {
                var ring = Mk("Ring", card); Stretch(ring);
                Img(ring, Ring, c.selected ? Amber : (c.can ? new Color(accent.r, accent.g, accent.b, 0.6f) : new Color(1, 1, 1, 0.08f)));
            }

            float a = c.can ? 1f : 0.5f;
            var inIcon = Mk("In", card);
            inIcon.anchorMin = inIcon.anchorMax = new Vector2(0, 1); inIcon.pivot = new Vector2(0, 1);
            inIcon.anchoredPosition = new Vector2(12, -14); inIcon.sizeDelta = new Vector2(52 * scale, 52 * scale);
            ItemIcon(inIcon, c.input, a);

            if (Tri != null)
            {
                var arrow = Mk("Arrow", card);
                arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 1); arrow.pivot = new Vector2(0.5f, 0.5f);
                arrow.anchoredPosition = new Vector2(0, -40); arrow.sizeDelta = new Vector2(14, 14);
                arrow.localRotation = Quaternion.Euler(0, 0, -90f);
                Img(arrow, Tri, new Color(Cream.r, Cream.g, Cream.b, 0.6f), false);
            }

            var outIcon = Mk("Out", card);
            outIcon.anchorMin = outIcon.anchorMax = new Vector2(1, 1); outIcon.pivot = new Vector2(1, 1);
            outIcon.anchoredPosition = new Vector2(-12, -14); outIcon.sizeDelta = new Vector2(52 * scale, 52 * scale);
            ItemIcon(outIcon, c.output, a);

            var nm = Mk("Name", card);
            nm.anchorMin = new Vector2(0, 0); nm.anchorMax = new Vector2(1, 0); nm.pivot = new Vector2(0.5f, 0);
            nm.anchoredPosition = new Vector2(0, 30); nm.sizeDelta = new Vector2(-8, 30);
            var t = Txt(nm, c.input != null ? c.input.itemName : "", 15, FontStyle.Bold, c.can ? Cream : Dim, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;

            var sub = Mk("Sub", card);
            sub.anchorMin = new Vector2(0, 0); sub.anchorMax = new Vector2(1, 0); sub.pivot = new Vector2(0.5f, 0);
            sub.anchoredPosition = new Vector2(0, 8); sub.sizeDelta = new Vector2(-8, 22);
            Txt(sub, "x" + c.inCount + " → " + (c.output != null ? c.output.itemName : "") + " x" + c.outCount, 13, FontStyle.Normal, c.can ? Green : Dim, TextAnchor.MiddleCenter);
        }

        // ---------- 여러 칸 가공 화면 (트럭 가공) ----------
        static void BuildSlotMachine(RectTransform right, ProcessModel m, Refs refs)
        {
            // 머리말: 시설 아이콘 + 이름 + 칸 수
            var head = Mk("Head", right);
            head.anchorMin = new Vector2(0, 1); head.anchorMax = new Vector2(1, 1); head.pivot = new Vector2(0.5f, 1);
            head.anchoredPosition = new Vector2(0, -14); head.sizeDelta = new Vector2(-36, 48);
            if (m.titleIcon != null)
            {
                var ic = Mk("Icon", head); ic.anchorMin = ic.anchorMax = new Vector2(0, 0.5f); ic.pivot = new Vector2(0, 0.5f);
                ic.sizeDelta = new Vector2(44, 44);
                var im = Img(ic, m.titleIcon, Color.white, false); im.preserveAspect = true;
            }
            var ht = Mk("Name", head); ht.anchorMin = Vector2.zero; ht.anchorMax = Vector2.one; ht.offsetMin = new Vector2(m.titleIcon != null ? 54 : 0, 0); ht.offsetMax = new Vector2(-150, 0);
            Txt(ht, m.title, 24, FontStyle.Bold, Cream, TextAnchor.MiddleLeft);
            var cnt = Mk("SlotCount", head); cnt.anchorMin = new Vector2(1, 0); cnt.anchorMax = new Vector2(1, 1); cnt.pivot = new Vector2(1, 0.5f);
            cnt.sizeDelta = new Vector2(150, 0); cnt.anchoredPosition = Vector2.zero;
            Txt(cnt, m.slotHeader ?? "", 18, FontStyle.Bold, Amber, TextAnchor.MiddleRight);

            // 안내 문구 (고른 재료 / 사용법)
            var hint = Mk("Hint", right);
            hint.anchorMin = new Vector2(0, 1); hint.anchorMax = new Vector2(1, 1); hint.pivot = new Vector2(0.5f, 1);
            hint.anchoredPosition = new Vector2(0, -66); hint.sizeDelta = new Vector2(-36, 44);
            var ht2 = Txt(hint, m.slotHint ?? "", 15, FontStyle.Normal, Dim, TextAnchor.MiddleLeft);
            ht2.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 칸 격자
            var gridRt = Mk("Slots", right);
            gridRt.anchorMin = new Vector2(0, 1); gridRt.anchorMax = new Vector2(1, 1); gridRt.pivot = new Vector2(0.5f, 1);
            int count = m.slots.Count;
            int cols = Mathf.Min(3, Mathf.Max(1, count));
            int rows = (count + cols - 1) / cols;
            const float cw = 128f, chh = 150f, gap = 10f;
            gridRt.anchoredPosition = new Vector2(0, -114);
            gridRt.sizeDelta = new Vector2(-24, rows * chh + (rows - 1) * gap);
            var g = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(cw, chh); g.spacing = new Vector2(gap, gap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = cols;
            g.childAlignment = TextAnchor.UpperCenter;

            for (int i = 0; i < count; i++)
                BuildSlotCard(gridRt, m.slots[i], i, refs);

            // 칸 늘리기
            if (m.upgrade != null)
                BuildUpgrade(right, m.upgrade);

            // 메시지
            if (!string.IsNullOrEmpty(m.message))
            {
                var msg = Mk("Message", right);
                msg.anchorMin = new Vector2(0, 0); msg.anchorMax = new Vector2(1, 0); msg.pivot = new Vector2(0.5f, 0);
                msg.anchoredPosition = new Vector2(0, m.upgrade != null ? 168 : 20); msg.sizeDelta = new Vector2(-36, 40);
                var t = Txt(msg, m.message, 15, FontStyle.Bold, new Color(1f, 0.82f, 0.45f), TextAnchor.MiddleCenter);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
        }

        static void BuildSlotCard(RectTransform grid, SlotInfo s, int index, Refs refs)
        {
            var card = Mk("Slot_" + index, grid);
            Color bgCol = s.state == 2 ? new Color(0.22f, 0.44f, 0.28f, 1f)
                        : s.state == 1 ? new Color(0.26f, 0.20f, 0.13f, 1f)
                        : new Color(0.09f, 0.075f, 0.065f, 1f);
            var bg = Img(card, Round, bgCol, true, true);
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = bg;
            var cb = btn.colors; cb.highlightedColor = CardHover; cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); btn.colors = cb;
            Action click = s.onClick;
            btn.onClick.AddListener(() => click?.Invoke());

            if (Ring != null)
            {
                var ring = Mk("Ring", card); Stretch(ring);
                Img(ring, Ring, s.state == 2 ? Green : (s.state == 1 ? new Color(Amber.r, Amber.g, Amber.b, 0.6f) : new Color(1, 1, 1, 0.14f)));
            }

            var num = Mk("Num", card);
            num.anchorMin = num.anchorMax = new Vector2(0, 1); num.pivot = new Vector2(0, 1);
            num.anchoredPosition = new Vector2(8, -6); num.sizeDelta = new Vector2(30, 22);
            Txt(num, (index + 1).ToString(), 14, FontStyle.Bold, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleLeft);

            var icon = Mk("Icon", card);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1); icon.pivot = new Vector2(0.5f, 1);
            icon.anchoredPosition = new Vector2(0, -16); icon.sizeDelta = new Vector2(64, 64);

            var label = Mk("Label", card);
            label.anchorMin = new Vector2(0, 0); label.anchorMax = new Vector2(1, 0); label.pivot = new Vector2(0.5f, 0);
            label.anchoredPosition = new Vector2(0, 8); label.sizeDelta = new Vector2(-8, 30);

            if (s.state == 0)
            {
                Txt(icon, "+", 54, FontStyle.Bold, new Color(1, 1, 1, 0.22f), TextAnchor.MiddleCenter);
                Txt(label, "빈 칸", 15, FontStyle.Bold, Dim, TextAnchor.MiddleCenter);
                refs.slots.Add(new SlotRef());
            }
            else if (s.state == 1)
            {
                ItemIcon(icon, s.input, 0.7f);
                Txt(label, "눌러서 빼기", 13, FontStyle.Normal, Dim, TextAnchor.MiddleCenter);

                var timer = Mk("Timer", card);
                timer.anchorMin = new Vector2(0, 0); timer.anchorMax = new Vector2(1, 0); timer.pivot = new Vector2(0.5f, 0);
                timer.anchoredPosition = new Vector2(0, 52); timer.sizeDelta = new Vector2(-8, 28);
                var tt = Txt(timer, s.timerText ?? "", 22, FontStyle.Bold, Amber, TextAnchor.MiddleCenter);

                var track = Mk("Track", card);
                track.anchorMin = new Vector2(0, 0); track.anchorMax = new Vector2(1, 0); track.pivot = new Vector2(0.5f, 0);
                track.anchoredPosition = new Vector2(0, 40); track.sizeDelta = new Vector2(-24, 8);
                Img(track, Round, new Color(0f, 0f, 0f, 0.45f));
                var fill = Mk("Fill", track);
                fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(Mathf.Clamp01(s.progress), 1f);
                fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
                Img(fill, Round, Amber);
                refs.slots.Add(new SlotRef { timer = tt, fill = fill });
            }
            else
            {
                ItemIcon(icon, s.output);
                Txt(label, "받기!", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                refs.slots.Add(new SlotRef());
            }
        }

        static void BuildUpgrade(RectTransform right, UpgradeInfo u)
        {
            var box = Mk("Upgrade", right);
            box.anchorMin = new Vector2(0, 0); box.anchorMax = new Vector2(1, 0); box.pivot = new Vector2(0.5f, 0);
            box.anchoredPosition = new Vector2(0, 16); box.sizeDelta = new Vector2(-28, 146);
            Img(box, Round, new Color(0.10f, 0.08f, 0.07f, 0.9f));

            var title = Mk("Title", box);
            title.anchorMin = new Vector2(0, 1); title.anchorMax = new Vector2(1, 1); title.pivot = new Vector2(0.5f, 1);
            title.anchoredPosition = new Vector2(0, -8); title.sizeDelta = new Vector2(-24, 26);
            Txt(title, u.title ?? "칸 늘리기", 17, FontStyle.Bold, Amber, TextAnchor.MiddleLeft);

            if (u.maxed)
            {
                var mx = Mk("Max", box); Stretch(mx, 12, 12, 12, 12);
                Txt(mx, "더 늘릴 수 없습니다 (최대 칸 수)", 17, FontStyle.Bold, Dim, TextAnchor.MiddleCenter);
                return;
            }

            // 재료 줄 (가로로 나열): [아이콘] 이름 x필요 (보유)
            var costs = Mk("Costs", box);
            costs.anchorMin = new Vector2(0, 1); costs.anchorMax = new Vector2(1, 1); costs.pivot = new Vector2(0.5f, 1);
            costs.anchoredPosition = new Vector2(0, -38); costs.sizeDelta = new Vector2(-24, 40);
            var hl = costs.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 12; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = false; hl.childControlHeight = false; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            foreach (CostLine c in u.costs)
            {
                bool enough = c.have >= c.need;
                var cell = Mk("Cost", costs);
                var le = cell.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 128; le.preferredHeight = 38;
                cell.sizeDelta = new Vector2(128, 38);
                var ic = Mk("Icon", cell); ic.anchorMin = ic.anchorMax = new Vector2(0, 0.5f); ic.pivot = new Vector2(0, 0.5f);
                ic.sizeDelta = new Vector2(32, 32); ItemIcon(ic, c.item);
                var tx = Mk("Text", cell); tx.anchorMin = Vector2.zero; tx.anchorMax = Vector2.one; tx.offsetMin = new Vector2(36, 0); tx.offsetMax = Vector2.zero;
                var t = Txt(tx, c.need + "  <size=12>(" + c.have + ")</size>", 17, FontStyle.Bold, enough ? Green : Red, TextAnchor.MiddleLeft);
                t.supportRichText = true;
            }

            var b = MkButton(box, "UpgradeButton", u.canAfford ? "칸 늘리기" : "재료가 부족합니다", BtnGreen, 18, () => u.onClick?.Invoke(), u.canAfford);
            var brt = (RectTransform)b.transform;
            brt.anchorMin = new Vector2(0.5f, 0); brt.anchorMax = new Vector2(0.5f, 0); brt.pivot = new Vector2(0.5f, 0);
            brt.anchoredPosition = new Vector2(0, 10); brt.sizeDelta = new Vector2(240, 42);
        }

        static void BuildMachine(RectTransform panel, ProcessModel m, Refs refs)
        {
            var right = Panel(panel, "Machine", new Color(0.15f, 0.12f, 0.10f, 1f));
            right.anchorMin = new Vector2(0.56f, 0); right.anchorMax = new Vector2(1, 1);
            right.offsetMin = new Vector2(8, 14); right.offsetMax = new Vector2(-16, -16);

            if (m.slots != null)
            {
                BuildSlotMachine(right, m, refs);
                return;
            }

            // 시설 머리말
            var head = Mk("Head", right);
            head.anchorMin = new Vector2(0, 1); head.anchorMax = new Vector2(1, 1); head.pivot = new Vector2(0.5f, 1);
            head.anchoredPosition = new Vector2(0, -16); head.sizeDelta = new Vector2(-40, 52);
            if (m.titleIcon != null)
            {
                var ic = Mk("Icon", head); ic.anchorMin = ic.anchorMax = new Vector2(0, 0.5f); ic.pivot = new Vector2(0, 0.5f);
                ic.sizeDelta = new Vector2(48, 48);
                var im = Img(ic, m.titleIcon, Color.white, false); im.preserveAspect = true;
            }
            var ht = Mk("Name", head); ht.anchorMin = Vector2.zero; ht.anchorMax = Vector2.one; ht.offsetMin = new Vector2(m.titleIcon != null ? 60 : 0, 0); ht.offsetMax = Vector2.zero;
            Txt(ht, m.title, 26, FontStyle.Bold, Cream, TextAnchor.MiddleLeft);

            // 슬롯 줄: [입력] → 가운데 → [결과]
            var inSlot = Slot(right, "InSlot", new Vector2(0, 0.5f), new Vector2(112, 0), m.inItem, m.inText, false);
            if (m.onInputClick != null)
            {
                var inImg = inSlot.GetComponent<Image>();
                var ib = inSlot.gameObject.AddComponent<Button>(); ib.targetGraphic = inImg;
                var icb = ib.colors; icb.highlightedColor = CardHover; icb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); ib.colors = icb;
                Action pull = m.onInputClick;
                ib.onClick.AddListener(() => pull());
            }
            var outSlot = Slot(right, "OutSlot", new Vector2(1, 0.5f), new Vector2(-112, 0), m.outItem, m.outText, m.outClaim);
            if (m.outClaim && m.onClaim != null)
            {
                var img = outSlot.GetComponent<Image>();
                var b = outSlot.gameObject.AddComponent<Button>(); b.targetGraphic = img;
                var cb = b.colors; cb.highlightedColor = CardHover; cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); b.colors = cb;
                Action claim = m.onClaim;
                b.onClick.AddListener(() => claim());
            }

            var mid = Mk("Mid", right);
            mid.anchorMin = mid.anchorMax = new Vector2(0.5f, 0.5f); mid.pivot = new Vector2(0.5f, 0.5f);
            mid.anchoredPosition = Vector2.zero; mid.sizeDelta = new Vector2(150, 150);

            if (Tri != null)
            {
                var ar = Mk("Arrow", mid);
                ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 1); ar.pivot = new Vector2(0.5f, 1);
                ar.anchoredPosition = new Vector2(0, -2); ar.sizeDelta = new Vector2(22, 22);
                ar.localRotation = Quaternion.Euler(0, 0, -90f);
                Img(ar, Tri, new Color(Cream.r, Cream.g, Cream.b, 0.7f), false);
            }

            var timer = Mk("Timer", mid);
            timer.anchorMin = new Vector2(0, 0.5f); timer.anchorMax = new Vector2(1, 0.5f); timer.pivot = new Vector2(0.5f, 0.5f);
            timer.anchoredPosition = new Vector2(0, 14); timer.sizeDelta = new Vector2(0, 46);
            refs.timer = Txt(timer, m.timerText ?? "", 32, FontStyle.Bold, Amber, TextAnchor.MiddleCenter);

            if (m.progress >= 0f)
            {
                var track = Mk("Track", mid);
                track.anchorMin = new Vector2(0, 0.5f); track.anchorMax = new Vector2(1, 0.5f); track.pivot = new Vector2(0.5f, 0.5f);
                track.anchoredPosition = new Vector2(0, -16); track.sizeDelta = new Vector2(-6, 12);
                Img(track, Round, new Color(0f, 0f, 0f, 0.45f));
                var fill = Mk("Fill", track);
                fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(Mathf.Clamp01(m.progress), 1f);
                fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
                Img(fill, Round, Amber);
                refs.fill = fill;
            }

            if (!string.IsNullOrEmpty(m.statusText))
            {
                var st = Mk("Status", mid);
                st.anchorMin = new Vector2(0, 0); st.anchorMax = new Vector2(1, 0); st.pivot = new Vector2(0.5f, 0);
                st.anchoredPosition = new Vector2(0, 0); st.sizeDelta = new Vector2(0, 26);
                Txt(st, m.statusText, 15, FontStyle.Normal, Dim, TextAnchor.MiddleCenter);
            }

            // 아래: 메시지 + 버튼
            if (!string.IsNullOrEmpty(m.message))
            {
                var msg = Mk("Message", right);
                msg.anchorMin = new Vector2(0, 0); msg.anchorMax = new Vector2(1, 0); msg.pivot = new Vector2(0.5f, 0);
                msg.anchoredPosition = new Vector2(0, m.onButton != null ? 92 : 24); msg.sizeDelta = new Vector2(-40, 52);
                var t = Txt(msg, m.message, 16, FontStyle.Bold, new Color(1f, 0.82f, 0.45f), TextAnchor.MiddleCenter);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            if (m.onButton != null)
            {
                var b = MkButton(right, "ProcessButton", m.buttonLabel ?? "가공하기", BtnGreen, 22, () => m.onButton(), m.buttonEnabled);
                var brt = (RectTransform)b.transform;
                brt.anchorMin = new Vector2(0.5f, 0); brt.anchorMax = new Vector2(0.5f, 0); brt.pivot = new Vector2(0.5f, 0);
                brt.anchoredPosition = new Vector2(0, 24); brt.sizeDelta = new Vector2(260, 56);
            }
        }

        static RectTransform Slot(RectTransform parent, string name, Vector2 anchor, Vector2 pos, ItemData item, string text, bool claim)
        {
            var slot = Mk(name, parent);
            slot.anchorMin = slot.anchorMax = anchor; slot.pivot = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = pos; slot.sizeDelta = new Vector2(124, 148);
            Img(slot, Round, claim ? new Color(0.22f, 0.44f, 0.28f, 1f) : new Color(0.09f, 0.075f, 0.065f, 1f), true, true);
            if (Ring != null)
            {
                var ring = Mk("Ring", slot); Stretch(ring);
                Img(ring, Ring, claim ? Green : new Color(1, 1, 1, 0.12f));
            }
            var ic = Mk("Icon", slot);
            ic.anchorMin = ic.anchorMax = new Vector2(0.5f, 1); ic.pivot = new Vector2(0.5f, 1);
            ic.anchoredPosition = new Vector2(0, -16); ic.sizeDelta = new Vector2(76, 76);
            if (item != null) ItemIcon(ic, item);
            else
            {
                var q = Txt(ic, "?", 40, FontStyle.Bold, new Color(1, 1, 1, 0.18f), TextAnchor.MiddleCenter);
            }
            var tx = Mk("Text", slot);
            tx.anchorMin = new Vector2(0, 0); tx.anchorMax = new Vector2(1, 0); tx.pivot = new Vector2(0.5f, 0);
            tx.anchoredPosition = new Vector2(0, 10); tx.sizeDelta = new Vector2(-8, 44);
            var t = Txt(tx, text ?? "", 17, FontStyle.Bold, claim ? Color.white : Cream, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return slot;
        }
    }
}
