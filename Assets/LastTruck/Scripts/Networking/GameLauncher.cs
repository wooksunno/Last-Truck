using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastTruck.Networking
{
    /// <summary>현재 네트워크 진행 단계. UI는 이 값을 보고 어떤 화면을 보여줄지 정한다.</summary>
    public enum LauncherState
    {
        Offline,          // 로비 서버에 연결 안 됨 (접속 실패 후 등)
        ConnectingLobby,  // 로비(방 목록) 서버에 접속 중
        InLobby,          // 방 목록 화면
        CreatingSession,  // 방 만드는 중
        JoiningSession,   // 방 들어가는 중
        InRoom,           // 방 안 (대기실)
        StartingGame,     // 게임 씬 불러오는 중
        InGame,           // 게임 씬 플레이 중
        Leaving,          // 방에서 나가는 중
    }

    /// <summary>
    /// Photon Fusion 2 (Host 모드) 세션 전체 흐름을 담당하는 관리자. 네트워킹 로직만 다루고 화면은 LobbyUIController가 그린다.
    ///
    /// 흐름: 메뉴 진입 → 로비 접속 → 방 목록 → (방 만들기 | 방 참가) → 대기실(준비) → 호스트가 시작 → 게임 씬
    ///
    /// - Menu 씬의 "[GameLauncher]" 오브젝트에 붙어 있고 DontDestroyOnLoad로 게임 씬까지 유지된다.
    /// - Menu 씬이 다시 로드되면 새로 생긴 중복 GameLauncher는 스스로 파괴되고, 기존 인스턴스(Instance)가 계속 쓰인다.
    ///   그래서 다른 스크립트는 인스펙터 연결 대신 GameLauncher.Instance로 접근해야 한다.
    /// - NetworkRunner는 한 번 끊기면 재사용할 수 없어서, 로비로 돌아갈 때마다 새로 만든다.
    /// </summary>
    public partial class GameLauncher : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static GameLauncher Instance { get; private set; }

        #region 이벤트 / 인스펙터 / 상태

        // ---------------- 이벤트 (UI가 구독) ----------------
        public event Action<LauncherState> StateChanged;
        public event Action<IReadOnlyList<SessionInfo>> SessionListUpdated;

        // ---------------- 인스펙터 ----------------
        [Header("방 참가자 정보 프리팹 (NetworkObject + LobbyPlayerEntry)")]
        [SerializeField] private NetworkObject lobbyPlayerEntryPrefab;

        [Header("씬 이름 (Build Settings에 등록되어 있어야 한다)")]
        [SerializeField] private string menuSceneName = "Menu";
        [SerializeField] private string gameSceneName = "MiningMapTest";

        [Header("플레이어 캐릭터 스폰 (캐릭터는 CharacterCatalog에서 각자 고른 것)")]
        [Tooltip("게임 씬에 싱글플레이용 캐릭터가 없어서 스폰 위치를 못 찾았을 때 쓸 중심점.")]
        [SerializeField] private Vector3 spawnOrigin = Vector3.zero;
        [Tooltip("스폰 중심점에서 각 플레이어까지의 거리 (서로 겹치지 않게).")]
        [SerializeField] private float spawnRadius = 1.5f;

        // ---------------- 상태 ----------------
        private NetworkRunner _runner;
        private LauncherState _state = LauncherState.Offline;
        private int _operationId;
        private bool _isDuplicate;
        private bool _quitting; // 플레이 종료 중에는 재접속/씬 로드를 하지 않는다.

        private List<SessionInfo> _sessions = new List<SessionInfo>();
        private readonly Dictionary<PlayerRef, NetworkObject> _entryObjects = new Dictionary<PlayerRef, NetworkObject>();
        private readonly Dictionary<PlayerRef, NetworkObject> _characterObjects = new Dictionary<PlayerRef, NetworkObject>();

        private string _roomTitle = string.Empty;
        private bool _roomIsPrivate;
        private string _hostPassword;          // 호스트만 가지고 있다. 공개 방이면 null.
        private string _cachedHostNickname = string.Empty;
        private bool _wasHostInSession;

        // ---------------- 외부 조회용 ----------------
        public LauncherState State => _state;
        public NetworkRunner Runner => _runner;
        public IReadOnlyList<SessionInfo> Sessions => _sessions;
        public bool IsHost => _runner != null && _runner.IsRunning && _runner.IsServer;
        public string RoomTitle => _roomTitle;
        public bool RoomIsPrivate => _roomIsPrivate;
        /// <summary>호스트에게만 값이 있다 (대기실에서 방장에게 비밀번호를 보여주기 위함).</summary>
        public string HostPassword => IsHost ? _hostPassword : null;
        public string CachedHostNickname => _cachedHostNickname;

        /// <summary>게임 중 지금 방장의 닉네임 (방장 변경 뒤 "호스트 접속 끊김" 안내에 쓴다).</summary>
        public void SetCachedHostNickname(string nickname)
        {
            if (!string.IsNullOrEmpty(nickname)) _cachedHostNickname = nickname;
        }

        public int RoomMaxPlayers
        {
            get
            {
                if (_runner == null || !_runner.IsRunning || _runner.SessionInfo == null) return 0;
                return _runner.SessionInfo.MaxPlayers;
            }
        }

        public int RoomPlayerCount => _runner != null && _runner.IsRunning ? _runner.ActivePlayers.Count() : 0;

        /// <summary>
        /// 지금 네트워크 세션(방/게임) 안에 있는가? 게임 씬 스크립트가 "멀티플레이로 들어왔는지" 판단할 때 쓴다.
        /// Demo_01에서 바로 Play를 누른 경우(싱글플레이)에는 false.
        /// </summary>
        public static bool IsOnlineSession
        {
            get
            {
                GameLauncher launcher = Instance;
                if (launcher == null) return false;
                // 방장 변경 중에는 러너가 잠깐 없지만 여전히 멀티플레이 중이다 (싱글플레이용 처리로 빠지지 않게).
                if (launcher._migrating) return true;
                return launcher._runner != null && launcher._runner.IsRunning
                       && (launcher._state == LauncherState.StartingGame || launcher._state == LauncherState.InGame
                           || launcher._state == LauncherState.InRoom);
            }
        }

        #endregion

        #region Unity 생명주기

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _isDuplicate = true;
                enabled = false;
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent != null) transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            LobbyPlayerEntry.Changed += OnLobbyEntriesChanged;
        }

        private void Start()
        {
            Screen.SetResolution(800, 800, FullScreenMode.Windowed);
            if (_isDuplicate) return;
            EnterLobby();
        }

        private void OnDestroy()
        {
            if (_isDuplicate) return;
            _quitting = true;
            BeginOperation();
            LobbyPlayerEntry.Changed -= OnLobbyEntriesChanged;
            if (Instance == this) Instance = null;
        }

        private void OnApplicationQuit()
        {
            _quitting = true;
            BeginOperation(); // 진행 중이던 비동기 작업의 이후 처리를 모두 무시한다.

            // 창을 닫을 때 정상 종료 신호를 보내서 다른 사람들이 끊김을 바로 알 수 있게 한다.
            if (_runner != null)
            {
                NetworkRunner runner = _runner;
                _runner = null;
                runner.Shutdown();
            }
        }

        #endregion

        #region 1. 로비(방 목록) 접속

        /// <summary>새 러너를 만들어 로비에 접속한다. 이미 접속 중이면 무시한다.</summary>
        public async void EnterLobby()
        {
            if (_state == LauncherState.ConnectingLobby) return;
            await EnterLobbyAsync();
        }

        private async Task EnterLobbyAsync()
        {
            if (!CanContinue()) return;
            int op = BeginOperation();
            await ShutdownRunnerAsync();
            if (op != _operationId) return;

            SetState(LauncherState.ConnectingLobby);
            SystemUI.ShowLoading("서버에 접속하는 중...");

            _sessions = new List<SessionInfo>();
            SessionListUpdated?.Invoke(_sessions);

            _runner = CreateRunner();
            OperationResult result = await AwaitWithTimeout(_runner.JoinSessionLobby(SessionLobby.ClientServer));
            if (op != _operationId) return;

            if (!result.Ok)
            {
                await ShutdownRunnerAsync();
                SetState(LauncherState.Offline);
                SystemUI.HideLoading();
                SystemUI.Alert("서버 접속 실패", result.Message, EnterLobby, "다시 시도");
                return;
            }

            SetState(LauncherState.InLobby);
            SystemUI.HideLoading();
        }

        #endregion

        #region 2. 방 만들기 (호스트)

        public async void CreateSession(string title, int maxPlayers, bool isPrivate, string password)
        {
            if (_state != LauncherState.InLobby || _runner == null) return;

            int op = BeginOperation();
            maxPlayers = Mathf.Clamp(maxPlayers, LobbyRules.MinPlayers, LobbyRules.MaxPlayersLimit);

            _roomTitle = title;
            _roomIsPrivate = isPrivate;
            _cachedHostNickname = PlayerProfile.Nickname;
            _hostPassword = isPrivate ? password : null;

            SetState(LauncherState.CreatingSession);
            SystemUI.ShowLoading("방을 만드는 중...", CancelCurrentOperation);

            var args = new StartGameArgs
            {
                GameMode = GameMode.Host,
                // 세션 이름은 방을 구분하는 고유 키라서 겹치면 안 된다. 화면에 보이는 제목은 프로퍼티로 따로 보낸다.
                SessionName = Guid.NewGuid().ToString("N"),
                PlayerCount = maxPlayers,
                IsOpen = true,
                // 1인 방은 어차피 아무도 못 들어오니 목록에서 숨긴다.
                IsVisible = maxPlayers > 1,
                SessionProperties = new Dictionary<string, SessionProperty>
                {
                    [LobbyRules.PropTitle] = title,
                    [LobbyRules.PropHasPassword] = isPrivate ? 1 : 0,
                    [LobbyRules.PropVersion] = SessionTicket.GameVersion,
                    [LobbyRules.PropInGame] = 0,
                },
                ConnectionToken = SessionTicket.Build(password),
                SceneManager = _runner.GetComponent<NetworkSceneManagerDefault>(),
            };

            OperationResult result = await AwaitWithTimeout(_runner.StartGame(args));
            if (op != _operationId) return;

            if (result.Ok && !IsRunnerAlive())
            {
                result = new OperationResult(false, "방을 만드는 중 서버와의 연결이 끊어졌습니다.");
            }
            if (result.Ok) RememberSession(_runner.SessionInfo != null ? _runner.SessionInfo.Name : args.SessionName, title, isPrivate, password);

            if (!result.Ok)
            {
                _hostPassword = null;
                SystemUI.Alert("방 만들기 실패", result.Message);
                await ReturnToLobbyAsync();
                return;
            }

            _wasHostInSession = true;
            SetState(LauncherState.InRoom);
            SystemUI.HideLoading();
            // 호스트 자신의 참가자 엔트리는 OnPlayerJoined(호스트 본인)에서 스폰된다.
        }

        #endregion

        #region 3. 방 참가 (클라이언트)

        /// <summary>비공개 방이면 password에 입력한 비밀번호, 공개 방이면 null.</summary>
        public void JoinSession(SessionInfo session, string password)
        {
            if (_state != LauncherState.InLobby || _runner == null || session == null) return;

            string version = ReadVersion(session);
            if (version != SessionTicket.GameVersion)
            {
                SystemUI.Alert("방 참가 실패", "게임 버전이 다른 방입니다.\n같은 버전의 게임으로 다시 시도해 주세요.");
                return;
            }
            JoinSessionByName(session.Name, ReadTitle(session), HasPassword(session), password);
        }

        /// <summary>세션 이름으로 바로 참가 (방 목록 클릭 / 재접속 공용).</summary>
        private async void JoinSessionByName(string sessionName, string title, bool isPrivate, string password)
        {
            if (_state != LauncherState.InLobby || _runner == null || string.IsNullOrEmpty(sessionName)) return;

            int op = BeginOperation();
            _roomTitle = title;
            _cachedHostNickname = string.Empty;
            _roomIsPrivate = isPrivate;
            _hostPassword = null;

            SetState(LauncherState.JoiningSession);
            SystemUI.ShowLoading("방에 들어가는 중...", CancelCurrentOperation);

            var args = new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = sessionName,
                // 비밀번호/버전/기기 ID는 세션 프로퍼티가 아니라 접속 토큰(입장권)으로 보낸다. 호스트가 OnConnectRequest에서 확인한다.
                ConnectionToken = SessionTicket.Build(password),
                SceneManager = _runner.GetComponent<NetworkSceneManagerDefault>(),
            };

            OperationResult result = await AwaitWithTimeout(_runner.StartGame(args));
            if (op != _operationId) return;

            if (result.Ok && !IsRunnerAlive())
            {
                result = new OperationResult(false, "방에 들어가는 중 연결이 끊어졌습니다.");
            }

            if (!result.Ok)
            {
                string message = result.Message;
                if (message == RefusedMessage)
                {
                    // 호스트가 거절한 이유는 전달되지 않는다 → 상황으로 짐작해서 알려 준다.
                    bool rejoining = CanReconnect && sessionName == _lastSessionName;
                    if (rejoining) message = "방장이 재접속을 받아 주지 않았습니다.\n(재접속 대기 시간이 지났거나, 내 캐릭터를 찾지 못했습니다)";
                    else if (!isPrivate) message = "들어갈 수 없는 방입니다.\n(이미 게임이 진행 중이거나 버전이 다릅니다)";
                }
                SystemUI.Alert("방 참가 실패", message);
                await ReturnToLobbyAsync();
                return;
            }

            RememberSession(sessionName, title, isPrivate, password);
            _wasHostInSession = false;

            // 재접속: 이미 게임 중인 방이면 접속하는 동안 게임 씬이 먼저 불러와졌을 수 있다 → 대기실 화면으로 돌리지 않는다.
            bool gameSceneLoaded = SceneManager.GetSceneByName(gameSceneName).isLoaded;
            if (_state == LauncherState.StartingGame || _state == LauncherState.InGame || gameSceneLoaded)
            {
                if (_state != LauncherState.InGame && gameSceneLoaded) OnSceneLoadDone(_runner);
                return;
            }

            SetState(LauncherState.InRoom);
            SystemUI.HideLoading();
        }

        #endregion

        #region 4. 준비 / 게임 시작

        /// <summary>클라이언트의 준비 버튼. 호스트에게 RPC로 요청한다.</summary>
        public void SetLocalReady(bool ready)
        {
            if (_state != LauncherState.InRoom) return;
            LobbyPlayerEntry local = LobbyPlayerEntry.Local;
            if (local != null && !local.IsHost) local.RPC_SetReady(ready);
        }

        /// <summary>호스트를 제외한 모든 참가자가 준비됐는가? (혼자면 항상 true)</summary>
        public bool AreAllPlayersReady()
        {
            if (_runner == null || !_runner.IsRunning) return false;

            int playerCount = _runner.ActivePlayers.Count();
            IReadOnlyList<LobbyPlayerEntry> entries = LobbyPlayerEntry.All;

            // 엔트리가 아직 다 스폰/복제되지 않았으면 준비 완료로 보지 않는다.
            if (entries.Count < playerCount) return false;

            foreach (LobbyPlayerEntry entry in entries)
            {
                if (!entry.IsHost && !entry.IsReady) return false;
            }
            return true;
        }

        /// <summary>대기실의 "게임 시작" 버튼 (호스트 전용).</summary>
        public void StartGameAsHost()
        {
            if (!IsHost || _state != LauncherState.InRoom) return;

            // 버튼이 켜진 뒤 누군가 준비를 풀었을 수도 있으니 누르는 순간 한 번 더 확인한다.
            if (!AreAllPlayersReady())
            {
                SystemUI.Alert("게임 시작", "아직 준비하지 않은 참가자가 있습니다.");
                return;
            }

            int gameIndex = FindBuildIndex(gameSceneName);
            if (gameIndex < 0)
            {
                SystemUI.Alert("게임 시작 실패", $"'{gameSceneName}' 씬이 Build Settings에 등록되어 있지 않습니다.");
                return;
            }

            // 게임 중에는 새 사람은 못 들어오고(호스트가 OnConnectRequest에서 거절), 끊겼던 사람의 재접속만 받는다.
            // 그래서 방은 열어 두고(IsOpen) "게임 중" 표시만 한다 → 방 목록에서 회색 + 재접속 가능한 사람만 누를 수 있다.
            NetworkPlayer.ClearLoadedPlayers();
            _gameOver = false;

            // 모든 참가자가 같은 맵을 만들도록 시드를 정해 방장 엔트리와 세션 프로퍼티에 넣는다
            // (엔트리: 씬 로드보다 먼저 복제됨 / 프로퍼티: 재접속·방장 변경 뒤에도 남는다).
            int seed = NetworkMapSeed.CreateSeed();
            LobbyPlayerEntry hostEntry = LobbyPlayerEntry.Local;
            if (hostEntry != null) hostEntry.MapSeed = seed;
            try
            {
                _runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty>
                {
                    [LobbyRules.PropInGame] = 1,
                    [LobbyRules.PropSeed] = seed,
                });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            SetState(LauncherState.StartingGame);
            SystemUI.ShowLoading("게임을 불러오는 중...");
            _runner.LoadScene(SceneRef.FromIndex(gameIndex), LoadSceneMode.Single);
        }

        #endregion

        #region 5. 나가기 / 취소 / 로비 복귀

        /// <summary>
        /// 방(또는 게임)에서 나가기. 호스트는 모든 참가자가 함께 나가게 되므로 확인 팝업을 먼저 띄운다.
        /// 인게임 메뉴의 "나가기" 버튼도 이 함수를 부르면 된다.
        /// </summary>
        public void RequestLeaveSession()
        {
            if (_state != LauncherState.InRoom && _state != LauncherState.InGame) return;

            if (IsHost && RoomPlayerCount > 1)
            {
                SystemUI.Confirm("방 나가기",
                    "방장이 나가면 방이 사라지고\n모든 참가자가 방 목록으로 돌아갑니다.\n나가시겠습니까?",
                    LeaveSession, null, "나가기", "취소");
            }
            else
            {
                LeaveSession();
            }
        }

        /// <summary>ESC "게임 나가기" 확인 팝업 이름 (호스트 이탈/게임 오버 때 자동으로 닫는다).</summary>
        public const string LeaveGamePopupTag = "LeaveGame";

        /// <summary>인게임 ESC 메뉴: 게임에서 나가 방 목록으로 (확인 팝업 후).</summary>
        public void RequestLeaveFromGame()
        {
            if (_state != LauncherState.InGame) return;

            string message = IsHost && RoomPlayerCount > 1
                ? "방장이 나가면 잠시 게임이 멈춘 뒤\n다른 참가자가 방장이 되어 이어서 진행합니다.\n나가시겠습니까?"
                : $"게임에서 나가 방 목록으로 돌아가시겠습니까?\n({ReconnectWindowSeconds / 60f:0}분 안에 다시 들어오면 이어서 할 수 있습니다)";
            SystemUI.Confirm("게임 나가기", message, LeaveSession, null, "나가기", "계속하기", LeaveGamePopupTag);
        }

        private async void LeaveSession()
        {
            if (_state != LauncherState.InRoom && _state != LauncherState.InGame) return;
            // 게임 도중 (게임 오버 전에) 직접 나간 참가자는 3분 안에 다시 들어올 수 있다. 방장은 나가면 방장이 바뀐다.
            bool inGame = _state == LauncherState.InGame && !_gameOver && !NetworkGameState.IsGameOver;
            MarkLeftGame(inGame);
            int op = BeginOperation();
            SetState(LauncherState.Leaving);
            SystemUI.ShowLoading("방에서 나가는 중...");

            // 방장이 게임 도중 나가면: 나가기 직전 상태를 올려 두어 새 방장이 되돌아감 없이 바로 이어받게 한다 (최대 2초 기다림).
            if (inGame && IsHost && RoomPlayerCount > 1 && IsRunnerAlive())
            {
                try
                {
                    Task<bool> push = _runner.PushHostMigrationSnapshot();
                    await Task.WhenAny(push, Task.Delay(2000));
                    LogSession(push.IsCompleted && push.Result ? "나가기 전 상태 저장 완료" : "나가기 전 상태 저장 실패 (마지막 자동 저장으로 이어감)");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                if (op != _operationId) return;
            }

            await ReturnToLobbyAsync();
        }

        /// <summary>로딩 패널의 "취소" 버튼. 진행 중인 방 만들기/참가를 포기하고 방 목록으로 돌아간다.</summary>
        public async void CancelCurrentOperation()
        {
            if (_state != LauncherState.CreatingSession && _state != LauncherState.JoiningSession) return;
            BeginOperation(); // 진행 중이던 작업의 결과는 무시된다.
            await ReturnToLobbyAsync();
        }

        /// <summary>러너 종료 → (필요하면) Menu 씬 로드 → 로비 재접속.</summary>
        private async Task ReturnToLobbyAsync()
        {
            _migrating = false;
            NetworkPause.EndAll();
            await ShutdownRunnerAsync();
            if (!CanContinue()) return;

            _roomTitle = string.Empty;
            _roomIsPrivate = false;
            _hostPassword = null;
            _gameOver = false;
            PlayerSpawnArea.Clear();

            if (SceneManager.GetActiveScene().name != menuSceneName)
            {
                SystemUI.ShowLoading("메뉴로 돌아가는 중...");
                AsyncOperation load = SceneManager.LoadSceneAsync(menuSceneName, LoadSceneMode.Single);
                if (load == null)
                {
                    Debug.LogError($"[GameLauncher] '{menuSceneName}' 씬을 불러올 수 없습니다. Build Settings를 확인하세요.");
                }
                else
                {
                    while (!load.isDone) await Task.Yield();
                }
                if (!CanContinue()) return;
            }

            await EnterLobbyAsync();
        }

        /// <summary>호스트 이탈 / 네트워크 끊김처럼 "내가 원하지 않았는데" 세션이 끊겼을 때.</summary>
        private async void HandleUnexpectedDisconnect(string reasonForLog)
        {
            if (!CanContinue()) return;
            LauncherState previous = _state;
            Debug.LogWarning($"[GameLauncher] 연결 끊김 ({previous}): {reasonForLog}");

            bool wasInSession = previous == LauncherState.InRoom
                                || previous == LauncherState.StartingGame
                                || previous == LauncherState.InGame;

            // 게임 오버 후 방장이 먼저 나가서 끊긴 경우: 이미 게임 오버 팝업이 떠 있으니 조용히 돌아간다.
            if (_gameOver)
            {
                _gameOver = false;
                BeginOperation();
                SetState(LauncherState.Leaving);
                await ReturnToLobbyAsync();
                return;
            }

            string message;
            if (wasInSession && !_wasHostInSession)
            {
                string hostName = string.IsNullOrEmpty(_cachedHostNickname) ? "알 수 없음" : _cachedHostNickname;
                message = $"호스트 접속 끊김: {hostName}";
            }
            else
            {
                message = "서버와의 연결이 끊어졌습니다.";
            }

            // 게임 도중 내 연결만 끊긴 경우(방장은 그대로): 방 목록으로 돌아간 뒤 "다시 접속" 버튼을 보여준다.
            MarkLeftGame(previous == LauncherState.InGame && !_wasHostInSession);
            bool offerReconnect = previous == LauncherState.InGame && !_wasHostInSession && CanReconnect;

            BeginOperation();
            SetState(LauncherState.Leaving);
            SystemUI.Dismiss(LeaveGamePopupTag); // ESC로 띄워 둔 "게임 나가기" 창이 있으면 닫는다
            if (!offerReconnect) SystemUI.Alert("연결 끊김", message);
            await ReturnToLobbyAsync();

            if (offerReconnect && _state == LauncherState.InLobby)
            {
                SystemUI.Confirm("연결 끊김",
                    $"{message}\n{ReconnectWindowSeconds / 60f:0}분 안에 다시 접속하면 이어서 할 수 있습니다.\n다시 접속할까요?",
                    TryReconnect, null, "다시 접속", "방 목록");
            }
        }

        #endregion

        #region 러너 관리 / 공용 도우미

        private NetworkRunner CreateRunner()
        {
            var go = new GameObject("NetworkRunner");
            DontDestroyOnLoad(go);

            var runner = go.AddComponent<NetworkRunner>();
            go.AddComponent<NetworkSceneManagerDefault>();
            runner.ProvideInput = true;
            runner.AddCallbacks(this);
            return runner;
        }

        private async Task ShutdownRunnerAsync()
        {
            NetworkRunner runner = _runner;
            _runner = null; // 먼저 비워둬서, 이 러너의 OnShutdown 콜백이 "예상치 못한 끊김"으로 처리되지 않게 한다.

            _entryObjects.Clear();
            _characterObjects.Clear();

            if (runner == null) return;

            runner.RemoveCallbacks(this);
            try
            {
                await runner.Shutdown();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (runner != null && runner.gameObject != null)
            {
                Destroy(runner.gameObject);
            }
        }

        private int BeginOperation()
        {
            return ++_operationId;
        }

        /// <summary>플레이 모드 종료 중이면 false (에디터에서 Stop을 눌렀을 때 러너가 새로 생기는 것 방지).</summary>
        private bool CanContinue()
        {
            return !_quitting && Application.isPlaying && this != null;
        }

        private bool IsRunnerAlive()
        {
            return _runner != null && _runner.IsRunning;
        }

        private void SetState(LauncherState state)
        {
            if (_state == state) return;
            _state = state;
            try
            {
                StateChanged?.Invoke(state);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private readonly struct OperationResult
        {
            public readonly bool Ok;
            public readonly string Message;

            public OperationResult(bool ok, string message)
            {
                Ok = ok;
                Message = message;
            }
        }

        /// <summary>Fusion 요청을 기다리되, 제한 시간 안에 끝나지 않으면 실패로 처리한다.</summary>
        private async Task<OperationResult> AwaitWithTimeout(Task<StartGameResult> task)
        {
            try
            {
                Task timeout = Task.Delay(TimeSpan.FromSeconds(LobbyRules.OperationTimeoutSeconds));
                Task finished = await Task.WhenAny(task, timeout);
                if (finished != task)
                {
                    // 나중에 실패로 끝나더라도 "관찰되지 않은 예외" 경고가 뜨지 않게 한다.
                    _ = task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                    return new OperationResult(false, "응답 시간이 초과되었습니다.\n네트워크 상태를 확인하고 다시 시도해 주세요.");
                }

                StartGameResult result = await task;
                if (result.Ok) return new OperationResult(true, null);

                Debug.LogWarning($"[GameLauncher] 요청 실패: {result.ShutdownReason} / {result.ErrorMessage}");
                return new OperationResult(false, DescribeFailure(result.ShutdownReason));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new OperationResult(false, "알 수 없는 오류가 발생했습니다.");
            }
        }

        private const string RefusedMessage = "비밀번호가 올바르지 않습니다.";

        private static string DescribeFailure(ShutdownReason reason)
        {
            switch (reason)
            {
                case ShutdownReason.GameIsFull:
                    return "방 인원이 가득 찼습니다.";
                case ShutdownReason.GameClosed:
                    return "이미 게임이 시작된 방입니다.";
                case ShutdownReason.GameNotFound:
                    return "방을 찾을 수 없습니다.\n방이 사라졌을 수 있어요.";
                case ShutdownReason.ConnectionRefused:
                    // 비공개 방이면 대부분 비밀번호 불일치. 공개 방/재접속이면 JoinSessionByName이 문구를 바꾼다.
                    return RefusedMessage;
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.OperationTimeout:
                    return "서버 응답 시간이 초과되었습니다.";
                case ShutdownReason.MaxCcuReached:
                    return "동시 접속자 수 한도를 초과했습니다.\n잠시 후 다시 시도해 주세요.";
                case ShutdownReason.InvalidRegion:
                    return "서버 지역 설정이 올바르지 않습니다.";
                case ShutdownReason.InvalidAuthentication:
                case ShutdownReason.CustomAuthenticationFailed:
                case ShutdownReason.AuthenticationTicketExpired:
                    return "Photon 인증에 실패했습니다.\nApp ID 설정을 확인해 주세요.";
                case ShutdownReason.OperationCanceled:
                    return "요청이 취소되었습니다.";
                default:
                    return $"연결에 실패했습니다. ({reason})";
            }
        }

        private static int FindBuildIndex(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (Path.GetFileNameWithoutExtension(path) == sceneName) return i;
            }
            return -1;
        }

        private void OnLobbyEntriesChanged()
        {
            LobbyPlayerEntry host = LobbyPlayerEntry.HostEntry;
            if (host != null)
            {
                string name = host.Nickname.ToString();
                if (!string.IsNullOrEmpty(name)) _cachedHostNickname = name;
            }
        }

        // ---------------- 세션 정보 읽기 (UI에서도 사용) ----------------

        public static string ReadTitle(SessionInfo session)
        {
            if (session != null && session.Properties != null
                && session.Properties.TryGetValue(LobbyRules.PropTitle, out SessionProperty prop)
                && prop.IsString)
            {
                return (string)prop.PropertyValue;
            }
            return "이름 없는 방";
        }

        public static bool HasPassword(SessionInfo session)
        {
            return session != null && session.Properties != null
                   && session.Properties.TryGetValue(LobbyRules.PropHasPassword, out SessionProperty prop)
                   && prop.IsInt && (int)prop.PropertyValue != 0;
        }

        public static bool IsFull(SessionInfo session)
        {
            return session.MaxPlayers > 0 && session.PlayerCount >= session.MaxPlayers;
        }

        public static bool IsJoinable(SessionInfo session)
        {
            if (session == null || !session.IsValid || !session.IsOpen || IsFull(session)) return false;
            // 게임 중인 방은 내가 끊겼던 방(재접속 가능 시간 안)만 들어갈 수 있다.
            if (IsInGame(session)) return CanRejoin(session);
            return true;
        }

        /// <summary>게임이 시작된 방인가 (세션 프로퍼티).</summary>
        public static bool IsInGame(SessionInfo session)
        {
            return session != null && session.Properties != null
                   && session.Properties.TryGetValue(LobbyRules.PropInGame, out SessionProperty prop)
                   && prop.IsInt && (int)prop.PropertyValue != 0;
        }

        /// <summary>방을 만든 게임 버전 (없으면 null = 예전 버전).</summary>
        public static string ReadVersion(SessionInfo session)
        {
            if (session != null && session.Properties != null
                && session.Properties.TryGetValue(LobbyRules.PropVersion, out SessionProperty prop) && prop.IsString)
            {
                return (string)prop.PropertyValue;
            }
            return null;
        }

        #endregion

        #region INetworkRunnerCallbacks

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            if (runner != _runner) return;
            // 같은 버전으로 만든 방만 보여준다 (다른 빌드끼리는 함께 할 수 없다).
            string version = SessionTicket.GameVersion;
            _sessions = sessionList.Where(s => s != null && s.IsValid && s.IsVisible && ReadVersion(s) == version).ToList();
            SessionListUpdated?.Invoke(_sessions);
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            // 호스트만 불린다.
            SessionTicket.Info ticket = SessionTicket.Parse(token);

            // 다른 버전 빌드는 받지 않는다 (방 목록에서도 안 보이지만, 이름으로 직접 들어오는 경우 대비).
            if (ticket.Version != SessionTicket.GameVersion)
            {
                LogSession($"접속 거절 (버전 다름: {ticket.Version ?? "예전 버전"})");
                request.Refuse();
                return;
            }

            if (!string.IsNullOrEmpty(_hostPassword) && ticket.Password != _hostPassword)
            {
                LogSession("접속 거절 (비밀번호 틀림)");
                request.Refuse();
                return;
            }

            // 대기실: 누구나 (방이 목록에 뜨는 시점과 StartGame 완료 사이에 들어오는 사람을 위해 CreatingSession도 허용)
            if (_state == LauncherState.InRoom || _state == LauncherState.CreatingSession)
            {
                request.Accept();
                return;
            }

            // 게임 중: 새 사람은 받지 않고, 끊겼던 사람(재접속 대기 캐릭터가 있는 기기)이거나
            // 방장 변경 직후 주인을 기다리는 캐릭터의 주인만 받는다.
            if (_state == LauncherState.InGame && !_gameOver && !NetworkGameState.IsGameOver && CanAcceptReturning(ticket.DeviceId))
            {
                LogSession("재접속 허용");
                request.Accept();
                return;
            }

            LogSession($"접속 거절 (게임 중 / 상태 {_state})");
            request.Refuse();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner != _runner || !runner.IsServer) return; // 스폰은 호스트만 한다.

            if (_state == LauncherState.StartingGame || _state == LauncherState.InGame)
            {
                // 게임 중: 재접속 / 방장 변경 뒤 다시 붙는 사람 → 원래 캐릭터를 돌려준다. 아니면 내보낸다.
                if (!HostAttachReturningPlayer(runner, player) && player != runner.LocalPlayer)
                {
                    LogSession($"게임 중 참가 → 원래 캐릭터가 없어 내보냄 ({player})");
                    runner.Disconnect(player);
                }
                return;
            }

            if (lobbyPlayerEntryPrefab == null)
            {
                Debug.LogError("[GameLauncher] lobbyPlayerEntryPrefab이 비어 있습니다. " +
                               "메뉴 'LastTruck > Multiplayer > Build Menu Scene'을 다시 실행하세요.");
                return;
            }

            if (_entryObjects.ContainsKey(player)) return;
            NetworkObject entry = runner.Spawn(lobbyPlayerEntryPrefab, Vector3.zero, Quaternion.identity, player);
            if (entry != null) _entryObjects[player] = entry;
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner != _runner || !runner.IsServer) return;

            if (_entryObjects.TryGetValue(player, out NetworkObject entry))
            {
                if (entry != null) runner.Despawn(entry);
                _entryObjects.Remove(player);
            }

            // 게임 중에 나간 사람의 캐릭터는 바로 없애지 않고 숨긴 채 재접속을 기다린다 (가방도 호스트가 보관).
            NetworkPlayer leaving = NetworkPlayer.Find(player);
            if (_state == LauncherState.InGame && leaving != null && !_gameOver)
            {
                LogSession($"{leaving.Nickname} 나감 → 재접속 대기 {ReconnectWindowSeconds:0}초");
                leaving.HostMarkDeparted(ReconnectWindowSeconds);
                _characterObjects.Remove(player);
                return;
            }

            if (_characterObjects.TryGetValue(player, out NetworkObject character))
            {
                if (character != null) runner.Despawn(character);
                _characterObjects.Remove(player);
            }
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
            if (runner != _runner) return;
            if (_reRegisteringScene) return; // 새 방장이 떠 있는 게임 씬을 다시 등록하는 중 (실제로 불러오지 않음)
            if (_state == LauncherState.InGame) return; // 이미 게임 중 (방장 변경 뒤 떠 있는 씬 이어받기) → 로딩 화면을 띄우지 않는다
            // 호스트가 시작을 누르면 모든 참가자(호스트 포함)에게 불린다 → 로딩 패널로 조작을 막는다.
            // 재접속(게임 중인 방에 들어가는 중)이면 접속이 끝나기 전에 불릴 수 있다 → 같이 게임 불러오기 단계로.
            if (_state == LauncherState.InRoom || _state == LauncherState.JoiningSession) SetState(LauncherState.StartingGame);
            SystemUI.ShowLoading("게임을 불러오는 중...");
            if (_state == LauncherState.StartingGame) StartCoroutine(SceneLoadWatchdog(runner));
        }

        /// <summary>게임 씬 불러오기가 너무 오래 걸리면(멈춤) 알리고 방 목록으로 돌아간다.</summary>
        private System.Collections.IEnumerator SceneLoadWatchdog(NetworkRunner runner)
        {
            yield return new WaitForSecondsRealtime(SceneLoadTimeoutSeconds);
            if (runner != _runner || _state != LauncherState.StartingGame) yield break;

            LogSession("씬 불러오기 시간 초과");
            MarkLeftGame(false);
            BeginOperation();
            SetState(LauncherState.Leaving);
            SystemUI.ShowLoading("방에서 나가는 중...");
            Task leaving = ReturnToLobbyAsync();
            while (!leaving.IsCompleted) yield return null;
            SystemUI.Alert("불러오기 실패", "게임을 불러오는 데 너무 오래 걸려 방 목록으로 돌아왔습니다.");
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (runner != _runner) return;
            if (_reRegisteringScene)
            {
                _reRegisteringScene = false;
                LogSession("새 방장: 게임 씬 등록 완료");
                return;
            }

            // 재접속(방장 변경 뒤 방) 등에서 게임 씬이 메뉴 위에 덧붙여(Additive) 불러와지는 경우가 있다
            // → 게임 씬을 활성 씬으로 만들고 메뉴 씬(대기실 화면/카메라)을 내린다.
            Scene gameScene = SceneManager.GetSceneByName(gameSceneName);
            if (gameScene.IsValid() && gameScene.isLoaded && SceneManager.GetActiveScene() != gameScene)
            {
                SceneManager.SetActiveScene(gameScene);
                Scene menuScene = SceneManager.GetSceneByName(menuSceneName);
                if (menuScene.IsValid() && menuScene.isLoaded)
                {
                    LogSession("메뉴 씬이 남아 있어 내림");
                    SceneManager.UnloadSceneAsync(menuScene);
                }
            }
            if (SceneManager.GetActiveScene().name != gameSceneName) return;

            SetState(LauncherState.InGame);
            _gameOver = false;

            // 방장 변경으로 다시 시작한 러너: 씬은 그대로 이어받고, 캐릭터/게임 상태는 이전 방장의 스냅샷에서 되살린다.
            if (runner.IsResume || _migrating) return;

            // 재접속: 이미 진행 중인 게임이면 대기 화면 없이 바로 (내 캐릭터는 호스트가 원래 것을 돌려준다).
            if (!runner.IsServer && NetworkGameState.Instance != null && NetworkGameState.Instance.Started)
            {
                SystemUI.HideLoading();
                return;
            }

            // 모든 참가자가 준비될 때까지 대기 화면 (NetworkGameState가 시작되면 닫는다).
            NetworkPrefabRegistry registry = NetworkPrefabRegistry.Instance;
            if (registry != null && registry.gameStatePrefab != null)
            {
                SystemUI.ShowLoading("다른 플레이어를 기다리는 중...");
                StartCoroutine(HideWaitingLoadingAfterTimeout(runner));
            }
            else
            {
                SystemUI.HideLoading();
            }

            // 호스트가 모든 참가자의 캐릭터를 스폰한다 (각자 대기실에서 고른 캐릭터로).
            // 아직 씬을 불러오는 중인 참가자에게는 로딩이 끝나는 대로 Fusion이 전달해 준다.
            if (runner.IsServer)
            {
                // 한 프레임 뒤에 스폰: 씬 오브젝트들의 Start(맵 생성 등)가 끝난 다음 바닥 높이를 재기 위해.
                StartCoroutine(SpawnAllCharactersNextFrame(runner));
            }
        }

        private System.Collections.IEnumerator SpawnAllCharactersNextFrame(NetworkRunner runner)
        {
            yield return null;
            if (runner != _runner || !IsRunnerAlive() || _state != LauncherState.InGame) yield break;

            int slot = 0;
            foreach (PlayerRef player in runner.ActivePlayers.OrderBy(p => p.PlayerId))
            {
                SpawnCharacterFor(runner, player, slot++);
            }

            // 게임 진행 상태 (준비 대기 → 낮/밤 동기화 → 게임 오버)
            NetworkPrefabRegistry registry = NetworkPrefabRegistry.Instance;
            if (registry != null && registry.gameStatePrefab != null)
            {
                runner.Spawn(registry.gameStatePrefab, Vector3.zero, Quaternion.identity);
            }
            else
            {
                Debug.LogWarning("[GameLauncher] NetworkPrefabRegistry.gameStatePrefab이 없어 낮/밤 동기화가 되지 않습니다. " +
                                 "메뉴 'LastTruck > Multiplayer > 1. 네트워크 프리팹 생성'을 실행하세요.");
            }
        }

        /// <summary>안전장치: 게임 상태 오브젝트를 끝내 못 받으면 대기 화면을 닫는다.</summary>
        private System.Collections.IEnumerator HideWaitingLoadingAfterTimeout(NetworkRunner runner)
        {
            yield return new WaitForSecondsRealtime(45f);
            bool stateMissing = NetworkGameState.Instance == null;
            if (runner == _runner && _state == LauncherState.InGame && stateMissing)
            {
                Debug.LogWarning("[GameLauncher] 게임 상태를 받지 못해 대기 화면을 닫습니다.");
                SystemUI.HideLoading();
            }
        }

        #endregion

        #region 게임 오버

        private bool _gameOver;

        /// <summary>NetworkGameState가 게임 오버를 알리면 호출 (이후 방장 이탈을 "호스트 끊김"으로 보여주지 않음).</summary>
        public void NotifyGameOver()
        {
            _gameOver = true;
            SystemUI.Dismiss(LeaveGamePopupTag);
        }

        /// <summary>게임 오버 팝업의 확인 버튼: 방 목록으로 돌아간다 (방장이면 방이 닫힌다).</summary>
        public void LeaveAfterGameOver()
        {
            if (_state == LauncherState.InGame || _state == LauncherState.InRoom)
            {
                LeaveSession();
            }
        }

        #endregion

        #region INetworkRunnerCallbacks (계속)

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (runner != _runner) return; // 우리가 직접 끈 러너는 이미 _runner에서 빠져 있다.
            if (IsOperationInProgress()) return; // 방 만들기/참가 실패는 해당 함수가 처리한다.

            _runner = null;
            HandleUnexpectedDisconnect(shutdownReason.ToString());
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (runner != _runner) return;
            if (IsOperationInProgress()) return;

            _runner = null; // 이후 이 러너의 OnShutdown은 runner != _runner라서 무시된다.
            runner.Shutdown();
            HandleUnexpectedDisconnect(reason.ToString());
        }

        private bool IsOperationInProgress()
        {
            return _state == LauncherState.ConnectingLobby
                   || _state == LauncherState.CreatingSession
                   || _state == LauncherState.JoiningSession
                   || _state == LauncherState.Leaving;
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (_state != LauncherState.InGame) return;

            var data = new NetworkInputData();

            // 채팅 입력 중이면 아무 입력도 보내지 않는다 (WASD/스페이스가 글자로만 들어가게).
            if (ChatHud.IsTyping)
            {
                input.Set(data);
                return;
            }

            // 내 캐릭터 상태: 트럭에 타는 등으로 PlayerMove가 꺼져 있으면 이동 입력을 보내지 않는다.
            NetworkPlayer localPlayer = NetworkPlayer.Local;
            PlayerMove localMove = localPlayer != null ? localPlayer.GetComponent<PlayerMove>() : null;
            bool canMove = localMove == null || (localMove.isActiveAndEnabled && localPlayer.gameObject.activeInHierarchy);
            // UI가 열려 있거나 공격 후딜/맨손 공격 등으로 이동이 잠겨 있으면 이동 입력을 보내지 않는다.
            if (localMove != null && localMove.IsMovementBlocked) canMove = false;
            data.SpeedMultiplier = localMove != null ? localMove.SpeedMultiplier : 1f;

            Camera cam = Camera.main;
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            if (cam != null)
            {
                Vector3 camForward = cam.transform.forward;
                Vector3 camRight = cam.transform.right;
                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();

                Vector3 moveDir = camForward * v + camRight * h;
                if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
                data.MoveDirection = canMove && NetworkGameState.AllowPlayerControl ? moveDir : Vector3.zero;
            }

            // 무기 조준 등으로 바라보는 방향을 바꾼 경우 (PlayerMove.SetFacing) 그 방향을 함께 보낸다.
            if (localMove != null && localMove.TryConsumeFacing(out Quaternion facing))
            {
                Vector3 forward = facing * Vector3.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f) data.FaceDirection = forward.normalized;
            }

            // 트럭에 타 있으면 캐릭터는 움직이지 않고, 운전석이면 같은 키로 트럭을 운전한다.
            int seat = localPlayer != null ? NetworkTruck.SeatOfPlayer(localPlayer.Owner) : -1;
            bool driving = false;
            if (seat >= 0)
            {
                data.MoveDirection = Vector3.zero;
                data.FaceDirection = Vector3.zero;
                bool dialogOpen = SystemUI.Instance != null && SystemUI.Instance.IsPopupVisible;
                if (seat == NetworkTruck.DriverSeat && NetworkGameState.AllowPlayerControl && !dialogOpen)
                {
                    driving = true;
                    data.TruckThrottle = Input.GetAxis("Vertical");
                    data.TruckSteer = Input.GetAxisRaw("Horizontal"); // 부드럽게 꺾는 것은 TruckMove.Steer가 한다
                }
            }

            var buttons = new NetworkButtons();
            buttons.Set(NetworkInputButton.TruckBrake, driving && Input.GetKey(KeyCode.Space));
            buttons.Set(NetworkInputButton.Walk, PlayerStamina.Sprinting);   // Walk 버튼 = 달리기 (Shift + 스태미나가 남아 있을 때)
            buttons.Set(NetworkInputButton.Attack, Input.GetMouseButton(0));
            buttons.Set(NetworkInputButton.Interact, Input.GetKey(KeyCode.E));
            // 점프: 걸어 다닐 때만 (트럭 탑승 중 Space는 브레이크)
            buttons.Set(NetworkInputButton.Jump, seat < 0 && canMove && NetworkGameState.AllowPlayerControl && Input.GetKey(KeyCode.Space));
            data.Buttons = buttons;

            input.Set(data);
        }

        /// <summary>호스트 전용: 한 참가자의 캐릭터를 스폰한다 (대기실에서 고른 캐릭터, 닉네임 포함).</summary>
        private void SpawnCharacterFor(NetworkRunner runner, PlayerRef player, int slot)
        {
            if (_characterObjects.ContainsKey(player)) return;

            LobbyPlayerEntry entry = LobbyPlayerEntry.Find(player);
            int characterIndex = entry != null ? entry.CharacterIndex : 0;
            string nickname = entry != null ? entry.Nickname.ToString() : "player";
            string deviceId = DeviceIdOf(runner, player);

            CharacterCatalog catalog = CharacterCatalog.Instance;
            NetworkObject prefab = catalog != null ? catalog.ResolveSpawnPrefab(ref characterIndex) : null;
            if (prefab == null)
            {
                Debug.LogError("[GameLauncher] 스폰할 네트워크 캐릭터 프리팹이 없습니다. " +
                               "메뉴 'LastTruck > Multiplayer > 1. 네트워크 프리팹 생성'을 실행하세요.");
                return;
            }

            PlayerSpawnArea.GetSpawnPose(slot, spawnOrigin, spawnRadius, out Vector3 position, out Quaternion rotation);

            NetworkObject character = runner.Spawn(prefab, position, rotation, player,
                (spawnRunner, spawned) =>
                {
                    NetworkInterest.Apply(spawnRunner, spawned, false);
                    NetworkPlayer networkPlayer = spawned.GetComponent<NetworkPlayer>();
                    if (networkPlayer != null) networkPlayer.InitializeBeforeSpawn(nickname, characterIndex, deviceId);
                });
            if (character != null)
            {
                _characterObjects[player] = character;
                runner.SetPlayerObject(player, character); // 방장 변경 때 "누구 캐릭터였는지" 스냅샷에 남긴다
            }
        }

        // 이번 단계에서 쓰지 않지만 인터페이스 구현을 위해 필요한 콜백들.
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        #endregion
    }
}
