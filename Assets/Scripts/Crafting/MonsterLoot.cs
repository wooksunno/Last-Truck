using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// 몬스터가 죽으면 그 자리에 고기와 젖은 가죽을 떨어뜨린다 (E키로 줍는 DroppedItem).
    /// 일정 확률로 고기 대신 썩은 고기가 나온다 (트럭에서 약초와 합치면 고기가 된다).
    ///  - 멀티플레이: 죽음은 모든 컴퓨터에서 같이 처리되고(NetworkHealth → Damageable.OnNetworkDeath),
    ///    전리품은 각자 화면에 따로 떨어진다. 그래서 같이 잡은 사람 모두가 자기 몫을 주울 수 있다.
    ///  - 체력이 많은(큰) 몬스터일수록 더 많이 나온다.
    /// </summary>
    public static class MonsterLoot
    {
        // 이 체력 이상이면 큰 몬스터로 쳐서 전리품이 늘어난다
        private const int BigMonsterHealth = 200;

        private const int MeatMin = 1;
        private const int MeatMax = 2;
        private const float HideChance = 0.6f;
        // 고기 대신 썩은 고기가 나올 확률
        private const float RottenChance = 0.35f;

        private static readonly HashSet<int> Dropped = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Dropped.Clear();

        /// <summary>monster가 몬스터면 전리품을 떨어뜨린다 (같은 몬스터는 한 번만).</summary>
        public static void Drop(GameObject monster, int maxHealth)
        {
            if (monster == null || !IsMonster(monster))
                return;
            if (!Dropped.Add(monster.GetInstanceID()))
                return;

            ItemCatalog catalog = ItemCatalog.GetOrCreate();
            ItemData meat = catalog.GetItem(ItemIds.Meat);
            ItemData rotten = catalog.GetItem(ItemIds.RottenMeat);
            if (rotten != null && Random.value < RottenChance)
                meat = rotten;
            ItemData hide = catalog.GetItem(ItemIds.WetHide);

            int bonus = maxHealth >= BigMonsterHealth ? 1 : 0;
            int meatCount = Random.Range(MeatMin, MeatMax + 1) + bonus;
            int hideCount = (Random.value < HideChance ? 1 : 0) + bonus;

            Vector3 at = monster.transform.position;
            Vector3 side = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 0.6f;
            if (meat != null && meatCount > 0)
                DroppedItem.SpawnAt(at + side, meat, meatCount);
            if (hide != null && hideCount > 0)
                DroppedItem.SpawnAt(at - side, hide, hideCount);
        }

        private static bool IsMonster(GameObject go)
        {
            return go.GetComponent<MonsterAI>() != null
                || go.GetComponent<LastTruck.Networking.NetworkMonster>() != null;
        }
    }
}
