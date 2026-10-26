using NUnit.Framework;

namespace Bush.Core.Tests
{
    public class EconomyTests
    {
        private TestGame _game;
        private Economy Economy => _game.economy;

        [SetUp]
        public void SetUp() => _game = new TestGame();

        [Test]
        public void Add_StacksSameItem()
        {
            Assert.IsTrue(Economy.Add(TestDesign.Leaf, 2));
            Assert.IsTrue(Economy.Add(TestDesign.Leaf, 3));
            Assert.AreEqual(5, Economy.Count(TestDesign.Leaf));
            Assert.AreEqual(1, _game.state.inventory.Count);
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void Add_RejectsZeroOrNegative(int qty)
        {
            Assert.IsFalse(Economy.Add(TestDesign.Leaf, qty));
            Assert.AreEqual(0, Economy.Count(TestDesign.Leaf));
        }

        [Test]
        public void Add_RejectsUnknownItem()
        {
            Assert.IsFalse(Economy.Add("item.nope", 1));
            Assert.IsEmpty(_game.state.inventory);
        }

        [Test]
        public void TryRemove_FailsWhenShortAndChangesNothing()
        {
            Economy.Add(TestDesign.Leaf, 2);
            Assert.IsFalse(Economy.TryRemove(TestDesign.Leaf, 3));
            Assert.AreEqual(2, Economy.Count(TestDesign.Leaf));
        }

        [Test]
        public void TryRemove_RemovesEmptyStack()
        {
            Economy.Add(TestDesign.Leaf, 2);
            Assert.IsTrue(Economy.TryRemove(TestDesign.Leaf, 2));
            Assert.IsEmpty(_game.state.inventory);
        }

        [Test]
        public void TryTransact_IsAllOrNothing()
        {
            Economy.Add(TestDesign.Leaf, 5);
            _game.state.currency = 3;

            // Enough leaves, not enough coins: nothing should change.
            var transaction = new Transaction { coinCost = 10 }.Cost(TestDesign.Leaf, 2).Gain(TestDesign.Berry, 1);
            Assert.IsFalse(Economy.TryTransact(transaction));
            Assert.AreEqual(5, Economy.Count(TestDesign.Leaf));
            Assert.AreEqual(0, Economy.Count(TestDesign.Berry));
            Assert.AreEqual(3, Economy.Currency);
        }

        [Test]
        public void TryTransact_TotalsRepeatedCosts()
        {
            Economy.Add(TestDesign.Leaf, 3);
            var transaction = new Transaction().Cost(TestDesign.Leaf, 2).Cost(TestDesign.Leaf, 2);
            Assert.IsFalse(Economy.TryTransact(transaction), "Two costs of 2 must not pass on a stack of 3.");
            Assert.AreEqual(3, Economy.Count(TestDesign.Leaf));
        }

        [Test]
        public void UniqueItems_AreCappedAtOne()
        {
            Assert.IsTrue(Economy.Add(TestDesign.Scrap1, 3));
            Assert.AreEqual(1, Economy.Count(TestDesign.Scrap1));
            Economy.Add(TestDesign.Scrap1, 1);
            Assert.AreEqual(1, Economy.Count(TestDesign.Scrap1));
            Assert.IsTrue(Economy.IsUniqueCollected(TestDesign.Scrap1));
        }

        [Test]
        public void KeyItems_CanOnlyLeaveThroughAQuestHandIn()
        {
            Economy.Add(TestDesign.Scrap1, 1);
            Assert.IsFalse(Economy.TryRemove(TestDesign.Scrap1, 1), "A scrap must not be lost before its quest.");
            Assert.IsFalse(Economy.TrySell(TestDesign.Scrap1, 1));
            Assert.AreEqual(1, Economy.Count(TestDesign.Scrap1));

            Assert.IsTrue(Economy.TryTransact(new Transaction { questHandIn = true }.Cost(TestDesign.Scrap1, 1)));
            Assert.AreEqual(0, Economy.Count(TestDesign.Scrap1));
            Assert.IsTrue(Economy.IsUniqueCollected(TestDesign.Scrap1), "Handed in still counts as collected.");

            Economy.Add(TestDesign.Scrap1, 1);
            Assert.AreEqual(0, Economy.Count(TestDesign.Scrap1), "A handed-in unique never comes back.");
        }

        [Test]
        public void Sell_PaysSellValueForNormalItems()
        {
            Economy.Add(TestDesign.Button, 2);
            Assert.IsTrue(Economy.TrySell(TestDesign.Button, 2));
            Assert.AreEqual(0, Economy.Count(TestDesign.Button));
            Assert.AreEqual(2, Economy.Currency, "Test items are worth 1 each.");
            Assert.IsFalse(Economy.TrySell(TestDesign.Button, 1), "None left.");
        }

        [Test]
        public void Normalise_MergesStacksAndDropsEmptyOnes()
        {
            _game.state.inventory.Add(new ItemStack(TestDesign.Leaf, 2));
            _game.state.inventory.Add(new ItemStack(TestDesign.Leaf, 3));
            _game.state.inventory.Add(new ItemStack(TestDesign.Berry, 0));
            _game.state.Normalise();
            Assert.AreEqual(1, _game.state.inventory.Count);
            Assert.AreEqual(5, Economy.Count(TestDesign.Leaf));
        }

        [Test]
        public void Currency_SpendAndEarn()
        {
            Assert.IsFalse(Economy.TrySpend(1));
            Assert.IsTrue(Economy.AddCurrency(10));
            Assert.IsTrue(Economy.TrySpend(4));
            Assert.AreEqual(6, Economy.Currency);
            Assert.IsFalse(Economy.AddCurrency(-5));
            Assert.IsFalse(Economy.TrySpend(0));
        }

        [Test]
        public void Events_FireOnChange()
        {
            int inventoryEvents = 0, coinEvents = 0;
            _game.events.InventoryChanged += () => inventoryEvents++;
            _game.events.CurrencyChanged += _ => coinEvents++;

            Economy.Add(TestDesign.Leaf, 1);
            Economy.AddCurrency(5);
            Economy.TryRemove(TestDesign.Leaf, 99); // fails, no event

            Assert.AreEqual(1, inventoryEvents);
            Assert.AreEqual(1, coinEvents);
        }
    }
}
