using UnityEngine;

namespace LastTruck.Networking
{
    /// <summary>
    /// "내 캐릭터"의 컴포넌트를 찾는다.
    /// 싱글플레이 코드의 FindObjectOfType&lt;PlayerInventory&gt;() 같은 호출은 멀티플레이에서 다른 사람 캐릭터를
    /// 집어 올 수 있으므로, 멀티플레이에서는 내 캐릭터(NetworkPlayer.Local)에서만 찾는다.
    /// </summary>
    public static class LocalPlayerLookup
    {
        public static T Find<T>() where T : Component
        {
            if (GameLauncher.IsOnlineSession)
            {
                NetworkPlayer local = NetworkPlayer.Local;
                return local != null ? local.GetComponentInChildren<T>(true) : null;
            }
            return Object.FindFirstObjectByType<T>();
        }

        public static CharacterAbilityController AbilityController => Find<CharacterAbilityController>();

        public static CraftingSystem.PlayerInventory Inventory => Find<CraftingSystem.PlayerInventory>();
    }
}
