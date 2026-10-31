using System;
using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>
    /// The cafe: learning recipes, the day's menu, and closing for the day. There's no customer
    /// simulation: the cafe sells a set number of dishes a day (<see cref="DayRules"/>), shared evenly
    /// across the menu and across the day. It serves each part of the day's share while you're out on that
    /// part's forage trip (<see cref="ServeWhileOut"/>): each sale cooks the dish (uses its ingredients,
    /// pays its coins) with what's in the larder as you head out. Menu changes count from the next part.
    /// Closing for the day tallies it all up. (With no day clock, it all sells at closing instead.)
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

        public bool IsUnlocked(string recipeId) => Unlocks.IsRecipeUnlocked(_design, _state, recipeId);

        /// <summary>Recipes that can be bought: locked, with a price.</summary>
        public bool CanLearn(string recipeId)
        {
            RecipeData recipe = _design.GetRecipe(recipeId);
            return recipe != null && recipe.price > 0 && !IsUnlocked(recipeId) && _economy.Currency >= recipe.price;
        }

        /// <summary>Learns a recipe for its price in coins (the GDD's "use what you earn to learn recipes").</summary>
        public bool Learn(string recipeId)
        {
            if (!CanLearn(recipeId)) return false;
            if (!_economy.TrySpend(_design.GetRecipe(recipeId).price)) return false;
            _state.unlockedRecipes.Add(recipeId);
            _events.RaiseRecipeLearned(recipeId);
            return true;
        }

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

        /// <summary>
        /// Whether any recipe you know could be made from what's in the larder: the only time an empty menu is
        /// worth a warning (on the first morning you know a recipe but have nothing to make it with).
        /// </summary>
        public bool CanCookAnything() => _design.recipes.Exists(r => CanCook(r.id, out _));

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

        // ----- The menu -----

        public IReadOnlyList<string> Menu => _state.menu;
        public bool IsOnMenu(string recipeId) => _state.menu.Contains(recipeId);
        public bool MenuIsFull => _state.menu.Count >= _design.rules.MenuSlots(_state);

        /// <summary>Puts a known recipe on today's menu, if there's a free slot.</summary>
        public bool AddToMenu(string recipeId)
        {
            if (_design.GetRecipe(recipeId) == null || !IsUnlocked(recipeId) || IsOnMenu(recipeId) || MenuIsFull) return false;
            _state.menu.Add(recipeId);
            _events.RaiseMenuChanged();
            return true;
        }

        public bool RemoveFromMenu(string recipeId)
        {
            if (!_state.menu.Remove(recipeId)) return false;
            _events.RaiseMenuChanged();
            return true;
        }

        /// <summary>What the cafe would serve during the next part of the day, from what's in the larder now. Changes nothing.</summary>
        public List<DishSale> PlanSales(out int unsold) => PlanSales(_design.rules.NextPartSales(_state), _state.menuTurn, out unsold, out _);

        /// <summary>
        /// <paramref name="sales"/> dishes, dish by dish (in menu order), shared evenly: each sale goes to the
        /// next dish in turn (from <paramref name="turn"/>) that can still be made from what's left. Doesn't
        /// change anything. <paramref name="unsold"/> is how many sales were missed for want of ingredients.
        /// </summary>
        private List<DishSale> PlanSales(int sales, int turn, out int unsold, out int nextTurn)
        {
            var plan = new List<DishSale>();
            var counts = new Dictionary<string, int>();
            int Have(string itemId) => counts.TryGetValue(itemId, out int n) ? n : counts[itemId] = _economy.Count(itemId);

            // Only as many dishes as there are slots (a save from before the slots shrank may have more).
            int slots = _design.rules.MenuSlots(_state);
            foreach (string recipeId in _state.menu)
                if (plan.Count < slots && _design.GetRecipe(recipeId) != null && IsUnlocked(recipeId)) plan.Add(new DishSale { recipeId = recipeId });

            int made = 0, next = plan.Count > 0 ? Math.Abs(turn) % plan.Count : 0;
            while (made < sales && plan.Count > 0)
            {
                bool any = false;
                for (int tried = 0; tried < plan.Count && !any; tried++)
                {
                    DishSale dish = plan[(next + tried) % plan.Count];
                    RecipeData recipe = _design.GetRecipe(dish.recipeId);
                    if (!recipe.ingredients.TrueForAll(i => Have(i.itemId) >= i.qty)) continue;
                    foreach (ItemAmount ingredient in recipe.ingredients) counts[ingredient.itemId] -= ingredient.qty;
                    dish.sold++;
                    dish.coins += recipe.coins;
                    next = (plan.IndexOf(dish) + 1) % plan.Count;
                    any = true;
                }
                if (!any) break; // nothing left can be made
                made++;
            }

            unsold = sales - made;
            nextTurn = next;
            return plan;
        }

        /// <summary>
        /// Serves the parts of the day that have begun but not been served: call when a forage trip sets off
        /// (that trip is the part). Cooks each sale, keeps the day's tally, and returns what sold, which is also
        /// kept in <see cref="GameState.servedWhileOut"/> for the trip's results. Nothing at night, or with no
        /// day clock (then it all sells at closing).
        /// </summary>
        public List<DishSale> ServeWhileOut()
        {
            var served = new List<DishSale>();
            _state.servedWhileOut = served;
            if (_state.isNight || _design.rules.TripsPerDay(_state) <= 0) return served;

            while (_state.partsServed < _design.rules.PartsPassed(_state))
            {
                _state.partsServed++;
                int sales = _design.rules.SalesForPart(_state, _state.partsServed);
                List<DishSale> plan = PlanSales(sales, _state.menuTurn, out int unsold, out int nextTurn);
                _state.menuTurn = nextTurn;
                _state.missedToday += unsold;
                foreach (DishSale planned in plan) Sell(planned, served);
            }
            foreach (DishSale dish in served) Tally(_state.soldToday, dish);
            return served;
        }

        /// <summary>Cooks a planned dish's sales and adds them to <paramref name="into"/>.</summary>
        private void Sell(DishSale planned, List<DishSale> into)
        {
            if (planned.sold <= 0) return; // dishes that didn't sell aren't listed
            var dish = new DishSale { recipeId = planned.recipeId };
            for (int i = 0; i < planned.sold; i++)
            {
                if (!Cook(planned.recipeId)) break; // can't happen: the plan used the same counts
                dish.sold++;
                int coins = _design.GetRecipe(planned.recipeId).coins;
                dish.coins += coins;
                if (_design.rules.TodaysEvent(_state)?.critic == true && !_state.criticDone)
                {
                    // The food critic: the first dish sold today pays double.
                    _state.criticDone = true;
                    _economy.AddCurrency(coins);
                    dish.coins += coins;
                    dish.critic = true;
                }
            }
            Tally(into, dish);
        }

        private static void Tally(List<DishSale> into, DishSale dish)
        {
            DishSale existing = into.Find(d => d.recipeId == dish.recipeId);
            if (existing == null) into.Add(new DishSale { recipeId = dish.recipeId, sold = dish.sold, coins = dish.coins, critic = dish.critic });
            else
            {
                existing.sold += dish.sold;
                existing.coins += dish.coins;
                existing.critic |= dish.critic;
            }
        }

        /// <summary>
        /// Whether "Close for the day" is offered yet: true once the quest in <see cref="DayRules.closeUnlockedBy"/>
        /// has been taken on (or done), or if there's none. <see cref="CloseDay"/> itself doesn't check, so cheats still work.
        /// </summary>
        public bool CloseUnlocked() => CloseUnlockedBy() == null;

        /// <summary>The quest that has to be taken on before closing for the day, while it hasn't been; otherwise null.</summary>
        public QuestData CloseUnlockedBy()
        {
            QuestData quest = _design.GetQuest(_design.rules?.closeUnlockedBy);
            if (quest == null) return null;
            QuestStatus status = _state.FindQuest(quest.id)?.status ?? QuestStatus.NotStarted;
            return status == QuestStatus.NotStarted ? quest : null;
        }

        /// <summary>
        /// Ends the day: cooks and sells what <see cref="PlanSales"/> says (fewer if it's not evening yet:
        /// see <see cref="DayRules"/>), moves to the next morning and resets the day's forage trips (any
        /// left are lost). Each sale counts as cooking the dish, for quests. The menu stays
        /// as it was. Save straight after.
        /// </summary>
        public DaySummary CloseDay()
        {
            if (_state.isNight) return new DaySummary { day = _state.day, nightFalls = true }; // already closed
            var summary = new DaySummary
            {
                day = _state.day,
                salesPlanned = _design.rules.SalesFor(_state),
                fullDaySales = _design.rules.FullDaySales(_state),
                closedEarly = !_design.rules.IsEvening(_state)
            };

            if (_design.rules.TripsPerDay(_state) <= 0)
            {
                // No day clock: the whole day sells now.
                List<DishSale> plan = PlanSales(summary.fullDaySales, _state.menuTurn, out int unsold, out _);
                _state.missedToday += unsold;
                foreach (DishSale planned in plan) Sell(planned, _state.soldToday);
            }
            else
            {
                ServeWhileOut(); // any part begun but not served yet (normally none)
            }

            // The day's tally: everything served while you were out.
            foreach (DishSale dish in _state.soldToday)
            {
                summary.dishes.Add(new DishSale { recipeId = dish.recipeId, sold = dish.sold, coins = dish.coins });
                summary.sold += dish.sold;
                summary.coins += dish.coins;
            }
            summary.salesPlanned = summary.sold + _state.missedToday;

            // A lucky day: the cosier the cafe, the likelier a happy guest leaves a tip.
            if (summary.sold > 0)
            {
                var dice = new Mulberry32(Visitors.DailySeed(_state, 1));
                if (dice.NextDouble() < CafeStyle.LuckyDayChance(_design, _state))
                {
                    summary.tip = Math.Max(3, summary.coins / 4);
                    _economy.AddCurrency(summary.tip);
                    summary.coins += summary.tip;
                }
            }

            if (_state.nightUnlocked)
            {
                // Into the night: the cafe's closed, nocturnal guests come out, one night forage trip.
                _state.isNight = true;
                _state.nightTripsToday = 0;
                summary.nightFalls = true;
                _events.RaiseDayEnded(summary);
                _events.RaiseNightStarted();
            }
            else
            {
                StartNextMorning();
                _events.RaiseDayEnded(summary);
            }
            return summary;
        }

        /// <summary>Ends the night and starts the next morning. Only at night.</summary>
        public bool GoToBed()
        {
            if (!_state.isNight) return false;
            StartNextMorning();
            return true;
        }

        private void StartNextMorning()
        {
            _state.isNight = false;
            _state.day++;
            _state.forageTripsToday = 0;
            _state.nightTripsToday = 0;
            _state.partsServed = 0;
            _state.soldToday.Clear();
            _state.missedToday = 0;
            _state.menuTurn = 0;
            _state.servedWhileOut.Clear();
            _state.timeOfDay = 0f;
            _state.companionsUsedToday.Clear();
            DayEvents.RollToday(_design, _state, _economy);
            Visitors.RollToday(_design, _state);
            _events.RaiseMorningStarted();
        }

        // ----- Upgrades -----

        public bool HasUpgrade(string upgradeId) => _state.upgrades.Contains(upgradeId);

        /// <summary>Whether it can be bought now; <paramref name="reason"/> says why not (already owned, needs another first, too dear).</summary>
        public bool CanBuyUpgrade(string upgradeId, out string reason)
        {
            reason = null;
            UpgradeData upgrade = _design.GetUpgrade(upgradeId);
            if (upgrade == null) reason = "Unknown upgrade.";
            else if (HasUpgrade(upgradeId)) reason = "Already done.";
            else if (!string.IsNullOrEmpty(upgrade.requires) && !HasUpgrade(upgrade.requires))
                reason = $"Needs {_design.GetUpgrade(upgrade.requires)?.name ?? upgrade.requires} first.";
            else if (_economy.Currency < upgrade.price) reason = $"{upgrade.price} coins.";
            return reason == null;
        }

        public bool BuyUpgrade(string upgradeId)
        {
            if (!CanBuyUpgrade(upgradeId, out _)) return false;
            if (!_economy.TrySpend(_design.GetUpgrade(upgradeId).price)) return false;
            _state.upgrades.Add(upgradeId);
            _events.RaiseUpgradeBought(upgradeId);
            return true;
        }

        /// <summary>How many of the recipe's scraps the player has collected (has now or handed in), for the recipe book.</summary>
        public (int found, int total) GetScrapProgress(string recipeId)
        {
            RecipeData recipe = _design.GetRecipe(recipeId);
            if (recipe == null) return (0, 0);
            int found = 0;
            foreach (string scrap in recipe.scraps)
                if (_state.IsUniqueCollected(scrap)) found++;
            return (found, recipe.scraps.Count);
        }
    }

    /// <summary>One dish's sales for the day.</summary>
    [Serializable]
    public class DishSale
    {
        public string recipeId;
        public int sold;
        public int coins;
        /// <summary>The food critic ate this one (it paid double).</summary>
        public bool critic;
    }

    /// <summary>What closing the day did, for the end-of-day screen.</summary>
    [Serializable]
    public class DaySummary
    {
        /// <summary>The day that just ended.</summary>
        public int day;
        /// <summary>Sales the parts of the day it was open for asked for: sold plus missed (less than a full day's if it closed early).</summary>
        public int salesPlanned;
        public int fullDaySales;
        public bool closedEarly;
        public int sold;
        public int coins;
        public List<DishSale> dishes = new();
        /// <summary>The cafe closed into the night (night's unlocked); otherwise it went straight to the next morning.</summary>
        public bool nightFalls;
        /// <summary>A tip from a lucky day (already in <see cref="coins"/>). 0 for none.</summary>
        public int tip;
        public int Missed => Math.Max(0, salesPlanned - sold);
    }
}
