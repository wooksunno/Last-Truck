using System;
using CraftingSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 맵에 있는 가공 시설(현장 모닥불 등) 창 (UI Toolkit): 왼쪽 = 가공할 재료 카드, 오른쪽 = 재료 → 결과, 남은 시간, "가공하기".
    /// 값은 GameUIController가 TruckProcessView.ProcessModel(현장 시설용 한 줄 화면 값)로 넘겨 준다.
    /// </summary>
    public partial class TruckStorageView
    {
        private VisualElement _facilityFill;
        private Label _facilityTimer;

        public void ShowFacility(TruckProcessView.ProcessModel m, Action onClose)
        {
            VisualElement frame = BeginWindow("facility", m.title ?? "가공", onClose);
            if (frame == null) return;
            _window.AddToClassList("tw-window--compact");

            var body = Row("tw-craft-body");
            frame.Add(body);

            // 왼쪽: 재료 카드
            var listPanel = new VisualElement();
            listPanel.AddToClassList("tw-craft-list");
            UiToolkitSkin.Slice(listPanel, _skin.panelBrownDark, 0.35f, new Color(0.40f, 0.32f, 0.28f, 1f));
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("tw-craft-line");
            line.style.backgroundColor = m.accent;
            listPanel.Add(line);
            var head = Row("tw-proc-head");
            if (m.titleIcon != null)
            {
                var ti = new VisualElement { pickingMode = PickingMode.Ignore };
                ti.AddToClassList("tw-proc-title-icon");
                ti.style.backgroundImage = new StyleBackground(m.titleIcon);
                head.Add(ti);
            }
            head.Add(MakeLabel(m.listTitle ?? "가공할 재료", "tw-proc-title"));
            listPanel.Add(head);
            _gridScroll = new ScrollView(ScrollViewMode.Vertical);
            _gridScroll.AddToClassList("tw-scroll");
            var grid = new VisualElement();
            grid.AddToClassList("tw-grid");
            foreach (var c in m.cards) grid.Add(MakeProcessCard(c, m.accent));
            if (m.cards.Count == 0) grid.Add(MakeLabel("이 시설에서 가공할 수 있는 재료가 없어요", "tw-empty"));
            _gridScroll.Add(grid);
            listPanel.Add(_gridScroll);
            body.Add(listPanel);

            // 오른쪽: 재료 → 결과
            var right = new VisualElement();
            right.AddToClassList("tw-fac-right");
            UiToolkitSkin.Slice(right, _skin.panelBrown, 0.4f, new Color(0.55f, 0.45f, 0.38f, 1f));
            body.Add(right);

            var flow = Row("tw-fac-flow");
            flow.Add(FacilitySlot(m.inItem, m.inText, m.onInputClick));
            flow.Add(MakeLabel("→", "tw-fac-arrow"));
            flow.Add(FacilitySlot(m.outItem, m.outText, m.outClaim ? m.onClaim : null));
            right.Add(flow);

            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("tw-fac-bar");
            _facilityFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _facilityFill.AddToClassList("tw-pslot-fill");
            _facilityFill.style.width = Length.Percent(Mathf.Clamp01(Mathf.Max(0f, m.progress)) * 100f);
            bar.Add(_facilityFill);
            bar.style.display = m.progress >= 0f ? DisplayStyle.Flex : DisplayStyle.None;
            right.Add(bar);

            _facilityTimer = MakeLabel(m.timerText ?? "", "tw-fac-timer");
            right.Add(_facilityTimer);
            if (!string.IsNullOrEmpty(m.statusText)) right.Add(MakeLabel(m.statusText, "tw-fac-status"));
            if (!string.IsNullOrEmpty(m.message)) right.Add(MakeLabel(m.message, "tw-proc-message"));

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            right.Add(spacer);

            var btn = new Button(() => { if (m.buttonEnabled) m.onButton?.Invoke(); }) { text = "" };
            btn.AddToClassList("tw-make");
            btn.AddToClassList("tw-fac-button");
            btn.EnableInClassList("tw-make--off", !m.buttonEnabled);
            UiToolkitSkin.Slice(btn, m.buttonEnabled ? _skin.buttonBrown : _skin.buttonGrey, 0.5f,
                m.buttonEnabled ? new Color(0.50f, 0.88f, 0.56f, 1f) : new Color(0.55f, 0.55f, 0.60f, 1f));
            btn.Add(MakeLabel(m.buttonLabel ?? "가공하기", "tw-make-text"));
            right.Add(btn);

            _hoverLabel = MakeLabel(m.listHint ?? "", "tw-hint");
            frame.Add(_hoverLabel);
            EndWindow(new System.Collections.Generic.List<Tab>());
        }

        /// <summary>현장 시설: 가공 중 남은 시간/진행도를 매 프레임 갱신한다.</summary>
        public void SetFacilityProgress(string timerText, float progress)
        {
            if (_mode != "facility" || _facilityTimer == null) return;
            _facilityTimer.text = timerText;
            if (_facilityFill != null) _facilityFill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
        }

        private VisualElement FacilitySlot(ItemData item, string text, Action onClick)
        {
            VisualElement box = onClick != null ? new Button(() => onClick()) { text = "" } : new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList("tw-fac-slot");
            UiToolkitSkin.Slice(box, _skin.panelBrownDark, 0.35f, item != null ? UiSkinUtil.SlotFilled : UiSkinUtil.SlotNormal);
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("tw-fac-slot-icon");
            if (item != null && item.icon != null) icon.style.backgroundImage = new StyleBackground(item.icon);
            box.Add(icon);
            box.Add(MakeLabel(text ?? "", "tw-fac-slot-text"));
            return box;
        }
    }
}
