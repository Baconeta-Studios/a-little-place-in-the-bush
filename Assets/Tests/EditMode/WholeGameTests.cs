using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Bush.Core.Tests
{
    /// <summary>
    /// Plays every quest in Design/design-data.json with the real rules, day by day: only talking to whoever's actually
    /// at the cafe, only using what the bag holds, and only finding what the forage table could give at that time of day.
    /// It fails if any quest can't be reached or finished, and says what it was stuck on. Run it after changing the story.
    /// </summary>
    public class WholeGameTests
    {
        private const int MaxDays = 300;
        private const int CoinsPerDay = 30; // roughly what a cafe that's getting going earns
        private const int HiddenItemDay = 25; // when this imaginary player happens to find the rare things

        private static DesignData LoadRealDesign()
        {
            string path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Design", "design-data.json"));
            DesignData design = UnityEngine.JsonUtility.FromJson<DesignFile>(File.ReadAllText(path)).ToDesign(new List<string>());
            // Like the GameDatabase (the day rules aren't in the JSON).
            design.rules.forageTripsPerDay = 2;
            design.rules.menuSlots = 1;
            design.rules.salesPerDay = 3;
            design.rules.nightTrips = 1;
            design.rules.perkSlots = 1;
            design.rules.forageUnlockedBy = "quest.main_hello";
            design.rules.closeUnlockedBy = "quest.main_first_close";
            design.RebuildIndex();
            return design;
        }

        private class Player
        {
            public readonly TestGame game;
            private readonly DesignData _design;
            private float _nextX = -20f;
            public int DaysPlayed;

            public Player(uint seed)
            {
                _design = LoadRealDesign();
                game = new TestGame(_design);
                game.state.luckSeed = seed;
            }

            public bool Finished => _design.quests.All(q => game.quests.GetStatus(q.id) == QuestStatus.Completed);

            public void PlayToTheEnd()
            {
                for (int guard = 0; guard < MaxDays * 2 && !Finished && game.state.day <= MaxDays; guard++)
                {
                    game.economy.AddCurrency(CoinsPerDay);
                    FindRareThings();
                    TalkToEveryone();
                    PlaceWhatIHave();
                    Forage();
                    Cook();
                    TalkToEveryone();
                    SetMenu();

                    if (!game.cafe.CloseUnlocked()) { Forage(); TalkToEveryone(); }
                    if (game.cafe.CloseUnlocked())
                    {
                        game.cafe.CloseDay();
                        TalkToEveryone();
                        if (game.state.isNight)
                        {
                            Forage();
                            Cook();
                            TalkToEveryone();
                            game.cafe.GoToBed();
                        }
                    }
                    DaysPlayed++;
                }
            }

            public string Stuck() => string.Join("\n", _design.quests
                .Where(q => game.quests.GetStatus(q.id) != QuestStatus.Completed)
                .Select(q => $"{q.id}: {game.quests.GetStatus(q.id)}, from {q.giver}"
                             + (game.quests.CanStart(q.id) ? " (could start)" : "")
                             + string.Join("", q.objectives.Select((o, i) => $"\n   {o.type} {o.target}: {game.quests.GetProgress(q.id, i)}"))));

            private void TalkToEveryone()
            {
                for (int guard = 0; guard < 40; guard++)
                {
                    bool anything = false;
                    foreach (string npc in Visitors.Present(_design, game.state, game.quests))
                    {
                        game.quests.BeginConversation(npc);
                        NpcTopic topic = game.quests.GetTopic(npc);
                        if (topic.kind == TopicKind.HandIn) anything |= game.quests.Claim(topic.questId);
                        else if (topic.kind == TopicKind.Offer) anything |= game.quests.Accept(topic.questId);
                        BuyRecipesFrom(npc);
                    }
                    if (!anything) return;
                }
            }

            private void BuyRecipesFrom(string npcId)
            {
                NpcData npc = _design.GetNpc(npcId);
                bool open = string.IsNullOrEmpty(npc.shopAfterQuest) || game.quests.GetStatus(npc.shopAfterQuest) == QuestStatus.Completed;
                if (!open) return;
                foreach (string recipe in npc.shop) game.cafe.Learn(recipe);
            }

            private IEnumerable<(QuestData quest, QuestObjective objective, int index)> WantedNow()
            {
                foreach (QuestData quest in _design.quests)
                {
                    if (game.quests.GetStatus(quest.id) != QuestStatus.Active) continue;
                    for (int i = 0; i < quest.objectives.Count; i++)
                    {
                        (int current, int required) = game.quests.GetProgress(quest.id, i);
                        if (current < required) yield return (quest, quest.objectives[i], i);
                    }
                }
            }

            private bool CanForageNow(string itemId)
            {
                foreach (ForageTableData table in _design.forageTables)
                foreach (ForageEntry entry in table.entries)
                {
                    if (entry.itemId != itemId) continue;
                    if (entry.time == EntryTime.Day && game.state.isNight) continue;
                    if (entry.time == EntryTime.Night && !game.state.isNight) continue;
                    if (!string.IsNullOrEmpty(entry.requiresQuest) && game.quests.GetStatus(entry.requiresQuest) != QuestStatus.Active && game.quests.GetStatus(entry.requiresQuest) != QuestStatus.ReadyToClaim) continue;
                    if (!string.IsNullOrEmpty(entry.requiresCompanion) && !game.state.unlockedCompanions.Contains(entry.requiresCompanion)) continue;
                    return true;
                }
                return false;
            }

            /// <summary>Quests that begin by finding something: only once the story's well under way, and at the right time of day.</summary>
            private void FindRareThings()
            {
                if (game.state.day < HiddenItemDay) return;
                foreach (QuestData quest in _design.quests)
                    if (!string.IsNullOrEmpty(quest.requiresItem) && game.economy.Count(quest.requiresItem) == 0
                        && game.quests.GetStatus(quest.id) == QuestStatus.NotStarted)
                        game.economy.Add(quest.requiresItem, 1);
            }

            private void Forage()
            {
                int trips = ForagingService.TripsLeftToday(_design, game.state);
                for (int trip = 0; trip < trips; trip++)
                {
                    var result = new ForageResult();

                    // Take along whoever a quest asks for, if they've joined you and they're awake.
                    foreach (var (_, objective, _) in WantedNow())
                    {
                        if (objective.type != ObjectiveType.TripWith || !game.state.unlockedCompanions.Contains(objective.target)) continue;
                        CompanionData companion = _design.GetCompanion(objective.target);
                        bool awake = companion.activeAt == ActiveTime.Both || companion.activeAt == (game.state.isNight ? ActiveTime.Night : ActiveTime.Day);
                        if (awake) { result.companionId = objective.target; break; }
                    }

                    // One thing that's wanted, per trip: a slow, unlucky player.
                    var wanted = WantedNow()
                        .Where(w => w.objective.type is ObjectiveType.FindItems or ObjectiveType.HaveItems or ObjectiveType.DeliverItems)
                        .Select(w => w.objective.target)
                        .FirstOrDefault(CanForageNow);
                    if (wanted != null)
                    {
                        result.stacks.Add(new ItemStack { itemId = wanted, qty = 1 });
                        game.economy.Add(wanted, 1);
                    }

                    game.state.forageTripsToday++;
                    game.events.RaiseExpeditionCompleted(result);
                }
            }

            /// <summary>Serves what's wanted. Ingredients have to be things the bush can give.</summary>
            private void Cook()
            {
                foreach (var (_, objective, _) in WantedNow().ToList())
                {
                    if (objective.type != ObjectiveType.CookRecipe) continue;
                    string recipeId = !string.IsNullOrEmpty(objective.target)
                        ? objective.target
                        : _design.recipes.Find(r => game.cafe.IsUnlocked(r.id))?.id;
                    if (recipeId == null || !game.cafe.IsUnlocked(recipeId)) continue;
                    RecipeData recipe = _design.GetRecipe(recipeId);
                    foreach (ItemAmount ingredient in recipe.ingredients)
                    {
                        Assert.IsTrue(_design.forageTables.Exists(t => t.entries.Exists(e => e.itemId == ingredient.itemId)),
                            $"{recipeId} needs {ingredient.itemId}, which the bush never gives.");
                        game.economy.Add(ingredient.itemId, ingredient.qty);
                    }
                    game.cafe.Cook(recipeId);
                }
            }

            private void PlaceWhatIHave()
            {
                foreach (PlaceableData placeable in _design.placeables)
                    while (game.economy.Count(placeable.itemId) > 0)
                    {
                        Assert.IsNotNull(game.build.Place(CafeStyle.CafeArea, placeable.id, _nextX, 0f, false), $"Couldn't place {placeable.id}.");
                        _nextX += 3f;
                    }
            }

            private void SetMenu()
            {
                game.state.menu.Clear();
                var wanted = new List<string>();
                foreach (var (_, objective, _) in WantedNow())
                {
                    if (objective.type is not (ObjectiveType.CloseWithOnMenu or ObjectiveType.HaveOnMenu)) continue;
                    if (!string.IsNullOrEmpty(objective.target)) wanted.Add(objective.target);
                    else
                    {
                        // A menu of this many dishes: buy the slots if there's money for them.
                        foreach (UpgradeData upgrade in _design.rules.upgrades)
                            if (upgrade.effect == UpgradeEffect.MenuSlot && _design.rules.MenuSlots(game.state) < objective.qty) game.cafe.BuyUpgrade(upgrade.id);
                    }
                }
                foreach (string recipe in wanted.Distinct()) game.cafe.AddToMenu(recipe);
                foreach (RecipeData recipe in _design.recipes)
                    if (game.cafe.IsUnlocked(recipe.id) && !game.cafe.MenuIsFull && !game.cafe.IsOnMenu(recipe.id)) game.cafe.AddToMenu(recipe.id);
            }
        }

        [TestCase(1u)]
        [TestCase(77u)]
        [TestCase(20261105u)]
        public void EveryQuest_CanBeFinished(uint seed)
        {
            var player = new Player(seed);
            player.PlayToTheEnd();
            Assert.IsTrue(player.Finished, $"Not finished by day {MaxDays}:\n{player.Stuck()}");
            System.Console.WriteLine($"[Whole game] seed {seed}: everything done on day {player.game.state.day}, story done: {player.game.quests.IsMainStoryComplete()}.");
        }

        [Test]
        public void TheDate_ComesLast_InTheStory()
        {
            DesignData design = LoadRealDesign();
            Assert.AreEqual("quest.main_date", design.quests.FindLast(q => q.isMain).id);
        }

        [Test]
        public void EveryCompanionAndPlaceable_CanBeEarned()
        {
            DesignData design = LoadRealDesign();
            IEnumerable<QuestReward> rewards = design.quests.SelectMany(q => q.rewards);
            foreach (CompanionData companion in design.companions)
                Assert.IsTrue(rewards.Any(r => r.type == RewardType.UnlockCompanion && r.target == companion.id), $"Nothing gives {companion.id}.");
            foreach (PlaceableData placeable in design.placeables)
                Assert.IsTrue(rewards.Any(r => r.type == RewardType.Item && r.target == placeable.itemId), $"Nothing gives {placeable.id}.");
        }

        [Test]
        public void EveryQuestGiver_IsSomeoneWhoCanBeMet()
        {
            DesignData design = LoadRealDesign();
            foreach (QuestData quest in design.quests) Assert.IsNotNull(design.GetNpc(quest.giver), $"{quest.id} has no giver.");
        }

        /// <summary>Kunekune's first quest gives the companion, and the others come at the end of their chains.</summary>
        [Test]
        public void Companions_ComeWhenTheyShould()
        {
            DesignData design = LoadRealDesign();
            string From(string companion) => design.quests.Find(q => q.rewards.Exists(r => r.type == RewardType.UnlockCompanion && r.target == companion)).id;
            Assert.AreEqual("quest.kunekune_nose", From("companion.kunekune"));
            Assert.AreEqual("quest.snail_somewhere_damp", From("companion.snail"));
            Assert.AreEqual("quest.spider_moonlight", From("companion.spider"));
            Assert.AreEqual("quest.weka_bait", From("companion.weka"));
            Assert.AreEqual("quest.pekapeka_bold", From("companion.pekapeka"));
            Assert.AreEqual("quest.gus_lights", From("companion.firefly"));
        }

        /// <summary>Ruru's scrap can't be got until you've started looking for the recipe.</summary>
        [Test]
        public void RurusScrap_OnlyAfterYouStartLooking()
        {
            DesignData design = LoadRealDesign();
            QuestData ruru = design.quests.Find(q => q.rewards.Exists(r => r.target == "item.recipe_scrap_4"));
            Assert.AreEqual("npc.ruru", ruru.giver);
            CollectionAssert.Contains(ruru.prerequisites, "quest.main_scroll_asked");
        }

        /// <summary>A hand-in that's just "have this in your bag" could be done as soon as it's accepted. Chains shouldn't feel like that.</summary>
        [Test]
        public void ChainQuests_CantJustBeHandedInFromWhatYouAlreadyHave()
        {
            DesignData design = LoadRealDesign();
            foreach (QuestData quest in design.quests)
            {
                if (quest.isMain || !string.IsNullOrEmpty(quest.requiresItem) || quest.prerequisites.Exists(p => design.GetQuest(p)?.requiresItem?.Length > 0)) continue;
                bool onlyBagChecks = quest.objectives.All(o => o.type is ObjectiveType.HaveItems or ObjectiveType.DeliverItems && design.GetItem(o.target)?.category != ItemCategory.KeyItem);
                if (quest.id == "quest.side_weka_trade") continue; // its button can only be found during the quest
                Assert.IsFalse(onlyBagChecks, $"{quest.id} can be finished the moment it's accepted, if you happen to have the items.");
            }
        }
    }

    /// <summary>The kinds of quest added for the character chains, on a small test design.</summary>
    public class NewQuestKindsTests
    {
        private TestGame _game;

        [SetUp]
        public void SetUp() => _game = new TestGame();

        private QuestData Add(string id, params QuestObjective[] objectives)
        {
            var quest = new QuestData { id = id, title = id, giver = TestDesign.Ruru };
            quest.objectives.AddRange(objectives);
            _game.design.quests.Add(quest);
            _game.design.RebuildIndex();
            return quest;
        }

        [Test]
        public void FindItems_IgnoresWhatYouAlreadyHave()
        {
            _game.economy.Add(TestDesign.Leaf, 10);
            Add("quest.find", new QuestObjective { type = ObjectiveType.FindItems, target = TestDesign.Leaf, qty = 3 });
            _game.quests.Start("quest.find");
            Assert.AreEqual(QuestStatus.Active, _game.quests.GetStatus("quest.find"));

            var trip = new ForageResult();
            trip.stacks.Add(new ItemStack { itemId = TestDesign.Leaf, qty = 2 });
            _game.events.RaiseExpeditionCompleted(trip);
            Assert.AreEqual((2, 3), _game.quests.GetProgress("quest.find", 0));
            _game.events.RaiseExpeditionCompleted(trip);
            Assert.AreEqual(QuestStatus.ReadyToClaim, _game.quests.GetStatus("quest.find"));
            Assert.AreEqual(10, _game.economy.Count(TestDesign.Leaf), "The find objective takes nothing from the bag.");
        }

        [Test]
        public void FindItems_BeforeTakingTheQuestDontCount()
        {
            Add("quest.find", new QuestObjective { type = ObjectiveType.FindItems, target = TestDesign.Leaf, qty = 1 });
            var trip = new ForageResult();
            trip.stacks.Add(new ItemStack { itemId = TestDesign.Leaf, qty = 5 });
            _game.events.RaiseExpeditionCompleted(trip);
            _game.quests.Start("quest.find");
            Assert.AreEqual(QuestStatus.Active, _game.quests.GetStatus("quest.find"));
        }

        [Test]
        public void TripWith_OnlyCountsThatCompanion()
        {
            Add("quest.trip", new QuestObjective { type = ObjectiveType.TripWith, target = TestDesign.Weka, qty = 1 });
            _game.quests.Start("quest.trip");
            _game.events.RaiseExpeditionCompleted(new ForageResult { companionId = TestDesign.Kea });
            _game.events.RaiseExpeditionCompleted(new ForageResult());
            Assert.AreEqual(QuestStatus.Active, _game.quests.GetStatus("quest.trip"));
            _game.events.RaiseExpeditionCompleted(new ForageResult { companionId = TestDesign.Weka });
            Assert.AreEqual(QuestStatus.ReadyToClaim, _game.quests.GetStatus("quest.trip"));
        }

        [Test]
        public void RequiresItem_HidesTheQuestUntilYouHaveIt()
        {
            QuestData quest = Add("quest.found", new QuestObjective { type = ObjectiveType.HaveItems, target = TestDesign.Note, qty = 1 });
            quest.requiresItem = TestDesign.Note;
            quest.giver = "npc.finder";
            Assert.IsFalse(_game.quests.CanStart("quest.found"));
            Assert.AreEqual(TopicKind.Chat, _game.quests.GetTopic("npc.finder").kind);

            _game.economy.Add(TestDesign.Note, 1);
            Assert.IsTrue(_game.quests.CanStart("quest.found"));
            Assert.AreEqual(TopicKind.Offer, _game.quests.GetTopic("npc.finder").kind);
        }

        [Test]
        public void TwoQuestsReady_WithOnePerson_AreHandedInOneAfterTheOther()
        {
            Add("quest.one", new QuestObjective { type = ObjectiveType.CloseDay, qty = 1 });
            Add("quest.two", new QuestObjective { type = ObjectiveType.CloseDay, qty = 1 });
            _game.quests.Start("quest.one");
            _game.quests.Start("quest.two");
            _game.events.RaiseDayEnded(new DaySummary());

            NpcTopic first = _game.quests.GetTopic(TestDesign.Ruru);
            Assert.AreEqual(TopicKind.HandIn, first.kind);
            Assert.IsTrue(_game.quests.Claim(first.questId));
            NpcTopic second = _game.quests.GetTopic(TestDesign.Ruru);
            Assert.AreEqual(TopicKind.HandIn, second.kind);
            Assert.AreNotEqual(first.questId, second.questId);
            Assert.IsTrue(_game.quests.Claim(second.questId));
        }

        [Test]
        public void HavePlaced_CountsThingsAlreadyOut()
        {
            _game.economy.Add(TestDesign.ChairItem, 1);
            _game.build.Place(CafeStyle.CafeArea, TestDesign.Chair, 0f, 0f, false);
            Add("quest.out", new QuestObjective { type = ObjectiveType.HavePlaced, target = TestDesign.ChairItem, qty = 1 });
            _game.quests.Start("quest.out");
            Assert.AreEqual(QuestStatus.ReadyToClaim, _game.quests.GetStatus("quest.out"));
        }

        [Test]
        public void Detach_TakesItOffTheTable()
        {
            _game.economy.Add(TestDesign.ChairItem, 1);
            _game.economy.Add(TestDesign.FernItem, 1);
            PlacedObjectData table = _game.build.Place(CafeStyle.CafeArea, TestDesign.Chair, 0f, 0f, false);
            PlacedObjectData fern = _game.build.Place(CafeStyle.CafeArea, TestDesign.Fern, 0f, 0f, false);
            Assert.IsTrue(_game.build.Attach(fern.instanceId, table.instanceId, 0));
            Assert.IsTrue(_game.build.Detach(fern.instanceId));
            Assert.AreEqual(0, fern.parentId);

            // Putting the table away now leaves it where it is.
            Assert.IsTrue(_game.build.Remove(table.instanceId));
            Assert.AreEqual(0, _game.economy.Count(TestDesign.FernItem));
        }

        [Test]
        public void SameThing_CanBeRewardedAndPlacedAgain()
        {
            QuestData quest = Add("quest.gift", new QuestObjective { type = ObjectiveType.CloseDay, qty = 1 });
            quest.rewards.Add(new QuestReward { type = RewardType.Item, target = TestDesign.ChairItem, qty = 1 });
            QuestData again = Add("quest.gift_again", new QuestObjective { type = ObjectiveType.CloseDay, qty = 1 });
            again.rewards.Add(new QuestReward { type = RewardType.Item, target = TestDesign.ChairItem, qty = 1 });
            _game.quests.Start(quest.id);
            _game.quests.Start(again.id);
            _game.events.RaiseDayEnded(new DaySummary());
            _game.quests.Claim(quest.id);
            _game.quests.Claim(again.id);
            Assert.AreEqual(2, _game.economy.Count(TestDesign.ChairItem));
            Assert.IsNotNull(_game.build.Place(CafeStyle.CafeArea, TestDesign.Chair, 0f, 0f, false));
            Assert.IsNotNull(_game.build.Place(CafeStyle.CafeArea, TestDesign.Chair, 3f, 0f, false));
        }
    }
}
