using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using Fusion;
using Fusion.Sockets;

using TMPro;


public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Session Settings")]
    [SerializeField] private string sessionName = "LastTruck_Test";
    [SerializeField] private int maxPlayers = 4;

    [Header("UI")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private TextMeshProUGUI statusText;

    private NetworkRunner runner;
    private int playerCount = 0;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        if (hostButton != null)
            hostButton.onClick.AddListener(OnClickHost);

        if (joinButton != null)
            joinButton.onClick.AddListener(OnClickJoin);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnClickLeave);

        UpdateStatusText();
    }


    // =========================================================
    // Button Event Handlers
    // =========================================================

    public void OnClickHost()
    {
        StartHost();
    }

    public void OnClickJoin()
    {
        JoinGame();
    }

    public void OnClickLeave()
    {
        LeaveGame();
    }


    // =========================================================
    // Host
    // =========================================================

    private async void StartHost()
    {
        if (runner != null)
        {
            Debug.LogWarning("이미 NetworkRunner가 존재합니다.");
            return;
        }

        Debug.Log("[Host] Session 생성 시도");

        CreateRunner();

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = sessionName,
            PlayerCount = maxPlayers,
            Scene = SceneRef.FromIndex(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex)
        });

        if (result.Ok)
        {
            Debug.Log($"[Host] Session 생성 성공! Session = {sessionName}");
        }
        else
        {
            Debug.LogError($"[Host] Session 생성 실패: {result.ShutdownReason}");
            DestroyRunner();
        }

        UpdateStatusText();
    }


    // =========================================================
    // Client
    // =========================================================

    private async void JoinGame()
    {
        if (runner != null)
        {
            Debug.LogWarning("이미 NetworkRunner가 존재합니다.");
            return;
        }

        Debug.Log($"[Client] Session 참가 시도: {sessionName}");

        CreateRunner();

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = sessionName,
            Scene = SceneRef.FromIndex(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex)
        });

        if (result.Ok)
        {
            Debug.Log($"[Client] Session 참가 성공! Session = {sessionName}");
        }
        else
        {
            Debug.LogError($"[Client] Session 참가 실패: {result.ShutdownReason}");
            DestroyRunner();
        }

        UpdateStatusText();
    }


    // =========================================================
    // Leave
    // =========================================================

    private async void LeaveGame()
    {
        if (runner == null)
            return;

        Debug.Log("[Network] Session에서 나가는 중...");

        await runner.Shutdown();

        DestroyRunner();

        Debug.Log("[Network] Session에서 나갔습니다.");

        UpdateStatusText();
    }


    // =========================================================
    // Runner 생성 / 제거
    // =========================================================

    private void CreateRunner()
    {
        GameObject runnerObject = new GameObject("NetworkRunner");

        runner = runnerObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);
    }

    private void DestroyRunner()
    {
        if (runner != null)
        {
            NetworkRunner oldRunner = runner;
            runner = null;
            Destroy(oldRunner.gameObject);
        }

        playerCount = 0;
    }


    // =========================================================
    // UI 갱신
    // =========================================================

    private void UpdateStatusText()
    {
        if (statusText == null)
            return;

        statusText.text = runner == null
            ? "대기 중"
            : $"Session: {sessionName}  |  Players: {playerCount}/{maxPlayers}";
    }


    // =========================================================
    // Player Joined / Left
    // =========================================================

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        playerCount = GetCurrentPlayerCount(runner);
        Debug.Log($"[Player Joined] PlayerId = {player.PlayerId} | 현재 플레이어 수 = {playerCount}/{maxPlayers}");
        UpdateStatusText();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        playerCount = GetCurrentPlayerCount(runner);
        Debug.Log($"[Player Left] PlayerId = {player.PlayerId} | 현재 플레이어 수 = {playerCount}/{maxPlayers}");
        UpdateStatusText();
    }

    private int GetCurrentPlayerCount(NetworkRunner runner)
    {
        int count = 0;
        foreach (PlayerRef player in runner.ActivePlayers)
            count++;
        return count;
    }


    // =========================================================
    // Connection Callbacks
    // =========================================================

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[Network] Server / Host 연결 성공");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"[Network] Server 연결 종료: {reason}");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"[Network] 연결 실패: {reason}");
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[Network] Runner Shutdown: {shutdownReason}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        // 현재는 별도 인증 없이 모든 접속 허용
        request.Accept();
    }


    // =========================================================
    // 사용하지 않지만 인터페이스 구현상 필요한 콜백
    // (내용 없이 비워둠)
    // =========================================================

    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}