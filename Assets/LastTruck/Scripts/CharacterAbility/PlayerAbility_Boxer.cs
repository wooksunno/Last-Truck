using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Ability_Boxer", menuName = "Character/Character Abilities/Boxer")]
    public class PlayerAbility_Boxer : CharacterAbility
    {
        [Header("복서 능력 설정")]
        [Range(0f, 1f)]
        [SerializeField] private float stunChance = 0.2f;     // 기절 확률 20%
        [SerializeField] private float stunDuration = 2f;     // 기절 2초

        public override void Execute(Character caster)
        {
            Debug.Log($"{caster.name} 복서 능력 적용");
        }

        public override string AbilityTitle => "기절 펀치";
        public override string GetDescription() =>
            $"근접 공격이 적중하면 {stunChance * 100f:0}% 확률로 적을 {stunDuration:0.#}초 동안 기절시킵니다.";

        // 맨손 + 모든 근접무기, 근접 공격만 해당
        public override bool TryApplyStun(bool isMeleeAttack, out float duration)
        {
            duration = 0f;
            if (!isMeleeAttack) return false;

            if (Random.value < stunChance)
            {
                duration = stunDuration;   // 발동 기록은 CharacterAbilityController가 남긴다 (AbilityDebug)
                return true;
            }
            return false;
        }
    }
}
