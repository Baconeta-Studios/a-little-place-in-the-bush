using System;
using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>
    /// Everything that gets saved. Lists rather than dictionaries so Unity's JsonUtility can
    /// serialise it. Only the services change it: never edit it from UI code.
    /// </summary>
    [Serializable]
    public class GameState
    {
        public const int CurrentSaveVersion = 1;

        public int saveVersion = CurrentSaveVersion;
        public int currency;
        public List<ItemStack> inventory = new();
        /// <summary>Unique items (like recipe scraps) handed in for a quest. With the inventory, this says which uniques are collected.</summary>
        public List<string> handedIn = new();

        public List<string> unlockedRecipes = new();
        public List<string> unlockedPerks = new();
        public List<string> unlockedCompanions = new();
        public List<CompanionProgress> companions = new();
        public List<QuestProgress> quests = new();
        public List<PlacedObjectData> placed = new();
        /// <summary>Gives each placed object its own id, so two identical chairs can be told apart.</summary>
        public int nextPlacedId = 1;
        /// <summary>Areas whose starting layout has been put down, so it only happens once per save.</summary>
        public List<string> initialisedAreas = new();

        public int expeditionsStarted;
        public int expeditionsCompleted;
        /// <summary>Set when the player presses Go, cleared when the rewards are claimed. Survives a refresh mid-expedition.</summary>
        public bool hasPendingExpedition;
        public PendingExpedition pendingExpedition = new();

        public int day = 1;
        /// <summary>0-1 through the current day.</summary>
        public float timeOfDay;
        /// <summary>Recipe ids on today's menu, in order. Change any time; it's what sells when the day closes.</summary>
        public List<string> menu = new();
        /// <summary>Forage trips started today; reset when the day closes.</summary>
        public int forageTripsToday;
        /// <summary>Parts of today the cafe has served (it serves while you're out foraging).</summary>
        public int partsServed;
        /// <summary>What the cafe has sold today, dish by dish, for the day's summary.</summary>
        public List<DishSale> soldToday = new();
        /// <summary>Sales today that couldn't be made for want of ingredients.</summary>
        public int missedToday;
        /// <summary>Whose turn it is on the menu next, so sales share out evenly across the day.</summary>
        public int menuTurn;
        /// <summary>What sold while you were out on the latest trip, for its results screen.</summary>
        public List<DishSale> servedWhileOut = new();
        /// <summary>Night time has been unlocked (a quest reward): closing the cafe leads into the night.</summary>
        public bool nightUnlocked;
        /// <summary>It's night: the cafe's closed, nocturnal guests are about, and Go to bed starts the next day.</summary>
        public bool isNight;
        /// <summary>Forage trips started tonight.</summary>
        public int nightTripsToday;
        /// <summary>Companions who've been out today (they're tired until tomorrow).</summary>
        public List<string> companionsUsedToday = new();
        /// <summary>Cafe upgrades bought.</summary>
        public List<string> upgrades = new();
        /// <summary>Seeds the daily dice (visitors, lucky days) so a reload doesn't reroll them. Set once per save.</summary>
        public uint luckSeed;
        /// <summary>The day <see cref="visitorsToday"/> was rolled for.</summary>
        public int visitorsDay;
        /// <summary>Random visitors at the cafe today.</summary>
        public List<string> visitorsToday = new();

        /// <summary>
        /// A fresh game. Default unlocks aren't copied in: <see cref="Unlocks"/> reads them from the design
        /// data, so a patched build that adds a default recipe or companion reaches existing saves too.
        /// </summary>
        public static GameState CreateNew(DesignData design) => new();

        /// <summary>Has it now, or handed it in. Collected uniques never drop or get rewarded again.</summary>
        public bool IsUniqueCollected(string itemId) =>
            handedIn.Contains(itemId) || inventory.Exists(s => s.itemId == itemId && s.qty > 0);

        /// <summary>
        /// Tidies a loaded save: merges duplicate stacks, drops empty ones, keeps placed-object ids unique,
        /// and (given the design data) drops placed things whose placeable no longer exists.
        /// SaveService calls it after loading.
        /// </summary>
        public void Normalise(DesignData design = null)
        {
            if (design != null) placed.RemoveAll(p => p == null || design.GetPlaceable(p.placeableId) == null);
            menu ??= new List<string>();
            var seen = new HashSet<string>();
            menu.RemoveAll(id => string.IsNullOrEmpty(id) || !seen.Add(id) || (design != null && design.GetRecipe(id) == null));
            if (design != null && menu.Count > design.rules.MenuSlots(this)) menu.RemoveRange(design.rules.MenuSlots(this), menu.Count - design.rules.MenuSlots(this));
            if (day < 1) day = 1;
            if (forageTripsToday < 0) forageTripsToday = 0;
            companionsUsedToday ??= new List<string>();
            soldToday ??= new List<DishSale>();
            servedWhileOut ??= new List<DishSale>();
            upgrades ??= new List<string>();
            visitorsToday ??= new List<string>();
            foreach (PlacedObjectData p in placed)
                if (p.instanceId >= nextPlacedId) nextPlacedId = p.instanceId + 1;

            var merged = new List<ItemStack>();
            foreach (ItemStack stack in inventory)
            {
                if (stack == null || string.IsNullOrEmpty(stack.itemId) || stack.qty <= 0) continue;
                ItemStack existing = merged.Find(s => s.itemId == stack.itemId);
                if (existing == null) merged.Add(new ItemStack(stack.itemId, stack.qty));
                else existing.qty += stack.qty;
            }
            inventory = merged;
        }

        public QuestProgress FindQuest(string questId) => quests.Find(q => q.questId == questId);

        public CompanionProgress GetOrAddCompanion(string companionId)
        {
            CompanionProgress progress = companions.Find(c => c.companionId == companionId);
            if (progress == null)
            {
                progress = new CompanionProgress { companionId = companionId };
                companions.Add(progress);
            }
            return progress;
        }

        public bool IsQuestActive(string questId) => FindQuest(questId)?.status == QuestStatus.Active;
    }

    [Serializable]
    public class ItemStack
    {
        public string itemId;
        public int qty;

        public ItemStack() { }

        public ItemStack(string itemId, int qty)
        {
            this.itemId = itemId;
            this.qty = qty;
        }

        public override string ToString() => $"{qty} x {itemId}";
    }

    [Serializable]
    public class CompanionProgress
    {
        public string companionId;
        public int trips;
    }

    [Serializable]
    public class QuestProgress
    {
        public string questId;
        /// <summary>NotStarted, Active or Completed. ReadyToClaim is never stored.</summary>
        public QuestStatus status;
        /// <summary>Counts for event objectives (cook, expeditions, talk, place), keyed by objective type and target
        /// rather than position, so reordering a quest's objectives in a patch doesn't move progress around.</summary>
        public List<QuestCounter> counters = new();
        public bool rewardClaimed;
    }

    [Serializable]
    public class QuestCounter
    {
        public string key;
        public int count;
    }

    [Serializable]
    public class PlacedObjectData
    {
        public int instanceId;
        /// <summary>The area (scene) it's in, e.g. Cafe.</summary>
        public string area;
        public string placeableId;
        /// <summary>Where its base touches the floor.</summary>
        public float x;
        public float y;
        public bool flipped;
        /// <summary>Took an item from the inventory when placed, so putting it away gives one back.
        /// False for the starting layout and for unlimited (prototype) placing.</summary>
        public bool fromInventory;
    }

    [Serializable]
    public class PendingExpedition
    {
        public int id;
        public uint seed;
        public ExpeditionSetup setup = new();
        /// <summary>Worked out once when Go is pressed, so the results shown are exactly what gets paid.</summary>
        public ForageResult result = new();
        /// <summary>The peek-a-boo bonus has been grabbed (or missed): one chance per trip.</summary>
        public bool peekABooDone;
    }
}
