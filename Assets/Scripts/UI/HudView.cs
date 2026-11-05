using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The HUD's look: the root of Resources/UI/HUD.prefab, holding the pieces <see cref="GameHud"/> drives.
/// Move, restyle or swap art on anything in the prefab; only the links here matter. Delete a button you
/// don't want and clear its link. <see cref="Build"/> is the default layout: Tools > Bush > Create UI
/// Prefabs saves it as the prefab, and the game uses it directly if there's no prefab yet.
/// </summary>
public class HudView : MonoBehaviour
{
    public const string ResourcePath = "UI/HUD";

    public RectTransform bar;
    public TextMeshProUGUI coins;
    public Button bag;
    public Button quests;
    public GameObject questsMarker;
    public Button forage;
    public Button build;
    public Button bed;
    [Tooltip("The job note: the story's current step. Tap it for the quests, or its x to tuck it away.")]
    public RectTransform tracker;
    public Button trackerButton;
    public TextMeshProUGUI trackerText;
    public Button trackerTuck;
    [Tooltip("What's left when the job note is tucked away. Tap it to bring the note back.")]
    public Button trackerTab;
    public GameObject trackerTabMarker;
    public DayDial dial;
    public DayInfoCard dayInfo;

    public static HudView Build(Transform parent)
    {
        RectTransform root = GameUi.NewRect("Hud", parent);
        GameUi.Stretch(root);
        var view = root.gameObject.AddComponent<HudView>();

        // Coins, top left, beside the settings button (which is on the pause menu's canvas).
        view.coins = GameUi.AddText(root, "", 46f, GameUi.Cream, TextAlignmentOptions.MidlineLeft, "CoinsText");
        GameUi.Place(view.coins.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(128f, -30f), new Vector2(420f, 64f));
        view.coins.raycastTarget = false;

        // Bag, quests and the rest, bottom left, in reach of a thumb. Objects, with no box around them.
        view.bar = GameUi.NewRect("HudBar", root);
        GameUi.Place(view.bar, Vector2.zero, Vector2.zero, new Vector2(22f, 14f), new Vector2(900f, 136f));
        GameUi.AddLayout<HorizontalLayoutGroup>(view.bar, 8f).childAlignment = TextAnchor.LowerLeft;

        view.bag = Tool(view.bar, "Bag", "BagBack", out TextMeshProUGUI bagLabel);
        RectTransform front = GameUi.NewRect("BagFront", view.bag.transform.Find("BagBack"));
        GameUi.Stretch(front);
        front.gameObject.AddComponent<Image>().raycastTarget = false;
        front.GetComponent<Image>().preserveAspect = true;
        view.quests = Tool(view.bar, "Quests", "RecipeIcon", out TextMeshProUGUI questsLabel);
        view.questsMarker = Marker(view.quests.transform);
        view.forage = Tool(view.bar, "Forage", "BushIcon", out TextMeshProUGUI forageLabel);
        view.build = Tool(view.bar, "Build", "StumpIcon", out TextMeshProUGUI buildLabel);
        view.bed = GameUi.AddButton(view.bar, "Go to bed", null, new Vector2(230f, 96f), GameUi.PanelDark, GameUi.Cream, 30f);

        // The day dial, top right. Tap it for what's on today.
        view.dial = DayDial.Create(root);
        GameUi.Place((RectTransform)view.dial.transform, Vector2.one, Vector2.one, new Vector2(-24f, -16f), new Vector2(140f, 140f));

        // The job note, beside it: tap for the quests, x to tuck it away.
        view.tracker = GameUi.NewRect("PaperTracker", root);
        GameUi.Place(view.tracker, Vector2.one, Vector2.one, new Vector2(-176f, -2f), new Vector2(560f, 172f));
        Image card = GameUi.AddPanel(view.tracker, GameUi.Panel2);
        view.trackerButton = view.tracker.gameObject.AddComponent<Button>();
        view.trackerButton.targetGraphic = card;
        view.trackerText = GameUi.AddText(view.tracker, "", 27f, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "TrackerText");
        view.trackerText.rectTransform.offsetMin = new Vector2(44f, 38f);
        view.trackerText.rectTransform.offsetMax = new Vector2(-104f, -38f);
        view.trackerText.overflowMode = TextOverflowModes.Ellipsis;
        view.trackerTuck = GameUi.AddButton(view.tracker, "x", null, new Vector2(72f, 72f), GameUi.PanelDark, GameUi.Cream, 28f);
        GameUi.Place((RectTransform)view.trackerTuck.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(72f, 72f));

        // What's left when it's tucked away.
        view.trackerTab = Tool(root, "Job", "ScrapIcon", out TextMeshProUGUI tabLabel);
        GameUi.Place((RectTransform)view.trackerTab.transform, Vector2.one, Vector2.one, new Vector2(-176f, -12f), new Vector2(110f, 130f));
        view.trackerTabMarker = Marker(view.trackerTab.transform);
        view.trackerTab.gameObject.SetActive(false);

        view.dayInfo = DayInfoCard.Build(root);
        foreach (TextMeshProUGUI onScenery in new[] { view.coins, bagLabel, questsLabel, forageLabel, buildLabel, tabLabel })
            onScenery.gameObject.AddComponent<TextOutline>();
        return view;
    }

    /// <summary>A button that's a picture with a word under it. Big enough for a thumb.</summary>
    private static Button Tool(Transform parent, string label, string iconName, out TextMeshProUGUI text)
    {
        RectTransform rect = GameUi.NewRect(label + "Button", parent);
        rect.sizeDelta = new Vector2(124f, 130f);
        var size = rect.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = 124f;
        size.preferredHeight = 130f;
        Image catcher = GameUi.AddPanel(rect, Color.clear, false);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = catcher;
        button.transition = Selectable.Transition.None;

        RectTransform icon = GameUi.NewRect(iconName, rect);
        icon.anchorMin = new Vector2(0.1f, 0.3f);
        icon.anchorMax = new Vector2(0.9f, 1f);
        icon.offsetMin = icon.offsetMax = Vector2.zero;
        var image = icon.gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;

        text = GameUi.AddText(rect, label, 26f, GameUi.Cream, TextAlignmentOptions.Center, "Label");
        UiFonts.UseBold(text);
        text.rectTransform.anchorMax = new Vector2(1f, 0.3f);
        return button;
    }

    /// <summary>A small ! on a button, for something waiting.</summary>
    private static GameObject Marker(Transform parent)
    {
        RectTransform rect = GameUi.NewRect("Marker", parent);
        GameUi.Place(rect, Vector2.one, new Vector2(1f, 1f), new Vector2(-6f, 0f), new Vector2(38f, 38f));
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = GeneratedSprites.Circle;
        image.color = GameUi.Apron;
        image.raycastTarget = false;
        GameUi.AddText(rect, "!", 26f, GameUi.Cream, TextAlignmentOptions.Center, "BadgeText");
        rect.gameObject.SetActive(false);
        return rect.gameObject;
    }
}
