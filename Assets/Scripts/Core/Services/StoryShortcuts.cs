using System;

namespace Bush.Core
{
    /// <summary>
    /// For testing the story: does whatever a quest asks (adding the items it needs, cooking, counting trips,
    /// talking, placing, closing the day) and hands it in. Used by the story playthrough test and the cheat
    /// "Quests: finish the current story step". Never used in normal play.
    /// </summary>
    public static class StoryShortcuts
    {
        /// <summary>Starts (if needed), completes and hands in the quest. Returns why not, or null if it worked.</summary>
        public static string Finish(DesignData design, GameState state, Economy economy, CafeService cafe, QuestService quests,
            GameEvents events, string questId)
        {
            QuestData quest = design.GetQuest(questId);
            if (quest == null) return $"Unknown quest {questId}.";
            if (quests.GetStatus(questId) == QuestStatus.NotStarted && !quests.Accept(questId)) return $"{questId} can't start yet.";

            foreach (QuestObjective objective in quest.objectives)
            {
                int qty = Math.Max(1, objective.qty);
                switch (objective.type)
                {
                    case ObjectiveType.HaveItems:
                    case ObjectiveType.DeliverItems:
                        int have = economy.Count(objective.target);
                        if (have < qty) economy.Add(objective.target, qty - have);
                        break;
                    case ObjectiveType.CookRecipe:
                        string recipeId = string.IsNullOrEmpty(objective.target)
                            ? design.recipes.Find(r => cafe.IsUnlocked(r.id))?.id
                            : objective.target;
                        if (recipeId == null || !cafe.IsUnlocked(recipeId)) return $"{questId} asks to serve {objective.target}, which isn't known yet.";
                        int cooked = quests.GetProgress(questId, quest.objectives.IndexOf(objective)).current;
                        for (int i = cooked; i < qty; i++)
                        {
                            foreach (ItemAmount ingredient in design.GetRecipe(recipeId).ingredients) economy.Add(ingredient.itemId, ingredient.qty);
                            if (!cafe.Cook(recipeId)) return $"{questId}: couldn't cook {recipeId}.";
                        }
                        break;
                    case ObjectiveType.CompleteExpeditions:
                        for (int i = 0; i < qty; i++) events.RaiseExpeditionCompleted(new ForageResult());
                        break;
                    case ObjectiveType.TalkTo:
                        quests.BeginConversation(objective.target);
                        break;
                    case ObjectiveType.PlaceObject:
                        for (int i = 0; i < qty; i++) events.RaiseObjectPlaced(objective.target ?? "");
                        break;
                    case ObjectiveType.CloseDay:
                        for (int i = 0; i < qty; i++) events.RaiseDayEnded(new DaySummary { day = state.day }); // counts without moving the day on
                        break;
                }
            }

            return quests.Claim(questId, out string error) ? null : error;
        }
    }
}
