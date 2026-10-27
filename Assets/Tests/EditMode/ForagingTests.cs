using System.Linq;
using NUnit.Framework;

namespace Bush.Core.Tests
{
    public class ForagingTests
    {
        private DesignData _design;
        private ForageContext _context;

        [SetUp]
        public void SetUp()
        {
            _design = TestDesign.Create();
            _context = new ForageContext();
        }

        [Test]
        public void Mulberry32_MatchesReferenceSequence()
        {
            // Reference values from the JavaScript mulberry32 used by the web sandbox.
            var rng = new Mulberry32(1);
            Assert.AreEqual(0.6270739405881613, rng.NextDouble(), 1e-15);
            Assert.AreEqual(0.002735721180215478, rng.NextDouble(), 1e-15);
            Assert.AreEqual(0.5274470399599522, rng.NextDouble(), 1e-15);
        }

        [Test]
        public void SameSeed_SameResult()
        {
            ExpeditionSetup setup = TestDesign.Setup(TestDesign.Weka, TestDesign.BigBasket, TestDesign.FocusBugs);
            for (uint seed = 0; seed < 50; seed++)
            {
                ForageResult a = ForagingService.Resolve(_design, setup, _context, seed);
                ForageResult b = ForagingService.Resolve(_design, setup, _context, seed);
                Assert.AreEqual(Describe(a), Describe(b), $"seed {seed}");
            }
        }

        [Test]
        public void RollCount_UsesTableAndExtraRolls()
        {
            Assert.AreEqual(3, ForagingService.Resolve(_design, TestDesign.Setup(), _context, 1u).rollCount);
            Assert.AreEqual(4, ForagingService.Resolve(_design, TestDesign.Setup(perk: TestDesign.BigBasket), _context, 1u).rollCount);
        }

        [Test]
        public void NightOnlyItems_NeverFoundInTheDay()
        {
            for (uint seed = 0; seed < 500; seed++)
            {
                ForageResult result = ForagingService.Resolve(_design, TestDesign.Setup(), _context, seed);
                Assert.IsFalse(result.stacks.Any(s => s.itemId == TestDesign.Glowworm), $"seed {seed}");
            }
        }

        [Test]
        public void QuestScrap_OnlyDropsWhileQuestIsActive()
        {
            bool foundWithoutQuest = false, foundWithQuest = false;
            var withQuest = new ForageContext();
            withQuest.activeQuests.Add(TestDesign.LostRecipe);

            for (uint seed = 0; seed < 300; seed++)
            {
                foundWithoutQuest |= Has(ForagingService.Resolve(_design, TestDesign.Setup(), _context, seed), TestDesign.Scrap1);
                foundWithQuest |= Has(ForagingService.Resolve(_design, TestDesign.Setup(), withQuest, seed), TestDesign.Scrap1);
            }

            Assert.IsFalse(foundWithoutQuest);
            Assert.IsTrue(foundWithQuest);
        }

        [Test]
        public void UniqueItems_AtMostOncePerTripAndNeverAfterObtained()
        {
            var context = new ForageContext();
            context.activeQuests.Add(TestDesign.LostRecipe);
            ExpeditionSetup setup = TestDesign.Setup(perk: TestDesign.BigBasket);

            for (uint seed = 0; seed < 500; seed++)
            {
                ForageResult result = ForagingService.Resolve(_design, setup, context, seed);
                Assert.LessOrEqual(result.rolls.Count(r => r.itemId == TestDesign.Scrap1), 1, $"seed {seed}");
            }

            context.obtained.Add(TestDesign.Scrap1);
            for (uint seed = 0; seed < 500; seed++)
                Assert.IsFalse(Has(ForagingService.Resolve(_design, setup, context, seed), TestDesign.Scrap1), $"seed {seed}");
        }

        [Test]
        public void TagWeight_MakesTaggedItemsMoreLikely()
        {
            int alone = CountFinds(TestDesign.Setup(), TestDesign.Bark);
            int withKea = CountFinds(TestDesign.Setup(TestDesign.Kea), TestDesign.Bark);
            Assert.Greater(withKea, alone * 1.8, $"Kea should find much more bark ({alone} alone vs {withKea}).");
        }

        [Test]
        public void Focus_MakesTaggedItemsMoreLikely()
        {
            int alone = CountFinds(TestDesign.Setup(), TestDesign.Bug);
            int focused = CountFinds(TestDesign.Setup(focus: TestDesign.FocusBugs), TestDesign.Bug);
            Assert.Greater(focused, alone * 1.4);
        }

        [Test]
        public void TagQuantity_AddsToEachFind()
        {
            for (uint seed = 0; seed < 200; seed++)
            {
                ForageResult result = ForagingService.Resolve(_design, TestDesign.Setup(TestDesign.Kea), _context, seed);
                foreach (ForageRoll roll in result.rolls.Where(r => r.itemId == TestDesign.Bark))
                    Assert.That(roll.qty, Is.InRange(2, 3), "Bark is 1-2 plus Kea's +1.");
            }
        }

        [Test]
        public void BonusRoll_OnlyFindsTaggedItems()
        {
            int bonusRolls = 0;
            for (uint seed = 0; seed < 400; seed++)
            {
                ForageResult result = ForagingService.Resolve(_design, TestDesign.Setup(TestDesign.Weka), _context, seed);
                foreach (ForageRoll roll in result.rolls.Where(r => r.bonus))
                {
                    bonusRolls++;
                    Assert.IsTrue(_design.GetItem(roll.itemId).HasTag("shiny"));
                }
            }

            // Weka has a 50% bonus chance: expect roughly 200 of 400.
            Assert.That(bonusRolls, Is.InRange(160, 240));
        }

        [Test]
        public void RareFinds_AreFlagged()
        {
            for (uint seed = 0; seed < 300; seed++)
            {
                ForageResult result = ForagingService.Resolve(_design, TestDesign.Setup(TestDesign.Weka), _context, seed);
                foreach (ItemStack stack in result.stacks)
                {
                    bool rare = _design.GetItem(stack.itemId).rarity >= Rarity.Rare;
                    Assert.AreEqual(rare, result.rareFinds.Contains(stack.itemId));
                }
            }
        }

        [Test]
        public void Preview_ChancesAddUpToOne()
        {
            ForagePreview preview = ForagingService.Preview(_design, TestDesign.Setup(TestDesign.Kea, focus: TestDesign.FocusBugs), _context);
            Assert.AreEqual(1.0, preview.odds.Sum(o => o.chancePerRoll), 1e-9);
            Assert.AreEqual(3, preview.rolls);
            Assert.IsFalse(preview.odds.Any(o => o.itemId == TestDesign.Glowworm), "Night-only items shouldn't show in the day.");
        }

        [Test]
        public void Claim_PaysOutExactlyOnce()
        {
            var game = new TestGame();
            ForagingService.Begin(game.design, game.state, TestDesign.Setup(), 42u);
            ForageResult expected = ForagingService.GetPendingResult(game.state);
            Assert.AreEqual(ForagingService.Resolve(game.design, TestDesign.Setup(), new ForageContext(), 42u).TotalItems(), expected.TotalItems());

            ExpeditionClaim first = ForagingService.Claim(game.design, game.state, game.economy, game.events);
            ExpeditionClaim second = ForagingService.Claim(game.design, game.state, game.economy, game.events);

            Assert.IsTrue(first.paid);
            Assert.IsFalse(second.paid);
            foreach (ItemStack stack in expected.stacks)
                Assert.AreEqual(stack.qty, game.economy.Count(stack.itemId), stack.itemId);
            Assert.AreEqual(1, game.state.expeditionsCompleted);
        }

        [Test]
        public void Begin_TwiceKeepsTheFirstExpedition()
        {
            var game = new TestGame();
            PendingExpedition first = ForagingService.Begin(game.design, game.state, TestDesign.Setup(), 1u);
            PendingExpedition second = ForagingService.Begin(game.design, game.state, TestDesign.Setup(TestDesign.Kea), 2u);
            Assert.AreSame(first, second, "Double-clicking Go must not start a second expedition.");
            Assert.AreEqual(1, game.state.expeditionsStarted);
        }

        [Test]
        public void Companion_TeachesPerkAfterEnoughTrips()
        {
            var game = new TestGame();
            string learned = null;
            game.events.PerkLearned += id => learned = id;

            ForagingService.Begin(game.design, game.state, TestDesign.Setup(TestDesign.Kea), 1u);
            Assert.IsNull(ForagingService.Claim(game.design, game.state, game.economy, game.events).learnedPerkId);

            ForagingService.Begin(game.design, game.state, TestDesign.Setup(TestDesign.Kea), 2u);
            Assert.AreEqual(TestDesign.BarkSense, ForagingService.Claim(game.design, game.state, game.economy, game.events).learnedPerkId);
            Assert.AreEqual(TestDesign.BarkSense, learned);
            Assert.Contains(TestDesign.BarkSense, game.state.unlockedPerks);

            ForagingService.Begin(game.design, game.state, TestDesign.Setup(TestDesign.Kea), 3u);
            Assert.IsNull(ForagingService.Claim(game.design, game.state, game.economy, game.events).learnedPerkId, "A perk is only learned once.");
        }

        [Test]
        public void CollectedScrap_NeverDropsAgain()
        {
            var game = new TestGame();
            game.state.quests.Add(new QuestProgress { questId = TestDesign.LostRecipe, status = QuestStatus.Active });

            // Find a seed that drops the scrap, then claim it.
            uint seed = 0;
            while (!Has(ForagingService.Resolve(game.design, TestDesign.Setup(), new GameStateForageContext(game.state), seed), TestDesign.Scrap1)) seed++;
            ForagingService.Begin(game.design, game.state, TestDesign.Setup(), seed);
            ForagingService.Claim(game.design, game.state, game.economy, game.events);
            Assert.AreEqual(1, game.economy.Count(TestDesign.Scrap1));

            for (uint s = 0; s < 300; s++)
                Assert.IsFalse(Has(ForagingService.Resolve(game.design, TestDesign.Setup(), new GameStateForageContext(game.state), s), TestDesign.Scrap1));

            // Handed in for a quest: still never drops again.
            game.economy.TryTransact(new Transaction { questHandIn = true }.Cost(TestDesign.Scrap1, 1));
            for (uint s = 0; s < 300; s++)
                Assert.IsFalse(Has(ForagingService.Resolve(game.design, TestDesign.Setup(), new GameStateForageContext(game.state), s), TestDesign.Scrap1));
        }

        [Test]
        public void Begin_RefusesLockedCompanionsAndCopiesTheSetup()
        {
            var game = new TestGame();
            game.design.GetCompanion(TestDesign.Kea).unlockedByDefault = false;
            Assert.IsNull(ForagingService.Begin(game.design, game.state, TestDesign.Setup(TestDesign.Kea), 1u));
            Assert.IsNotNull(ForagingService.Begin(game.design, game.state, TestDesign.Setup(TestDesign.Kea), 1u, force: true), "Cheats can force it.");

            var other = new TestGame();
            ExpeditionSetup setup = TestDesign.Setup(perk: TestDesign.BigBasket);
            setup.perkIds.Add(TestDesign.BigBasket); // the same perk twice must not stack
            PendingExpedition pending = ForagingService.Begin(other.design, other.state, setup, 5u);
            Assert.AreEqual(1, pending.setup.perkIds.Count);
            Assert.AreEqual(4, pending.result.rollCount);

            setup.companionId = TestDesign.Weka; // editing the screen's object after Go changes nothing
            Assert.IsNull(pending.setup.companionId);
        }

        [Test]
        public void Focus_CountsOncePerItem()
        {
            DesignData design = TestDesign.Create();
            design.items.Find(i => i.id == TestDesign.Bug).tags.Add("leaf"); // two of the focus's tags
            design.focuses[0].tags.Add("leaf");
            design.RebuildIndex();

            ForagePreview plain = ForagingService.Preview(design, TestDesign.Setup(), _context);
            ForagePreview focused = ForagingService.Preview(design, TestDesign.Setup(focus: TestDesign.FocusBugs), _context);
            double before = plain.odds.Find(o => o.itemId == TestDesign.Bug).weight;
            double after = focused.odds.Find(o => o.itemId == TestDesign.Bug).weight;
            Assert.AreEqual(before * 2, after, 1e-9, "x2, not x4, even with two matching tags.");
        }

        [Test]
        public void CompanionOnlyEntries_NeedThatCompanion()
        {
            bool withoutWeka = false, withWeka = false;
            for (uint seed = 0; seed < 400; seed++)
            {
                withoutWeka |= Has(ForagingService.Resolve(_design, TestDesign.Setup(TestDesign.Kea), _context, seed), TestDesign.Note);
                withWeka |= Has(ForagingService.Resolve(_design, TestDesign.Setup(TestDesign.Weka), _context, seed), TestDesign.Note);
            }
            Assert.IsFalse(withoutWeka);
            Assert.IsTrue(withWeka);
        }

        [Test]
        public void Encounter_TeachesItsRecipeOnceWhenClaimed()
        {
            var game = new TestGame();
            string learnedEvent = null;
            game.events.RecipeLearned += id => learnedEvent = id;
            Assert.IsFalse(game.cafe.IsUnlocked(TestDesign.SecretPie));

            uint seed = 0;
            while (!Has(ForagingService.Resolve(game.design, TestDesign.Setup(TestDesign.Weka), new GameStateForageContext(game.state), seed), TestDesign.Note)) seed++;
            ForagingService.Begin(game.design, game.state, TestDesign.Setup(TestDesign.Weka), seed);
            ExpeditionClaim claim = ForagingService.Claim(game.design, game.state, game.economy, game.events);

            CollectionAssert.AreEqual(new[] { TestDesign.SecretPie }, claim.learnedRecipeIds);
            Assert.AreEqual(TestDesign.SecretPie, learnedEvent);
            Assert.IsTrue(game.cafe.IsUnlocked(TestDesign.SecretPie));
            Assert.AreEqual(1, game.economy.Count(TestDesign.Note), "The note is kept as a keepsake.");

            // Collected, so it never turns up again.
            for (uint s = 0; s < 300; s++)
                Assert.IsFalse(Has(ForagingService.Resolve(game.design, TestDesign.Setup(TestDesign.Weka), new GameStateForageContext(game.state), s), TestDesign.Note));
        }

        private int CountFinds(ExpeditionSetup setup, string itemId)
        {
            int finds = 0;
            for (uint seed = 0; seed < 2000; seed++)
                finds += ForagingService.Resolve(_design, setup, _context, seed).rolls.Count(r => r.itemId == itemId);
            return finds;
        }

        private static bool Has(ForageResult result, string itemId) => result.stacks.Any(s => s.itemId == itemId);

        private static string Describe(ForageResult result) =>
            string.Join(",", result.rolls.Select(r => $"{r.itemId}x{r.qty}{(r.bonus ? "!" : "")}"));
    }
}
