/// <summary>
/// While true, the world keeps showing the day that just ended: guests don't come or go, change seats
/// or update their quest markers. Set when the cafe closes for the day (the rules move to the next
/// morning straight away), and cleared once the screen has faded to black, so nothing visibly changes
/// behind the summary or the fade.
/// </summary>
public static class DayTransition
{
    public static bool Holding { get; set; }
}
