using System;
using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>
    /// How cosy the cafe is, and what kind of cafe it looks like, from what's placed in it. Cosiness is
    /// each placed thing's <see cref="PlaceableData.cosiness"/> plus a little for variety; it raises the
    /// chance of random visitors and of lucky days. The style comes from the placed things' styles, for
    /// the sign by the door. All placeholder numbers and names, to tweak.
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
            return score + kinds.Count; // a bit extra for each different kind of thing
        }

        /// <summary>"Bare", "Getting there", "Cosy", "Very cosy" or "The cosiest spot in the bush".</summary>
        public static string CosinessLevel(int score) =>
            score < 8 ? "Bare" : score < 20 ? "Getting there" : score < 40 ? "Cosy" : score < 70 ? "Very cosy" : "The cosiest spot in the bush";

        /// <summary>Random visitors' chances are multiplied by this: 1 for a bare cafe, up to 2.</summary>
        public static double VisitorBoost(DesignData design, GameState state) => 1 + Math.Min(Cosiness(design, state), 60) / 60.0;

        /// <summary>The chance a day ends with a happy guest's tip: up to 40%.</summary>
        public static double LuckyDayChance(DesignData design, GameState state) => Math.Min(0.4, Cosiness(design, state) / 125.0);

        /// <summary>What the sign by the door says.</summary>
        public static CafeDescription Describe(DesignData design, GameState state)
        {
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
            var description = new CafeDescription { cosiness = score, level = CosinessLevel(score), things = things, seats = seats };
            if (things == 0)
            {
                description.title = "An empty clearing";
                description.blurb = "Just you, some dirt and a lot of potential.";
                return description;
            }

            // The biggest style wins, unless it's close: then it's a mix.
            string top = null, second = null;
            foreach (KeyValuePair<string, int> style in styles)
            {
                if (top == null || style.Value > styles[top]) { second = top; top = style.Key; }
                else if (second == null || style.Value > styles[second]) second = style.Key;
            }
            bool mixed = second != null && styles[second] * 4 >= styles[top] * 3;

            (description.title, description.blurb) = mixed ? ("An eclectic little cafe", "A bit of everything, and somehow it works.") : top switch
            {
                "garden" => ("A garden cafe", "More leaves than tables. The bugs love it."),
                "rustic" => ("A rustic bush cafe", "Sturdy wood, honest food, and a bench for everyone."),
                "glow" => ("A lantern-lit cafe", "It glows at dusk. Moths are already queueing."),
                "fancy" => ("A rather fancy cafe", "Is that a doily? In the bush?"),
                _ => ("A simple little cafe", "Nothing fancy, but the salad's fresh.")
            };
            if (seats == 0) description.blurb += " Nowhere to sit, though.";
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
