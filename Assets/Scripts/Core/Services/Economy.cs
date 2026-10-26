using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>A set of costs and gains that is applied all at once or not at all.</summary>
    public class Transaction
    {
        public readonly List<ItemAmount> costs = new();
        public readonly List<ItemAmount> gains = new();
        public int coinCost;
        public int coinGain;
        /// <summary>
        /// Set only by quest claims. Key items (recipe scraps...) can't be spent, sold or cooked any other
        /// way, so a quest item can never be lost before it's handed in. Handed-in uniques are remembered.
        /// </summary>
        public bool questHandIn;

        public Transaction Cost(string itemId, int qty) { costs.Add(new ItemAmount(itemId, qty)); return this; }
        public Transaction Gain(string itemId, int qty) { gains.Add(new ItemAmount(itemId, qty)); return this; }
    }

    /// <summary>
    /// The only code that changes the inventory or currency. Cooking, quest turn-ins, foraging
    /// rewards and buying all go through here, so the rules (no negative amounts, unique items,
    /// all-or-nothing transactions) live in one place.
    /// </summary>
    public class Economy
    {
        private readonly GameState _state;
        private readonly DesignData _design;
        private readonly GameEvents _events;

        public Economy(GameState state, DesignData design, GameEvents events)
        {
            _state = state;
            _design = design;
            _events = events;
        }

        public int Currency => _state.currency;
        public IReadOnlyList<ItemStack> Inventory => _state.inventory;

        public int Count(string itemId)
        {
            ItemStack stack = Find(itemId);
            return stack?.qty ?? 0;
        }

        public bool Has(string itemId, int qty = 1) => qty > 0 && Count(itemId) >= qty;

        /// <summary>Has it now or handed it in (unique items only make sense here).</summary>
        public bool IsUniqueCollected(string itemId) => _state.IsUniqueCollected(itemId);

        /// <summary>Adds items. Fails on unknown items or qty &lt;= 0. Unique items are capped at one and never come back once handed in.</summary>
        public bool Add(string itemId, int qty)
        {
            return TryTransact(new Transaction().Gain(itemId, qty));
        }

        public bool TryRemove(string itemId, int qty)
        {
            return TryTransact(new Transaction().Cost(itemId, qty));
        }

        public bool AddCurrency(int amount)
        {
            if (amount <= 0) return false;
            return TryTransact(new Transaction { coinGain = amount });
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0) return false;
            return TryTransact(new Transaction { coinCost = amount });
        }

        /// <summary>Sells items for their sell value. Key items, unique items and items worth nothing can't be sold.</summary>
        public bool TrySell(string itemId, int qty)
        {
            ItemData item = _design.GetItem(itemId);
            if (item == null || item.unique || item.category == ItemCategory.KeyItem || item.sellValue <= 0 || qty <= 0) return false;
            return TryTransact(new Transaction { coinGain = item.sellValue * qty }.Cost(itemId, qty));
        }

        /// <summary>Checks the whole transaction first, then applies all of it. Never applies half.</summary>
        public bool TryTransact(Transaction transaction)
        {
            if (!CanTransact(transaction, out _)) return false;

            bool itemsChanged = false;
            foreach (ItemAmount cost in transaction.costs)
            {
                ItemStack stack = Find(cost.itemId);
                stack.qty -= cost.qty;
                if (stack.qty <= 0) _state.inventory.Remove(stack);
                if (_design.GetItem(cost.itemId).unique && !_state.handedIn.Contains(cost.itemId)) _state.handedIn.Add(cost.itemId);
                itemsChanged = true;
            }

            foreach (ItemAmount gain in transaction.gains)
            {
                ItemData item = _design.GetItem(gain.itemId);
                int qty = gain.qty;
                if (item.unique)
                {
                    // Capped at one, and never given again once handed in; a duplicate gain is ignored.
                    if (_state.IsUniqueCollected(gain.itemId)) continue;
                    qty = 1;
                }

                ItemStack stack = Find(gain.itemId);
                if (stack == null) _state.inventory.Add(new ItemStack(gain.itemId, qty));
                else stack.qty += qty;
                itemsChanged = true;
            }

            int coinDelta = transaction.coinGain - transaction.coinCost;
            _state.currency += coinDelta;

            if (itemsChanged) _events?.RaiseInventoryChanged();
            if (coinDelta != 0) _events?.RaiseCurrencyChanged(_state.currency);
            return true;
        }

        /// <summary>Same checks as <see cref="TryTransact"/> without changing anything. <paramref name="error"/> says why not.</summary>
        public bool CanTransact(Transaction transaction, out string error)
        {
            error = null;
            if (transaction.coinCost < 0 || transaction.coinGain < 0)
            {
                error = "Coin amounts can't be negative.";
                return false;
            }

            // Total each cost per item first, so two costs of the same item can't both pass on one stack.
            var totals = new Dictionary<string, int>();
            foreach (ItemAmount cost in transaction.costs)
            {
                if (!IsValid(cost, out error)) return false;
                if (!transaction.questHandIn && _design.GetItem(cost.itemId).category == ItemCategory.KeyItem)
                {
                    error = $"{cost.itemId} is a key item: it can only be handed in for a quest.";
                    return false;
                }
                totals.TryGetValue(cost.itemId, out int total);
                totals[cost.itemId] = total + cost.qty;
            }

            foreach (KeyValuePair<string, int> total in totals)
            {
                if (Count(total.Key) < total.Value)
                {
                    error = $"Not enough {total.Key}: need {total.Value}, have {Count(total.Key)}.";
                    return false;
                }
            }

            foreach (ItemAmount gain in transaction.gains)
            {
                if (!IsValid(gain, out error)) return false;
            }

            if (_state.currency < transaction.coinCost)
            {
                error = $"Not enough coins: need {transaction.coinCost}, have {_state.currency}.";
                return false;
            }

            return true;
        }

        private bool IsValid(ItemAmount amount, out string error)
        {
            error = null;
            if (amount.qty <= 0)
            {
                error = $"Quantity for {amount.itemId} must be at least 1 (was {amount.qty}).";
                return false;
            }

            if (_design.GetItem(amount.itemId) == null)
            {
                error = $"Unknown item '{amount.itemId}'.";
                return false;
            }

            return true;
        }

        private ItemStack Find(string itemId) => _state.inventory.Find(s => s.itemId == itemId);
    }
}
