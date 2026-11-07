using CraftingSystem;
using UnityEngine;

namespace LastTruck
{
    public abstract class CharacterAbility : ScriptableObject
    {
        public float cooldown;

        public abstract void Execute(Character caster);
        public virtual float MarkDuration => 0f;

        /// <summary>
        /// 데미지 보정 (군인 오버라이드)
        /// </summary>
        public virtual int CalculateAttackDamage(int baseDamage, out bool isEnhanced)
        {
            isEnhanced = false;
            return baseDamage;
        }

        /// <summary>
        /// 자원 획득량 보정 (탐험가 오버라이드)
        /// </summary>
        public virtual int CalculateGatherAmount(int baseAmount)
        {
            return baseAmount;
        }

        /// <summary>
        /// 광물 채집 획득량 보정 (광부 오버라이드)
        /// </summary>
        public virtual int CalculateGatherAmount(ItemData targetItem, int baseAmount)
        {
            return baseAmount;
        }

        /// <summary>
        /// 광물 채집 시간 보정 (광부 오버라이드)
        /// </summary>
        public virtual float CalculateGatherTime(float baseTime)
        {
            return baseTime;
        }

        /// <summary>
        /// 재료량 보정, 가공 시간 보정 (대장장이 오버라이드)
        /// </summary>
        public virtual int CalculateCraftingCost(int originalCost, bool isWeapon)
        {
            return originalCost;
        }

        public virtual float CalculateProcessingTime(float originalSeconds, bool isWeapon)
        {
            return originalSeconds;
        }

        /// <summary>
        /// 경찰 표식 대상 공격 시 추가 데미지 보정 (경찰 오버라이드)
        /// </summary>
        public virtual int CalculateMarkBonusDamage(int baseDamage, bool targetHasMark)
        {
            return baseDamage;
        }

        // ───────── 복서 ─────────
        /// <summary>
        /// 근접 공격 적중 시 기절 판정 (복서 오버라이드)
        /// </summary>
        public virtual bool TryApplyStun(bool isMeleeAttack, out float stunDuration)
        {
            stunDuration = 0f;
            return false;
        }

        // ───────── 수리공 ─────────
        /// <summary>
        /// 트럭 일반 수리 시간 보정 (수리공 오버라이드)
        /// </summary>
        public virtual float CalculateRepairTime(float baseSeconds) => baseSeconds;

        /// <summary>
        /// 트럭 일반 수리 재료 보정 (수리공 오버라이드)
        /// </summary>
        public virtual int CalculateRepairCost(int baseCost) => baseCost;

        /// <summary>
        /// 야간 전용 수리 가능 여부 / 회복 비율 (수리공 오버라이드)
        /// </summary>
        public virtual bool CanUseNightRepair => false;
        public virtual float NightRepairRatio => 0f;

        // ───────── 요리사 ─────────
        /// <summary>
        /// 음식 제작 재료 보정 (요리사 오버라이드)
        /// </summary>
        public virtual int CalculateFoodCraftingCost(int originalCost) => originalCost;

        // ───────── 간호사 ─────────
        /// <summary>
        /// 구조 가능 거리 배율 (간호사 오버라이드)
        /// </summary>
        public virtual float RescueRangeMultiplier => 1f;

        /// <summary>
        /// 구조로 부활시킨 아군에게 주는 무적 시간 (간호사 오버라이드)
        /// </summary>
        public virtual float ReviveInvincibleDuration => 0f;

        /// <summary>
        /// HP가 낮은 아군에게 접근할 때 받는 이동속도 보너스 (간호사 오버라이드)
        /// </summary>
        public virtual float CalculateAllyApproachSpeedBonus(float allyHpRatio, bool isApproaching) => 0f;

        // ───────── 과학자 ─────────
        /// <summary>
        /// 광물 변환 가능 여부 (과학자 오버라이드)
        /// </summary>
        public virtual bool CanConvertMinerals => false;

        /// <summary>
        /// 광물 변환 조합 조회 (과학자 오버라이드)
        /// </summary>
        public virtual bool TryGetMineralConversion(ItemData inputItem, out string resultItemId, out int inputCount, out int outputCount)
        {
            resultItemId = null;
            inputCount = 0;
            outputCount = 0;
            return false;
        }
    }
}