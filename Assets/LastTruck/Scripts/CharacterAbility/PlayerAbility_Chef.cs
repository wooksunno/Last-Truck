using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Ability_Chef", menuName = "Character/Character Abilities/Chef")]
    public class PlayerAbility_Chef : CharacterAbility
    {
        [Header("Settings")]
        public float healAmount = 20f;

        [Header("요리사 능력 설정 - 음식 재료 절약")]
        [Range(0f, 0.9f)]
        [SerializeField] private float foodMaterialDiscount = 0.5f;   // 음식 제작 재료 50% 감소 (최소 1개)

        public override void Execute(Character caster)
        {
            Debug.Log($"{caster.name} 요리사");

            if (caster.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.Heal(healAmount);
            }
        }

        public override string AbilityTitle => "알뜰 요리";
        public override string GetDescription() =>
            $"요리할 때 2개 이상 들어가는 재료를 {foodMaterialDiscount * 100f:0}% 아껴 씁니다.";

        // 음식(ItemIds.IsFood) 제작 재료 감소. 재료가 1개짜리 레시피는 최소 1개라 그대로다.
        public override int CalculateFoodCraftingCost(int originalCost)
        {
            if (originalCost <= 1) return originalCost;

            float floatCost = originalCost * (1f - foodMaterialDiscount);
            return Mathf.Max(1, Mathf.FloorToInt(floatCost + 0.0001f));
        }
    }
}