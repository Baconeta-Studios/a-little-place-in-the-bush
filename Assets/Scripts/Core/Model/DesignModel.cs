using System;
using System.Collections.Generic;

namespace Bush.Core
{
    // Plain C# versions of the game's design data. The ScriptableObject definitions in Unity and
    // Design/design-data.json both convert to these, so the rules can be tested without Unity.

    [Serializable]
    public struct ItemAmount
    {
        public string itemId;
        public int qty;

        public ItemAmount(string itemId, int qty)
        {
            this.itemId = itemId;
            this.qty = qty;
        }

        public override string ToString() => $"{qty} x {itemId}";
    }

    [Serializable]
    public class ItemData
    {
        public string id;
        public string name;
        public ItemCategory category;
        public Rarity rarity;
        public List<string> tags = new();
        public int sellValue;
        public bool unique;
        public string description;
        /// <summary>A rare encounter or note: getting it teaches this recipe on the spot. Empty for normal items.</summary>
        public string teachesRecipe;

        public bool HasTag(string tag) => !string.IsNullOrEmpty(tag) && tags != null && tags.Contains(tag);
    }

    /// <summary>
    /// Tuning for the cafe day. The forage trips are the day's clock: each trip is one part of the
    /// day (morning, midday, afternoon), and when they're used up it's evening. The cafe sells its
    /// share of the day's dishes for each part it was open, rounded down, so closing early costs sales.
    /// </summary>
    [Serializable]
    public class DayRules
    {
        /// <summary>Dishes the cafe sells in a full day, shared across the menu.</summary>
        public int salesPerDay = 3;
        /// <summary>How many dishes can be on the menu at once.</summary>
        public int menuSlots = 3;
        /// <summary>Forage trips per day, which are also the parts of the day. 0 means no limit, and no day clock.</summary>
        public int forageTripsPerDay;

        /// <summary>Dishes in a full day. One place to add progression later (story steps, upgrades, seats).</summary>
        public int FullDaySales(GameState state) => Math.Max(0, salesPerDay);

        /// <summary>Parts of the day so far (trips taken), up to <see cref="forageTripsPerDay"/>.</summary>
        public int PartsPassed(GameState state) => forageTripsPerDay <= 0 ? 0 : Math.Min(state.forageTripsToday, forageTripsPerDay);

        /// <summary>Every trip taken: it's evening and closing now isn't early. Always true with no day clock.</summary>
        public bool IsEvening(GameState state) => forageTripsPerDay <= 0 || state.forageTripsToday >= forageTripsPerDay;

        /// <summary>What the cafe sells if it closes now: its share of the day so far, rounded down.</summary>
        public int SalesFor(GameState state) =>
            forageTripsPerDay <= 0 ? FullDaySales(state) : FullDaySales(state) * PartsPassed(state) / forageTripsPerDay;
    }

    [Serializable]
    public class RecipeData
    {
        public string id;
        public string name;
        public List<ItemAmount> ingredients = new();
        public int coins;
        public bool unlockedByDefault;
        /// <summary>Coins to learn it at the cafe. 0 = can't be bought (default or quest unlock only).</summary>
        public int price;
        /// <summary>Key items that piece this recipe together (shown as progress in the recipe book).</summary>
        public List<string> scraps = new();
        public string description;
    }

    [Serializable]
    public class ForageModifier
    {
        public ModifierType type;
        public string tag;
        public Rarity rarity;
        public double value;

        public ForageModifier() { }

        public ForageModifier(ModifierType type, double value, string tag = "", Rarity rarity = Rarity.Common)
        {
            this.type = type;
            this.value = value;
            this.tag = tag;
            this.rarity = rarity;
        }

        /// <summary>Player-facing description, e.g. for the expedition setup screen.</summary>
        public override string ToString() => type switch
        {
            ModifierType.TagWeight => $"{tag} chance x{value}",
            ModifierType.TagQuantity => $"+{Math.Floor(value)} {tag} per find",
            ModifierType.RarityWeight => $"{rarity} chance x{value}",
            ModifierType.ExtraRolls => $"+{Math.Floor(value)} finds per trip",
            ModifierType.BonusRoll => $"{Math.Round(value * 100)}% chance of a bonus {tag}",
            _ => type.ToString()
        };
    }

    [Serializable]
    public class CompanionData
    {
        public string id;
        public string name;
        public string description;
        public bool unlockedByDefault;
        public List<ForageModifier> modifiers = new();
        /// <summary>Perk the player learns after <see cref="tripsToTeach"/> trips with this companion. Empty for none.</summary>
        public string teachesPerk;
        public int tripsToTeach;
    }

    [Serializable]
    public class PerkData
    {
        public string id;
        public string name;
        public string description;
        public bool unlockedByDefault;
        public List<ForageModifier> modifiers = new();
    }

    [Serializable]
    public class FocusData
    {
        public string id;
        public string name;
        public List<string> tags = new();
        public double multiplier = 2;
    }

    [Serializable]
    public class ForageEntry
    {
        public string itemId;
        public double weight;
        public int min = 1;
        public int max = 1;
        public EntryTime time;
        /// <summary>Only drops while this quest is active. Empty for always.</summary>
        public string requiresQuest;
        /// <summary>Only drops when this companion is on the expedition. Empty for any.</summary>
        public string requiresCompanion;
    }

    [Serializable]
    public class ForageTableData
    {
        public string id;
        public string name;
        public int rolls = 4;
        public List<ForageEntry> entries = new();
    }

    [Serializable]
    public class NpcData
    {
        public string id;
        public string name;
        public string species;
        public string description;
    }

    [Serializable]
    public class QuestObjective
    {
        public ObjectiveType type;
        public string target;
        public int qty = 1;
    }

    [Serializable]
    public class QuestReward
    {
        public RewardType type;
        public string target;
        public int qty = 1;
    }

    [Serializable]
    public class QuestDialogue
    {
        public string offer;
        public string active;
        public string ready;
        public string done;
    }

    [Serializable]
    public class QuestData
    {
        public string id;
        public string title;
        public string giver;
        public string summary;
        public bool isMain;
        public List<string> prerequisites = new();
        public List<QuestObjective> objectives = new();
        public List<QuestReward> rewards = new();
        public QuestDialogue dialogue = new();
    }

    [Serializable]
    public class PlaceableData
    {
        public string id;
        /// <summary>The inventory item this places (category Placeable).</summary>
        public string itemId;
        public PlacementType placement;
        /// <summary>Width of the base that blocks walking, in world units.</summary>
        public float footprintWidth = 1f;
        /// <summary>Depth (front to back) of the base that blocks walking, in world units.</summary>
        public float footprintDepth = 0.4f;
        public bool canFlip = true;
        /// <summary>Tray tab, e.g. Furniture, Plants, Decor.</summary>
        public string category;
        /// <summary>How many guests can perch on it. Guests come later; quests can already count seats.</summary>
        public int seats;
    }
}
