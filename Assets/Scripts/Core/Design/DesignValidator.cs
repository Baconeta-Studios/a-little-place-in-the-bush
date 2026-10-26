using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>
    /// Finds mistakes in the design data: duplicate or missing IDs, broken references, impossible
    /// forage entries and modifiers that can never do anything. Run from Tools > Bush > Validate
    /// Design Data, and by the tests.
    /// </summary>
    public static class DesignValidator
    {
        public static List<string> Validate(DesignData design)
        {
            var errors = new List<string>();
            design.RebuildIndex();

            CheckIds(errors, "item", design.items, x => x.id);
            CheckIds(errors, "recipe", design.recipes, x => x.id);
            CheckIds(errors, "companion", design.companions, x => x.id);
            CheckIds(errors, "perk", design.perks, x => x.id);
            CheckIds(errors, "focus", design.focuses, x => x.id);
            CheckIds(errors, "forage table", design.forageTables, x => x.id);
            CheckIds(errors, "npc", design.npcs, x => x.id);
            CheckIds(errors, "quest", design.quests, x => x.id);

            var allTags = new HashSet<string>();
            foreach (ItemData item in design.items)
                if (item.tags != null) allTags.UnionWith(item.tags);

            foreach (RecipeData recipe in design.recipes)
            {
                if (recipe.ingredients.Count == 0) errors.Add($"Recipe {recipe.id} has no ingredients.");
                foreach (ItemAmount ingredient in recipe.ingredients)
                {
                    ItemData item = RequireItem(errors, design, ingredient.itemId, $"recipe {recipe.id}");
                    if (ingredient.qty <= 0) errors.Add($"Recipe {recipe.id} needs a quantity of at least 1 for {ingredient.itemId}.");
                    if (item != null && item.category == ItemCategory.KeyItem) errors.Add($"Recipe {recipe.id} uses key item {item.id}; key items can't be cooked.");
                }
                foreach (string scrap in recipe.scraps)
                {
                    ItemData item = RequireItem(errors, design, scrap, $"recipe {recipe.id} scraps");
                    if (item != null && !item.unique) errors.Add($"Recipe scrap {scrap} should be unique.");
                }
            }

            foreach (CompanionData companion in design.companions)
            {
                CheckModifiers(errors, $"companion {companion.id}", companion.modifiers, allTags);
                if (!string.IsNullOrEmpty(companion.teachesPerk))
                {
                    if (design.GetPerk(companion.teachesPerk) == null) errors.Add($"Companion {companion.id} teaches unknown perk {companion.teachesPerk}.");
                    if (companion.tripsToTeach < 1) errors.Add($"Companion {companion.id} teaches a perk but tripsToTeach is {companion.tripsToTeach}.");
                }
            }

            foreach (PerkData perk in design.perks)
                CheckModifiers(errors, $"perk {perk.id}", perk.modifiers, allTags);

            foreach (FocusData focus in design.focuses)
            {
                if (focus.tags.Count == 0) errors.Add($"Focus {focus.id} has no tags.");
                foreach (string tag in focus.tags)
                    if (!allTags.Contains(tag)) errors.Add($"Focus {focus.id} uses tag '{tag}', which no item has.");
                if (focus.multiplier <= 0) errors.Add($"Focus {focus.id} multiplier must be above 0.");
            }

            foreach (ForageTableData table in design.forageTables)
            {
                if (table.rolls < 1) errors.Add($"Forage table {table.id} needs at least 1 roll.");
                if (table.entries.Count == 0) errors.Add($"Forage table {table.id} has no entries.");
                foreach (ForageEntry entry in table.entries)
                {
                    string where = $"forage table {table.id}";
                    RequireItem(errors, design, entry.itemId, where);
                    if (entry.weight <= 0) errors.Add($"{where}: {entry.itemId} weight must be above 0.");
                    if (entry.min < 1) errors.Add($"{where}: {entry.itemId} min must be at least 1.");
                    if (entry.min > entry.max) errors.Add($"{where}: {entry.itemId} min ({entry.min}) is more than max ({entry.max}).");
                    if (!string.IsNullOrEmpty(entry.requiresQuest) && design.GetQuest(entry.requiresQuest) == null)
                        errors.Add($"{where}: {entry.itemId} requires unknown quest {entry.requiresQuest}.");
                }
            }

            foreach (QuestData quest in design.quests)
            {
                string where = $"quest {quest.id}";
                if (!string.IsNullOrEmpty(quest.giver) && design.GetNpc(quest.giver) == null) errors.Add($"{where} has unknown giver {quest.giver}.");
                if (quest.objectives.Count == 0) errors.Add($"{where} has no objectives.");
                foreach (string prerequisite in quest.prerequisites)
                {
                    if (design.GetQuest(prerequisite) == null) errors.Add($"{where} has unknown prerequisite {prerequisite}.");
                    if (prerequisite == quest.id) errors.Add($"{where} is its own prerequisite.");
                }

                foreach (QuestObjective objective in quest.objectives)
                {
                    if (objective.qty < 1) errors.Add($"{where}: objective {objective.type} needs a quantity of at least 1.");
                    switch (objective.type)
                    {
                        case ObjectiveType.HaveItems:
                        case ObjectiveType.DeliverItems:
                            RequireItem(errors, design, objective.target, where);
                            break;
                        case ObjectiveType.CookRecipe:
                            if (!string.IsNullOrEmpty(objective.target) && design.GetRecipe(objective.target) == null)
                                errors.Add($"{where}: cook objective has unknown recipe {objective.target}.");
                            break;
                        case ObjectiveType.TalkTo:
                            if (design.GetNpc(objective.target) == null) errors.Add($"{where}: talk objective has unknown npc {objective.target}.");
                            break;
                    }
                }

                foreach (QuestReward reward in quest.rewards)
                {
                    switch (reward.type)
                    {
                        case RewardType.Item:
                            RequireItem(errors, design, reward.target, where);
                            break;
                        case RewardType.Coins:
                            if (reward.qty <= 0) errors.Add($"{where}: coin reward must be above 0.");
                            break;
                        case RewardType.UnlockRecipe:
                            if (design.GetRecipe(reward.target) == null) errors.Add($"{where}: unlocks unknown recipe {reward.target}.");
                            break;
                        case RewardType.UnlockCompanion:
                            if (design.GetCompanion(reward.target) == null) errors.Add($"{where}: unlocks unknown companion {reward.target}.");
                            break;
                        case RewardType.UnlockPerk:
                            if (design.GetPerk(reward.target) == null) errors.Add($"{where}: unlocks unknown perk {reward.target}.");
                            break;
                    }
                }
            }

            return errors;
        }

        private static void CheckModifiers(List<string> errors, string where, List<ForageModifier> modifiers, HashSet<string> allTags)
        {
            foreach (ForageModifier modifier in modifiers)
            {
                bool usesTag = modifier.type is ModifierType.TagWeight or ModifierType.TagQuantity or ModifierType.BonusRoll;
                if (usesTag && string.IsNullOrEmpty(modifier.tag)) errors.Add($"{where}: {modifier.type} needs a tag.");
                else if (usesTag && !allTags.Contains(modifier.tag)) errors.Add($"{where}: tag '{modifier.tag}' isn't on any item, so {modifier.type} does nothing.");

                if ((modifier.type is ModifierType.TagWeight or ModifierType.RarityWeight) && modifier.value <= 0)
                    errors.Add($"{where}: {modifier.type} multiplier must be above 0.");
                if (modifier.type == ModifierType.BonusRoll && (modifier.value <= 0 || modifier.value > 1))
                    errors.Add($"{where}: BonusRoll chance must be between 0 and 1 (was {modifier.value}).");
                if ((modifier.type is ModifierType.TagQuantity or ModifierType.ExtraRolls) && modifier.value < 1)
                    errors.Add($"{where}: {modifier.type} value below 1 does nothing (it's rounded down).");
            }
        }

        private static ItemData RequireItem(List<string> errors, DesignData design, string itemId, string where)
        {
            ItemData item = design.GetItem(itemId);
            if (item == null) errors.Add($"{where} refers to unknown item '{itemId}'.");
            return item;
        }

        private static void CheckIds<T>(List<string> errors, string kind, List<T> list, System.Func<T, string> key)
        {
            var seen = new HashSet<string>();
            foreach (T entry in list)
            {
                string id = key(entry);
                if (string.IsNullOrWhiteSpace(id)) errors.Add($"A {kind} has no id.");
                else if (!seen.Add(id)) errors.Add($"Duplicate {kind} id '{id}'.");
            }
        }
    }
}
