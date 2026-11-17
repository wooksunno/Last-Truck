using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Ability_Nurse", menuName = "Character/Character Abilities/Nurse")]
    public class PlayerAbility_Nurse : CharacterAbility
    {
        [Header("간호사 능력 설정 - 원거리 구조")]
        [SerializeField] private float rescueRangeMultiplier = 8f;     // 일반 구조 거리의 8배

        [Header("간호사 능력 설정 - 구조 후 무적")]
        [SerializeField] private float reviveInvincibleDuration = 1f;  // 부활 후 1초 무적

        [Header("간호사 능력 설정 - 아군 접근")]
        [Range(0f, 1f)]
        [SerializeField] private float lowHpThreshold = 0.25f;         // 최대 HP 25% 이하
        [SerializeField] private float approachSpeedBonus = 0.5f;      // 이동속도 +50%

        public override float RescueRangeMultiplier => rescueRangeMultiplier;
        public override float ReviveInvincibleDuration => reviveInvincibleDuration;

        public override void Execute(Character caster)
        {
            Debug.Log($"{caster.name} 간호사 능력 적용");
        }

        public override string AbilityTitle => "응급 구조";
        public override string GetDescription() =>
            $"쓰러진 동료를 {rescueRangeMultiplier:0.#}배 먼 거리에서 살리고 살린 동료는 {reviveInvincibleDuration:0.#}초간 무적이 되며, " +
            $"체력이 {lowHpThreshold * 100f:0}% 이하인 동료에게 다가갈 때 {approachSpeedBonus * 100f:0}% 빨라집니다.";

        public override float CalculateAllyApproachSpeedBonus(float allyHpRatio, bool isApproaching)
        {
            if (!isApproaching) return 0f;
            if (allyHpRatio <= 0f) return 0f;  // 쓰러진 아군은 구조 대상이라 제외 (필요 없으면 이 줄 삭제)
            return allyHpRatio <= lowHpThreshold ? approachSpeedBonus : 0f;
        }
    }
}
