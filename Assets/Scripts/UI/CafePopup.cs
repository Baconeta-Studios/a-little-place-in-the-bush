using System.Collections.Generic;
using System.Text;
using Bush.Core;
using TMPro;
using UI.Popups;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The cafe's menu board, behind the cafe door (<see cref="CafeSpot"/>). Recipes you know are on the left as
/// cards, and today's menu is on the right, as pages hanging under the sign. Drag a card onto a page, or tap it
/// to fill the next free one; drag a page off, or press its x, to take it down. Changes count straight away.
///
/// The day's forage trips are its clock (see <see cref="DayRules"/>). "Head out foraging" closes the board and
/// the gecko walks to the path, so every trip starts from the cafe. Once the trips are taken it's evening, and
/// "Close for the day" sells a full day's dishes. Closing before then asks first, because it sells only the
/// share of the day so far and loses the trips left. After the day's summary, starting the next day closes the
/// screen and fades through black.
///
/// How it looks is in Resources/UI/CafePopup.prefab (see docs/UI.md), made from <see cref="BuildDefault"/>,
/// which is also used if there's no prefab.
/// </summary>
public class CafePopup : Popup
{
    public const string ResourcePath = "UI/CafePopup";

    private static CafePopup _instance;

    [Header("The parts")]
    [SerializeField] private RectTransform _board;
    [SerializeField] private RectTransform _summary;
    [SerializeField] private RectTransform _confirm;

    [Header("Recipes")]
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _status;
    [SerializeField] private TextMeshProUGUI _hint;
    [SerializeField] private GameObject _eventTape;
    [SerializeField] private TextMeshProUGUI _eventText;
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private RectTransform _cards;
    [SerializeField] private RecipeCardView _cardTemplate;
    [SerializeField] private TextMeshProUGUI _empty;

    [Header("Today's menu")]
    [SerializeField] private RectTransform _slotRow;
    [SerializeField] private MenuSlotView[] _slots;
    [SerializeField] private RectTransform _dragGhost;
    [SerializeField] private Image _dragGhostPage;
    [SerializeField] private TextMeshProUGUI _dragGhostName;
    [SerializeField] private RectTransform _sales;
    [SerializeField] private RectTransform _saleTemplate;
    [SerializeField] private TextMeshProUGUI _forecast;
    [SerializeField] private RectTransform _pips;
    [SerializeField] private Image _pipTemplate;
    [SerializeField] private float _usedPipAlpha = 0.3f;

    [Header("Buttons")]
    [SerializeField] private Button _main;
    [SerializeField] private Button _upgrades;
    [SerializeField] private Button _closeEarly;
    [SerializeField] private Button _back;

    [Header("Closing")]
    [SerializeField] private TextMeshProUGUI _confirmText;
    [SerializeField] private Button _confirmStay;
    [SerializeField] private Button _confirmGo;
    [SerializeField] private TextMeshProUGUI _summaryTitle;
    [SerializeField] private TextMeshProUGUI _summaryText;
    [SerializeField] private Button _nextDay;

    private const string DefaultHint = "Drag a recipe onto the board, or tap it.";

    private string _dragId;
    private int _dragFrom;
    private bool _scrolling;
    private bool _warnedFull;

    private static GameSession Session => GameSession.Current;

    /// <summary>Opens the menu board, creating it the first time.</summary>
    public static void Show()
    {
        if (!_instance) _instance = Create();
        _instance.Open();
    }

    private static CafePopup Create()
    {
        Transform safeRoot = GameUi.Current.SafeRoot;
        var prefab = Resources.Load<CafePopup>(ResourcePath);
        if (!prefab) return BuildDefault(safeRoot);

        // A popup is "open" while active, and the prefab is saved on so it can be seen and edited:
        // copy it under something switched off, so it arrives closed without ever opening.
        var closed = new GameObject("Closed");
        closed.SetActive(false);
        CafePopup popup = Instantiate(prefab, closed.transform);
        popup.gameObject.SetActive(false);
        popup.transform.SetParent(safeRoot, false);
        Destroy(closed);
        return popup;
    }

    /// <summary>The buttons are linked here rather than in the prefab, so restyling can't unhook them.</summary>
    protected override void Awake()
    {
        base.Awake();
        _main.onClick.AddListener(OnMainPressed);
        _upgrades.onClick.AddListener(UpgradesPopup.Show);
        _closeEarly.onClick.AddListener(OnCloseDayPressed);
        _back.onClick.AddListener(Close);
        _confirmStay.onClick.AddListener(() => _confirm.gameObject.SetActive(false));
        _confirmGo.onClick.AddListener(CloseForTheDay);
        _nextDay.onClick.AddListener(Close);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (Session != null)
        {
            Session.Events.InventoryChanged += Refresh;
            Session.Events.MenuChanged += Refresh;
            Session.Events.RecipeLearned += OnChanged;
            Session.Events.UpgradeBought += OnChanged;
        }
        ShowBoard();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        CancelDrag();
        if (Session != null)
        {
            Session.Events.InventoryChanged -= Refresh;
            Session.Events.MenuChanged -= Refresh;
            Session.Events.RecipeLearned -= OnChanged;
            Session.Events.UpgradeBought -= OnChanged;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void OnChanged(string id) => Refresh();

    // ----- The menu board -----

    private void ShowBoard()
    {
        _board.gameObject.SetActive(true);
        _summary.gameObject.SetActive(false);
        _confirm.gameObject.SetActive(false);
        _warnedFull = false;
        Refresh();
    }

    private void Refresh()
    {
        GameSession session = Session;
        if (session == null || !_board.gameObject.activeSelf) return;
        DesignData design = session.Design;
        DayRules rules = design.rules;
        GameState state = session.State;

        string part = PartOfDay(rules, state);
        _title.text = $"The cafe, day {state.day}";
        _status.text = (part != null ? char.ToUpperInvariant(part[0]) + part.Substring(1) + ", " : "") + $"{session.Economy.Currency} coins";
        _hint.text = _warnedFull ? "The board's full. Drag this dish onto a page to swap it." : DefaultHint;
        _hint.color = _warnedFull ? new Color(0.49f, 0.12f, 0.09f) : GameUi.InkSoft;

        DayEventData today = DayEvents.Today(design, state);
        bool showEvent = today != null && (today.extraSales > 0 || (today.critic && !state.criticDone));
        _eventTape.SetActive(showEvent);
        if (showEvent) _eventText.text = $"{today.name}: {today.effect}";

        RefreshCards(session);
        RefreshSlots(session);
        RefreshForecast(session);
        RefreshPips(session);
        RefreshButtons(session);
    }

    private void RefreshCards(GameSession session)
    {
        ClearExcept(_cards, _cardTemplate.transform);
        bool any = false;
        foreach (RecipeData recipe in session.Design.recipes)
        {
            if (!session.Cafe.IsUnlocked(recipe.id)) continue;
            any = true;
            RecipeCardView card = Instantiate(_cardTemplate, _cards);
            card.gameObject.SetActive(true);
            card.Fill(this, session, recipe);
        }
        _empty.gameObject.SetActive(!any);
    }

    private void RefreshSlots(GameSession session)
    {
        int owned = session.Design.rules.MenuSlots(session.State);
        IReadOnlyList<string> menu = session.Cafe.Menu;
        var toBuy = new List<UpgradeData>();
        foreach (UpgradeData upgrade in session.Design.rules.upgrades)
            if (upgrade.effect == UpgradeEffect.MenuSlot && !session.Cafe.HasUpgrade(upgrade.id)) toBuy.Add(upgrade);

        for (int i = 0; i < _slots.Length; i++)
        {
            MenuSlotView slot = _slots[i];
            bool shown = i < owned || i - owned < toBuy.Count;
            slot.gameObject.SetActive(shown);
            if (!shown) continue;
            if (i >= owned)
            {
                UpgradeData upgrade = toBuy[i - owned];
                slot.ShowLocked(this, i, $"{upgrade.name}\n{upgrade.price} coins");
            }
            else if (i < menu.Count)
            {
                RecipeData recipe = session.Design.GetRecipe(menu[i]);
                slot.ShowDish(this, i, menu[i], recipe?.name ?? menu[i], session.Database.recipeIcon);
            }
            else slot.ShowFree(this, i);
        }
    }

    /// <summary>What the cafe will do while you're out: the next trip's dishes, then the rest of the day.</summary>
    private void RefreshForecast(GameSession session)
    {
        ClearExcept(_sales, _saleTemplate);
        DayRules rules = session.Design.rules;
        GameState state = session.State;
        CafeService cafe = session.Cafe;
        string soft = Hex(GameUi.InkSoft), bad = Hex(new Color(0.49f, 0.12f, 0.09f));

        var text = new StringBuilder();
        if (state.isNight) text.Append($"<color={soft}>The cafe's closed for the night. The menu here is for tomorrow.</color>");
        else if (rules.IsEvening(state) && rules.TripsPerDay(state) > 0)
        {
            int sold = 0, coins = 0;
            foreach (DishSale dish in state.soldToday)
            {
                sold += dish.sold;
                coins += dish.coins;
            }
            text.Append($"<color={soft}>Service is done: {sold} sold, {coins} coins. Changes now are for tomorrow.</color>");
        }
        else if (cafe.Menu.Count == 0) text.Append($"<color={bad}>Nothing on the menu yet.</color>");
        else
        {
            foreach (DishSale dish in cafe.PlanSales(out _))
            {
                if (dish.sold <= 0) continue;
                RectTransform sale = Instantiate(_saleTemplate, _sales);
                sale.gameObject.SetActive(true);
                RecipeData recipe = session.Design.GetRecipe(dish.recipeId);
                ItemDefinition first = recipe.ingredients.Count > 0 ? session.Database.Find<ItemDefinition>(recipe.ingredients[0].itemId) : null;
                var icon = sale.GetChild(0).GetComponent<Image>();
                icon.sprite = ItemGridView.IconFor(first, null, out Color tint);
                icon.color = tint;
                sale.GetComponentInChildren<TextMeshProUGUI>().text = dish.sold > 1 ? $"{dish.sold} {recipe.name}" : recipe.name;
            }
            DayPlan plan = cafe.PlanRestOfDay();
            text.Append($"<color={soft}>About {plan.coins} coins by evening.</color>");
            if (plan.missed > 0) text.Append($"\n<color={bad}>{plan.missed} guest{(plan.missed == 1 ? "" : "s")} would go hungry today.</color>");
        }

        QuestData closeQuest = state.isNight ? null : cafe.CloseUnlockedBy();
        if (closeQuest != null) text.Append($"\n<color={soft}>{QuestLogPopup.NpcName(session.Design, closeQuest.giver)} wants a word before you close up.</color>");
        _forecast.text = text.ToString();
    }

    private void RefreshPips(GameSession session)
    {
        ClearExcept(_pips, _pipTemplate.transform);
        GameState state = session.State;
        int total = state.isNight ? session.Design.rules.nightTrips : session.Design.rules.TripsPerDay(state);
        int left = ForagingService.TripsLeftToday(session.Design, state);
        _pips.gameObject.SetActive(total > 0 && left >= 0);
        for (int i = 0; i < total; i++)
        {
            Image pip = Instantiate(_pipTemplate, _pips);
            pip.gameObject.SetActive(true);
            Color color = pip.color;
            color.a = i < left ? 1f : _usedPipAlpha;
            pip.color = color;
        }
    }

    private void RefreshButtons(GameSession session)
    {
        GameState state = session.State;
        int tripsLeft = ForagingService.TripsLeftToday(session.Design, state);
        bool foraging = tripsLeft != 0;
        bool canClose = state.isNight || session.Cafe.CloseUnlocked(); // hidden until the story teaches it, so day 1 can't be skipped
        GameUi.SetLabel(_main, foraging ? "Head out foraging" : state.isNight ? "Go to bed" : "Close for the day");
        _main.gameObject.SetActive(foraging || canClose);
        GameUi.SetLabel(_closeEarly, state.isNight ? "Go to bed" : "Close early");
        _closeEarly.gameObject.SetActive(foraging && canClose);
    }

    private void OnMainPressed()
    {
        GameSession session = Session;
        if (session == null) return;
        if (ForagingService.TripsLeftToday(session.Design, session.State) == 0)
        {
            OnCloseDayPressed();
            return;
        }

        // Out through the cafe, not straight to another screen: the gecko walks to the path, and that opens the forage screen.
        Close();
        var spot = FindAnyObjectByType<ForageSpot>();
        if (spot && Interactor.Player) Interactor.Player.Request(spot);
        else ForagePopup.Show();
    }

    // ----- Changing the menu -----

    public void ToggleOnMenu(string recipeId)
    {
        CafeService cafe = Session?.Cafe;
        if (cafe == null) return;
        _warnedFull = false;
        if (cafe.IsOnMenu(recipeId)) cafe.RemoveFromMenu(recipeId);
        else if (!cafe.AddToMenu(recipeId))
        {
            _warnedFull = true;
            Refresh();
            return;
        }
        Session.SaveNow();
    }

    public void TakeOffMenu(string recipeId)
    {
        if (Session == null || !Session.Cafe.RemoveFromMenu(recipeId)) return;
        _warnedFull = false;
        Session.SaveNow();
    }

    public void OpenUpgrades() => UpgradesPopup.Show();

    private Camera UiCamera
    {
        get
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }
    }

    public void BeginDrag(string recipeId, int fromSlot, PointerEventData eventData)
    {
        // Mostly up or down on a card, in a list long enough to scroll: that's scrolling, not picking it up.
        Vector2 moved = eventData.delta;
        if (fromSlot < 0 && Mathf.Abs(moved.y) > Mathf.Abs(moved.x) && _scroll.content.rect.height > _scroll.viewport.rect.height)
        {
            _scrolling = true;
            _scroll.OnBeginDrag(eventData);
            return;
        }

        _dragId = recipeId;
        _dragFrom = fromSlot;
        _dragGhostName.text = Session?.Design.GetRecipe(recipeId)?.name ?? recipeId;
        Sprite page = Session?.Database.recipeIcon;
        _dragGhostPage.gameObject.SetActive(page);
        _dragGhostPage.sprite = page;
        _dragGhost.gameObject.SetActive(true);
        _dragGhost.SetAsLastSibling();
        MoveGhost(eventData.position);
    }

    public void Drag(PointerEventData eventData)
    {
        if (_scrolling)
        {
            _scroll.OnDrag(eventData);
            return;
        }
        if (_dragId == null) return;
        MoveGhost(eventData.position);
        MenuSlotView over = SlotAt(eventData.position);
        foreach (MenuSlotView slot in _slots) slot.SetHot(slot == over);
    }

    public void EndDrag(PointerEventData eventData)
    {
        if (_scrolling)
        {
            _scrolling = false;
            _scroll.OnEndDrag(eventData);
            return;
        }
        if (_dragId == null) return;

        string recipeId = _dragId;
        int from = _dragFrom;
        MenuSlotView over = SlotAt(eventData.position);
        bool overBoard = RectTransformUtility.RectangleContainsScreenPoint(_slotRow, eventData.position, UiCamera);
        int target = over ? System.Array.IndexOf(_slots, over) : -1;
        CancelDrag();

        CafeService cafe = Session?.Cafe;
        if (cafe == null) return;
        _warnedFull = false;
        if (target >= 0) cafe.PlaceOnMenu(recipeId, target);
        else if (from >= 0 && !overBoard) cafe.RemoveFromMenu(recipeId);
        Session.SaveNow();
        Refresh();
    }

    private void CancelDrag()
    {
        _dragId = null;
        _scrolling = false;
        if (_dragGhost) _dragGhost.gameObject.SetActive(false);
        if (_slots == null) return;
        foreach (MenuSlotView slot in _slots)
            if (slot) slot.SetHot(false);
    }

    private void MoveGhost(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_dragGhost.parent, screen, UiCamera, out Vector2 local);
        _dragGhost.localPosition = local;
    }

    /// <summary>The page under a screen point, not counting ones you haven't bought.</summary>
    private MenuSlotView SlotAt(Vector2 screen)
    {
        foreach (MenuSlotView slot in _slots)
            if (slot.gameObject.activeSelf && !slot.IsLocked && RectTransformUtility.RectangleContainsScreenPoint(slot.Rect, screen, UiCamera))
                return slot;
        return null;
    }

    private static void ClearExcept(Transform parent, Transform keep)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child == keep) continue;
            // Off the layout now, rather than at the end of the frame.
            child.gameObject.SetActive(false);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    // ----- Closing for the day -----

    /// <summary>"morning", "midday", "afternoon" or "evening"; null with no day clock.</summary>
    private static string PartOfDay(DayRules rules, GameState state)
    {
        if (state.isNight) return "night";
        if (rules.forageTripsPerDay <= 0) return null;
        if (rules.IsEvening(state)) return "evening";
        int trips = rules.TripsPerDay(state);
        if (trips == 2) return new[] { "morning", "afternoon" }[rules.PartsPassed(state)];
        if (trips == 3) return new[] { "morning", "midday", "afternoon" }[rules.PartsPassed(state)];
        return $"part {rules.PartsPassed(state) + 1} of {rules.TripsPerDay(state)}";
    }

    private void OnCloseDayPressed()
    {
        GameSession session = Session;
        if (session == null) return;
        if (!session.State.isNight && !session.Cafe.CloseUnlocked()) return; // the button's hidden; just in case
        if (session.State.isNight)
        {
            base.Close();
            DayFlow.GoToBed();
            return;
        }
        DayRules rules = session.Design.rules;
        bool evening = rules.IsEvening(session.State);
        bool emptyMenu = session.State.menu.Count == 0 && session.Cafe.CanCookAnything();
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
            text.Append($"\n<color={Hex(GameUi.Berry)}>{summary.Missed} hungry guest{(summary.Missed == 1 ? "" : "s")} went without: forage for more ingredients.</color>");
        if (summary.nightFalls) text.Append("\n\nThe sun's going down. The bush comes alive at night.");
        _summaryText.text = text.ToString();
        GameUi.SetLabel(_nextDay, summary.nightFalls ? "Into the night" : $"Start day {session.State.day}");
    }

    /// <summary>Leaving the day's summary (the button or Escape) fades through black into the next morning.</summary>
    public override void Close()
    {
        bool fromSummary = _summary && _summary.gameObject.activeSelf;
        base.Close();
        if (!fromSummary) return;
        if (Session == null) DayTransition.Holding = false;
        else DayFlow.FadeInto(Session.State.isNight ? "Night" : $"Day {Session.State.day}");
    }

    private static string Hex(Color colour) => "#" + ColorUtility.ToHtmlStringRGB(colour);

    // ----- The default layout -----

    /// <summary>Art a prefab can keep: the backdrop, the torn paper for the panels, the cafe sign and the trip markers.</summary>
    public void UseArt(Sprite backdrop, Sprite paper, Sprite sign, Sprite pip)
    {
        Image background = transform.Find("Backdrop").GetComponent<Image>();
        background.sprite = backdrop;
        background.color = Color.white;
        foreach (Image image in GetComponentsInChildren<Image>(true))
        {
            if (image.name is "Recipes" or "Forecast" or "SummaryPaper" or "ConfirmPaper")
            {
                image.sprite = paper;
                image.type = Image.Type.Tiled;
            }
            else if (image.name == "Sign")
            {
                image.sprite = sign;
                image.color = Color.white;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }
        }
        _pipTemplate.sprite = pip;
        _pipTemplate.color = Color.white;
        _pipTemplate.preserveAspect = true;
    }

    public static CafePopup BuildDefault(Transform parent)
    {
        RectTransform root = GameUi.NewRect("CafePopup", parent);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var popup = root.gameObject.AddComponent<CafePopup>();
        RectTransform backdrop = GameUi.NewRect("Backdrop", root);
        GameUi.Stretch(backdrop);
        GameUi.AddPanel(backdrop, new Color(0.3f, 0.27f, 0.18f), false);
        RectTransform dim = GameUi.NewRect("Dim", root);
        GameUi.Stretch(dim);
        GameUi.AddPanel(dim, new Color(0.08f, 0.07f, 0.05f, 0.42f), false);

        popup._board = GameUi.NewRect("Board", root);
        GameUi.Stretch(popup._board);
        popup.BuildRecipes();
        popup.BuildMenuColumn();
        popup.BuildGhost();
        popup.BuildConfirm();
        popup.BuildSummary(root);
        return popup;
    }

    private void BuildRecipes()
    {
        RectTransform paper = GameUi.NewRect("Recipes", _board);
        // The torn paper has a clear edge, about 30 at the sides and 55 top and bottom, so it sits out and what's on it sits in.
        Anchor(paper, Vector2.zero, new Vector2(0.625f, 1f), new Vector2(-4f, -8f), new Vector2(8f, 8f));
        GameUi.AddPanel(paper, GameUi.Panel2);

        _title = GameUi.AddText(paper, "", 54f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Title");
        Anchor(_title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(64f, -130f), new Vector2(-64f, -60f));
        _status = GameUi.AddText(paper, "", 30f, GameUi.Ink, TextAlignmentOptions.TopRight, "Status");
        Anchor(_status.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(64f, -130f), new Vector2(-64f, -70f));
        _hint = GameUi.AddText(paper, DefaultHint, 27f, GameUi.InkSoft, TextAlignmentOptions.TopLeft, "Hint");
        Anchor(_hint.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(64f, -170f), new Vector2(-64f, -130f));

        RectTransform tape = GameUi.NewRect("EventTape", paper);
        GameUi.Place(tape, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-64f, -122f), new Vector2(470f, 40f));
        tape.localRotation = Quaternion.Euler(0f, 0f, 1f);
        GameUi.AddPanel(tape, new Color(0.89f, 0.75f, 0.29f)).raycastTarget = false;
        _eventTape = tape.gameObject;
        _eventText = GameUi.AddText(tape, "", 23f, GameUi.Ink, TextAlignmentOptions.Center, "Nudge");

        RectTransform list = GameUi.NewRect("List", paper);
        Anchor(list, Vector2.zero, Vector2.one, new Vector2(52f, 56f), new Vector2(-52f, -170f));
        _scroll = list.gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 30f;
        RectTransform viewport = GameUi.NewRect("Viewport", list);
        GameUi.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        // A clear image so the empty space between cards still catches the wheel and drags.
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        _scroll.viewport = viewport;

        _cards = GameUi.NewRect("Cards", viewport);
        _cards.anchorMin = new Vector2(0f, 1f);
        _cards.anchorMax = Vector2.one;
        _cards.pivot = new Vector2(0.5f, 1f);
        _cards.offsetMin = _cards.offsetMax = Vector2.zero;
        var grid = _cards.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(258f, 250f);
        grid.spacing = new Vector2(14f, 22f);
        grid.padding = new RectOffset(4, 4, 22, 8);
        grid.childAlignment = TextAnchor.UpperCenter;
        _cards.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _scroll.content = _cards;

        _cardTemplate = RecipeCardView.Build(_cards);
        _empty = GameUi.AddText(viewport, "You don't know any recipes yet. Someone might teach you one...", 30f, GameUi.InkSoft, TextAlignmentOptions.Center, "Empty");
    }

    private void BuildMenuColumn()
    {
        RectTransform column = GameUi.NewRect("MenuColumn", _board);
        Anchor(column, new Vector2(0.625f, 0f), Vector2.one, new Vector2(16f, 16f), new Vector2(-26f, -6f));
        VerticalLayoutGroup layout = GameUi.AddLayout<VerticalLayoutGroup>(column, 10f);
        layout.childForceExpandWidth = true;

        // The sign's picture is square with clear space around it, so it hangs from the top of a shorter space.
        RectTransform signSpace = GameUi.NewRect("SignSpace", column);
        signSpace.gameObject.AddComponent<LayoutElement>().preferredHeight = 262f;
        RectTransform sign = GameUi.NewRect("Sign", signSpace);
        GameUi.Place(sign, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 34f), new Vector2(456f, 456f));
        GameUi.AddPanel(sign, GameUi.Bark).raycastTarget = false;
        TextMeshProUGUI signTitle = GameUi.AddText(signSpace, "Today's menu", 50f, GameUi.Ink, TextAlignmentOptions.Center, "SignTitle");
        Anchor(signTitle.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -112f));

        _slotRow = GameUi.NewRect("Slots", column);
        var slotRowSize = _slotRow.gameObject.AddComponent<LayoutElement>();
        slotRowSize.preferredHeight = 240f;
        slotRowSize.flexibleHeight = 0f; // any spare height goes to the forecast
        HorizontalLayoutGroup row = GameUi.AddLayout<HorizontalLayoutGroup>(_slotRow, 16f);
        row.childForceExpandHeight = true;
        _slots = new[] { MenuSlotView.Build(_slotRow, 0), MenuSlotView.Build(_slotRow, 1), MenuSlotView.Build(_slotRow, 2) };

        RectTransform forecast = GameUi.NewRect("Forecast", column);
        GameUi.AddPanel(forecast, GameUi.Panel2);
        var forecastSize = forecast.gameObject.AddComponent<LayoutElement>();
        forecastSize.preferredHeight = 200f;
        forecastSize.flexibleHeight = 1f;
        VerticalLayoutGroup inside = GameUi.AddLayout<VerticalLayoutGroup>(forecast, 4f, new RectOffset(60, 60, 66, 58));
        inside.childAlignment = TextAnchor.UpperLeft;
        inside.childForceExpandWidth = true;
        TextMeshProUGUI heading = GameUi.AddText(forecast, "While you're out", 29f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Heading");

        _sales = GameUi.NewRect("Sales", forecast);
        GameUi.AddLayout<HorizontalLayoutGroup>(_sales, 8f).childAlignment = TextAnchor.MiddleLeft;
        _saleTemplate = GameUi.NewRect("SaleTemplate", _sales);
        GameUi.AddPanel(_saleTemplate, GameUi.CardBack).raycastTarget = false;
        GameUi.AddLayout<HorizontalLayoutGroup>(_saleTemplate, 6f, new RectOffset(10, 12, 4, 4));
        RectTransform saleIcon = GameUi.NewRect("Icon", _saleTemplate);
        var saleIconImage = saleIcon.gameObject.AddComponent<Image>();
        saleIconImage.preserveAspect = true;
        saleIconImage.raycastTarget = false;
        var saleIconSize = saleIcon.gameObject.AddComponent<LayoutElement>();
        saleIconSize.preferredWidth = saleIconSize.preferredHeight = 34f;
        GameUi.AddText(_saleTemplate, "", 24f, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "DishName").textWrappingMode = TextWrappingModes.NoWrap;
        _saleTemplate.gameObject.SetActive(false);

        _forecast = GameUi.AddText(forecast, "", 25f, GameUi.Ink, TextAlignmentOptions.TopLeft, "ForecastText");
        _forecast.enableAutoSizing = true;
        _forecast.fontSizeMin = 19f;
        _forecast.fontSizeMax = 25f;
        _forecast.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        _pips = GameUi.NewRect("Pips", column);
        _pips.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
        GameUi.AddLayout<HorizontalLayoutGroup>(_pips, 8f);
        RectTransform pip = GameUi.NewRect("PipTemplate", _pips);
        _pipTemplate = pip.gameObject.AddComponent<Image>();
        _pipTemplate.sprite = GeneratedSprites.Circle;
        _pipTemplate.color = new Color(0.89f, 0.75f, 0.29f);
        _pipTemplate.raycastTarget = false;
        var pipSize = pip.gameObject.AddComponent<LayoutElement>();
        pipSize.preferredWidth = pipSize.preferredHeight = 40f;
        pip.gameObject.SetActive(false);

        _main = GameUi.AddButton(column, "Head out foraging", null, new Vector2(660f, 96f), GameUi.Apron, GameUi.Cream, 40f);

        RectTransform buttons = GameUi.NewRect("Buttons", column);
        buttons.gameObject.AddComponent<LayoutElement>().preferredHeight = 68f;
        GameUi.AddLayout<HorizontalLayoutGroup>(buttons, 12f).childForceExpandWidth = true;
        _upgrades = GameUi.AddButton(buttons, "Upgrades", null, new Vector2(210f, 68f), GameUi.Cream, GameUi.Ink, 27f);
        _closeEarly = GameUi.AddButton(buttons, "Close early", null, new Vector2(210f, 68f), GameUi.PanelDark, GameUi.Cream, 27f);
        _back = GameUi.AddButton(buttons, "Back", null, new Vector2(210f, 68f), GameUi.PanelDark, GameUi.Cream, 27f);
    }

    private void BuildGhost()
    {
        _dragGhost = GameUi.NewRect("DragGhost", _board);
        GameUi.Place(_dragGhost, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(176f, 214f));
        _dragGhost.localRotation = Quaternion.Euler(0f, 0f, -4f);
        GameUi.AddPanel(_dragGhost, new Color(0.83f, 0.78f, 0.7f)).raycastTarget = false;
        RectTransform page = GameUi.NewRect("Page", _dragGhost);
        page.anchorMin = new Vector2(0.2f, 0.42f);
        page.anchorMax = new Vector2(0.8f, 0.94f);
        page.offsetMin = page.offsetMax = Vector2.zero;
        _dragGhostPage = page.gameObject.AddComponent<Image>();
        _dragGhostPage.preserveAspect = true;
        _dragGhostPage.raycastTarget = false;
        _dragGhostName = GameUi.AddText(_dragGhost, "", 24f, GameUi.Ink, TextAlignmentOptions.Center, "DishName");
        Anchor(_dragGhostName.rectTransform, new Vector2(0f, 0.04f), new Vector2(1f, 0.42f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
        _dragGhost.gameObject.SetActive(false);
    }

    private void BuildConfirm()
    {
        _confirm = GameUi.NewRect("ConfirmEarly", _board);
        GameUi.Stretch(_confirm);
        GameUi.AddPanel(_confirm, new Color(0.08f, 0.07f, 0.05f, 0.5f), false);
        RectTransform box = GameUi.NewRect("ConfirmPaper", _confirm);
        GameUi.Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 560f));
        GameUi.AddPanel(box, GameUi.Panel2);
        _confirmText = GameUi.AddText(box, "", 34f, GameUi.Ink, TextAlignmentOptions.Top, "Text");
        _confirmText.rectTransform.offsetMin = new Vector2(56f, 170f);
        _confirmText.rectTransform.offsetMax = new Vector2(-56f, -76f);
        _confirmStay = GameUi.AddButton(box, "Stay open", null, new Vector2(300f, 100f), GameUi.Moss, GameUi.Cream);
        GameUi.Place((RectTransform)_confirmStay.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 62f), new Vector2(300f, 100f));
        _confirmGo = GameUi.AddButton(box, "Close anyway", null, new Vector2(300f, 100f), GameUi.Bark, GameUi.Cream);
        GameUi.Place((RectTransform)_confirmGo.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(16f, 62f), new Vector2(300f, 100f));
        _confirm.gameObject.SetActive(false);
    }

    private void BuildSummary(RectTransform root)
    {
        _summary = GameUi.NewRect("Summary", root);
        GameUi.Stretch(_summary);
        RectTransform paper = GameUi.NewRect("SummaryPaper", _summary);
        GameUi.Place(paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 860f));
        GameUi.AddPanel(paper, GameUi.Panel2);
        _summaryTitle = GameUi.AddText(paper, "", 60f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Title");
        _summaryTitle.rectTransform.offsetMin = new Vector2(76f, 0f);
        _summaryTitle.rectTransform.offsetMax = new Vector2(-76f, -86f);
        _summaryText = GameUi.AddText(paper, "", 34f, GameUi.Ink, TextAlignmentOptions.TopLeft, "SummaryText");
        _summaryText.rectTransform.offsetMin = new Vector2(76f, 190f);
        _summaryText.rectTransform.offsetMax = new Vector2(-76f, -172f);
        _nextDay = GameUi.AddButton(paper, "Next day", null, new Vector2(380f, 110f), GameUi.Moss, GameUi.Cream, 36f);
        GameUi.Place((RectTransform)_nextDay.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-76f, 84f), new Vector2(380f, 110f));
        _summary.gameObject.SetActive(false);
    }

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
