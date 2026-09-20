using System.Collections.Generic;

namespace CraftingSystem
{
    /// <summary>
    /// 트럭 가공 탭에서 바로 가공할 수 있는 레시피 목록.
    /// 월드의 실제 가공 시설(모닥불/절삭기 등)에서 한 번 이상 성공적으로 가공해야 등록된다.
    /// </summary>
    public static class RecipeUnlocks
    {
        private static readonly HashSet<string> _unlocked = new HashSet<string>();

        public static bool IsUnlocked(RecipeData recipe) =>
            recipe != null && !string.IsNullOrEmpty(recipe.recipeID) && _unlocked.Contains(recipe.recipeID);

        public static void MarkUnlocked(RecipeData recipe)
        {
            if (recipe != null && !string.IsNullOrEmpty(recipe.recipeID))
                _unlocked.Add(recipe.recipeID);
        }

        public static void Reset() => _unlocked.Clear();
    }
}
