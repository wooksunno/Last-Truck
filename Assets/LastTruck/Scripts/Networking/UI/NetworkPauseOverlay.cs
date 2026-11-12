using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastTruck.Networking
{
    /// <summary>
    /// 게임 전체 멈춤(방장 변경 등) 동안 화면을 덮는 안내 패널.
    ///  - 반투명 검은 배경 + 가운데 카드(제목 / 안내 문구 / 돌아가는 로딩 점 / 경과 시간).
    ///  - Time.timeScale = 0 상태에서도 움직이도록 실제 시간(unscaled)으로 애니메이션한다.
    ///  - 팝업(SystemUI, sortingOrder 100)보다는 아래, 게임 화면보다는 위 (sortingOrder 90).
    /// NetworkPause가 Show/Hide를 부른다. 씬이 바뀌어도 유지된다(DontDestroyOnLoad).
    /// </summary>
    public class NetworkPauseOverlay : MonoBehaviour
    {
        #region 생성 / 조회

        private static NetworkPauseOverlay _instance;

        public static void Show(string title, string message)
        {
            Ensure();
            _instance.Open(title, message);
        }

        public static void Hide()
        {
            if (_instance != null) _instance.Close();
        }

        private static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("NetworkPauseOverlay");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NetworkPauseOverlay>();
            _instance.Build();
        }

        #endregion

        #region 화면 구성

        private const int DotCount = 12;
        private const float SpinnerRadius = 34f;
        private const float FadeSeconds = 0.2f;

        private CanvasGroup _group;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _message;
        private TextMeshProUGUI _elapsed;
        private readonly Image[] _dots = new Image[DotCount];

        private bool _open;
        private float _openedAt;
        private float _alpha;

        private void Build()
        {
            Canvas canvas = InGameHud.CreateOverlayCanvas(transform, "Canvas", 90);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>(); // 뒤쪽 클릭을 막는다

            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            // 배경 (화면 전체)
            Image dim = NewImage(canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
            Stretch(dim.rectTransform);

            // 가운데 카드
            Image card = NewImage(canvas.transform, "Card", new Color(0.11f, 0.13f, 0.19f, 0.96f));
            RectTransform cardRect = card.rectTransform;
            cardRect.sizeDelta = new Vector2(620f, 330f);
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.6f, 0.2f, 0.85f); // 게임 주황
            outline.effectDistance = new Vector2(3f, -3f);

            // 로딩 점 (원형으로 12개, 하나씩 밝아지며 돈다)
            var spinner = new GameObject("Spinner", typeof(RectTransform)).GetComponent<RectTransform>();
            spinner.SetParent(cardRect, false);
            spinner.anchoredPosition = new Vector2(0f, 70f);
            for (int i = 0; i < DotCount; i++)
            {
                Image dot = NewImage(spinner, $"Dot{i}", Color.white);
                dot.sprite = InGameHud.CircleSprite;
                float angle = Mathf.PI * 0.5f - i * Mathf.PI * 2f / DotCount;
                dot.rectTransform.sizeDelta = new Vector2(12f, 12f);
                dot.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * SpinnerRadius;
                _dots[i] = dot;
            }

            _title = NewText(cardRect, "Title", 34f, FontStyles.Bold, new Color(1f, 0.72f, 0.3f));
            _title.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            _message = NewText(cardRect, "Message", 22f, FontStyles.Normal, new Color(0.9f, 0.92f, 0.96f));
            _message.rectTransform.anchoredPosition = new Vector2(0f, -68f);
            _message.rectTransform.sizeDelta = new Vector2(560f, 70f);
            _elapsed = NewText(cardRect, "Elapsed", 18f, FontStyles.Normal, new Color(0.6f, 0.64f, 0.72f));
            _elapsed.rectTransform.anchoredPosition = new Vector2(0f, -128f);

            canvas.gameObject.SetActive(false);
        }

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        private static TextMeshProUGUI NewText(Transform parent, string name, float size, FontStyles style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            NetworkPrefabRegistry registry = NetworkPrefabRegistry.Instance;
            if (registry != null && registry.tmpFont != null) text.font = registry.tmpFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.rectTransform.sizeDelta = new Vector2(580f, 50f);
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        #endregion

        #region 열기 / 닫기 / 애니메이션

        private void Open(string title, string message)
        {
            if (!_open) _openedAt = Time.realtimeSinceStartup;
            _open = true;
            _title.text = title;
            _message.text = message;
            _group.blocksRaycasts = true;
            _group.transform.gameObject.SetActive(true);
        }

        private void Close()
        {
            _open = false;
            _group.blocksRaycasts = false;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _alpha = Mathf.MoveTowards(_alpha, _open ? 1f : 0f, dt / FadeSeconds);
            _group.alpha = _alpha;
            if (!_open && _alpha <= 0f)
            {
                if (_group.gameObject.activeSelf) _group.gameObject.SetActive(false);
                return;
            }

            // 점 하나가 밝고 뒤따르는 점들이 점점 흐려지는 회전
            float t = Time.realtimeSinceStartup;
            int head = Mathf.FloorToInt(t * 12f) % DotCount;
            for (int i = 0; i < DotCount; i++)
            {
                int behind = (head - i + DotCount) % DotCount;
                float a = Mathf.Lerp(1f, 0.15f, behind / (float)(DotCount - 1));
                _dots[i].color = new Color(1f, 0.72f, 0.3f, a);
            }

            if (_open)
            {
                int seconds = Mathf.FloorToInt(Time.realtimeSinceStartup - _openedAt);
                _elapsed.text = $"{seconds}초 경과 · 연결되면 자동으로 이어집니다";
            }
        }

        #endregion
    }
}
