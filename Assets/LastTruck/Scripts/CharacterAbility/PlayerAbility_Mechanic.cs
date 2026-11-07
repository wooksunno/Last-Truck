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

        public override float CalculateRepairTime(float baseSeconds)
        {
            if (baseSeconds <= 0f) return baseSeconds;

            float result = Mathf.Max(0f, baseSeconds * (1f - repairTimeDiscount));
            Debug.Log($"[수리공] 수리 시간 감소: {baseSeconds}초 -> {result:F2}초");
            return result;
        }

        public override int CalculateRepairCost(int baseCost)
        {
            if (baseCost <= 0) return baseCost;

            float floatCost = baseCost * (1f - repairCostDiscount);
            int result = Mathf.Max(1, Mathf.FloorToInt(floatCost + 0.0001f));
            Debug.Log($"[수리공] 수리 재료 감소: {baseCost}개 -> {result}개");
            return result;
        }
    }
}
