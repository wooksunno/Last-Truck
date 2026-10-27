using UnityEngine;
using UnityEngine.UI;

namespace LastTruck
{
    /// <summary>
    /// 화면 위쪽 가운데에 트럭 내구도(체력) 막대를 보여준다.
    /// 씬에 따로 만들어 둘 필요가 없다: 게임이 시작되면 자동으로 만들어지고, 씬에서 트럭(TruckInformation)을 찾으면 표시한다.
    /// 많이 부서질수록 초록 → 노랑 → 빨강, 25% 아래에서는 깜빡인다.
    /// </summary>
    public class TruckHealthHud : MonoBehaviour
    {
        private TruckInformation _truck;
        private Image _fill;
        private Text _label;
        private CanvasGroup _group;
        private float _nextFind;
        private float _shown = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<TruckHealthHud>() != null)
                return;

            var go = new GameObject("TruckHealthHud");
            DontDestroyOnLoad(go);
            go.AddComponent<TruckHealthHud>();
        }

        private void Awake()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(canvasGo.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, -18f);
            prt.sizeDelta = new Vector2(420f, 46f);
            var bg = panel.GetComponent<Image>();
            bg.color = new Color(0.07f, 0.06f, 0.05f, 0.88f);
            bg.raycastTarget = false;
            _group = panel.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // 아이콘 대신 글자 라벨
            var title = new GameObject("Title", typeof(RectTransform), typeof(Text));
            title.transform.SetParent(panel.transform, false);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0f, 0.5f); trt.anchorMax = new Vector2(0f, 0.5f); trt.pivot = new Vector2(0f, 0.5f);
            trt.anchoredPosition = new Vector2(14f, 0f); trt.sizeDelta = new Vector2(70f, 30f);
            Text t = title.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = "트럭";
            t.fontSize = 22; t.fontStyle = FontStyle.Bold;
            t.color = new Color(1f, 0.9f, 0.6f);
            t.alignment = TextAnchor.MiddleLeft;
            t.raycastTarget = false;

            // 막대 바탕 + 채움
            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(panel.transform, false);
            var krt = (RectTransform)track.transform;
            krt.anchorMin = new Vector2(0f, 0.5f); krt.anchorMax = new Vector2(1f, 0.5f); krt.pivot = new Vector2(0.5f, 0.5f);
            krt.offsetMin = new Vector2(84f, -12f); krt.offsetMax = new Vector2(-14f, 12f);
            var kimg = track.GetComponent<Image>();
            kimg.color = new Color(0f, 0f, 0f, 0.55f);
            kimg.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            var frt = (RectTransform)fill.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);
            _fill = fill.GetComponent<Image>();
            // 스프라이트 없는 Image는 Filled/fillAmount가 무시되어 항상 꽉 차 보인다 → 오른쪽 앵커로 길이를 조절한다
            _fill.raycastTarget = false;

            var lbl = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lbl.transform.SetParent(track.transform, false);
            var lrt = (RectTransform)lbl.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            _label = lbl.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 17; _label.fontStyle = FontStyle.Bold;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.white;
            _label.raycastTarget = false;
        }

        private void Update()
        {
            if (_truck == null)
            {
                _group.alpha = 0f;
                if (Time.unscaledTime >= _nextFind)
                {
                    _nextFind = Time.unscaledTime + 1f;
                    _truck = FindFirstObjectByType<TruckInformation>();
                    _shown = -1f;
                }
                if (_truck == null)
                    return;
            }

            float max = Mathf.Max(1f, _truck.MaxDurability);
            float cur = _truck.CurrentDurability;
            float ratio = Mathf.Clamp01(cur / max);

            if (!Mathf.Approximately(cur, _shown))
            {
                _shown = cur;
                _fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
                _fill.color = ratio > 0.5f
                    ? Color.Lerp(new Color(0.95f, 0.8f, 0.2f), new Color(0.35f, 0.8f, 0.3f), (ratio - 0.5f) * 2f)
                    : Color.Lerp(new Color(0.9f, 0.22f, 0.18f), new Color(0.95f, 0.8f, 0.2f), ratio * 2f);
                _label.text = _truck.IsBroken ? "고장!" : Mathf.CeilToInt(cur) + " / " + Mathf.CeilToInt(max);
            }

            // 25% 아래에서는 깜빡여서 위험을 알린다
            float a = 1f;
            if (ratio < 0.25f)
                a = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
            _group.alpha = a;
        }
    }
}
