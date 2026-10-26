namespace Bush.Core
{
    /// <summary>
    /// What the player has unlocked: anything marked unlocked-by-default in the design data, plus what
    /// the save has earned. Always check through here rather than reading the save's lists directly.
    /// </summary>
    public static class Unlocks
    {
        public static bool IsRecipeUnlocked(DesignData design, GameState state, string recipeId)
        {
            RecipeData recipe = design.GetRecipe(recipeId);
            return recipe != null && (recipe.unlockedByDefault || state.unlockedRecipes.Contains(recipeId));
        }

        public static bool IsPerkUnlocked(DesignData design, GameState state, string perkId)
        {
            PerkData perk = design.GetPerk(perkId);
            return perk != null && (perk.unlockedByDefault || state.unlockedPerks.Contains(perkId));
        }

        public static bool IsCompanionUnlocked(DesignData design, GameState state, string companionId)
        {
            CompanionData companion = design.GetCompanion(companionId);
            return companion != null && (companion.unlockedByDefault || state.unlockedCompanions.Contains(companionId));
        }
    }
}
