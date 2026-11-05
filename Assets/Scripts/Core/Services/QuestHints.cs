namespace Bush.Core
{
    /// <summary>Where an item comes from: foraging, or a character who gives it as a quest reward.</summary>
    public struct ItemSource
    {
        public bool foraged;
        public string fromNpc;
    }

    /// <summary>Tells the player where to look for something a quest asks for, worked out from the design data.</summary>
    public static class QuestHints
    {
        public static ItemSource WhereToGet(DesignData design, string itemId)
        {
            var source = new ItemSource
            {
                foraged = design.forageTables.Exists(table => table.entries.Exists(entry => entry.itemId == itemId))
            };
            foreach (QuestData quest in design.quests)
            {
                if (!quest.rewards.Exists(r => r.type == RewardType.Item && r.target == itemId)) continue;
                source.fromNpc = quest.giver;
                break;
            }
            return source;
        }
    }
}
