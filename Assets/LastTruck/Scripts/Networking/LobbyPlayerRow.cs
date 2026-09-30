using TMPro;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 대기실 참가자 목록의 한 줄: [방장 마크] 닉네임 (나) ........ [준비 완료]
    /// - 방장 마크: 방장(호스트)일 때만 켜진다.
    /// - 준비 완료 마크: 방장이 아닌 참가자가 준비했을 때만 켜진다.
    /// 프리팹(LobbyPlayerRow.prefab)은 에디터 메뉴가 자동으로 만든다.
    /// </summary>
    public class LobbyPlayerRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private GameObject hostBadge;
        [SerializeField] private GameObject readyBadge;
        [SerializeField] private TMP_Text waitingText;

        public void Set(string nickname, bool isHost, bool isReady, bool isLocal)
        {
            string safeName = LobbyRules.SafeText(nickname);
            nicknameText.text = isLocal ? $"{safeName} <color=#E8913A>(나)</color>" : safeName;
            hostBadge.SetActive(isHost);
            readyBadge.SetActive(!isHost && isReady);
            if (waitingText != null) waitingText.gameObject.SetActive(!isHost && !isReady);
        }
    }
}
