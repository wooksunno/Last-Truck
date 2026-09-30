using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Ability_Chef", menuName = "Character/Character Abilities/Chef")]
    public class PlayerAbility_Chef : CharacterAbility
    {
        [Header("Settings")]
        public float healAmount = 20f;

        public override void Execute(Character caster)
        {
            Debug.Log($"{caster.name} ¿ä¸®»ç");

            if (caster.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.Heal(healAmount);
            }
        }
    }
}