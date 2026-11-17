using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Ability_Explorer", menuName = "Character/Character Abilities/Explorer")]
    public class PlayerAbility_Explorer : CharacterAbility
    {
        [Header("탐험가 능력 설정")]
        [SerializeField] private float resourceMultiplier = 1.2f;

        public override void Execute(Character caster)
        {
            Debug.Log($"{caster.name} 탐험가 능력 적용");
        }

        public override string AbilityTitle => "풍부한 수확";
        public override string GetDescription() => $"자원을 채집할 때 얻는 양이 {(resourceMultiplier - 1f) * 100f:0}% 늘어납니다.";

        public override int CalculateGatherAmount(int baseAmount)
        {
            if (baseAmount <= 0) return baseAmount;

            // 기록은 CharacterAbilityController → AbilityDebug
            return Mathf.CeilToInt(baseAmount * resourceMultiplier);
        }
    }
}