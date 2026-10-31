using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Bush.Core.Tests
{
    /// <summary>
    /// Plays the real story in Design/design-data.json from the first quest to the end, day by day: it takes
    /// each main quest as soon as it opens, does what it asks (adding whatever items it needs), hands it in,
    /// and closes the day when the story is waiting for tomorrow. Catches broken chains (a quest that can
    /// never open, or asks for something that can't be done yet) and checks the tutorial's pacing. Run it
    /// after rewriting the story.
    /// </summary>
    public class StoryTests
    {
        private const int MaxDays = 60;

        private static DesignData LoadRealDesign()
        {
            string path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Design", "design-data.json"));
            var file = UnityEngine.JsonUtility.FromJson<DesignFile>(File.ReadAllText(path));
            DesignData design = file.ToDesign(new List<string>());
            // Like the GameDatabase: closing opens with the first story quest that asks you to close up.
            design.rules.closeUnlockedBy = design.quests.Find(q => q.isMain && q.objectives.Exists(o => o.type == ObjectiveType.CloseDay))?.id;
            return design;
        }

        /// <summary>Plays the main story; returns the day each main quest was handed in.</summary>
        private static Dictionary<string, int> PlayTheStory(TestGame game, out int nightUnlockedOn)
        {
            var handedIn = new Dictionary<string, int>();
            nightUnlockedOn = 0;
            for (int guard = 0; guard < 1000 && game.state.day <= MaxDays && !game.quests.IsMainStoryComplete(); guard++)
            {
                QuestData quest = game.quests.CurrentMainQuest();
                if (quest == null)
                {
                    // Nothing to do in the story today: on to tomorrow.
                    Assert.IsNotNull(game.quests.WaitingMainQuest(), $"Day {game.state.day}: the story is stuck (no main quest can open).");
                    NextDay(game);
                    continue;
                }

                if (game.quests.GetStatus(quest.id) == QuestStatus.NotStarted) Assert.IsTrue(game.quests.Accept(quest.id), quest.id);
                DoWhatItAsks(game, quest);
                Assert.IsTrue(game.quests.Claim(quest.id, out string error), $"Day {game.state.day}: couldn't hand in {quest.id}: {error}");
                handedIn[quest.id] = game.state.day;
                if (game.state.nightUnlocked && nightUnlockedOn == 0) nightUnlockedOn = game.state.day;
            }
            return handedIn;
        }

        private static void NextDay(TestGame game)
        {
            Assert.IsTrue(game.state.isNight || game.cafe.CloseUnlocked(),
                $"Day {game.state.day}: the story needs the day closed, but closing isn't open yet ({game.design.rules.closeUnlockedBy} not taken on).");
            if (game.state.isNight) game.cafe.GoToBed();
            else
            {
                game.cafe.CloseDay();
                if (game.state.isNight) game.cafe.GoToBed();
            }
        }

        private static void DoWhatItAsks(TestGame game, QuestData quest)
        {
            foreach (QuestObjective objective in quest.objectives)
            {
                int qty = System.Math.Max(1, objective.qty);
                switch (objective.type)
                {
                    case ObjectiveType.HaveItems:
                    case ObjectiveType.DeliverItems:
                        int have = game.economy.Count(objective.target);
                        if (have < qty) game.economy.Add(objective.target, qty - have);
                        break;
                    case ObjectiveType.CookRecipe:
                        string recipeId = string.IsNullOrEmpty(objective.target)
                            ? game.design.recipes.Find(r => game.cafe.IsUnlocked(r.id))?.id
                            : objective.target;
                        Assert.IsTrue(recipeId != null && game.cafe.IsUnlocked(recipeId), $"{quest.id} asks to serve {objective.target} before it can be learned.");
                        for (int i = 0; i < qty; i++)
                        {
                            foreach (ItemAmount ingredient in game.design.GetRecipe(recipeId).ingredients) game.economy.Add(ingredient.itemId, ingredient.qty);
                            Assert.IsTrue(game.cafe.Cook(recipeId), $"{quest.id}: couldn't cook {recipeId}");
                        }
                        break;
                    case ObjectiveType.CompleteExpeditions:
                        for (int i = 0; i < qty; i++) game.events.RaiseExpeditionCompleted(new ForageResult());
                        break;
                    case ObjectiveType.TalkTo:
                        game.quests.BeginConversation(objective.target);
                        break;
                    case ObjectiveType.PlaceObject:
                        for (int i = 0; i < qty; i++) game.events.RaiseObjectPlaced(objective.target ?? "");
                        break;
                    case ObjectiveType.CloseDay:
                        for (int i = 0; i < qty; i++) NextDay(game);
                        break;
                }
            }
        }

        [Test]
        public void TheStory_CanBePlayedToTheEnd()
        {
            var game = new TestGame(LoadRealDesign());
            PlayTheStory(game, out _);
            Assert.IsTrue(game.quests.IsMainStoryComplete(), $"The story wasn't finished by day {MaxDays}.");
        }

        [Test]
        public void Closing_IsLockedOnTheFirstMorning()
        {
            var game = new TestGame(LoadRealDesign());
            Assert.IsNotNull(game.design.rules.closeUnlockedBy, "The story has a quest that teaches closing.");
            Assert.IsFalse(game.cafe.CloseUnlocked());
        }

        [Test]
        public void TheTutorial_IsSpreadOverDays_AndNightComesNoSoonerThanDayThree()
        {
            var game = new TestGame(LoadRealDesign());
            Dictionary<string, int> handedIn = PlayTheStory(game, out int nightOn);
            Assert.GreaterOrEqual(nightOn, 3, "Night unlocked too soon.");
            if (handedIn.TryGetValue("quest.main_set_out", out int setOut) && handedIn.TryGetValue("quest.main_welcome", out int welcome))
                Assert.Greater(setOut, welcome, "Furnishing comes after the first day.");
        }
    }
}
