using Combat;
using UnityEngine;

namespace LastTruck
{
    [RequireComponent(typeof(WeaponController))]

    public class CharacterAbilityController : MonoBehaviour
    {
        [Header("자동 연동 컴포넌트")]
        [SerializeField] private WeaponController weaponController;

        [Header("현재 캐릭터 능력 데이터")]
        [SerializeField] private CharacterAbility currentAbility;

        private void Reset()
        {
            weaponController = GetComponent<WeaponController>();
        }

        private void Awake()
        {
            if (weaponController == null)
                weaponController = GetComponent<WeaponController>();

            if (currentAbility != null && TryGetComponent<Character>(out var caster))
            {
                currentAbility.Execute(caster);
            }
        }

        // 군인
        public int GetCalculatedDamage(int originalDamage, out bool isEnhanced)
        {
            if (!enabled || currentAbility == null)
            {
                isEnhanced = false;
                return originalDamage;
            }

            return currentAbility.CalculateAttackDamage(originalDamage, out isEnhanced);
        }

        // 탐험가
        public int GetCalculatedResourceAmount(int baseAmount)
        {
            if (!enabled || currentAbility == null)
            {
                return baseAmount;
            }

            return currentAbility.CalculateGatherAmount(baseAmount);
        }

        // 대장장이
        public int GetCalculatedCraftingCost(int originalCost, bool isWeapon)
        {
            if (!enabled || currentAbility == null) { return originalCost; }
            return currentAbility.CalculateCraftingCost(originalCost, isWeapon);
        }
    }
}