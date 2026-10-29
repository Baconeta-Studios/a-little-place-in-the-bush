using System.Collections;
using System.Collections.Generic;
using Bush.Core;
using TMPro;
using UI.Popups;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Foraging: pick a focus, a companion (each can go once a day, for a few coins) and perks (as many as
/// the perk slots allow), press Go, see what you found, collect it into the bag. It's always daytime for now, and there are a few trips a day (see <see cref="DayRules"/>). Companions and perks aren't offered yet; the rules already support
/// them (see docs/FORAGING.md). The setup shows what you might find, without the odds, and hides rare
/// things so they stay a surprise. Opened from the HUD (<see cref="GameHud"/>), or call
/// <see cref="Show"/> from anything else, such as a garden gate. Built in code.
///
/// The result is worked out and saved when Go is pressed (<see cref="ForagingService.Begin"/>), so
/// closing or refreshing mid-trip can't lose or duplicate it: reopening shows the same results.
/// Each trip logs its seed, which gives the same result in the web sandbox.
/// </summary>
public class ForagePopup : Popup
{
    private const float TripSeconds = 1.2f;
    private const int MaxPreviewItems = 8;

    private static ForagePopup _instance;

    private string _focusId = "";
    private string _companionId = "";
    private readonly List<string> _perkIds = new();
    private Coroutine _trip;

    private RectTransform _setup;
    private RectTransform _out;
    private RectTransform _results;
    private TextMeshProUGUI _tableName;
    private RectTransform _focusRow;
    private RectTransform _companionRow;
    private RectTransform _perkRow;
    private TextMeshProUGUI _perkLabel;
    private TextMeshProUGUI _tripsLeft;
    private Button _go;
    private ItemGridView _preview;
    private TextMeshProUGUI _previewNote;
    private TextMeshProUGUI _outText;
    private TextMeshProUGUI _resultsTitle;
    private ItemGridView _found;
    private TextMeshProUGUI _resultsNote;

    /// <summary>Opens the forage screen, creating it the first time.</summary>
    public static void Show()
    {
        if (!_instance) _instance = Create();
        _instance.Open();
    }

    private static ForagePopup Create()
    {
        GameUi ui = GameUi.Current;
        RectTransform root = GameUi.NewRect("Forage", ui.SafeRoot);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var popup = root.gameObject.AddComponent<ForagePopup>();
        popup.Build(root);
        return popup;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // A trip already under way (e.g. the page was refreshed on the results): show its results.
        if (Session != null && Session.State.hasPendingExpedition) ShowResults();
        else ShowSetup();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_trip != null) StopCoroutine(_trip);
        _trip = null;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>Closing from the results (Escape, or Collect) puts the finds in the bag.</summary>
    public override void Close()
    {
        if (_results && _results.gameObject.activeSelf) Collect(false);
        base.Close();
    }

    private static GameSession Session => GameSession.Current;

    private static string TableId(GameSession session) =>
        session.Design.forageTables.Count > 0 ? session.Design.forageTables[0].id : "";

    private ExpeditionSetup CurrentSetup(GameSession session) => new()
    {
        tableId = TableId(session),
        focusId = _focusId,
        companionId = _companionId,
        perkIds = new List<string>(_perkIds),
        time = TimeOfDay.Day // no night yet
    };

    // ----- Setup -----

    private void ShowSetup()
    {
        SetStep(_setup);
        RefreshSetup();
    }

    private void RefreshSetup()
    {
        GameSession session = Session;
        if (session == null) return;

        ForageTableData table = session.Design.GetForageTable(TableId(session));
        _tableName.text = table != null ? table.name : "Nowhere to forage (no forage table in the design data)";

        Clear(_focusRow);
        AddChoice(_focusRow, "Anything", _focusId == "", () => { _focusId = ""; RefreshSetup(); });
        foreach (FocusData focus in session.Design.focuses)
        {
            string id = focus.id;
            AddChoice(_focusRow, focus.name, _focusId == id, () => { _focusId = id; RefreshSetup(); });
        }

        // Who comes along: companions you've befriended, each once a day for a few coins.
        Clear(_companionRow);
        if (session.Design.GetCompanion(_companionId) == null || !Unlocks.IsCompanionUnlocked(session.Design, session.State, _companionId)) _companionId = "";
        AddChoice(_companionRow, "Just me", _companionId == "", () => { _companionId = ""; RefreshSetup(); });
        foreach (CompanionData companion in session.Design.companions)
        {
            if (!Unlocks.IsCompanionUnlocked(session.Design, session.State, companion.id)) continue;
            string id = companion.id;
            bool tired = ForagingService.IsTired(session.State, id);
            string label = tired ? $"{companion.name} (tired)" : companion.tripCost > 0 ? $"{companion.name}, {companion.tripCost}c" : companion.name;
            Button choice = AddChoice(_companionRow, label, _companionId == id, () => { _companionId = id; RefreshSetup(); });
            choice.interactable = !tired;
        }

        // Perks: learned from companions; as many as the slots allow, and they stack.
        Clear(_perkRow);
        int slots = session.Design.rules.PerkSlots(session.State);
        _perkIds.RemoveAll(id => !Unlocks.IsPerkUnlocked(session.Design, session.State, id));
        while (_perkIds.Count > slots) _perkIds.RemoveAt(_perkIds.Count - 1);
        bool anyPerks = false;
        foreach (PerkData perk in session.Design.perks)
        {
            if (!Unlocks.IsPerkUnlocked(session.Design, session.State, perk.id)) continue;
            anyPerks = true;
            string id = perk.id;
            bool on = _perkIds.Contains(id);
            AddChoice(_perkRow, perk.name, on, () =>
            {
                if (_perkIds.Contains(id)) _perkIds.Remove(id);
                else if (_perkIds.Count < slots) _perkIds.Add(id);
                else if (slots == 1) { _perkIds.Clear(); _perkIds.Add(id); } // one slot: swap
                RefreshSetup();
            });
        }
        _perkLabel.text = anyPerks ? $"Perks ({_perkIds.Count}/{slots})" : "Perks: none yet. Companions teach them after a few trips together.";

        int tripsLeft = ForagingService.TripsLeftToday(session.Design, session.State);
        bool canGo = ForagingService.CanBegin(session.Design, session.State, CurrentSetup(session), out string whyNot);
        string berry = ColorUtility.ToHtmlStringRGB(GameUi.Berry);
        _tripsLeft.text = tripsLeft == 0
            ? $"<color=#{berry}>It's evening. Close the cafe for the day, and forage again tomorrow.</color>"
            : !canGo ? $"<color=#{berry}>{whyNot}</color>"
            : tripsLeft < 0 ? "" : $"{tripsLeft} trip{(tripsLeft == 1 ? "" : "s")} left today";
        _go.interactable = canGo;

        // What you might find: the likeliest first, rare things left out so they stay a surprise.
        ExpeditionSetup setup = CurrentSetup(session);
        ForagePreview preview = ForagingService.Preview(session.Design, setup, new GameStateForageContext(session.State));
        FocusData chosenFocus = session.Design.GetFocus(_focusId);
        preview.odds.Sort((a, b) => b.chancePerRoll.CompareTo(a.chancePerRoll));

        var cards = new List<ItemCard>();
        bool anyRare = false;
        foreach (ForageOdds odds in preview.odds)
        {
            ItemData item = session.Design.GetItem(odds.itemId);
            if (item == null || odds.chancePerRoll <= 0) continue;
            if (item.rarity >= Rarity.Rare)
            {
                anyRare = true;
                continue;
            }
            if (cards.Count >= MaxPreviewItems) continue;
            bool focused = chosenFocus != null && chosenFocus.tags.Exists(item.HasTag);
            cards.Add(Card(session, item, focused ? "more" : ""));
        }
        _preview.SetItems(cards);

        _previewNote.text = (chosenFocus != null ? $"Looking out for {chosenFocus.name.ToLowerInvariant()}: those turn up more. " : "") +
                            (anyRare ? "Something rare might turn up too." : "");
    }

    private void Go()
    {
        GameSession session = Session;
        if (session == null) return;

        ExpeditionSetup setup = CurrentSetup(session);
        if (!ForagingService.CanBegin(session.Design, session.State, setup, out string error))
        {
            Debug.LogWarning($"[Forage] Can't go: {error}");
            return;
        }

        var seed = (uint)Random.Range(int.MinValue, int.MaxValue);
        PendingExpedition trip = ForagingService.Begin(session.Design, session.State, setup, seed, false, session.Economy);
        if (trip == null) return;
        session.SaveNow(); // the result is decided now; keep it if the page closes mid-trip

        Debug.Log($"[Forage] Trip {trip.id} (day {session.State.day}): table {setup.tableId}, focus '{setup.focusId}', " +
                  $"companion '{setup.companionId}', perks [{string.Join(", ", setup.perkIds)}], {setup.time}, seed {seed}. " +
                  "The web sandbox gives the same result with the same seed.");
        _trip = StartCoroutine(Trip());
    }

    private IEnumerator Trip()
    {
        SetStep(_out);
        for (float t = 0f; t < TripSeconds; t += Time.unscaledDeltaTime) // the popup pauses the game
        {
            _outText.text = "Out foraging" + new string('.', 1 + (int)(t * 3f) % 3);
            yield return null;
        }
        _trip = null;
        ShowResults();
    }

    // ----- Results -----

    private void ShowResults()
    {
        GameSession session = Session;
        ForageResult result = session != null ? ForagingService.GetPendingResult(session.State) : null;
        if (result == null)
        {
            ShowSetup();
            return;
        }

        SetStep(_results);
        var cards = new List<ItemCard>();
        foreach (ItemStack stack in result.stacks)
        {
            ItemData item = session.Design.GetItem(stack.itemId);
            if (item != null) cards.Add(Card(session, item, item.unique ? "" : "x" + stack.qty));
        }
        _found.SetItems(cards);

        _resultsTitle.text = cards.Count == 0 ? "Nothing this time" : $"You found {result.TotalItems()} things";

        var notes = new List<string>();
        foreach (string rareId in result.rareFinds)
        {
            ItemData rare = session.Design.GetItem(rareId);
            if (rare != null) notes.Add($"<color=#{ColorUtility.ToHtmlStringRGB(GameUi.Berry)}><b>A rare find: {rare.name}!</b></color>");
        }
        foreach (ItemStack stack in result.stacks)
        {
            ItemData item = session.Design.GetItem(stack.itemId);
            RecipeData recipe = item != null ? session.Design.GetRecipe(item.teachesRecipe) : null;
            if (recipe != null && !Unlocks.IsRecipeUnlocked(session.Design, session.State, recipe.id))
                notes.Add($"<b>{item.name}</b> will teach you a recipe: {recipe.name}.");
        }
        // A companion about to teach their perk (Claim does it).
        CompanionData companion = session.Design.GetCompanion(session.State.pendingExpedition.setup.companionId);
        PerkData perk = companion != null ? session.Design.GetPerk(companion.teachesPerk) : null;
        if (perk != null && !Unlocks.IsPerkUnlocked(session.Design, session.State, perk.id))
        {
            int trips = session.State.GetOrAddCompanion(companion.id).trips + 1;
            notes.Add(trips >= companion.tripsToTeach
                ? $"<b>{companion.name} has taught you a perk: {perk.name}!</b> You can use it without them now."
                : $"Trips with {companion.name}: {trips} of {companion.tripsToTeach} before they teach you {perk.name}.");
        }
        if (ForagingService.TripsLeftToday(session.Design, session.State) == 0)
            notes.Add("The sun's going down. Head back to the cafe to close up for the day.");
        if (Debug.isDebugBuild) notes.Add($"<size=70%><color=#{ColorUtility.ToHtmlStringRGB(GameUi.InkSoft)}>seed {result.seed}</color></size>");
        _resultsNote.text = string.Join("\n", notes);
    }

    private void Collect(bool close)
    {
        GameSession session = Session;
        if (session == null) return;
        ExpeditionClaim claim = ForagingService.Claim(session.Design, session.State, session.Economy, session.Events);
        if (claim.paid)
        {
            foreach (string recipeId in claim.learnedRecipeIds)
                Debug.Log($"[Forage] Learned recipe {recipeId}.");
            session.SaveNow();
        }
        if (close) Close();
    }

    // ----- Building the screen -----

    private void SetStep(RectTransform step)
    {
        _setup.gameObject.SetActive(step == _setup);
        _out.gameObject.SetActive(step == _out);
        _results.gameObject.SetActive(step == _results);
    }

    private static ItemCard Card(GameSession session, ItemData item, string badge)
    {
        ItemDefinition definition = session.Database.Find<ItemDefinition>(item.id);
        Sprite icon = ItemGridView.IconFor(definition, null, out Color tint);
        return new ItemCard { key = item.id, title = item.name, badge = badge, icon = icon, iconTint = tint };
    }

    private static Button AddChoice(RectTransform row, string label, bool selected, UnityEngine.Events.UnityAction onClick)
    {
        return GameUi.AddButton(row, label, onClick, new Vector2(210f, 76f),
            selected ? GameUi.Moss : GameUi.Cream, selected ? GameUi.Cream : GameUi.Ink, 28f);
    }

    private static void Clear(RectTransform rect)
    {
        for (int i = rect.childCount - 1; i >= 0; i--) Destroy(rect.GetChild(i).gameObject);
    }

    private void Build(RectTransform root)
    {
        // Dim the game behind, and block clicks to it.
        GameUi.AddPanel(root, new Color(0.08f, 0.07f, 0.05f, 0.55f), false);

        RectTransform panel = GameUi.NewRect("Panel", root);
        panel.anchorMin = new Vector2(0.03f, 0.04f);
        panel.anchorMax = new Vector2(0.97f, 0.96f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        GameUi.AddPanel(panel, GameUi.Panel);

        _setup = Step(panel, "Setup");
        _out = Step(panel, "Out");
        _results = Step(panel, "Results");

        // Setup: title, where, focus, time, what you might find, Go.
        Title(_setup, "Go foraging");
        CloseButton(_setup, "Close", Close);

        _tableName = Line(_setup, "", 32f, GameUi.Bark, -120f, 50f);

        Line(_setup, "Look out for", 30f, GameUi.InkSoft, -190f, 44f);
        _focusRow = Row(_setup, -240f, 80f);
        Line(_setup, "Take along", 30f, GameUi.InkSoft, -330f, 44f);
        _companionRow = Row(_setup, -374f, 76f);
        _perkLabel = Line(_setup, "Perks", 30f, GameUi.InkSoft, -460f, 44f);
        _perkRow = Row(_setup, -504f, 76f);
        Line(_setup, "You might find", 30f, GameUi.InkSoft, -590f, 44f);

        RectTransform previewArea = GameUi.NewRect("Preview", _setup);
        previewArea.anchorMin = new Vector2(0f, 0f);
        previewArea.anchorMax = new Vector2(1f, 1f);
        previewArea.offsetMin = new Vector2(24f, 190f);
        previewArea.offsetMax = new Vector2(-24f, -636f);
        _preview = ItemGridView.Create(previewArea, true, new Vector2(150f, 160f));

        _previewNote = GameUi.AddText(_setup, "", 28f, GameUi.InkSoft, TextAlignmentOptions.Left, "PreviewNote");
        Anchor(_previewNote.rectTransform, 40f, 110f, -400f, 190f);
        _tripsLeft = GameUi.AddText(_setup, "", 30f, GameUi.Bark, TextAlignmentOptions.Left, "TripsLeft");
        Anchor(_tripsLeft.rectTransform, 40f, 30f, -400f, 100f);

        _go = GameUi.AddButton(_setup, "Go!", Go, new Vector2(320f, 110f), GameUi.Moss, GameUi.Cream, 40f);
        GameUi.Place((RectTransform)_go.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 32f), new Vector2(320f, 110f));

        // Out: a short pause so it feels like a trip.
        _outText = GameUi.AddText(_out, "Out foraging...", 48f, GameUi.Ink, TextAlignmentOptions.Center, "OutText");

        // Results: what you found, notes (rare finds, recipes), Collect.
        _resultsTitle = Title(_results, "You found");
        RectTransform foundArea = GameUi.NewRect("Found", _results);
        foundArea.anchorMin = new Vector2(0f, 0f);
        foundArea.anchorMax = new Vector2(1f, 1f);
        foundArea.offsetMin = new Vector2(24f, 300f);
        foundArea.offsetMax = new Vector2(-24f, -130f);
        _found = ItemGridView.Create(foundArea, false, new Vector2(190f, 210f));

        _resultsNote = GameUi.AddText(_results, "", 32f, GameUi.Ink, TextAlignmentOptions.TopLeft, "ResultsNote");
        Anchor(_resultsNote.rectTransform, 40f, 150f, -400f, 290f);

        Button collect = GameUi.AddButton(_results, "Collect", () => Collect(true), new Vector2(320f, 110f), GameUi.Moss, GameUi.Cream, 40f);
        GameUi.Place((RectTransform)collect.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 32f), new Vector2(320f, 110f));
    }

    private static RectTransform Step(RectTransform panel, string name)
    {
        RectTransform step = GameUi.NewRect(name, panel);
        GameUi.Stretch(step);
        return step;
    }

    private static TextMeshProUGUI Title(RectTransform parent, string text)
    {
        TextMeshProUGUI title = GameUi.AddText(parent, text, 54f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Title");
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.offsetMin = new Vector2(40f, 0f);
        title.rectTransform.offsetMax = new Vector2(-260f, -28f);
        return title;
    }

    private static void CloseButton(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        Button close = GameUi.AddButton(parent, label, onClick, new Vector2(190f, 84f), GameUi.Bark, GameUi.Cream);
        GameUi.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(190f, 84f));
    }

    /// <summary>A line of text across the top part of the panel, <paramref name="top"/> below the top edge.</summary>
    private static TextMeshProUGUI Line(RectTransform parent, string text, float size, Color color, float top, float height)
    {
        TextMeshProUGUI line = GameUi.AddText(parent, text, size, color, TextAlignmentOptions.Left, "Line");
        RectTransform rect = line.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(40f, top - height);
        rect.offsetMax = new Vector2(-40f, top);
        return line;
    }

    private static RectTransform Row(RectTransform parent, float top, float height)
    {
        RectTransform row = GameUi.NewRect("Row", parent);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.offsetMin = new Vector2(32f, top - height);
        row.offsetMax = new Vector2(-32f, top);
        HorizontalLayoutGroup layout = GameUi.AddLayout<HorizontalLayoutGroup>(row, 12f);
        layout.childAlignment = TextAnchor.MiddleLeft;
        return row;
    }

    /// <summary>Stretches along the bottom of the panel, from <paramref name="bottom"/> to <paramref name="top"/>, leaving <paramref name="right"/> (negative) for a button.</summary>
    private static void Anchor(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }
}
