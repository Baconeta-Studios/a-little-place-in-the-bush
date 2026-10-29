using System.Collections.Generic;
using System.Text;
using Bush.Core;
using TMPro;
using UI.Popups;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A character's recipe shop: the few recipes they sell (<see cref="NpcData.shop"/>), at each
/// recipe's price. Opened from their conversation. A template popup, so it pauses the game and
/// Escape closes it. Built in code.
/// </summary>
public class RecipeShopPopup : Popup
{
    private static RecipeShopPopup _instance;

    private NpcDefinition _npc;
    private string _selected;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _coins;
    private ItemGridView _grid;
    private TextMeshProUGUI _details;
    private Button _buy;

    public static void Show(NpcDefinition npc)
    {
        if (!npc || GameSession.Current == null) return;
        if (!_instance) _instance = Create();
        _instance._npc = npc;
        _instance._selected = null;
        _instance.Open();
        _instance.Refresh();
    }

    private static RecipeShopPopup Create()
    {
        RectTransform root = GameUi.NewRect("RecipeShop", GameUi.Current.SafeRoot);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var popup = root.gameObject.AddComponent<RecipeShopPopup>();
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
        if (session == null || !_npc) return;
        DesignData design = session.Design;
        NpcData npc = design.GetNpc(_npc.id);

        _title.text = $"{_npc.displayName}'s recipes";
        _coins.text = $"{session.Economy.Currency} coins";

        var cards = new List<ItemCard>();
        foreach (string recipeId in npc?.shop ?? new List<string>())
        {
            RecipeData recipe = design.GetRecipe(recipeId);
            if (recipe == null) continue;
            bool alreadyKnown = session.Cafe.IsUnlocked(recipe.id);
            ItemData first = recipe.ingredients.Count > 0 ? design.GetItem(recipe.ingredients[0].itemId) : null;
            string id = recipe.id;
            cards.Add(new ItemCard
            {
                key = id,
                title = recipe.name,
                badge = alreadyKnown ? "known" : $"{recipe.price}c",
                icon = ItemGridView.IconFor(first != null ? session.Database.Find<ItemDefinition>(first.id) : null, null, out Color tint),
                iconTint = tint,
                dimmed = alreadyKnown,
                selected = id == _selected,
                onClick = () => { _selected = id; Refresh(); }
            });
        }
        _grid.SetItems(cards);

        RecipeData selected = design.GetRecipe(_selected);
        if (selected == null)
        {
            _details.text = cards.Count == 0 ? "Nothing for sale today." : "Tap a recipe to see what it needs.";
            _buy.gameObject.SetActive(false);
            return;
        }

        var text = new StringBuilder($"<b>{selected.name}</b>  <color=#{ColorUtility.ToHtmlStringRGB(GameUi.InkSoft)}>sells for {selected.coins} coins a dish</color>\n");
        foreach (ItemAmount ingredient in selected.ingredients)
            text.Append($"{design.GetItem(ingredient.itemId)?.name ?? ingredient.itemId} x{ingredient.qty}    ");
        if (!string.IsNullOrEmpty(selected.description)) text.Append($"\n<size=85%>{selected.description}</size>");
        _details.text = text.ToString();

        bool known = session.Cafe.IsUnlocked(selected.id);
        _buy.gameObject.SetActive(true);
        _buy.interactable = session.Cafe.CanLearn(selected.id);
        GameUi.SetLabel(_buy, known ? "Known" : $"Learn, {selected.price}c");
    }

    private void Buy()
    {
        GameSession session = GameSession.Current;
        if (session == null || !session.Cafe.Learn(_selected)) return;
        session.SaveNow();
        Refresh();
    }

    private void Build(RectTransform root)
    {
        GameUi.AddPanel(root, new Color(0.08f, 0.07f, 0.05f, 0.55f), false);

        RectTransform panel = GameUi.NewRect("Panel", root);
        panel.anchorMin = new Vector2(0.12f, 0.08f);
        panel.anchorMax = new Vector2(0.88f, 0.92f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        GameUi.AddPanel(panel, GameUi.Panel);

        _title = GameUi.AddText(panel, "", 50f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Title");
        _title.fontStyle = FontStyles.Bold;
        _title.rectTransform.offsetMin = new Vector2(40f, 0f);
        _title.rectTransform.offsetMax = new Vector2(-480f, -28f);
        _coins = GameUi.AddText(panel, "", 34f, GameUi.Bark, TextAlignmentOptions.TopRight, "Coins");
        _coins.rectTransform.offsetMax = new Vector2(-250f, -38f);

        Button close = GameUi.AddButton(panel, "Close", Close, new Vector2(190f, 84f), GameUi.Bark, GameUi.Cream);
        GameUi.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(190f, 84f));

        RectTransform list = GameUi.NewRect("List", panel);
        list.anchorMin = Vector2.zero;
        list.anchorMax = Vector2.one;
        list.offsetMin = new Vector2(24f, 180f);
        list.offsetMax = new Vector2(-24f, -130f);
        _grid = ItemGridView.Create(list, false, new Vector2(190f, 210f));

        RectTransform details = GameUi.NewRect("Details", panel);
        details.anchorMin = new Vector2(0f, 0f);
        details.anchorMax = new Vector2(1f, 0f);
        details.pivot = new Vector2(0.5f, 0f);
        details.offsetMin = new Vector2(24f, 24f);
        details.offsetMax = new Vector2(-24f, 160f);
        GameUi.AddPanel(details, GameUi.Cream);
        _details = GameUi.AddText(details, "", 28f, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "DetailsText");
        _details.rectTransform.offsetMin = new Vector2(24f, 8f);
        _details.rectTransform.offsetMax = new Vector2(-300f, -8f);
        _buy = GameUi.AddButton(details, "Learn", Buy, new Vector2(260f, 96f), GameUi.Moss, GameUi.Cream, 30f);
        GameUi.Place((RectTransform)_buy.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(260f, 96f));
    }
}
