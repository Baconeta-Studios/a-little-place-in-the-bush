using NUnit.Framework;

namespace Bush.Core.Tests
{
    public class QuestAndCafeTests
    {
        private TestGame _game;

        [SetUp]
        public void SetUp() => _game = new TestGame();

        [Test]
        public void Cook_UsesIngredientsAndPaysOnce()
        {
            _game.economy.Add(TestDesign.Leaf, 4);
            string cooked = null;
            _game.events.RecipeCooked += id => cooked = id;

            Assert.IsTrue(_game.cafe.Cook(TestDesign.Salad));
            Assert.AreEqual(1, _game.economy.Count(TestDesign.Leaf));
            Assert.AreEqual(5, _game.economy.Currency);
            Assert.AreEqual(TestDesign.Salad, cooked);

            Assert.IsFalse(_game.cafe.Cook(TestDesign.Salad), "Only 1 leaf left.");
            Assert.AreEqual(5, _game.economy.Currency);
        }

        [Test]
        public void CanCook_ReportsWhatsMissing()
        {
            _game.economy.Add(TestDesign.Leaf, 1);
            Assert.IsFalse(_game.cafe.CanCook(TestDesign.Salad, out var missing));
            Assert.AreEqual(1, missing.Count);
            Assert.AreEqual(2, missing[0].qty);
        }

        [Test]
        public void LockedRecipes_CantBeCooked()
        {
            _game.economy.Add(TestDesign.Berry, 5);
            _game.economy.Add(TestDesign.Bug, 5);
            Assert.IsFalse(_game.cafe.Cook(TestDesign.SecretPie));
        }

        [Test]
        public void Quest_NeedsPrerequisites()
        {
            Assert.IsFalse(_game.quests.Start(TestDesign.LostRecipe));
            Assert.IsTrue(_game.quests.Start(TestDesign.Welcome));
            Assert.IsFalse(_game.quests.Start(TestDesign.Welcome), "Can't start twice.");
        }

        [Test]
        public void CookObjective_OnlyCountsWhileActive()
        {
            _game.economy.Add(TestDesign.Leaf, 6);
            _game.cafe.Cook(TestDesign.Salad); // before the quest: doesn't count
            _game.quests.Start(TestDesign.Welcome);
            Assert.AreEqual(QuestStatus.Active, _game.quests.GetStatus(TestDesign.Welcome));

            _game.cafe.Cook(TestDesign.Salad);
            Assert.AreEqual(QuestStatus.ReadyToClaim, _game.quests.GetStatus(TestDesign.Welcome));
        }

        [Test]
        public void Claim_PaysOnce()
        {
            CompleteWelcome();
            Assert.AreEqual(QuestStatus.Completed, _game.quests.GetStatus(TestDesign.Welcome));
            int coins = _game.economy.Currency;
            Assert.IsFalse(_game.quests.Claim(TestDesign.Welcome));
            Assert.AreEqual(coins, _game.economy.Currency);
        }

        [Test]
        public void Claim_FailsWhenNotReady()
        {
            _game.quests.Start(TestDesign.Welcome);
            Assert.IsFalse(_game.quests.Claim(TestDesign.Welcome));
            Assert.AreEqual(QuestStatus.Active, _game.quests.GetStatus(TestDesign.Welcome));
        }

        [Test]
        public void RecipeScraps_DeliveredTogetherUnlockTheRecipe()
        {
            CompleteWelcome();
            _game.quests.Start(TestDesign.LostRecipe);
            _game.quests.Start(TestDesign.Trade);

            // Scrap 1 from foraging, scrap 2 from trading two buttons.
            _game.economy.Add(TestDesign.Scrap1, 1);
            Assert.AreEqual((1, 1), _game.quests.GetProgress(TestDesign.LostRecipe, 0));
            Assert.AreEqual(QuestStatus.Active, _game.quests.GetStatus(TestDesign.LostRecipe));

            _game.economy.Add(TestDesign.Button, 2);
            Assert.IsTrue(_game.quests.Claim(TestDesign.Trade));
            Assert.AreEqual(0, _game.economy.Count(TestDesign.Button));
            Assert.AreEqual(1, _game.economy.Count(TestDesign.Scrap2));
            Assert.AreEqual((2, 2), _game.cafe.GetScrapProgress(TestDesign.SecretPie));

            Assert.AreEqual(QuestStatus.ReadyToClaim, _game.quests.GetStatus(TestDesign.LostRecipe));
            Assert.IsTrue(_game.quests.Claim(TestDesign.LostRecipe));
            Assert.AreEqual(0, _game.economy.Count(TestDesign.Scrap1), "Scraps are handed in.");
            Assert.AreEqual(0, _game.economy.Count(TestDesign.Scrap2));
            Assert.IsTrue(_game.cafe.IsUnlocked(TestDesign.SecretPie));
            Assert.IsTrue(_game.quests.IsMainStoryComplete());
        }

        [Test]
        public void DeliverObjective_GoesBackToActiveIfItemsAreSpent()
        {
            CompleteWelcome();
            _game.quests.Start(TestDesign.Trade);
            _game.economy.Add(TestDesign.Button, 2);
            Assert.AreEqual(QuestStatus.ReadyToClaim, _game.quests.GetStatus(TestDesign.Trade));

            _game.economy.TryRemove(TestDesign.Button, 1);
            Assert.AreEqual(QuestStatus.Active, _game.quests.GetStatus(TestDesign.Trade));
            Assert.IsFalse(_game.quests.Claim(TestDesign.Trade));
        }

        [Test]
        public void Dialogue_FollowsQuestState()
        {
            QuestData quest = _game.design.GetQuest(TestDesign.Welcome);
            quest.dialogue = new QuestDialogue { offer = "offer", active = "active", ready = "ready", done = "done" };

            Assert.AreEqual("offer", _game.quests.GetDialogue(TestDesign.Welcome));
            _game.quests.Start(TestDesign.Welcome);
            Assert.AreEqual("active", _game.quests.GetDialogue(TestDesign.Welcome));
            _game.economy.Add(TestDesign.Leaf, 3);
            _game.cafe.Cook(TestDesign.Salad);
            Assert.AreEqual("ready", _game.quests.GetDialogue(TestDesign.Welcome));
            _game.quests.Claim(TestDesign.Welcome);
            Assert.AreEqual("done", _game.quests.GetDialogue(TestDesign.Welcome));
        }

        [Test]
        public void NewGame_HasDefaultUnlocks()
        {
            CollectionAssert.Contains(_game.state.unlockedRecipes, TestDesign.Salad);
            CollectionAssert.DoesNotContain(_game.state.unlockedRecipes, TestDesign.SecretPie);
            CollectionAssert.Contains(_game.state.unlockedPerks, TestDesign.BigBasket);
            CollectionAssert.Contains(_game.state.unlockedCompanions, TestDesign.Kea);
        }

        private void CompleteWelcome()
        {
            _game.quests.Start(TestDesign.Welcome);
            _game.economy.Add(TestDesign.Leaf, 3);
            _game.cafe.Cook(TestDesign.Salad);
            Assert.IsTrue(_game.quests.Claim(TestDesign.Welcome));
        }
    }
}
