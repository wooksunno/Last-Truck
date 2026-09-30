using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Ability_BlackSmith", menuName = "Character/Character Abilities/BlackSmith")]
    public class PlayerAbility_BlackSmith : CharacterAbility
    {
        [Header("대장장이 능력 설정")]
        [SerializeField] private float weaponMaterialDiscount = 0.2f;   // 무기 제작 재료비용 20% 감소

        public override void Execute(Character caster)
        {
            Debug.Log($"{caster.name} 대장장이 능력 적용");
        }

        public override int CalculateCraftingCost(int originalCost, bool isWeapon)
        {
            if (originalCost <= 0) return originalCost;

            // 무기 제작 시에만 감소 적용
            if (!isWeapon) return originalCost;

            // 20% 감소 계산
            int discountedCost = Mathf.Max(1, Mathf.FloorToInt(originalCost * (1f - weaponMaterialDiscount)));

            Debug.Log($"[대장장이] {originalCost}개 -> {discountedCost}개");
            return discountedCost;
        }
    }
}

