using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Ability_BlackSmith", menuName = "Character/Character Abilities/BlackSmith")]
    public class PlayerAbility_BlackSmith : CharacterAbility
    {
        [Header("대장장이 능력 설정")]
        [SerializeField] private float weaponMaterialDiscount = 0.2f;   // 무기 제작 재료비용 20% 감소
        [SerializeField] private float processingTimeDiscount = 0.2f;   // 무기 제작 가공시간 20% 감소

        public override void Execute(Character caster)
        {
            Debug.Log($"{caster.name} 대장장이 능력 적용");
        }

        // 1. 제작 비용 감소
        public override int CalculateCraftingCost(int originalCost, bool isWeapon)
        {
            if (originalCost <= 0) return originalCost;
            if (!isWeapon) return originalCost;

            float floatCost = originalCost * (1f - weaponMaterialDiscount);
            int discountedCost = Mathf.Max(1, Mathf.FloorToInt(floatCost + 0.0001f));

            return discountedCost;
        }

        public override float CalculateProcessingTime(float originalSeconds, bool isWeapon)
        {
            if (originalSeconds <= 0f) { return originalSeconds; }

            float discountedSeconds = originalSeconds * (1f - processingTimeDiscount);
            discountedSeconds = Mathf.Max(0f, discountedSeconds);

            return discountedSeconds;
        }
    }
}