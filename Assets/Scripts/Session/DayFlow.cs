/// <summary>
/// The fades between parts of the day: into the night after closing, and into the next morning when
/// you go to bed. The rules change straight away; the world holds what it showed
/// (<see cref="DayTransition"/>) until the screen is black, then the lighting and guests switch over.
/// </summary>
public static class DayFlow
{
    /// <summary>Fades to black, shows <paramref name="title"/>, and lets the world catch up with the rules while it's dark.</summary>
    public static void FadeInto(string title)
    {
        DayTransition.Holding = true;
        if (!AreaTravel.Instance.PassTime(title, CatchUp)) CatchUp();
    }

    public static void GoToBed()
    {
        GameSession session = GameSession.Current;
        if (session == null || !session.State.isNight) return;
        DayTransition.Holding = true; // keep tonight in view until it's dark
        session.Cafe.GoToBed();
        session.SaveNow();
        FadeInto($"Day {session.State.day}");
    }

    private static void CatchUp()
    {
        DayTransition.Holding = false;
        DayNightLighting.SnapNow();
    }
}
