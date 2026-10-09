using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 멀티플레이 맵 시드. 호스트가 "게임 시작"을 누를 때 정해서 방장 엔트리(LobbyPlayerEntry.MapSeed)에 넣으면
    /// 모든 참가자에게 복제되고, 각자 게임 씬의 MapGenerator가 같은 시드로 맵을 만든다
    /// (바닥 높이/구덩이/자원 위치가 모두 같아야 캐릭터 이동이 서로 어긋나지 않는다).
    /// </summary>
    public static class NetworkMapSeed
    {
        /// <summary>멀티플레이 중이고 시드가 정해져 있으면 true.</summary>
        public static bool TryGetSeed(out int seed)
        {
            seed = 0;
            if (!GameLauncher.IsOnlineSession) return false;

            LobbyPlayerEntry host = LobbyPlayerEntry.HostEntry;
            if (host == null || host.MapSeed == 0)
            {
                Debug.LogWarning("[NetworkMapSeed] 멀티플레이 중인데 맵 시드를 아직 받지 못했습니다. 맵이 참가자마다 다를 수 있습니다.");
                return false;
            }

            seed = host.MapSeed;
            return true;
        }

        /// <summary>0이 아닌 새 시드.</summary>
        public static int CreateSeed()
        {
            int seed = Random.Range(int.MinValue, int.MaxValue);
            return seed == 0 ? 1 : seed;
        }
    }
}
