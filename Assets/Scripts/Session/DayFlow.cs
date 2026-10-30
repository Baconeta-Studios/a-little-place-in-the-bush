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
        if (!AreaTravel.Instance.PassTime(title, CatchUp, MorningNews())) CatchUp();
    }

    /// <summary>This morning's event and doorstep gift, for under the day's title. Empty at night or with no event.</summary>
    public static string MorningNews()
    {
        GameSession session = GameSession.Current;
        if (session == null || session.State.isNight) return "";
        Bush.Core.DayEventData today = Bush.Core.DayEvents.Today(session.Design, session.State);
        if (today == null) return "";
        string news = today.line;
        Bush.Core.ItemData gift = session.Design.GetItem(session.State.giftItem);
        if (gift != null)
            news += session.State.giftExclusive ? $"\nSomething special: {gift.name}!" : $"\n{session.State.giftQty} x {gift.name}, in your bag.";
        return news;
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
