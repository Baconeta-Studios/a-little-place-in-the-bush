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
    public Button forage;
    public Button build;
    public Button bed;
    public RectTransform tracker;
    public Button trackerButton;
    public TextMeshProUGUI trackerText;
    public DayDial dial;

    public static HudView Build(Transform parent)
    {
        RectTransform root = GameUi.NewRect("Hud", parent);
        GameUi.Stretch(root);
        var view = root.gameObject.AddComponent<HudView>();

        view.bar = GameUi.NewRect("HudBar", root);
        GameUi.Place(view.bar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(1360f, 110f));
        HorizontalLayoutGroup row = GameUi.AddLayout<HorizontalLayoutGroup>(view.bar, 14f);
        row.childAlignment = TextAnchor.MiddleLeft;

        RectTransform coins = GameUi.NewRect("Coins", view.bar);
        var coinsLayout = coins.gameObject.AddComponent<LayoutElement>();
        coinsLayout.preferredWidth = 200f;
        coinsLayout.preferredHeight = 90f;
        GameUi.AddPanel(coins, GameUi.PanelDark).raycastTarget = false;
        view.coins = GameUi.AddText(coins, "", 32f, GameUi.Cream);

        view.bag = GameUi.AddButton(view.bar, "Bag", null, new Vector2(180f, 110f), GameUi.Cream, GameUi.Ink);
        view.quests = GameUi.AddButton(view.bar, "Quests", null, new Vector2(200f, 110f), GameUi.Cream, GameUi.Ink);
        view.forage = GameUi.AddButton(view.bar, "Forage", null, new Vector2(200f, 110f), GameUi.Bark, GameUi.Cream);
        view.build = GameUi.AddButton(view.bar, "Build", null, new Vector2(200f, 110f), GameUi.Moss, GameUi.Cream);
        view.bed = GameUi.AddButton(view.bar, "Go to bed", null, new Vector2(230f, 110f), GameUi.PanelDark, GameUi.Cream);

        // The day dial, top right, left of the quest tracker.
        view.dial = DayDial.Create(root);
        GameUi.Place((RectTransform)view.dial.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f - 560f - 24f, -20f), new Vector2(140f, 140f));

        // The quest tracker: a small card, top right. The whole card opens the quest log.
        view.tracker = GameUi.NewRect("QuestTracker", root);
        GameUi.Place(view.tracker, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(560f, 150f));
        Image card = GameUi.AddPanel(view.tracker, GameUi.Panel);
        view.trackerButton = view.tracker.gameObject.AddComponent<Button>();
        view.trackerButton.targetGraphic = card;
        view.trackerText = GameUi.AddText(view.tracker, "", 28f, GameUi.Ink, TextAlignmentOptions.TopLeft, "TrackerText");
        view.trackerText.rectTransform.offsetMin = new Vector2(22f, 12f);
        view.trackerText.rectTransform.offsetMax = new Vector2(-22f, -12f);
        view.trackerText.overflowMode = TextOverflowModes.Ellipsis;
        return view;
    }
}
