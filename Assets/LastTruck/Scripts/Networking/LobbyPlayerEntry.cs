using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// "방 안에 누가 있는지"를 모든 참가자에게 보여주기 위한 가벼운 네트워크 데이터 오브젝트.
    /// 화면에 보이는 3D 오브젝트가 아니며, 실제 게임 캐릭터와도 별개다.
    ///
    /// - 호스트가 플레이어가 들어올 때마다 하나씩 스폰한다 (Input Authority = 그 플레이어).
    /// - 값(닉네임/방장 여부/준비 여부/고른 캐릭터)은 호스트(State Authority)만 바꿀 수 있고, 자동으로 모두에게 복제된다.
    /// - 각 클라이언트는 자기 값을 바꾸고 싶을 때 RPC로 호스트에게 "바꿔 달라"고 요청한다.
    /// - DontDestroyOnLoad 처리되어 게임 씬으로 넘어가도 유지된다 (인게임에서 방장 닉네임 등을 알기 위해).
    ///
    /// 프리팹(LobbyPlayerEntry.prefab)은 에디터 메뉴 "LastTruck > Multiplayer > Build Menu Scene"이 자동으로 만든다.
    /// </summary>
    public class LobbyPlayerEntry : NetworkBehaviour
    {
        private static readonly List<LobbyPlayerEntry> AllEntries = new List<LobbyPlayerEntry>();

        /// <summary>현재 이 컴퓨터에 스폰되어 있고 네트워크 상태를 읽을 수 있는 엔트리들.</summary>
        public static IReadOnlyList<LobbyPlayerEntry> All
        {
            get
            {
                AllEntries.RemoveAll(e => !e.IsAlive);
                return AllEntries;
            }
        }

        private bool _alive;

        /// <summary>Spawned ~ Despawned 사이이고 네트워크 오브젝트가 유효할 때만 true. false면 [Networked] 값을 읽으면 안 된다.</summary>
        public bool IsAlive => _alive && this != null && Object != null && Object.IsValid;

        /// <summary>엔트리가 생기거나 사라지거나 값이 바뀔 때마다 발생 (UI 갱신용).</summary>
        public static event Action Changed;

        [Networked, OnChangedRender(nameof(OnNetworkDataChanged))]
        public NetworkString<_32> Nickname { get; set; }

        [Networked, OnChangedRender(nameof(OnNetworkDataChanged))]
        public NetworkBool IsHost { get; set; }

        [Networked, OnChangedRender(nameof(OnNetworkDataChanged))]
        public NetworkBool IsReady { get; set; }

        /// <summary>고른 캐릭터 번호 (CharacterCatalog 순서). 게임 씬에서 이 캐릭터로 스폰된다.</summary>
        [Networked, OnChangedRender(nameof(OnNetworkDataChanged))]
        public int CharacterIndex { get; set; }

        /// <summary>방장 엔트리에만 값이 있다: 게임 맵 시드 (호스트가 게임 시작 시 정함, NetworkMapSeed 참고).</summary>
        [Networked]
        public int MapSeed { get; set; }

        /// <summary>이 엔트리의 주인(플레이어).</summary>
        public PlayerRef Owner => Object != null ? Object.InputAuthority : PlayerRef.None;

        /// <summary>내 엔트리인가?</summary>
        public bool IsLocal => Object != null && Object.HasInputAuthority;

        /// <summary>닉네임 RPC가 아직 도착하지 않았으면 빈 문자열이다.</summary>
        public string DisplayName
        {
            get
            {
                string name = Nickname.ToString();
                return string.IsNullOrEmpty(name) ? "접속 중..." : name;
            }
        }

        public static LobbyPlayerEntry Local
        {
            get
            {
                foreach (LobbyPlayerEntry entry in All)
                {
                    if (entry.IsLocal) return entry;
                }
                return null;
            }
        }

        public static LobbyPlayerEntry HostEntry
        {
            get
            {
                foreach (LobbyPlayerEntry entry in All)
                {
                    if (entry.IsHost) return entry;
                }
                return null;
            }
        }

        public override void Spawned()
        {
            _alive = true;
            // Menu -> 게임 씬으로 넘어갈 때 Menu 씬과 함께 파괴되지 않도록 한다.
            Runner.MakeDontDestroyOnLoad(gameObject);

            if (HasStateAuthority)
            {
                // Host 모드에서는 호스트 자신의 플레이어 = Runner.LocalPlayer.
                IsHost = Object.InputAuthority == Runner.LocalPlayer;
                IsReady = false;
            }

            if (Object.HasInputAuthority)
            {
                RPC_SetNickname(PlayerProfile.Nickname);
                RPC_SetCharacter(PlayerProfile.CharacterIndex);
            }

            if (!AllEntries.Contains(this)) AllEntries.Add(this);
            RaiseChanged();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _alive = false;
            AllEntries.Remove(this);
            RaiseChanged();
        }

        private void OnDestroy()
        {
            // 러너가 강제로 종료되면서 Despawned 없이 파괴되는 경우 대비.
            _alive = false;
            if (AllEntries.Remove(this)) RaiseChanged();
        }

        private void OnNetworkDataChanged()
        {
            RaiseChanged();
        }

        private static void RaiseChanged()
        {
            try
            {
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // ---- 클라이언트 -> 호스트 요청 ----

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetNickname(string nickname)
        {
            Nickname = PlayerProfile.ClampForNetwork(nickname);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetReady(NetworkBool ready)
        {
            if (IsHost) return; // 방장은 준비 버튼이 없다.
            IsReady = ready;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetCharacter(int characterIndex)
        {
            // 게임 시작 버튼을 누른 뒤에는 바꿀 수 없다 (이미 그 캐릭터로 스폰 준비 중).
            GameLauncher launcher = GameLauncher.Instance;
            if (launcher != null && (launcher.State == LauncherState.StartingGame || launcher.State == LauncherState.InGame))
            {
                return;
            }
            CharacterIndex = CharacterCatalog.SafeClamp(characterIndex);
        }

        /// <summary>이 PlayerRef의 엔트리 (없으면 null).</summary>
        public static LobbyPlayerEntry Find(PlayerRef player)
        {
            foreach (LobbyPlayerEntry entry in All)
            {
                if (entry.Owner == player) return entry;
            }
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AllEntries.Clear();
            Changed = null;
        }
    }
}
