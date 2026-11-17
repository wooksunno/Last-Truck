using System.Collections.Generic;
using CraftingSystem;
using UnityEngine;

namespace LastTruck
{
    [CreateAssetMenu(fileName = "Ability_Scientist", menuName = "Character/Character Abilities/Scientist")]
    public class PlayerAbility_Scientist : CharacterAbility
    {
        [System.Serializable]
        public class MineralConversionRecipe
        {
            public string inputItemId;
            public int inputCount = 3;
            public string resultItemId;
            public int outputCount = 1;
        }

        [Header("과학자 능력 설정 - 가공 효율")]
        [Range(0f, 0.9f)]
        [SerializeField] private float processingTimeDiscount = 0.3f;  // 가공 시간 30% 감소

        [Header("과학자 능력 설정 - 광물 변환 조합")]
        [SerializeField]
        private List<MineralConversionRecipe> conversionRecipes = new List<MineralConversionRecipe>
        {
            new MineralConversionRecipe { inputItemId = "stone",    inputCount = 5, resultItemId = "iron_ore",   outputCount = 1 },
            new MineralConversionRecipe { inputItemId = "iron_ore", inputCount = 3, resultItemId = "copper_ore", outputCount = 1 },
        };

        public override bool CanConvertMinerals => true;

        public override void Execute(Character caster)
        {
            Debug.Log($"{caster.name} 과학자 능력 적용");
        }

        public override string AbilityTitle => "연구 효율";
        public override string GetDescription()
        {
            var parts = new List<string>();
            foreach (MineralConversionRecipe r in conversionRecipes)
            {
                if (r == null) continue;
                parts.Add($"{ItemLabel(r.inputItemId)} {r.inputCount}개를 {ItemLabel(r.resultItemId)} {r.outputCount}개로");
            }
            if (parts.Count == 0)
                return $"모든 가공 시간이 {processingTimeDiscount * 100f:0}% 줄어듭니다.";
            return $"모든 가공 시간이 {processingTimeDiscount * 100f:0}% 줄고, 낮에는 트럭 보관함에서 {string.Join(", ", parts)} 바꿀 수 있습니다.";
        }

        private static string ItemLabel(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "?";
            ItemData item = ItemCatalog.GetOrCreate().GetItem(itemId);
            return item != null ? item.itemName : itemId;
        }

        // 가공 작업 전체 + 광물 변환에 적용 (무기 여부와 무관)
        public override float CalculateProcessingTime(float originalSeconds, bool isWeapon)
        {
            if (originalSeconds <= 0f) return originalSeconds;

            // 가공 창을 그릴 때마다 불리므로 여기서 로그를 남기지 않는다 (기록은 CharacterAbilityController → AbilityDebug)
            return Mathf.Max(0f, originalSeconds * (1f - processingTimeDiscount));
        }

        public override bool TryGetMineralConversion(ItemData inputItem, out string resultItemId, out int inputCount, out int outputCount)
        {
            resultItemId = null;
            inputCount = 0;
            outputCount = 0;
            if (inputItem == null) return false;

            var recipe = conversionRecipes.Find(r =>
                string.Equals(r.inputItemId, inputItem.itemID, System.StringComparison.OrdinalIgnoreCase));
            if (recipe == null) return false;

            resultItemId = recipe.resultItemId;
            inputCount = recipe.inputCount;
            outputCount = recipe.outputCount;
            return true;
        }
    }
}
