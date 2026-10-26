using System;
using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>
    /// The shape of Design/design-data.json: the file the web sandbox exports and
    /// Tools > Bush > Import Design Data reads. Enums are strings so the file stays readable.
    /// Field names must match the JSON exactly (Unity's JsonUtility reads them by name).
    /// </summary>
    [Serializable]
    public class DesignFile
    {
        public int version = 1;
        public List<Item> items = new();
        public List<Recipe> recipes = new();
        public List<Companion> companions = new();
        public List<Perk> perks = new();
        public List<Focus> focuses = new();
        public List<Table> forageTables = new();
        public List<Npc> npcs = new();
        public List<Quest> quests = new();

        [Serializable] public class Item { public string id; public string name; public string category; public string rarity; public List<string> tags = new(); public int sellValue; public bool unique; public string description; }
        [Serializable] public class Amount { public string item; public int qty; }
        [Serializable] public class Recipe { public string id; public string name; public List<Amount> ingredients = new(); public int coins; public bool unlockedByDefault; public List<string> scraps = new(); public string description; }
        [Serializable] public class Modifier { public string type; public string tag; public string rarity; public double value; }
        [Serializable] public class Companion { public string id; public string name; public string description; public bool unlockedByDefault; public List<Modifier> modifiers = new(); public string teachesPerk; public int tripsToTeach; }
        [Serializable] public class Perk { public string id; public string name; public string description; public bool unlockedByDefault; public List<Modifier> modifiers = new(); }
        [Serializable] public class Focus { public string id; public string name; public List<string> tags = new(); public double multiplier; }
        [Serializable] public class Entry { public string item; public double weight; public int min; public int max; public string time; public string requiresQuest; }
        [Serializable] public class Table { public string id; public string name; public int rolls; public List<Entry> entries = new(); }
        [Serializable] public class Npc { public string id; public string name; public string species; public string description; }
        [Serializable] public class Objective { public string type; public string target; public int qty; }
        [Serializable] public class Reward { public string type; public string target; public int qty; }
        [Serializable] public class Dialogue { public string offer; public string active; public string ready; public string done; }
        [Serializable] public class Quest { public string id; public string title; public string giver; public bool isMain; public List<string> prerequisites = new(); public string summary; public List<Objective> objectives = new(); public List<Reward> rewards = new(); public Dialogue dialogue = new(); }

        /// <summary>Converts to the model. Unknown enum names are reported in <paramref name="errors"/> and fall back to the first value.</summary>
        public DesignData ToDesign(List<string> errors)
        {
            var design = new DesignData();

            foreach (Item x in items)
                design.items.Add(new ItemData
                {
                    id = x.id, name = x.name, description = x.description,
                    category = Parse(x.category, ItemCategory.Ingredient, errors, x.id),
                    rarity = Parse(x.rarity, Rarity.Common, errors, x.id),
                    tags = new List<string>(x.tags ?? new List<string>()),
                    sellValue = x.sellValue, unique = x.unique
                });

            foreach (Recipe x in recipes)
            {
                var recipe = new RecipeData
                {
                    id = x.id, name = x.name, description = x.description, coins = x.coins,
                    unlockedByDefault = x.unlockedByDefault, scraps = new List<string>(x.scraps ?? new List<string>())
                };
                foreach (Amount amount in x.ingredients) recipe.ingredients.Add(new ItemAmount(amount.item, amount.qty));
                design.recipes.Add(recipe);
            }

            foreach (Companion x in companions)
                design.companions.Add(new CompanionData
                {
                    id = x.id, name = x.name, description = x.description, unlockedByDefault = x.unlockedByDefault,
                    modifiers = ToModifiers(x.modifiers, errors, x.id), teachesPerk = x.teachesPerk, tripsToTeach = x.tripsToTeach
                });

            foreach (Perk x in perks)
                design.perks.Add(new PerkData
                {
                    id = x.id, name = x.name, description = x.description, unlockedByDefault = x.unlockedByDefault,
                    modifiers = ToModifiers(x.modifiers, errors, x.id)
                });

            foreach (Focus x in focuses)
                design.focuses.Add(new FocusData { id = x.id, name = x.name, tags = new List<string>(x.tags ?? new List<string>()), multiplier = x.multiplier });

            foreach (Table x in forageTables)
            {
                var table = new ForageTableData { id = x.id, name = x.name, rolls = x.rolls };
                foreach (Entry entry in x.entries)
                    table.entries.Add(new ForageEntry
                    {
                        itemId = entry.item, weight = entry.weight, min = entry.min, max = entry.max,
                        time = Parse(entry.time, EntryTime.Any, errors, $"{x.id}/{entry.item}"),
                        requiresQuest = entry.requiresQuest
                    });
                design.forageTables.Add(table);
            }

            foreach (Npc x in npcs)
                design.npcs.Add(new NpcData { id = x.id, name = x.name, species = x.species, description = x.description });

            foreach (Quest x in quests)
            {
                var quest = new QuestData
                {
                    id = x.id, title = x.title, giver = x.giver, isMain = x.isMain, summary = x.summary,
                    prerequisites = new List<string>(x.prerequisites ?? new List<string>()),
                    dialogue = new QuestDialogue
                    {
                        offer = x.dialogue?.offer, active = x.dialogue?.active, ready = x.dialogue?.ready, done = x.dialogue?.done
                    }
                };
                foreach (Objective objective in x.objectives)
                    quest.objectives.Add(new QuestObjective
                    {
                        type = Parse(objective.type, ObjectiveType.HaveItems, errors, x.id), target = objective.target, qty = objective.qty
                    });
                foreach (Reward reward in x.rewards)
                    quest.rewards.Add(new QuestReward
                    {
                        type = Parse(reward.type, RewardType.Item, errors, x.id), target = reward.target, qty = reward.qty
                    });
                design.quests.Add(quest);
            }

            design.RebuildIndex();
            return design;
        }

        public static DesignFile FromDesign(DesignData design)
        {
            var file = new DesignFile();

            foreach (ItemData x in design.items)
                file.items.Add(new Item
                {
                    id = x.id, name = x.name, category = x.category.ToString(), rarity = x.rarity.ToString(),
                    tags = new List<string>(x.tags), sellValue = x.sellValue, unique = x.unique, description = x.description
                });

            foreach (RecipeData x in design.recipes)
            {
                var recipe = new Recipe
                {
                    id = x.id, name = x.name, coins = x.coins, unlockedByDefault = x.unlockedByDefault,
                    scraps = new List<string>(x.scraps), description = x.description
                };
                foreach (ItemAmount amount in x.ingredients) recipe.ingredients.Add(new Amount { item = amount.itemId, qty = amount.qty });
                file.recipes.Add(recipe);
            }

            foreach (CompanionData x in design.companions)
                file.companions.Add(new Companion
                {
                    id = x.id, name = x.name, description = x.description, unlockedByDefault = x.unlockedByDefault,
                    modifiers = FromModifiers(x.modifiers), teachesPerk = x.teachesPerk, tripsToTeach = x.tripsToTeach
                });

            foreach (PerkData x in design.perks)
                file.perks.Add(new Perk
                {
                    id = x.id, name = x.name, description = x.description, unlockedByDefault = x.unlockedByDefault,
                    modifiers = FromModifiers(x.modifiers)
                });

            foreach (FocusData x in design.focuses)
                file.focuses.Add(new Focus { id = x.id, name = x.name, tags = new List<string>(x.tags), multiplier = x.multiplier });

            foreach (ForageTableData x in design.forageTables)
            {
                var table = new Table { id = x.id, name = x.name, rolls = x.rolls };
                foreach (ForageEntry entry in x.entries)
                    table.entries.Add(new Entry
                    {
                        item = entry.itemId, weight = entry.weight, min = entry.min, max = entry.max,
                        time = entry.time.ToString(), requiresQuest = entry.requiresQuest ?? ""
                    });
                file.forageTables.Add(table);
            }

            foreach (NpcData x in design.npcs)
                file.npcs.Add(new Npc { id = x.id, name = x.name, species = x.species, description = x.description });

            foreach (QuestData x in design.quests)
            {
                var quest = new Quest
                {
                    id = x.id, title = x.title, giver = x.giver, isMain = x.isMain, summary = x.summary,
                    prerequisites = new List<string>(x.prerequisites),
                    dialogue = new Dialogue { offer = x.dialogue?.offer, active = x.dialogue?.active, ready = x.dialogue?.ready, done = x.dialogue?.done }
                };
                foreach (QuestObjective objective in x.objectives)
                    quest.objectives.Add(new Objective { type = objective.type.ToString(), target = objective.target ?? "", qty = objective.qty });
                foreach (QuestReward reward in x.rewards)
                    quest.rewards.Add(new Reward { type = reward.type.ToString(), target = reward.target ?? "", qty = reward.qty });
                file.quests.Add(quest);
            }

            return file;
        }

        private static List<ForageModifier> ToModifiers(List<Modifier> modifiers, List<string> errors, string owner)
        {
            var result = new List<ForageModifier>();
            if (modifiers == null) return result;
            foreach (Modifier x in modifiers)
            {
                Rarity rarity = string.IsNullOrEmpty(x.rarity) ? Rarity.Common : Parse(x.rarity, Rarity.Common, errors, owner);
                result.Add(new ForageModifier(Parse(x.type, ModifierType.TagWeight, errors, owner), x.value, x.tag ?? "", rarity));
            }
            return result;
        }

        private static List<Modifier> FromModifiers(List<ForageModifier> modifiers)
        {
            var result = new List<Modifier>();
            foreach (ForageModifier x in modifiers)
            {
                bool usesRarity = x.type == ModifierType.RarityWeight;
                result.Add(new Modifier { type = x.type.ToString(), tag = x.tag ?? "", rarity = usesRarity ? x.rarity.ToString() : "", value = x.value });
            }
            return result;
        }

        private static T Parse<T>(string value, T fallback, List<string> errors, string owner) where T : struct, Enum
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            if (Enum.TryParse(value, true, out T parsed)) return parsed;
            errors?.Add($"{owner}: unknown {typeof(T).Name} '{value}', using {fallback}.");
            return fallback;
        }
    }
}
