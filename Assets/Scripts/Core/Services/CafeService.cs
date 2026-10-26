using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>
    /// Cooking. Per the production plan there's no customer simulation for the jam: cooking a
    /// dish uses its ingredients and pays its coins straight away.
    /// </summary>
    public class CafeService
    {
        private readonly DesignData _design;
        private readonly GameState _state;
        private readonly Economy _economy;
        private readonly GameEvents _events;

        public CafeService(DesignData design, GameState state, Economy economy, GameEvents events)
        {
            _design = design;
            _state = state;
            _economy = economy;
            _events = events;
        }

        public bool IsUnlocked(string recipeId) => _state.unlockedRecipes.Contains(recipeId);

        /// <summary>Whether the recipe can be cooked now. <paramref name="missing"/> lists what's short, for the UI.</summary>
        public bool CanCook(string recipeId, out List<ItemAmount> missing)
        {
            missing = new List<ItemAmount>();
            RecipeData recipe = _design.GetRecipe(recipeId);
            if (recipe == null || !IsUnlocked(recipeId)) return false;

            foreach (ItemAmount ingredient in recipe.ingredients)
            {
                int have = _economy.Count(ingredient.itemId);
                if (have < ingredient.qty) missing.Add(new ItemAmount(ingredient.itemId, ingredient.qty - have));
            }
            return missing.Count == 0;
        }

        /// <summary>Uses the ingredients and pays the coins in one transaction.</summary>
        public bool Cook(string recipeId)
        {
            if (!CanCook(recipeId, out _)) return false;

            RecipeData recipe = _design.GetRecipe(recipeId);
            var transaction = new Transaction { coinGain = recipe.coins };
            foreach (ItemAmount ingredient in recipe.ingredients) transaction.Cost(ingredient.itemId, ingredient.qty);
            if (!_economy.TryTransact(transaction)) return false;

            _events.RaiseRecipeCooked(recipeId);
            return true;
        }

        /// <summary>How many of the recipe's scraps the player has ever found, for the recipe book.</summary>
        public (int found, int total) GetScrapProgress(string recipeId)
        {
            RecipeData recipe = _design.GetRecipe(recipeId);
            if (recipe == null) return (0, 0);
            int found = 0;
            foreach (string scrap in recipe.scraps)
                if (_state.everObtained.Contains(scrap)) found++;
            return (found, recipe.scraps.Count);
        }
    }
}
