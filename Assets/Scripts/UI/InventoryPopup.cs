using System.Collections.Generic;
using Bush.Core;
using TMPro;
using UI.Popups;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The bag: everything the player owns, in tabs (Larder, Materials, Treasures, Satchel, Placeables),
/// plus coins. Read-only for now: using, selling and sorting come later. Tap an item to read about it.
/// A template <see cref="Popup"/>, so it pauses the game and Escape closes it.
///
/// How it looks is in Resources/UI/InventoryPopup.prefab (see docs/UI.md), made from <see cref="BuildDefault"/>,
/// which is also used if there's no prefab.
/// </summary>
public class InventoryPopup : Popup
{
    public const string ResourcePath = "UI/InventoryPopup";

    private static readonly (string label, ItemCategory category)[] Tabs =
    {
        ("Larder", ItemCategory.Ingredient),
        ("Materials", ItemCategory.Material),
        ("Treasures", ItemCategory.Treasure),
        ("Satchel", ItemCategory.KeyItem),
        ("Placeables", ItemCategory.Placeable)
    };

    [SerializeField] private RectTransform _tabRow;
    [SerializeField] private Button _tabTemplate;
    [SerializeField] private Color _tabColor = GameUi.Cream;
    [SerializeField] private Color _tabTextColor = GameUi.Ink;
    [SerializeField] private Color _chosenTabColor = new(0.18f, 0.32f, 0.19f);
    [SerializeField] private Color _chosenTabTextColor = GameUi.Cream;
    [SerializeField] private ItemGridView _grid;
    [SerializeField] private TextMeshProUGUI _coins;
    [SerializeField] private TextMeshProUGUI _empty;
    [SerializeField] private Image _detailIcon;
    [SerializeField] private TextMeshProUGUI _detailName;
    [SerializeField] private TextMeshProUGUI _detailMeta;
    [SerializeField] private TextMeshProUGUI _detailText;
    [SerializeField] private Button _back;

    private ItemCategory _tab = ItemCategory.Ingredient;
    private string _selected;

    public static InventoryPopup Create() => GameUi.LoadPopup(ResourcePath, BuildDefault);

    protected override void Awake()
    {
        base.Awake();
        _back.onClick.AddListener(Close);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (GameSession.Current != null) GameSession.Current.Events.InventoryChanged += Refresh;
        Refresh();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (GameSession.Current != null) GameSession.Current.Events.InventoryChanged -= Refresh;
    }

    private void Refresh()
    {
        GameSession session = GameSession.Current;
        if (session == null || !_grid) return;

        _coins.text = $"{session.Economy.Currency} coins";
        RefreshTabs(session);

        var cards = new List<ItemCard>();
        foreach (ItemStack stack in session.Economy.Inventory)
        {
            ItemDefinition item = session.Database.Find<ItemDefinition>(stack.itemId);
            ItemData data = session.Design.GetItem(stack.itemId);
            if (data == null || data.category != _tab) continue;
            PlaceableDefinition placeable = session.Database.placeables.Find(p => p && p.item == item);
            Sprite icon = ItemGridView.IconFor(item, placeable, out Color tint);
            string id = stack.itemId;
            cards.Add(new ItemCard
            {
                key = id,
                title = data.name,
                badge = data.unique ? "" : stack.qty.ToString(),
                icon = icon,
                iconTint = tint,
                selected = id == _selected,
                onClick = () => { _selected = id; Refresh(); }
            });
        }
        cards.Sort((a, b) => string.CompareOrdinal(a.title, b.title));
        _grid.SetItems(cards);

        _empty.gameObject.SetActive(cards.Count == 0);
        _empty.text = _tab == ItemCategory.Placeable && session.Build.UnlimitedItems
            ? "Nothing here yet. Building is in unlimited mode while we prototype, so you don't need any."
            : "Nothing here yet.";

        RefreshDetails(session);
    }

    private void RefreshDetails(GameSession session)
    {
        ItemData selected = session.Design.GetItem(_selected);
        bool show = selected != null && selected.category == _tab;
        _detailIcon.gameObject.SetActive(show);
        _detailMeta.gameObject.SetActive(show);
        if (!show)
        {
            _detailName.text = "";
            _detailText.text = "Tap something to read about it.";
            return;
        }

        ItemDefinition item = session.Database.Find<ItemDefinition>(selected.id);
        PlaceableDefinition placeable = session.Database.placeables.Find(p => p && p.item == item);
        Sprite icon = ItemGridView.IconFor(item, placeable, out Color tint);
        _detailIcon.gameObject.SetActive(icon);
        _detailIcon.sprite = icon;
        _detailIcon.color = tint;
        _detailName.text = selected.name;
        _detailMeta.text = $"{selected.rarity} {CategoryName(selected.category)}" +
                           (selected.sellValue > 0 && selected.category != ItemCategory.KeyItem ? $", worth {selected.sellValue} coins" : "");
        _detailText.text = selected.description;
    }

    private void RefreshTabs(GameSession session)
    {
        GameUi.ClearExcept(_tabRow, _tabTemplate.transform);
        foreach ((string label, ItemCategory category) in Tabs)
        {
            int count = 0;
            foreach (ItemStack stack in session.Economy.Inventory)
                if (session.Design.GetItem(stack.itemId)?.category == category) count++;
            bool chosen = category == _tab;
            ItemCategory tab = category;
            Button button = Instantiate(_tabTemplate, _tabRow);
            button.gameObject.SetActive(true);
            button.targetGraphic.color = chosen ? _chosenTabColor : _tabColor;
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.text = count > 0 ? $"{label} ({count})" : label;
            text.color = chosen ? _chosenTabTextColor : _tabTextColor;
            button.onClick.AddListener(() => { _tab = tab; Refresh(); });
        }
    }

    private static string CategoryName(ItemCategory category) => category switch
    {
        ItemCategory.Ingredient => "ingredient",
        ItemCategory.Material => "material",
        ItemCategory.Treasure => "treasure",
        ItemCategory.KeyItem => "quest item",
        _ => "placeable"
    };

    // ----- The default layout -----

    public static InventoryPopup BuildDefault(Transform parent)
    {
        RectTransform root = GameUi.NewRect("InventoryPopup", parent);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var popup = root.gameObject.AddComponent<InventoryPopup>();
        GameUi.AddBackdrop(root);

        RectTransform paper = GameUi.NewRect("PaperItems", root);
        GameUi.Anchor(paper, Vector2.zero, new Vector2(0.625f, 1f), new Vector2(-4f, -8f), new Vector2(8f, 8f));
        GameUi.AddPanel(paper, GameUi.Panel2);

        TextMeshProUGUI title = GameUi.AddText(paper, "Bag", 54f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Title");
        GameUi.Anchor(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(64f, -130f), new Vector2(-64f, -60f));
        popup._coins = GameUi.AddText(paper, "", 30f, GameUi.Ink, TextAlignmentOptions.TopRight, "Coins");
        GameUi.Anchor(popup._coins.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(64f, -130f), new Vector2(-64f, -70f));

        popup._tabRow = GameUi.NewRect("Tabs", paper);
        GameUi.Anchor(popup._tabRow, new Vector2(0f, 1f), Vector2.one, new Vector2(60f, -206f), new Vector2(-60f, -130f));
        GameUi.AddLayout<HorizontalLayoutGroup>(popup._tabRow, 8f).childAlignment = TextAnchor.MiddleLeft;
        popup._tabTemplate = GameUi.AddButton(popup._tabRow, "Tab", null, new Vector2(205f, 72f), GameUi.Cream, GameUi.Ink, 24f);
        popup._tabTemplate.name = "TabTemplate";
        popup._tabTemplate.gameObject.SetActive(false);

        RectTransform list = GameUi.NewRect("List", paper);
        GameUi.Anchor(list, Vector2.zero, Vector2.one, new Vector2(46f, 56f), new Vector2(-46f, -210f));
        popup._grid = ItemGridView.Create(list, false, new Vector2(196f, 214f), true);
        popup._grid.MakeCardsCounted();
        popup._empty = GameUi.AddText(list, "", 30f, GameUi.InkSoft, TextAlignmentOptions.Center, "Empty");

        RectTransform side = GameUi.NewRect("Side", root);
        GameUi.Anchor(side, new Vector2(0.625f, 0f), Vector2.one, new Vector2(16f, 112f), new Vector2(-26f, 0f));
        GameUi.AddSign(side, "What you've got");

        RectTransform details = GameUi.NewRect("PaperDetails", side);
        GameUi.Anchor(details, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -640f), new Vector2(0f, -262f));
        GameUi.AddPanel(details, GameUi.Panel2).raycastTarget = false;
        RectTransform icon = GameUi.NewRect("Icon", details);
        GameUi.Place(icon, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -52f), new Vector2(120f, 120f));
        popup._detailIcon = icon.gameObject.AddComponent<Image>();
        popup._detailIcon.preserveAspect = true;
        popup._detailIcon.raycastTarget = false;
        popup._detailName = GameUi.AddText(details, "", 32f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Name");
        GameUi.Anchor(popup._detailName.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(184f, -130f), new Vector2(-50f, -60f));
        popup._detailMeta = GameUi.AddText(details, "", 25f, GameUi.InkSoft, TextAlignmentOptions.TopLeft, "Meta");
        GameUi.Anchor(popup._detailMeta.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(184f, -176f), new Vector2(-50f, -126f));
        popup._detailText = GameUi.AddText(details, "", 28f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Description");
        GameUi.Anchor(popup._detailText.rectTransform, Vector2.zero, Vector2.one, new Vector2(50f, 50f), new Vector2(-50f, -184f));

        popup._back = GameUi.AddBack(root);
        return popup;
    }
}
