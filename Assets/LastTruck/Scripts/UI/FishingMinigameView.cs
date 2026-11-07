using UnityEngine;
using UnityEngine.UIElements;

namespace LastTruck
{
    /// <summary>
    /// 낚시 미니게임 (UI Toolkit). 세로로 긴 물길 안에서 물고기가 위아래로 움직인다.
    ///  - 왼쪽 클릭을 누르고 있으면 초록 막대가 위로 가속하고, 떼면 아래로 떨어진다 (바닥에 닿으면 살짝 튄다).
    ///  - 물고기가 막대 안에 있으면 오른쪽 게이지가 차고, 밖에 있으면 줄어든다.
    ///  - 게이지가 가득 차면 Result = +1(낚음), 바닥나면 -1(놓침). 계산은 FishingController가 Tick으로 돌린다.
    /// 좌표는 모두 물길 높이 기준 0(아래) ~ 1(위).
    /// </summary>
    public class FishingMinigameView : MonoBehaviour
    {
        private const string PanelSettingsPath = "UI/LastTruckOverlayPanelSettings";
        private const string UssPath = "UI/LastTruckFishing";

        [Header("초록 막대")]
        [SerializeField] private float barSize = 0.26f;
        [SerializeField] private float liftAccel = 3.4f;
        [SerializeField] private float gravity = 3.0f;
        [SerializeField] private float maxSpeed = 1.5f;
        [SerializeField] private float bottomBounce = 0.45f;

        [Header("게이지")]
        [SerializeField] private float startProgress = 0.3f;
        [SerializeField] private float gainPerSecond = 0.2f;
        [SerializeField] private float lossPerSecond = 0.1f;

        private VisualElement _root, _frame, _track, _bar, _fish, _progressFill;
        private Label _title;

        private float _barPos, _barVel;
        private float _fishPos, _fishTarget, _fishVel, _nextMove;
        private float _difficulty;
        private float _progress;
        private float _shake;

        /// <summary>0 = 진행 중, +1 = 낚음, -1 = 놓침.</summary>
        public int Result { get; private set; }

        public static FishingMinigameView Create()
        {
            PanelSettings settings = Resources.Load<PanelSettings>(PanelSettingsPath);
            StyleSheet uss = Resources.Load<StyleSheet>(UssPath);
            if (settings == null || uss == null) return null;

            var go = new GameObject("UIDocument_FishingMinigame");
            DontDestroyOnLoad(go);
            var view = go.AddComponent<FishingMinigameView>();
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            view.Build(doc.rootVisualElement, uss);
            return view;
        }

        private void Build(VisualElement root, StyleSheet uss)
        {
            _root = root;
            root.styleSheets.Add(uss);
            root.AddToClassList("lt-root");
            root.pickingMode = PickingMode.Ignore;

            LastTruckUiSkin skin = LastTruckUiSkin.Get();
            StyleFontDefinition? font = LastTruckHud.SharedFont;

            _frame = new VisualElement { pickingMode = PickingMode.Ignore };
            _frame.AddToClassList("fm-frame");
            if (skin != null) UiToolkitSkin.Slice(_frame, skin.panelBrownDark, 0.5f, new Color(0.66f, 0.55f, 0.48f, 1f));
            root.Add(_frame);

            _title = new Label("낚시!") { pickingMode = PickingMode.Ignore };
            _title.AddToClassList("lt-text");
            _title.AddToClassList("fm-title");
            if (font.HasValue) _title.style.unityFontDefinition = font.Value;
            _frame.Add(_title);

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("fm-row");
            _frame.Add(row);

            _track = new VisualElement { pickingMode = PickingMode.Ignore };
            _track.AddToClassList("fm-track");
            row.Add(_track);

            // 물결 무늬 몇 줄
            for (int i = 1; i < 6; i++)
            {
                var wave = new VisualElement { pickingMode = PickingMode.Ignore };
                wave.AddToClassList("fm-wave");
                wave.style.top = Length.Percent(i * 16f);
                _track.Add(wave);
            }

            _bar = new VisualElement { pickingMode = PickingMode.Ignore };
            _bar.AddToClassList("fm-bar");
            _bar.style.height = Length.Percent(barSize * 100f);
            _track.Add(_bar);

            _fish = new VisualElement { pickingMode = PickingMode.Ignore };
            _fish.AddToClassList("fm-fish");
            _track.Add(_fish);

            var progress = new VisualElement { pickingMode = PickingMode.Ignore };
            progress.AddToClassList("fm-progress");
            _progressFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _progressFill.AddToClassList("fm-progress-fill");
            progress.Add(_progressFill);
            row.Add(progress);

            var hint = new Label("왼쪽 클릭을 누르고 있으면 막대가 올라가요\n오른쪽 클릭: 그만두기") { pickingMode = PickingMode.Ignore };
            hint.AddToClassList("lt-text");
            hint.AddToClassList("fm-hint");
            if (font.HasValue) hint.style.unityFontDefinition = font.Value;
            _frame.Add(hint);

            _frame.style.display = DisplayStyle.None;
        }

        public void Begin(float difficulty01, Sprite fishIcon, bool bigFish)
        {
            _difficulty = Mathf.Clamp01(difficulty01);
            _barPos = 0f;
            _barVel = 0f;
            _fishPos = 0.3f;
            _fishTarget = 0.5f;
            _fishVel = 0f;
            _nextMove = 0.6f;
            _progress = startProgress;
            Result = 0;
            _fish.style.backgroundImage = fishIcon != null ? new StyleBackground(fishIcon) : new StyleBackground(StyleKeyword.None);
            _fish.EnableInClassList("fm-fish--big", bigFish);
            _title.text = bigFish ? "대물이다!" : "입질!";
            _frame.style.display = DisplayStyle.Flex;
            _frame.RemoveFromClassList("fm-frame--in");
            _frame.schedule.Execute(() => _frame.AddToClassList("fm-frame--in"));
            Apply();
        }

        public void End()
        {
            if (_frame != null) _frame.style.display = DisplayStyle.None;
        }

        /// <summary>한 프레임 진행. holding = 왼쪽 클릭을 누르고 있는가.</summary>
        public void Tick(float dt, bool holding)
        {
            if (Result != 0 || dt <= 0f) return;
            dt = Mathf.Min(dt, 0.05f);

            // 초록 막대: 누르면 위로 가속, 떼면 중력
            _barVel += (holding ? liftAccel : -gravity) * dt;
            _barVel = Mathf.Clamp(_barVel, -maxSpeed, maxSpeed);
            _barPos += _barVel * dt;
            float top = 1f - barSize;
            if (_barPos < 0f)
            {
                _barPos = 0f;
                _barVel = _barVel < -0.4f ? -_barVel * bottomBounce : 0f;
            }
            else if (_barPos > top)
            {
                _barPos = top;
                if (_barVel > 0f) _barVel = 0f;
            }

            // 물고기: 가끔 새 목표로 이동 (어려울수록 자주, 멀리, 빠르게)
            _nextMove -= dt;
            if (_nextMove <= 0f)
            {
                float reach = Mathf.Lerp(0.25f, 0.75f, _difficulty);
                _fishTarget = Mathf.Clamp01(_fishPos + Random.Range(-reach, reach));
                // 가끔 위/아래 끝으로 확 도망간다
                if (Random.value < 0.12f + 0.25f * _difficulty) _fishTarget = Random.value < 0.5f ? Random.Range(0f, 0.12f) : Random.Range(0.88f, 1f);
                _nextMove = Random.Range(0.35f, 1.3f) * Mathf.Lerp(1.3f, 0.55f, _difficulty);
            }
            float stiffness = Mathf.Lerp(6f, 16f, _difficulty);
            float damping = Mathf.Lerp(4.5f, 5.5f, _difficulty);
            _fishVel += ((_fishTarget - _fishPos) * stiffness - _fishVel * damping) * dt;
            _fishPos = Mathf.Clamp01(_fishPos + _fishVel * dt);

            // 게이지
            float fishCenter = _fishPos;
            bool inside = fishCenter >= _barPos && fishCenter <= _barPos + barSize;
            _progress += (inside ? gainPerSecond : -lossPerSecond * Mathf.Lerp(0.8f, 1.3f, _difficulty)) * dt;
            _shake = inside ? 0f : Mathf.Min(1f, _shake + dt * 4f);
            if (_progress >= 1f) { _progress = 1f; Result = 1; }
            else if (_progress <= 0f) { _progress = 0f; Result = -1; }

            _bar.EnableInClassList("fm-bar--on", inside);
            Apply();
        }

        private void Apply()
        {
            _bar.style.bottom = Length.Percent(_barPos * 100f);
            // 물고기 그림 가운데가 _fishPos에 오도록 (그림 높이 약 12%)
            _fish.style.bottom = Length.Percent(Mathf.Clamp(_fishPos * 100f - 6f, -2f, 92f));
            float jitter = _shake > 0f ? Mathf.Sin(Time.unscaledTime * 60f) * 2f * _shake : 0f;
            _fish.style.translate = new Translate(jitter, 0f);
            _progressFill.style.height = Length.Percent(_progress * 100f);
            _progressFill.style.backgroundColor = Color.Lerp(new Color(0.92f, 0.32f, 0.22f), new Color(0.40f, 0.85f, 0.36f), _progress);
        }
    }
}
