using CraftingSystem;
using Unity.Cinemachine;
using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// 내 캐릭터가 스폰되었을 때 게임 씬의 "플레이어 한 명을 가정한" 것들을 내 캐릭터에 연결한다.
    /// 싱글플레이에서는 씬에 미리 놓인 캐릭터에 인스펙터로 연결되어 있던 것들이다.
    ///
    ///  1. 카메라 (Cinemachine 카메라, CameraFollow)
    ///  2. HP 바 (PlayerHPUI)
    ///  3. 인벤토리/제작 UI, 채집 진행 UI, 미니맵, 클릭 상호작용, 무기, 파우치 (CraftingSceneBootstrap.SetupLocalPlayer)
    /// </summary>
    public static class LocalPlayerBinder
    {
        public static void BindLocalPlayer(NetworkPlayer player)
        {
            if (player == null) return;
            Transform target = player.transform;

            // 1. 카메라: 비어 있거나 (지워진) 싱글플레이용 캐릭터를 보던 카메라를 내 캐릭터로
            foreach (CinemachineCamera vcam in Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (IsReplaceablePlayerTarget(vcam.Target.TrackingTarget)) vcam.Target.TrackingTarget = target;
            }
            foreach (CameraFollow follow in Object.FindObjectsByType<CameraFollow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (IsReplaceablePlayerTarget(follow.target)) follow.target = target;
            }

            // 2. HP 바
            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                foreach (PlayerHPUI hp in Object.FindObjectsByType<PlayerHPUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    hp.BindPlayer(stats);
                }
            }

            // 3. 인벤토리/제작/미니맵 등
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                CraftingSceneBootstrap.SetupLocalPlayer(inventory);
            }
        }

        /// <summary>
        /// 바꿔도 되는 대상인가? 비어 있거나, 꺼져 있거나(지우는 중인 싱글플레이 캐릭터),
        /// 네트워크가 아닌 플레이어 캐릭터면 true. 트럭 등 다른 대상을 보고 있으면 건드리지 않는다.
        /// </summary>
        private static bool IsReplaceablePlayerTarget(Transform current)
        {
            if (current == null) return true;
            if (!current.gameObject.activeInHierarchy) return true;
            return current.GetComponentInParent<PlayerMove>() != null && current.GetComponentInParent<NetworkPlayer>() == null;
        }
    }
}
