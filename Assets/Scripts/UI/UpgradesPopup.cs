using Bush.Core;
using TMPro;
using UI.Popups;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cafe upgrades, opened from the menu board: each bought once with coins, in ladders (the second
/// menu slot before the third), pricier further down. A template popup, so it pauses the game and
/// Escape closes it.
///
/// How it looks is in Resources/UI/UpgradesPopup.prefab (see docs/UI.md), made from <see cref="BuildDefault"/>,
/// which is also used if there's no prefab.
/// </summary>
public class UpgradesPopup : Popup
{
    public const string ResourcePath = "UI/UpgradesPopup";

    private static UpgradesPopup _instance;

    [SerializeField] private TextMeshProUGUI _coins;
    [SerializeField] private RectTransform _list;
    [SerializeField] private RectTransform _rowTemplate;
    [SerializeField] private TextMeshProUGUI _empty;
    [SerializeField] private Button _back;

    public static void Show()
    {
        if (GameSession.Current == null) return;
        if (!_instance) _instance = GameUi.LoadPopup(ResourcePath, BuildDefault);
        _instance.Open();
        _instance.Refresh();
    }

    protected override void Awake()
    {
        base.Awake();
        _back.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Refresh()
    {
        GameSession session = GameSession.Current;
        if (session == null) return;
        _coins.text = $"{session.Economy.Currency} coins";

        GameUi.ClearExcept(_list, _rowTemplate);
        _empty.gameObject.SetActive(session.Design.rules.upgrades.Count == 0);
        foreach (UpgradeData upgrade in session.Design.rules.upgrades)
        {
            bool owned = session.Cafe.HasUpgrade(upgrade.id);
            // Hide the far end of a ladder until the step before it is bought, so the list stays short.
            if (!owned && !string.IsNullOrEmpty(upgrade.requires) && !session.Cafe.HasUpgrade(upgrade.requires)) continue;
            AddRow(session, upgrade, owned);
        }
    }

    private void AddRow(GameSession session, UpgradeData upgrade, bool owned)
    {
        RectTransform row = Instantiate(_rowTemplate, _list);
        row.gameObject.SetActive(true);
        row.Find("Words/Name").GetComponent<TextMeshProUGUI>().text = upgrade.name;
        row.Find("Words/Description").GetComponent<TextMeshProUGUI>().text = upgrade.description;
        row.Find("Bought").gameObject.SetActive(owned);

        Button buy = row.Find("Buy").GetComponent<Button>();
        buy.gameObject.SetActive(!owned);
        if (owned) return;
        GameUi.SetLabel(buy, $"{upgrade.price} coins");
        buy.interactable = session.Cafe.CanBuyUpgrade(upgrade.id, out _);
        string id = upgrade.id;
        buy.onClick.AddListener(() =>
        {
            if (session.Cafe.BuyUpgrade(id)) session.SaveNow();
            Refresh();
        });
    }

    // ----- The default layout -----

    public static UpgradesPopup BuildDefault(Transform parent)
    {
        RectTransform root = GameUi.NewRect("UpgradesPopup", parent);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var popup = root.gameObject.AddComponent<UpgradesPopup>();
        GameUi.AddBackdrop(root);

        RectTransform paper = GameUi.NewRect("PaperUpgrades", root);
        GameUi.Anchor(paper, Vector2.zero, new Vector2(0.625f, 1f), new Vector2(-4f, -8f), new Vector2(8f, 8f));
        GameUi.AddPanel(paper, GameUi.Panel2);

        TextMeshProUGUI title = GameUi.AddText(paper, "Make the cafe better", 54f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Title");
        GameUi.Anchor(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(64f, -130f), new Vector2(-64f, -60f));
        popup._coins = GameUi.AddText(paper, "", 30f, GameUi.Ink, TextAlignmentOptions.TopRight, "Coins");
        GameUi.Anchor(popup._coins.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(64f, -130f), new Vector2(-64f, -70f));

        RectTransform list = GameUi.NewRect("List", paper);
        GameUi.Anchor(list, Vector2.zero, Vector2.one, new Vector2(56f, 56f), new Vector2(-56f, -136f));
        GameUi.AddScroll(list, out popup._list);
        popup._list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        VerticalLayoutGroup column = GameUi.AddLayout<VerticalLayoutGroup>(popup._list, 12f, new RectOffset(4, 4, 4, 4));
        column.childForceExpandWidth = true;
        popup._rowTemplate = RowTemplate(popup._list);
        popup._empty = GameUi.AddText(list, "No upgrades yet.", 30f, GameUi.InkSoft, TextAlignmentOptions.Center, "Empty");

        RectTransform side = GameUi.NewRect("Side", root);
        GameUi.Anchor(side, new Vector2(0.625f, 0f), Vector2.one, new Vector2(16f, 112f), new Vector2(-26f, 0f));
        GameUi.AddSign(side, "Upgrades");
        RectTransform gecko = GameUi.NewRect("Gecko", side);
        GameUi.Place(gecko, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(420f, 480f));
        var geckoImage = gecko.gameObject.AddComponent<Image>();
        geckoImage.preserveAspect = true;
        geckoImage.raycastTarget = false;

        popup._back = GameUi.AddBack(root);
        return popup;
    }

    private static RectTransform RowTemplate(RectTransform parent)
    {
        RectTransform row = GameUi.NewRect("RowTemplate", parent);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 118f;
        GameUi.AddPanel(row, GameUi.CardBack).raycastTarget = false;

        RectTransform words = GameUi.NewRect("Words", row);
        GameUi.Anchor(words, Vector2.zero, Vector2.one, new Vector2(20f, 8f), new Vector2(-270f, -8f));
        VerticalLayoutGroup column = GameUi.AddLayout<VerticalLayoutGroup>(words, 2f);
        column.childForceExpandWidth = true;
        column.childAlignment = TextAnchor.MiddleLeft;
        GameUi.AddText(words, "", 29f, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "Name");
        GameUi.AddText(words, "", 26f, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "Description");

        Button buy = GameUi.AddButton(row, "Buy", null, new Vector2(230f, 88f), GameUi.Cream, GameUi.Ink, 28f);
        buy.name = "Buy";
        GameUi.Place((RectTransform)buy.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(230f, 88f));

        TextMeshProUGUI bought = GameUi.AddText(row, "Bought", 28f, new Color(0.17f, 0.29f, 0.13f), TextAlignmentOptions.MidlineRight, "Bought");
        UiFonts.UseBold(bought);
        GameUi.Place(bought.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(230f, 88f));

        row.gameObject.SetActive(false);
        return row;
    }
}
