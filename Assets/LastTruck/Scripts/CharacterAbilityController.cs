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

        private int lastNightRepairIndex = -1;

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

        

        // 복서
        public bool TryGetStun(bool isMeleeAttack, out float stunDuration)
        {
            stunDuration = 0f;
            var ability = CurrentAbility;
            if (!enabled || ability == null) return false;

            return ability.TryApplyStun(isMeleeAttack, out stunDuration);
        }

        // 수리공 - 일반 수리
        public float GetCalculatedRepairTime(float baseSeconds)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return baseSeconds;
            return ability.CalculateRepairTime(baseSeconds);
        }

        public int GetCalculatedRepairCost(int baseCost)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return baseCost;
            return ability.CalculateRepairCost(baseCost);
        }

        // 수리공 - 야간 전용 수리 (트럭 근처에서 E 길게 누름, 밤당 1회)
        public bool CanUseNightRepair(int nightIndex)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null || !ability.CanUseNightRepair) return false;
            return lastNightRepairIndex != nightIndex;
        }

        public bool TryUseNightRepair(int nightIndex, float currentLevelMaxDurability, out float repairAmount)
        {
            repairAmount = 0f;
            if (!CanUseNightRepair(nightIndex)) return false;

            repairAmount = currentLevelMaxDurability * CurrentAbility.NightRepairRatio;
            lastNightRepairIndex = nightIndex;
            Debug.Log($"[수리공] 야간 수리: 내구도 {repairAmount:F1} 회복 (밤 {nightIndex})");
            return true;
        }

        // 요리사
        public int GetCalculatedFoodCraftingCost(int originalCost)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return originalCost;
            return ability.CalculateFoodCraftingCost(originalCost);
        }

        // 간호사
        public float GetRescueRange(float baseRange)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return baseRange;
            return baseRange * ability.RescueRangeMultiplier;
        }

        public float GetReviveInvincibleDuration()
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return 0f;
            return ability.ReviveInvincibleDuration;
        }

        public float GetAllyApproachSpeedMultiplier(float allyHpRatio, bool isApproaching)
        {
            var ability = CurrentAbility;
            if (!enabled || ability == null) return 1f;
            return 1f + ability.CalculateAllyApproachSpeedBonus(allyHpRatio, isApproaching);
        }

        // 과학자 - 광물 변환 (낮에만, 공용 창고의 돌·원석 사용)
        public bool TryGetMineralConversion(ItemData inputItem, bool isDay,
            out string resultItemId, out int inputCount, out int outputCount)
        {
            resultItemId = null; inputCount = 0; outputCount = 0;
            var ability = CurrentAbility;
            if (!enabled || ability == null || !ability.CanConvertMinerals) return false;
            if (!isDay) return false;

            return ability.TryGetMineralConversion(inputItem, out resultItemId, out inputCount, out outputCount);
        }


    }
}