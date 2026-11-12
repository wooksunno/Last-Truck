using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastTruck.Networking
{
    /// <summary>
    /// GameLauncher 중 "연결이 끊겼을 때"를 다루는 부분: 재접속, 게임 중 참가 막기, 방장 변경(호스트 이전).
    ///
    /// 재접속 (끊긴 사람)
    ///  - 게임 중에 나가거나 끊긴 사람의 캐릭터는 바로 없애지 않고 숨긴 채 ReconnectWindowSeconds(3분) 동안 기다린다.
    ///    가방(플레이어 칸 + 파우치)은 평소에 호스트에 보관해 두므로 그대로 남는다.
    ///  - 같은 기기(SessionTicket.DeviceId)로 다시 들어오면 원래 캐릭터/체력/위치/가방을 돌려준다.
    ///  - 3분이 지나면 가방 아이템은 트럭으로 옮기고 캐릭터를 없앤다.
    ///  - 새 사람은 게임 도중에 들어올 수 없다 (방 목록에 "게임 중", 호스트가 접속을 거절).
    ///
    /// 방장 변경 (호스트 이전, Photon Fusion Host Migration)
    ///  - 방장이 나가면 남은 사람 중 한 명이 새 방장이 된다. 그동안 게임 전체가 멈춘다(NetworkPause).
    ///  - 새 방장은 이전 방장이 몇 초마다 올려 둔 스냅샷으로 네트워크 오브젝트(캐릭터/몬스터/게임 상태/트럭)를 되살리고,
    ///    다른 사람들은 기기 ID로 자기 캐릭터를 다시 찾는다. 씬(맵/트럭)은 다시 불러오지 않고 그대로 쓴다.
    ///  - 트럭에 타 있던 사람은 모두 트럭 옆에 내려진다(좌석 번호가 새로 매겨지므로). 나간 방장의 캐릭터는 3분간 재접속 대기.
    ///  - 실패하면 결과 안내 후 방 목록으로 돌아간다.
    /// </summary>
    public partial class GameLauncher
    {
        #region 설정

        /// <summary>나간 사람의 캐릭터를 남겨 두고 재접속을 기다리는 시간(초).</summary>
        public const float ReconnectWindowSeconds = 180f;

        /// <summary>방장 변경 직후, 각 캐릭터의 주인이 새 방에 다시 붙기를 기다리는 시간(초). 넘으면 "나감"으로 본다.</summary>
        public const float ClaimWaitSeconds = 10f;

        /// <summary>방장 변경 뒤 내 캐릭터를 다시 받을 때까지 멈춰서 기다리는 최대 시간(초).</summary>
        private const float MigrationLocalWaitSeconds = 15f;
        /// <summary>게임 씬 불러오기 최대 시간(초). 넘으면 방 목록으로.</summary>
        private const float SceneLoadTimeoutSeconds = 60f;

        #endregion

        #region 상태

        private bool _migrating;        // 방장 변경 진행 중 (러너를 다시 만드는 중)
        private bool _resumeDone = true; // 새 방장: 스냅샷 복원이 끝났는가
        private bool _reRegisteringScene; // 새 방장: 이미 떠 있는 게임 씬을 네트워크 씬으로 다시 등록하는 중
        private string _pendingAnnouncement; // 새 방장: 멈춤이 풀리면 모두에게 보낼 알림

        /// <summary>미뤄 둔 알림을 꺼낸다 (한 번만).</summary>
        public string TakePendingAnnouncement()
        {
            string message = _pendingAnnouncement;
            _pendingAnnouncement = null;
            return message;
        }

        private string _lastSessionName;
        private string _lastSessionTitle;
        private bool _lastSessionPrivate;
        private string _lastSessionPassword;
        private float _reconnectDeadline; // Time.realtimeSinceStartup 기준

        /// <summary>방장 변경 진행 중인가 (UI/디버그용).</summary>
        public bool IsMigrating => _migrating;

        #endregion

        #region 재접속 정보

        private void RememberSession(string sessionName, string title, bool isPrivate, string password)
        {
            _lastSessionName = sessionName;
            _lastSessionTitle = title;
            _lastSessionPrivate = isPrivate;
            _lastSessionPassword = password;
            _reconnectDeadline = 0f; // 접속해 있는 동안은 재접속 대상이 아니다
        }

        /// <summary>게임 도중 나가거나 끊겼다: 지금부터 ReconnectWindowSeconds 동안 같은 방에 다시 들어갈 수 있다.</summary>
        private void MarkLeftGame(bool reconnectable)
        {
            _reconnectDeadline = reconnectable && !string.IsNullOrEmpty(_lastSessionName)
                ? Time.realtimeSinceStartup + ReconnectWindowSeconds
                : 0f;
        }

        /// <summary>최근에 있던 방으로 재접속할 수 있는 시간 안인가.</summary>
        private bool CanReconnect => !string.IsNullOrEmpty(_lastSessionName) && Time.realtimeSinceStartup < _reconnectDeadline;

        /// <summary>방 목록의 이 방이 "내가 끊겼던 게임"이라 다시 들어갈 수 있는가.</summary>
        public static bool CanRejoin(SessionInfo session)
        {
            GameLauncher launcher = Instance;
            return launcher != null && session != null && launcher.CanReconnect && session.Name == launcher._lastSessionName;
        }

        /// <summary>방 목록에서 "재접속 가능" 방을 눌렀을 때.</summary>
        public void RejoinLastSession() => TryReconnect();

        /// <summary>연결 끊김 팝업의 "다시 접속" 버튼.</summary>
        private void TryReconnect()
        {
            if (!CanReconnect)
            {
                SystemUI.Alert("다시 접속", "재접속 가능 시간이 지났습니다.");
                return;
            }
            if (_state != LauncherState.InLobby)
            {
                SystemUI.Alert("다시 접속", "서버에 접속한 뒤 다시 시도해 주세요.");
                return;
            }
            LogSession($"재접속 시도: {_lastSessionTitle}");
            JoinSessionByName(_lastSessionName, _lastSessionTitle, _lastSessionPrivate, _lastSessionPassword);
        }

        #endregion

        #region 호스트: 돌아온 사람 받기

        /// <summary>이 참가자의 기기 ID (호스트 자신이면 내 기기 ID, 아니면 접속 토큰에서 읽는다).</summary>
        private static string DeviceIdOf(NetworkRunner runner, PlayerRef player)
        {
            if (runner == null) return string.Empty;
            if (player == runner.LocalPlayer) return SessionTicket.DeviceId;
            byte[] token = null;
            try { token = runner.GetPlayerConnectionToken(player); }
            catch (Exception e) { Debug.LogException(e); }
            return SessionTicket.Parse(token).DeviceId ?? string.Empty;
        }

        /// <summary>호스트: 이 기기의 캐릭터가 재접속(또는 방장 변경 뒤 주인)을 기다리고 있는가.</summary>
        private bool CanAcceptReturning(string deviceId)
        {
            // 새 방장이 스냅샷을 복원하는 중이면 일단 받고, 붙을 때(OnPlayerJoined) 확인한다.
            if (_migrating && !_resumeDone) return true;
            return FindReturningCharacter(deviceId) != null;
        }

        private static NetworkPlayer FindUnclaimedByNickname(string nickname)
        {
            if (string.IsNullOrEmpty(nickname)) return null;
            foreach (NetworkPlayer player in NetworkPlayer.AllIncludingDeparted)
            {
                if (player == null || player.Object == null || !player.Object.IsValid) continue;
                if (player.IsUnclaimed && player.Nickname.ToString() == nickname) return player;
            }
            return null;
        }

        private static string ShortId(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return "-";
            return deviceId.Length <= 6 ? deviceId : deviceId.Substring(0, 4) + ".." + deviceId.Substring(deviceId.Length - 2);
        }

        private static NetworkPlayer FindReturningCharacter(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return null;
            foreach (NetworkPlayer player in NetworkPlayer.AllIncludingDeparted)
            {
                if (player == null || player.Object == null || !player.Object.IsValid) continue;
                if (!(player.IsAwaitingReturn || player.IsUnclaimed)) continue;
                if (player.DeviceId.ToString() == deviceId) return player;
            }
            return null;
        }

        /// <summary>
        /// 호스트: 게임 중에 들어온 사람에게 원래 캐릭터를 돌려준다. 돌려줄 캐릭터가 있으면 true.
        /// (새 방장이 아직 스냅샷 복원 전이면 복원이 끝날 때 다시 처리한다 → true)
        /// </summary>
        private bool HostAttachReturningPlayer(NetworkRunner runner, PlayerRef player)
        {
            if (_migrating && !_resumeDone) return true;

            // 이미 캐릭터가 붙어 있으면 또 붙이지 않는다 (한 사람에게 캐릭터 2개 → 같이 움직이던 문제).
            if (_characterObjects.TryGetValue(player, out NetworkObject owned) && owned != null && owned.IsValid) return true;
            foreach (NetworkPlayer existing in NetworkPlayer.AllIncludingDeparted)
            {
                if (existing != null && existing.Object != null && existing.Object.IsValid && existing.Object.InputAuthority == player)
                {
                    _characterObjects[player] = existing.Object;
                    return true;
                }
            }

            string deviceId = DeviceIdOf(runner, player);
            NetworkPlayer character = FindReturningCharacter(deviceId);
            // 새 방장 자신: 기기 ID로 못 찾으면 닉네임으로 한 번 더 찾는다.
            if (character == null && player == runner.LocalPlayer) character = FindUnclaimedByNickname(PlayerProfile.ClampForNetwork(PlayerProfile.Nickname));
            if (character == null)
            {
                LogSession($"돌려줄 캐릭터 없음 ({player}, 기기 {ShortId(deviceId)})");
                return false;
            }

            bool wasAway = character.IsAwaitingReturn; // 재접속 (방장 변경 직후 되찾기는 알리지 않는다)
            character.HostReattach(player);
            _characterObjects[player] = character.Object;
            if (wasAway) NetworkGameState.HostSystemMessage($"{character.Nickname}님이 다시 들어왔습니다.");
            LogSession($"{character.Nickname} 다시 연결 ({player})");
            return true;
        }

        #endregion

        #region 방장 변경 (호스트 이전)

        public async void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
            if (runner != _runner) return;
            LogSession($"방장 변경 시작 (내 새 역할: {hostMigrationToken.GameMode})");

            // 게임 중이 아니거나(대기실) 이미 게임 오버면 이어갈 필요 없이 끊긴 것으로 처리한다.
            if (_state != LauncherState.InGame || _gameOver || NetworkGameState.IsGameOver)
            {
                _runner = null;
                runner.RemoveCallbacks(this);
                try { await runner.Shutdown(true, ShutdownReason.HostMigration); }
                catch (Exception e) { Debug.LogException(e); }
                if (runner != null && runner.gameObject != null) Destroy(runner.gameObject);
                HandleUnexpectedDisconnect("방장 나감 (게임 중 아님)");
                return;
            }

            int op = BeginOperation();
            _migrating = true;
            _resumeDone = hostMigrationToken.GameMode != GameMode.Host;
            SystemUI.Dismiss(LeaveGamePopupTag);
            ChatHud.AddLocal(string.Empty, "방장이 나갔습니다. 새 방장에게 연결하는 중...", ChatKind.Warning);
            NetworkPause.Begin(NetworkPause.MigrationReason, "방장이 나갔습니다.\n다른 플레이어가 방장이 되어 게임을 이어갑니다. 잠시만 기다려 주세요.");

            // 1. 내 가방은 내 컴퓨터의 최신 상태를 붙잡아 둔다 (새 캐릭터에 그대로 넣는다).
            NetworkPlayer local = NetworkPlayer.Local;
            NetworkPlayer.PendingLocalBag = local != null ? local.EncodeLocalBag() : null;

            // 2. 화면의 네트워크 오브젝트는 멈춘 모습 그대로 남겨 두었다가(파괴 X) 새 오브젝트가 생기면 지운다.
            //    그동안 내 옛 캐릭터의 입력 스크립트는 꺼서 아이템을 버리거나 하지 못하게 한다.
            var leftovers = new List<GameObject>();
            foreach (NetworkObject no in FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
            {
                if (no != null && no.gameObject.scene.IsValid()) leftovers.Add(no.gameObject);
            }
            if (local != null)
            {
                foreach (MonoBehaviour behaviour in local.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null && !(behaviour is NetworkBehaviour)) behaviour.enabled = false;
                }
            }

            _runner = null; // 옛 러너의 OnShutdown이 "예상치 못한 끊김"으로 처리되지 않게
            runner.RemoveCallbacks(this);
            try { await runner.Shutdown(false, ShutdownReason.HostMigration); }
            catch (Exception e) { Debug.LogException(e); }
            if (runner != null && runner.gameObject != null) Destroy(runner.gameObject);
            if (op != _operationId || !CanContinue()) return;

            // 3. 새 러너로 다시 시작 (새 방장이면 HostMigrationResume에서 이전 상태를 되살린다)
            _entryObjects.Clear();
            _characterObjects.Clear();
            bool becomeHost = hostMigrationToken.GameMode == GameMode.Host;
            if (becomeHost) _hostPassword = _lastSessionPrivate ? _lastSessionPassword : null;

            _runner = CreateRunner();
            var args = new StartGameArgs
            {
                HostMigrationToken = hostMigrationToken,
                HostMigrationResume = HostMigrationResume,
                ConnectionToken = SessionTicket.Build(_lastSessionPassword),
                SceneManager = _runner.GetComponent<NetworkSceneManagerDefault>(),
            };

            OperationResult result = await AwaitWithTimeout(_runner.StartGame(args));
            if (op != _operationId) return;

            if (!result.Ok || !IsRunnerAlive())
            {
                LogSession($"방장 변경 실패: {result.Message}");
                DestroyLeftovers(leftovers);
                NetworkPlayer.PendingLocalBag = null;
                BeginOperation();
                SetState(LauncherState.Leaving);
                await ReturnToLobbyAsync();
                SystemUI.Alert("게임 종료", "방장이 나가서 게임을 이어갈 수 없습니다.\n" + (result.Ok ? "새 방장에게 연결하지 못했습니다." : result.Message));
                return;
            }

            _wasHostInSession = _runner.IsServer;
            _resumeDone = true; // (스냅샷이 없어 복원 콜백이 안 불린 경우 대비)

            // 새 방장: 복원 콜백 때는 아직 내 플레이어가 방에 없어서 못 붙였을 수 있다 → 지금 내 캐릭터를 붙인다.
            if (_runner.IsServer)
            {
                if (_runner.LocalPlayer != PlayerRef.None) HostAttachReturningPlayer(_runner, _runner.LocalPlayer);
                // 아직 주인이 안 온 캐릭터는 되찾기 대기 시간을 건다 (안 오면 자리 비움 → 3분 재접속 대기).
                foreach (NetworkPlayer character in NetworkPlayer.AllIncludingDeparted)
                {
                    if (character != null && character.Object != null && character.Object.IsValid) character.HostEnsureClaimWait(ClaimWaitSeconds);
                }
                EnsureGameSceneRegistered(_runner);
            }
            if (_state != LauncherState.InGame) SetState(LauncherState.InGame);
            LogSession(becomeHost ? "내가 새 방장이 되었습니다." : "새 방장에게 다시 연결했습니다.");
            // 모두 다시 연결되어 멈춤이 풀릴 때 NetworkGameState가 모두에게 보낸다.
            if (becomeHost) _pendingAnnouncement = $"방장이 나갔습니다. {PlayerProfile.Nickname}님이 새 방장이 되었습니다.";

            // 4. 내 캐릭터를 다시 받을 때까지(최대 MigrationLocalWaitSeconds) 멈춘 채 기다린 뒤 이어서 진행한다.
            float deadline = Time.realtimeSinceStartup + MigrationLocalWaitSeconds;
            while (op == _operationId && Time.realtimeSinceStartup < deadline && !HasNewLocalCharacter())
            {
                await Task.Yield();
            }
            if (op != _operationId) return;

            DestroyLeftovers(leftovers);
            _migrating = false;
            NetworkPause.End(NetworkPause.MigrationReason);
            if (!HasNewLocalCharacter())
            {
                LogSession("방장 변경 후 내 캐릭터를 찾지 못했습니다.");
                SystemUI.Alert("방장 변경", "새 방장에게 연결됐지만 내 캐릭터를 찾지 못했습니다.");
            }
        }

        /// <summary>
        /// 새 방장: 방장이 바뀐 뒤에는 네트워크 씬 목록이 비어 있을 수 있다.
        /// 그러면 나중에 재접속하는 사람이 게임 씬을 불러오지 않아 메뉴(대기실) 화면에 남는다
        /// → 이미 떠 있는 게임 씬을 다시 불러오지 않고 그대로 "이어받기"(Additive + 씬 이어받기)로 목록에 올린다.
        /// (Single로 부르면 Fusion이 씬의 런타임 오브젝트(캐릭터/몬스터)를 지우므로 Additive를 쓴다)
        /// </summary>
        private void EnsureGameSceneRegistered(NetworkRunner runner)
        {
            int index = FindBuildIndex(gameSceneName);
            if (runner == null || !runner.IsServer || index < 0) return;

            SceneRef sceneRef = SceneRef.FromIndex(index);
            int count = 0;
            bool listed = false;
            if (runner.TryGetSceneInfo(out NetworkSceneInfo info))
            {
                count = info.SceneCount;
                for (int i = 0; i < count; i++)
                {
                    if (info.Scenes[i] == sceneRef) listed = true;
                }
            }
            LogSession($"새 방장: 네트워크 씬 {count}개{(listed ? string.Empty : " → 게임 씬 다시 등록")}");
            if (listed) return;

            try
            {
                _reRegisteringScene = true;
                runner.LoadScene(sceneRef, LoadSceneMode.Additive);
            }
            catch (Exception e)
            {
                _reRegisteringScene = false;
                Debug.LogException(e);
            }
        }

        private bool HasNewLocalCharacter()
        {
            NetworkPlayer local = NetworkPlayer.Local;
            return local != null && local.Object != null && local.Object.IsValid && local.Runner == _runner;
        }

        private static void DestroyLeftovers(List<GameObject> leftovers)
        {
            foreach (GameObject go in leftovers)
            {
                if (go == null) continue;
                NetworkObject no = go.GetComponent<NetworkObject>();
                // 새 러너가 쓰고 있는 오브젝트는 건드리지 않는다
                if (no != null && no.Runner != null && no.IsValid) continue;
                Destroy(go);
            }
            leftovers.Clear();
        }

        /// <summary>
        /// 새 방장: 이전 방장의 스냅샷으로 네트워크 오브젝트를 다시 만든다 (StartGame 도중 Fusion이 부른다).
        /// 대기실 정보(LobbyPlayerEntry)는 게임 중에 필요 없어서 되살리지 않는다.
        /// </summary>
        private void HostMigrationResume(NetworkRunner runner)
        {
            LogSession("새 방장: 이전 게임 상태 복원 시작");

            var oldRefById = new Dictionary<NetworkId, PlayerRef>();
            try
            {
                foreach (var pair in runner.GetResumeSnapshotNetworkObjectPlayerObjects()) oldRefById[pair.Value] = pair.Key;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            var oldOwners = new Dictionary<PlayerRef, NetworkPlayer>();
            int restored = 0;
            foreach (NetworkObject resumeNO in runner.GetResumeSnapshotNetworkObjects())
            {
                if (resumeNO == null) continue;
                if (resumeNO.GetComponent<LobbyPlayerEntry>() != null) continue;
                // 씬에 원래 있던 오브젝트(트럭 등)는 씬 이어받기로 자동 복원되므로 다시 만들지 않는다.
                if (resumeNO.NetworkTypeId.IsSceneObject) continue;

                Vector3 position = resumeNO.transform.position;
                Quaternion rotation = resumeNO.transform.rotation;
                if (resumeNO.TryGetBehaviour<NetworkTRSP>(out NetworkTRSP trsp))
                {
                    position = trsp.Data.Position;
                    rotation = trsp.Data.Rotation;
                }

                NetworkObject spawned = null;
                try
                {
                    spawned = runner.Spawn(resumeNO, position, rotation, PlayerRef.None,
                        (r, newNO) =>
                        {
                            newNO.CopyStateFrom(resumeNO);
                            NetworkInterest.Apply(r, newNO, newNO.GetComponent<NetworkMonster>() != null);
                        });
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                if (spawned == null) continue;
                restored++;

                NetworkPlayer character = spawned.GetComponent<NetworkPlayer>();
                if (character != null)
                {
                    if (oldRefById.TryGetValue(resumeNO.Id, out PlayerRef oldRef)) oldOwners[oldRef] = character;
                    if (!character.Departed) character.HostBeginClaimWait(ClaimWaitSeconds);
                }
            }

            // 좌석 번호(PlayerRef)가 새로 매겨지므로 트럭에 타 있던 사람은 모두 내린다.
            if (NetworkTruck.IsReady) NetworkTruck.Instance.HostEjectAllAfterMigration(oldOwners);

            _resumeDone = true;
            LogSession($"새 방장: 오브젝트 {restored}개 복원");

            // 복원보다 먼저 들어와 있던 사람(나 자신 포함)에게 캐릭터를 돌려준다.
            foreach (PlayerRef player in runner.ActivePlayers)
            {
                if (_characterObjects.ContainsKey(player)) continue;
                if (!HostAttachReturningPlayer(runner, player) && player != runner.LocalPlayer)
                {
                    LogSession($"방장 변경 뒤 캐릭터가 없는 참가자를 내보냄 ({player})");
                    runner.Disconnect(player);
                }
            }
        }

        #endregion

        #region 접속 기록 (F3 네트워크 정보 화면에 표시)

        private static readonly List<string> RecentLogLines = new List<string>();
        public static IReadOnlyList<string> RecentLog => RecentLogLines;

        public static void LogSession(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Debug.Log("[Session] " + line);
            RecentLogLines.Add(line);
            if (RecentLogLines.Count > 12) RecentLogLines.RemoveAt(0);
        }

        #endregion
    }
}
