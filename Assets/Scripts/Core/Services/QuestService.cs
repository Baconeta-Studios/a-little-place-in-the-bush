using System;
using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>
    /// Quest states and objectives. Item objectives are checked against the inventory when asked;
    /// cooking, expeditions, talking and placing are counted from <see cref="GameEvents"/> while a
    /// quest is active. <see cref="Claim"/> pays out once and only once.
    /// </summary>
    public class QuestService : IDisposable
    {
        private readonly DesignData _design;
        private readonly GameState _state;
        private readonly Economy _economy;
        private readonly GameEvents _events;

        public QuestService(DesignData design, GameState state, Economy economy, GameEvents events)
        {
            _design = design;
            _state = state;
            _economy = economy;
            _events = events;

            _events.RecipeCooked += OnRecipeCooked;
            _events.ExpeditionCompleted += OnExpeditionCompleted;
            _events.TalkedTo += OnTalkedTo;
            _events.ObjectPlaced += OnObjectPlaced;
        }

        public void Dispose()
        {
            _events.RecipeCooked -= OnRecipeCooked;
            _events.ExpeditionCompleted -= OnExpeditionCompleted;
            _events.TalkedTo -= OnTalkedTo;
            _events.ObjectPlaced -= OnObjectPlaced;
        }

        public QuestStatus GetStatus(string questId)
        {
            QuestProgress progress = _state.FindQuest(questId);
            if (progress == null) return QuestStatus.NotStarted;
            if (progress.status == QuestStatus.Active && AllObjectivesMet(questId)) return QuestStatus.ReadyToClaim;
            return progress.status;
        }

        /// <summary>Not started yet, and every prerequisite quest is completed.</summary>
        public bool CanStart(string questId)
        {
            QuestData quest = _design.GetQuest(questId);
            if (quest == null || GetStatus(questId) != QuestStatus.NotStarted) return false;
            foreach (string prerequisite in quest.prerequisites)
                if (GetStatus(prerequisite) != QuestStatus.Completed) return false;
            return true;
        }

        public bool Start(string questId)
        {
            if (!CanStart(questId)) return false;

            QuestData quest = _design.GetQuest(questId);
            var progress = new QuestProgress { questId = questId, status = QuestStatus.Active };
            for (int i = 0; i < quest.objectives.Count; i++) progress.counters.Add(0);
            _state.quests.Add(progress);
            _events.RaiseQuestStarted(questId);
            return true;
        }

        /// <summary>Progress on one objective, for the quest log and dialogue.</summary>
        public (int current, int required) GetProgress(string questId, int objectiveIndex)
        {
            QuestData quest = _design.GetQuest(questId);
            QuestProgress progress = _state.FindQuest(questId);
            if (quest == null || objectiveIndex < 0 || objectiveIndex >= quest.objectives.Count) return (0, 0);

            QuestObjective objective = quest.objectives[objectiveIndex];
            int required = Math.Max(1, objective.qty);
            if (progress == null) return (0, required);
            if (progress.status == QuestStatus.Completed) return (required, required);

            int current = objective.type switch
            {
                ObjectiveType.HaveItems or ObjectiveType.DeliverItems => _economy.Count(objective.target),
                _ => objectiveIndex < progress.counters.Count ? progress.counters[objectiveIndex] : 0
            };
            return (Math.Min(current, required), required);
        }

        public bool AllObjectivesMet(string questId)
        {
            QuestData quest = _design.GetQuest(questId);
            if (quest == null) return false;
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                (int current, int required) = GetProgress(questId, i);
                if (current < required) return false;
            }
            return true;
        }

        /// <summary>
        /// Hands in DeliverItems objectives and pays every reward in one transaction, then marks
        /// the quest completed. Does nothing if it isn't ready, or was already claimed.
        /// </summary>
        public bool Claim(string questId)
        {
            QuestData quest = _design.GetQuest(questId);
            QuestProgress progress = _state.FindQuest(questId);
            if (quest == null || progress == null || progress.rewardClaimed) return false;
            if (GetStatus(questId) != QuestStatus.ReadyToClaim) return false;

            var transaction = new Transaction();
            foreach (QuestObjective objective in quest.objectives)
                if (objective.type == ObjectiveType.DeliverItems) transaction.Cost(objective.target, objective.qty);

            foreach (QuestReward reward in quest.rewards)
            {
                if (reward.type == RewardType.Item) transaction.Gain(reward.target, Math.Max(1, reward.qty));
                else if (reward.type == RewardType.Coins) transaction.coinGain += reward.qty;
            }

            if (!_economy.TryTransact(transaction)) return false;

            foreach (QuestReward reward in quest.rewards)
            {
                switch (reward.type)
                {
                    case RewardType.UnlockRecipe: AddOnce(_state.unlockedRecipes, reward.target); break;
                    case RewardType.UnlockCompanion: AddOnce(_state.unlockedCompanions, reward.target); break;
                    case RewardType.UnlockPerk: AddOnce(_state.unlockedPerks, reward.target); break;
                }
            }

            progress.status = QuestStatus.Completed;
            progress.rewardClaimed = true;
            _events.RaiseQuestCompleted(questId);
            return true;
        }

        /// <summary>True once every main quest is completed: the GDD's win condition.</summary>
        public bool IsMainStoryComplete()
        {
            bool anyMain = false;
            foreach (QuestData quest in _design.quests)
            {
                if (!quest.isMain) continue;
                anyMain = true;
                if (GetStatus(quest.id) != QuestStatus.Completed) return false;
            }
            return anyMain;
        }

        /// <summary>The dialogue line for this quest's current state.</summary>
        public string GetDialogue(string questId)
        {
            QuestData quest = _design.GetQuest(questId);
            if (quest?.dialogue == null) return null;
            return GetStatus(questId) switch
            {
                QuestStatus.NotStarted => quest.dialogue.offer,
                QuestStatus.Active => quest.dialogue.active,
                QuestStatus.ReadyToClaim => quest.dialogue.ready,
                _ => quest.dialogue.done
            };
        }

        /// <summary>Quests this NPC gives, in design order.</summary>
        public List<QuestData> GetQuestsFrom(string npcId) => _design.quests.FindAll(q => q.giver == npcId);

        private void OnRecipeCooked(string recipeId) =>
            Count(ObjectiveType.CookRecipe, o => string.IsNullOrEmpty(o.target) || o.target == recipeId);

        private void OnExpeditionCompleted(ForageResult _) => Count(ObjectiveType.CompleteExpeditions, _ => true);

        private void OnTalkedTo(string npcId) => Count(ObjectiveType.TalkTo, o => o.target == npcId);

        private void OnObjectPlaced(string itemId) =>
            Count(ObjectiveType.PlaceObject, o => string.IsNullOrEmpty(o.target) || o.target == itemId);

        private void Count(ObjectiveType type, Func<QuestObjective, bool> matches)
        {
            foreach (QuestProgress progress in _state.quests)
            {
                if (progress.status != QuestStatus.Active) continue;
                QuestData quest = _design.GetQuest(progress.questId);
                if (quest == null) continue;

                for (int i = 0; i < quest.objectives.Count; i++)
                {
                    QuestObjective objective = quest.objectives[i];
                    if (objective.type != type || !matches(objective)) continue;
                    while (progress.counters.Count <= i) progress.counters.Add(0);
                    progress.counters[i]++;
                }
            }
        }

        private static void AddOnce(List<string> list, string id)
        {
            if (!string.IsNullOrEmpty(id) && !list.Contains(id)) list.Add(id);
        }
    }
}
