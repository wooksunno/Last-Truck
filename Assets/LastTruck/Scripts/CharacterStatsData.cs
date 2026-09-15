using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Character Data", menuName = "LastTruck/CharacterStats Data")]
    public class CharacterStatsData : ScriptableObject
    {
        // 기본
        public string playerName;
        public float maxHealth;
        public float attackPower;
        public float moveSpeed;

        // 유틸리티
        public float costReducation;    // 제작비용 감소
        public float harvestSpeed;      // 채집 속도
        public float repairBonus;       // 수리 보너스 증가
    }
}
