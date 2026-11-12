using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastTruck.Networking
{
    /// <summary>채팅 줄의 종류 (색이 다르다).</summary>
    public enum ChatKind
    {
        Player = 0,  // 플레이어 채팅: "닉네임 : 내용" (닉네임 하늘색, 내용 흰색)
        System = 1,  // 시스템 알림: 노란색 (방장 변경, 접속/퇴장 등)
        Warning = 2, // 경고: 주황색 (웨이브 시작, 보스 등장 등)
    }

    /// <summary>
    /// 마인크래프트식 채팅창 (화면 좌하단).
    ///  - Enter: 입력창 열기 → 입력 후 Enter로 보내기, Esc로 닫기.
    ///  - 메시지는 10초 동안 보였다가 흐려지고, 입력창을 열면 최근 메시지가 다시 모두 보인다.
    ///  - 입력하는 동안은 캐릭터 조작(이동/공격/상호작용 키)을 막는다 (ChatHud.IsTyping).
    ///  - 보내기: NetworkGameState.SendChat → 호스트가 확인 → 모두에게. 시스템 메시지는 호스트가 뿌리거나 각자 띄운다(AddLocal).
    /// InGameHud.Ensure가 게임 씬에 만든다. 그 전에 온 메시지는 모아 두었다가 보여 준다.
    /// </summary>
    public class ChatHud : MonoBehaviour
    {
        #region 정적: 메시지 보관 / 조회

        private struct Line
        {
            public string Formatted;
            public float Time;
        }

        private const int MaxStored = 60;
        private const int VisibleLines = 10;
        private const float ShowSeconds = 10f;
        private const float FadeSeconds = 1.2f;

        private static readonly List<Line> Lines = new List<Line>();
        private static ChatHud _instance;
        private static int _closedFrame = -1;

        /// <summary>채팅 입력 중인가 (이동/키 입력을 막는 데 쓴다).</summary>
        public static bool IsTyping => _instance != null && _instance._open;

        /// <summary>이번 프레임 Esc는 채팅이 썼다 (ESC 메뉴가 같이 열리지 않게).</summary>
        public static bool ConsumedEscape => IsTyping || _closedFrame == Time.frameCount;

        /// <summary>이 컴퓨터의 채팅창에만 한 줄 추가한다 (네트워크로 보내지 않음).</summary>
        public static void AddLocal(string sender, string text, ChatKind kind)
        {
            if (string.IsNullOrEmpty(text)) return;
            Lines.Add(new Line { Formatted = Format(sender, text, kind), Time = UnityEngine.Time.unscaledTime });
            if (Lines.Count > MaxStored) Lines.RemoveAt(0);
            if (_instance != null) _instance._dirty = true;
        }

        /// <summary>리치 텍스트 태그를 못 쓰게 하고 길이를 자른다.</summary>
        public static string Sanitize(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var builder = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '<') builder.Append('\uFF1C');      // 전각 < (태그로 해석되지 않게)
                else if (c == '>') builder.Append('\uFF1E');
                else if (!char.IsControl(c)) builder.Append(c);
            }
            string result = builder.ToString().Trim();
            return result.Length > maxLength ? result.Substring(0, maxLength) : result;
        }

        private static string Format(string sender, string text, ChatKind kind)
        {
            switch (kind)
            {
                case ChatKind.Player:
                    return $"<color=#7FD3FF>{sender}</color> : {text}";
                case ChatKind.Warning:
                    return $"<color=#FF9A4A>[!] {text}</color>";
                default:
                    return $"<color=#FFD54A>{text}</color>";
            }
        }

        #endregion

        #region 화면 구성

        private RectTransform _lineRoot;
        private readonly TextMeshProUGUI[] _texts = new TextMeshProUGUI[VisibleLines];
        private readonly Image[] _backs = new Image[VisibleLines];
        private GameObject _inputRoot;
        private TMP_InputField _input;
        private bool _open;
        private int _openedFrame;
        private bool _dirty = true;

        private const float Width = 560f;
        private const float LinePadding = 6f;

        private void Awake()
        {
            _instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                SetPlayerSuspended(false);
                _instance = null;
                Lines.Clear(); // 게임에서 나가면 지난 판 채팅은 지운다
            }
        }

        private void Build()
        {
            Canvas canvas = InGameHud.CreateOverlayCanvas(transform, "ChatCanvas", 30);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            var root = new GameObject("Chat", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvas.transform, false);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.zero;
            root.anchoredPosition = new Vector2(20f, 20f);
            root.sizeDelta = new Vector2(Width, 400f);

            // 입력창 (맨 아래)
            _inputRoot = new GameObject("Input", typeof(RectTransform), typeof(Image));
            var inputRect = _inputRoot.GetComponent<RectTransform>();
            inputRect.SetParent(root, false);
            inputRect.anchorMin = inputRect.anchorMax = inputRect.pivot = Vector2.zero;
            inputRect.sizeDelta = new Vector2(Width, 38f);
            _inputRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            area.SetParent(inputRect, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(10f, 4f);
            area.offsetMax = new Vector2(-10f, -4f);

            TextMeshProUGUI placeholder = NewText(area, "Placeholder", 20f);
            placeholder.text = "메시지 입력 (Enter: 보내기, Esc: 닫기)";
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            Stretch(placeholder.rectTransform);
            TextMeshProUGUI inputText = NewText(area, "Text", 20f);
            Stretch(inputText.rectTransform);

            _input = _inputRoot.AddComponent<TMP_InputField>();
            _input.textViewport = area;
            _input.textComponent = inputText;
            _input.placeholder = placeholder;
            _input.characterLimit = NetworkGameState.ChatMaxLength;
            _input.lineType = TMP_InputField.LineType.SingleLine;
            _input.richText = false;
            if (inputText.font != null) _input.fontAsset = inputText.font;
            _input.onSubmit.AddListener(OnSubmit);
            _inputRoot.SetActive(false);

            // 메시지 줄 (아래에서 위로)
            _lineRoot = new GameObject("Lines", typeof(RectTransform)).GetComponent<RectTransform>();
            _lineRoot.SetParent(root, false);
            _lineRoot.anchorMin = _lineRoot.anchorMax = _lineRoot.pivot = Vector2.zero;
            _lineRoot.anchoredPosition = new Vector2(0f, 44f);
            _lineRoot.sizeDelta = new Vector2(Width, 340f);

            for (int i = 0; i < VisibleLines; i++)
            {
                var back = new GameObject($"Line{i}", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                back.rectTransform.SetParent(_lineRoot, false);
                back.rectTransform.anchorMin = back.rectTransform.anchorMax = back.rectTransform.pivot = Vector2.zero;
                back.color = new Color(0f, 0f, 0f, 0.4f);
                back.raycastTarget = false;
                TextMeshProUGUI text = NewText(back.rectTransform, "Text", 20f);
                text.richText = true;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.alignment = TextAlignmentOptions.Left;
                Stretch(text.rectTransform);
                text.rectTransform.offsetMin = new Vector2(8f, 2f);
                text.rectTransform.offsetMax = new Vector2(-8f, -2f);
                _texts[i] = text;
                _backs[i] = back;
                back.gameObject.SetActive(false);
            }
        }

        private static TextMeshProUGUI NewText(Transform parent, string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            NetworkPrefabRegistry registry = NetworkPrefabRegistry.Instance;
            if (registry != null && registry.tmpFont != null) text.font = registry.tmpFont;
            text.fontSize = size;
            text.color = Color.white;
            text.raycastTarget = false;
            text.outlineWidth = 0.15f;
            text.outlineColor = new Color32(0, 0, 0, 180);
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

        #region 입력

        private void Update()
        {
            if (!_open)
            {
                bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
                if (enter && _closedFrame != Time.frameCount && CanOpen()) Open();
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    Close();
                }
                else if (Time.frameCount > _openedFrame + 3 && !_input.isFocused)
                {
                    Close(); // 다른 곳을 클릭해서 입력창에서 벗어났다
                }
            }

            RefreshLines();
        }

        private static bool CanOpen()
        {
            if (NetworkPause.IsPaused) return false;
            if (SystemUI.Instance != null && SystemUI.Instance.IsPopupVisible) return false;
            return true;
        }

        private void Open()
        {
            _open = true;
            _openedFrame = Time.frameCount;
            _inputRoot.SetActive(true);
            _input.text = string.Empty;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EventSystem.current.SetSelectedGameObject(_inputRoot);
            _input.ActivateInputField();
            SetPlayerSuspended(true);
            _dirty = true;
        }

        private void Close()
        {
            _open = false;
            _closedFrame = Time.frameCount;
            _input.DeactivateInputField();
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == _inputRoot) EventSystem.current.SetSelectedGameObject(null);
            _inputRoot.SetActive(false);
            SetPlayerSuspended(false);
            _dirty = true;
        }

        private void OnSubmit(string text)
        {
            if (!_open) return;
            string message = Sanitize(text, NetworkGameState.ChatMaxLength);
            Close();
            if (!string.IsNullOrEmpty(message)) NetworkGameState.SendChat(message);
        }

        private static void SetPlayerSuspended(bool suspended)
        {
            NetworkPlayer local = NetworkPlayer.Local;
            if (local != null) local.SetChatSuspended(suspended);
        }

        #endregion

        #region 줄 표시

        private void RefreshLines()
        {
            float now = Time.unscaledTime;
            int count = Mathf.Min(VisibleLines, Lines.Count);
            float y = 0f;
            for (int slot = 0; slot < VisibleLines; slot++)
            {
                Image back = _backs[slot];
                if (slot >= count)
                {
                    if (back.gameObject.activeSelf) back.gameObject.SetActive(false);
                    continue;
                }

                Line line = Lines[Lines.Count - 1 - slot];
                float age = now - line.Time;
                float alpha = _open ? 1f : Mathf.Clamp01((ShowSeconds - age) / FadeSeconds);
                if (alpha <= 0f)
                {
                    if (back.gameObject.activeSelf) back.gameObject.SetActive(false);
                    continue;
                }

                TextMeshProUGUI text = _texts[slot];
                if (!back.gameObject.activeSelf) back.gameObject.SetActive(true);
                if (_dirty || text.text != line.Formatted) text.text = line.Formatted;

                float height = Mathf.Max(28f, text.GetPreferredValues(line.Formatted, Width - 16f, 0f).y + 8f);
                back.rectTransform.sizeDelta = new Vector2(Width, height);
                back.rectTransform.anchoredPosition = new Vector2(0f, y);
                y += height + 2f;

                back.color = new Color(0f, 0f, 0f, 0.4f * alpha);
                text.alpha = alpha;
            }
            _dirty = false;
        }

        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Lines.Clear();
            _instance = null;
            _closedFrame = -1;
        }
    }
}
