
using UnityEngine;

namespace LastTruck
{
    public abstract class CharacterAbility : ScriptableObject
    {
        public float cooldown;

        public abstract void Execute(Character caster);

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
        /// 제작 재료 요구량 보정 (대장장이 오버라이드)
        /// </summary>
        public virtual int CalculateCraftingCost(int originalCost, bool isWeapon)
        {
            return originalCost;
        }

        public virtual float CalculateProcessingTime(float originalSeconds, bool isWeapon) => originalSeconds;
    }
}