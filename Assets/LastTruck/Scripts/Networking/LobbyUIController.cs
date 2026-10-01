using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastTruck.Networking
{
    /// <summary>
    /// Menu 씬의 화면을 그리는 스크립트. 네트워크 처리는 전부 GameLauncher.Instance에 맡기고,
    /// 여기서는 "어떤 패널을 켤지 / 무엇을 표시할지 / 버튼이 눌리면 GameLauncher의 무엇을 부를지"만 다룬다.
    ///
    /// 화면 구성
    ///  - 방 목록(SessionListPanel): 닉네임 수정, 방 만들기 버튼, 방 목록(자동 갱신)
    ///  - 방 만들기(CreateRoomPanel, 모달): 제목 / 최대 인원 / 비공개 토글 / 비밀번호
    ///  - 비밀번호 입력(PasswordPanel, 모달): 자물쇠 방을 눌렀을 때
    ///  - 대기실(RoomPanel): 방 제목, 참가자 목록(방장 마크/준비 마크), 준비 버튼(참가자), 시작 버튼(방장), 나가기
    /// 로딩 패널과 팝업은 SystemUI(씬 전환에도 유지)가 담당한다.
    ///
    /// 이 씬과 모든 참조 연결은 에디터 메뉴 "LastTruck > Multiplayer > Build Menu Scene"이 자동으로 만든다.
    /// </summary>
    public class LobbyUIController : MonoBehaviour
    {
        #region 인스펙터 참조 / 상태

        [Header("패널")]
        [SerializeField] private GameObject sessionListPanel;
        [SerializeField] private GameObject createRoomPanel;
        [SerializeField] private GameObject passwordPanel;
        [SerializeField] private GameObject roomPanel;

        [Header("방 목록 화면")]
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private Button openCreateRoomButton;
        [SerializeField] private TMP_Text sessionCountText;
        [SerializeField] private Transform sessionListContent;
        [SerializeField] private SessionListRow sessionRowPrefab;
        [SerializeField] private GameObject emptyListMessage;

        [Header("방 만들기 화면")]
        [SerializeField] private TMP_InputField roomTitleInput;
        [SerializeField] private TMP_Text maxPlayersText;
        [SerializeField] private Button maxPlayersMinusButton;
        [SerializeField] private Button maxPlayersPlusButton;
        [SerializeField] private Toggle privateToggle;
        [SerializeField] private TMP_Text privateToggleText;
        [SerializeField] private TMP_InputField createPasswordInput;
        [SerializeField] private CanvasGroup createPasswordGroup;
        [SerializeField] private Button createConfirmButton;
        [SerializeField] private Button createCancelButton;

        [Header("비밀번호 입력 화면")]
        [SerializeField] private TMP_Text passwordRoomTitleText;
        [SerializeField] private TMP_InputField joinPasswordInput;
        [SerializeField] private Button passwordConfirmButton;
        [SerializeField] private Button passwordCancelButton;

        [Header("대기실 화면")]
        [SerializeField] private TMP_Text roomTitleText;
        [SerializeField] private TMP_Text roomInfoText;
        [SerializeField] private TMP_Text roomPasswordText;
        [SerializeField] private Transform playerListContent;
        [SerializeField] private LobbyPlayerRow playerRowPrefab;
        [SerializeField] private TMP_Text roomStatusText;
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonLabel;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveRoomButton;

        [Header("대기실 안전 갱신 주기(초) - 이벤트를 놓쳐도 이 주기로 다시 그린다")]
        [SerializeField] private float roomRefreshInterval = 0.5f;

        private GameLauncher _launcher;
        private readonly List<SessionListRow> _sessionRows = new List<SessionListRow>();
        private readonly List<LobbyPlayerRow> _playerRows = new List<LobbyPlayerRow>();
        private int _createMaxPlayers = LobbyRules.DefaultMaxPlayers;
        private SessionInfo _pendingPasswordSession;
        private float _refreshTimer;

        #endregion

        #region 초기화

        private void Start()
        {
            _launcher = GameLauncher.Instance;
            if (_launcher == null)
            {
                Debug.LogError("[LobbyUIController] 씬에 GameLauncher가 없습니다.");
                enabled = false;
                return;
            }

            // 입력칸 글자 수 제한
            nicknameInput.characterLimit = LobbyRules.NicknameMaxLength;
            roomTitleInput.characterLimit = LobbyRules.TitleMaxLength;
            createPasswordInput.characterLimit = LobbyRules.PasswordMaxLength;
            joinPasswordInput.characterLimit = LobbyRules.PasswordMaxLength;

            // 방 목록 화면
            nicknameInput.text = PlayerProfile.Nickname;
            nicknameInput.onEndEdit.AddListener(OnNicknameEdited);
            openCreateRoomButton.onClick.AddListener(OpenCreateRoomPanel);

            // 방 만들기 화면
            maxPlayersMinusButton.onClick.AddListener(() => ChangeMaxPlayers(-1));
            maxPlayersPlusButton.onClick.AddListener(() => ChangeMaxPlayers(+1));
            privateToggle.onValueChanged.AddListener(_ => RefreshPasswordFieldState());
            createConfirmButton.onClick.AddListener(OnCreateConfirmClicked);
            createCancelButton.onClick.AddListener(() => createRoomPanel.SetActive(false));

            // 비밀번호 화면
            passwordConfirmButton.onClick.AddListener(OnPasswordConfirmClicked);
            passwordCancelButton.onClick.AddListener(ClosePasswordPanel);

            // 대기실 화면
            readyButton.onClick.AddListener(OnReadyClicked);
            startGameButton.onClick.AddListener(() => _launcher.StartGameAsHost());
            leaveRoomButton.onClick.AddListener(() => _launcher.RequestLeaveSession());

            _launcher.StateChanged += OnLauncherStateChanged;
            _launcher.SessionListUpdated += OnSessionListUpdated;
            LobbyPlayerEntry.Changed += RefreshRoom;

            createRoomPanel.SetActive(false);
            passwordPanel.SetActive(false);
            OnLauncherStateChanged(_launcher.State);
            OnSessionListUpdated(_launcher.Sessions);
        }

        private void OnDestroy()
        {
            if (_launcher != null)
            {
                _launcher.StateChanged -= OnLauncherStateChanged;
                _launcher.SessionListUpdated -= OnSessionListUpdated;
            }
            LobbyPlayerEntry.Changed -= RefreshRoom;
        }

        private void Update()
        {
            if (!roomPanel.activeSelf) return;

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer < roomRefreshInterval) return;
            _refreshTimer = 0f;
            RefreshRoom();
        }

        #endregion

        #region 상태에 따른 화면 전환

        private void OnLauncherStateChanged(LauncherState state)
        {
            bool inRoom = state == LauncherState.InRoom || state == LauncherState.StartingGame;
            bool inList = state == LauncherState.Offline || state == LauncherState.ConnectingLobby
                          || state == LauncherState.InLobby || state == LauncherState.CreatingSession
                          || state == LauncherState.JoiningSession;

            // Leaving / InGame 동안에는 현재 화면을 그대로 둔다 (로딩 패널이 위를 덮고 있음).
            if (!inRoom && !inList) return;

            roomPanel.SetActive(inRoom);
            sessionListPanel.SetActive(inList);

            if (state != LauncherState.InLobby)
            {
                // 방 목록에서만 쓰는 모달들은 다른 상태가 되면 닫는다.
                createRoomPanel.SetActive(false);
                ClosePasswordPanel();
            }

            openCreateRoomButton.interactable = state == LauncherState.InLobby;
            if (inRoom) RefreshRoom();
        }

        #endregion

        #region 방 목록

        private void OnNicknameEdited(string value)
        {
            if (PlayerProfile.Clean(value) == PlayerProfile.Nickname) return;

            if (!PlayerProfile.TrySetNickname(value, out string error))
            {
                SystemUI.Alert("닉네임", error);
            }
            nicknameInput.SetTextWithoutNotify(PlayerProfile.Nickname);
        }

        private void OnSessionListUpdated(IReadOnlyList<SessionInfo> sessions)
        {
            // 입장 가능한 방을 위로, 회색(게임 중/마감) 방을 아래로.
            List<SessionInfo> sorted = sessions
                .OrderByDescending(GameLauncher.IsJoinable)
                .ThenBy(GameLauncher.ReadTitle)
                .ToList();

            // 행 오브젝트를 재사용해서 깜빡임과 스크롤 초기화를 막는다.
            for (int i = 0; i < sorted.Count; i++)
            {
                SessionListRow row;
                if (i < _sessionRows.Count)
                {
                    row = _sessionRows[i];
                }
                else
                {
                    row = Instantiate(sessionRowPrefab, sessionListContent);
                    _sessionRows.Add(row);
                }
                row.gameObject.SetActive(true);
                row.Bind(sorted[i], OnSessionRowClicked);
            }
            for (int i = sorted.Count; i < _sessionRows.Count; i++)
            {
                _sessionRows[i].gameObject.SetActive(false);
            }

            emptyListMessage.SetActive(sorted.Count == 0);
            int joinable = sorted.Count(GameLauncher.IsJoinable);
            sessionCountText.text = $"방 {sorted.Count}개 · 입장 가능 {joinable}개";
        }

        private void OnSessionRowClicked(SessionInfo session)
        {
            if (_launcher.State != LauncherState.InLobby) return;

            if (!GameLauncher.IsJoinable(session))
            {
                SystemUI.Alert("방 참가", "방이 가득 찼거나 이미 게임이 시작되었습니다.");
                return;
            }

            if (GameLauncher.HasPassword(session))
            {
                _pendingPasswordSession = session;
                passwordRoomTitleText.text = LobbyRules.SafeText(GameLauncher.ReadTitle(session));
                joinPasswordInput.text = string.Empty;
                passwordPanel.SetActive(true);
                joinPasswordInput.ActivateInputField();
            }
            else
            {
                _launcher.JoinSession(session, null);
            }
        }

        #endregion

        #region 비밀번호 입력

        private void OnPasswordConfirmClicked()
        {
            string password = joinPasswordInput.text.Trim();
            if (string.IsNullOrEmpty(password))
            {
                SystemUI.Alert("비밀번호", "비밀번호를 입력해 주세요.");
                return;
            }

            SessionInfo session = _pendingPasswordSession;
            ClosePasswordPanel();
            if (session != null) _launcher.JoinSession(session, password);
        }

        private void ClosePasswordPanel()
        {
            _pendingPasswordSession = null;
            passwordPanel.SetActive(false);
        }

        #endregion

        #region 방 만들기

        private void OpenCreateRoomPanel()
        {
            if (_launcher.State != LauncherState.InLobby) return;

            roomTitleInput.text = LobbyRules.DefaultRoomTitle(PlayerProfile.Nickname);
            _createMaxPlayers = LobbyRules.DefaultMaxPlayers;
            privateToggle.SetIsOnWithoutNotify(LobbyRules.DefaultPrivate);
            createPasswordInput.text = GeneratePassword();

            RefreshMaxPlayers();
            RefreshPasswordFieldState();
            createRoomPanel.SetActive(true);
        }

        private static string GeneratePassword()
        {
            var chars = new char[LobbyRules.GeneratedPasswordLength];
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)('0' + UnityEngine.Random.Range(0, 10));
            }
            return new string(chars);
        }

        private void ChangeMaxPlayers(int delta)
        {
            _createMaxPlayers = Mathf.Clamp(_createMaxPlayers + delta, LobbyRules.MinPlayers, LobbyRules.MaxPlayersLimit);
            RefreshMaxPlayers();
        }

        private void RefreshMaxPlayers()
        {
            maxPlayersText.text = _createMaxPlayers.ToString();
            maxPlayersMinusButton.interactable = _createMaxPlayers > LobbyRules.MinPlayers;
            maxPlayersPlusButton.interactable = _createMaxPlayers < LobbyRules.MaxPlayersLimit;
        }

        private void RefreshPasswordFieldState()
        {
            bool isPrivate = privateToggle.isOn;
            createPasswordInput.interactable = isPrivate;
            createPasswordGroup.alpha = isPrivate ? 1f : 0.4f;
            if (isPrivate && string.IsNullOrWhiteSpace(createPasswordInput.text))
            {
                createPasswordInput.text = GeneratePassword();
            }
        }

        private void OnCreateConfirmClicked()
        {
            // 제목: 비워두면 기본값("닉네임's room"), 입력했으면 그 값.
            string title = roomTitleInput.text.Trim();
            if (string.IsNullOrEmpty(title)) title = LobbyRules.DefaultRoomTitle(PlayerProfile.Nickname);
            if (title.Length > LobbyRules.TitleMaxLength) title = title.Substring(0, LobbyRules.TitleMaxLength);

            bool isPrivate = privateToggle.isOn;
            string password = null;
            if (isPrivate)
            {
                password = createPasswordInput.text.Trim();
                if (string.IsNullOrEmpty(password))
                {
                    SystemUI.Alert("방 만들기", "비공개 방은 비밀번호를 입력해야 합니다.");
                    return;
                }
            }

            createRoomPanel.SetActive(false);
            _launcher.CreateSession(title, _createMaxPlayers, isPrivate, password);
        }

        #endregion

        #region 대기실

        private void OnReadyClicked()
        {
            LobbyPlayerEntry local = LobbyPlayerEntry.Local;
            if (local == null || local.IsHost) return;
            _launcher.SetLocalReady(!local.IsReady);
        }

        private void RefreshRoom()
        {
            if (_launcher == null || !roomPanel.activeSelf) return;

            bool isHost = _launcher.IsHost;
            int playerCount = _launcher.RoomPlayerCount;
            int maxPlayers = _launcher.RoomMaxPlayers;

            roomTitleText.text = LobbyRules.SafeText(_launcher.RoomTitle);
            roomInfoText.text = $"{playerCount} / {maxPlayers}명 · {(_launcher.RoomIsPrivate ? "비공개 방" : "공개 방")}";

            string password = _launcher.HostPassword;
            roomPasswordText.gameObject.SetActive(isHost && !string.IsNullOrEmpty(password));
            if (roomPasswordText.gameObject.activeSelf)
            {
                roomPasswordText.text = $"비밀번호: {LobbyRules.SafeText(password)}  (함께할 친구에게 알려주세요)";
            }

            // 참가자 목록: 방장 먼저, 그다음 들어온 순서(PlayerId).
            List<LobbyPlayerEntry> entries = LobbyPlayerEntry.All
                .Where(e => e != null && e.IsAlive)
                .OrderByDescending(e => (bool)e.IsHost)
                .ThenBy(e => e.Owner.PlayerId)
                .ToList();

            for (int i = 0; i < entries.Count; i++)
            {
                LobbyPlayerRow row;
                if (i < _playerRows.Count)
                {
                    row = _playerRows[i];
                }
                else
                {
                    row = Instantiate(playerRowPrefab, playerListContent);
                    _playerRows.Add(row);
                }
                row.gameObject.SetActive(true);
                row.Set(entries[i].DisplayName, entries[i].IsHost, entries[i].IsReady, entries[i].IsLocal,
                    entries[i].CharacterIndex);
            }
            for (int i = entries.Count; i < _playerRows.Count; i++)
            {
                _playerRows[i].gameObject.SetActive(false);
            }

            // 버튼: 방장은 [시작], 참가자는 [준비]
            bool canInteract = _launcher.State == LauncherState.InRoom;
            startGameButton.gameObject.SetActive(isHost);
            readyButton.gameObject.SetActive(!isHost);
            leaveRoomButton.interactable = canInteract;

            if (isHost)
            {
                bool allReady = _launcher.AreAllPlayersReady();
                startGameButton.interactable = canInteract && allReady;

                int others = entries.Count(e => !e.IsHost);
                int readyOthers = entries.Count(e => !e.IsHost && e.IsReady);
                roomStatusText.text = others == 0
                    ? "혼자 시작할 수 있습니다."
                    : allReady
                        ? "모든 참가자가 준비되었습니다. 게임을 시작하세요!"
                        : $"모든 참가자가 준비하면 시작할 수 있습니다. ({readyOthers}/{others})";
            }
            else
            {
                LobbyPlayerEntry local = LobbyPlayerEntry.Local;
                bool ready = local != null && local.IsReady;
                readyButton.interactable = canInteract && local != null;
                readyButtonLabel.text = ready ? "준비 취소" : "준비";
                roomStatusText.text = ready
                    ? "방장이 게임을 시작하기를 기다리는 중..."
                    : "준비 버튼을 눌러 주세요.";
            }
        }

        public void PrivateToggleTextChange()
        {
            privateToggleText.text = privateToggle.isOn ? "비공개" : "공개";
        }

        #endregion
    }
}
