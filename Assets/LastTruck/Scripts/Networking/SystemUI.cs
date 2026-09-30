using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastTruck.Networking
{
    /// <summary>
    /// 씬이 바뀌어도 살아있는 공용 UI: 로딩 패널 + 팝업(큐).
    ///
    /// - 로딩 패널: 네트워크 요청을 보내고 완료 콜백이 올 때까지 화면 전체를 덮어서 추가 조작을 막는다.
    ///   취소 가능한 작업이면 "취소" 버튼이 같이 뜬다.
    /// - 팝업: 알림(확인 버튼 1개)과 선택(예/아니오) 두 종류. 여러 개가 연달아 요청되면
    ///   서로 덮어쓰지 않고 큐에 쌓였다가 하나씩 순서대로 보여준다. 팝업은 항상 로딩 패널보다 위에 뜬다.
    ///
    /// Menu 씬의 "SystemCanvas" 오브젝트에 붙어 있고 DontDestroyOnLoad로 게임 씬까지 따라간다.
    /// Menu 씬이 다시 로드되면 새로 생긴 중복 SystemCanvas는 스스로 파괴된다.
    /// </summary>
    public class SystemUI : MonoBehaviour
    {
        public static SystemUI Instance { get; private set; }

        [Header("로딩 패널")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private TMP_Text loadingMessageText;
        [SerializeField] private RectTransform loadingSpinner;
        [SerializeField] private Button loadingCancelButton;

        [Header("팝업 패널")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private TMP_Text popupTitleText;
        [SerializeField] private TMP_Text popupMessageText;
        [SerializeField] private Button popupConfirmButton;
        [SerializeField] private TMP_Text popupConfirmLabel;
        [SerializeField] private Button popupCancelButton;
        [SerializeField] private TMP_Text popupCancelLabel;

        [Header("팝업 버튼 배치 (버튼 1개일 때 / 2개일 때 X 위치)")]
        [SerializeField] private float twoButtonOffsetX = 150f;

        private sealed class PopupRequest
        {
            public string Title;
            public string Message;
            public string ConfirmLabel;
            public string CancelLabel; // null이면 알림(버튼 1개)
            public Action OnConfirm;
            public Action OnCancel;
        }

        private readonly Queue<PopupRequest> _popupQueue = new Queue<PopupRequest>();
        private PopupRequest _currentPopup;
        private Action _loadingCancelAction;
        private bool _isDuplicate;

        public bool IsLoadingVisible => loadingPanel != null && loadingPanel.activeSelf;
        public bool IsPopupVisible => _currentPopup != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent != null) transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);

            loadingPanel.SetActive(false);
            popupPanel.SetActive(false);

            loadingCancelButton.onClick.AddListener(OnLoadingCancelClicked);
            popupConfirmButton.onClick.AddListener(OnPopupConfirmClicked);
            popupCancelButton.onClick.AddListener(OnPopupCancelClicked);
        }

        private void OnDestroy()
        {
            if (!_isDuplicate && Instance == this) Instance = null;
        }

        private void Update()
        {
            if (loadingSpinner != null && IsLoadingVisible)
            {
                loadingSpinner.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);
            }
        }

        // ------------------------------------------------------------------
        // 로딩
        // ------------------------------------------------------------------

        /// <summary>로딩 패널을 띄운다. onCancel을 넘기면 취소 버튼이 보인다.</summary>
        public void ShowLoadingPanel(string message, Action onCancel = null)
        {
            _loadingCancelAction = onCancel;
            loadingMessageText.text = message;
            loadingCancelButton.gameObject.SetActive(onCancel != null);
            loadingPanel.SetActive(true);
        }

        public void HideLoadingPanel()
        {
            _loadingCancelAction = null;
            loadingPanel.SetActive(false);
        }

        private void OnLoadingCancelClicked()
        {
            Action cancel = _loadingCancelAction;
            _loadingCancelAction = null;
            loadingCancelButton.gameObject.SetActive(false); // 두 번 눌리지 않게
            cancel?.Invoke();
        }

        // ------------------------------------------------------------------
        // 팝업 (큐)
        // ------------------------------------------------------------------

        public void EnqueueAlert(string title, string message, Action onClosed = null, string confirmLabel = "확인")
        {
            Enqueue(new PopupRequest
            {
                Title = title,
                Message = message,
                ConfirmLabel = confirmLabel,
                CancelLabel = null,
                OnConfirm = onClosed,
            });
        }

        public void EnqueueConfirm(string title, string message, Action onYes, Action onNo = null,
            string yesLabel = "예", string noLabel = "아니오")
        {
            Enqueue(new PopupRequest
            {
                Title = title,
                Message = message,
                ConfirmLabel = yesLabel,
                CancelLabel = noLabel,
                OnConfirm = onYes,
                OnCancel = onNo,
            });
        }

        private void Enqueue(PopupRequest request)
        {
            _popupQueue.Enqueue(request);
            if (_currentPopup == null) ShowNextPopup();
        }

        private void ShowNextPopup()
        {
            if (_popupQueue.Count == 0)
            {
                _currentPopup = null;
                popupPanel.SetActive(false);
                return;
            }

            _currentPopup = _popupQueue.Dequeue();
            bool twoButtons = _currentPopup.CancelLabel != null;

            popupTitleText.text = _currentPopup.Title;
            popupMessageText.text = _currentPopup.Message;
            popupConfirmLabel.text = _currentPopup.ConfirmLabel;
            popupCancelButton.gameObject.SetActive(twoButtons);
            if (twoButtons) popupCancelLabel.text = _currentPopup.CancelLabel;

            SetButtonX(popupConfirmButton, twoButtons ? twoButtonOffsetX : 0f);
            SetButtonX(popupCancelButton, -twoButtonOffsetX);

            popupPanel.SetActive(true);
            popupPanel.transform.SetAsLastSibling(); // 항상 로딩 패널보다 위
        }

        private static void SetButtonX(Button button, float x)
        {
            var rt = (RectTransform)button.transform;
            rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
        }

        private void OnPopupConfirmClicked() => ClosePopup(true);
        private void OnPopupCancelClicked() => ClosePopup(false);

        private void ClosePopup(bool confirmed)
        {
            PopupRequest closed = _currentPopup;
            _currentPopup = null;
            ShowNextPopup();

            if (closed == null) return;
            try
            {
                if (confirmed) closed.OnConfirm?.Invoke();
                else closed.OnCancel?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // ------------------------------------------------------------------
        // 어디서든 부를 수 있는 정적 단축 함수 (SystemUI가 없는 씬에서 테스트해도 에러 없이 로그만 남긴다)
        // ------------------------------------------------------------------

        public static void ShowLoading(string message, Action onCancel = null)
        {
            if (Instance != null) Instance.ShowLoadingPanel(message, onCancel);
            else Debug.Log($"[SystemUI] (로딩) {message}");
        }

        public static void HideLoading()
        {
            if (Instance != null) Instance.HideLoadingPanel();
        }

        public static void Alert(string title, string message, Action onClosed = null, string confirmLabel = "확인")
        {
            if (Instance != null) Instance.EnqueueAlert(title, message, onClosed, confirmLabel);
            else
            {
                // UI가 없으면 로그만 남긴다. (콜백을 바로 부르면 "다시 시도" 같은 콜백이 무한 반복될 수 있다)
                Debug.LogWarning($"[SystemUI] (팝업) {title}: {message}");
            }
        }

        public static void Confirm(string title, string message, Action onYes, Action onNo = null,
            string yesLabel = "예", string noLabel = "아니오")
        {
            if (Instance != null) Instance.EnqueueConfirm(title, message, onYes, onNo, yesLabel, noLabel);
            else
            {
                Debug.LogWarning($"[SystemUI] (확인 팝업 - UI 없음, 자동으로 '예' 처리) {title}: {message}");
                if (Application.isPlaying) onYes?.Invoke();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
    }
}
