using System;
using System.Collections.Generic;
using UnityEngine;

namespace CraftingSystem
{
    /// <summary>
    /// None = 트럭 기본조립. Campfire/PrecisionCutter/RollerPress = 가공 시설.
    /// </summary>
    public enum FacilityType
    {
        None,
        Campfire,
        Grindstone,
        RollerPress,
        PrecisionCutter,
        ChemicalRefinery
    }

    [Serializable]
    public class RecipeIngredient
    {
        public ItemData item;
        public int count;

        public RecipeIngredient()
        {
        }

        public RecipeIngredient(ItemData item, int count)
        {
            this.item = item;
            this.count = count;
        }
    }

    [CreateAssetMenu(fileName = "NewRecipeData", menuName = "Crafting/Recipe Data")]
    public class RecipeData : ScriptableObject
    {
        public string recipeID;
        public string displayName;
        public FacilityType requiredFacility = FacilityType.None;
        public List<RecipeIngredient> inputs = new List<RecipeIngredient>();
        public RecipeIngredient output = new RecipeIngredient();

        [Tooltip("가공에 걸리는 시간(초). 기획서에 명시된 값이 없어 기본값으로 둠.")]
        public float processingSeconds = 2f;

        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(displayName))
                return displayName;

            if (output != null && output.item != null)
                return output.item.itemName;

            return recipeID;
        }

        public string GetIngredientSummary()
        {
            if (inputs == null || inputs.Count == 0)
                return "-";

            var parts = new List<string>();
            foreach (RecipeIngredient input in inputs)
            {
                if (input == null || input.item == null || input.count <= 0)
                    continue;
                parts.Add($"{input.item.itemName} x{input.count}");
            }

            string inputText = parts.Count > 0 ? string.Join(" + ", parts) : "-";
            string outputText = output != null && output.item != null
                ? $"{output.item.itemName} x{output.count}"
                : "?";
            return $"{inputText} → {outputText}";
        }
    }
}
