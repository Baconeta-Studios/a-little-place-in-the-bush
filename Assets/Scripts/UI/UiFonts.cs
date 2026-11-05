using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The game's fonts, each with a job: titles, reading, buttons and names, and characters' handwriting.
/// Text built in code gets its font here, going by what it's called and what it's on, so every screen
/// matches without being set up by hand. Lives in Resources/UI/UiFonts.asset. See docs/UI.md.
/// </summary>
public class UiFonts : ScriptableObject
{
    private const string ResourcePath = "UI/UiFonts";

    [Tooltip("Titles and signs.")]
    public TMP_FontAsset title;
    [Tooltip("Anything you read. Also the project's default font (TMP Settings).")]
    public TMP_FontAsset body;
    [Tooltip("Buttons, names and short labels.")]
    public TMP_FontAsset bold;
    [Tooltip("Notes in a character's own handwriting.")]
    public TMP_FontAsset note;

    private static UiFonts _instance;

    public static UiFonts Instance => _instance ? _instance : _instance = Resources.Load<UiFonts>(ResourcePath);

    /// <summary>Gives a piece of text the font for its job. Leaves it alone if the fonts can't be found.</summary>
    public static void Apply(TMP_Text text)
    {
        if (Instance) Use(text, Instance.ForJob(text));
    }

    public static void UseTitle(TMP_Text text)
    {
        if (Instance) Use(text, Instance.title);
    }

    public static void UseBold(TMP_Text text)
    {
        if (Instance) Use(text, Instance.bold);
    }

    private static void Use(TMP_Text text, TMP_FontAsset font)
    {
        if (!font) return;
        text.font = font;
        text.fontStyle = FontStyles.Normal; // these are drawn the weight they should be, so no thickening on top
    }

    private TMP_FontAsset ForJob(TMP_Text text)
    {
        string name = text.gameObject.name;
        bool onButton = text.transform.parent && text.transform.parent.GetComponent<Button>();
        if (name == "TrackerText") return body; // on a button, but it's something you read
        return name is "Title" or "OutText" or "SignTitle" or "CoinsText" ? title
            : name == "Nudge" ? note
            : name is "Letter" or "BadgeText" or "DishName" or "Heading" or "CanMake" or "Name" or "Day" || onButton ? bold
            : body;
    }
}
