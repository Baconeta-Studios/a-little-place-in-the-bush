using System;

namespace Bush.Core
{
    /// <summary>
    /// The handful of things that happened which other systems (mostly quests) care about.
    /// One instance per game session, so tests don't share static state.
    /// </summary>
    public class GameEvents
    {
        public event Action InventoryChanged;
        public event Action<int> CurrencyChanged;
        public event Action<string> RecipeCooked;
        public event Action<ForageResult> ExpeditionCompleted;
        public event Action<string> TalkedTo;
        public event Action<string> ObjectPlaced;
        public event Action<string> QuestStarted;
        public event Action<string> QuestCompleted;
        public event Action<string> PerkLearned;

        public void RaiseInventoryChanged() => InventoryChanged?.Invoke();
        public void RaiseCurrencyChanged(int currency) => CurrencyChanged?.Invoke(currency);
        public void RaiseRecipeCooked(string recipeId) => RecipeCooked?.Invoke(recipeId);
        public void RaiseExpeditionCompleted(ForageResult result) => ExpeditionCompleted?.Invoke(result);
        public void RaiseTalkedTo(string npcId) => TalkedTo?.Invoke(npcId);
        public void RaiseObjectPlaced(string placeableItemId) => ObjectPlaced?.Invoke(placeableItemId);
        public void RaiseQuestStarted(string questId) => QuestStarted?.Invoke(questId);
        public void RaiseQuestCompleted(string questId) => QuestCompleted?.Invoke(questId);
        public void RaisePerkLearned(string perkId) => PerkLearned?.Invoke(perkId);
    }
}
