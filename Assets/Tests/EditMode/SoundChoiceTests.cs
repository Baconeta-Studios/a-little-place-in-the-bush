using NUnit.Framework;

namespace Bush.Core.Tests
{
    public class SoundChoiceTests
    {
        private static DesignData Design() => new() { rules = new DayRules { forageTripsPerDay = 2 } };

        [Test]
        public void Music_FollowsTheDay()
        {
            DesignData design = Design();
            var state = new GameState { forageTripsToday = 0 };
            Assert.AreEqual(GameSound.MusicCafeDay, SoundChoice.MusicFor(design, state, false));

            state.forageTripsToday = design.rules.TripsPerDay(state);
            Assert.AreEqual(GameSound.MusicCafeEvening, SoundChoice.MusicFor(design, state, false));

            state.isNight = true;
            Assert.AreEqual(GameSound.MusicCafeNight, SoundChoice.MusicFor(design, state, false));
        }

        [Test]
        public void Music_ForagingWinsWheneverItIs()
        {
            DesignData design = Design();
            Assert.AreEqual(GameSound.MusicForaging, SoundChoice.MusicFor(design, new GameState { isNight = true }, true));
        }

        [Test]
        public void Ambient_FollowsDayAndNight()
        {
            Assert.AreEqual(GameSound.AmbientBushDay, SoundChoice.AmbientFor(new GameState()));
            Assert.AreEqual(GameSound.AmbientBushNight, SoundChoice.AmbientFor(new GameState { isNight = true }));
        }

        [Test]
        public void EverySound_HasItsOwnNumber()
        {
            var values = System.Enum.GetValues(typeof(GameSound));
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (GameSound sound in values) Assert.IsTrue(seen.Add((int)sound), $"{sound} shares a number");
        }
    }
}
