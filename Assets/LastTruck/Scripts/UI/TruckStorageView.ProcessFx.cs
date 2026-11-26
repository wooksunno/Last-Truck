using System;
using CraftingSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 트럭 가공 칸의 설비별 가공 연출 (UI Toolkit, 에셋 없이 코드로 그린다).
    ///  모닥불: 꼬치에 꿴 재료가 불 위에서 흔들리며 구워지고 불똥이 튄다 (진행될수록 갈색으로).
    ///  숫돌 연마대: 재료를 돌 바퀴에 대고 앞뒤로 갈며 불꽃이 튄다.
    ///  롤러 프레스기: 재료가 두 롤러 사이로 들어가 납작하게 눌린다.
    ///  절삭기: 레이저 날이 가로로 훑으며 재료를 자르고 파편이 튄다.
    ///  화학 정제탑: 플라스크 용액 속에 재료가 떠다니며 거품이 오른다.
    ///  초고온 용광로: 붉게 이글거리는 불덩이 속에서 재료가 달아오른다.
    ///  가죽 무두질 건조대: 건조대에 매달려 흔들리고 물방울이 떨어진다.
    /// 다 되면 결과물이 반짝이며 튀어 오른다. 모든 움직임은 절대 시간(realtime)으로 계산해서, 창이 다시 그려져도 끊기지 않고 이어진다.
    /// </summary>
    public partial class TruckStorageView
    {
        private const float StageW = 134f, StageH = 84f;

        private sealed class StageState
        {
            public float progress;
            public bool fresh;
            public float birth;

            /// <summary>방금 넣은 칸이면 처음 0.45초 동안 재료가 톡 튀어 들어간다 (0~1.15~1).</summary>
            public float Pop(float now)
            {
                if (!fresh) return 1f;
                float a = Mathf.Clamp01((now - birth) / 0.45f);
                if (a >= 1f) return 1f;
                const float c1 = 1.70158f, c3 = c1 + 1f;
                float x = a - 1f;
                return Mathf.Lerp(0.2f, 1f, 1f + c3 * x * x * x + c1 * x * x);
            }
        }

        // ---------------------------------------------------------------- 도구

        private static VisualElement Part(VisualElement parent, float w, float h, Color c, float radius = 0f)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.top = 0;
            e.style.width = w; e.style.height = h;
            e.style.backgroundColor = c;
            if (radius > 0f)
            {
                e.style.borderTopLeftRadius = radius; e.style.borderTopRightRadius = radius;
                e.style.borderBottomLeftRadius = radius; e.style.borderBottomRightRadius = radius;
            }
            parent.Add(e);
            return e;
        }

        /// <summary>왼쪽 위 좌표(x, y)로 옮기고 회전/크기/투명도를 정한다.</summary>
        private static void Place(VisualElement e, float x, float y, float rot = 0f, float sx = 1f, float sy = 1f, float opacity = 1f)
        {
            e.style.translate = new Translate(x, y);
            e.style.rotate = new Rotate(new Angle(rot, AngleUnit.Degree));
            e.style.scale = new Scale(new Vector3(sx, sy, 1f));
            e.style.opacity = opacity;
        }

        private static void Pivot(VisualElement e, float xPercent, float yPercent)
        {
            e.style.transformOrigin = new TransformOrigin(Length.Percent(xPercent), Length.Percent(yPercent));
        }

        private static float Wave(float t, float speed, float phase = 0f) => Mathf.Sin(t * speed + phase);

        private VisualElement StageIcon(VisualElement stage, ItemData item, float size)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            if (item == null) icon.style.display = DisplayStyle.None;   // 빈 칸의 설비 장면: 재료 없이 설비만 보인다
            icon.style.position = Position.Absolute;
            icon.style.left = 0; icon.style.top = 0;
            icon.style.width = size; icon.style.height = size;
            if (item != null && item.icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(item.icon);
                icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            else
            {
                icon.style.backgroundColor = new Color(1f, 1f, 1f, 0.25f);
                icon.style.borderTopLeftRadius = 8; icon.style.borderTopRightRadius = 8;
                icon.style.borderBottomLeftRadius = 8; icon.style.borderBottomRightRadius = 8;
            }
            stage.Add(icon);
            return icon;
        }

        // ---------------------------------------------------------------- 무대

        /// <summary>한 칸의 무대. done이면 결과물이 반짝이고, 아니면 설비별 가공 연출.</summary>
        private VisualElement MakeProcessStage(FacilityType fac, ItemData input, ItemData output, bool done, bool fresh, float progress, ProcSlotEl el)
        {
            var stage = new VisualElement { pickingMode = PickingMode.Ignore };
            stage.style.width = StageW; stage.style.height = StageH;
            stage.style.overflow = Overflow.Hidden;
            stage.style.borderTopLeftRadius = 10; stage.style.borderTopRightRadius = 10;
            stage.style.borderBottomLeftRadius = 10; stage.style.borderBottomRightRadius = 10;
            stage.style.marginBottom = 4;

            var st = new StageState { progress = progress, fresh = fresh, birth = Time.realtimeSinceStartup };
            Action<float> tick;
            if (done) tick = BuildDoneFx(stage, output, st);
            else
            {
                switch (fac)
                {
                    case FacilityType.Campfire: tick = BuildCampfireFx(stage, input, st); break;
                    case FacilityType.Grindstone: tick = BuildGrindstoneFx(stage, input, st); break;
                    case FacilityType.RollerPress: tick = BuildRollerFx(stage, input, st); break;
                    case FacilityType.PrecisionCutter: tick = BuildCutterFx(stage, input, st); break;
                    case FacilityType.ChemicalRefinery: tick = BuildRefineryFx(stage, input, st); break;
                    case FacilityType.SuperheatedFurnace: tick = BuildFurnaceFx(stage, input, st); break;
                    default: tick = BuildTanningFx(stage, input, st); break;
                }
            }

            if (el != null) el.onProgress = p => st.progress = p;
            tick(Time.realtimeSinceStartup);   // 첫 프레임부터 제자리에 있게
            stage.schedule.Execute(() => tick(Time.realtimeSinceStartup)).Every(33);
            return stage;
        }

        // ---------------------------------------------------------------- 모닥불

        private Action<float> BuildCampfireFx(VisualElement s, ItemData input, StageState st)
        {
            s.style.backgroundColor = new Color(0.17f, 0.09f, 0.06f, 1f);
            var glow = Part(s, 90, 60, new Color(1f, 0.45f, 0.1f, 0.22f), 45);
            var logA = Part(s, 78, 11, new Color(0.34f, 0.2f, 0.11f, 1f), 5);
            var logB = Part(s, 78, 11, new Color(0.42f, 0.26f, 0.13f, 1f), 5);
            var flames = new VisualElement[3];
            Color[] fc = { new Color(1f, 0.42f, 0.1f, 0.95f), new Color(1f, 0.62f, 0.15f, 0.95f), new Color(1f, 0.83f, 0.3f, 0.95f) };
            float[] fw = { 26f, 22f, 15f };
            for (int i = 0; i < 3; i++) { flames[i] = Part(s, fw[i], 40, fc[i], fw[i] * 0.5f); Pivot(flames[i], 50, 100); }
            var skewer = Part(s, 108, 3, new Color(0.45f, 0.3f, 0.16f, 1f), 1);
            var icon = StageIcon(s, input, 40);
            var embers = new VisualElement[7];
            for (int i = 0; i < embers.Length; i++) embers[i] = Part(s, 3, 3, new Color(1f, 0.7f, 0.25f, 1f), 1);
            var steam = new VisualElement[3];
            for (int i = 0; i < steam.Length; i++) steam[i] = Part(s, 8, 8, new Color(1f, 1f, 1f, 0.35f), 4);

            return t =>
            {
                float pop = st.Pop(t);
                Place(glow, 22, 40, 0, 1f + 0.08f * Wave(t, 6f), 1f);
                Place(logA, 28, 70, 8); Place(logB, 28, 69, -9);
                float xc = 67f;
                for (int i = 0; i < 3; i++)
                {
                    float h = 0.8f + 0.3f * Wave(t, 9f + i * 2f, i * 1.7f) + 0.1f * Wave(t, 19f, i);
                    float dx = (i - 1) * 14f;
                    Place(flames[i], xc + dx - fw[i] * 0.5f, 72f - 40f, 0, 1f + 0.08f * Wave(t, 7f, i), h * (i == 2 ? 0.85f : 1f));
                }
                Place(skewer, 13, 33 + Wave(t, 2.4f) * 1.5f, Wave(t, 1.6f) * 4f);
                Place(icon, 47, 11 + Wave(t, 2.4f) * 2f, Wave(t, 1.6f) * 12f, pop, pop);
                icon.style.unityBackgroundImageTintColor = Color.Lerp(Color.white, new Color(0.62f, 0.42f, 0.28f, 1f), Mathf.Clamp01(st.progress));   // 구워질수록 갈색으로
                for (int i = 0; i < embers.Length; i++)
                {
                    float ph = (t * 0.9f + i * 0.143f) % 1f;
                    Place(embers[i], xc + Wave(t, 2f, i * 2.1f) * 26f, 70f - ph * 60f, 0, 1f, 1f, 1f - ph);
                }
                for (int i = 0; i < steam.Length; i++)
                {
                    float ph = (t * 0.6f + i * 0.33f) % 1f;
                    Place(steam[i], 52f + i * 14f + Wave(t, 3f, i) * 4f, 24f - ph * 22f, 0, 0.6f + ph * 0.8f, 0.6f + ph * 0.8f, 0.5f * (1f - ph) * Mathf.Clamp01(st.progress * 3f));
                }
            };
        }

        // ---------------------------------------------------------------- 숫돌 연마대

        private Action<float> BuildGrindstoneFx(VisualElement s, ItemData input, StageState st)
        {
            s.style.backgroundColor = new Color(0.16f, 0.17f, 0.2f, 1f);
            var wheel = Part(s, 70, 70, new Color(0.58f, 0.6f, 0.64f, 1f), 35);
            var hub = Part(s, 44, 44, new Color(0.43f, 0.45f, 0.5f, 1f), 22);
            var spokeA = Part(s, 56, 5, new Color(0.26f, 0.27f, 0.31f, 1f), 2);
            var spokeB = Part(s, 56, 5, new Color(0.26f, 0.27f, 0.31f, 1f), 2);
            var icon = StageIcon(s, input, 38);
            var sparks = new VisualElement[9];
            for (int i = 0; i < sparks.Length; i++) sparks[i] = Part(s, 3, 3, i % 2 == 0 ? new Color(1f, 0.9f, 0.4f, 1f) : new Color(1f, 1f, 1f, 1f), 1);

            return t =>
            {
                float pop = st.Pop(t);
                Place(wheel, 32, 46);
                Place(hub, 45, 59);
                Place(spokeA, 39, 71, t * 320f); Place(spokeB, 39, 71, t * 320f + 90f);
                float shake = Wave(t, 7f);
                Place(icon, 48f + shake * 7f, 14f + Mathf.Abs(shake) * 1.5f, shake * 6f, pop, pop);
                icon.style.unityBackgroundImageTintColor = Color.Lerp(Color.white, new Color(1f, 1f, 1f, 1f), st.progress);
                float ox = 67f + shake * 7f, oy = 53f;
                for (int i = 0; i < sparks.Length; i++)
                {
                    float tau = (t + i * 0.07f) % 0.45f;
                    float ang = Mathf.Deg2Rad * (-25f - (i * 37f) % 130f);
                    Place(sparks[i], ox + Mathf.Cos(ang) * 78f * tau * (shake >= 0 ? 1f : -1f), oy + Mathf.Sin(ang) * 78f * tau + 150f * tau * tau, 0, 1f, 1f, 1f - tau / 0.45f);
                }
            };
        }

        // ---------------------------------------------------------------- 롤러 프레스기

        private Action<float> BuildRollerFx(VisualElement s, ItemData input, StageState st)
        {
            s.style.backgroundColor = new Color(0.15f, 0.17f, 0.17f, 1f);
            var belt = Part(s, StageW, 4, new Color(0.3f, 0.32f, 0.33f, 1f));
            var top = Part(s, 36, 36, new Color(0.62f, 0.65f, 0.68f, 1f), 18);
            var bot = Part(s, 36, 36, new Color(0.62f, 0.65f, 0.68f, 1f), 18);
            var topBar = Part(s, 30, 4, new Color(0.3f, 0.32f, 0.36f, 1f), 2);
            var botBar = Part(s, 30, 4, new Color(0.3f, 0.32f, 0.36f, 1f), 2);
            var icon = StageIcon(s, input, 38);

            return t =>
            {
                float pop = st.Pop(t);
                Place(belt, 0, 41);
                Place(top, 49, -4); Place(bot, 49, 52);
                Place(topBar, 52, 12, t * 220f); Place(botBar, 52, 68, -t * 220f);
                float u = (t % 2.2f) / 2.2f;
                float x = 14f + u * 106f;                      // 아이콘 중심
                float k = Mathf.Clamp01(1f - Mathf.Abs(x - 67f) / 22f);   // 롤러 아래를 지날 때 눌린다
                float sy = 1f - 0.58f * k, sx = 1f + 0.35f * k;
                Place(icon, x - 19f, 23f + 0.58f * k * 8f, 0, sx * pop, sy * pop);
                icon.style.unityBackgroundImageTintColor = Color.Lerp(Color.white, new Color(0.88f, 0.9f, 0.95f, 1f), x > 67f ? 1f : 0f);
            };
        }

        // ---------------------------------------------------------------- 절삭기

        private Action<float> BuildCutterFx(VisualElement s, ItemData input, StageState st)
        {
            s.style.backgroundColor = new Color(0.1f, 0.15f, 0.2f, 1f);
            var line = Part(s, StageW, 2, new Color(0.35f, 0.65f, 0.8f, 0.25f));
            var icon = StageIcon(s, input, 40);
            var glow = Part(s, 16, 76, new Color(0.35f, 0.8f, 1f, 0.22f), 8);
            var blade = Part(s, 3, 76, new Color(0.85f, 1f, 1f, 1f), 1);
            var sparks = new VisualElement[6];
            for (int i = 0; i < sparks.Length; i++) sparks[i] = Part(s, 3, 3, new Color(0.7f, 1f, 1f, 1f), 1);

            return t =>
            {
                float pop = st.Pop(t);
                float u = Mathf.PingPong(t * 0.95f, 1f);
                float bx = 12f + u * 110f;
                float near = Mathf.Clamp01(1f - Mathf.Abs(bx - 67f) / 20f);
                Place(line, 0, 62);
                Place(icon, 47f + Wave(t, 45f) * 0.8f * near, 19f, 0, pop * (1f + 0.05f * near), pop * (1f + 0.05f * near));
                Place(glow, bx - 8f, 4);
                Place(blade, bx - 1.5f, 4);
                for (int i = 0; i < sparks.Length; i++)
                {
                    float tau = (t * 2.2f + i * 0.17f) % 0.5f;
                    float ang = Mathf.Deg2Rad * (-70f + i * 28f);
                    Place(sparks[i], bx + Mathf.Cos(ang) * 40f * tau, 44f + Mathf.Sin(ang) * 40f * tau, 0, 1f, 1f, near * (1f - tau / 0.5f));
                }
            };
        }

        // ---------------------------------------------------------------- 화학 정제탑

        private Action<float> BuildRefineryFx(VisualElement s, ItemData input, StageState st)
        {
            s.style.backgroundColor = new Color(0.08f, 0.14f, 0.19f, 1f);
            var flask = Part(s, 76, 68, new Color(0.45f, 0.85f, 1f, 0.12f), 14);
            SetBorder(flask, new Color(0.6f, 0.92f, 1f, 0.9f), 3f, 14f);
            flask.style.overflow = Overflow.Hidden;
            var liquid = Part(flask, 76, 70, new Color(0.24f, 0.8f, 0.74f, 0.78f), 0);
            var bubbles = new VisualElement[7];
            for (int i = 0; i < bubbles.Length; i++) bubbles[i] = Part(flask, 7, 7, new Color(1f, 1f, 1f, 0.7f), 3.5f);
            var icon = StageIcon(s, input, 36);
            var vapor = new VisualElement[3];
            for (int i = 0; i < vapor.Length; i++) vapor[i] = Part(s, 12, 12, new Color(0.85f, 0.97f, 1f, 0.4f), 6);

            return t =>
            {
                float pop = st.Pop(t);
                Place(flask, 29, 10);
                float level = 26f + st.progress * 16f + Wave(t, 3f) * 2f;
                liquid.style.backgroundColor = Color.Lerp(new Color(0.24f, 0.8f, 0.74f, 0.78f), new Color(0.62f, 0.9f, 0.34f, 0.8f), st.progress);
                Place(liquid, -3, 62f - level);
                for (int i = 0; i < bubbles.Length; i++)
                {
                    float ph = (t * 0.7f + i * 0.17f) % 1f;
                    Place(bubbles[i], 6f + i * 9f + Wave(t, 3f, i) * 3f, 58f - ph * 52f, 0, 0.7f + 0.5f * ph, 0.7f + 0.5f * ph, ph < 0.9f ? 0.8f : 0f);
                }
                Place(icon, 49f, 28f + Wave(t, 2.2f) * 4f, Wave(t, 1.8f) * 10f, pop, pop);
                for (int i = 0; i < vapor.Length; i++)
                {
                    float ph = (t * 0.5f + i * 0.33f) % 1f;
                    Place(vapor[i], 42f + i * 18f + Wave(t, 2.5f, i) * 4f, 12f - ph * 14f, 0, 0.7f + ph, 0.7f + ph, 0.45f * (1f - ph));
                }
            };
        }

        // ---------------------------------------------------------------- 초고온 용광로

        private Action<float> BuildFurnaceFx(VisualElement s, ItemData input, StageState st)
        {
            s.style.backgroundColor = new Color(0.27f, 0.06f, 0.03f, 1f);
            var halo = Part(s, 100, 100, new Color(1f, 0.35f, 0.08f, 0.35f), 50);
            var core = Part(s, 70, 70, new Color(1f, 0.58f, 0.15f, 0.9f), 35);
            var hot = Part(s, 40, 40, new Color(1f, 0.92f, 0.65f, 0.95f), 20);
            var icon = StageIcon(s, input, 38);
            var lines = new VisualElement[4];
            for (int i = 0; i < lines.Length; i++) lines[i] = Part(s, 3, 26, new Color(1f, 0.82f, 0.5f, 0.55f), 1.5f);
            var embers = new VisualElement[6];
            for (int i = 0; i < embers.Length; i++) embers[i] = Part(s, 3, 3, new Color(1f, 0.78f, 0.35f, 1f), 1);

            return t =>
            {
                float pop = st.Pop(t);
                float pulse = 0.5f + 0.5f * Wave(t, 3f);
                s.style.backgroundColor = Color.Lerp(new Color(0.27f, 0.06f, 0.03f, 1f), new Color(0.5f, 0.13f, 0.04f, 1f), pulse);
                float p5 = Wave(t, 5f);
                Place(halo, 17, -8, 0, 1f + 0.06f * p5, 1f + 0.06f * p5);
                Place(core, 32, 7, 0, 0.92f + 0.08f * p5, 0.92f + 0.08f * p5);
                Place(hot, 47, 22, 0, 0.9f + 0.12f * Wave(t, 7f), 0.9f + 0.12f * Wave(t, 7f));
                Place(icon, 48f + Wave(t, 11f) * 0.8f, 22f + Wave(t, 2.5f) * 2f, Wave(t, 1.4f) * 5f, pop, pop);
                icon.style.unityBackgroundImageTintColor = Color.Lerp(Color.white, new Color(1f, 0.72f, 0.4f, 1f), Mathf.Clamp01(st.progress));   // 달아오른다
                for (int i = 0; i < lines.Length; i++)
                {
                    float ph = (t * 0.8f + i * 0.25f) % 1f;
                    Place(lines[i], 22f + i * 28f + Wave(t, 4f, i) * 4f, 62f - ph * 70f, Wave(t, 3f, i) * 8f, 1f, 1f, 0.7f * (1f - ph));
                }
                for (int i = 0; i < embers.Length; i++)
                {
                    float ph = (t * 1.2f + i * 0.17f) % 1f;
                    Place(embers[i], 30f + i * 15f + Wave(t, 5f, i * 1.3f) * 6f, 74f - ph * 70f, 0, 1f, 1f, 1f - ph);
                }
            };
        }

        // ---------------------------------------------------------------- 가죽 무두질 건조대

        private Action<float> BuildTanningFx(VisualElement s, ItemData input, StageState st)
        {
            s.style.backgroundColor = new Color(0.2f, 0.15f, 0.1f, 1f);
            var rayA = Part(s, 18, 90, new Color(1f, 0.9f, 0.55f, 0.1f));
            var rayB = Part(s, 14, 90, new Color(1f, 0.9f, 0.55f, 0.1f));
            var postL = Part(s, 6, 80, new Color(0.45f, 0.29f, 0.14f, 1f), 3);
            var postR = Part(s, 6, 80, new Color(0.45f, 0.29f, 0.14f, 1f), 3);
            var bar = Part(s, 120, 7, new Color(0.55f, 0.36f, 0.18f, 1f), 3);
            var icon = StageIcon(s, input, 40);
            Pivot(icon, 50, 0);   // 위(막대)에 걸려 흔들린다
            var drops = new VisualElement[2];
            for (int i = 0; i < drops.Length; i++) drops[i] = Part(s, 4, 7, new Color(0.55f, 0.7f, 0.85f, 0.9f), 3);

            return t =>
            {
                float pop = st.Pop(t);
                Place(rayA, 18, 0, -12, 1f, 1f, 0.6f + 0.4f * Wave(t, 1.4f));
                Place(rayB, 96, 0, 12, 1f, 1f, 0.6f + 0.4f * Wave(t, 1.9f, 1f));
                Place(postL, 6, 4); Place(postR, 122, 4);
                Place(bar, 7, 4);
                float sway = Wave(t, 1.7f) * 9f;
                Place(icon, 47f, 11f, sway, pop, pop);
                icon.style.unityBackgroundImageTintColor = Color.Lerp(Color.white, new Color(0.85f, 0.72f, 0.55f, 1f), Mathf.Clamp01(st.progress));   // 말라 갈수록 색이 변한다
                for (int i = 0; i < drops.Length; i++)
                {
                    float ph = (t * 0.8f + i * 0.5f) % 1f;
                    Place(drops[i], 62f + i * 10f + sway * 0.6f, 52f + ph * 28f, 0, 1f, 1f, 1f - ph);
                }
            };
        }

        // ---------------------------------------------------------------- 완성

        private Action<float> BuildDoneFx(VisualElement s, ItemData output, StageState st)
        {
            s.style.backgroundColor = new Color(0.1f, 0.24f, 0.13f, 1f);
            var glow = Part(s, 96, 96, new Color(1f, 0.93f, 0.55f, 0.28f), 48);
            var icon = StageIcon(s, output, 54);
            var sparkles = new VisualElement[6];
            for (int i = 0; i < sparkles.Length; i++) sparkles[i] = Part(s, 7, 7, new Color(1f, 0.96f, 0.6f, 1f), 1);

            return t =>
            {
                float pulse = 1f + 0.07f * Wave(t, 4f);
                Place(glow, 19, -6, 0, pulse, pulse, 0.7f + 0.3f * Wave(t, 3f));
                Place(icon, 40f, 13f + Wave(t, 3f) * 2f, Wave(t, 2f) * 4f, pulse, pulse);
                for (int i = 0; i < sparkles.Length; i++)
                {
                    float ang = t * 1.2f + i * Mathf.PI / 3f;
                    float r = 36f + Wave(t, 3f, i) * 4f;
                    Place(sparkles[i], 63f + Mathf.Cos(ang) * r * 1.25f, 38f + Mathf.Sin(ang) * r * 0.8f, 45f, 1f, 1f, 0.5f + 0.5f * Wave(t, 5f, i * 1.7f));
                }
            };
        }

        // ---------------------------------------------------------------- 가진 아이템 칸 / 가공법 고르기

        private VisualElement MakeOwnedSlot(CraftingSystem.TruckProcessView.OwnedInfo o, Color accent)
        {
            var b = new Button(() => o.onClick?.Invoke()) { text = "" };
            b.AddToClassList("tw-slot");
            UiToolkitSkin.Slice(b, _skin.panelBrownDark, 0.35f, UiSkinUtil.SlotFilled);
            b.AddToClassList(o.canProcess ? "tw-slot--proc" : "tw-slot--dim");

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("tw-slot-icon");
            if (o.item != null && o.item.icon != null) icon.style.backgroundImage = new StyleBackground(o.item.icon);
            b.Add(icon);
            if (o.item != null && o.item.icon == null)
                b.Add(MakeLabel(o.item.itemName.Length <= 2 ? o.item.itemName : o.item.itemName.Substring(0, 2), "tw-slot-letter"));
            if (o.count > 1) b.Add(MakeLabel(o.count.ToString(), "tw-slot-count"));
            b.Add(MakeLabel(o.item != null ? o.item.itemName : "", "tw-slot-name"));

            string name = o.item != null ? o.item.itemName : "?";
            string hint = o.canProcess ? name + " ×" + o.count + "  —  누르면 빈 가공 칸에 들어가요"
                                       : name + " ×" + o.count + "  —  이 설비로는 가공할 수 없어요";
            b.RegisterCallback<PointerEnterEvent>(_ => SetHint(hint));
            return b;
        }

        private VisualElement MakeClaimAll(CraftingSystem.TruckProcessView.ProcessModel m)
        {
            var btn = new Button(() => m.onClaimAll?.Invoke()) { text = "" };
            btn.AddToClassList("tw-make");
            btn.AddToClassList("tw-claimall");
            UiToolkitSkin.Slice(btn, _skin.buttonBrown, 0.5f, new Color(0.50f, 0.88f, 0.56f, 1f));
            btn.Add(MakeLabel("완성된 결과물 모두 받기 (" + m.readyTotal + "칸)", "tw-make-text"));
            return btn;
        }
    }
}
