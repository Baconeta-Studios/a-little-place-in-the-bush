using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bush.Core;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Moves design data between Design/design-data.json (what the web sandbox edits) and the
/// ScriptableObject assets in Assets/Data. Importing matches assets by ID, so renamed assets
/// keep their icons, portraits and prefabs; it never deletes anything.
/// </summary>
public static class DesignDataTools
{
    private const string DataFolder = "Assets/Data";
    private const string DatabasePath = DataFolder + "/GameDatabase.asset";
    private static string DefaultJsonPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Design", "design-data.json"));

    [MenuItem("Tools/Bush/Import Design Data (Design folder)", priority = 1)]
    public static void ImportDefault() => Import(DefaultJsonPath);

    [MenuItem("Tools/Bush/Import Design Data From File...", priority = 2)]
    public static void ImportFromFile()
    {
        string path = EditorUtility.OpenFilePanel("Import design data", Path.GetDirectoryName(DefaultJsonPath), "json");
        if (!string.IsNullOrEmpty(path)) Import(path);
    }

    [MenuItem("Tools/Bush/Export Design Data...", priority = 3)]
    public static void Export()
    {
        GameDatabase database = LoadOrCreateDatabase();
        string path = EditorUtility.SaveFilePanel("Export design data", Path.GetDirectoryName(DefaultJsonPath), "design-data", "json");
        if (string.IsNullOrEmpty(path)) return;

        DesignFile file = DesignFile.FromDesign(database.BuildDesign());
        File.WriteAllText(path, JsonUtility.ToJson(file, true) + "\n");
        Debug.Log($"[Bush] Exported design data to {path}. Open it in the forage sandbox, or commit it to Design/.");
    }

    [MenuItem("Tools/Bush/Validate Design Data", priority = 20)]
    public static void Validate()
    {
        GameDatabase database = LoadOrCreateDatabase();
        List<string> errors = DesignValidator.Validate(database.BuildDesign());
        errors.AddRange(ValidateAssets(database));

        if (errors.Count == 0)
        {
            Debug.Log("[Bush] Design data is valid.");
            return;
        }

        foreach (string error in errors) Debug.LogWarning($"[Bush] {error}");
        Debug.LogError($"[Bush] Design data has {errors.Count} problem(s). See the warnings above.");
    }

    [MenuItem("Tools/Bush/Collect All Definitions Into Database", priority = 21)]
    public static void Collect()
    {
        GameDatabase database = LoadOrCreateDatabase();
        database.items = FindAll<ItemDefinition>();
        database.recipes = FindAll<RecipeDefinition>();
        database.companions = FindAll<CompanionDefinition>();
        database.perks = FindAll<PerkDefinition>();
        database.focuses = FindAll<FocusDefinition>();
        database.forageTables = FindAll<ForageTableDefinition>();
        database.npcs = FindAll<NpcDefinition>();
        database.quests = FindAll<QuestDefinition>();
        database.placeables = FindAll<PlaceableDefinition>();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Bush] Database now lists {database.All().Count()} definitions.");
    }

    public static void Import(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"[Bush] No design data at {path}.");
            return;
        }

        DesignFile file = JsonUtility.FromJson<DesignFile>(File.ReadAllText(path));
        var errors = new List<string>();
        DesignData design = file.ToDesign(errors);
        errors.AddRange(DesignValidator.Validate(design));
        if (errors.Count > 0)
        {
            foreach (string error in errors) Debug.LogWarning($"[Bush] {error}");
            if (!EditorUtility.DisplayDialog("Design data has problems",
                    $"{errors.Count} problem(s) found (see the Console). Import anyway?", "Import", "Cancel")) return;
        }

        var assets = new AssetIndex();
        {

            // Pass 1: create or find an asset for every ID and set the plain fields.
            foreach (ItemData x in design.items)
            {
                ItemDefinition asset = assets.GetOrCreate<ItemDefinition>(x.id, "Items");
                SetBasics(asset, x.id, x.name, x.description);
                asset.category = x.category;
                asset.rarity = x.rarity;
                asset.tags = new List<string>(x.tags);
                asset.sellValue = x.sellValue;
                asset.unique = x.unique;
            }
            foreach (PerkData x in design.perks)
            {
                PerkDefinition asset = assets.GetOrCreate<PerkDefinition>(x.id, "Perks");
                SetBasics(asset, x.id, x.name, x.description);
                asset.unlockedByDefault = x.unlockedByDefault;
                asset.modifiers = x.modifiers.Select(ToEntry).ToList();
            }
            foreach (FocusData x in design.focuses)
            {
                FocusDefinition asset = assets.GetOrCreate<FocusDefinition>(x.id, "Focuses");
                SetBasics(asset, x.id, x.name, null);
                asset.tags = new List<string>(x.tags);
                asset.multiplier = x.multiplier;
            }
            foreach (NpcData x in design.npcs)
            {
                NpcDefinition asset = assets.GetOrCreate<NpcDefinition>(x.id, "Npcs");
                SetBasics(asset, x.id, x.name, x.description);
                asset.species = x.species;
            }
            foreach (RecipeData x in design.recipes) SetBasics(assets.GetOrCreate<RecipeDefinition>(x.id, "Recipes"), x.id, x.name, x.description);
            foreach (CompanionData x in design.companions) SetBasics(assets.GetOrCreate<CompanionDefinition>(x.id, "Companions"), x.id, x.name, x.description);
            foreach (ForageTableData x in design.forageTables) SetBasics(assets.GetOrCreate<ForageTableDefinition>(x.id, "ForageTables"), x.id, x.name, null);
            foreach (QuestData x in design.quests) SetBasics(assets.GetOrCreate<QuestDefinition>(x.id, "Quests"), x.id, x.title, x.summary);

            // Pass 2: everything that refers to other assets.
            foreach (RecipeData x in design.recipes)
            {
                var asset = assets.Get<RecipeDefinition>(x.id);
                asset.coins = x.coins;
                asset.unlockedByDefault = x.unlockedByDefault;
                asset.ingredients = x.ingredients.Select(i => new IngredientEntry { item = assets.Get<ItemDefinition>(i.itemId), qty = i.qty }).ToList();
                asset.scraps = x.scraps.Select(assets.Get<ItemDefinition>).Where(s => s).ToList();
            }
            foreach (CompanionData x in design.companions)
            {
                var asset = assets.Get<CompanionDefinition>(x.id);
                asset.unlockedByDefault = x.unlockedByDefault;
                asset.modifiers = x.modifiers.Select(ToEntry).ToList();
                asset.teachesPerk = assets.Get<PerkDefinition>(x.teachesPerk);
                asset.tripsToTeach = x.tripsToTeach;
            }
            foreach (ForageTableData x in design.forageTables)
            {
                var asset = assets.Get<ForageTableDefinition>(x.id);
                asset.rolls = x.rolls;
                asset.entries = x.entries.Select(e => new ForageEntryDefinition
                {
                    item = assets.Get<ItemDefinition>(e.itemId), weight = e.weight, min = e.min, max = e.max, time = e.time,
                    requiresQuest = assets.Get<QuestDefinition>(e.requiresQuest)
                }).ToList();
            }
            foreach (QuestData x in design.quests)
            {
                var asset = assets.Get<QuestDefinition>(x.id);
                asset.giver = assets.Get<NpcDefinition>(x.giver);
                asset.isMain = x.isMain;
                asset.prerequisites = x.prerequisites.Select(assets.Get<QuestDefinition>).Where(q => q).ToList();
                asset.objectives = x.objectives.Select(o => new ObjectiveEntry { type = o.type, target = assets.GetAny(o.target), qty = o.qty }).ToList();
                asset.rewards = x.rewards.Select(r => new RewardEntry { type = r.type, target = assets.GetAny(r.target), qty = r.qty }).ToList();
                asset.offerLine = x.dialogue?.offer;
                asset.activeLine = x.dialogue?.active;
                asset.readyLine = x.dialogue?.ready;
                asset.doneLine = x.dialogue?.done;
            }

            assets.SaveAll();
            Debug.Log($"[Bush] Imported {path}: {assets.Created} created, {assets.Updated} updated.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Collect();
    }

    private static IEnumerable<string> ValidateAssets(GameDatabase database)
    {
        foreach (QuestDefinition quest in database.quests.Where(q => q))
        {
            foreach (ObjectiveEntry objective in quest.objectives)
            {
                Type expected = objective.type switch
                {
                    ObjectiveType.HaveItems or ObjectiveType.DeliverItems or ObjectiveType.PlaceObject => typeof(ItemDefinition),
                    ObjectiveType.CookRecipe => typeof(RecipeDefinition),
                    ObjectiveType.TalkTo => typeof(NpcDefinition),
                    _ => null
                };
                if (expected != null && objective.target && !expected.IsInstanceOfType(objective.target))
                    yield return $"Quest {quest.id}: {objective.type} target should be a {expected.Name}, not {objective.target.GetType().Name}.";
            }
        }

        foreach (ItemDefinition item in database.items.Where(i => i && !i.icon))
            yield return $"Item {item.id} has no icon yet.";
    }

    private static void SetBasics(GameDefinition asset, string id, string displayName, string description)
    {
        asset.id = id;
        asset.displayName = displayName;
        if (description != null) asset.description = description;
    }

    private static ModifierEntry ToEntry(ForageModifier modifier) =>
        new() { type = modifier.type, tag = modifier.tag, rarity = modifier.rarity, value = modifier.value };

    private static GameDatabase LoadOrCreateDatabase()
    {
        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
        if (database) return database;

        EnsureFolder(null);
        database = ScriptableObject.CreateInstance<GameDatabase>();
        AssetDatabase.CreateAsset(database, DatabasePath);
        return database;
    }

    /// <summary>Assets/Data, or a subfolder of it, created through the AssetDatabase so assets can go straight in.</summary>
    private static string EnsureFolder(string subfolder)
    {
        if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets", "Data");
        if (string.IsNullOrEmpty(subfolder)) return DataFolder;
        string path = $"{DataFolder}/{subfolder}";
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(DataFolder, subfolder);
        return path;
    }

    private static List<T> FindAll<T>() where T : GameDefinition =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset)
            .OrderBy(asset => asset.id)
            .ToList();

    /// <summary>Every existing definition by ID, plus the ones created during this import.</summary>
    private class AssetIndex
    {
        private readonly Dictionary<string, GameDefinition> _byId = new();
        private readonly HashSet<GameDefinition> _touched = new();
        public int Created { get; private set; }
        public int Updated => _touched.Count - Created;

        public AssetIndex()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:GameDefinition"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset && !string.IsNullOrEmpty(asset.id) && !_byId.ContainsKey(asset.id)) _byId.Add(asset.id, asset);
            }
        }

        public T GetOrCreate<T>(string id, string folder) where T : GameDefinition
        {
            if (_byId.TryGetValue(id, out GameDefinition existing) && existing is T typed)
            {
                _touched.Add(typed);
                return typed;
            }

            string directory = EnsureFolder(folder);
            var asset = ScriptableObject.CreateInstance<T>();
            asset.id = id;
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{directory}/{id}.asset"));
            _byId[id] = asset;
            _touched.Add(asset);
            Created++;
            return asset;
        }

        public T Get<T>(string id) where T : GameDefinition =>
            !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out GameDefinition asset) ? asset as T : null;

        public GameDefinition GetAny(string id) => Get<GameDefinition>(id);

        public void SaveAll()
        {
            foreach (GameDefinition asset in _touched) EditorUtility.SetDirty(asset);
        }
    }
}
