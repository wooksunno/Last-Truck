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

        // 맨손 + 모든 근접무기, 근접 공격만 해당
        public override bool TryApplyStun(bool isMeleeAttack, out float duration)
        {
            duration = 0f;
            if (!isMeleeAttack) return false;

            if (Random.value < stunChance)
            {
                duration = stunDuration;
                Debug.Log($"[복서] 기절 발동! {duration}초");
                return true;
            }
            return false;
        }
    }
}
