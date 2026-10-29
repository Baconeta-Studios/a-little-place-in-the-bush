using System;
using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>
    /// Who's at the cafe. Story characters (<see cref="VisitType.Story"/>) are there while they give
    /// the current story quest, and move on when the story does. Random visitors drop by on some days:
    /// each morning they're rolled once (so a reload doesn't change who's in), with their chance raised
    /// by how cosy the cafe is (<see cref="CafeStyle"/>) and by having a quest of theirs under way.
    /// Anyone who gives the current story quest is there regardless.
    /// </summary>
    public static class Visitors
    {
        /// <summary>The highest chance a random visitor can have, so nobody is guaranteed.</summary>
        public const double MaxChance = 0.9;
        /// <summary>Added to a visitor's chance while a quest of theirs is under way (so you can hand it in).</summary>
        public const double QuestBoost = 0.25;

        public static bool IsPresent(DesignData design, GameState state, QuestService quests, string npcId)
        {
            NpcData npc = design.GetNpc(npcId);
            if (npc == null) return false;
            if (quests?.CurrentMainQuest()?.giver == npcId) return true;
            switch (npc.visits)
            {
                case VisitType.Always: return true;
                case VisitType.Story: return false; // only while the story is theirs (above)
                default:
                    EnsureRolled(design, state);
                    return state.visitorsToday.Contains(npcId);
            }
        }

        /// <summary>Everyone at the cafe now, in design order.</summary>
        public static List<string> Present(DesignData design, GameState state, QuestService quests) =>
            design.npcs.FindAll(n => IsPresent(design, state, quests, n.id)).ConvertAll(n => n.id);

        /// <summary>A random visitor's chance of dropping by today.</summary>
        public static double ChanceToday(DesignData design, GameState state, NpcData npc)
        {
            if (npc.visits != VisitType.Random || state.day < npc.firstDay) return 0;
            if (!string.IsNullOrEmpty(npc.afterQuest) && state.FindQuest(npc.afterQuest)?.status != QuestStatus.Completed) return 0;
            double chance = npc.visitChance * CafeStyle.VisitorBoost(design, state);
            bool questUnderWay = design.quests.Exists(q => q.giver == npc.id && state.IsQuestActive(q.id));
            if (questUnderWay) chance += QuestBoost;
            return Math.Min(MaxChance, Math.Max(0, chance));
        }

        /// <summary>Rolls today's random visitors. Called each morning; also on first use of a day.</summary>
        public static void RollToday(DesignData design, GameState state)
        {
            state.visitorsToday.Clear();
            state.visitorsDay = state.day;
            var dice = new Mulberry32(DailySeed(state, 2));
            foreach (NpcData npc in design.npcs)
            {
                if (npc.visits != VisitType.Random) continue;
                double roll = dice.NextDouble(); // always drawn, so adding one visitor doesn't reshuffle the rest
                if (roll < ChanceToday(design, state, npc)) state.visitorsToday.Add(npc.id);
            }
        }

        public static void EnsureRolled(DesignData design, GameState state)
        {
            if (state.visitorsDay != state.day) RollToday(design, state);
        }

        /// <summary>A seed for one of the day's dice (<paramref name="purpose"/> keeps them apart).</summary>
        public static uint DailySeed(GameState state, uint purpose) =>
            unchecked(state.luckSeed ^ ((uint)state.day * 2654435761u) ^ (purpose * 40503u));
    }
}
