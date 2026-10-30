using System.Collections.Generic;
using System.IO;
using Bush.Core;
using UnityEditor;
using UnityEngine;

/// <summary>Makes the starting set of day events (Tools > Bush > Create Default Day Events). Safe to run again: existing ones are left as they are.</summary>
public static class DayEventTools
{
    private const string Folder = "Assets/Data/DayEvents";
    private const string DatabasePath = "Assets/Data/GameDatabase.asset";

    [MenuItem("Tools/Bush/Create Default Day Events", priority = 31)]
    public static void CreateDefaults()
    {
        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
        if (!database)
        {
            Debug.LogError($"[Bush] No GameDatabase at {DatabasePath}. Import the design data first.");
            return;
        }
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "DayEvents");

        int created = 0;
        void Make(string id, string name, string line, string effect, float weight, Rarity rarity, System.Action<DayEventDefinition> setUp)
        {
            string path = $"{Folder}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<DayEventDefinition>(path)) return;
            var asset = ScriptableObject.CreateInstance<DayEventDefinition>();
            asset.id = id;
            asset.displayName = name;
            asset.line = line;
            asset.effect = effect;
            asset.weight = weight;
            asset.rarity = rarity;
            setUp?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            if (!database.dayEvents.Contains(asset)) database.dayEvents.Add(asset);
            created++;
        }

        ModifierEntry Mod(ModifierType type, double value, string tag = "", Rarity rarity = Rarity.Common) =>
            new() { type = type, value = value, tag = tag, rarity = rarity };
        GiftEntryDefinition Gift(string itemId, int qty, float weight, bool exclusive = false) =>
            new() { item = database.Find<ItemDefinition>(itemId), qty = qty, weight = weight, exclusive = exclusive };

        Make("event.shiny_day", "Shiny day", "Something glints all over the bush today.", "Every trip has a chance of a bonus shiny find.", 3, Rarity.Common,
            e => e.forage = new List<ModifierEntry> { Mod(ModifierType.BonusRoll, 0.4, "shiny") });
        Make("event.still_air", "Still air", "Not a breath of wind. You'd hear a mouse rustle.", "Peek-a-boo rustles for longer, and the bush hides something good.", 3, Rarity.Common,
            e =>
            {
                e.peekLonger = true;
                e.peek = new List<ModifierEntry> { Mod(ModifierType.RarityWeight, 3, rarity: Rarity.Uncommon), Mod(ModifierType.RarityWeight, 3, rarity: Rarity.Rare), Mod(ModifierType.RarityWeight, 2, rarity: Rarity.Legendary) };
            });
        Make("event.mushroom_weather", "Mushroom weather", "Rain overnight. The mushrooms will be out.", "Fungi turn up twice as often today.", 3, Rarity.Common,
            e => { e.forage = new List<ModifierEntry> { Mod(ModifierType.TagWeight, 2, "fungi") }; e.forageAt = ActiveTime.Day; });
        Make("event.berry_glut", "Berry glut", "The bushes are heavy with fruit.", "+1 fruit with every fruit find today.", 3, Rarity.Common,
            e => e.forage = new List<ModifierEntry> { Mod(ModifierType.TagQuantity, 1, "fruit") });
        Make("event.busy_day", "Busy day", "Word's got around. It'll be a busy one.", "One extra dish sells today.", 3, Rarity.Common,
            e => e.extraSales = 1);
        Make("event.familiar_face", "A familiar face", "Someone you know is on their way.", "A guest with a quest of yours under way is sure to drop by.", 2, Rarity.Common,
            e => e.familiarFace = true);
        Make("event.food_critic", "A food critic", "A food critic is in the bush today.", "The first dish sold today pays double.", 2, Rarity.Uncommon,
            e => e.critic = true);
        Make("event.meteor_shower", "Meteor shower", "Clear skies tonight: a meteor shower after dark.", "Night trips find more, and night visitors are likelier.", 2, Rarity.Uncommon,
            e =>
            {
                e.needsNight = true;
                e.forageAt = ActiveTime.Night;
                e.forage = new List<ModifierEntry> { Mod(ModifierType.ExtraRolls, 1), Mod(ModifierType.TagWeight, 4, "encounter") };
            });
        Make("event.doorstep", "Left on the doorstep", "There's something on the doorstep this morning.", "A gift, straight into your bag. Sometimes something special.", 2, Rarity.Uncommon,
            e => e.gift = new List<GiftEntryDefinition>
            {
                Gift("item.leaves", 3, 3), Gift("item.berries", 3, 3), Gift("item.mushroom", 2, 2), Gift("item.feijoa", 2, 2), Gift("item.shiny_button", 1, 1),
                Gift("item.lantern_post", 1, 2, true), Gift("item.flower_barrel", 1, 2, true), Gift("item.bench", 1, 1, true), Gift("item.kauri_gum", 1, 1, true)
            });

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Bush] Created {created} day event(s) in {Folder} and listed them on the GameDatabase. Edit them in the Inspector.");
    }
}
