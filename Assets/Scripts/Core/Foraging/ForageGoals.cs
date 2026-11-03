using System;

namespace Bush.Core
{
    /// <summary>An item a quest under way asks for, and how far along it is.</summary>
    public class ForageGoal
    {
        public QuestData quest;
        public ItemData item;
        public int have;
        public int needed;
        /// <summary>The focus that finds more of it, if any does.</summary>
        public FocusData focus;

        public int Still => Math.Max(0, needed - have);
        public bool IsMet => have >= needed;
    }

    /// <summary>
    /// What the forage screen knows about why you're foraging: which item the story wants next, so it can
    /// point at the right focus and show progress, and whether a quest is ready to hand in.
    /// </summary>
    public static class ForageGoals
    {
        /// <summary>
        /// The first item still wanted by the main quest, then by side quests under way. If nothing's still
        /// wanted, the last one that was met, so the results can show it finished. Null if no quest wants items.
        /// </summary>
        public static ForageGoal Current(DesignData design, QuestService quests)
        {
            ForageGoal met = null;
            QuestData main = quests.CurrentMainQuest();
            if (main != null && IsUnderWay(quests, main.id) && TryFind(design, quests, main, ref met) is { } wanted) return wanted;
            foreach (QuestData side in quests.ActiveSideQuests())
                if (TryFind(design, quests, side, ref met) is { } sideWanted) return sideWanted;
            return met;
        }

        /// <summary>A quest with everything done, waiting to be handed in: the main quest first. Null if none.</summary>
        public static QuestData ReadyToHandIn(QuestService quests)
        {
            QuestData main = quests.CurrentMainQuest();
            if (main != null && quests.GetStatus(main.id) == QuestStatus.ReadyToClaim) return main;
            return quests.ActiveSideQuests().Find(q => quests.GetStatus(q.id) == QuestStatus.ReadyToClaim);
        }

        /// <summary>"Ruru needs 3 leaves", or "Ruru needs a worm".</summary>
        public static string Describe(ForageGoal goal, string giverName)
        {
            string name = goal.item.name.ToLowerInvariant();
            if (goal.Still == 1) return $"{giverName} needs {(name.EndsWith("s") ? "" : Article(name) + " ")}{name}";
            return $"{giverName} needs {goal.Still} {(name.EndsWith("s") ? name : name + "s")}";
        }

        private static string Article(string name) => "aeiouāēīōū".IndexOf(name[0]) >= 0 ? "an" : "a";

        private static bool IsUnderWay(QuestService quests, string questId)
        {
            QuestStatus status = quests.GetStatus(questId);
            return status is QuestStatus.Active or QuestStatus.ReadyToClaim;
        }

        private static ForageGoal TryFind(DesignData design, QuestService quests, QuestData quest, ref ForageGoal met)
        {
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                QuestObjective objective = quest.objectives[i];
                if (objective.type != ObjectiveType.HaveItems && objective.type != ObjectiveType.DeliverItems) continue;
                ItemData item = design.GetItem(objective.target);
                if (item == null) continue;
                (int current, int required) = quests.GetProgress(quest.id, i);
                var goal = new ForageGoal
                {
                    quest = quest, item = item, have = current, needed = required,
                    focus = design.focuses.Find(f => f.tags.Exists(item.HasTag))
                };
                if (!goal.IsMet) return goal;
                met ??= goal;
            }
            return null;
        }
    }
}
