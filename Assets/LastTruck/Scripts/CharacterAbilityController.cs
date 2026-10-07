using Combat;
using CraftingSystem;
using UnityEngine;

namespace LastTruck
{
    [RequireComponent(typeof(WeaponController))]
    public class CharacterAbilityController : MonoBehaviour
    {
        [Header("자동 연동 컴포넌트")]
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private Character character;

        public CharacterAbility CurrentAbility
        {
            get
            {
                if (character != null && character.StatsData != null)
                {
                    return character.StatsData.UniqueAbility;
                }
                return null;
            }
        }

        private void Reset()
        {
            weaponController = GetComponent<WeaponController>();
        }

        private void Awake()
        {
            if (weaponController == null)
                weaponController = GetComponent<WeaponController>();

            if (character == null)
                character = GetComponent<Character>();

            if (CurrentAbility != null && character != null)
            {
                CurrentAbility.Execute(character);
            }
        }

        // 군인
        public int GetCalculatedDamage(int originalDamage, out bool isEnhanced)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null)
            {
                isEnhanced = false;
                return originalDamage;
            }

            return ability.CalculateAttackDamage(originalDamage, out isEnhanced);
        }

        // 탐험가
        public int GetCalculatedResourceAmount(int baseAmount)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null)
            {
                return baseAmount;
            }

            return ability.CalculateGatherAmount(baseAmount);
        }

        // 대장장이
        public int GetCalculatedCraftingCost(int originalCost, bool isWeapon)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) { return originalCost; }
            return ability.CalculateCraftingCost(originalCost, isWeapon);
        }

        public float GetCalculatedProcessingTime(float originalSeconds, bool isWeapon)
        {
            var ability = CurrentAbility;
            if (ability == null) return originalSeconds;

            return ability.CalculateProcessingTime(originalSeconds, isWeapon);
        }

        // 광부
        public float GetCalculatedMiningTime(float baseTime)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return baseTime;

            return ability.CalculateGatherTime(baseTime);
        }

        public int GetCalculatedMiningAmount(ItemData targetItem, int baseAmount)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return baseAmount;

            return ability.CalculateGatherAmount(targetItem, baseAmount);
        }

        // 경찰
        // 경찰 - 표식 시스템
        public int GetCalculatedPoliceDamage(int baseDamage, bool targetHasMark, out float markDuration)
        {
            markDuration = 0f;
            var ability = CurrentAbility;
            if (!enabled || ability == null) return baseDamage;

            markDuration = ability.MarkDuration; // 경찰이면 2.0f, 다른 캐릭터면 0f 반환

            return ability.CalculateMarkBonusDamage(baseDamage, targetHasMark);
        }
    }
}