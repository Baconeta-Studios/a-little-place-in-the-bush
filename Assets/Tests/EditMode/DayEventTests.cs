using System.Collections.Generic;
using NUnit.Framework;

namespace Bush.Core.Tests
{
    /// <summary>Day events: the morning roll, each kind of effect, and the doorstep gift.</summary>
    public class DayEventTests
    {
        private TestGame _game;

        [SetUp]
        public void SetUp()
        {
            _game = new TestGame();
            _game.state.luckSeed = 777;
            _game.design.rules = new DayRules { salesPerDay = 3, menuSlots = 2, forageTripsPerDay = 3 };
            _game.design.rules.events = new DayEventSettings { chance = 1, chanceFromCosiness = 0, firstDay = 1 };
        }

        private DayEventData Only(DayEventData dayEvent)
        {
            dayEvent.id ??= "event.test";
            _game.design.rules.dayEvents.Clear();
            _game.design.rules.dayEvents.Add(dayEvent);
            return dayEvent;
        }

        private void NextMorning()
        {
            _game.state.isNight = false;
            _game.cafe.CloseDay();
        }

        [Test]
        public void Events_StartAfterTheTutorial_AndRollOnceADay()
        {
            Only(new DayEventData { name = "Shiny day" });
            _game.design.rules.events.firstDay = 3;
            NextMorning(); // day 2
            Assert.IsNull(DayEvents.Today(_game.design, _game.state), "Not yet: the tutorial.");
            NextMorning(); // day 3
            Assert.AreEqual("event.test", DayEvents.Today(_game.design, _game.state)?.id);
        }

        [Test]
        public void TheSameEvent_NeverComesTwoDaysRunning()
        {
            _game.design.rules.dayEvents.Add(new DayEventData { id = "event.a" });
            _game.design.rules.dayEvents.Add(new DayEventData { id = "event.b" });
            string previous = null;
            for (int day = 0; day < 20; day++)
            {
                NextMorning();
                string today = DayEvents.Today(_game.design, _game.state)?.id;
                Assert.AreNotEqual(previous, today);
                previous = today;
            }
        }

        [Test]
        public void ACosierCafe_MakesEventsAndRarerEventsLikelier()
        {
            _game.design.rules.events = new DayEventSettings { chance = 0.4, chanceFromCosiness = 0.4, rarerFromCosiness = 1 };
            var rare = new DayEventData { id = "event.rare", weight = 1, rarity = Rarity.Rare };
            Only(rare);
            double bareChance = DayEvents.Chance(_game.design, _game.state);
            double bareWeight = DayEvents.WeightToday(_game.design, _game.state, rare);

            _game.design.GetPlaceable(TestDesign.Fern).cosiness = 200;
            _game.state.placed.Add(new PlacedObjectData { instanceId = 1, area = CafeStyle.CafeArea, placeableId = TestDesign.Fern });
            Assert.AreEqual(0.8, DayEvents.Chance(_game.design, _game.state), 1e-9);
            Assert.AreEqual(bareWeight * 3, DayEvents.WeightToday(_game.design, _game.state, rare), 1e-9, "Rare is 2 steps up: x(1 + 2).");
            Assert.Less(bareChance, 0.8);
        }

        [Test]
        public void MinCosiness_AndNight_Gate_Events()
        {
            var fancy = Only(new DayEventData { minCosiness = 10 });
            Assert.AreEqual(0, DayEvents.WeightToday(_game.design, _game.state, fancy));
            var meteor = new DayEventData { id = "event.meteor", needsNight = true };
            Assert.AreEqual(0, DayEvents.WeightToday(_game.design, _game.state, meteor));
            _game.state.nightUnlocked = true;
            Assert.Greater(DayEvents.WeightToday(_game.design, _game.state, meteor), 0);
        }

        [Test]
        public void BusyDay_SellsAnExtraDish()
        {
            Only(new DayEventData { extraSales = 1 });
            Assert.AreEqual(3, _game.design.rules.FullDaySales(_game.state));
            NextMorning();
            Assert.AreEqual(4, _game.design.rules.FullDaySales(_game.state));
        }

        [Test]
        public void FoodCritic_PaysDoubleForTheFirstDishOnly()
        {
            Only(new DayEventData { critic = true });
            NextMorning();
            _game.cafe.AddToMenu(TestDesign.Salad);
            _game.economy.Add(TestDesign.Leaf, 30);
            int before = _game.economy.Currency;

            _game.state.forageTripsToday = 1;
            List<DishSale> served = _game.cafe.ServeWhileOut();
            Assert.IsTrue(served[0].critic);
            Assert.AreEqual(10, served[0].coins, "5 coins, doubled.");
            _game.state.forageTripsToday = 2;
            Assert.AreEqual(5, _game.cafe.ServeWhileOut()[0].coins, "Only the first dish.");
            Assert.AreEqual(before + 15, _game.economy.Currency);
        }

        [Test]
        public void ForageEvents_AddTheirModifiers_AtTheRightTime()
        {
            var shiny = Only(new DayEventData { forage = { new ForageModifier(ModifierType.ExtraRolls, 2) }, forageAt = ActiveTime.Night });
            int usual = ForagingService.Preview(_game.design, TestDesign.Setup(), new ForageContext()).rolls;
            ExpeditionSetup day = TestDesign.Setup();
            day.eventId = shiny.id;
            Assert.AreEqual(usual, ForagingService.Preview(_game.design, day, new ForageContext()).rolls, "A night event doesn't touch day trips.");
            ExpeditionSetup night = TestDesign.Setup(time: TimeOfDay.Night);
            night.eventId = shiny.id;
            Assert.AreEqual(usual + 2, ForagingService.Preview(_game.design, night, new ForageContext()).rolls);
        }

        [Test]
        public void FamiliarFace_BringsSomeoneWithAQuestUnderWay()
        {
            _game.design.npcs.Find(n => n.id == TestDesign.Ruru).visits = VisitType.Random;
            _game.design.npcs.Find(n => n.id == TestDesign.Ruru).visitChance = 0.0001;
            _game.design.RebuildIndex();
            _game.state.quests.Add(new QuestProgress { questId = TestDesign.Welcome, status = QuestStatus.Active });
            Only(new DayEventData { familiarFace = true });
            NextMorning();
            CollectionAssert.Contains(_game.state.visitorsToday, TestDesign.Ruru);
        }

        [Test]
        public void Doorstep_GivesAGift_ExclusiveOnALuckyRoll()
        {
            var doorstep = Only(new DayEventData
            {
                gift =
                {
                    new GiftEntry { itemId = TestDesign.Leaf, qty = 3, weight = 1 },
                    new GiftEntry { itemId = TestDesign.ChairItem, qty = 1, weight = 1, exclusive = true }
                }
            });
            _game.design.rules.events.exclusiveChance = 0;
            _game.design.rules.events.exclusiveFromCosiness = 0;
            NextMorning();
            Assert.AreEqual(TestDesign.Leaf, _game.state.giftItem);
            Assert.AreEqual(3, _game.economy.Count(TestDesign.Leaf), "Straight into the bag.");

            _game.design.rules.events.exclusiveChance = 1;
            _game.design.rules.dayEvents.Add(new DayEventData { id = "event.other", weight = 0.000001 }); // so the doorstep can come back after a day off
            NextMorning();
            NextMorning();
            if (DayEvents.Today(_game.design, _game.state) == doorstep)
            {
                Assert.IsTrue(_game.state.giftExclusive);
                Assert.AreEqual(1, _game.economy.Count(TestDesign.ChairItem));
            }
        }

        [Test]
        public void ExclusiveChance_GrowsWithCosiness()
        {
            _game.design.rules.events = new DayEventSettings { exclusiveChance = 0.05, exclusiveFromCosiness = 0.3, cosinessForFullBoost = 100 };
            Assert.AreEqual(0.05, DayEvents.ExclusiveChance(_game.design, _game.state), 1e-9);
            _game.design.GetPlaceable(TestDesign.Fern).cosiness = 49;
            _game.state.placed.Add(new PlacedObjectData { instanceId = 1, area = CafeStyle.CafeArea, placeableId = TestDesign.Fern });
            Assert.AreEqual(0.05 + 0.3 * 0.5, DayEvents.ExclusiveChance(_game.design, _game.state), 1e-9, "Half cosy (49 + 1 for variety), half the boost.");
        }
    }
}
