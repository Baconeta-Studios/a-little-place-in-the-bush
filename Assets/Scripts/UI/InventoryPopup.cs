using System.Collections.Generic;
using Bush.Core;
using TMPro;
using UI.Popups;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The bag: everything the player owns, in tabs (Larder, Materials, Treasures, Satchel, Placeables),
/// plus coins. Read-only for now: using, selling and sorting come later. Tap an item to read about it.
/// A template <see cref="Popup"/>, so it pauses the game and Escape closes it. Built in code.
/// </summary>
public class InventoryPopup : Popup
{
    private static readonly (string label, ItemCategory category)[] Tabs =
    {
        ("Larder", ItemCategory.Ingredient),
        ("Materials", ItemCategory.Material),
        ("Treasures", ItemCategory.Treasure),
        ("Satchel", ItemCategory.KeyItem),
        ("Placeables", ItemCategory.Placeable)
    };

    private ItemCategory _tab = ItemCategory.Ingredient;
    private string _selected;
    private RectTransform _tabRow;
    private ItemGridView _grid;
    private TextMeshProUGUI _coins;
    private TextMeshProUGUI _details;
    private TextMeshProUGUI _empty;

    public static InventoryPopup Create()
    {
        GameUi ui = GameUi.Current;
        RectTransform root = GameUi.NewRect("Bag", ui.SafeRoot);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var popup = root.gameObject.AddComponent<InventoryPopup>();
        popup.Build(root);
        return popup;
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
                badge = data.unique ? "" : "x" + stack.qty,
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

        ItemData selected = session.Design.GetItem(_selected);
        _details.text = selected == null || selected.category != _tab
            ? "Tap something to read about it."
            : $"<b>{selected.name}</b>  <color=#6b5c47>{selected.rarity} {CategoryName(selected.category)}" +
              (selected.sellValue > 0 && selected.category != ItemCategory.KeyItem ? $", worth {selected.sellValue} coins" : "") +
              $"</color>\n{selected.description}";
    }

    private void RefreshTabs(GameSession session)
    {
        for (int i = _tabRow.childCount - 1; i >= 0; i--) Destroy(_tabRow.GetChild(i).gameObject);
        foreach ((string label, ItemCategory category) in Tabs)
        {
            int count = 0;
            foreach (ItemStack stack in session.Economy.Inventory)
                if (session.Design.GetItem(stack.itemId)?.category == category) count++;
            bool selected = category == _tab;
            ItemCategory tab = category;
            GameUi.AddButton(_tabRow, count > 0 ? $"{label} ({count})" : label, () => { _tab = tab; Refresh(); },
                new Vector2(230f, 70f), selected ? GameUi.Moss : GameUi.Cream, selected ? GameUi.Cream : GameUi.Ink, 28f);
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

    private void Build(RectTransform root)
    {
        // Dim the game behind, and block clicks to it.
        GameUi.AddPanel(root, new Color(0.08f, 0.07f, 0.05f, 0.55f), false);

        RectTransform panel = GameUi.NewRect("Panel", root);
        GameUi.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 900f));
        GameUi.AddPanel(panel, GameUi.Panel);

        TextMeshProUGUI title = GameUi.AddText(panel, "Bag", 54f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Title");
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.offsetMin = new Vector2(40f, 0f);
        title.rectTransform.offsetMax = new Vector2(0f, -28f);

        _coins = GameUi.AddText(panel, "", 36f, GameUi.Bark, TextAlignmentOptions.TopRight, "Coins");
        _coins.rectTransform.offsetMin = new Vector2(0f, 0f);
        _coins.rectTransform.offsetMax = new Vector2(-250f, -38f);

        Button close = GameUi.AddButton(panel, "Close", Close, new Vector2(190f, 84f), GameUi.Bark, GameUi.Cream);
        GameUi.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(190f, 84f));

        _tabRow = GameUi.NewRect("Tabs", panel);
        _tabRow.anchorMin = new Vector2(0f, 1f);
        _tabRow.anchorMax = new Vector2(1f, 1f);
        _tabRow.pivot = new Vector2(0.5f, 1f);
        _tabRow.offsetMin = new Vector2(32f, -200f);
        _tabRow.offsetMax = new Vector2(-32f, -124f);
        HorizontalLayoutGroup row = GameUi.AddLayout<HorizontalLayoutGroup>(_tabRow, 12f);
        row.childAlignment = TextAnchor.MiddleLeft;

        RectTransform list = GameUi.NewRect("List", panel);
        list.anchorMin = Vector2.zero;
        list.anchorMax = Vector2.one;
        list.offsetMin = new Vector2(24f, 170f);
        list.offsetMax = new Vector2(-24f, -214f);
        _grid = ItemGridView.Create(list, false, new Vector2(190f, 210f));

        _empty = GameUi.AddText(list, "", 30f, GameUi.InkSoft, TextAlignmentOptions.Center, "Empty");

        RectTransform details = GameUi.NewRect("Details", panel);
        details.anchorMin = new Vector2(0f, 0f);
        details.anchorMax = new Vector2(1f, 0f);
        details.pivot = new Vector2(0.5f, 0f);
        details.offsetMin = new Vector2(24f, 24f);
        details.offsetMax = new Vector2(-24f, 150f);
        GameUi.AddPanel(details, GameUi.Cream);
        _details = GameUi.AddText(details, "", 28f, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "DetailsText");
        _details.rectTransform.offsetMin = new Vector2(24f, 8f);
        _details.rectTransform.offsetMax = new Vector2(-24f, -8f);
    }
}
