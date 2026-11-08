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
        /// The first forageable item still wanted by the main quest, then by side quests under way. Items that come
        /// from somewhere else (a scrap someone's holding on to) aren't goals here, because foraging won't find them.
        /// If nothing's still wanted, the first item of a quest that's ready to hand in, so the results can show it
        /// finished. Null if there's nothing to show. Given a time of day, only items found then count (no "needs
        /// crickets" in the day, when crickets only come out at night).
        /// </summary>
        public static ForageGoal Current(DesignData design, QuestService quests, TimeOfDay? now = null)
        {
            ForageGoal met = null;
            QuestData main = quests.CurrentMainQuest();
            if (main != null && IsUnderWay(quests, main.id) && TryFind(design, quests, main, now, ref met) is { } wanted) return wanted;
            foreach (QuestData side in quests.ActiveSideQuests())
                if (TryFind(design, quests, side, now, ref met) is { } sideWanted) return sideWanted;
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

        private static ForageGoal TryFind(DesignData design, QuestService quests, QuestData quest, TimeOfDay? now, ref ForageGoal met)
        {
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                QuestObjective objective = quest.objectives[i];
                if (objective.type is not (ObjectiveType.HaveItems or ObjectiveType.DeliverItems or ObjectiveType.FindItems)) continue;
                ItemData item = design.GetItem(objective.target);
                if (item == null || !CanBeForaged(design, item, now)) continue;
                (int current, int required) = quests.GetProgress(quest.id, i);
                var goal = new ForageGoal
                {
                    quest = quest, item = item, have = current, needed = required,
                    focus = design.focuses.Find(f => f.tags.Exists(item.HasTag))
                };
                if (!goal.IsMet) return goal;
                if (quests.GetStatus(quest.id) == QuestStatus.ReadyToClaim) met ??= goal;
            }
            return null;
        }

        private static bool CanBeForaged(DesignData design, ItemData item, TimeOfDay? now) =>
            design.forageTables.Exists(table => table.entries.Exists(entry => entry.itemId == item.id && FoundAt(entry.time, now)));

        private static bool FoundAt(EntryTime time, TimeOfDay? now) => now == null || time switch
        {
            EntryTime.Day => now == TimeOfDay.Day,
            EntryTime.Night => now == TimeOfDay.Night,
            _ => true
        };
    }
}
