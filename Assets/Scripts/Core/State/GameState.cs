using System;
using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>
    /// Everything that gets saved. Lists rather than dictionaries so Unity's JsonUtility can
    /// serialise it. Only the services change it: never edit it from UI code.
    /// </summary>
    [Serializable]
    public class GameState
    {
        public const int CurrentSaveVersion = 1;

        public int saveVersion = CurrentSaveVersion;
        public int currency;
        public List<ItemStack> inventory = new();
        /// <summary>Unique items (like recipe scraps) the player has ever had, so they don't drop again after being handed in.</summary>
        public List<string> everObtained = new();

        public List<string> unlockedRecipes = new();
        public List<string> unlockedPerks = new();
        public List<string> unlockedCompanions = new();
        public List<CompanionProgress> companions = new();
        public List<QuestProgress> quests = new();
        public List<PlacedObjectData> placed = new();

        public int expeditionsStarted;
        public int expeditionsCompleted;
        /// <summary>Set when the player presses Go, cleared when the rewards are claimed. Survives a refresh mid-expedition.</summary>
        public bool hasPendingExpedition;
        public PendingExpedition pendingExpedition = new();

        public int day = 1;
        /// <summary>0-1 through the current day.</summary>
        public float timeOfDay;

        /// <summary>A fresh game: default unlocks from the design data, nothing else.</summary>
        public static GameState CreateNew(DesignData design)
        {
            var state = new GameState();
            foreach (RecipeData recipe in design.recipes)
                if (recipe.unlockedByDefault) state.unlockedRecipes.Add(recipe.id);
            foreach (PerkData perk in design.perks)
                if (perk.unlockedByDefault) state.unlockedPerks.Add(perk.id);
            foreach (CompanionData companion in design.companions)
                if (companion.unlockedByDefault) state.unlockedCompanions.Add(companion.id);
            return state;
        }

        public QuestProgress FindQuest(string questId) => quests.Find(q => q.questId == questId);

        public CompanionProgress GetOrAddCompanion(string companionId)
        {
            CompanionProgress progress = companions.Find(c => c.companionId == companionId);
            if (progress == null)
            {
                progress = new CompanionProgress { companionId = companionId };
                companions.Add(progress);
            }
            return progress;
        }

        public bool IsQuestActive(string questId) => FindQuest(questId)?.status == QuestStatus.Active;
    }

    [Serializable]
    public class ItemStack
    {
        public string itemId;
        public int qty;

        public ItemStack() { }

        public ItemStack(string itemId, int qty)
        {
            this.itemId = itemId;
            this.qty = qty;
        }

        public override string ToString() => $"{qty} x {itemId}";
    }

    [Serializable]
    public class CompanionProgress
    {
        public string companionId;
        public int trips;
    }

    [Serializable]
    public class QuestProgress
    {
        public string questId;
        /// <summary>NotStarted, Active or Completed. ReadyToClaim is never stored.</summary>
        public QuestStatus status;
        /// <summary>Counters for event objectives (cook, expeditions, talk, place), one per objective.</summary>
        public List<int> counters = new();
        public bool rewardClaimed;
    }

    [Serializable]
    public class PlacedObjectData
    {
        public string placeableId;
        public float x;
        public float y;
        public bool flipped;
    }

    [Serializable]
    public class PendingExpedition
    {
        public int id;
        public uint seed;
        public ExpeditionSetup setup = new();
    }
}
