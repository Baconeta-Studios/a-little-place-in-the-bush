using NUnit.Framework;

namespace Bush.Core.Tests
{
    /// <summary>Talking to characters: what they bring up, accepting, handing in, and the tracker.</summary>
    public class ConversationTests
    {
        private TestGame _game;

        [SetUp]
        public void SetUp() => _game = new TestGame();

        private void FinishWelcome()
        {
            _game.quests.Accept(TestDesign.Welcome);
            _game.economy.Add(TestDesign.Leaf, 3);
            _game.cafe.Cook(TestDesign.Salad);
            Assert.IsTrue(_game.quests.Claim(TestDesign.Welcome));
        }

        [Test]
        public void FirstTopic_IsTheMainQuestOffer()
        {
            NpcTopic topic = _game.quests.GetTopic(TestDesign.Ruru);
            Assert.AreEqual(TopicKind.Offer, topic.kind);
            Assert.AreEqual(TestDesign.Welcome, topic.questId);
        }

        [Test]
        public void Accepted_ThenInProgress_ThenHandIn()
        {
            Assert.IsTrue(_game.quests.Accept(TestDesign.Welcome));
            Assert.AreEqual(TopicKind.InProgress, _game.quests.GetTopic(TestDesign.Ruru).kind);

            _game.economy.Add(TestDesign.Leaf, 3);
            _game.cafe.Cook(TestDesign.Salad);
            NpcTopic topic = _game.quests.GetTopic(TestDesign.Ruru);
            Assert.AreEqual(TopicKind.HandIn, topic.kind);
            Assert.AreEqual(TestDesign.Welcome, topic.questId);
        }

        [Test]
        public void MainQuestsAreOfferedBeforeSideQuests()
        {
            FinishWelcome();
            // Both the main Lost recipe and the side Trade can start now; Ruru gives both.
            Assert.AreEqual(TestDesign.LostRecipe, _game.quests.GetTopic(TestDesign.Ruru).questId);
        }

        [Test]
        public void QuestsUnderWay_AreMentioned()
        {
            FinishWelcome();
            _game.quests.Accept(TestDesign.LostRecipe);
            _game.quests.Accept(TestDesign.Trade);
            NpcTopic topic = _game.quests.GetTopic(TestDesign.Ruru);
            Assert.AreEqual(TopicKind.InProgress, topic.kind, "Still has quests under way.");
        }

        [Test]
        public void NoQuests_IsJustAChat()
        {
            NpcTopic topic = _game.quests.GetTopic("npc.nobody");
            Assert.AreEqual(TopicKind.Chat, topic.kind);
            Assert.IsNull(topic.questId);
        }

        [Test]
        public void AcceptingFromTheGiver_CountsAsTalkingToThem()
        {
            _game.design.quests.Add(new QuestData
            {
                id = "quest.hello", title = "Hello", giver = TestDesign.Ruru, isMain = true,
                objectives = { new QuestObjective { type = ObjectiveType.TalkTo, target = TestDesign.Ruru } },
                rewards = { new QuestReward { type = RewardType.UnlockRecipe, target = TestDesign.SecretPie } }
            });
            _game.design.quests.Insert(0, _game.design.quests[_game.design.quests.Count - 1]);
            _game.design.quests.RemoveAt(_game.design.quests.Count - 1);
            _game.design.RebuildIndex();

            Assert.AreEqual("quest.hello", _game.quests.GetTopic(TestDesign.Ruru).questId);
            _game.quests.Accept("quest.hello");
            Assert.AreEqual(QuestStatus.ReadyToClaim, _game.quests.GetStatus("quest.hello"));
            Assert.IsTrue(_game.quests.Claim("quest.hello"));
            Assert.IsTrue(_game.cafe.IsUnlocked(TestDesign.SecretPie));
        }

        [Test]
        public void Tracker_FollowsTheMainStory()
        {
            Assert.AreEqual(TestDesign.Welcome, _game.quests.CurrentMainQuest().id, "Not started yet: talk to Ruru.");
            FinishWelcome();
            Assert.AreEqual(TestDesign.LostRecipe, _game.quests.CurrentMainQuest().id);
            CollectionAssert.IsEmpty(_game.quests.ActiveSideQuests());
            Assert.AreEqual(1, _game.quests.AvailableSideQuests().Count);
            _game.quests.Accept(TestDesign.Trade);
            Assert.AreEqual(1, _game.quests.ActiveSideQuests().Count);
        }

        [Test]
        public void NextDayQuests_WaitUntilTomorrow()
        {
            _game.design.quests.Find(q => q.id == TestDesign.LostRecipe).nextDay = true;
            FinishWelcome(); // day 1
            Assert.IsFalse(_game.quests.CanStart(TestDesign.LostRecipe), "Not the same day.");
            Assert.IsTrue(_game.quests.WaitingForAnotherDay(TestDesign.LostRecipe));
            Assert.AreEqual(TestDesign.LostRecipe, _game.quests.WaitingMainQuest()?.id, "The tracker says: tomorrow.");
            Assert.AreNotEqual(TestDesign.LostRecipe, _game.quests.GetTopic(TestDesign.Ruru).questId, "Not offered today.");

            _game.cafe.CloseDay(); // day 2
            Assert.IsTrue(_game.quests.CanStart(TestDesign.LostRecipe));
            Assert.IsNull(_game.quests.WaitingMainQuest());
        }

        [Test]
        public void MinDayQuests_WaitForTheirDay()
        {
            _game.design.quests.Find(q => q.id == TestDesign.Welcome).minDay = 3;
            Assert.IsFalse(_game.quests.CanStart(TestDesign.Welcome));
            _game.cafe.CloseDay();
            _game.cafe.CloseDay();
            Assert.IsTrue(_game.quests.CanStart(TestDesign.Welcome));
        }

        [Test]
        public void CloseDayObjective_CountsClosingUp()
        {
            _game.design.quests.Add(new QuestData
            {
                id = "quest.close", title = "Closing time", giver = TestDesign.Ruru,
                objectives = { new QuestObjective { type = ObjectiveType.CloseDay, qty = 1 } }
            });
            _game.design.RebuildIndex();
            _game.quests.Accept("quest.close");
            Assert.AreEqual(QuestStatus.Active, _game.quests.GetStatus("quest.close"));
            _game.cafe.CloseDay();
            Assert.AreEqual(QuestStatus.ReadyToClaim, _game.quests.GetStatus("quest.close"));
            Assert.AreEqual("Close the cafe for the day", QuestText.Objective(_game.design, _game.design.GetQuest("quest.close").objectives[0]));
        }

        [Test]
        public void QuestText_IsReadable()
        {
            QuestData welcome = _game.design.GetQuest(TestDesign.Welcome);
            Assert.AreEqual("Serve Salad at the cafe", QuestText.Objective(_game.design, welcome.objectives[0]));
            Assert.AreEqual("20 coins", QuestText.Reward(_game.design, welcome.rewards[0]));
            Assert.AreEqual("New recipe: Secret pie",
                QuestText.Reward(_game.design, new QuestReward { type = RewardType.UnlockRecipe, target = TestDesign.SecretPie }));
        }
    }
}
