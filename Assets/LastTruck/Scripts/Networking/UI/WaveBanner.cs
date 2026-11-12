using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastTruck.Networking
{
    /// <summary>
    /// 화면 가운데 조금 위에 잠깐 뜨는 큰 안내 문구 (웨이브 제목 + 설명, 보스 등장 등).
    /// 0.35초 동안 나타나서 머문 뒤 0.6초 동안 사라진다. 실제 시간 기준.
    /// </summary>
    public class WaveBanner : MonoBehaviour
    {
        #region 공개

        private static WaveBanner _instance;

        public static void Show(string title, string description, float seconds = 4f)
        {
            if (_instance == null)
            {
                var go = new GameObject("WaveBanner");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<WaveBanner>();
                _instance.Build();
            }
            _instance.Play(title, description, seconds);
        }

        #endregion

        #region 화면

        private const float FadeIn = 0.35f;
        private const float FadeOut = 0.6f;

        private CanvasGroup _group;
        private RectTransform _content;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _description;
        private float _startTime = -100f;
        private float _hold;

        private void Build()
        {
            Canvas canvas = InGameHud.CreateOverlayCanvas(transform, "BannerCanvas", 40);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            _content.SetParent(canvas.transform, false);
            _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.anchoredPosition = new Vector2(0f, 230f);
            _content.sizeDelta = new Vector2(1400f, 200f);

            _title = NewText(_content, "Title", 60f, FontStyles.Bold, new Color(1f, 0.72f, 0.3f));
            _title.rectTransform.anchoredPosition = new Vector2(0f, 30f);
            _description = NewText(_content, "Description", 28f, FontStyles.Normal, new Color(0.95f, 0.95f, 0.95f));
            _description.rectTransform.anchoredPosition = new Vector2(0f, -40f);
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
            text.outlineWidth = 0.22f;
            text.outlineColor = new Color32(0, 0, 0, 220);
            text.rectTransform.sizeDelta = new Vector2(1400f, 80f);
            return text;
        }

        private void Play(string title, string description, float seconds)
        {
            _title.text = ChatHud.Sanitize(title, 60);
            _description.text = ChatHud.Sanitize(description, 120);
            _hold = Mathf.Max(1f, seconds);
            _startTime = Time.unscaledTime;
        }

        private void Update()
        {
            float age = Time.unscaledTime - _startTime;
            float total = FadeIn + _hold + FadeOut;
            float alpha;
            if (age < 0f || age > total) alpha = 0f;
            else if (age < FadeIn) alpha = age / FadeIn;
            else if (age < FadeIn + _hold) alpha = 1f;
            else alpha = 1f - (age - FadeIn - _hold) / FadeOut;
            _group.alpha = alpha;

            // 나타날 때 살짝 내려오며 커진다
            float t = Mathf.Clamp01(age / FadeIn);
            _content.anchoredPosition = new Vector2(0f, 230f + (1f - t) * 30f);
            _content.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, t);
        }

        #endregion
    }
}
