using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastTruck.Networking
{
    /// <summary>
    /// 대기실 참가자 목록의 한 줄: [방장 마크] [초상화] 닉네임 (나) .... 캐릭터 이름 [준비 완료]
    /// - 방장 마크: 방장(호스트)일 때만 켜진다.
    /// - 준비 완료 마크: 방장이 아닌 참가자가 준비했을 때만 켜진다.
    /// - 초상화/캐릭터 이름: 그 참가자가 고른 캐릭터 (없으면 숨김).
    /// 프리팹(LobbyPlayerRow.prefab)은 에디터 메뉴가 자동으로 만든다.
    /// </summary>
    public class LobbyPlayerRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private GameObject hostBadge;
        [SerializeField] private GameObject readyBadge;
        [SerializeField] private TMP_Text waitingText;

        [Header("고른 캐릭터 (없어도 동작함)")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text characterNameText;

        public void Set(string nickname, bool isHost, bool isReady, bool isLocal)
        {
            string safeName = LobbyRules.SafeText(nickname);
            nicknameText.text = isLocal ? $"{safeName} <color=#E8913A>(나)</color>" : safeName;
            hostBadge.SetActive(isHost);
            readyBadge.SetActive(!isHost && isReady);
            if (waitingText != null) waitingText.gameObject.SetActive(!isHost && !isReady);
        }

        public void Set(string nickname, bool isHost, bool isReady, bool isLocal, int characterIndex)
        {
            Set(nickname, isHost, isReady, isLocal);

            CharacterCatalog catalog = CharacterCatalog.Instance;
            Sprite portrait = catalog != null ? catalog.GetPortrait(characterIndex) : null;

            if (portraitImage != null)
            {
                portraitImage.sprite = portrait;
                // 초상화가 아직 없으면(촬영 전) 빈 칸 대신 어두운 사각형으로 둔다.
                portraitImage.color = portrait != null ? Color.white : new Color(1f, 1f, 1f, 0.08f);
            }

            if (characterNameText != null)
            {
                characterNameText.text = catalog != null ? catalog.GetDisplayName(characterIndex) : string.Empty;
            }
        }
    }
}
