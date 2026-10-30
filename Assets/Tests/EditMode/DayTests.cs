using NUnit.Framework;

namespace Bush.Core.Tests
{
    /// <summary>The cafe day: the menu, closing for the day, and the forage limit.</summary>
    public class DayTests
    {
        private TestGame _game;

        [SetUp]
        public void SetUp()
        {
            _game = new TestGame();
            _game.design.rules = new DayRules { salesPerDay = 3, menuSlots = 2, forageTripsPerDay = 2 };
        }

        private void LearnPie() => _game.state.unlockedRecipes.Add(TestDesign.SecretPie);

        /// <summary>Both of the day's trips taken: it's evening, so closing gets a full day's sales.</summary>
        private void Evening() => _game.state.forageTripsToday = 2;

        [Test]
        public void Menu_OnlyTakesKnownRecipes_UpToTheSlots()
        {
            Assert.IsFalse(_game.cafe.AddToMenu(TestDesign.SecretPie), "Not known yet.");
            Assert.IsTrue(_game.cafe.AddToMenu(TestDesign.Salad));
            Assert.IsFalse(_game.cafe.AddToMenu(TestDesign.Salad), "Already on it.");
            LearnPie();
            Assert.IsTrue(_game.cafe.AddToMenu(TestDesign.SecretPie));
            Assert.IsTrue(_game.cafe.MenuIsFull);
            Assert.IsTrue(_game.cafe.RemoveFromMenu(TestDesign.Salad));
            CollectionAssert.AreEqual(new[] { TestDesign.SecretPie }, _game.cafe.Menu);
        }

        [Test]
        public void CloseDay_SharesSalesEvenly_AndPays()
        {
            Evening();
            LearnPie();
            _game.cafe.AddToMenu(TestDesign.Salad);
            _game.cafe.AddToMenu(TestDesign.SecretPie);
            _game.economy.Add(TestDesign.Leaf, 9);
            _game.economy.Add(TestDesign.Berry, 4);
            _game.economy.Add(TestDesign.Bug, 2);

            DaySummary summary = _game.cafe.CloseDay();

            // 3 sales: salad, pie, salad.
            Assert.AreEqual(3, summary.sold);
            Assert.AreEqual(2, summary.dishes.Find(d => d.recipeId == TestDesign.Salad).sold);
            Assert.AreEqual(1, summary.dishes.Find(d => d.recipeId == TestDesign.SecretPie).sold);
            Assert.AreEqual(5 + 40 + 5, summary.coins);
            Assert.AreEqual(50, _game.economy.Currency);
            Assert.AreEqual(3, _game.economy.Count(TestDesign.Leaf));
            Assert.AreEqual(0, summary.Missed);
        }

        [Test]
        public void CloseDay_GivesSalesToWhatCanStillBeMade()
        {
            Evening();
            LearnPie();
            _game.cafe.AddToMenu(TestDesign.SecretPie);
            _game.cafe.AddToMenu(TestDesign.Salad);
            _game.economy.Add(TestDesign.Leaf, 9); // salad x3
            // No pie ingredients: all 3 sales go to salad.
            DaySummary summary = _game.cafe.CloseDay();
            Assert.AreEqual(3, summary.dishes.Find(d => d.recipeId == TestDesign.Salad).sold);
            Assert.AreEqual(0, summary.dishes.Find(d => d.recipeId == TestDesign.SecretPie).sold);
        }

        [Test]
        public void CloseDay_RunningOut_MissesSales()
        {
            Evening();
            _game.cafe.AddToMenu(TestDesign.Salad);
            _game.economy.Add(TestDesign.Leaf, 4); // one salad
            DaySummary summary = _game.cafe.CloseDay();
            Assert.AreEqual(1, summary.sold);
            Assert.AreEqual(2, summary.Missed);
        }

        [Test]
        public void PlanSales_MatchesCloseDay_AndChangesNothing()
        {
            Evening();
            _game.cafe.AddToMenu(TestDesign.Salad);
            _game.economy.Add(TestDesign.Leaf, 7);
            var plan = _game.cafe.PlanSales(out int unsold);
            Assert.AreEqual(2, plan[0].sold);
            Assert.AreEqual(1, unsold);
            Assert.AreEqual(7, _game.economy.Count(TestDesign.Leaf));
            Assert.AreEqual(2, _game.cafe.CloseDay().sold);
        }

        [Test]
        public void CloseDay_NextMorning_KeepsTheMenu_ResetsTrips()
        {
            _game.cafe.AddToMenu(TestDesign.Salad);
            _game.state.forageTripsToday = 2;
            DaySummary ended = null;
            _game.events.DayEnded += s => ended = s;

            _game.cafe.CloseDay();

            Assert.AreEqual(2, _game.state.day);
            Assert.AreEqual(0, _game.state.forageTripsToday);
            Assert.AreEqual(1, ended.day);
            CollectionAssert.AreEqual(new[] { TestDesign.Salad }, _game.cafe.Menu);
        }

        [Test]
        public void SalesCountAsCooking_ForQuests()
        {
            Evening();
            _game.quests.Start(TestDesign.Welcome); // cook 1 salad
            _game.cafe.AddToMenu(TestDesign.Salad);
            _game.economy.Add(TestDesign.Leaf, 3);
            _game.cafe.CloseDay();
            Assert.IsTrue(_game.quests.AllObjectivesMet(TestDesign.Welcome));
        }

        [Test]
        public void ClosingEarly_SellsTheShareOfTheDaySoFar_RoundedDown()
        {
            _game.cafe.AddToMenu(TestDesign.Salad);
            _game.economy.Add(TestDesign.Leaf, 30);

            Assert.AreEqual(0, _game.design.rules.SalesFor(_game.state), "Closing first thing sells nothing.");
            _game.state.forageTripsToday = 1;
            Assert.AreEqual(1, _game.design.rules.SalesFor(_game.state), "Half the day: 3 / 2 rounds down to 1.");
            Assert.IsFalse(_game.design.rules.IsEvening(_game.state));

            DaySummary summary = _game.cafe.CloseDay();
            Assert.IsTrue(summary.closedEarly);
            Assert.AreEqual(1, summary.sold);
            Assert.AreEqual(3, summary.fullDaySales);
            Assert.AreEqual(0, summary.Missed, "Missed counts only sales lost to running out.");
            Assert.AreEqual(0, _game.state.forageTripsToday, "The unused trip is gone.");
        }

        [Test]
        public void Evening_SellsAFullDay()
        {
            _game.cafe.AddToMenu(TestDesign.Salad);
            _game.economy.Add(TestDesign.Leaf, 30);
            Evening();
            Assert.IsTrue(_game.design.rules.IsEvening(_game.state));
            DaySummary summary = _game.cafe.CloseDay();
            Assert.IsFalse(summary.closedEarly);
            Assert.AreEqual(3, summary.sold);
        }

        [Test]
        public void NoDayClock_WithoutAForageLimit()
        {
            _game.design.rules.forageTripsPerDay = 0;
            Assert.IsTrue(_game.design.rules.IsEvening(_game.state));
            Assert.AreEqual(3, _game.design.rules.SalesFor(_game.state));
        }

        [Test]
        public void Foraging_IsLimitedPerDay()
        {
            for (int trip = 0; trip < 2; trip++)
            {
                Assert.IsNotNull(ForagingService.Begin(_game.design, _game.state, TestDesign.Setup(), 1u));
                ForagingService.Claim(_game.design, _game.state, _game.economy, _game.events);
            }
            Assert.AreEqual(0, ForagingService.TripsLeftToday(_game.design, _game.state));
            Assert.IsFalse(ForagingService.CanBegin(_game.design, _game.state, TestDesign.Setup(), out _));
            Assert.IsNull(ForagingService.Begin(_game.design, _game.state, TestDesign.Setup(), 1u));

            _game.cafe.CloseDay();
            Assert.AreEqual(2, ForagingService.TripsLeftToday(_game.design, _game.state));
        }

        [Test]
        public void NoForageLimit_ByDefault()
        {
            Assert.AreEqual(-1, ForagingService.TripsLeftToday(TestDesign.Create(), new GameState()));
        }

        [Test]
        public void OneSlot_OneDishADay()
        {
            _game.design.rules.menuSlots = 1;
            LearnPie();
            Assert.IsTrue(_game.cafe.AddToMenu(TestDesign.Salad));
            Assert.IsFalse(_game.cafe.AddToMenu(TestDesign.SecretPie), "Only one slot.");
            Assert.IsTrue(_game.cafe.MenuIsFull);
        }

        [Test]
        public void FewerSlots_TrimsAnOldMenu()
        {
            _game.state.menu.AddRange(new[] { TestDesign.Salad, TestDesign.SecretPie });
            LearnPie();
            _game.design.rules.menuSlots = 1;
            _game.economy.Add(TestDesign.Berry, 10);
            _game.economy.Add(TestDesign.Bug, 10);
            Evening();
            Assert.AreEqual(1, _game.cafe.PlanSales(out _).Count, "Only the first slot sells.");
            _game.state.Normalise(_game.design);
            CollectionAssert.AreEqual(new[] { TestDesign.Salad }, _game.state.menu);
        }

        [Test]
        public void Night_ComesAfterClosing_OnceUnlocked()
        {
            _game.cafe.CloseDay();
            Assert.AreEqual(2, _game.state.day, "No night yet: straight to the next morning.");
            Assert.IsFalse(_game.state.isNight);

            _game.state.nightUnlocked = true;
            DaySummary summary = _game.cafe.CloseDay();
            Assert.IsTrue(summary.nightFalls);
            Assert.IsTrue(_game.state.isNight);
            Assert.AreEqual(2, _game.state.day, "Still the same day until bed.");
            Assert.AreEqual(0, _game.cafe.CloseDay().sold, "Already closed for the night.");

            Assert.IsTrue(_game.cafe.GoToBed());
            Assert.IsFalse(_game.state.isNight);
            Assert.AreEqual(3, _game.state.day);
            Assert.IsFalse(_game.cafe.GoToBed(), "Only at night.");
        }

        [Test]
        public void Night_HasOneForageTrip_WithNightFinds()
        {
            _game.state.nightUnlocked = true;
            _game.state.forageTripsToday = 2; // the day's trips are used up
            _game.cafe.CloseDay();
            Assert.AreEqual(1, ForagingService.TripsLeftToday(_game.design, _game.state));
            Assert.IsNotNull(ForagingService.Begin(_game.design, _game.state, TestDesign.Setup(time: TimeOfDay.Night), 1u));
            ForagingService.Claim(_game.design, _game.state, _game.economy, _game.events);
            Assert.AreEqual(0, ForagingService.TripsLeftToday(_game.design, _game.state));
            Assert.IsFalse(ForagingService.CanBegin(_game.design, _game.state, TestDesign.Setup(time: TimeOfDay.Night), out string error));
            StringAssert.Contains("bed", error);

            _game.cafe.GoToBed();
            Assert.AreEqual(2, ForagingService.TripsLeftToday(_game.design, _game.state), "A fresh day's trips.");
        }

        [Test]
        public void NightUnlock_IsAQuestReward()
        {
            _game.design.quests.Find(q => q.id == TestDesign.Welcome).rewards.Add(new QuestReward { type = RewardType.UnlockNight });
            _game.quests.Start(TestDesign.Welcome);
            _game.economy.Add(TestDesign.Leaf, 3);
            _game.cafe.Cook(TestDesign.Salad);
            Assert.IsTrue(_game.quests.Claim(TestDesign.Welcome));
            Assert.IsTrue(_game.state.nightUnlocked);
        }

        [Test]
        public void Normalise_TidiesTheMenu()
        {
            _game.state.menu.AddRange(new[] { TestDesign.Salad, TestDesign.Salad, "recipe.gone", "" });
            _game.state.Normalise(_game.design);
            CollectionAssert.AreEqual(new[] { TestDesign.Salad }, _game.state.menu);
        }
    }
}
