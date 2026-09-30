using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Character Data", menuName = "Character/CharacterStats Data")]
    public class CharacterStatsData : ScriptableObject
    {
        // 기본
        [System.Serializable]
        public struct BasicStats
        {
            public string playerName;
            public float maxHealth;
            public float attackPower;
            public float moveSpeed;
        }

        // 유틸리티
        [System.Serializable]
        public struct UtilityStats
        {
            public float costReduction;     // 제작비용 감소
            public float harvestSpeed;      // 채집 속도
            public float repairBonus;       // 수리 보너스 증가
        }

        [Header("Basic Info")]
        [SerializeField] private BasicStats basicStats;

        [Header("Utility Info")]
        [SerializeField] private UtilityStats utilityStats;

        [Header("Unique Ability")]
        [SerializeField] private CharacterAbility uniqueAbility;

        // 프로퍼티
        public string PlayerName => basicStats.playerName;
        public float MaxHealth => basicStats.maxHealth;
        public float AttackPower => basicStats.attackPower;
        public float MoveSpeed => basicStats.moveSpeed;
        public float ScaledMoveSpeed => basicStats.moveSpeed * 0.3f;

        public float CostReduction => utilityStats.costReduction;
        public float HarvestSpeed => utilityStats.harvestSpeed;
        public float RepairBonus => utilityStats.repairBonus;

        public CharacterAbility UniqueAbility => uniqueAbility;
    }
}
