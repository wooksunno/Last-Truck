using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Ability_Mechanic", menuName = "Character/Character Abilities/Mechanic")]
    public class PlayerAbility_Mechanic : CharacterAbility
    {
        [Header("수리공 능력 설정 - 일반 수리")]
        [Range(0f, 0.9f)]
        [SerializeField] private float repairTimeDiscount = 0.4f;   // 수리 시간 40% 감소
        [Range(0f, 0.9f)]
        [SerializeField] private float repairCostDiscount = 0.5f;   // 재료 50% 감소

        [Header("수리공 능력 설정 - 야간 전용 수리")]
        [Range(0f, 1f)]
        [SerializeField] private float nightRepairRatio = 0.4f;     // 현재 레벨 최대 내구도의 40%

        public override bool CanUseNightRepair => true;
        public override float NightRepairRatio => nightRepairRatio;

        public override void Execute(Character caster)
        {
            Debug.Log($"{caster.name} 수리공 능력 적용");
        }

        public override string AbilityTitle => "정비 전문가";
        public override string GetDescription() =>
            $"트럭 수리 시간이 {repairTimeDiscount * 100f:0}% 줄고, 수리 재료를 {repairCostDiscount * 100f:0}% 덜 씁니다.";

        public override float CalculateRepairTime(float baseSeconds)
        {
            if (baseSeconds <= 0f) return baseSeconds;

            // 트럭 E 상호작용이 매 프레임 묻는 값이라 여기서 로그를 남기지 않는다 (기록은 CharacterAbilityController → AbilityDebug)
            return Mathf.Max(0f, baseSeconds * (1f - repairTimeDiscount));
        }

        public override int CalculateRepairCost(int baseCost)
        {
            if (baseCost <= 0) return baseCost;

            float floatCost = baseCost * (1f - repairCostDiscount);
            return Mathf.Max(1, Mathf.FloorToInt(floatCost + 0.0001f));
        }
    }
}
