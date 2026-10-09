using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastTruck.Networking
{
    /// <summary>
    /// 방 목록의 한 줄: [자물쇠] 방 제목 ........ 상태  2 / 4
    /// 들어갈 수 없는 방(게임 중 IsOpen=false, 인원 마감)은 회색으로 표시되고 클릭이 막힌다.
    /// 프리팹(SessionListRow.prefab)은 에디터 메뉴가 자동으로 만든다.
    /// </summary>
    public class SessionListRow : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private GameObject lockIcon;

        [Header("상태 색상")]
        [SerializeField] private Color openColor = new Color(0.55f, 0.85f, 0.55f);
        [SerializeField] private Color closedColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField, Range(0f, 1f)] private float disabledAlpha = 0.45f;

        private SessionInfo _session;
        private Action<SessionInfo> _onClick;

        private void Awake()
        {
            button.onClick.AddListener(() =>
            {
                if (_session != null) _onClick?.Invoke(_session);
            });
        }

        public void Bind(SessionInfo session, Action<SessionInfo> onClick)
        {
            _session = session;
            _onClick = onClick;

            bool joinable = GameLauncher.IsJoinable(session);

            titleText.text = LobbyRules.SafeText(GameLauncher.ReadTitle(session));
            playerCountText.text = $"{session.PlayerCount} / {session.MaxPlayers}";
            lockIcon.SetActive(GameLauncher.HasPassword(session));

            if (!session.IsOpen)
            {
                statusText.text = "게임 중";
                statusText.color = closedColor;
            }
            else if (GameLauncher.IsFull(session))
            {
                statusText.text = "인원 마감";
                statusText.color = closedColor;
            }
            else
            {
                statusText.text = "모집 중";
                statusText.color = openColor;
            }

            button.interactable = joinable;
            canvasGroup.alpha = joinable ? 1f : disabledAlpha;
        }
    }
}
