using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastTruck.Networking
{
    /// <summary>
    /// 대기실의 캐릭터 선택 (오버워치/발로란트 스타일).
    ///
    /// 대기실 왼쪽: [내 캐릭터 초상화] [캐릭터 이름] [캐릭터 변경] 버튼
    /// 캐릭터 변경을 누르면 전체 화면 선택 창이 열린다.
    ///  - 아래쪽: 모든 캐릭터 초상화 (CharacterCatalog 순서, 많아지면 스크롤)
    ///  - 초상화를 누르면: 가운데에 큰 초상화 + 이름 + 설명 + 능력치 막대 (미리보기, 아직 확정 아님)
    ///  - 가운데 아래 [선택] 버튼: 확정 (지금 쓰는 캐릭터도 선택 가능) → 호스트에게 RPC로 알리고 모두의 대기실 목록에 반영
    ///  - [닫기] 또는 ESC: 바꾸지 않고 닫기
    /// 캐릭터 중복 선택은 허용한다.
    ///
    /// Menu 씬의 MenuCanvas/CharacterSelectPanel에 붙어 있고, 에디터 메뉴
    /// "LastTruck > Multiplayer > 3. 캐릭터 선택 UI 설치"가 만들고 연결한다.
    /// </summary>
    public class CharacterSelectUI : MonoBehaviour
    {
        #region 인스펙터 참조 / 상태 / 생명주기

        [Header("선택 창")]
        [SerializeField] private GameObject selectPanel;
        [SerializeField] private Transform slotContainer;
        [SerializeField] private CharacterPortraitSlot slotPrefab;
        [SerializeField] private Image previewPortrait;
        [SerializeField] private TMP_Text previewNameText;
        [SerializeField] private TMP_Text previewDescriptionText;
        [SerializeField] private RectTransform[] statFills = new RectTransform[0];
        [SerializeField] private TMP_Text[] statValueTexts = new TMP_Text[0];
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text emptyMessage;

        [Header("대기실 - 내 캐릭터")]
        [SerializeField] private Image roomPortrait;
        [SerializeField] private TMP_Text roomCharacterNameText;
        [SerializeField] private Button openButton;

        /// <summary>능력치 막대 순서 (에디터 설치 스크립트가 같은 순서로 막대를 만든다).</summary>
        public static readonly string[] StatLabels = { "체력", "공격력", "이동 속도", "제작 비용 감소", "채집 속도", "수리 보너스" };

        private readonly List<CharacterPortraitSlot> _slots = new List<CharacterPortraitSlot>();
        private GameLauncher _launcher;
        private int _previewIndex;
        private bool _slotsBuilt;

        public bool IsOpen => selectPanel != null && selectPanel.activeSelf;

        private int _requestedIndex = -1;

        /// <summary>
        /// 지금 내가 쓰고 있는 캐릭터 번호. 호스트가 확정한 값(LobbyPlayerEntry)을 기준으로 하되,
        /// [변경]을 누른 직후 호스트 응답이 오기 전까지는 요청한 번호를 보여준다.
        /// </summary>
        private int CurrentIndex
        {
            get
            {
                LobbyPlayerEntry local = LobbyPlayerEntry.Local;
                if (local == null) return PlayerProfile.CharacterIndex;
                if (_requestedIndex >= 0 && local.CharacterIndex != _requestedIndex) return _requestedIndex;
                _requestedIndex = -1;
                return local.CharacterIndex;
            }
        }

        private void Start()
        {
            _launcher = GameLauncher.Instance;

            selectPanel.SetActive(false);
            openButton.onClick.AddListener(Open);
            confirmButton.onClick.AddListener(Confirm);
            closeButton.onClick.AddListener(Close);

            if (_launcher != null) _launcher.StateChanged += OnLauncherStateChanged;
            LobbyPlayerEntry.Changed += RefreshRoomWidget;

            RefreshRoomWidget();
            OnLauncherStateChanged(_launcher != null ? _launcher.State : LauncherState.Offline);
        }

        private void OnDestroy()
        {
            if (_launcher != null) _launcher.StateChanged -= OnLauncherStateChanged;
            LobbyPlayerEntry.Changed -= RefreshRoomWidget;
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void OnLauncherStateChanged(LauncherState state)
        {
            // 대기실에서만 바꿀 수 있다. 게임 시작/나가기/연결 끊김이 되면 창을 닫는다.
            bool canChange = state == LauncherState.InRoom;
            openButton.interactable = canChange;
            if (!canChange && IsOpen) Close();
            if (state == LauncherState.InRoom) RefreshRoomWidget();
        }

        #endregion

        #region 열기 / 닫기 / 확정

        public void Open()
        {
            if (_launcher != null && _launcher.State != LauncherState.InRoom) return;

            BuildSlotsIfNeeded();
            _previewIndex = CurrentIndex;
            Preview(_previewIndex);

            selectPanel.SetActive(true);
            selectPanel.transform.SetAsLastSibling(); // 대기실 화면 위로
        }

        public void Close()
        {
            selectPanel.SetActive(false);
        }

        private void Confirm()
        {
            CharacterCatalog catalog = CharacterCatalog.Instance;
            if (catalog == null || catalog.Count == 0) return;

            if (_launcher != null && _launcher.State != LauncherState.InRoom)
            {
                Close();
                return;
            }

            int index = catalog.ClampIndex(_previewIndex);
            PlayerProfile.CharacterIndex = index;

            LobbyPlayerEntry local = LobbyPlayerEntry.Local;
            if (local != null)
            {
                _requestedIndex = index;
                local.RPC_SetCharacter(index);
            }

            Close();
            RefreshRoomWidget();
        }

        #endregion

        #region 화면 갱신

        private void BuildSlotsIfNeeded()
        {
            if (_slotsBuilt) return;
            _slotsBuilt = true;

            CharacterCatalog catalog = CharacterCatalog.Instance;
            int count = catalog != null ? catalog.Count : 0;

            for (int i = 0; i < count; i++)
            {
                CharacterPortraitSlot slot = Instantiate(slotPrefab, slotContainer);
                slot.gameObject.SetActive(true);
                slot.Bind(i, catalog.Get(i), Preview);
                _slots.Add(slot);
            }

            if (emptyMessage != null) emptyMessage.gameObject.SetActive(count == 0);
        }

        private void Preview(int index)
        {
            CharacterCatalog catalog = CharacterCatalog.Instance;
            CharacterStatsData data = catalog != null ? catalog.Get(catalog.ClampIndex(index)) : null;
            _previewIndex = catalog != null ? catalog.ClampIndex(index) : 0;

            int current = CurrentIndex;
            foreach (CharacterPortraitSlot slot in _slots)
            {
                slot.SetState(slot.Index == _previewIndex, slot.Index == current);
            }

            previewPortrait.sprite = data != null ? data.portrait : null;
            previewPortrait.color = data != null && data.portrait != null ? Color.white : new Color(1f, 1f, 1f, 0.06f);
            previewNameText.text = data != null ? LobbyRules.SafeText(data.DisplayName) : "캐릭터 없음";
            previewDescriptionText.text = data == null
                ? "캐릭터 목록이 비어 있습니다."
                : string.IsNullOrWhiteSpace(data.description)
                    ? "<color=#9AA3B2>(아직 설명이 없습니다)</color>"
                    : LobbyRules.SafeText(data.description);

            UpdateStats(catalog, data);

            // 지금 쓰는 캐릭터를 다시 골라도 [선택]으로 확정할 수 있다 (둘러보다가 원래 캐릭터로 돌아오는 경우).
            confirmButton.interactable = data != null;
            confirmLabel.text = "선택";
        }

        private void UpdateStats(CharacterCatalog catalog, CharacterStatsData data)
        {
            for (int i = 0; i < statFills.Length; i++)
            {
                float value = data != null ? GetStat(data, i) : 0f;

                // 막대 길이는 "목록에서 가장 높은 값" 대비 비율.
                float max = 0.0001f;
                if (catalog != null)
                {
                    foreach (CharacterStatsData other in catalog.Characters)
                    {
                        if (other != null) max = Mathf.Max(max, GetStat(other, i));
                    }
                }

                float ratio = Mathf.Clamp01(value / max);
                if (statFills[i] != null)
                {
                    statFills[i].anchorMin = new Vector2(0f, 0f);
                    statFills[i].anchorMax = new Vector2(ratio, 1f);
                    statFills[i].offsetMin = Vector2.zero;
                    statFills[i].offsetMax = Vector2.zero;
                }

                if (i < statValueTexts.Length && statValueTexts[i] != null)
                {
                    statValueTexts[i].text = data != null ? value.ToString("0.##") : "-";
                }
            }
        }

        private static float GetStat(CharacterStatsData data, int statIndex)
        {
            switch (statIndex)
            {
                case 0: return data.maxHealth;
                case 1: return data.attackPower;
                case 2: return data.moveSpeed;
                case 3: return data.costReducation;
                case 4: return data.harvestSpeed;
                case 5: return data.repairBonus;
                default: return 0f;
            }
        }

        private void RefreshRoomWidget()
        {
            CharacterCatalog catalog = CharacterCatalog.Instance;
            int index = CurrentIndex;

            Sprite portrait = catalog != null ? catalog.GetPortrait(index) : null;
            roomPortrait.sprite = portrait;
            roomPortrait.color = portrait != null ? Color.white : new Color(1f, 1f, 1f, 0.06f);
            roomCharacterNameText.text = catalog != null ? LobbyRules.SafeText(catalog.GetDisplayName(index)) : "캐릭터 목록 없음";
        }

        #endregion
    }
}
