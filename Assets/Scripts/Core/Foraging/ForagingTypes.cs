using System;
using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>What the player picked on the expedition setup screen.</summary>
    [Serializable]
    public class ExpeditionSetup
    {
        public string tableId;
        /// <summary>Empty to go alone.</summary>
        public string companionId;
        public List<string> perkIds = new();
        /// <summary>Empty for no focus.</summary>
        public string focusId;
        public TimeOfDay time;
    }

    /// <summary>What the resolver needs to know about the player's progress.</summary>
    public interface IForageContext
    {
        bool IsQuestActive(string questId);
        /// <summary>A unique item the player has now or has handed in: it shouldn't drop again.</summary>
        bool IsUniqueCollected(string itemId);
    }

    /// <summary>A context from plain sets, for tests, tools and cheats.</summary>
    public class ForageContext : IForageContext
    {
        public readonly HashSet<string> activeQuests = new();
        /// <summary>Unique items already collected (the sandbox's "obtained").</summary>
        public readonly HashSet<string> obtained = new();

        public bool IsQuestActive(string questId) => activeQuests.Contains(questId);
        public bool IsUniqueCollected(string itemId) => obtained.Contains(itemId);
    }

    /// <summary>The context for a real game.</summary>
    public class GameStateForageContext : IForageContext
    {
        private readonly GameState _state;

        public GameStateForageContext(GameState state)
        {
            _state = state;
        }

        public bool IsQuestActive(string questId) => _state.IsQuestActive(questId);
        public bool IsUniqueCollected(string itemId) => _state.IsUniqueCollected(itemId);
    }

    /// <summary>One roll of the expedition, kept for the results screen and debugging.</summary>
    [Serializable]
    public class ForageRoll
    {
        public string itemId;
        public int qty;
        /// <summary>The chance this item had on this roll (0-1).</summary>
        public double chance;
        /// <summary>True for a bonus roll (e.g. Weka's shiny).</summary>
        public bool bonus;
    }

    [Serializable]
    public class ForageResult
    {
        public uint seed;
        public int rollCount;
        public List<ForageRoll> rolls = new();
        /// <summary>Rolls merged by item, in the order they were first found.</summary>
        public List<ItemStack> stacks = new();
        /// <summary>Rare or Legendary items found, to call out on the results screen.</summary>
        public List<string> rareFinds = new();

        public int TotalItems()
        {
            int total = 0;
            foreach (ItemStack stack in stacks) total += stack.qty;
            return total;
        }
    }

    /// <summary>Odds for one item, for the "what could I find?" part of the setup screen.</summary>
    public class ForageOdds
    {
        public string itemId;
        public double weight;
        public double chancePerRoll;
        public double chanceAtLeastOne;
        public double expectedQty;
    }

    public class ForagePreview
    {
        public int rolls;
        public List<ForageOdds> odds = new();
    }

    /// <summary>What claiming an expedition did.</summary>
    public class ExpeditionClaim
    {
        public bool paid;
        public ForageResult result;
        /// <summary>Perk learned from the companion on this trip, or null.</summary>
        public string learnedPerkId;
    }
}
