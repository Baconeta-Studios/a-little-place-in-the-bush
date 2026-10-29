using System;
using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>
    /// All the design data for one run of the game, with lookups by ID. Built from the
    /// GameDatabase asset at runtime, or from Design/design-data.json in tests and tools.
    /// </summary>
    public class DesignData
    {
        public List<ItemData> items = new();
        public List<RecipeData> recipes = new();
        public List<CompanionData> companions = new();
        public List<PerkData> perks = new();
        public List<FocusData> focuses = new();
        public List<ForageTableData> forageTables = new();
        public List<NpcData> npcs = new();
        public List<QuestData> quests = new();
        public List<PlaceableData> placeables = new();
        /// <summary>Tuning for the day loop. Set on the GameDatabase asset; defaults here for tests and tools.</summary>
        public DayRules rules = new();

        private Dictionary<string, ItemData> _items;
        private Dictionary<string, RecipeData> _recipes;
        private Dictionary<string, CompanionData> _companions;
        private Dictionary<string, PerkData> _perks;
        private Dictionary<string, FocusData> _focuses;
        private Dictionary<string, ForageTableData> _tables;
        private Dictionary<string, NpcData> _npcs;
        private Dictionary<string, QuestData> _quests;
        private Dictionary<string, PlaceableData> _placeables;

        /// <summary>Call after changing the lists. Lookups also build themselves the first time they're used.</summary>
        public void RebuildIndex()
        {
            _items = Index(items, x => x.id);
            _recipes = Index(recipes, x => x.id);
            _companions = Index(companions, x => x.id);
            _perks = Index(perks, x => x.id);
            _focuses = Index(focuses, x => x.id);
            _tables = Index(forageTables, x => x.id);
            _npcs = Index(npcs, x => x.id);
            _quests = Index(quests, x => x.id);
            _placeables = Index(placeables, x => x.id);
        }

        public ItemData GetItem(string id) => Get(ref _items, id);
        public RecipeData GetRecipe(string id) => Get(ref _recipes, id);
        public CompanionData GetCompanion(string id) => Get(ref _companions, id);
        public PerkData GetPerk(string id) => Get(ref _perks, id);
        public FocusData GetFocus(string id) => Get(ref _focuses, id);
        public ForageTableData GetForageTable(string id) => Get(ref _tables, id);
        public NpcData GetNpc(string id) => Get(ref _npcs, id);
        public QuestData GetQuest(string id) => Get(ref _quests, id);
        public PlaceableData GetPlaceable(string id) => Get(ref _placeables, id);
        public UpgradeData GetUpgrade(string id) => rules?.GetUpgrade(id);
        public PlaceableData GetPlaceableForItem(string itemId) => placeables.Find(p => p.itemId == itemId);

        private T Get<T>(ref Dictionary<string, T> lookup, string id) where T : class
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (lookup == null) RebuildIndex();
            return lookup.TryGetValue(id, out T value) ? value : null;
        }

        private static Dictionary<string, T> Index<T>(List<T> list, Func<T, string> key)
        {
            var lookup = new Dictionary<string, T>();
            foreach (T entry in list)
            {
                string id = entry == null ? null : key(entry);
                // Duplicates are reported by DesignValidator; the first one wins here.
                if (!string.IsNullOrEmpty(id) && !lookup.ContainsKey(id)) lookup.Add(id, entry);
            }
            return lookup;
        }
    }
}
