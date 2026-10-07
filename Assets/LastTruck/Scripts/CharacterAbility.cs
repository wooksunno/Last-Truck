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
    }
}