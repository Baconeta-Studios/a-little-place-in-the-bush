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
    }

    private void BuildLookup()
    {
        _byId = new Dictionary<string, GameDefinition>();
        foreach (GameDefinition definition in All())
            if (definition && !string.IsNullOrEmpty(definition.id)) _byId[definition.id] = definition;
    }

    private void OnValidate() => _byId = null;
}
