using System.Linq;
using NUnit.Framework;

namespace Bush.Core.Tests
{
    public class BuildTests
    {
        private TestGame _game;
        private BuildService Build => _game.build;

        [SetUp]
        public void SetUp() => _game = new TestGame();

        [Test]
        public void Place_TakesTheItemAndPutAwayGivesItBack()
        {
            _game.economy.Add(TestDesign.ChairItem, 2);
            PlacedObjectData chair = Build.Place(TestDesign.Area, TestDesign.Chair, 1f, -2f, false);

            Assert.IsNotNull(chair);
            Assert.AreEqual(1, _game.economy.Count(TestDesign.ChairItem));
            Assert.IsTrue(chair.fromInventory);

            Assert.IsTrue(Build.Remove(chair.instanceId));
            Assert.AreEqual(2, _game.economy.Count(TestDesign.ChairItem));
            Assert.IsEmpty(Build.GetPlaced(TestDesign.Area).ToList());
        }

        [Test]
        public void Place_FailsWithoutTheItem()
        {
            Assert.IsNull(Build.Place(TestDesign.Area, TestDesign.Chair, 0f, 0f, false));
            Assert.IsNull(Build.Place(TestDesign.Area, "placeable.missing", 0f, 0f, false));
            Assert.IsEmpty(_game.state.placed);
        }

        [Test]
        public void Unlimited_PlacesWithoutItemsAndGivesNothingBack()
        {
            Build.UnlimitedItems = true;
            Assert.AreEqual(int.MaxValue, Build.CountAvailable(TestDesign.Chair));

            PlacedObjectData chair = Build.Place(TestDesign.Area, TestDesign.Chair, 0f, 0f, false);
            Assert.IsNotNull(chair);
            Assert.IsFalse(chair.fromInventory);

            Build.Remove(chair.instanceId);
            Assert.AreEqual(0, _game.economy.Count(TestDesign.ChairItem), "Free items don't turn into real ones.");
        }

        [Test]
        public void ItemsPlacedFromTheInventory_StillComeBackAfterSwitchingToUnlimited()
        {
            _game.economy.Add(TestDesign.ChairItem, 1);
            PlacedObjectData chair = Build.Place(TestDesign.Area, TestDesign.Chair, 0f, 0f, false);
            Build.UnlimitedItems = true;
            Build.Remove(chair.instanceId);
            Assert.AreEqual(1, _game.economy.Count(TestDesign.ChairItem));
        }

        [Test]
        public void EachPlacedObject_GetsItsOwnId()
        {
            Build.UnlimitedItems = true;
            PlacedObjectData a = Build.Place(TestDesign.Area, TestDesign.Chair, 0f, 0f, false);
            PlacedObjectData b = Build.Place(TestDesign.Area, TestDesign.Chair, 1f, 0f, false);
            Build.Remove(a.instanceId);
            PlacedObjectData c = Build.Place(TestDesign.Area, TestDesign.Chair, 2f, 0f, false);

            Assert.AreNotEqual(a.instanceId, b.instanceId);
            Assert.AreNotEqual(a.instanceId, c.instanceId, "Ids are never reused.");
            Assert.AreNotEqual(b.instanceId, c.instanceId);
        }

        [Test]
        public void MoveAndFlip_UpdateTheSave()
        {
            Build.UnlimitedItems = true;
            PlacedObjectData chair = Build.Place(TestDesign.Area, TestDesign.Chair, 0f, 0f, false);
            Assert.IsTrue(Build.Move(chair.instanceId, 3f, -1.5f));
            Assert.IsTrue(Build.SetFlipped(chair.instanceId, true));
            Assert.AreEqual((3f, -1.5f, true), (chair.x, chair.y, chair.flipped));

            PlacedObjectData fern = Build.Place(TestDesign.Area, TestDesign.Fern, 0f, 0f, true);
            Assert.IsFalse(fern.flipped, "The fern can't flip.");
            Assert.IsFalse(Build.SetFlipped(fern.instanceId, true));
        }

        [Test]
        public void Areas_AreKeptApart()
        {
            Build.UnlimitedItems = true;
            Build.Place(TestDesign.Area, TestDesign.Chair, 0f, 0f, false);
            Build.Place("Garden", TestDesign.Chair, 0f, 0f, false);
            Assert.AreEqual(1, Build.GetPlaced(TestDesign.Area).Count());
            Assert.AreEqual(1, Build.CountSeats("Garden"));
        }

        [Test]
        public void Seats_AreCounted()
        {
            Build.UnlimitedItems = true;
            Build.Place(TestDesign.Area, TestDesign.Chair, 0f, 0f, false);
            Build.Place(TestDesign.Area, TestDesign.Chair, 1f, 0f, false);
            Build.Place(TestDesign.Area, TestDesign.Fern, 2f, 0f, false);
            Assert.AreEqual(2, Build.CountSeats(TestDesign.Area));
        }

        [Test]
        public void StartingLayout_IsPutDownOnceAndIsFree()
        {
            var layout = new[] { (TestDesign.Chair, -1f, -2f, false), (TestDesign.Fern, 2f, -1f, false) };
            Assert.IsTrue(Build.InitialiseArea(TestDesign.Area, layout));
            Assert.IsFalse(Build.InitialiseArea(TestDesign.Area, layout), "Only the first visit.");
            Assert.AreEqual(2, Build.GetPlaced(TestDesign.Area).Count());

            PlacedObjectData chair = Build.GetPlaced(TestDesign.Area).First();
            Build.Remove(chair.instanceId);
            Assert.AreEqual(1, _game.economy.Count(TestDesign.ChairItem), "Starting furniture is the player's own: putting it away puts it in the bag.");
        }

        [Test]
        public void Placing_CountsForPlaceObjectQuests()
        {
            _game.design.quests.Add(new QuestData
            {
                id = "quest.decorate", title = "Decorate", giver = TestDesign.Ruru,
                objectives = { new QuestObjective { type = ObjectiveType.PlaceObject, target = TestDesign.ChairItem, qty = 2 } }
            });
            _game.design.RebuildIndex();
            _game.quests.Start("quest.decorate");

            Build.UnlimitedItems = true;
            Build.Place(TestDesign.Area, TestDesign.Chair, 0f, 0f, false);
            Build.Place(TestDesign.Area, TestDesign.Fern, 1f, 0f, false);
            Assert.AreEqual((1, 2), _game.quests.GetProgress("quest.decorate", 0));
            Build.Place(TestDesign.Area, TestDesign.Chair, 2f, 0f, false);
            Assert.AreEqual(QuestStatus.ReadyToClaim, _game.quests.GetStatus("quest.decorate"));
        }

        [Test]
        public void Normalise_KeepsIdsUniqueAndDropsUnknownPlaceables()
        {
            _game.state.placed.Add(new PlacedObjectData { instanceId = 7, area = TestDesign.Area, placeableId = TestDesign.Chair });
            _game.state.placed.Add(new PlacedObjectData { instanceId = 8, area = TestDesign.Area, placeableId = "placeable.gone" });
            _game.state.nextPlacedId = 1;
            _game.state.Normalise(_game.design);

            Assert.AreEqual(1, _game.state.placed.Count);
            Build.UnlimitedItems = true;
            Assert.AreEqual(8, Build.Place(TestDesign.Area, TestDesign.Chair, 0f, 0f, false).instanceId);
        }

        [Test]
        public void GameState_SurvivesAJsonRoundTrip()
        {
            Build.UnlimitedItems = true;
            Build.Place(TestDesign.Area, TestDesign.Chair, 1.25f, -2.5f, true);
            _game.economy.Add(TestDesign.Leaf, 3);
            _game.economy.AddCurrency(12);
            _game.quests.Start(TestDesign.Welcome);
            ForagingService.Begin(_game.design, _game.state, TestDesign.Setup(TestDesign.Weka), 99u);

            string json = UnityEngine.JsonUtility.ToJson(_game.state);
            GameState loaded = UnityEngine.JsonUtility.FromJson<GameState>(json);

            Assert.AreEqual(12, loaded.currency);
            Assert.AreEqual(3, loaded.inventory.Find(s => s.itemId == TestDesign.Leaf).qty);
            Assert.AreEqual(QuestStatus.Active, loaded.FindQuest(TestDesign.Welcome).status);
            PlacedObjectData chair = loaded.placed.Single();
            Assert.AreEqual((1.25f, -2.5f, true, TestDesign.Area), (chair.x, chair.y, chair.flipped, chair.area));
            Assert.AreEqual(_game.state.nextPlacedId, loaded.nextPlacedId);
            Assert.IsTrue(loaded.hasPendingExpedition);
            Assert.AreEqual(_game.state.pendingExpedition.result.TotalItems(), loaded.pendingExpedition.result.TotalItems());
        }
    }
}
