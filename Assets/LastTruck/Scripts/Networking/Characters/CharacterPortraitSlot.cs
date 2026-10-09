using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastTruck.Networking
{
    /// <summary>
    /// 캐릭터 선택 창 아래쪽에 쭉 나열되는 초상화 버튼 하나.
    /// - 누르면 가운데에 그 캐릭터 설명이 뜬다 (아직 확정 아님).
    /// - 테두리(previewFrame): 지금 설명을 보고 있는 캐릭터
    /// - 체크 표시(equippedMark): 지금 내가 쓰고 있는 캐릭터
    /// 프리팹(CharacterPortraitSlot.prefab)은 에디터 메뉴가 자동으로 만든다.
    /// </summary>
    public class CharacterPortraitSlot : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject previewFrame;
        [SerializeField] private GameObject equippedMark;

        private int _index;
        private Action<int> _onClick;

        public int Index => _index;

        private void Awake()
        {
            button.onClick.AddListener(() => _onClick?.Invoke(_index));
        }

        public void Bind(int index, CharacterStatsData data, Action<int> onClick)
        {
            _index = index;
            _onClick = onClick;

            Sprite portrait = data != null ? data.portrait : null;
            portraitImage.sprite = portrait;
            portraitImage.color = portrait != null ? Color.white : new Color(1f, 1f, 1f, 0.08f);
            if (nameText != null) nameText.text = data != null ? data.DisplayName : "?";
        }

        public void SetState(bool previewing, bool equipped)
        {
            if (previewFrame != null) previewFrame.SetActive(previewing);
            if (equippedMark != null) equippedMark.SetActive(equipped);
        }
    }
}
