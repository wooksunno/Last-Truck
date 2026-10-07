using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Ability_Police", menuName = "Character/Character Abilities/Police")]
    public class PlayerAbility_Police : CharacterAbility
    {
        [Header("경찰 능력 설정")]
        [Tooltip("피격 당한 적에게 표식이 유지되는 시간 (초)")]
        [SerializeField] private float markDuration = 2.0f;

        [Tooltip("표식 상태인 적 공격 시 추가 피해량")]
        [SerializeField] private int bonusDamage = 5;

        public override float MarkDuration => markDuration;
        public int BonusDamage => bonusDamage;

        public override void Execute(Character caster)
        {
        }

        public override int CalculateMarkBonusDamage(int baseDamage, bool targetHasMark)
        {
            if (targetHasMark)
            {
                return baseDamage + bonusDamage;
            }
            return baseDamage;
        }
    }
}