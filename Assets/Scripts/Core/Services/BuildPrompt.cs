namespace Bush.Core
{
    /// <summary>
    /// Whether Build is the next thing to do: a quest under way wants something placed, and you have something it
    /// would count. The HUD shows a ! on its Build button while this is true.
    /// </summary>
    public static class BuildPrompt
    {
        public static bool IsWanted(DesignData design, QuestService quests, BuildService build)
        {
            QuestData main = quests.CurrentMainQuest();
            if (main != null && quests.GetStatus(main.id) == QuestStatus.Active && NeedsPlacing(design, quests, build, main)) return true;
            foreach (QuestData side in quests.ActiveSideQuests())
                if (NeedsPlacing(design, quests, build, side)) return true;
            return false;
        }

        private static bool NeedsPlacing(DesignData design, QuestService quests, BuildService build, QuestData quest)
        {
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                QuestObjective objective = quest.objectives[i];
                if (objective.type != ObjectiveType.PlaceObject) continue;
                (int current, int required) = quests.GetProgress(quest.id, i);
                if (current < required && HasSomethingFor(design, build, objective)) return true;
            }
            return false;
        }

        private static bool HasSomethingFor(DesignData design, BuildService build, QuestObjective objective)
        {
            foreach (PlaceableData placeable in design.placeables)
            {
                bool counts = string.IsNullOrEmpty(objective.target) || objective.target == placeable.itemId;
                if (counts && build.CanPlace(placeable.id)) return true;
            }
            return false;
        }
    }
}
