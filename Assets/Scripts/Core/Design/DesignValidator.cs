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
            var allRarities = new HashSet<Rarity>();
            foreach (ItemData item in design.items) allRarities.Add(item.rarity);
            foreach (ForageModifier modifier in AllModifiers(design))
                if (modifier.type == ModifierType.RarityWeight && !allRarities.Contains(modifier.rarity))
                    errors.Add($"A RarityWeight modifier boosts {modifier.rarity} items, but no item is {modifier.rarity}.");

            var unlockableRecipes = new HashSet<string>();
            var unlockableCompanions = new HashSet<string>();
            var unlockablePerks = new HashSet<string>();
            foreach (QuestData quest in design.quests)
            {
                foreach (QuestReward reward in quest.rewards)
                {
                    if (reward.type == RewardType.UnlockRecipe) unlockableRecipes.Add(reward.target);
                    if (reward.type == RewardType.UnlockCompanion) unlockableCompanions.Add(reward.target);
                    if (reward.type == RewardType.UnlockPerk) unlockablePerks.Add(reward.target);
                }
            }
            foreach (CompanionData companion in design.companions)
                if (!string.IsNullOrEmpty(companion.teachesPerk)) unlockablePerks.Add(companion.teachesPerk);

            foreach (ItemData item in design.items)
            {
                if (item.category == ItemCategory.KeyItem && item.sellValue > 0) errors.Add($"Key item {item.id} has a sell value, but key items can't be sold.");
            }

            foreach (RecipeData recipe in design.recipes)
            {
                if (recipe.ingredients.Count == 0) errors.Add($"Recipe {recipe.id} has no ingredients.");
                if (recipe.coins < 0) errors.Add($"Recipe {recipe.id} pays negative coins, so it could never be cooked.");
                if (recipe.price < 0) errors.Add($"Recipe {recipe.id} has a negative price.");
                if (!recipe.unlockedByDefault && recipe.price <= 0 && !unlockableRecipes.Contains(recipe.id))
                    errors.Add($"Recipe {recipe.id} can never be unlocked: give it a price, a quest that unlocks it, or unlock it by default.");
                foreach (ItemAmount ingredient in recipe.ingredients)
                {
                    ItemData item = RequireItem(errors, design, ingredient.itemId, $"recipe {recipe.id}");
                    if (ingredient.qty <= 0) errors.Add($"Recipe {recipe.id} needs a quantity of at least 1 for {ingredient.itemId}.");
                    if (item != null && item.category == ItemCategory.KeyItem) errors.Add($"Recipe {recipe.id} uses key item {item.id}; key items can't be cooked.");
                    if (item != null && item.unique && ingredient.qty > 1) errors.Add($"Recipe {recipe.id} needs {ingredient.qty} of unique item {item.id}, but only one can exist.");
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
                if (!companion.unlockedByDefault && !unlockableCompanions.Contains(companion.id))
                    errors.Add($"Companion {companion.id} can never be unlocked: unlock it by default or add a quest reward for it.");
                if (!string.IsNullOrEmpty(companion.teachesPerk))
                {
                    if (design.GetPerk(companion.teachesPerk) == null) errors.Add($"Companion {companion.id} teaches unknown perk {companion.teachesPerk}.");
                    if (companion.tripsToTeach < 1) errors.Add($"Companion {companion.id} teaches a perk but tripsToTeach is {companion.tripsToTeach}.");
                }
            }

            foreach (PerkData perk in design.perks)
            {
                CheckModifiers(errors, $"perk {perk.id}", perk.modifiers, allTags);
                if (!perk.unlockedByDefault && !unlockablePerks.Contains(perk.id))
                    errors.Add($"Perk {perk.id} can never be learned: unlock it by default, have a companion teach it, or add a quest reward for it.");
            }

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
                    ItemData item = RequireItem(errors, design, entry.itemId, where);
                    if (item != null && item.category == ItemCategory.KeyItem && string.IsNullOrEmpty(entry.requiresQuest))
                        errors.Add($"{where}: key item {item.id} should only drop while its quest is active (set requiresQuest).");
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

                var delivered = new HashSet<string>();
                foreach (QuestObjective objective in quest.objectives)
                {
                    if (objective.qty < 1) errors.Add($"{where}: objective {objective.type} needs a quantity of at least 1.");
                    switch (objective.type)
                    {
                        case ObjectiveType.HaveItems:
                        case ObjectiveType.DeliverItems:
                            ItemData item = RequireItem(errors, design, objective.target, where);
                            if (item != null && item.unique && objective.qty > 1) errors.Add($"{where}: needs {objective.qty} of unique item {item.id}, but only one can exist.");
                            if (objective.type == ObjectiveType.DeliverItems && !delivered.Add(objective.target))
                                errors.Add($"{where}: delivers {objective.target} in two objectives; combine them into one.");
                            break;
                        case ObjectiveType.PlaceObject:
                            if (!string.IsNullOrEmpty(objective.target)) RequireItem(errors, design, objective.target, where);
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

            CheckPrerequisiteCycles(errors, design);
            return errors;
        }

        /// <summary>A chain like A needs B, B needs A means neither can ever start.</summary>
        private static void CheckPrerequisiteCycles(List<string> errors, DesignData design)
        {
            var state = new Dictionary<string, int>(); // 1 = visiting, 2 = done
            foreach (QuestData quest in design.quests) Visit(quest.id);

            bool Visit(string id)
            {
                state.TryGetValue(id, out int mark);
                if (mark == 2) return false;
                if (mark == 1)
                {
                    errors.Add($"Quest prerequisites loop back to {id}, so those quests can never start.");
                    return true;
                }

                state[id] = 1;
                QuestData quest = design.GetQuest(id);
                bool looped = false;
                if (quest != null)
                    foreach (string prerequisite in quest.prerequisites)
                        if (prerequisite != id && Visit(prerequisite)) { looped = true; break; }
                state[id] = 2;
                return looped;
            }
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

        private static IEnumerable<ForageModifier> AllModifiers(DesignData design)
        {
            foreach (CompanionData companion in design.companions)
                foreach (ForageModifier modifier in companion.modifiers) yield return modifier;
            foreach (PerkData perk in design.perks)
                foreach (ForageModifier modifier in perk.modifiers) yield return modifier;
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
