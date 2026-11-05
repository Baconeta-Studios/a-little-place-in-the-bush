namespace Bush.Core
{
    /// <summary>
    /// Every sound the game asks for by name. Each one is a row in the sound library. To add one, add it at the end
    /// with the next number: rows are saved by number, so don't change or reuse a number.
    /// </summary>
    public enum GameSound
    {
        MusicMainMenu = 100,
        MusicCafeDay = 101,
        MusicCafeEvening = 102,
        MusicCafeNight = 103,
        MusicForaging = 104,

        AmbientBushDay = 200,
        AmbientBushNight = 201,

        UiTap = 300,
        UiBack = 301,
        UiNope = 302,
        PaperOpen = 303,
        PaperClose = 304,

        Footstep = 400,
        ForageSetOff = 401,
        ForageFind = 402,
        ForageRareFind = 403,
        ForageHome = 404,
        Coins = 405,
        QuestStarted = 406,
        QuestDone = 407,
        RecipeLearned = 408,
        PerkLearned = 409,
        UpgradeBought = 410,
        MenuChanged = 411,
        DayEnded = 412,
        NightFalls = 413,
        Morning = 414,
        PlaceObject = 415,
        PickUpObject = 416,
        PutAwayObject = 417
    }

    /// <summary>Which music and background sound suit the moment.</summary>
    public static class SoundChoice
    {
        public static GameSound MusicFor(DesignData design, GameState state, bool foraging)
        {
            if (foraging) return GameSound.MusicForaging;
            if (state.isNight) return GameSound.MusicCafeNight;
            return design.rules.IsEvening(state) ? GameSound.MusicCafeEvening : GameSound.MusicCafeDay;
        }

        public static GameSound AmbientFor(GameState state) => state.isNight ? GameSound.AmbientBushNight : GameSound.AmbientBushDay;
    }
}
