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

        public override string AbilityTitle => "숙련 채굴";
        public override string GetDescription() =>
            $"광물을 {gatherSpeedBonus * 100f:0}% 더 빨리 캐고, 캘 때마다 {extraYieldBonus}개를 더 얻습니다.";

        public override void Execute(Character caster)
        {
            Debug.Log($"<color=yellow>[광부]</color> {caster.name} 광부 능력 적용");
        }

        public override float CalculateGatherTime(float baseTime)
        {
            // 채집 UI가 매 프레임 이 값을 읽기 때문에 여기서는 로그를 남기지 않는다 (콘솔이 도배됨).
            if (baseTime <= 0f) return baseTime;
            return Mathf.Max(0.1f, baseTime * (1f - gatherSpeedBonus));
        }

        // 광물(채집 대상 목록)일 때만 채집 시간이 줄어든다. 나무/약초 등은 그대로.
        public override float CalculateGatherTime(ItemData targetItem, float baseTime)
        {
            return IsMiningTarget(targetItem) ? CalculateGatherTime(baseTime) : baseTime;
        }

        public bool IsMiningTarget(ItemData targetItem)
        {
            if (targetItem == null) return false;
            return miningTargetItemIds.Count == 0 ||
                   miningTargetItemIds.Exists(id => string.Equals(id, targetItem.itemID, System.StringComparison.OrdinalIgnoreCase));
        }

        public override int CalculateGatherAmount(ItemData targetItem, int baseAmount)
        {
            if (targetItem == null)
            {
                return baseAmount;
            }

            // 대소문자 구분 없이 아이템 ID 비교
            if (IsMiningTarget(targetItem))
            {
                int finalAmount = baseAmount + extraYieldBonus;
                return finalAmount;
            }
            return baseAmount;
        }
    }
}