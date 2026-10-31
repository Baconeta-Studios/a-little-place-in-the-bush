using System.Collections.Generic;
using Bush.Core;
using UnityEngine;

/// <summary>
/// Lists every design asset. The game builds its <see cref="DesignData"/> from this once at
/// startup. Tools > Bush > Import Design Data fills it in, and so does the "Collect all
/// definitions" button in its inspector menu.
/// </summary>
[CreateAssetMenu(menuName = "Bush/Game Database", fileName = "GameDatabase")]
public class GameDatabase : ScriptableObject
{
    public List<ItemDefinition> items = new();
    public List<RecipeDefinition> recipes = new();
    public List<CompanionDefinition> companions = new();
    public List<PerkDefinition> perks = new();
    public List<FocusDefinition> focuses = new();
    public List<ForageTableDefinition> forageTables = new();
    public List<NpcDefinition> npcs = new();
    public List<QuestDefinition> quests = new();
    public List<PlaceableDefinition> placeables = new();
    public List<UpgradeDefinition> upgrades = new();
    [Tooltip("Day events: edit each one in the Inspector. Tools > Bush > Create Default Day Events makes the starting set.")]
    public List<DayEventDefinition> dayEvents = new();

    [Header("Day loop tuning (not part of the design data file)")]
    [Tooltip("Dishes the cafe sells when you close for the day, shared across the menu.")]
    [Min(0)] public int salesPerDay = 3;
    [Tooltip("How many dishes can be on the menu at once, at the start of the game.")]
    [Min(1)] public int menuSlots = 1;
    [Tooltip("Forage trips per day. 0 means no limit.")]
    [Min(0)] public int forageTripsPerDay = 3;
    [Tooltip("Perks that can come along on one trip, at the start. Upgrades can add more.")]
    [Min(0)] public int perkSlots = 1;
    [Tooltip("Forage trips at night, once night is unlocked.")]
    [Min(0)] public int nightTrips = 1;
    [Header("Day events (how often, and how cosiness helps)")]
    public DayEventSettings dayEventTuning = new();

    [Tooltip("Luck protection: while this quest is under way, a forage trip always finds what its item objectives still need.")]
    public QuestDefinition luckProtectedQuest;
    [Tooltip("Closing early stays hidden at the cafe door until this quest has been taken on. In the evening it's always open. Empty for always open.")]
    public QuestDefinition closeUnlockedBy;

    private Dictionary<string, GameDefinition> _byId;

    /// <summary>Converts every asset to the plain model the rules run on.</summary>
    public DesignData BuildDesign()
    {
        var design = new DesignData();
        foreach (ItemDefinition x in items) if (x) design.items.Add(x.ToData());
        foreach (RecipeDefinition x in recipes) if (x) design.recipes.Add(x.ToData());
        foreach (CompanionDefinition x in companions) if (x) design.companions.Add(x.ToData());
        foreach (PerkDefinition x in perks) if (x) design.perks.Add(x.ToData());
        foreach (FocusDefinition x in focuses) if (x) design.focuses.Add(x.ToData());
        foreach (ForageTableDefinition x in forageTables) if (x) design.forageTables.Add(x.ToData());
        foreach (NpcDefinition x in npcs) if (x) design.npcs.Add(x.ToData());
        foreach (QuestDefinition x in quests) if (x) design.quests.Add(x.ToData());
        foreach (PlaceableDefinition x in placeables) if (x) design.placeables.Add(x.ToData());
        design.rules = new DayRules { salesPerDay = salesPerDay, menuSlots = menuSlots, forageTripsPerDay = forageTripsPerDay, perkSlots = perkSlots, nightTrips = nightTrips,
            luckProtectedQuest = luckProtectedQuest ? luckProtectedQuest.id : "",
            closeUnlockedBy = closeUnlockedBy ? closeUnlockedBy.id : "" };
        foreach (UpgradeDefinition x in upgrades) if (x) design.rules.upgrades.Add(x.ToData());
        foreach (DayEventDefinition x in dayEvents) if (x) design.rules.dayEvents.Add(x.ToData());
        design.rules.events = dayEventTuning ?? new DayEventSettings();
        design.RebuildIndex();
        return design;
    }

    /// <summary>The asset with this ID (for icons, portraits and prefabs), or null.</summary>
    public T Find<T>(string id) where T : GameDefinition
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_byId == null) BuildLookup();
        return _byId.TryGetValue(id, out GameDefinition definition) ? definition as T : null;
    }

    public Sprite GetItemIcon(string itemId) => Find<ItemDefinition>(itemId)?.icon;

    public IEnumerable<GameDefinition> All()
    {
        foreach (var x in items) yield return x;
        foreach (var x in recipes) yield return x;
        foreach (var x in companions) yield return x;
        foreach (var x in perks) yield return x;
        foreach (var x in focuses) yield return x;
        foreach (var x in forageTables) yield return x;
        foreach (var x in npcs) yield return x;
        foreach (var x in quests) yield return x;
        foreach (var x in placeables) yield return x;
        foreach (var x in upgrades) yield return x;
        foreach (var x in dayEvents) yield return x;
    }

    private void BuildLookup()
    {
        _byId = new Dictionary<string, GameDefinition>();
        foreach (GameDefinition definition in All())
            if (definition && !string.IsNullOrEmpty(definition.id)) _byId[definition.id] = definition;
    }

    private void OnValidate() => _byId = null;
}
