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
    private const float TripSeconds = 3.2f;
    /// <summary>How long a bush rustles: tap it in time to grab what's in it.</summary>
    private const float RustleSeconds = 0.5f;
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
    private TextMeshProUGUI _companionLabel;
    private TextMeshProUGUI _tripsLeft;
    private Button _go;
    /// <summary>Go was pressed with an empty menu and the warning shown: the next press goes anyway.</summary>
    private bool _emptyMenuWarned;
    /// <summary>The menu was empty when this trip set off although something could have been made: worth a note in the results.</summary>
    private bool _emptyMenuMissed;
    private ItemGridView _preview;
    private TextMeshProUGUI _previewNote;
    private TextMeshProUGUI _outText;
    private TextMeshProUGUI _peekNote;
    private RectTransform[] _bushes;
    private int _tappedBush = -1;
    private ForageRoll _bonus;
    private bool _skipped;
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
        time = session.State.isNight ? TimeOfDay.Night : TimeOfDay.Day,
        eventId = DayEvents.Today(session.Design, session.State)?.id ?? ""
    };

    // ----- Setup -----

    private void ShowSetup()
    {
        _emptyMenuWarned = false;
        SetStep(_setup);
        RefreshSetup();
    }

    private void RefreshSetup()
    {
        GameSession session = Session;
        if (session == null) return;

        ForageTableData table = session.Design.GetForageTable(TableId(session));
        _tableName.text = table != null ? table.name : "Nowhere to forage (no forage table in the design data)";
        DayEventData today = DayEvents.Today(session.Design, session.State);
        TimeOfDay now = session.State.isNight ? TimeOfDay.Night : TimeOfDay.Day;
        if (today != null && (DayEvents.ForageModifiersFor(session.Design, today.id, now).Count > 0 || today.peekLonger || today.peek.Count > 0))
            _tableName.text += $"   <color=#{ColorUtility.ToHtmlStringRGB(GameUi.Moss)}><b>{today.name}:</b> {today.effect}</color>";

        Clear(_focusRow);
        AddChoice(_focusRow, "Anything", _focusId == "", () => { _focusId = ""; RefreshSetup(); });
        foreach (FocusData focus in session.Design.focuses)
        {
            string id = focus.id;
            AddChoice(_focusRow, focus.name, _focusId == id, () => { _focusId = id; RefreshSetup(); });
        }

        // Who comes along: companions you've befriended, each once a day for a few coins.
        Clear(_companionRow);
        CompanionData kept = session.Design.GetCompanion(_companionId);
        if (kept == null || !Unlocks.IsCompanionUnlocked(session.Design, session.State, _companionId) || !ForagingService.IsAboutNow(session.State, kept)) _companionId = "";
        AddChoice(_companionRow, "Just me", _companionId == "", () => { _companionId = ""; RefreshSetup(); });
        foreach (CompanionData companion in session.Design.companions)
        {
            if (!Unlocks.IsCompanionUnlocked(session.Design, session.State, companion.id)) continue;
            if (!ForagingService.IsAboutNow(session.State, companion)) continue; // day companions sleep at night, and the other way round
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
        string soft = ColorUtility.ToHtmlStringRGB(GameUi.InkSoft);
        var chosenPerks = new List<ForageModifier>();
        foreach (string id in _perkIds) chosenPerks.AddRange(session.Design.GetPerk(id)?.modifiers ?? new List<ForageModifier>());
        _perkLabel.text = !anyPerks ? "Perks: none yet. Companions teach them after many trips together."
            : _perkIds.Count == 0 ? $"Perks (0/{slots})  <color=#{soft}>Tap one to take it along.</color>"
            : $"Perks ({_perkIds.Count}/{slots})  <color=#{soft}>{ModifierText.Describe(chosenPerks)}</color>";

        CompanionData chosen = session.Design.GetCompanion(_companionId);
        _companionLabel.text = chosen == null ? $"Take along  <color=#{soft}>Going alone is free.</color>"
            : $"Take along  <color=#{soft}>{chosen.name}: {ModifierText.Describe(chosen.modifiers)}. " +
              $"{(chosen.tripCost > 0 ? $"{chosen.tripCost} coins, " : "")}once a day.</color>";

        int tripsLeft = ForagingService.TripsLeftToday(session.Design, session.State);
        bool canGo = ForagingService.CanBegin(session.Design, session.State, CurrentSetup(session), out string whyNot);
        string berry = ColorUtility.ToHtmlStringRGB(GameUi.Berry);
        _tripsLeft.text = tripsLeft == 0
            ? session.State.isNight
                ? $"<color=#{berry}>That's tonight's trip done. Go to bed, and forage again tomorrow.</color>"
                : $"<color=#{berry}>It's evening. Close the cafe for the day, and forage again tomorrow.</color>"
            : !canGo ? $"<color=#{berry}>{whyNot}</color>"
            : tripsLeft < 0 ? "" : session.State.isNight ? "One trip tonight. Night brings different finds."
            : $"{tripsLeft} trip{(tripsLeft == 1 ? "" : "s")} left today";
        _go.interactable = canGo;
        GameUi.SetLabel(_go, _emptyMenuWarned ? "Go anyway" : "Go!");
        if (_emptyMenuWarned && canGo)
            _tripsLeft.text = $"<color=#{berry}>Your menu's empty, so the cafe won't serve anyone while you're out. Set it at the cafe door, or go anyway.</color>";

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

        // Teaches the focus: until you pick one, say what it does.
        _previewNote.text = (chosenFocus != null
                                ? $"Looking out for {chosenFocus.name.ToLowerInvariant()}: those turn up more (marked \"more\"). "
                                : "Tip: choose something to look out for, and you'll find more of it. ") +
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

        // By day, with nothing on the menu but something you could make, the cafe would serve nobody: say so once.
        // (Not when there's nothing to make anyway, like the very first trip.)
        bool emptyMenu = !session.State.isNight && session.Design.rules.TripsPerDay(session.State) > 0 && session.State.menu.Count == 0 &&
                         session.Cafe.CanCookAnything();
        _emptyMenuMissed = emptyMenu;
        if (emptyMenu && !_emptyMenuWarned)
        {
            _emptyMenuWarned = true;
            RefreshSetup();
            return;
        }
        _emptyMenuWarned = false;

        var seed = (uint)Random.Range(int.MinValue, int.MaxValue);
        seed = ForagingService.LuckySeed(session.Design, session.State, setup, seed); // the tutorial's leaves
        PendingExpedition trip = ForagingService.Begin(session.Design, session.State, setup, seed, false, session.Economy);
        if (trip == null) return;
        session.Cafe.ServeWhileOut(); // the cafe serves this part of the day while you're out
        session.SaveNow(); // the result is decided now; keep it if the page closes mid-trip

        Debug.Log($"[Forage] Trip {trip.id} (day {session.State.day}): table {setup.tableId}, focus '{setup.focusId}', " +
                  $"companion '{setup.companionId}', perks [{string.Join(", ", setup.perkIds)}], {setup.time}, seed {seed}. " +
                  "The web sandbox gives the same result with the same seed.");
        _trip = StartCoroutine(Trip());
    }

    /// <summary>
    /// The trip itself: "Out foraging...", and a little peek-a-boo. After a moment one of three bushes
    /// rustles: tap that bush while it's rustling to grab what's in it as a bonus find. What it was is only
    /// revealed in the results (no item art needed). Miss it and it's gone; the trip's own finds are never
    /// affected. Skip goes straight to the results.
    /// </summary>
    private IEnumerator Trip()
    {
        SetStep(_out);
        GameSession session = Session;
        ForageRoll peek = session != null ? ForagingService.PeekABooFind(session.Design, session.State) : null;
        uint seed = session != null && session.State.hasPendingExpedition ? session.State.pendingExpedition.seed : 0u;
        int bush = (int)(seed % 3u);
        float rustleAt = 0.7f + (seed >> 4) % 9 * 0.1f; // a different moment each trip
        bool stillAir = session != null && DayEvents.Today(session.Design, session.State)?.peekLonger == true;
        float rustleFor = stillAir ? RustleSeconds * 2f : RustleSeconds;
        _tappedBush = -1;
        _skipped = false;
        _bonus = null;
        _peekNote.text = peek != null ? "Keep an eye on the bushes..." : "";
        foreach (RectTransform b in _bushes) b.localRotation = Quaternion.identity;

        bool resolved = peek == null;
        for (float t = 0f; t < TripSeconds && !_skipped; t += Time.unscaledDeltaTime) // the popup pauses the game
        {
            _outText.text = "Out foraging" + new string('.', 1 + (int)(t * 3f) % 3);

            if (!resolved)
            {
                bool rustling = t >= rustleAt && t < rustleAt + rustleFor;
                _bushes[bush].localRotation = Quaternion.Euler(0f, 0f, rustling ? Mathf.Sin(t * 40f) * 7f : 0f);
                if (rustling && _peekNote.text != "Something's rustling!") _peekNote.text = "Something's rustling!";

                if (_tappedBush == bush && rustling)
                {
                    _bonus = ForagingService.TakePeekABoo(session.Design, session.State);
                    session.SaveNow();
                    _peekNote.text = $"<color=#{ColorUtility.ToHtmlStringRGB(GameUi.Moss)}><b>Got something!</b></color>";
                    resolved = true;
                }
                else if (t >= rustleAt + rustleFor)
                {
                    ForagingService.MissPeekABoo(session.State);
                    _peekNote.text = "It got away.";
                    resolved = true;
                }
                if (resolved) _bushes[bush].localRotation = Quaternion.identity;
            }
            _tappedBush = -1; // a tap only counts on the frame it lands
            yield return null;
        }

        if (!resolved && session != null) ForagingService.MissPeekABoo(session.State);
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
        if (_bonus != null)
        {
            ItemData bonusItem = session.Design.GetItem(_bonus.itemId);
            notes.Add($"<color=#{ColorUtility.ToHtmlStringRGB(GameUi.Moss)}>From the rustling bush: +{_bonus.qty} {bonusItem?.name ?? _bonus.itemId}</color>");
            _bonus = null;
        }

        // What the cafe sold while you were out on this trip.
        if (_emptyMenuMissed)
            notes.Add($"<color=#{ColorUtility.ToHtmlStringRGB(GameUi.InkSoft)}>The cafe's menu was empty, so nothing sold while you were out.</color>");
        else if (session.State.servedWhileOut.Count > 0)
        {
            var sold = new List<string>();
            int coins = 0;
            foreach (DishSale dish in session.State.servedWhileOut)
            {
                sold.Add($"{dish.sold} {session.Design.GetRecipe(dish.recipeId)?.name ?? dish.recipeId}");
                coins += dish.coins;
            }
            notes.Add($"While you were out, the cafe sold {string.Join(", ", sold)}. <color=#{ColorUtility.ToHtmlStringRGB(GameUi.Moss)}>+{coins} coins</color>");
        }
        if (ForagingService.TripsLeftToday(session.Design, session.State) == 0)
            notes.Add("The sun's going down. Head back to the cafe to close up for the day.");
        if (Debug.isDebugBuild) notes.Add($"<size=70%><color=#{ColorUtility.ToHtmlStringRGB(GameUi.InkSoft)}>seed {result.seed}</color></size>");
        _resultsNote.text = string.Join("\n", notes);
    }

    private void Collect(bool close)
    {
        _emptyMenuMissed = false; // that note was for this trip only
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
        _companionLabel = Line(_setup, "Take along", 30f, GameUi.InkSoft, -330f, 44f);
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
        _outText = GameUi.AddText(_out, "Out foraging...", 48f, GameUi.Ink, TextAlignmentOptions.Top, "OutText");
        _outText.rectTransform.offsetMax = new Vector2(0f, -60f);

        // Peek-a-boo: three bushes (plain circles, no art needed); tap the one that rustles.
        _bushes = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            RectTransform bushRect = GameUi.NewRect($"Bush{i}", _out);
            GameUi.Place(bushRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2((i - 1) * 380f, -170f), new Vector2(300f, 200f));
            Image bushImage = bushRect.gameObject.AddComponent<Image>();
            bushImage.sprite = GeneratedSprites.Circle;
            bushImage.color = new Color(0.33f + i * 0.04f, 0.52f, 0.28f);
            var bushButton = bushRect.gameObject.AddComponent<Button>();
            bushButton.targetGraphic = bushImage;
            bushButton.onClick.AddListener(() => _tappedBush = index);
            _bushes[i] = bushRect;
        }

        _peekNote = GameUi.AddText(_out, "", 34f, GameUi.InkSoft, TextAlignmentOptions.Center, "PeekNote");
        _peekNote.rectTransform.anchorMin = new Vector2(0f, 0f);
        _peekNote.rectTransform.anchorMax = new Vector2(1f, 0f);
        _peekNote.rectTransform.pivot = new Vector2(0.5f, 0f);
        _peekNote.rectTransform.offsetMin = new Vector2(40f, 150f);
        _peekNote.rectTransform.offsetMax = new Vector2(-40f, 220f);

        Button skip = GameUi.AddButton(_out, "Skip", () => _skipped = true, new Vector2(200f, 84f), GameUi.Bark, GameUi.Cream);
        GameUi.Place((RectTransform)skip.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 32f), new Vector2(200f, 84f));

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
