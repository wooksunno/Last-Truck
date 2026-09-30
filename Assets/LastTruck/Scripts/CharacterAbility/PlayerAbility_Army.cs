using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Ability_Army", menuName = "Character/Character Abilities/Army")]
    public class PlayerAbility_Army : CharacterAbility
    {
        [Header("군인 능력 설정")]
        [SerializeField] private int requiredAttackCount = 5;
        [SerializeField] private float enhancedDamageMultiplier = 1.2f;

        private int _currentAttackCount = 0;

        public override void Execute(Character caster)
        {
            Debug.Log($"{caster.name} 군인 능력 적용");
            _currentAttackCount = 0;
        }

        public override int CalculateAttackDamage(int baseDamage, out bool isEnhanced)
        {
            _currentAttackCount++;

            if (_currentAttackCount >= requiredAttackCount)
            {
                _currentAttackCount = 0;
                isEnhanced = true;
                return Mathf.RoundToInt(baseDamage * enhancedDamageMultiplier);
            }

            isEnhanced = false;
            return baseDamage;
        }
    }
}