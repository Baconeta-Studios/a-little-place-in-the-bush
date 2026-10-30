using System.Collections.Generic;
using System.Text;
using Bush.Core;
using TMPro;
using UI.Popups;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The cafe's menu board, behind the cafe door (<see cref="CafeSpot"/>). Shows today's menu and the
/// recipes you know; tap one to read its ingredients and put it on or take it off the menu. Changes
/// count straight away. The day's forage trips are its clock (see <see cref="DayRules"/>): once they're
/// all taken it's evening, and "Close for the day" sells a full day's dishes. Closing before then asks
/// first, because it sells only the share of the day so far and loses the trips left. After the day's
/// summary, starting the next day closes the screen and fades through black. Built in code.
/// </summary>
public class CafePopup : Popup
{
    private static CafePopup _instance;

    private string _selected;

    private RectTransform _board;
    private RectTransform _summary;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _menuText;
    private ItemGridView _recipes;
    private TextMeshProUGUI _empty;
    private TextMeshProUGUI _details;
    private Button _menuButton;
    private Button _closeDay;
    private RectTransform _confirm;
    private TextMeshProUGUI _confirmText;
    private Button _confirmStay;
    private Button _confirmGo;
    private TextMeshProUGUI _summaryTitle;
    private TextMeshProUGUI _summaryText;
    private Button _nextDay;

    /// <summary>Opens the menu board, creating it the first time.</summary>
    public static void Show()
    {
        if (!_instance) _instance = Create();
        _instance.Open();
    }

    private static CafePopup Create()
    {
        RectTransform root = GameUi.NewRect("Cafe", GameUi.Current.SafeRoot);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var popup = root.gameObject.AddComponent<CafePopup>();
        popup.Build(root);
        return popup;
    }

    private static GameSession Session => GameSession.Current;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (Session != null)
        {
            Session.Events.InventoryChanged += Refresh;
            Session.Events.MenuChanged += Refresh;
            Session.Events.RecipeLearned += OnRecipeLearned;
        }
        ShowBoard();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (Session != null)
        {
            Session.Events.InventoryChanged -= Refresh;
            Session.Events.MenuChanged -= Refresh;
            Session.Events.RecipeLearned -= OnRecipeLearned;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void OnRecipeLearned(string recipeId) => Refresh();

    // ----- The menu board -----

    private void ShowBoard()
    {
        _board.gameObject.SetActive(true);
        _summary.gameObject.SetActive(false);
        _confirm.gameObject.SetActive(false);
        Refresh();
    }

    private void Refresh()
    {
        GameSession session = Session;
        if (session == null || !_recipes || !_board.gameObject.activeSelf) return;
        CafeService cafe = session.Cafe;
        DesignData design = session.Design;

        DayRules rules = design.rules;
        string part = PartOfDay(rules, session.State);
        _title.text = $"The cafe, day {session.State.day}" + (part != null ? $": {part}" : "");
        bool evening = rules.IsEvening(session.State);
        GameUi.SetLabel(_closeDay, session.State.isNight ? "Go to bed" : evening ? "Close for the day" : "Close early");

        // Today's menu, with what the cafe will serve while you're out on the next trip.
        List<DishSale> plan = cafe.PlanSales(out int unsold);
        int slots = design.rules.MenuSlots(session.State);
        int now = rules.NextPartSales(session.State);
        int soldToday = 0, coinsToday = 0;
        foreach (DishSale dish in session.State.soldToday)
        {
            soldToday += dish.sold;
            coinsToday += dish.coins;
        }
        string sales = session.State.isNight
            ? "The cafe's closed for the night. The menu here is for tomorrow."
            : evening && rules.TripsPerDay(session.State) > 0
                ? $"Today's service is done: {soldToday} sold ({coinsToday} coins). Menu changes now are for tomorrow."
                : rules.TripsPerDay(session.State) <= 0
                    ? $"Sold today: {soldToday} ({coinsToday} coins)."
                    : $"Serves {now} while you're out on your next trip. Sold today: {soldToday} ({coinsToday} coins).";
        DayEventData today = DayEvents.Today(design, session.State);
        if (today != null && (today.extraSales > 0 || (today.critic && !session.State.criticDone)))
            sales += $" <color={Hex(GameUi.Moss)}>{today.name}: {today.effect}</color>";
        var menu = new StringBuilder($"<b>Today's menu</b>  <color={Hex(GameUi.InkSoft)}>({cafe.Menu.Count}/{slots})  {sales}</color>\n");
        if (cafe.Menu.Count == 0) menu.Append($"<color={Hex(GameUi.InkSoft)}>Nothing yet. Pick a recipe below.</color>");
        foreach (DishSale dish in plan)
        {
            RecipeData recipe = design.GetRecipe(dish.recipeId);
            string sells = dish.sold > 0 ? $"next trip: {dish.sold}"
                : now == 0 || evening || session.State.isNight ? "on the menu"
                : $"<color={Hex(GameUi.Berry)}>missing ingredients</color>";
            menu.Append($"{recipe.name} <color={Hex(GameUi.InkSoft)}>({sells})</color>     ");
        }
        if (cafe.Menu.Count > 0 && unsold > 0)
            menu.Append($"\n<color={Hex(GameUi.Berry)}>Not enough ingredients for {unsold} of the next trip's guests.</color>");
        _menuText.text = menu.ToString();

        // The recipes you know.
        var cards = new List<ItemCard>();
        foreach (RecipeData recipe in design.recipes)
        {
            if (!cafe.IsUnlocked(recipe.id)) continue;
            string id = recipe.id;
            bool canMakeOne = cafe.CanCook(id, out _);
            ItemData firstIngredient = recipe.ingredients.Count > 0 ? design.GetItem(recipe.ingredients[0].itemId) : null;
            ItemDefinition icon = firstIngredient != null ? session.Database.Find<ItemDefinition>(firstIngredient.id) : null;
            cards.Add(new ItemCard
            {
                key = id,
                title = recipe.name,
                badge = cafe.IsOnMenu(id) ? "on menu" : "",
                icon = ItemGridView.IconFor(icon, null, out Color tint),
                iconTint = tint,
                dimmed = !canMakeOne,
                selected = id == _selected,
                onClick = () => { _selected = id; Refresh(); }
            });
        }
        _recipes.SetItems(cards);
        _empty.gameObject.SetActive(cards.Count == 0);

        RefreshDetails(session);
    }

    private void RefreshDetails(GameSession session)
    {
        CafeService cafe = session.Cafe;
        RecipeData recipe = session.Design.GetRecipe(_selected);
        if (recipe == null || !cafe.IsUnlocked(recipe.id))
        {
            _details.text = "Tap a recipe to see what it needs.";
            _menuButton.gameObject.SetActive(false);
            return;
        }

        var text = new StringBuilder($"<b>{recipe.name}</b>  <color={Hex(GameUi.InkSoft)}>{recipe.coins} coins each</color>\n");
        foreach (ItemAmount ingredient in recipe.ingredients)
        {
            ItemData item = session.Design.GetItem(ingredient.itemId);
            int have = session.Economy.Count(ingredient.itemId);
            string colour = Hex(have >= ingredient.qty ? GameUi.Moss : GameUi.Berry);
            text.Append($"<color={colour}>{(item != null ? item.name : ingredient.itemId)} {have}/{ingredient.qty}</color>    ");
        }
        _details.text = text.ToString();

        bool onMenu = cafe.IsOnMenu(recipe.id);
        _menuButton.gameObject.SetActive(true);
        _menuButton.interactable = onMenu || !cafe.MenuIsFull;
        GameUi.SetLabel(_menuButton, onMenu ? "Take off menu" : cafe.MenuIsFull ? "Menu is full" : "Add to menu");
    }

    private void ToggleSelected()
    {
        CafeService cafe = Session?.Cafe;
        if (cafe == null || string.IsNullOrEmpty(_selected)) return;
        if (cafe.IsOnMenu(_selected)) cafe.RemoveFromMenu(_selected);
        else cafe.AddToMenu(_selected);
        Session.SaveNow();
    }

    // ----- Closing for the day -----

    /// <summary>"morning", "midday", "afternoon" or "evening" for a 3-trip day; null with no day clock.</summary>
    private static string PartOfDay(DayRules rules, GameState state)
    {
        if (state.isNight) return "night";
        if (rules.forageTripsPerDay <= 0) return null;
        if (rules.IsEvening(state)) return "evening";
        if (rules.forageTripsPerDay == 3) return new[] { "morning", "midday", "afternoon" }[rules.PartsPassed(state)];
        return $"part {rules.PartsPassed(state) + 1} of {rules.forageTripsPerDay}";
    }

    private void OnCloseDayPressed()
    {
        GameSession session = Session;
        if (session == null) return;
        if (session.State.isNight)
        {
            base.Close();
            DayFlow.GoToBed();
            return;
        }
        DayRules rules = session.Design.rules;
        bool evening = rules.IsEvening(session.State);
        bool emptyMenu = session.State.menu.Count == 0 && HasAnyRecipe(session);
        if (evening && !emptyMenu)
        {
            CloseForTheDay();
            return;
        }

        // Ask first: closing early, or with nothing on the menu for tomorrow.
        int tripsLeft = ForagingService.TripsLeftToday(session.Design, session.State);
        string menuNote = emptyMenu ? "There's nothing on the menu for tomorrow. Pick a dish first, or the morning's guests will go hungry." : "";
        _confirmText.text = evening
            ? $"<b>Close for the day?</b>\n\n{menuNote}"
            : $"<b>Close early?</b>\n\nIt's only {PartOfDay(rules, session.State)}. " +
              $"The cafe serves while you forage, so closing now means no more guests today, " +
              $"and you'd lose {tripsLeft} forage trip{(tripsLeft == 1 ? "" : "s")}." + (emptyMenu ? $"\n{menuNote}" : "");
        GameUi.SetLabel(_confirmStay, emptyMenu ? "Set the menu" : "Stay open");
        _confirm.gameObject.SetActive(true);
    }

    private static bool HasAnyRecipe(GameSession session) => session.Design.recipes.Exists(r => session.Cafe.IsUnlocked(r.id));

    private void CloseForTheDay()
    {
        _confirm.gameObject.SetActive(false);
        GameSession session = Session;
        if (session == null) return;
        DayTransition.Holding = true; // keep showing today until the fade is black
        DaySummary summary = session.Cafe.CloseDay();
        session.SaveNow();
        Debug.Log($"[Cafe] Day {summary.day} closed: sold {summary.sold}/{summary.salesPlanned} for {summary.coins} coins.");

        _board.gameObject.SetActive(false);
        _summary.gameObject.SetActive(true);

        _summaryTitle.text = $"Day {summary.day} is done";
        var text = new StringBuilder();
        foreach (DishSale dish in summary.dishes)
        {
            if (dish.sold == 0) continue;
            RecipeData recipe = session.Design.GetRecipe(dish.recipeId);
            text.Append($"{recipe?.name ?? dish.recipeId} x{dish.sold}   <color={Hex(GameUi.Moss)}>+{dish.coins} coins</color>" +
                        (dish.critic ? $"  <color={Hex(GameUi.InkSoft)}>(the food critic loved it: double!)</color>" : "") + "\n");
        }
        if (summary.closedEarly)
            text.Append("You closed early, so fewer guests came today.\n");
        if (summary.sold == 0 && summary.salesPlanned > 0)
            text.Append(session.State.menu.Count == 0
                ? "The menu was empty, so nothing sold today.\n"
                : "There weren't enough ingredients to make anything today.\n");
        if (summary.tip > 0)
            text.Append($"<color={Hex(GameUi.Moss)}>A guest loved how cosy it was and left a {summary.tip} coin tip!</color>\n");
        text.Append($"\n<b>{summary.coins} coins</b> today.");
        if (summary.sold > 0 && summary.Missed > 0)
            text.Append($"\n<color={Hex(GameUi.Berry)}>{summary.Missed} hungry guests went without: forage for more ingredients.</color>");
        _summaryText.text = text.ToString();
        if (summary.nightFalls) text.Append("\n\nThe sun's going down. The bush comes alive at night.");
        GameUi.SetLabel(_nextDay, summary.nightFalls ? "Into the night" : $"Start day {session.State.day}");
    }

    private void StartNextDay() => Close();

    /// <summary>Leaving the day's summary (the button or Escape) fades through black into the next morning.</summary>
    public override void Close()
    {
        bool fromSummary = _summary && _summary.gameObject.activeSelf;
        base.Close();
        if (!fromSummary) return;
        if (Session == null) DayTransition.Holding = false;
        else DayFlow.FadeInto(Session.State.isNight ? "Night" : $"Day {Session.State.day}");
    }

    // ----- Building the screen -----

    private static string Hex(Color colour) => "#" + ColorUtility.ToHtmlStringRGB(colour);

    private void Build(RectTransform root)
    {
        GameUi.AddPanel(root, new Color(0.08f, 0.07f, 0.05f, 0.55f), false);

        RectTransform panel = GameUi.NewRect("Panel", root);
        panel.anchorMin = new Vector2(0.03f, 0.04f);
        panel.anchorMax = new Vector2(0.97f, 0.96f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        GameUi.AddPanel(panel, GameUi.Panel);

        _board = GameUi.NewRect("Board", panel);
        GameUi.Stretch(_board);
        _summary = GameUi.NewRect("Summary", panel);
        GameUi.Stretch(_summary);

        // Board: title, today's menu, known recipes, details with the menu button, Close for the day.
        _title = Title(_board);
        Button close = GameUi.AddButton(_board, "Close", Close, new Vector2(190f, 84f), GameUi.Bark, GameUi.Cream);
        GameUi.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(190f, 84f));
        Button upgrades = GameUi.AddButton(_board, "Upgrades", UpgradesPopup.Show, new Vector2(230f, 84f), GameUi.Cream, GameUi.Ink);
        GameUi.Place((RectTransform)upgrades.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-232f, -24f), new Vector2(230f, 84f));

        RectTransform menuBox = GameUi.NewRect("Menu", _board);
        menuBox.anchorMin = new Vector2(0f, 1f);
        menuBox.anchorMax = new Vector2(1f, 1f);
        menuBox.pivot = new Vector2(0.5f, 1f);
        menuBox.offsetMin = new Vector2(24f, -290f);
        menuBox.offsetMax = new Vector2(-24f, -124f);
        GameUi.AddPanel(menuBox, GameUi.MossLight);
        _menuText = GameUi.AddText(menuBox, "", 30f, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "MenuText");
        _menuText.rectTransform.offsetMin = new Vector2(24f, 8f);
        _menuText.rectTransform.offsetMax = new Vector2(-24f, -8f);

        RectTransform list = GameUi.NewRect("Recipes", _board);
        list.anchorMin = Vector2.zero;
        list.anchorMax = Vector2.one;
        list.offsetMin = new Vector2(24f, 170f);
        list.offsetMax = new Vector2(-24f, -306f);
        _recipes = ItemGridView.Create(list, false, new Vector2(190f, 210f));
        _empty = GameUi.AddText(list, "You don't know any recipes yet. Someone might teach you one...", 30f, GameUi.InkSoft, TextAlignmentOptions.Center, "Empty");

        RectTransform details = GameUi.NewRect("Details", _board);
        details.anchorMin = new Vector2(0f, 0f);
        details.anchorMax = new Vector2(1f, 0f);
        details.pivot = new Vector2(0.5f, 0f);
        details.offsetMin = new Vector2(24f, 24f);
        details.offsetMax = new Vector2(-404f, 150f);
        GameUi.AddPanel(details, GameUi.Cream);
        _details = GameUi.AddText(details, "", 28f, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "DetailsText");
        _details.rectTransform.offsetMin = new Vector2(24f, 8f);
        _details.rectTransform.offsetMax = new Vector2(-300f, -8f);
        _menuButton = GameUi.AddButton(details, "Add to menu", ToggleSelected, new Vector2(270f, 96f), GameUi.Moss, GameUi.Cream, 30f);
        GameUi.Place((RectTransform)_menuButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(270f, 96f));

        _closeDay = GameUi.AddButton(_board, "Close for the day", OnCloseDayPressed, new Vector2(360f, 126f), GameUi.Bark, GameUi.Cream, 32f);
        GameUi.Place((RectTransform)_closeDay.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(360f, 126f));

        // Close early? A box over the board.
        _confirm = GameUi.NewRect("ConfirmEarly", _board);
        GameUi.Stretch(_confirm);
        GameUi.AddPanel(_confirm, new Color(0.08f, 0.07f, 0.05f, 0.5f), false);
        RectTransform box = GameUi.NewRect("Box", _confirm);
        GameUi.Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 460f));
        GameUi.AddPanel(box, GameUi.Cream);
        _confirmText = GameUi.AddText(box, "", 34f, GameUi.Ink, TextAlignmentOptions.Top, "Text");
        _confirmText.rectTransform.offsetMin = new Vector2(40f, 150f);
        _confirmText.rectTransform.offsetMax = new Vector2(-40f, -36f);
        Button stay = GameUi.AddButton(box, "Stay open", () => _confirm.gameObject.SetActive(false), new Vector2(300f, 100f), GameUi.Moss, GameUi.Cream);
        _confirmStay = stay;
        GameUi.Place((RectTransform)stay.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 32f), new Vector2(300f, 100f));
        Button closeAnyway = GameUi.AddButton(box, "Close anyway", CloseForTheDay, new Vector2(300f, 100f), GameUi.Bark, GameUi.Cream);
        GameUi.Place((RectTransform)closeAnyway.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(16f, 32f), new Vector2(300f, 100f));
        _confirm.gameObject.SetActive(false);

        // Summary: what sold, then the next morning.
        _summaryTitle = Title(_summary);
        _summaryText = GameUi.AddText(_summary, "", 36f, GameUi.Ink, TextAlignmentOptions.TopLeft, "SummaryText");
        _summaryText.rectTransform.offsetMin = new Vector2(48f, 180f);
        _summaryText.rectTransform.offsetMax = new Vector2(-48f, -140f);
        _nextDay = GameUi.AddButton(_summary, "Next day", StartNextDay, new Vector2(360f, 110f), GameUi.Moss, GameUi.Cream, 36f);
        GameUi.Place((RectTransform)_nextDay.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 32f), new Vector2(360f, 110f));
    }

    private static TextMeshProUGUI Title(RectTransform parent)
    {
        TextMeshProUGUI title = GameUi.AddText(parent, "", 54f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Title");
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.offsetMin = new Vector2(40f, 0f);
        title.rectTransform.offsetMax = new Vector2(-500f, -28f);
        return title;
    }
}
