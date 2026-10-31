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

    /// <summary>One thing that can be left on the doorstep.</summary>
    [Serializable]
    public class GiftEntry
    {
        public string itemId;
        public int qty = 1;
        public double weight = 1;
        /// <summary>Something special (rare furniture, a treasure): only given on an "exclusive" roll, which cosiness makes likelier.</summary>
        public bool exclusive;
    }

    /// <summary>A nice thing about today, announced in the morning (see <see cref="DayEvents"/>).</summary>
    [Serializable]
    public class DayEventData
    {
        public string id;
        public string name;
        /// <summary>The morning announcement, e.g. "Something glints in the bush today."</summary>
        public string line;
        /// <summary>What it does, in a few words, for the HUD and screens.</summary>
        public string effect;
        /// <summary>How likely, against the other events.</summary>
        public double weight = 1;
        /// <summary>Rarer events get likelier as the cafe gets cosier.</summary>
        public Rarity rarity;
        /// <summary>Only happens once the cafe is at least this cosy.</summary>
        public int minCosiness;
        /// <summary>Only once night is unlocked (for night events).</summary>
        public bool needsNight;

        /// <summary>Added to forage trips today (at <see cref="forageAt"/>).</summary>
        public List<ForageModifier> forage = new();
        public ActiveTime forageAt = ActiveTime.Both;
        /// <summary>Added to the peek-a-boo find today, which also rustles for longer.</summary>
        public List<ForageModifier> peek = new();
        public bool peekLonger;
        /// <summary>Extra dishes the cafe sells today.</summary>
        public int extraSales;
        /// <summary>The first dish sold today pays double.</summary>
        public bool critic;
        /// <summary>A guest with a quest of yours under way is guaranteed to drop by.</summary>
        public bool familiarFace;
        /// <summary>Something left on the doorstep: one of these.</summary>
        public List<GiftEntry> gift = new();
    }

    /// <summary>How often day events happen, and how cosiness helps.</summary>
    [Serializable]
    public class DayEventSettings
    {
        /// <summary>The chance of an event on a given morning, in a bare cafe.</summary>
        public double chance = 0.3;
        /// <summary>Added to that chance at full cosiness (see <see cref="cosinessForFullBoost"/>).</summary>
        public double chanceFromCosiness = 0.3;
        /// <summary>At full cosiness, rarer events' weights are multiplied by 1 + this x their rarity step (uncommon 1, rare 2, legendary 3).</summary>
        public double rarerFromCosiness = 1;
        /// <summary>The cosiness at which the boosts are full.</summary>
        public int cosinessForFullBoost = 150;
        /// <summary>The chance a doorstep gift is something exclusive, in a bare cafe.</summary>
        public double exclusiveChance = 0.05;
        /// <summary>Added to that at full cosiness.</summary>
        public double exclusiveFromCosiness = 0.3;
        /// <summary>No events before this day (the tutorial).</summary>
        public int firstDay = 3;
    }

    /// <summary>A cafe upgrade, bought once with coins. <see cref="requires"/> makes a ladder (second slot before third).</summary>
    [Serializable]
    public class UpgradeData
    {
        public string id;
        public string name;
        public string description;
        public UpgradeEffect effect;
        public int amount = 1;
        public int price;
        /// <summary>Another upgrade that must be bought first. Empty for none.</summary>
        public string requires;
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
        /// <summary>How many dishes can be on the menu at once, at the start of the game.</summary>
        public int menuSlots = 1;
        /// <summary>Forage trips per day, which are also the parts of the day. 0 means no limit, and no day clock.</summary>
        public int forageTripsPerDay;
        /// <summary>Forage trips at night, once night is unlocked.</summary>
        public int nightTrips = 1;
        /// <summary>Perks that can be taken on one trip, at the start.</summary>
        public int perkSlots = 1;
        /// <summary>
        /// Luck protection: while this quest is under way, a forage trip is guaranteed to find what its
        /// item objectives still need (the tutorial's "3 leaves"). Empty for none.
        /// </summary>
        public string luckProtectedQuest;
        /// <summary>
        /// "Close for the day" stays hidden until this quest has been taken on (the tutorial's "Closing time"),
        /// so a new player can't skip day 1 before learning anything. Empty for always open.
        /// </summary>
        public string closeUnlockedBy;
        /// <summary>The cafe upgrades on offer, in shop order. They add to the numbers above.</summary>
        public List<UpgradeData> upgrades = new();
        /// <summary>The day events (see <see cref="DayEvents"/>).</summary>
        public List<DayEventData> dayEvents = new();
        public DayEventSettings events = new();

        public DayEventData GetEvent(string id) => string.IsNullOrEmpty(id) ? null : dayEvents.Find(e => e.id == id);

        /// <summary>Today's event, if it's been rolled for today.</summary>
        public DayEventData TodaysEvent(GameState state) => state.eventDay == state.day ? GetEvent(state.todayEvent) : null;

        /// <summary>The total of an effect from upgrades the player owns.</summary>
        public int Bonus(GameState state, UpgradeEffect effect)
        {
            int total = 0;
            foreach (UpgradeData upgrade in upgrades)
                if (upgrade.effect == effect && state.upgrades.Contains(upgrade.id)) total += upgrade.amount;
            return total;
        }

        public UpgradeData GetUpgrade(string id) => upgrades.Find(u => u.id == id);

        /// <summary>Forage trips (and parts of the day) today. 0 means no limit.</summary>
        public int TripsPerDay(GameState state) => forageTripsPerDay <= 0 ? 0 : forageTripsPerDay + Bonus(state, UpgradeEffect.ForageTrip);

        public int PerkSlots(GameState state) => Math.Max(0, perkSlots + Bonus(state, UpgradeEffect.PerkSlot));

        /// <summary>Dishes in a full day. One place to add progression later (story steps, upgrades, seats).</summary>
        public int FullDaySales(GameState state) =>
            Math.Max(0, salesPerDay + Bonus(state, UpgradeEffect.DailySale) + (TodaysEvent(state)?.extraSales ?? 0));

        /// <summary>Menu slots now. One place to add progression later (story steps, upgrades), like <see cref="FullDaySales"/>.</summary>
        public int MenuSlots(GameState state) => Math.Max(1, menuSlots + Bonus(state, UpgradeEffect.MenuSlot));

        /// <summary>Parts of the day so far (trips taken), up to <see cref="forageTripsPerDay"/>.</summary>
        public int PartsPassed(GameState state) => TripsPerDay(state) <= 0 ? 0 : Math.Min(state.forageTripsToday, TripsPerDay(state));

        /// <summary>Every trip taken: it's evening and closing now isn't early. Always true with no day clock.</summary>
        public bool IsEvening(GameState state) => TripsPerDay(state) <= 0 || state.forageTripsToday >= TripsPerDay(state);

        /// <summary>The day's share of dishes by now: its share for each part of the day so far, rounded down.</summary>
        public int SalesFor(GameState state) =>
            TripsPerDay(state) <= 0 ? FullDaySales(state) : FullDaySales(state) * PartsPassed(state) / TripsPerDay(state);

        /// <summary>
        /// Dishes served during part <paramref name="part"/> (1 for the morning) of the day. The parts add up to
        /// a full day, spread as evenly as whole dishes allow (3 dishes over 3 parts: 1 each).
        /// </summary>
        public int SalesForPart(GameState state, int part)
        {
            int parts = TripsPerDay(state);
            if (parts <= 0) return FullDaySales(state);
            if (part < 1 || part > parts) return 0;
            return FullDaySales(state) * part / parts - FullDaySales(state) * (part - 1) / parts;
        }

        /// <summary>Dishes the cafe will serve during the next part of the day (while you're out on the next trip).</summary>
        public int NextPartSales(GameState state) =>
            TripsPerDay(state) <= 0 ? FullDaySales(state) : SalesForPart(state, PartsPassed(state) + 1);
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
        /// <summary>Coins to take them on a trip. They can go once a day (they get tired).</summary>
        public int tripCost;
        /// <summary>When they'll come foraging: by day, at night (nocturnal), or either, though still only once a day.</summary>
        public ActiveTime activeAt;
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
        /// <summary>When they're at the cafe.</summary>
        public VisitType visits;
        /// <summary>For Random visitors: the chance (0-1) they drop by on a given day, before the cosiness boost.</summary>
        public double visitChance;
        /// <summary>For Random visitors: the first day they can turn up.</summary>
        public int firstDay;
        /// <summary>For Random visitors: they only turn up once this quest is completed. Empty for any time.</summary>
        public string afterQuest;
        /// <summary>By day, by night (nocturnal guests), or both.</summary>
        public ActiveTime activeAt;
        /// <summary>Recipes they sell (each recipe's price is in the recipe). Empty for no shop.</summary>
        public List<string> shop = new();
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
        /// <summary>Only opens the day after its prerequisites were done ("come back tomorrow"), so the story spreads over days.</summary>
        public bool nextDay;
        /// <summary>Doesn't open before this day. 0 for any day.</summary>
        public int minDay;
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
        /// <summary>How much it adds to the cafe's cosiness (more visitors, more lucky days).</summary>
        public int cosiness;
        /// <summary>Its look, for the cafe sign: e.g. garden, rustic, glow, fancy.</summary>
        public string style;
    }
}
