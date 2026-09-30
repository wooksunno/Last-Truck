using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Ability_Explorer", menuName = "Character/Character Abilities/Explorer")]
    public class PlayerAbility_Explorer : CharacterAbility
    {
        [Header("Å½Çè°¡ ´É·Â ¼³Á¤")]
        [SerializeField] private float resourceMultiplier = 1.2f;

        public override void Execute(Character caster)
        {
            Debug.Log($"{caster.name} Å½Çè°¡ ´É·Â Àû¿ë");
        }

        public override int CalculateGatherAmount(int baseAmount)
        {
            if (baseAmount <= 0) return baseAmount;

            int extraAmount = Mathf.CeilToInt(baseAmount * resourceMultiplier);
            Debug.Log($"[Å½Çè°¡] ÀÚ¿ø È¹µæ·® Áõ°¡: {baseAmount}°³ -> {extraAmount}°³");
            return extraAmount;
        }
    }
}