using System;
using CraftingSystem;
using UnityEngine;
using UnityEngine.UIElements;
using PView = CraftingSystem.TruckProcessView;

namespace LastTruck
{
    /// <summary>
    /// 가공 화면의 "장면" 레이아웃 (시안 이미지처럼):
    ///   왼쪽 인벤토리 | 가운데 설비 장면(위에 "가공 중..." 진행 막대, 가운데 큰 설비 연출, 아래 취소/받기) | 오른쪽 "결과 예상".
    /// 설비마다 배경 색/소품이 다르다. 설비별 일러스트 이미지가 있으면 그것을 배경으로 쓴다:
    ///   Assets/Resources/UI/ProcessScenes/{Campfire|Grindstone|RollerPress|PrecisionCutter|ChemicalRefinery|SuperheatedFurnace|LeatherTanningRack}.png
    ///   (Sprite로 임포트하면 자동으로 적용. 없으면 코드로 그린 배경을 쓴다.)
    /// </summary>
    public partial class TruckStorageView
    {
        private const float SceneK = 3.5f;   // 설비 연출(작은 무대)을 이만큼 키워서 가운데에 보여 준다

        private struct ScenePalette
        {
            public Color sky, floor, line, rock, prop;
            public bool grass, crates, drums;
        }

        private static ScenePalette PaletteOf(FacilityType f)
        {
            switch (f)
            {
                case FacilityType.Campfire:
                    return new ScenePalette { sky = C(0.13f, 0.21f, 0.15f), floor = C(0.30f, 0.23f, 0.14f), line = C(0.20f, 0.30f, 0.17f), rock = C(0.55f, 0.55f, 0.62f), prop = C(0.36f, 0.24f, 0.13f), grass = true, crates = true };
                case FacilityType.Grindstone:
                    return new ScenePalette { sky = C(0.20f, 0.14f, 0.10f), floor = C(0.34f, 0.23f, 0.14f), line = C(0.26f, 0.18f, 0.11f), rock = C(0.5f, 0.48f, 0.46f), prop = C(0.42f, 0.28f, 0.15f), crates = true };
                case FacilityType.RollerPress:
                    return new ScenePalette { sky = C(0.15f, 0.17f, 0.19f), floor = C(0.27f, 0.29f, 0.31f), line = C(0.20f, 0.22f, 0.24f), rock = C(0.5f, 0.53f, 0.56f), prop = C(0.32f, 0.36f, 0.4f), drums = true };
                case FacilityType.PrecisionCutter:
                    return new ScenePalette { sky = C(0.12f, 0.15f, 0.21f), floor = C(0.22f, 0.26f, 0.33f), line = C(0.17f, 0.20f, 0.27f), rock = C(0.46f, 0.5f, 0.58f), prop = C(0.28f, 0.33f, 0.42f), drums = true, crates = true };
                case FacilityType.ChemicalRefinery:
                    return new ScenePalette { sky = C(0.07f, 0.15f, 0.19f), floor = C(0.12f, 0.22f, 0.26f), line = C(0.09f, 0.18f, 0.22f), rock = C(0.35f, 0.5f, 0.55f), prop = C(0.16f, 0.32f, 0.38f), drums = true };
                case FacilityType.SuperheatedFurnace:
                    return new ScenePalette { sky = C(0.21f, 0.10f, 0.08f), floor = C(0.32f, 0.17f, 0.12f), line = C(0.26f, 0.13f, 0.09f), rock = C(0.5f, 0.38f, 0.34f), prop = C(0.38f, 0.2f, 0.14f), crates = true };
                default:
                    return new ScenePalette { sky = C(0.20f, 0.18f, 0.11f), floor = C(0.37f, 0.30f, 0.17f), line = C(0.27f, 0.25f, 0.14f), rock = C(0.55f, 0.5f, 0.42f), prop = C(0.42f, 0.30f, 0.16f), grass = true, crates = true };
            }
        }

        private static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

        private static VisualElement Fill(VisualElement parent, Color c)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
            e.style.backgroundColor = c;
            parent.Add(e);
            return e;
        }

        private static VisualElement Blob(VisualElement parent, float leftPct, float bottomPct, float w, float h, Color c, float radius)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = Length.Percent(leftPct); e.style.bottom = Length.Percent(bottomPct);
            e.style.width = w; e.style.height = h;
            e.style.backgroundColor = c;
            e.style.borderTopLeftRadius = radius; e.style.borderTopRightRadius = radius;
            e.style.borderBottomLeftRadius = radius; e.style.borderBottomRightRadius = radius;
            parent.Add(e);
            return e;
        }

        /// <summary>설비 일러스트(Resources/UI/ProcessScenes/설비이름)가 있으면 그것을, 없으면 코드로 그린 배경(하늘/바닥/바위/풀/상자/드럼통)을 깐다.</summary>
        private void BuildSceneBackdrop(VisualElement scene, FacilityType f)
        {
            ScenePalette p = PaletteOf(f);
            Fill(scene, p.sky);

            // 바닥 + 지평선
            var floor = new VisualElement { pickingMode = PickingMode.Ignore };
            floor.style.position = Position.Absolute;
            floor.style.left = 0; floor.style.right = 0; floor.style.bottom = 0;
            floor.style.height = Length.Percent(52);
            floor.style.backgroundColor = p.floor;
            scene.Add(floor);
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.style.position = Position.Absolute;
            line.style.left = 0; line.style.right = 0; line.style.bottom = Length.Percent(52);
            line.style.height = 26;
            line.style.backgroundColor = p.line;
            scene.Add(line);

            if (Resources.Load<Sprite>("UI/ProcessScenes/" + f) != null) return;   // 일러스트가 있으면 소품은 그림에 이미 있다

            var rng = new System.Random((int)f * 977 + 13);
            Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);

            // 뒤쪽 소품 (지평선 위)
            if (p.crates)
                for (int i = 0; i < 3; i++)
                    Blob(scene, i == 0 ? 3f : R(70f, 88f), 51f + R(0f, 3f), R(46f, 70f), R(40f, 60f), p.prop * R(0.8f, 1.1f), 4f);
            if (p.drums)
                for (int i = 0; i < 4; i++)
                    Blob(scene, i < 2 ? R(2f, 14f) : R(80f, 92f), 51f + R(0f, 2f), R(30f, 40f), R(54f, 80f), p.prop * R(0.8f, 1.15f), 8f);

            // 바닥 바위
            for (int i = 0; i < 9; i++)
            {
                bool edge = i % 2 == 0;
                float x = edge ? (i % 4 == 0 ? R(0f, 12f) : R(78f, 94f)) : R(0f, 94f);
                float y = edge ? R(20f, 44f) : R(2f, 12f);
                Color rc = p.rock * R(0.75f, 1.05f); rc.a = 1f;
                Blob(scene, x, y, R(26f, 64f), R(16f, 34f), rc, 14f);
            }

            // 풀
            if (p.grass)
                for (int i = 0; i < 12; i++)
                {
                    Color gc = C(R(0.18f, 0.28f), R(0.42f, 0.58f), R(0.18f, 0.26f));
                    Blob(scene, R(0f, 96f), R(0f, 40f), R(6f, 10f), R(14f, 30f), gc, 4f);
                }
        }

        // ---------------------------------------------------------------- 가운데 장면

        private VisualElement MakeProcessScene(PView.ProcessModel m, PView.SlotInfo sl, ProcSlotEl el, VisualElement tiles)
        {
            var scene = new VisualElement();
            scene.AddToClassList("tw-scene");

            bool idle0 = sl == null || sl.state == 0;
            // 움직이는 3D 미니 장면(진짜 모델 + 파티클)이 있으면 그 화면을 배경으로 깐다. 없으면 예전처럼 코드로 그린 장면
            RenderTexture diorama = ProcessDiorama.Show(m.facility,
                idle0 ? null : (sl.input ?? sl.inItem), idle0 ? null : sl.output,
                idle0 ? 0 : sl.state, idle0 ? 0f : sl.progress, !idle0 && sl.fresh);
            if (diorama != null)
            {
                scene.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(diorama));
                scene.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                if (!idle0 && sl.state == 1 && el != null) el.onProgress = ProcessDiorama.SetProgress;
            }
            else BuildSceneBackdrop(scene, m.facility);

            var ui = new VisualElement();
            ui.AddToClassList("tw-scene-ui");
            ui.pickingMode = PickingMode.Ignore;
            scene.Add(ui);

            bool idle = sl == null || sl.state == 0;
            bool done = !idle && sl.state == 2;

            // 위: "— 가공 중... —" + 진행 막대(퍼센트) + 남은 시간
            var titleRow = Row("tw-scene-title-row");
            titleRow.pickingMode = PickingMode.Ignore;
            titleRow.Add(SceneDash());
            if (m.titleIcon != null)
            {
                var ti = new VisualElement { pickingMode = PickingMode.Ignore };
                ti.AddToClassList("tw-scene-title-icon");
                ti.style.backgroundImage = new StyleBackground(m.titleIcon);
                titleRow.Add(ti);
            }
            titleRow.Add(MakeLabel(idle ? (m.title ?? "") + " 비어 있음" : (done ? "완성!" : "가공 중..."), "tw-scene-title"));
            titleRow.Add(SceneDash());
            ui.Add(titleRow);

            float p = idle ? 0f : (done ? 1f : Mathf.Clamp01(sl.progress));
            var barRow = Row("tw-scene-bar-row");
            barRow.pickingMode = PickingMode.Ignore;
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("tw-bbar");
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("tw-bbar-fill");
            fill.style.width = Length.Percent(p * 100f);
            bar.Add(fill);
            var pct = MakeLabel(Mathf.FloorToInt(p * 100f) + "%", "tw-bbar-pct");
            bar.Add(pct);
            barRow.Add(bar);
            var time = MakeLabel(idle ? "" : (done ? "완료" : (sl.timerText ?? "")), "tw-bbar-time");
            barRow.Add(time);
            ui.Add(barRow);
            if (!idle && !done && el != null) { el.bigFill = fill; el.bigPercent = pct; el.bigTimer = time; }

            // 가운데: 설비 연출 (작은 무대를 크게)
            var area = new VisualElement { pickingMode = PickingMode.Ignore };
            area.AddToClassList("tw-scene-area");
            var stageBox = new VisualElement { pickingMode = PickingMode.Ignore };
            stageBox.style.width = StageW * SceneK;
            stageBox.style.height = StageH * SceneK;
            stageBox.style.flexShrink = 0;
            area.Add(stageBox);

            ItemData input = idle ? null : (sl.input ?? sl.inItem);

            if (diorama != null)
            {
                // 3D 장면이 배경이라 가운데는 비워 두고 글자/버튼만 얹는다
                area.Remove(stageBox);
                if (idle) area.Add(MakeLabel("왼쪽 인벤토리에서 아이템을 누르면 여기서 가공해요", "tw-scene-idle"));
                ui.Add(area);
                AddSceneButtons(ui, sl, idle);
                ui.Add(tiles);
                return scene;
            }

            // 설비 일러스트가 있으면 그 그림 위에 재료/불똥/빛만 움직인다 (완성 연출은 기존 것)
            Sprite art = Resources.Load<Sprite>("UI/ProcessScenes/" + m.facility);
            if (art != null && !done)
            {
                area.Remove(stageBox);
                area.Insert(0, MakeArtStage(art, m.facility, input, !idle && sl.fresh, idle ? 0f : sl.progress, idle ? null : el));
                if (idle) area.Add(MakeLabel("왼쪽 인벤토리에서 아이템을 누르면 여기서 가공해요", "tw-scene-idle"));
                ui.Add(area);
                AddSceneButtons(ui, sl, idle);
                ui.Add(tiles);
                return scene;
            }
            var stage = MakeProcessStage(m.facility, input, idle ? null : sl.output, done, !idle && sl.fresh, idle ? 0f : sl.progress, idle ? null : el);
            stage.style.marginBottom = 0;
            stage.style.backgroundColor = Color.clear;   // 배경 장면이 비쳐 보이게
            stage.style.borderTopLeftRadius = 0; stage.style.borderTopRightRadius = 0;
            stage.style.borderBottomLeftRadius = 0; stage.style.borderBottomRightRadius = 0;
            stage.style.overflow = Overflow.Visible;
            stage.style.position = Position.Absolute;
            stage.style.left = 0; stage.style.top = 0;
            stage.style.transformOrigin = new TransformOrigin(0f, 0f);
            stage.style.scale = new Scale(new Vector3(SceneK, SceneK, 1f));
            stageBox.Add(stage);
            if (idle)
                area.Add(MakeLabel("왼쪽 인벤토리에서 아이템을 누르면 여기서 가공해요", "tw-scene-idle"));
            ui.Add(area);

            AddSceneButtons(ui, sl, idle);
            ui.Add(tiles);
            return scene;
        }

        /// <summary>아래: 취소 / 받기</summary>
        private void AddSceneButtons(VisualElement ui, PView.SlotInfo sl, bool idle)
        {
            if (idle) return;
            var btns = Row("tw-bbtns");
            if (sl.onCancel != null) btns.Add(MakeBigButton("취소", false, sl.onCancel));
            if (sl.onClaim != null) btns.Add(MakeBigButton("받기 ×" + Mathf.Max(1, sl.readyCount), true, sl.onClaim));
            if (btns.childCount > 0) ui.Add(btns);
        }

        // ---------------------------------------------------------------- 일러스트 장면 (그림 위에 재료/불똥/빛)

        /// <summary>그림 안에서 재료가 놓일 자리(0~1)와 크기(그림 너비 비율), 불/열기가 올라오는 자리.</summary>
        private static void ArtAnchors(FacilityType f, out Vector2 item, out float itemSize, out Vector2 fire)
        {
            switch (f)
            {
                case FacilityType.Campfire: item = new Vector2(0.496f, 0.29f); itemSize = 0.17f; fire = new Vector2(0.54f, 0.62f); return;
                default: item = new Vector2(0.5f, 0.42f); itemSize = 0.16f; fire = new Vector2(0.5f, 0.6f); return;
            }
        }

        private VisualElement MakeArtStage(Sprite art, FacilityType f, ItemData input, bool fresh, float progress, ProcSlotEl el)
        {
            const float W = 520f;
            float H = W * art.rect.height / art.rect.width;
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.width = W; box.style.height = H; box.style.flexShrink = 0;
            var img = new VisualElement { pickingMode = PickingMode.Ignore };
            img.style.position = Position.Absolute;
            img.style.left = 0; img.style.top = 0; img.style.right = 0; img.style.bottom = 0;
            img.style.backgroundImage = new StyleBackground(art);
            img.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            box.Add(img);

            ArtAnchors(f, out Vector2 itemAt, out float itemRel, out Vector2 fireAt);
            float isz = W * itemRel;
            var st = new StageState { progress = progress, fresh = fresh, birth = Time.realtimeSinceStartup };
            if (el != null) el.onProgress = p => st.progress = p;

            var glow = Part(box, W * 0.5f, W * 0.34f, new Color(1f, 0.5f, 0.12f, 0.16f), W * 0.25f);
            var icon = StageIcon(box, input, isz);
            var embers = new VisualElement[12];
            for (int i = 0; i < embers.Length; i++) embers[i] = Part(box, 5, 5, new Color(1f, 0.72f, 0.28f, 1f), 3);
            var steam = new VisualElement[4];
            for (int i = 0; i < steam.Length; i++) steam[i] = Part(box, 16, 16, new Color(1f, 1f, 1f, 0.25f), 8);

            Action<float> tick = t =>
            {
                float pop = st.Pop(t);
                Place(glow, fireAt.x * W - W * 0.25f, fireAt.y * H - W * 0.17f, 0, 1f + 0.1f * Wave(t, 6f), 1f + 0.1f * Wave(t, 5f, 1f), 0.8f + 0.2f * Wave(t, 7f));
                float bob = Wave(t, 2.2f) * 3f;
                Place(icon, itemAt.x * W - isz * 0.5f, itemAt.y * H - isz * 0.5f + bob, Wave(t, 1.5f) * 6f, pop, pop);
                if (f == FacilityType.Campfire)
                    icon.style.unityBackgroundImageTintColor = Color.Lerp(Color.white, new Color(0.65f, 0.45f, 0.32f, 1f), Mathf.Clamp01(st.progress));   // 구워질수록 갈색으로
                for (int i = 0; i < embers.Length; i++)
                {
                    float ph = (t * 0.55f + i * 0.083f) % 1f;
                    Place(embers[i], fireAt.x * W + Wave(t, 1.7f, i * 2.3f) * W * 0.12f + (i - 6) * 6f, fireAt.y * H - ph * H * 0.55f, 0, 1f, 1f, (1f - ph) * 0.9f);
                }
                for (int i = 0; i < steam.Length; i++)
                {
                    float ph = (t * 0.35f + i * 0.25f) % 1f;
                    float k = 0.6f + ph * 1.4f;
                    Place(steam[i], itemAt.x * W - 30f + i * 20f + Wave(t, 2.5f, i) * 6f, itemAt.y * H - isz * 0.4f - ph * H * 0.2f, 0, k, k, 0.35f * (1f - ph) * Mathf.Clamp01(st.progress * 3f + 0.2f));
                }
            };
            tick(Time.realtimeSinceStartup);
            box.schedule.Execute(() => tick(Time.realtimeSinceStartup)).Every(33);
            return box;
        }

        private VisualElement SceneDash()
        {
            var d = new VisualElement { pickingMode = PickingMode.Ignore };
            d.AddToClassList("tw-scene-dash");
            return d;
        }

        private VisualElement MakeBigButton(string text, bool positive, Action onClick)
        {
            var btn = new Button(() => onClick?.Invoke()) { text = "" };
            btn.AddToClassList("tw-make");
            btn.AddToClassList("tw-bbtn");
            UiToolkitSkin.Slice(btn, positive ? _skin.buttonBrown : _skin.buttonGrey, 0.5f,
                positive ? new Color(0.50f, 0.88f, 0.56f, 1f) : new Color(0.95f, 0.62f, 0.55f, 1f));
            btn.Add(MakeLabel(text, "tw-make-text"));
            return btn;
        }

        // ---------------------------------------------------------------- 오른쪽 "결과 예상"

        private VisualElement MakeResultPanel(PView.ProcessModel m, PView.SlotInfo sl, ProcSlotEl el)
        {
            var box = new VisualElement();
            box.AddToClassList("tw-result");
            UiToolkitSkin.Slice(box, _skin.panelBrownDark, 0.35f, new Color(0.62f, 0.55f, 0.52f, 1f));
            box.Add(MakeLabel("결과 예상", "tw-result-head"));
            var div = new VisualElement { pickingMode = PickingMode.Ignore };
            div.AddToClassList("tw-result-div");
            box.Add(div);

            bool idle = sl == null || sl.state == 0;
            var stageArea = new VisualElement { pickingMode = PickingMode.Ignore };
            stageArea.AddToClassList("tw-result-art");
            var glow = new VisualElement { pickingMode = PickingMode.Ignore };
            glow.AddToClassList("tw-result-glow");
            stageArea.Add(glow);
            if (idle)
            {
                stageArea.Add(MakeLabel("?", "tw-result-q"));
            }
            else
            {
                stageArea.Add(ItemIconWithCount(sl.output, 0, "tw-result-icon"));
                // 결과물 뒤 빛이 천천히 숨 쉰다
                glow.schedule.Execute(() =>
                {
                    float s = 1f + 0.08f * Mathf.Sin(Time.realtimeSinceStartup * 2.2f);
                    glow.style.scale = new Scale(new Vector3(s, s, 1f));
                    glow.style.opacity = 0.8f + 0.2f * Mathf.Sin(Time.realtimeSinceStartup * 2.2f);
                }).Every(33);
            }
            box.Add(stageArea);

            if (idle)
            {
                box.Add(MakeLabel("가공할 아이템을 고르면\n나올 결과물이 보여요", "tw-result-sub"));
                return box;
            }

            box.Add(MakeLabel(sl.outName ?? (sl.output != null ? sl.output.itemName : "?"), "tw-result-name"));
            box.Add(MakeLabel("×" + Mathf.Max(1, sl.outCount), "tw-result-count"));

            string need = (sl.inName ?? (sl.inItem != null ? sl.inItem.itemName : "?")) + " ×" + Mathf.Max(1, sl.inCount);
            box.Add(MakeLabel("재료  " + need, "tw-result-sub"));
            string info = (sl.queued > 1 ? "대기 " + (sl.queued - 1) + "개" : "") + (sl.queued > 1 && sl.readyCount > 0 ? "  ·  " : "") + (sl.readyCount > 0 ? "완성 " + sl.readyCount + "개" : "");
            if (info.Length > 0) box.Add(MakeLabel(info, "tw-result-sub"));
            return box;
        }
    }
}
