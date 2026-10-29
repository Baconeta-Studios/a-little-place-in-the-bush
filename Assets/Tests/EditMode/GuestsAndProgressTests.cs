using NUnit.Framework;

namespace Bush.Core.Tests
{
    /// <summary>Visitors, companions getting tired and costing coins, perk slots, cafe upgrades, cosiness and the sign.</summary>
    public class GuestsAndProgressTests
    {
        private const string Weka = "npc.weka";
        private TestGame _game;

        [SetUp]
        public void SetUp()
        {
            _game = new TestGame();
            _game.state.luckSeed = 12345;
            _game.design.npcs.Find(n => n.id == TestDesign.Ruru).visits = VisitType.Story;
            _game.design.npcs.Add(new NpcData { id = Weka, name = "Weka", visits = VisitType.Random, visitChance = 0.5 });
            _game.design.RebuildIndex();
        }

        private void Place(string placeableId, float x)
        {
            _game.state.placed.Add(new PlacedObjectData { instanceId = _game.state.nextPlacedId++, area = CafeStyle.CafeArea, placeableId = placeableId, x = x });
        }

        // ----- Visitors -----

        [Test]
        public void StoryGuest_StaysWhileTheStoryIsTheirs()
        {
            Assert.IsTrue(Visitors.IsPresent(_game.design, _game.state, _game.quests, TestDesign.Ruru), "Ruru gives the first story quest.");
            // Finish every main quest: the story moves on, and so does Ruru.
            foreach (QuestData quest in _game.design.quests)
                if (quest.isMain) _game.state.quests.Add(new QuestProgress { questId = quest.id, status = QuestStatus.Completed });
            Assert.IsFalse(Visitors.IsPresent(_game.design, _game.state, _game.quests, TestDesign.Ruru));
        }

        [Test]
        public void RandomVisitors_AreRolledOncePerDay()
        {
            bool first = Visitors.IsPresent(_game.design, _game.state, _game.quests, Weka);
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(first, Visitors.IsPresent(_game.design, _game.state, _game.quests, Weka), "Same answer all day.");

            int days = 0, visits = 0;
            for (int d = 0; d < 200; d++)
            {
                _game.cafe.CloseDay();
                days++;
                if (Visitors.IsPresent(_game.design, _game.state, _game.quests, Weka)) visits++;
            }
            Assert.That(visits, Is.InRange(70, 130), "About half the days at a 50% chance.");
        }

        [Test]
        public void RandomVisitors_WaitForTheirDayAndQuest()
        {
            NpcData weka = _game.design.GetNpc(Weka);
            weka.firstDay = 3;
            Assert.AreEqual(0, Visitors.ChanceToday(_game.design, _game.state, weka));
            _game.state.day = 3;
            Assert.Greater(Visitors.ChanceToday(_game.design, _game.state, weka), 0);

            weka.afterQuest = TestDesign.Welcome;
            Assert.AreEqual(0, Visitors.ChanceToday(_game.design, _game.state, weka));
            _game.state.quests.Add(new QuestProgress { questId = TestDesign.Welcome, status = QuestStatus.Completed });
            Assert.Greater(Visitors.ChanceToday(_game.design, _game.state, weka), 0);
        }

        [Test]
        public void ACosyCafe_BringsMoreVisitors()
        {
            NpcData weka = _game.design.GetNpc(Weka);
            double bare = Visitors.ChanceToday(_game.design, _game.state, weka);
            _game.design.GetPlaceable(TestDesign.Fern).cosiness = 10;
            for (int i = 0; i < 4; i++) Place(TestDesign.Fern, i);
            Assert.Greater(Visitors.ChanceToday(_game.design, _game.state, weka), bare);
            Assert.LessOrEqual(Visitors.ChanceToday(_game.design, _game.state, weka), Visitors.MaxChance);
        }

        // ----- Companions and perks -----

        [Test]
        public void Companions_GetTired_AndCostCoins()
        {
            _game.design.GetCompanion(TestDesign.Kea).tripCost = 5;
            Assert.IsFalse(ForagingService.CanBegin(_game.design, _game.state, TestDesign.Setup(TestDesign.Kea), out string error));
            StringAssert.Contains("costs 5", error);

            _game.economy.AddCurrency(20);
            Assert.IsNotNull(ForagingService.Begin(_game.design, _game.state, TestDesign.Setup(TestDesign.Kea), 1u, false, _game.economy));
            Assert.AreEqual(15, _game.economy.Currency);
            ForagingService.Claim(_game.design, _game.state, _game.economy, _game.events);

            Assert.IsFalse(ForagingService.CanBegin(_game.design, _game.state, TestDesign.Setup(TestDesign.Kea), out error));
            StringAssert.Contains("tired", error);
            Assert.IsTrue(ForagingService.CanBegin(_game.design, _game.state, TestDesign.Setup(TestDesign.Weka), out _), "Others can still go.");

            _game.cafe.CloseDay();
            Assert.IsTrue(ForagingService.CanBegin(_game.design, _game.state, TestDesign.Setup(TestDesign.Kea), out _), "Rested by morning.");
        }

        [Test]
        public void PerkSlots_LimitHowManyPerksGo()
        {
            ExpeditionSetup setup = TestDesign.Setup(perk: TestDesign.BigBasket);
            _game.state.unlockedPerks.Add(TestDesign.BarkSense);
            setup.perkIds.Add(TestDesign.BarkSense);
            Assert.IsFalse(ForagingService.CanBegin(_game.design, _game.state, setup, out _), "One slot to start.");

            _game.design.rules.upgrades.Add(new UpgradeData { id = "upgrade.perk", effect = UpgradeEffect.PerkSlot, amount = 1, price = 10 });
            _game.state.upgrades.Add("upgrade.perk");
            Assert.IsTrue(ForagingService.CanBegin(_game.design, _game.state, setup, out _), "Perks stack with a second slot.");
        }

        [Test]
        public void ModifierText_IsReadable()
        {
            CompanionData kea = _game.design.GetCompanion(TestDesign.Kea);
            Assert.IsNotEmpty(ModifierText.Describe(kea.modifiers));
            Assert.AreEqual("more bark (x3)", ModifierText.Describe(new ForageModifier(ModifierType.TagWeight, 3, "bark", Rarity.Common)));
            Assert.AreEqual("+1 bark each find", ModifierText.Describe(new ForageModifier(ModifierType.TagQuantity, 1, "bark", Rarity.Common)));
            Assert.AreEqual("40% chance of a bonus shiny find", ModifierText.Describe(new ForageModifier(ModifierType.BonusRoll, 0.4, "shiny", Rarity.Common)));
            Assert.AreEqual("+1 find per trip", ModifierText.Describe(new ForageModifier(ModifierType.ExtraRolls, 1, "", Rarity.Common)));
            Assert.AreEqual("nothing special", ModifierText.Describe(new System.Collections.Generic.List<ForageModifier>()));
        }

        [Test]
        public void LuckProtection_FindsWhatTheQuestNeeds()
        {
            _game.design.quests.Add(new QuestData
            {
                id = "quest.leaves", title = "Leaves", giver = TestDesign.Ruru,
                objectives = { new QuestObjective { type = ObjectiveType.HaveItems, target = TestDesign.Leaf, qty = 6 } }
            });
            _game.design.RebuildIndex();
            ExpeditionSetup setup = TestDesign.Setup();
            Assert.AreEqual(77u, ForagingService.LuckySeed(_game.design, _game.state, setup, 77u), "Off until it's set up.");

            _game.design.rules.luckProtectedQuest = "quest.leaves";
            Assert.AreEqual(77u, ForagingService.LuckySeed(_game.design, _game.state, setup, 77u), "Only while the quest is under way.");

            _game.state.quests.Add(new QuestProgress { questId = "quest.leaves", status = QuestStatus.Active });
            _game.economy.Add(TestDesign.Leaf, 1);
            uint seed = ForagingService.LuckySeed(_game.design, _game.state, setup, 77u);
            ForageResult result = ForagingService.Resolve(_game.design, setup, new GameStateForageContext(_game.state), seed);
            Assert.GreaterOrEqual(result.stacks.Find(s => s.itemId == TestDesign.Leaf)?.qty ?? 0, 5, "Finds the 5 still needed.");

            _game.economy.Add(TestDesign.Leaf, 5);
            Assert.AreEqual(77u, ForagingService.LuckySeed(_game.design, _game.state, setup, 77u), "Nothing needed once you have them.");
        }

        // ----- Upgrades -----

        [Test]
        public void Upgrades_CostCoins_ComeInOrder_AndTakeEffect()
        {
            _game.design.rules.menuSlots = 1;
            _game.design.rules.upgrades.Add(new UpgradeData { id = "upgrade.slot2", name = "Second slot", effect = UpgradeEffect.MenuSlot, amount = 1, price = 100 });
            _game.design.rules.upgrades.Add(new UpgradeData { id = "upgrade.slot3", name = "Third slot", effect = UpgradeEffect.MenuSlot, amount = 1, price = 250, requires = "upgrade.slot2" });

            Assert.IsFalse(_game.cafe.CanBuyUpgrade("upgrade.slot2", out string reason));
            StringAssert.Contains("100", reason);
            _game.economy.AddCurrency(400);
            Assert.IsFalse(_game.cafe.CanBuyUpgrade("upgrade.slot3", out reason), "Needs the second slot first.");
            StringAssert.Contains("Second slot", reason);

            Assert.IsTrue(_game.cafe.BuyUpgrade("upgrade.slot2"));
            Assert.AreEqual(300, _game.economy.Currency);
            Assert.AreEqual(2, _game.design.rules.MenuSlots(_game.state));
            Assert.IsFalse(_game.cafe.BuyUpgrade("upgrade.slot2"), "Once only.");
            Assert.IsTrue(_game.cafe.BuyUpgrade("upgrade.slot3"));
            Assert.AreEqual(3, _game.design.rules.MenuSlots(_game.state));
        }

        [Test]
        public void TripUpgrade_LengthensTheDay()
        {
            _game.design.rules.forageTripsPerDay = 3;
            _game.design.rules.upgrades.Add(new UpgradeData { id = "upgrade.trip", effect = UpgradeEffect.ForageTrip, amount = 1, price = 10 });
            _game.state.upgrades.Add("upgrade.trip");
            Assert.AreEqual(4, ForagingService.TripsLeftToday(_game.design, _game.state));
            _game.state.forageTripsToday = 3;
            Assert.IsFalse(_game.design.rules.IsEvening(_game.state));
        }

        // ----- Cosiness, lucky days and the sign -----

        [Test]
        public void Cosiness_AddsUp_WithABitForVariety()
        {
            _game.design.GetPlaceable(TestDesign.Chair).cosiness = 2;
            _game.design.GetPlaceable(TestDesign.Fern).cosiness = 3;
            Place(TestDesign.Chair, 0);
            Place(TestDesign.Chair, 1);
            Place(TestDesign.Fern, 2);
            Assert.AreEqual(2 + 2 + 3 + 2, CafeStyle.Cosiness(_game.design, _game.state), "Items plus 1 per different kind.");
        }

        [Test]
        public void LuckyDays_AreRareInABareCafe_AndPayATip()
        {
            Assert.AreEqual(0, CafeStyle.LuckyDayChance(_game.design, _game.state));

            _game.design.GetPlaceable(TestDesign.Fern).cosiness = 100;
            for (int i = 0; i < 3; i++) Place(TestDesign.Fern, i);
            _game.design.rules = new DayRules { salesPerDay = 1, menuSlots = 1 };
            _game.cafe.AddToMenu(TestDesign.Salad);
            int tips = 0;
            for (int d = 0; d < 100; d++)
            {
                _game.economy.Add(TestDesign.Leaf, 3);
                if (_game.cafe.CloseDay().tip > 0) tips++;
            }
            Assert.That(tips, Is.InRange(12, 40), "About 25% at the cap.");
        }

        [Test]
        public void TheSign_DescribesTheCafe()
        {
            Assert.AreEqual("An empty clearing", CafeStyle.Describe(_game.design, _game.state).title);

            _game.design.GetPlaceable(TestDesign.Fern).style = "garden";
            _game.design.GetPlaceable(TestDesign.Fern).cosiness = 3;
            for (int i = 0; i < 3; i++) Place(TestDesign.Fern, i);
            CafeDescription garden = CafeStyle.Describe(_game.design, _game.state);
            Assert.AreEqual("A garden cafe", garden.title);
            StringAssert.Contains("Nowhere to sit", garden.blurb);

            _game.design.GetPlaceable(TestDesign.Chair).style = "rustic";
            _game.design.GetPlaceable(TestDesign.Chair).cosiness = 3;
            for (int i = 0; i < 3; i++) Place(TestDesign.Chair, 5 + i);
            Assert.AreEqual("An eclectic little cafe", CafeStyle.Describe(_game.design, _game.state).title, "Even mix.");
            Assert.AreEqual(3, CafeStyle.Describe(_game.design, _game.state).seats);
        }
    }
}
