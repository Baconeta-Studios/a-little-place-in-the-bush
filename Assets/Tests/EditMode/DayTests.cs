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
            _game.cafe.AddToMenu(TestDesign.Salad);
            _game.economy.Add(TestDesign.Leaf, 4); // one salad
            DaySummary summary = _game.cafe.CloseDay();
            Assert.AreEqual(1, summary.sold);
            Assert.AreEqual(2, summary.Missed);
        }

        [Test]
        public void PlanSales_MatchesCloseDay_AndChangesNothing()
        {
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
            _game.quests.Start(TestDesign.Welcome); // cook 1 salad
            _game.cafe.AddToMenu(TestDesign.Salad);
            _game.economy.Add(TestDesign.Leaf, 3);
            _game.cafe.CloseDay();
            Assert.IsTrue(_game.quests.AllObjectivesMet(TestDesign.Welcome));
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
        public void Normalise_TidiesTheMenu()
        {
            _game.state.menu.AddRange(new[] { TestDesign.Salad, TestDesign.Salad, "recipe.gone", "" });
            _game.state.Normalise(_game.design);
            CollectionAssert.AreEqual(new[] { TestDesign.Salad }, _game.state.menu);
        }
    }
}
