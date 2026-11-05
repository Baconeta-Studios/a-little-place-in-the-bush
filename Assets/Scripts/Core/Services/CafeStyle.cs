using System;
using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>
    /// How cosy the cafe is, and what kind of cafe it looks like, from what's placed in it. Cosiness is
    /// each placed thing's <see cref="PlaceableData.cosiness"/> plus a little for variety; it raises the
    /// chance of random visitors and of lucky days. The style comes from the placed things' styles, for
    /// the sign by the door. The numbers and wording are <see cref="CafeSignSettings"/>, on the GameDatabase.
    /// </summary>
    public static class CafeStyle
    {
        public const string CafeArea = "Cafe";

        public static int Cosiness(DesignData design, GameState state)
        {
            int score = 0;
            var kinds = new HashSet<string>();
            foreach (PlacedObjectData placed in state.placed)
            {
                if (placed.area != CafeArea) continue;
                PlaceableData placeable = design.GetPlaceable(placed.placeableId);
                if (placeable == null) continue;
                score += Math.Max(0, placeable.cosiness);
                kinds.Add(placeable.id);
            }
            return score + kinds.Count * design.rules.cafeSign.cosinessPerKind; // a bit extra for each different kind of thing
        }

        public static int CosinessSteps(DesignData design) => design.rules.cafeSign.levels.Count;

        /// <summary>Which level this score is at, from 0.</summary>
        public static int CosinessStep(DesignData design, int score)
        {
            int reached = 0;
            foreach (CosinessLevelData level in design.rules.cafeSign.levels)
                if (score >= level.from) reached++;
            return Math.Max(0, reached - 1);
        }

        public static string CosinessLevel(DesignData design, int score)
        {
            CosinessLevelData best = null;
            foreach (CosinessLevelData level in design.rules.cafeSign.levels)
                if (score >= level.from && (best == null || level.from > best.from)) best = level;
            return best?.name ?? "";
        }

        /// <summary>The level after this score's, and how much more it takes. False at the top.</summary>
        public static bool TryNextLevel(DesignData design, int score, out string name, out int more)
        {
            CosinessLevelData next = null;
            foreach (CosinessLevelData level in design.rules.cafeSign.levels)
                if (level.from > score && (next == null || level.from < next.from)) next = level;
            name = next?.name;
            more = next != null ? next.from - score : 0;
            return next != null;
        }

        /// <summary>Random visitors' chances are multiplied by this: 1 for a bare cafe, more as it gets cosier.</summary>
        public static double VisitorBoost(DesignData design, GameState state)
        {
            CafeSignSettings sign = design.rules.cafeSign;
            return 1 + sign.extraVisitorsAtFull * ShareOfFull(Cosiness(design, state), sign.cosinessForFullVisitors);
        }

        /// <summary>The chance a day ends with a happy guest's tip.</summary>
        public static double LuckyDayChance(DesignData design, GameState state)
        {
            CafeSignSettings sign = design.rules.cafeSign;
            return sign.luckyDayChanceAtFull * ShareOfFull(Cosiness(design, state), sign.cosinessForFullLuck);
        }

        private static double ShareOfFull(int cosiness, int full) => Math.Min(cosiness, Math.Max(1, full)) / (double)Math.Max(1, full);

        /// <summary>What the sign by the door says.</summary>
        public static CafeDescription Describe(DesignData design, GameState state)
        {
            CafeSignSettings sign = design.rules.cafeSign;
            var styles = new Dictionary<string, int>();
            int things = 0, seats = 0;
            foreach (PlacedObjectData placed in state.placed)
            {
                if (placed.area != CafeArea) continue;
                PlaceableData placeable = design.GetPlaceable(placed.placeableId);
                if (placeable == null) continue;
                things++;
                seats += placeable.seats;
                string style = string.IsNullOrEmpty(placeable.style) ? "plain" : placeable.style;
                styles.TryGetValue(style, out int count);
                styles[style] = count + Math.Max(1, placeable.cosiness);
            }

            int score = Cosiness(design, state);
            var description = new CafeDescription { cosiness = score, level = CosinessLevel(design, score), things = things, seats = seats };
            if (things == 0)
            {
                description.title = sign.emptyTitle;
                description.blurb = sign.emptyBlurb;
                return description;
            }

            // The biggest style wins, unless it's close: then it's a mix.
            string top = null, second = null;
            foreach (KeyValuePair<string, int> style in styles)
            {
                if (top == null || style.Value > styles[top]) { second = top; top = style.Key; }
                else if (second == null || style.Value > styles[second]) second = style.Key;
            }
            bool mixed = second != null && styles[second] >= styles[top] * sign.mixedAt;

            CafeStyleWording wording = sign.styles.Find(w => w.style == top);
            description.title = mixed ? sign.mixedTitle : wording?.title ?? sign.otherTitle;
            description.blurb = mixed ? sign.mixedBlurb : wording?.blurb ?? sign.otherBlurb;
            if (seats == 0 && !string.IsNullOrEmpty(sign.noSeatsLine)) description.blurb += " " + sign.noSeatsLine;
            return description;
        }
    }

    public class CafeDescription
    {
        public string title;
        public string blurb;
        public int cosiness;
        public string level;
        public int things;
        public int seats;
    }
}
