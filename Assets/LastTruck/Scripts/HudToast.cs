using UnityEngine;
using UnityEngine.UI;

namespace LastTruck
{
    /// <summary>
    /// 화면 아래쪽에 잠깐 떴다 사라지는 안내 문구 (예: "밤에는 트럭을 운전할 수 없습니다").
    /// 사용: HudToast.Show("문구");  — 씬에 따로 만들어 둘 것이 없다(처음 부를 때 자동 생성).
    /// </summary>
    public class HudToast : MonoBehaviour
    {
        private static HudToast _instance;

        private Text _text;
        private CanvasGroup _group;
        private float _hideAt;

        public static void Show(string message, float seconds = 2.5f)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (_instance == null)
                _instance = Create();
            _instance.Display(message, seconds);
        }

        private static HudToast Create()
        {
            var root = new GameObject("HudToast", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            DontDestroyOnLoad(root);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            box.transform.SetParent(root.transform, false);
            var rt = (RectTransform)box.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 190f);
            rt.sizeDelta = new Vector2(640f, 56f);
            box.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.04f, 0.92f);
            box.GetComponent<Image>().raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(box.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12f, 0f); trt.offsetMax = new Vector2(-12f, 0f);
            Text text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.82f, 0.45f);
            text.raycastTarget = false;

            var toast = root.AddComponent<HudToast>();
            toast._text = text;
            toast._group = box.GetComponent<CanvasGroup>();
            toast._group.alpha = 0f;
            return toast;
        }

        private void Display(string message, float seconds)
        {
            _text.text = message;
            _group.alpha = 1f;
            _hideAt = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            if (_group.alpha <= 0f)
                return;

            float left = _hideAt - Time.unscaledTime;
            _group.alpha = left <= 0f ? 0f : Mathf.Clamp01(left / 0.4f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }
    }
}
