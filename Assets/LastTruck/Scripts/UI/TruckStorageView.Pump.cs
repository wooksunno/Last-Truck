using System;
using System.Collections.Generic;
using CraftingSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 석유 펌프잭 화면 (가공 화면과 같은 틀).
    ///  - 가운데: 땅을 세로로 자른 3D 장면. 땅속 기름층에서 기름이 유정 관을 타고 빨려 올라와 배관을 지나 탱크에 차오른다 (OilPumpDiorama)
    ///    위에는 "기름을 빨아올리는 중..." + 탱크 막대(모인 양) + 다음 1개까지 남은 시간
    ///  - 오른쪽: 모인 기름 개수, 기름 받기 / 펌프잭 회수 버튼
    /// 창을 열어 둔 동안 0.15초마다 값만 바꾼다 (창을 다시 그리지 않음).
    /// </summary>
    public partial class TruckStorageView
    {
        public class PumpModel
        {
            public string title = "석유 펌프잭";
            public OilPumpjack pump;
            public Sprite oilIcon;
            public Action onClose;
            public Action onCollect;
            public Action onRetrieve;
        }

        public void ShowPump(PumpModel m)
        {
            VisualElement frame = BeginWindow("pump", m.title, m.onClose);
            if (frame == null) return;
            OilPumpjack pump = m.pump;

            var body = Row("tw-craft-body");
            frame.Add(body);

            // 가운데: 단면 장면
            var scene = new VisualElement();
            scene.AddToClassList("tw-scene");
            int state = pump != null && !pump.Working ? 2 : 1;
            float fill = pump != null ? pump.Tank / OilPumpjack.Capacity : 0f;
            RenderTexture rt = ProcessDiorama.ShowCustom(OilPumpDiorama.Key, OilPumpDiorama.Build, state, fill);
            if (OilPumpDiorama.Current != null) OilPumpDiorama.Current.SetSource(pump);
            if (rt != null)
            {
                scene.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
                scene.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            }
            else scene.style.backgroundColor = new Color(0.16f, 0.12f, 0.1f);
            body.Add(scene);

            var ui = new VisualElement { pickingMode = PickingMode.Ignore };
            ui.AddToClassList("tw-scene-ui");
            scene.Add(ui);

            var titleRow = Row("tw-scene-title-row");
            titleRow.pickingMode = PickingMode.Ignore;
            titleRow.Add(SceneDash());
            if (m.oilIcon != null)
            {
                var ti = new VisualElement { pickingMode = PickingMode.Ignore };
                ti.AddToClassList("tw-scene-title-icon");
                ti.style.backgroundImage = new StyleBackground(m.oilIcon);
                titleRow.Add(ti);
            }
            Label title = MakeLabel("", "tw-scene-title");
            titleRow.Add(title);
            titleRow.Add(SceneDash());
            ui.Add(titleRow);

            var barRow = Row("tw-scene-bar-row");
            barRow.pickingMode = PickingMode.Ignore;
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("tw-bbar");
            var barFill = new VisualElement { pickingMode = PickingMode.Ignore };
            barFill.AddToClassList("tw-bbar-fill");
            barFill.style.backgroundColor = new Color(0.32f, 0.27f, 0.24f);   // 기름색 막대
            bar.Add(barFill);
            Label barText = MakeLabel("", "tw-bbar-pct");
            bar.Add(barText);
            barRow.Add(bar);
            Label next = MakeLabel("", "tw-bbar-time");
            next.style.width = 110;
            barRow.Add(next);
            ui.Add(barRow);

            var area = new VisualElement { pickingMode = PickingMode.Ignore };
            area.AddToClassList("tw-scene-area");
            area.Add(MakeLabel("땅속 기름층 → 유정 관 → 펌프잭 → 저장 탱크", "tw-scene-idle"));
            ui.Add(area);

            // 오른쪽: 모인 기름 + 버튼
            var right = new VisualElement();
            right.AddToClassList("tw-proc-right");
            UiToolkitSkin.Slice(right, _skin.panelBrown, 0.4f, new Color(0.55f, 0.45f, 0.38f, 1f));
            body.Add(right);

            var box = new VisualElement();
            box.AddToClassList("tw-result");
            UiToolkitSkin.Slice(box, _skin.panelBrownDark, 0.35f, new Color(0.62f, 0.55f, 0.52f, 1f));
            box.Add(MakeLabel("모인 기름", "tw-result-head"));
            var div = new VisualElement { pickingMode = PickingMode.Ignore };
            div.AddToClassList("tw-result-div");
            box.Add(div);
            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            art.AddToClassList("tw-result-art");
            var glow = new VisualElement { pickingMode = PickingMode.Ignore };
            glow.AddToClassList("tw-result-glow");
            glow.style.backgroundColor = new Color(1f, 0.78f, 0.4f, 0.18f);
            art.Add(glow);
            if (m.oilIcon != null)
            {
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("tw-result-icon");
                icon.style.backgroundImage = new StyleBackground(m.oilIcon);
                art.Add(icon);
            }
            box.Add(art);
            Label count = MakeLabel("", "tw-result-name");
            box.Add(count);
            box.Add(MakeLabel($"{OilPumpjack.SecondsPerOil:0}초마다 1개 · 최대 {OilPumpjack.Capacity}개", "tw-result-sub"));
            right.Add(box);

            var buttons = new VisualElement();
            right.Add(buttons);
            Label note = MakeLabel("", "tw-result-sub");
            right.Add(note);

            _hoverLabel = MakeLabel("창을 닫아도 펌프잭은 계속 기름을 모아요. 탱크가 가득 차면 멈춰요.", "tw-hint");
            frame.Add(_hoverLabel);

            int shownReady = -1;
            bool shownWorking = true;
            void Refresh()
            {
                if (pump == null) { m.onClose?.Invoke(); return; }   // 다른 사람이 회수했거나 사라짐
                int ready = pump.Ready;
                bool working = pump.Working;
                float f = pump.Tank / OilPumpjack.Capacity;
                title.text = working ? "기름을 빨아올리는 중..." : "탱크가 가득 찼어요!";
                barFill.style.width = Length.Percent(Mathf.Clamp01(f) * 100f);
                barText.text = $"기름 {ready} / {OilPumpjack.Capacity}";
                next.text = working ? $"다음 1개 {Mathf.CeilToInt(pump.SecondsToNext)}초" : "가득 참";
                count.text = ready > 0 ? $"기름 ×{ready}" : "아직 없어요";
                ProcessDiorama.SetState(working ? 1 : 2, f);

                if (ready == shownReady && working == shownWorking) return;
                shownReady = ready; shownWorking = working;
                buttons.Clear();
                if (ready >= 1)
                {
                    buttons.Add(MakeBigButton("기름 받기 ×" + ready, true, () => { m.onCollect?.Invoke(); Refresh(); }));
                    note.text = "기름을 다 받으면 펌프잭을 회수할 수 있어요";
                }
                else
                {
                    buttons.Add(MakeBigButton("펌프잭 회수", false, () => m.onRetrieve?.Invoke()));
                    note.text = "탱크가 비어 있어요. 회수하면 펌프잭 아이템으로 돌아와요";
                }
                foreach (VisualElement b in buttons.Children()) { b.style.marginTop = 8; b.style.alignSelf = Align.Stretch; }
            }
            Refresh();
            scene.schedule.Execute(() => { if (IsVisible && _mode == "pump") Refresh(); }).Every(150);

            EndWindow(new List<Tab>());
        }
    }
}
