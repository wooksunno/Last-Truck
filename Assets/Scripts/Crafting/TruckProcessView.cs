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

        public class ProcessModel
        {
            public string title;                 // 시설 이름
            public Sprite titleIcon;
            public Color accent = new Color(0.96f, 0.78f, 0.40f);
            public List<TabInfo> tabs;           // 트럭 가공일 때 시설 탭(없으면 null)
            public List<CardInfo> cards = new List<CardInfo>();
            public string listTitle = "가공할 재료";
            public string listHint;
            public ItemData inItem; public string inText;
            public ItemData outItem; public string outText;
            public bool outClaim; public Action onClaim;
            public string timerText; public float progress = -1f;   // progress < 0 이면 막대를 숨긴다
            public string statusText;
            public string message;
            public string buttonLabel; public bool buttonEnabled; public Action onButton;   // 현장 시설용 "가공하기" 버튼
        }

        public class Refs
        {
            public Text timer;
            public RectTransform fill;
            public void SetProgress(float p)
            {
                if (fill == null) return;
                fill.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
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
            g.constraint = GridLayoutGroup.Constraint.Flexible; g.childAlignment = TextAnchor.UpperLeft;
            var csf = grid.gameObject.AddComponent<ContentSizeFitter>(); csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (m.cards.Count == 0)
            {
                var none = Mk("None", area); Stretch(none, 0, 0, 0, 0);
                Txt(none, "가공할 수 있는 재료가 없습니다", 20, FontStyle.Bold, Dim, TextAnchor.MiddleCenter);
            }

            foreach (CardInfo c in m.cards)
                BuildCard(grid, c, m.accent);
        }

        static void BuildCard(RectTransform grid, CardInfo c, Color accent)
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
            inIcon.anchoredPosition = new Vector2(12, -14); inIcon.sizeDelta = new Vector2(52, 52);
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
            outIcon.anchoredPosition = new Vector2(-12, -14); outIcon.sizeDelta = new Vector2(52, 52);
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

        static void BuildMachine(RectTransform panel, ProcessModel m, Refs refs)
        {
            var right = Panel(panel, "Machine", new Color(0.15f, 0.12f, 0.10f, 1f));
            right.anchorMin = new Vector2(0.56f, 0); right.anchorMax = new Vector2(1, 1);
            right.offsetMin = new Vector2(8, 14); right.offsetMax = new Vector2(-16, -16);

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
