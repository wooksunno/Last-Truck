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
    public class GameLauncher : MonoBehaviour, INetworkRunnerCallbacks
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
        [SerializeField] private string gameSceneName = "Demo_01";

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
                return launcher != null && launcher._runner != null && launcher._runner.IsRunning
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
                },
                SceneManager = _runner.GetComponent<NetworkSceneManagerDefault>(),
            };

            OperationResult result = await AwaitWithTimeout(_runner.StartGame(args));
            if (op != _operationId) return;

            if (result.Ok && !IsRunnerAlive())
            {
                result = new OperationResult(false, "방을 만드는 중 서버와의 연결이 끊어졌습니다.");
            }

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
        public async void JoinSession(SessionInfo session, string password)
        {
            if (_state != LauncherState.InLobby || _runner == null || session == null) return;

            int op = BeginOperation();
            _roomTitle = ReadTitle(session);
            _cachedHostNickname = string.Empty;
            _roomIsPrivate = HasPassword(session);
            _hostPassword = null;

            SetState(LauncherState.JoiningSession);
            SystemUI.ShowLoading("방에 들어가는 중...", CancelCurrentOperation);

            var args = new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = session.Name,
                // 비밀번호는 세션 프로퍼티가 아니라 접속 토큰으로 보낸다. 호스트가 OnConnectRequest에서 확인한다.
                ConnectionToken = string.IsNullOrEmpty(password) ? null : Encoding.UTF8.GetBytes(password),
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
                SystemUI.Alert("방 참가 실패", result.Message);
                await ReturnToLobbyAsync();
                return;
            }

            _wasHostInSession = false;
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

            // 전환 중/게임 중에는 아무도 못 들어오게 닫는다 → 방 목록에서 회색으로 보인다.
            _runner.SessionInfo.IsOpen = false;

            // 모든 참가자가 같은 맵을 만들도록 시드를 정해 방장 엔트리에 넣는다 (씬 로드보다 먼저 복제됨).
            NetworkPlayer.ClearLoadedPlayers();
            _gameOver = false;

            LobbyPlayerEntry hostEntry = LobbyPlayerEntry.Local;
            if (hostEntry != null) hostEntry.MapSeed = NetworkMapSeed.CreateSeed();

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

        /// <summary>인게임 ESC 메뉴: 게임에서 나가 방 목록으로 (확인 팝업 후).</summary>
        public void RequestLeaveFromGame()
        {
            if (_state != LauncherState.InGame) return;

            string message = IsHost && RoomPlayerCount > 1
                ? "방장이 나가면 게임이 끝나고\n모든 참가자가 방 목록으로 돌아갑니다.\n나가시겠습니까?"
                : "게임에서 나가 방 목록으로 돌아가시겠습니까?";
            SystemUI.Confirm("게임 나가기", message, LeaveSession, null, "나가기", "계속하기");
        }

        private async void LeaveSession()
        {
            if (_state != LauncherState.InRoom && _state != LauncherState.InGame) return;
            BeginOperation();
            SetState(LauncherState.Leaving);
            SystemUI.ShowLoading("방에서 나가는 중...");
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

            BeginOperation();
            SetState(LauncherState.Leaving);
            SystemUI.Alert("연결 끊김", message);
            await ReturnToLobbyAsync();
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
                    // 호스트가 접속을 거절하는 경우는 현재 비밀번호 불일치뿐이다.
                    return "비밀번호가 올바르지 않습니다.";
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
            return session != null && session.IsValid && session.IsOpen && !IsFull(session);
        }

        #endregion

        #region INetworkRunnerCallbacks

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            if (runner != _runner) return;
            _sessions = sessionList.Where(s => s != null && s.IsValid && s.IsVisible).ToList();
            SessionListUpdated?.Invoke(_sessions);
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            // 호스트만 불린다. 게임 시작 후에는 아무도 받지 않는다.
            // (방이 목록에 뜨는 시점과 StartGame 완료 시점 사이에 들어오는 사람을 위해 CreatingSession도 허용)
            if (_state != LauncherState.InRoom && _state != LauncherState.CreatingSession)
            {
                request.Refuse();
                return;
            }

            if (string.IsNullOrEmpty(_hostPassword))
            {
                request.Accept();
                return;
            }

            string given = token != null && token.Length > 0 ? Encoding.UTF8.GetString(token) : string.Empty;
            if (given == _hostPassword) request.Accept();
            else request.Refuse();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner != _runner || !runner.IsServer) return; // 스폰은 호스트만 한다.

            if (_state == LauncherState.StartingGame || _state == LauncherState.InGame)
            {
                // 세션을 닫아두므로 오면 안 되지만, 혹시 들어오면 내보낸다.
                if (player != runner.LocalPlayer) runner.Disconnect(player);
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

            if (_characterObjects.TryGetValue(player, out NetworkObject character))
            {
                if (character != null) runner.Despawn(character);
                _characterObjects.Remove(player);
            }
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
            if (runner != _runner) return;
            // 호스트가 시작을 누르면 모든 참가자(호스트 포함)에게 불린다 → 로딩 패널로 조작을 막는다.
            if (_state == LauncherState.InRoom) SetState(LauncherState.StartingGame);
            SystemUI.ShowLoading("게임을 불러오는 중...");
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (runner != _runner) return;
            if (SceneManager.GetActiveScene().name != gameSceneName) return;

            SetState(LauncherState.InGame);
            _gameOver = false;

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

            // 내 캐릭터 상태: 트럭에 타는 등으로 PlayerMove가 꺼져 있으면 이동 입력을 보내지 않는다.
            NetworkPlayer localPlayer = NetworkPlayer.Local;
            PlayerMove localMove = localPlayer != null ? localPlayer.GetComponent<PlayerMove>() : null;
            bool canMove = localMove == null || (localMove.isActiveAndEnabled && localPlayer.gameObject.activeInHierarchy);

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
                    data.TruckSteer = Input.GetAxis("Horizontal");
                }
            }

            var buttons = new NetworkButtons();
            buttons.Set(NetworkInputButton.TruckBrake, driving && Input.GetKey(KeyCode.Space));
            buttons.Set(NetworkInputButton.Walk, Input.GetButton("Walk"));
            buttons.Set(NetworkInputButton.Attack, Input.GetMouseButton(0));
            buttons.Set(NetworkInputButton.Interact, Input.GetKey(KeyCode.E));
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
                    NetworkPlayer networkPlayer = spawned.GetComponent<NetworkPlayer>();
                    if (networkPlayer != null) networkPlayer.InitializeBeforeSpawn(nickname, characterIndex);
                });
            if (character != null) _characterObjects[player] = character;
        }

        // 이번 단계에서 쓰지 않지만 인터페이스 구현을 위해 필요한 콜백들.
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
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
