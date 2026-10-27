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
