using Fusion;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// MonsterSpawner(팀 코드)가 멀티플레이에서 쓰는 창구.
    ///  - 몬스터 스폰은 호스트만 한다 (클라이언트에서는 CanSpawnMonsters = false).
    ///  - 네트워크 몬스터 프리팹(NetworkPrefabRegistry.monsterPrefab)을 Runner.Spawn으로 만들어서
    ///    모든 참가자에게 같은 몬스터가 보이게 한다.
    /// </summary>
    public static class NetworkMonsterSpawning
    {
        /// <summary>이 컴퓨터가 몬스터를 스폰해도 되는가 (싱글플레이 또는 멀티플레이 호스트).</summary>
        public static bool CanSpawnMonsters
        {
            get
            {
                if (!GameLauncher.IsOnlineSession) return true;
                return GameLauncher.Instance != null && GameLauncher.Instance.IsHost;
            }
        }

        /// <summary>멀티플레이 호스트에서 네트워크 몬스터로 스폰해야 하는가.</summary>
        public static bool UseNetworkSpawn => GameLauncher.IsOnlineSession;

        /// <summary>
        /// 멀티플레이 호스트: 네트워크 몬스터를 스폰한다. 실패하면 null.
        /// </summary>
        public static GameObject Spawn(Vector3 position, Quaternion rotation)
        {
            GameLauncher launcher = GameLauncher.Instance;
            NetworkRunner runner = launcher != null ? launcher.Runner : null;
            if (runner == null || !runner.IsRunning || !runner.IsServer) return null;

            NetworkPrefabRegistry registry = NetworkPrefabRegistry.Instance;
            if (registry == null || registry.monsterPrefab == null)
            {
                Debug.LogError("[NetworkMonsterSpawning] 네트워크 몬스터 프리팹이 없습니다. " +
                               "메뉴 'LastTruck > Multiplayer > 1. 네트워크 프리팹 생성'을 실행하세요.");
                return null;
            }

            NetworkObject spawned = runner.Spawn(registry.monsterPrefab, position, rotation);
            if (spawned == null) return null;

            // 킬 집계: 체력이 0이 되는 순간 호스트의 GameManager에 알린다
            // (오브젝트 파괴 시점으로 세면 게임 종료/씬 전환 때 남은 몬스터까지 킬로 잡힌다).
            NetworkHealth health = spawned.GetComponent<NetworkHealth>();
            if (health != null)
            {
                health.DiedOnHost += _ =>
                {
                    if (GameManager.Instance != null) GameManager.Instance.NotifyMonsterKilled();
                };
            }
            return spawned.gameObject;
        }

        /// <summary>멀티플레이에서 몬스터 스폰 기준점: 트럭 (없으면 내 캐릭터).</summary>
        public static Transform GetSpawnCenter()
        {
            LastTruck.TruckInformation truck = Object.FindFirstObjectByType<LastTruck.TruckInformation>();
            if (truck != null) return truck.transform;
            return NetworkPlayer.Local != null ? NetworkPlayer.Local.transform : null;
        }
    }
}
