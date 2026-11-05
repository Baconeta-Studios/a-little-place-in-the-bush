using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The game's two fonts: one for titles and signs, and one for everything else, in bold for buttons and names.
/// Text built in code gets its font here, going by what it's called and what it's on, so every screen
/// matches without being set up by hand. Lives in Resources/UI/UiFonts.asset. See docs/UI.md.
/// </summary>
public class UiFonts : ScriptableObject
{
    private const string ResourcePath = "UI/UiFonts";

    [Tooltip("Titles and signs.")]
    public TMP_FontAsset title;
    [Tooltip("Everything else, in bold for buttons, names and short labels. Also the project's default font (TMP Settings).")]
    public TMP_FontAsset body;

    private static UiFonts _instance;

    public static UiFonts Instance => _instance ? _instance : _instance = Resources.Load<UiFonts>(ResourcePath);

    /// <summary>Gives a piece of text the font for its job. Leaves it alone if the fonts can't be found.</summary>
    public static void Apply(TMP_Text text)
    {
        if (!Instance) return;
        bool title = Instance.IsTitle(text);
        Use(text, title ? Instance.title : Instance.body, !title && IsBold(text));
    }

    public static void UseTitle(TMP_Text text)
    {
        if (Instance) Use(text, Instance.title, false);
    }

    public static void UseBold(TMP_Text text)
    {
        if (Instance) Use(text, Instance.body, true);
    }

    private static void Use(TMP_Text text, TMP_FontAsset font, bool bold)
    {
        if (!font) return;
        text.font = font;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
    }

    private bool IsTitle(TMP_Text text) => text.gameObject.name is "Title" or "OutText" or "SignTitle" or "CoinsText";

    private static bool IsBold(TMP_Text text)
    {
        string name = text.gameObject.name;
        if (name == "TrackerText") return false; // on a button, but it's something you read
        bool onButton = text.transform.parent && text.transform.parent.GetComponent<Button>();
        return onButton || name is "Letter" or "BadgeText" or "DishName" or "Heading" or "CanMake" or "Name" or "Day";
    }
}
