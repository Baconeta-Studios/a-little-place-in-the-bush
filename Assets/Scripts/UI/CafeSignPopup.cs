using Bush.Core;
using TMPro;
using UI.Popups;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What the sign by the cafe door says: what kind of cafe you've made (from the styles of what's
/// placed), how cosy it is, and what that cosiness does. See <see cref="CafeStyle"/>. Opened by a
/// <see cref="CafeSign"/>. A template popup, so it pauses the game and Escape closes it. Built in code.
/// </summary>
public class CafeSignPopup : Popup
{
    private static CafeSignPopup _instance;

    private TextMeshProUGUI _title;
    private TextMeshProUGUI _text;

    public static void Show()
    {
        if (GameSession.Current == null) return;
        if (!_instance) _instance = Create();
        _instance.Open();
        _instance.Refresh();
    }

    private static CafeSignPopup Create()
    {
        RectTransform root = GameUi.NewRect("CafeSign", GameUi.Current.SafeRoot);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var popup = root.gameObject.AddComponent<CafeSignPopup>();
        popup.Build(root);
        return popup;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Refresh()
    {
        GameSession session = GameSession.Current;
        CafeDescription cafe = CafeStyle.Describe(session.Design, session.State);
        _title.text = cafe.title;

        string soft = ColorUtility.ToHtmlStringRGB(GameUi.InkSoft);
        int visitorBoost = Mathf.RoundToInt((float)(CafeStyle.VisitorBoost(session.Design, session.State) - 1) * 100f);
        int luck = Mathf.RoundToInt((float)CafeStyle.LuckyDayChance(session.Design, session.State) * 100f);
        _text.text = $"<i>{cafe.blurb}</i>\n\n" +
                     $"<b>Cosiness:</b> {cafe.level} ({cafe.cosiness})\n" +
                     $"<color=#{soft}>{cafe.things} things out, {cafe.seats} seat{(cafe.seats == 1 ? "" : "s")}.</color>\n\n" +
                     $"Visitors drop by {(visitorBoost > 0 ? $"{visitorBoost}% more often" : "now and then")}, " +
                     $"and there's a {luck}% chance of a lucky day.\n" +
                     $"<color=#{soft}>More things, and more kinds of things, make it cosier.</color>";
    }

    private void Build(RectTransform root)
    {
        GameUi.AddPanel(root, new Color(0.08f, 0.07f, 0.05f, 0.55f), false);

        RectTransform panel = GameUi.NewRect("Sign", root);
        GameUi.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 640f));
        GameUi.AddPanel(panel, GameUi.Cream);

        _title = GameUi.AddText(panel, "", 56f, GameUi.Bark, TextAlignmentOptions.Top, "Title");
        _title.fontStyle = FontStyles.Bold;
        _title.rectTransform.offsetMin = new Vector2(40f, 0f);
        _title.rectTransform.offsetMax = new Vector2(-40f, -36f);

        _text = GameUi.AddText(panel, "", 34f, GameUi.Ink, TextAlignmentOptions.Top, "Text");
        _text.rectTransform.offsetMin = new Vector2(60f, 140f);
        _text.rectTransform.offsetMax = new Vector2(-60f, -130f);

        Button close = GameUi.AddButton(panel, "Nice", Close, new Vector2(240f, 96f), GameUi.Moss, GameUi.Cream);
        GameUi.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(240f, 96f));
    }
}
