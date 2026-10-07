using System.Collections.Generic;
using UnityEngine;
using CraftingSystem;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Ability_Miner", menuName = "Character/Character Abilities/Miner")]
    public class PlayerAbility_Miner : CharacterAbility
    {
        [Header("광부 능력 설정")]
        [Range(0f, 0.8f)]
        [SerializeField] private float gatherSpeedBonus = 0.2f;
        [SerializeField] private int extraYieldBonus = 1;

        [Header("대상 아이템 ID")]
        [Tooltip("광부 능력 적용 itemID")]
        [SerializeField]
        private List<string> miningTargetItemIds = new List<string>
        {
            "stone",
            "iron_ore",
            "copper_ore",
            "platinum_ore",
            "diamond"
        };

        public float GatherSpeedBonus => gatherSpeedBonus;

        public override void Execute(Character caster)
        {
            Debug.Log($"<color=yellow>[광부]</color> {caster.name} 광부 능력 적용");
        }

        public override float CalculateGatherTime(float baseTime)
        {
            float finalTime = Mathf.Max(0.1f, baseTime * (1f - gatherSpeedBonus));

            Debug.Log($"<color=cyan>[광부]</color> 채집 시간 감소: {baseTime}초 ➔ {finalTime:F2}초");

            return finalTime;
        }

        public override int CalculateGatherAmount(ItemData targetItem, int baseAmount)
        {
            if (targetItem == null)
            {
                return baseAmount;
            }

            // 대소문자 구분 없이 아이템 ID 비교
            bool isMiningTarget = miningTargetItemIds.Count == 0 ||
                                  miningTargetItemIds.Exists(id => string.Equals(id, targetItem.itemID, System.StringComparison.OrdinalIgnoreCase));

            if (isMiningTarget)
            {
                int finalAmount = baseAmount + extraYieldBonus;
                return finalAmount;
            }
            return baseAmount;
        }
    }
}