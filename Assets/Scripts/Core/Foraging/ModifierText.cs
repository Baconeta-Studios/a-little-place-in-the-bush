using System;
using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>What companions and perks do, in plain words, for the forage screen.</summary>
    public static class ModifierText
    {
        public static string Describe(ForageModifier modifier)
        {
            switch (modifier.type)
            {
                case ModifierType.TagWeight:
                    return modifier.value >= 1 ? $"more {modifier.tag} (x{Number(modifier.value)})" : $"less {modifier.tag}";
                case ModifierType.TagQuantity:
                    return $"+{Math.Floor(modifier.value)} {modifier.tag} each find";
                case ModifierType.RarityWeight:
                    return $"{modifier.rarity.ToString().ToLowerInvariant()} finds more likely (x{Number(modifier.value)})";
                case ModifierType.ExtraRolls:
                    int extra = (int)Math.Floor(modifier.value);
                    return $"+{extra} find{(extra == 1 ? "" : "s")} per trip";
                case ModifierType.BonusRoll:
                    return $"{Math.Round(Math.Min(1, modifier.value) * 100)}% chance of a bonus {modifier.tag} find";
                default:
                    return modifier.type.ToString();
            }
        }

        /// <summary>"more bark (x3), +1 bark each find", or "nothing special" for none.</summary>
        public static string Describe(IEnumerable<ForageModifier> modifiers)
        {
            var parts = new List<string>();
            foreach (ForageModifier modifier in modifiers) parts.Add(Describe(modifier));
            return parts.Count == 0 ? "nothing special" : string.Join(", ", parts);
        }

        private static string Number(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
