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
            _state.quests.Add(new QuestProgress { questId = questId, status = QuestStatus.Active });
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
                _ => GetCounter(progress, objective)
            };
            return (Math.Min(current, required), required);
        }

        /// <summary>
        /// Every objective met. Items asked for by more than one objective are totalled, so "ready"
        /// always means <see cref="Claim"/> will succeed.
        /// </summary>
        public bool AllObjectivesMet(string questId)
        {
            QuestData quest = _design.GetQuest(questId);
            if (quest == null) return false;

            var itemTotals = new Dictionary<string, int>();
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                QuestObjective objective = quest.objectives[i];
                if (objective.type is ObjectiveType.HaveItems or ObjectiveType.DeliverItems)
                {
                    itemTotals.TryGetValue(objective.target ?? "", out int total);
                    itemTotals[objective.target ?? ""] = total + Math.Max(1, objective.qty);
                    continue;
                }

                (int current, int required) = GetProgress(questId, i);
                if (current < required) return false;
            }

            foreach (KeyValuePair<string, int> total in itemTotals)
                if (_economy.Count(total.Key) < total.Value) return false;
            return true;
        }

        public bool Claim(string questId) => Claim(questId, out _);

        /// <summary>
        /// Hands in DeliverItems objectives and pays every reward in one transaction, then marks the
        /// quest completed. Does nothing if it isn't ready or was already claimed; <paramref name="error"/> says why.
        /// </summary>
        public bool Claim(string questId, out string error)
        {
            error = null;
            QuestData quest = _design.GetQuest(questId);
            QuestProgress progress = _state.FindQuest(questId);
            if (quest == null) error = $"Unknown quest '{questId}'.";
            else if (progress == null) error = "The quest hasn't been started.";
            else if (progress.rewardClaimed) error = "The reward was already claimed.";
            else if (GetStatus(questId) != QuestStatus.ReadyToClaim) error = "Not every objective is done yet.";
            if (error != null) return false;

            var transaction = new Transaction { questHandIn = true };
            foreach (QuestObjective objective in quest.objectives)
                if (objective.type == ObjectiveType.DeliverItems) transaction.Cost(objective.target, objective.qty);

            foreach (QuestReward reward in quest.rewards)
            {
                if (reward.type == RewardType.Item) transaction.Gain(reward.target, Math.Max(1, reward.qty));
                else if (reward.type == RewardType.Coins) transaction.coinGain += reward.qty;
            }

            if (!_economy.CanTransact(transaction, out error) || !_economy.TryTransact(transaction)) return false;

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

        /// <summary>Counters are keyed by objective type and target, not position, so patches can reorder objectives safely.</summary>
        public static string CounterKey(QuestObjective objective) => $"{objective.type}:{objective.target}";

        private static int GetCounter(QuestProgress progress, QuestObjective objective)
        {
            string key = CounterKey(objective);
            QuestCounter counter = progress.counters.Find(c => c.key == key);
            return counter?.count ?? 0;
        }

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

                var counted = new HashSet<string>();
                foreach (QuestObjective objective in quest.objectives)
                {
                    if (objective.type != type || !matches(objective)) continue;
                    string key = CounterKey(objective);
                    if (!counted.Add(key)) continue; // two identical objectives share one counter

                    QuestCounter counter = progress.counters.Find(c => c.key == key);
                    if (counter == null) progress.counters.Add(counter = new QuestCounter { key = key });
                    counter.count++;
                }
            }
        }

        private static void AddOnce(List<string> list, string id)
        {
            if (!string.IsNullOrEmpty(id) && !list.Contains(id)) list.Add(id);
        }
    }
}
