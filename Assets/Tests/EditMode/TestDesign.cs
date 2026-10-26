using System.Collections.Generic;

namespace Bush.Core.Tests
{
    /// <summary>A small, fixed design for tests, so they don't break when the real design data is tuned.</summary>
    public static class TestDesign
    {
        public const string Leaf = "item.leaf";
        public const string Berry = "item.berry";
        public const string Bug = "item.bug";
        public const string Glowworm = "item.glowworm";
        public const string Bark = "item.bark";
        public const string Button = "item.button";
        public const string Gem = "item.gem";
        public const string Scrap1 = "item.scrap_1";
        public const string Scrap2 = "item.scrap_2";

        public const string Table = "table.test";
        public const string Kea = "companion.kea";
        public const string Weka = "companion.weka";
        public const string BigBasket = "perk.big_basket";
        public const string BarkSense = "perk.bark_sense";
        public const string FocusBugs = "focus.bugs";

        public const string Salad = "recipe.salad";
        public const string SecretPie = "recipe.secret_pie";

        public const string Ruru = "npc.ruru";
        public const string Welcome = "quest.welcome";
        public const string LostRecipe = "quest.lost_recipe";
        public const string Trade = "quest.trade";

        public static DesignData Create()
        {
            var design = new DesignData();
            design.items.AddRange(new[]
            {
                Item(Leaf, ItemCategory.Ingredient, Rarity.Common, "leaf"),
                Item(Berry, ItemCategory.Ingredient, Rarity.Common, "fruit"),
                Item(Bug, ItemCategory.Ingredient, Rarity.Common, "insect"),
                Item(Glowworm, ItemCategory.Ingredient, Rarity.Rare, "insect"),
                Item(Bark, ItemCategory.Material, Rarity.Common, "bark"),
                Item(Button, ItemCategory.Treasure, Rarity.Uncommon, "shiny"),
                Item(Gem, ItemCategory.Treasure, Rarity.Rare, "shiny"),
                Item(Scrap1, ItemCategory.KeyItem, Rarity.Rare, "scrap", unique: true),
                Item(Scrap2, ItemCategory.KeyItem, Rarity.Rare, "scrap", unique: true),
            });

            design.forageTables.Add(new ForageTableData
            {
                id = Table, name = "Test bush", rolls = 3,
                entries = new List<ForageEntry>
                {
                    Entry(Leaf, 30, 2, 4),
                    Entry(Berry, 20, 1, 3),
                    Entry(Bug, 20, 1, 2),
                    Entry(Glowworm, 5, 1, 1, EntryTime.Night),
                    Entry(Bark, 15, 1, 2),
                    Entry(Button, 4, 1, 1),
                    Entry(Gem, 1, 1, 1),
                    Entry(Scrap1, 10, 1, 1, EntryTime.Any, LostRecipe),
                }
            });

            design.perks.Add(new PerkData { id = BigBasket, name = "Big basket", unlockedByDefault = true, modifiers = { new ForageModifier(ModifierType.ExtraRolls, 1) } });
            design.perks.Add(new PerkData { id = BarkSense, name = "Bark sense", modifiers = { new ForageModifier(ModifierType.TagQuantity, 1, "bark") } });

            design.companions.Add(new CompanionData
            {
                id = Kea, name = "Kea", unlockedByDefault = true, teachesPerk = BarkSense, tripsToTeach = 2,
                modifiers = { new ForageModifier(ModifierType.TagWeight, 3, "bark"), new ForageModifier(ModifierType.TagQuantity, 1, "bark") }
            });
            design.companions.Add(new CompanionData
            {
                id = Weka, name = "Weka", unlockedByDefault = true,
                modifiers = { new ForageModifier(ModifierType.BonusRoll, 0.5, "shiny") }
            });

            design.focuses.Add(new FocusData { id = FocusBugs, name = "Bugs", tags = { "insect" }, multiplier = 2 });

            design.recipes.Add(new RecipeData { id = Salad, name = "Salad", coins = 5, unlockedByDefault = true, ingredients = { new ItemAmount(Leaf, 3) } });
            design.recipes.Add(new RecipeData
            {
                id = SecretPie, name = "Secret pie", coins = 40, ingredients = { new ItemAmount(Berry, 2), new ItemAmount(Bug, 1) },
                scraps = { Scrap1, Scrap2 }
            });

            design.npcs.Add(new NpcData { id = Ruru, name = "Ruru" });

            design.quests.Add(new QuestData
            {
                id = Welcome, title = "Welcome", giver = Ruru, isMain = true,
                objectives = { new QuestObjective { type = ObjectiveType.CookRecipe, target = Salad, qty = 1 } },
                rewards = { new QuestReward { type = RewardType.Coins, qty = 20 } }
            });
            design.quests.Add(new QuestData
            {
                id = LostRecipe, title = "Lost recipe", giver = Ruru, isMain = true, prerequisites = { Welcome },
                objectives =
                {
                    new QuestObjective { type = ObjectiveType.DeliverItems, target = Scrap1, qty = 1 },
                    new QuestObjective { type = ObjectiveType.DeliverItems, target = Scrap2, qty = 1 }
                },
                rewards = { new QuestReward { type = RewardType.UnlockRecipe, target = SecretPie }, new QuestReward { type = RewardType.Coins, qty = 50 } }
            });
            design.quests.Add(new QuestData
            {
                id = Trade, title = "Trade", giver = Ruru, prerequisites = { Welcome },
                objectives = { new QuestObjective { type = ObjectiveType.DeliverItems, target = Button, qty = 2 } },
                rewards = { new QuestReward { type = RewardType.Item, target = Scrap2, qty = 1 } }
            });

            design.RebuildIndex();
            return design;
        }

        public static ExpeditionSetup Setup(string companion = null, string perk = null, string focus = null, TimeOfDay time = TimeOfDay.Day)
        {
            var setup = new ExpeditionSetup { tableId = Table, companionId = companion, focusId = focus, time = time };
            if (perk != null) setup.perkIds.Add(perk);
            return setup;
        }

        private static ItemData Item(string id, ItemCategory category, Rarity rarity, string tag, bool unique = false) =>
            new() { id = id, name = id, category = category, rarity = rarity, tags = { tag }, unique = unique, sellValue = 1 };

        private static ForageEntry Entry(string item, double weight, int min, int max, EntryTime time = EntryTime.Any, string quest = "") =>
            new() { itemId = item, weight = weight, min = min, max = max, time = time, requiresQuest = quest };
    }

    /// <summary>A game wired up the way GameSession will do it.</summary>
    public class TestGame
    {
        public readonly DesignData design;
        public readonly GameState state;
        public readonly GameEvents events = new();
        public readonly Economy economy;
        public readonly CafeService cafe;
        public readonly QuestService quests;

        public TestGame(DesignData design = null)
        {
            this.design = design ?? TestDesign.Create();
            state = GameState.CreateNew(this.design);
            economy = new Economy(state, this.design, events);
            cafe = new CafeService(this.design, state, economy, events);
            quests = new QuestService(this.design, state, economy, events);
        }
    }
}
