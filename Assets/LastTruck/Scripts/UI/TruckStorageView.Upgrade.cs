using System;
using System.Collections.Generic;
using CraftingSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>트럭 창 "강화" 탭 (UI Toolkit): 왼쪽 = 파츠 4자리 + 보관함의 장착 가능한 파츠, 오른쪽 = 스탯 8줄 레벨 강화.</summary>
    public partial class TruckStorageView
    {
        public class PartSlot
        {
            public Sprite slotIcon;     // 자리 모양 (엔진/전면/장갑/지붕)
            public string slotName;
            public ItemData part;       // 끼워진 파츠 (없으면 null)
            public string hover;
            public Action onClick;      // 끼워져 있으면 빼기
        }

        public class PartCandidate
        {
            public ItemData item;
            public int count;
            public string hover;
            public Action onClick;      // 장착
        }

        public class CostChip
        {
            public ItemData item;
            public int have, need;
        }

        public class StatRow
        {
            public Sprite icon;
            public Color iconTint = Color.white;
            public string name;
            public bool locked, maxed;
            public int level, maxLevel;
            public string value;
            public List<CostChip> costs = new List<CostChip>();
            public bool canAfford;
            public string hover;
            public Action onUpgrade;
        }

        public class UpgradeModel
        {
            public List<Tab> tabs = new List<Tab>();
            public Action onClose;
            public List<PartSlot> slots = new List<PartSlot>();
            public List<PartCandidate> candidates = new List<PartCandidate>();
            public List<StatRow> stats = new List<StatRow>();
            public string message;   // 트럭이 없을 때 등
        }

        public void ShowUpgrade(UpgradeModel m)
        {
            VisualElement frame = BeginWindow("upgrade", "트럭 강화", m.onClose);
            if (frame == null) return;

            if (!string.IsNullOrEmpty(m.message))
            {
                frame.Add(MakeLabel(m.message, "tw-empty"));
                EndWindow(m.tabs);
                return;
            }

            var body = Row("tw-craft-body");
            frame.Add(body);

            // 왼쪽: 파츠
            var left = new VisualElement();
            left.AddToClassList("tw-up-parts");
            UiToolkitSkin.Slice(left, _skin.panelBrownDark, 0.35f, new Color(0.40f, 0.32f, 0.28f, 1f));
            left.Add(MakeLabel("파츠", "tw-detail-need"));
            var slotGrid = new VisualElement();
            slotGrid.AddToClassList("tw-up-slots");
            foreach (PartSlot s in m.slots) slotGrid.Add(MakePartSlot(s));
            left.Add(slotGrid);

            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("tw-up-line");
            left.Add(line);
            left.Add(MakeLabel("보관함에 있는 파츠 (누르면 장착)", "tw-up-sub"));
            var candScroll = new ScrollView(ScrollViewMode.Vertical);
            candScroll.AddToClassList("tw-scroll");
            var candGrid = new VisualElement();
            candGrid.AddToClassList("tw-grid");
            foreach (PartCandidate c in m.candidates) candGrid.Add(MakePartCandidate(c));
            if (m.candidates.Count == 0) candGrid.Add(MakeLabel("파츠가 없어요. 제작 탭 → 트럭 강화에서 만들어 보세요", "tw-up-none"));
            candScroll.Add(candGrid);
            left.Add(candScroll);
            body.Add(left);

            // 오른쪽: 스탯 레벨
            var right = new VisualElement();
            right.AddToClassList("tw-up-stats");
            UiToolkitSkin.Slice(right, _skin.panelBrown, 0.4f, new Color(0.55f, 0.45f, 0.38f, 1f));
            right.Add(MakeLabel("레벨 강화", "tw-detail-need"));
            _gridScroll = new ScrollView(ScrollViewMode.Vertical);
            _gridScroll.AddToClassList("tw-scroll");
            foreach (StatRow r in m.stats) _gridScroll.Add(MakeStatRow(r));
            right.Add(_gridScroll);
            body.Add(right);

            _hoverLabel = MakeLabel("파츠 자리를 누르면 빼고, 아래 파츠를 누르면 끼워요. + 를 누르면 레벨이 올라가요 (비용은 트럭 보관함에서).", "tw-hint");
            frame.Add(_hoverLabel);
            EndWindow(m.tabs);
        }

        private VisualElement MakePartSlot(PartSlot s)
        {
            var b = new Button(() => s.onClick?.Invoke()) { text = "" };
            b.AddToClassList("tw-up-slot");
            if (s.part != null) b.AddToClassList("tw-up-slot--on");
            UiToolkitSkin.Slice(b, _skin.panelBrownDark, 0.35f, s.part != null ? new Color(0.62f, 0.86f, 0.62f, 1f) : UiSkinUtil.SlotNormal);

            var big = new VisualElement { pickingMode = PickingMode.Ignore };
            big.AddToClassList("tw-up-slot-icon");
            if (s.part != null && s.part.icon != null) big.style.backgroundImage = new StyleBackground(s.part.icon);
            else if (s.slotIcon != null)
            {
                big.style.backgroundImage = new StyleBackground(s.slotIcon);
                big.style.opacity = 0.3f;
            }
            b.Add(big);
            b.Add(MakeLabel(s.part != null ? s.part.itemName : s.slotName, "tw-card-name"));

            if (s.part != null)
            {
                if (s.slotIcon != null)
                {
                    var tag = new VisualElement { pickingMode = PickingMode.Ignore };
                    tag.AddToClassList("tw-up-slot-tag");
                    tag.style.backgroundImage = new StyleBackground(s.slotIcon);
                    b.Add(tag);
                }
                Sprite x = UiIcons.Get("close");
                if (x != null)
                {
                    var rm = new VisualElement { pickingMode = PickingMode.Ignore };
                    rm.AddToClassList("tw-up-slot-remove");
                    rm.style.backgroundImage = new StyleBackground(x);
                    b.Add(rm);
                }
            }
            b.RegisterCallback<PointerEnterEvent>(_ => SetHint(s.hover));
            return b;
        }

        private VisualElement MakePartCandidate(PartCandidate c)
        {
            var b = new Button(() => c.onClick?.Invoke()) { text = "" };
            b.AddToClassList("tw-up-cand");
            UiToolkitSkin.Slice(b, _skin.buttonBrown, 0.5f, new Color(0.62f, 0.88f, 0.62f, 1f));
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("tw-ing-icon");
            if (c.item != null && c.item.icon != null) icon.style.backgroundImage = new StyleBackground(c.item.icon);
            b.Add(icon);
            b.Add(MakeLabel("×" + c.count, "tw-up-cand-count"));
            b.RegisterCallback<PointerEnterEvent>(_ => SetHint(c.hover));
            return b;
        }

        private VisualElement MakeStatRow(StatRow r)
        {
            var row = Row("tw-stat");
            if (r.locked) row.AddToClassList("tw-stat--locked");
            row.RegisterCallback<PointerEnterEvent>(_ => SetHint(r.hover));

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("tw-stat-icon");
            if (r.icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(r.icon);
                icon.style.unityBackgroundImageTintColor = r.locked ? new Color(1f, 1f, 1f, 0.3f) : r.iconTint;
                row.Add(icon);
            }
            var nameBox = new VisualElement { pickingMode = PickingMode.Ignore };
            nameBox.AddToClassList("tw-stat-namebox");
            nameBox.Add(MakeLabel(r.name, "tw-stat-name"));
            var pips = Row("tw-stat-pips");
            pips.pickingMode = PickingMode.Ignore;
            for (int p = 0; p < r.maxLevel; p++)
            {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.AddToClassList("tw-stat-pip");
                if (p < r.level) pip.AddToClassList("tw-stat-pip--on");
                pips.Add(pip);
            }
            nameBox.Add(pips);
            row.Add(nameBox);

            row.Add(MakeLabel("Lv " + r.level, "tw-stat-level"));
            row.Add(MakeLabel(r.value ?? "", "tw-stat-value"));

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            row.Add(spacer);

            if (r.locked)
            {
                Sprite lk = UiIcons.Get("lock");
                if (lk != null)
                {
                    var l = new VisualElement { pickingMode = PickingMode.Ignore };
                    l.AddToClassList("tw-stat-badge");
                    l.style.backgroundImage = new StyleBackground(lk);
                    row.Add(l);
                }
                else row.Add(MakeLabel("포탑 필요", "tw-stat-value"));
                return row;
            }
            if (r.maxed)
            {
                row.Add(MakeLabel("MAX", "tw-stat-max"));
                return row;
            }

            foreach (CostChip c in r.costs)
            {
                var chip = Row("tw-stat-cost");
                chip.pickingMode = PickingMode.Ignore;
                var ci = new VisualElement { pickingMode = PickingMode.Ignore };
                ci.AddToClassList("tw-stat-cost-icon");
                if (c.item != null && c.item.icon != null) ci.style.backgroundImage = new StyleBackground(c.item.icon);
                chip.Add(ci);
                chip.Add(MakeLabel(c.have + "/" + c.need, c.have >= c.need ? "tw-stat-cost-n tw-ing-count--ok" : "tw-stat-cost-n tw-ing-count--short"));
                row.Add(chip);
            }

            var plus = new Button(() => { if (r.canAfford) r.onUpgrade?.Invoke(); }) { text = "" };
            plus.AddToClassList("tw-stat-plus");
            plus.EnableInClassList("tw-make--off", !r.canAfford);
            UiToolkitSkin.Slice(plus, r.canAfford ? _skin.buttonBrown : _skin.buttonGrey, 0.5f,
                r.canAfford ? new Color(0.50f, 0.88f, 0.56f, 1f) : new Color(0.55f, 0.55f, 0.60f, 1f));
            Sprite ps = UiIcons.Get("plus");
            if (ps != null)
            {
                var pi = new VisualElement { pickingMode = PickingMode.Ignore };
                pi.AddToClassList("tw-stat-plus-icon");
                pi.style.backgroundImage = new StyleBackground(ps);
                plus.Add(pi);
            }
            else plus.Add(MakeLabel("+", "tw-make-text"));
            row.Add(plus);
            return row;
        }
    }
}
