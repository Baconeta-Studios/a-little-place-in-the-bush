using System.Collections;
using System.Collections.Generic;
using Bush.Core;
using TMPro;
using UI.Popups;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Foraging, in three steps. At the edge of the bush: one question, what are we after? The focus the story
/// needs is already chosen and marked (<see cref="ForageGoals"/>), and companions and perks only appear
/// once you have some. Out foraging: a place of its own in the bush, made from crops of the backdrop and
/// layers of the Bush art. The gecko looks around, and tapping the bush that rustles grabs a bonus. Back again: finds drop into the basket and go straight in the bag, with what the cafe
/// sold, how the quest is going, and one main button for what to do next. Opened from
/// <see cref="ForageSpot"/>, or call <see cref="Show"/> from anything else, such as a garden gate.
/// How it looks is in Resources/UI/ForagePopup.prefab (see docs/UI.md), made from <see cref="BuildDefault"/>,
/// which is also used if there's no prefab.
///
/// The result is worked out and saved when you head out (<see cref="ForagingService.Begin"/>), so
/// closing or refreshing mid-trip can't lose or duplicate it: reopening shows the same results.
/// Each trip logs its seed, which gives the same result in the web sandbox.
/// </summary>
public class ForagePopup : Popup
{
    public const string ResourcePath = "UI/ForagePopup";

    private const float TripSeconds = 3.2f;
    /// <summary>How long a bush rustles: tap it in time to grab what's in it.</summary>
    private const float RustleSeconds = 0.5f;
    private const int MaxPreviewItems = 5;
    private const float DropHeight = 520f;
    private const float DropSeconds = 0.4f;
    private const float DropGap = 0.16f;
    private const float SceneryDrift = 70f;
    private const float HopSeconds = 0.45f;

    private static ForagePopup _instance;

    private string _focusId = "";
    private string _companionId = "";
    private readonly List<string> _perkIds = new();
    private Coroutine _trip;

    [Header("The three steps")]
    [SerializeField] private RectTransform _setup;
    [SerializeField] private RectTransform _out;
    [SerializeField] private RectTransform _results;
    [Header("Setup")]
    [SerializeField] private RectTransform _focusRow;
    [SerializeField] private GameObject _companionSection;
    [SerializeField] private RectTransform _companionRow;
    [SerializeField] private TextMeshProUGUI _companionLabel;
    [SerializeField] private GameObject _perkSection;
    [SerializeField] private RectTransform _perkRow;
    [SerializeField] private TextMeshProUGUI _perkLabel;
    [SerializeField] private TextMeshProUGUI _previewLabel;
    [SerializeField] private ItemGridView _preview;
    [SerializeField] private TextMeshProUGUI _previewNote;
    [SerializeField] private Image _companionArt;
    [SerializeField] private RectTransform _setupPips;
    [SerializeField] private TextMeshProUGUI _status;
    [SerializeField] private Button _close;
    [SerializeField] private Button _go;
    [Tooltip("Copied for each choice above, so restyling this restyles them all. A child called Nudge is the note on the one a quest needs.")]
    [SerializeField] private Button _choiceTemplate;
    [SerializeField] private Color _choiceColor = GameUi.Cream;
    [SerializeField] private Color _choiceTextColor = GameUi.Ink;
    [SerializeField] private Color _chosenColor = new(0.2f, 0.36f, 0.21f);
    [SerializeField] private Color _chosenTextColor = GameUi.Cream;
    [Tooltip("One marker for each trip in the day, copied. Faded once used.")]
    [SerializeField] private Image _pipTemplate;
    [SerializeField, Range(0f, 1f)] private float _usedPipAlpha = 0.3f;
    [Header("Out foraging")]
    [SerializeField] private TextMeshProUGUI _outText;
    [SerializeField] private TextMeshProUGUI _peekNote;
    [Tooltip("The bushes to tap: one rustles each trip. Each needs a Button.")]
    [SerializeField] private RectTransform[] _bushes;
    [Tooltip("Stands where you put it, pads on the spot, and turns to face the bush that rustles.")]
    [SerializeField] private RectTransform _gecko;
    [Tooltip("Drifts slowly to the side during the trip, so it feels like moving along. Can be left empty.")]
    [SerializeField] private RectTransform _scenery;
    [SerializeField] private Button _skip;
    [Header("Back again")]
    [SerializeField] private TextMeshProUGUI _resultsTitle;
    [SerializeField] private TextMeshProUGUI _resultsNote;
    [Tooltip("Finds land here, above the front of the basket.")]
    [SerializeField] private RectTransform _pile;
    [SerializeField] private ItemCardView _foundTemplate;
    [Tooltip("Finds in one row before they split into a front and a back row.")]
    [SerializeField, Min(1)] private int _findsInARow = 5;
    [Tooltip("How far above the front row the back row sits.")]
    [SerializeField] private float _backRowRise = 70f;
    [Tooltip("The most a find leans either way, in degrees.")]
    [SerializeField] private float _findTilt = 10f;
    [Tooltip("The most a find sits above or below its row.")]
    [SerializeField] private float _findJitter = 10f;
    [SerializeField] private GameObject _questCard;
    [SerializeField] private TextMeshProUGUI _questLabel;
    [SerializeField] private Image _questBar;
    private GameObject _companionCard;
    [SerializeField] private RectTransform _resultPips;
    [SerializeField] private Button _nextMain;
    [SerializeField] private Button _nextOther;

    /// <summary>Go was pressed with an empty menu and the warning shown: the next press goes anyway.</summary>
    private bool _emptyMenuWarned;
    /// <summary>The menu was empty when this trip set off although something could have been made: worth a note in the results.</summary>
    private bool _emptyMenuMissed;
    private string _hintKey;
    private int _tappedBush = -1;
    private ForageRoll _bonus;
    private bool _skipped;
    private Vector2 _geckoStart;
    private Vector2 _sceneryStart;
    private System.Action _mainAction;
    private System.Action _otherAction;

    private static GameSession Session => GameSession.Current;

    /// <summary>Opens the forage screen, creating it the first time.</summary>
    public static void Show()
    {
        if (!_instance) _instance = Create();
        _instance.Open();
    }

    private static ForagePopup Create()
    {
        Transform safeRoot = GameUi.Current.SafeRoot;
        var prefab = Resources.Load<ForagePopup>(ResourcePath);
        if (!prefab) return BuildDefault(safeRoot);

        // A popup is "open" while active, and the prefab is saved on so it can be seen and edited:
        // copy it under something switched off, so it arrives closed without ever opening.
        var closed = new GameObject("Closed");
        closed.SetActive(false);
        ForagePopup popup = Instantiate(prefab, closed.transform);
        popup.gameObject.SetActive(false);
        popup.transform.SetParent(safeRoot, false);
        Destroy(closed);
        return popup;
    }

    /// <summary>The buttons are linked here rather than in the prefab, so restyling can't unhook them.</summary>
    protected override void Awake()
    {
        base.Awake();
        _close.onClick.AddListener(Close);
        _go.onClick.AddListener(Go);
        _skip.onClick.AddListener(() => _skipped = true);
        _nextMain.onClick.AddListener(() => _mainAction?.Invoke());
        _nextOther.onClick.AddListener(() => _otherAction?.Invoke());
        for (int i = 0; i < _bushes.Length; i++)
        {
            int index = i;
            _bushes[i].GetComponent<Button>().onClick.AddListener(() => _tappedBush = index);
        }
        if (_gecko) _geckoStart = _gecko.anchoredPosition;
        if (_scenery) _sceneryStart = _scenery.anchoredPosition;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // A trip already under way (e.g. the page was refreshed mid-trip): show its results.
        if (Session != null && Session.State.hasPendingExpedition) ShowResults();
        else ShowSetup();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopAllCoroutines();
        _trip = null;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

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

        // The focus a quest needs is chosen for you once. After that, your choice sticks.
        ForageGoal goal = ForageGoals.Current(session.Design, session.Quests);
        bool hint = goal != null && !goal.IsMet && goal.focus != null;
        if (hint)
        {
            string key = goal.quest.id + goal.item.id;
            if (key != _hintKey)
            {
                _hintKey = key;
                _focusId = goal.focus.id;
            }
        }

        Clear(_focusRow);
        AddChoice(_focusRow, "Anything", _focusId == "", () => { _focusId = ""; RefreshSetup(); });
        foreach (FocusData focus in session.Design.focuses)
        {
            string id = focus.id;
            Button choice = AddChoice(_focusRow, focus.name, _focusId == id, () => { _focusId = id; RefreshSetup(); });
            if (hint && goal.focus.id == id)
                SetNudge(choice, ForageGoals.Describe(goal, QuestLogPopup.NpcName(session.Design, goal.quest.giver)));
        }

        RefreshCompanions(session);
        RefreshPerks(session);
        RefreshPreview(session);
        RefreshStatus(session);
    }

    /// <summary>Who comes along: companions you've befriended, each once a day. Hidden until you have one.</summary>
    private void RefreshCompanions(GameSession session)
    {
        Clear(_companionRow);
        CompanionData kept = session.Design.GetCompanion(_companionId);
        if (kept == null || !Unlocks.IsCompanionUnlocked(session.Design, session.State, _companionId) || !ForagingService.IsAboutNow(session.State, kept)
            || ForagingService.IsTired(session.State, _companionId)) _companionId = "";

        bool any = false;
        AddChoice(_companionRow, "Just me", _companionId == "", () => { _companionId = ""; RefreshSetup(); });
        foreach (CompanionData companion in session.Design.companions)
        {
            if (!Unlocks.IsCompanionUnlocked(session.Design, session.State, companion.id)) continue;
            if (!ForagingService.IsAboutNow(session.State, companion)) continue; // day companions sleep at night, and the other way round
            any = true;
            string id = companion.id;
            bool tired = ForagingService.IsTired(session.State, id);
            string label = tired ? $"{companion.name} (tired)" : companion.tripCost > 0 ? $"{companion.name}, {companion.tripCost}c" : companion.name;
            Button choice = AddChoice(_companionRow, label, _companionId == id, () => { _companionId = id; RefreshSetup(); });
            choice.interactable = !tired;
        }
        _companionSection.SetActive(any);

        CompanionData chosen = session.Design.GetCompanion(_companionId);
        string soft = ColorUtility.ToHtmlStringRGB(GameUi.InkSoft);
        _companionLabel.text = chosen == null ? "Companions"
            : $"Companions  <color=#{soft}>{chosen.name}: {ModifierText.Describe(chosen.modifiers)}. " +
              $"{(chosen.tripCost > 0 ? $"{chosen.tripCost} coins, once" : "Once")} a day.</color>";

        Sprite portrait = chosen != null ? session.Database.Find<CompanionDefinition>(chosen.id)?.portrait : null;
        if (_companionArt)
        {
            _companionArt.gameObject.SetActive(portrait);
            _companionArt.sprite = portrait;
        }
    }

    /// <summary>Perks: learned from companions; as many as the slots allow, and they stack. Hidden until you have one.</summary>
    private void RefreshPerks(GameSession session)
    {
        Clear(_perkRow);
        int slots = session.Design.rules.PerkSlots(session.State);
        _perkIds.RemoveAll(id => !Unlocks.IsPerkUnlocked(session.Design, session.State, id));
        while (_perkIds.Count > slots) _perkIds.RemoveAt(_perkIds.Count - 1);
        bool any = false;
        foreach (PerkData perk in session.Design.perks)
        {
            if (!Unlocks.IsPerkUnlocked(session.Design, session.State, perk.id)) continue;
            any = true;
            string id = perk.id;
            AddChoice(_perkRow, perk.name, _perkIds.Contains(id), () =>
            {
                if (_perkIds.Contains(id)) _perkIds.Remove(id);
                else if (_perkIds.Count < slots) _perkIds.Add(id);
                else if (slots == 1) { _perkIds.Clear(); _perkIds.Add(id); } // one slot: swap
                RefreshSetup();
            });
        }
        _perkSection.SetActive(any);

        string soft = ColorUtility.ToHtmlStringRGB(GameUi.InkSoft);
        var chosen = new List<ForageModifier>();
        foreach (string id in _perkIds) chosen.AddRange(session.Design.GetPerk(id)?.modifiers ?? new List<ForageModifier>());
        _perkLabel.text = _perkIds.Count == 0 ? $"Perks (0/{slots})"
            : $"Perks ({_perkIds.Count}/{slots})  <color=#{soft}>{ModifierText.Describe(chosen)}</color>";
    }

    /// <summary>What you might find: what you're looking for first, then the likeliest, leaving rare things as a surprise.</summary>
    private void RefreshPreview(GameSession session)
    {
        ForagePreview preview = ForagingService.Preview(session.Design, CurrentSetup(session), new GameStateForageContext(session.State));
        FocusData focus = session.Design.GetFocus(_focusId);
        preview.odds.Sort((a, b) => b.chancePerRoll.CompareTo(a.chancePerRoll));

        var focused = new List<ItemCard>();
        var others = new List<ItemCard>();
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
            if (focus != null && focus.tags.Exists(item.HasTag)) focused.Add(Card(session, item, "more"));
            else others.Add(Card(session, item, ""));
        }
        focused.AddRange(others);
        if (focused.Count > MaxPreviewItems) focused.RemoveRange(MaxPreviewItems, focused.Count - MaxPreviewItems);
        _preview.SetItems(focused);

        _previewLabel.text = focus != null ? "You'll find more of these:" : "A bit of everything:";
        string note = anyRare ? "And maybe something rare." : "";
        // Today's event changes what turns up, so it goes with the finds.
        DayEventData today = DayEvents.Today(session.Design, session.State);
        TimeOfDay now = session.State.isNight ? TimeOfDay.Night : TimeOfDay.Day;
        if (today != null && (DayEvents.ForageModifiersFor(session.Design, today.id, now).Count > 0 || today.peekLonger || today.peek.Count > 0))
            note += (note.Length > 0 ? "\n" : "") + $"<color=#{ColorUtility.ToHtmlStringRGB(GameUi.Moss)}><b>{today.name}:</b> {today.effect}</color>";
        _previewNote.text = note;
    }

    /// <summary>Trips left, whether you can go, and what the cafe does while you're out.</summary>
    private void RefreshStatus(GameSession session)
    {
        DayRules rules = session.Design.rules;
        GameState state = session.State;
        int tripsLeft = ForagingService.TripsLeftToday(session.Design, state);
        bool canGo = ForagingService.CanBegin(session.Design, state, CurrentSetup(session), out string whyNot);
        QuestData waitsFor = session.Quests.ForagingWaitsFor();
        if (canGo && waitsFor != null)
        {
            canGo = false;
            whyNot = $"{QuestLogPopup.NpcName(session.Design, waitsFor.giver)} wants to say hello before you head into the bush.";
        }

        string status;
        if (tripsLeft == 0)
            status = state.isNight ? "That's tonight's trip done. Go to bed, and forage again tomorrow."
                : "It's evening. Close the cafe for the day, and forage again tomorrow.";
        else if (!canGo) status = whyNot;
        else if (_emptyMenuWarned) status = "Your menu's empty, so the cafe won't serve anyone while you're out. Set it at the cafe door, or go anyway.";
        else if (state.isNight) status = "Night brings different finds.";
        else if (tripsLeft < 0) status = "";
        else
        {
            int serves = rules.NextPartSales(state);
            status = state.menu.Count > 0 && serves > 0 ? $"The cafe serves {serves} dish{(serves == 1 ? "" : "es")} while you're out"
                : state.menu.Count == 0 && session.Cafe.CanCookAnything() ? "The cafe's menu is empty"
                : ""; // nothing to say until there's something you could make
        }
        _status.text = status;
        _status.transform.parent.gameObject.SetActive(status.Length > 0);

        _go.interactable = canGo;
        SetLabel(_go, _emptyMenuWarned ? "Go anyway" : "Head out");
        RefreshPips(_setupPips, session);
    }

    private void RefreshPips(RectTransform row, GameSession session)
    {
        Clear(row);
        GameState state = session.State;
        int total = state.isNight ? session.Design.rules.nightTrips : session.Design.rules.TripsPerDay(state);
        int left = ForagingService.TripsLeftToday(session.Design, state);
        row.gameObject.SetActive(total > 0 && left >= 0);
        for (int i = 0; i < total; i++)
        {
            Image pip = Instantiate(_pipTemplate, row);
            pip.gameObject.SetActive(true);
            Color color = pip.color;
            color.a = i < left ? 1f : _usedPipAlpha;
            pip.color = color;
        }
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
        if (session.Quests.ForagingWaitsFor() != null) return; // Go is greyed out; just in case

        // By day, with nothing on the menu but something you could make, the cafe would serve nobody: say so once.
        // (Not when there's nothing to make anyway, like the very first trip.)
        bool emptyMenu = !session.State.isNight && session.Design.rules.TripsPerDay(session.State) > 0 && session.State.menu.Count == 0 &&
                         session.Cafe.CanCookAnything();
        _emptyMenuMissed = emptyMenu;
        if (emptyMenu && !_emptyMenuWarned)
        {
            _emptyMenuWarned = true;
            SetStep(_setup); // pressing Go again from the results comes back here for the warning
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
    /// The trip itself: the gecko looks around the bushes, and a little peek-a-boo. After a moment one bush
    /// rustles: tap it while it's rustling to grab what's in it as a bonus find. What it was is only revealed
    /// in the results. Miss it and it's gone; the trip's own finds are never affected. Skip goes straight to the results.
    /// </summary>
    private IEnumerator Trip()
    {
        SetStep(_out);
        GameSession session = Session;
        ForageRoll peek = session != null ? ForagingService.PeekABooFind(session.Design, session.State) : null;
        uint seed = session != null && session.State.hasPendingExpedition ? session.State.pendingExpedition.seed : 0u;
        int bush = (int)(seed % (uint)_bushes.Length);
        float rustleAt = 0.7f + (seed >> 4) % 9 * 0.1f; // a different moment each trip
        bool stillAir = session != null && DayEvents.Today(session.Design, session.State)?.peekLonger == true;
        float rustleFor = stillAir ? RustleSeconds * 2f : RustleSeconds;
        _tappedBush = -1;
        _skipped = false;
        _bonus = null;
        _outText.text = "Out foraging...";
        _peekNote.text = peek != null ? "Keep an eye on the bushes" : "";
        foreach (RectTransform b in _bushes) b.localRotation = Quaternion.identity;

        bool resolved = peek == null;
        float hopAt = -1f;
        float facing = 1f;
        for (float t = 0f; t < TripSeconds && !_skipped; t += Time.unscaledDeltaTime) // the popup pauses the game
        {
            if (_scenery) _scenery.anchoredPosition = _sceneryStart + new Vector2(-SceneryDrift * t / TripSeconds, 0f);

            if (!resolved)
            {
                bool rustling = t >= rustleAt && t < rustleAt + rustleFor;
                _bushes[bush].localRotation = Quaternion.Euler(0f, 0f, rustling ? Mathf.Sin(t * 40f) * 7f : 0f);
                if (rustling)
                {
                    _outText.text = "Something's rustling!";
                    _peekNote.text = "";
                    if (_gecko) facing = _bushes[bush].position.x < _gecko.position.x ? -1f : 1f;
                }

                if (_tappedBush == bush && rustling)
                {
                    _bonus = ForagingService.TakePeekABoo(session.Design, session.State);
                    session.SaveNow();
                    _outText.text = "Got something!";
                    hopAt = t;
                    _peekNote.text = "";
                    resolved = true;
                }
                else if (t >= rustleAt + rustleFor)
                {
                    ForagingService.MissPeekABoo(session.State);
                    _outText.text = "Out foraging...";
                    _peekNote.text = "It got away.";
                    resolved = true;
                }
                if (resolved) _bushes[bush].localRotation = Quaternion.identity;
            }
            MoveGecko(t, hopAt, facing);
            _tappedBush = -1; // a tap only counts on the frame it lands
            yield return null;
        }

        if (!resolved && session != null) ForagingService.MissPeekABoo(session.State);
        _trip = null;
        ShowResults();
    }

    /// <summary>Pads on the spot, facing the way it's looking, with a happy hop when you grab something.</summary>
    private void MoveGecko(float t, float hopAt, float facing)
    {
        if (!_gecko) return;
        float height = Mathf.Abs(Mathf.Sin(t * 7f)) * 10f;
        if (hopAt >= 0f && t - hopAt < HopSeconds) height += Mathf.Sin((t - hopAt) / HopSeconds * Mathf.PI) * 90f;
        _gecko.anchoredPosition = _geckoStart + new Vector2(0f, height);
        Vector3 scale = _gecko.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        _gecko.localScale = scale;
    }

    // ----- Back again -----

    /// <summary>Shows what you found and puts it straight in the bag.</summary>
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
            if (item != null) cards.Add(Card(session, item, stack.qty > 1 ? "x" + stack.qty : ""));
        }
        int found = result.TotalItems();
        _resultsTitle.text = found == 0 ? "Nothing this time" : $"{found} find{(found == 1 ? "" : "s")}, in your bag";
        _resultsNote.text = ResultNotes(session, result);
        RefreshCompanionCard(session); // before Claim, which teaches the perk

        Claim(session);
        RefreshQuestCard(session);
        RefreshPips(_resultPips, session);
        SetNextSteps(session);
        StartCoroutine(DropFinds(cards));
    }

    private string ResultNotes(GameSession session, ForageResult result)
    {
        var notes = new List<string>();
        if (result.stacks.Count > 0) notes.Add(FindsLine(session, result));
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
                notes.Add($"<b>{item.name}</b> has taught you a recipe: {recipe.name}.");
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
            notes.Add($"The cafe sold {string.Join(", ", sold)}. <color=#{ColorUtility.ToHtmlStringRGB(GameUi.Moss)}>+{coins} coins</color>");
        }
        if (Debug.isDebugBuild) notes.Add($"<size=70%><color=#{ColorUtility.ToHtmlStringRGB(GameUi.InkSoft)}>seed {result.seed}</color></size>");
        return string.Join("\n", notes);
    }

    /// <summary>What's in the basket, by name: "Leaves x5, Mushroom, Fern frond".</summary>
    private static string FindsLine(GameSession session, ForageResult result)
    {
        var finds = new List<string>();
        foreach (ItemStack stack in result.stacks)
        {
            string name = session.Design.GetItem(stack.itemId)?.name ?? stack.itemId;
            finds.Add(stack.qty > 1 ? $"{name} x{stack.qty}" : name);
        }
        return string.Join(", ", finds);
    }

    private void Claim(GameSession session)
    {
        _emptyMenuMissed = false; // that note was for this trip only
        ExpeditionClaim claim = ForagingService.Claim(session.Design, session.State, session.Economy, session.Events);
        if (!claim.paid) return;
        foreach (string recipeId in claim.learnedRecipeIds)
            Debug.Log($"[Forage] Learned recipe {recipeId}.");
        session.SaveNow();
    }

    private void RefreshQuestCard(GameSession session)
    {
        ForageGoal goal = ForageGoals.Current(session.Design, session.Quests);
        _questCard.SetActive(goal != null);
        if (goal == null) return;
        _questLabel.text = $"{goal.quest.title}: {goal.have} of {goal.needed} {goal.item.name.ToLowerInvariant()}";
        if (!_questBar.sprite) _questBar.sprite = GeneratedSprites.Square; // an image with no sprite ignores its fill, and shows full
        _questBar.fillAmount = goal.needed > 0 ? (float)goal.have / goal.needed : 1f;
    }

    /// <summary>
    /// A companion teaching their perk gets a card of its own, under the quest's. It's a copy of the quest card,
    /// so restyling that one in the prefab restyles both.
    /// </summary>
    private void RefreshCompanionCard(GameSession session)
    {
        if (!_companionCard)
        {
            _companionCard = Instantiate(_questCard, _questCard.transform.parent);
            _companionCard.name = "CompanionCard";
            _companionCard.transform.SetSiblingIndex(_questCard.transform.GetSiblingIndex() + 1);
        }
        CompanionData companion = session.Design.GetCompanion(session.State.pendingExpedition.setup.companionId);
        PerkData perk = companion != null ? session.Design.GetPerk(companion.teachesPerk) : null;
        bool teaching = perk != null && !Unlocks.IsPerkUnlocked(session.Design, session.State, perk.id);
        _companionCard.SetActive(teaching);
        if (!teaching) return;

        int trips = session.State.GetOrAddCompanion(companion.id).trips + 1;
        _companionCard.GetComponentInChildren<TextMeshProUGUI>(true).text = trips >= companion.tripsToTeach
            ? $"<b>{companion.name} has taught you a perk: {perk.name}!</b>"
            : $"Trips with {companion.name}: {trips} of {companion.tripsToTeach} to learn {perk.name}";
        Image bar = _companionCard.transform.Find("Bar/Fill").GetComponent<Image>();
        if (!bar.sprite) bar.sprite = GeneratedSprites.Square;
        bar.fillAmount = companion.tripsToTeach > 0 ? Mathf.Clamp01((float)trips / companion.tripsToTeach) : 1f;
    }

    /// <summary>One main button for what to do next: see whoever has finished work for you if they're here, go again, or head home.</summary>
    private void SetNextSteps(GameSession session)
    {
        int tripsLeft = ForagingService.TripsLeftToday(session.Design, session.State);
        bool canGoAgain = tripsLeft != 0;
        // The same trip can't always run twice (a companion who went along is tired now), so that one goes back to the choices.
        bool sameTrip = ForagingService.CanBegin(session.Design, session.State, CurrentSetup(session), out _);
        string againLabel = sameTrip ? "Go again" : "Plan another trip";
        System.Action again = sameTrip ? Go : ShowSetup;
        QuestData ready = ForageGoals.ReadyToHandIn(session.Quests);

        bool giverHere = ready != null && Visitors.IsPresent(session.Design, session.State, session.Quests, ready.giver);
        if (ready != null && !giverHere)
        {
            string name = QuestLogPopup.NpcName(session.Design, ready.giver);
            string when = Visitors.WhenAbout(session.Design, session.State, ready.giver);
            _resultsNote.text += $"\n<color=#{ColorUtility.ToHtmlStringRGB(GameUi.InkSoft)}>" +
                                 (when.Length > 0 ? $"You can hand in {ready.title} to {name}{when}." : $"{name} isn't here today. {ready.title} can wait until they visit.") +
                                 "</color>";
        }

        if (giverHere)
        {
            string giver = QuestLogPopup.NpcName(session.Design, ready.giver);
            SetNext(_nextMain, $"Go and see {giver}", ref _mainAction, Close);
            SetNext(_nextOther, canGoAgain ? againLabel : null, ref _otherAction, again);
        }
        else if (canGoAgain)
        {
            SetNext(_nextMain, againLabel, ref _mainAction, again);
            SetNext(_nextOther, "Back to the cafe", ref _otherAction, Close);
        }
        else
        {
            string done = session.State.isNight ? "Back to bed" : "Close up for the day";
            SetNext(_nextMain, done, ref _mainAction, Close);
            SetNext(_nextOther, null, ref _otherAction, null);
        }
    }

    private void SetNext(Button button, string label, ref System.Action action, System.Action onClick)
    {
        button.gameObject.SetActive(label != null);
        if (label == null) return;
        SetLabel(button, label);
        action = onClick;
    }

    /// <summary>Finds fall into the basket one at a time. They're already in the bag, so skipping this loses nothing.</summary>
    private IEnumerator DropFinds(List<ItemCard> cards)
    {
        Clear(_pile);
        var dropping = new List<RectTransform>();
        var resting = new List<Vector2>();
        // Too many for one row: the first half go in behind, so they're covered by the rest.
        int backRow = cards.Count > _findsInARow ? cards.Count / 2 : 0;
        for (int i = 0; i < cards.Count; i++)
        {
            ItemCardView view = Instantiate(_foundTemplate, _pile);
            view.gameObject.SetActive(true);
            view.Fill(cards[i]);
            var rect = (RectTransform)view.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-_findTilt, _findTilt));
            bool back = i < backRow;
            float x = RowPosition(back ? i : i - backRow, back ? backRow : cards.Count - backRow, rect.rect.width);
            resting.Add(new Vector2(x, (back ? _backRowRise : 0f) + Random.Range(-_findJitter, _findJitter)));
            rect.anchoredPosition = resting[i] + new Vector2(0f, DropHeight * 2f);
            dropping.Add(rect);
        }

        float end = cards.Count * DropGap + DropSeconds;
        for (float t = 0f; t < end; t += Time.unscaledDeltaTime)
        {
            for (int i = 0; i < dropping.Count; i++)
            {
                float k = Mathf.Clamp01((t - i * DropGap) / DropSeconds);
                float fall = 1f - k;
                float bounce = Mathf.Sin(k * Mathf.PI) * 0.06f * (1f - k);
                dropping[i].anchoredPosition = resting[i] + new Vector2(0f, (k <= 0f ? 2f : fall * fall + bounce) * DropHeight);
            }
            yield return null;
        }
        for (int i = 0; i < dropping.Count; i++) dropping[i].anchoredPosition = resting[i];
    }

    /// <summary>Where a find sits along its row, centred in the pile: side by side, overlapping if the pile's too narrow for that.</summary>
    private float RowPosition(int index, int count, float findWidth)
    {
        if (count <= 1) return 0f;
        float step = Mathf.Min(findWidth * 0.85f, (_pile.rect.width - findWidth) / (count - 1));
        return (index - (count - 1) * 0.5f) * step;
    }

    // ----- Small helpers -----

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

    private Button AddChoice(RectTransform row, string label, bool selected, UnityEngine.Events.UnityAction onClick)
    {
        Button choice = Instantiate(_choiceTemplate, row);
        choice.gameObject.SetActive(true);
        choice.targetGraphic.color = selected ? _chosenColor : _choiceColor;
        Transform labelObject = choice.transform.Find("Text");
        if (labelObject && labelObject.TryGetComponent(out TextMeshProUGUI text))
        {
            text.text = label;
            text.color = selected ? _chosenTextColor : _choiceTextColor;
        }
        choice.onClick.AddListener(onClick);
        return choice;
    }

    private static void SetNudge(Button choice, string text)
    {
        Transform nudge = choice.transform.Find("NudgeTag");
        if (!nudge) return;
        nudge.gameObject.SetActive(true);
        nudge.GetComponentInChildren<TextMeshProUGUI>().text = text;
    }

    private static void SetLabel(Button button, string label)
    {
        Transform labelObject = button.transform.Find("Text");
        if (labelObject && labelObject.TryGetComponent(out TextMeshProUGUI text)) text.text = label;
    }

    private static void Clear(RectTransform rect)
    {
        for (int i = rect.childCount - 1; i >= 0; i--)
        {
            // Off the layout now, rather than at the end of the frame.
            GameObject child = rect.GetChild(i).gameObject;
            child.SetActive(false);
            child.transform.SetParent(null);
            Destroy(child);
        }
    }

    // ----- The default layout -----

    /// <summary>The default layout, closed. Tools > Bush > Rebuild Forage Screen Prefab saves this as the prefab.</summary>
    public static ForagePopup BuildDefault(Transform parent)
    {
        RectTransform root = GameUi.NewRect("ForagePopup", parent);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var popup = root.gameObject.AddComponent<ForagePopup>();
        popup.Build(root);
        return popup;
    }

    /// <summary>The trip's step, for the prefab tool.</summary>
    public GameObject OutStep => _out.gameObject;

    /// <summary>For the prefab tool: replaces just the trip's step, leaving the other two as they are.</summary>
    public void RebuildOutStep()
    {
        for (int i = _out.childCount - 1; i >= 0; i--) DestroyImmediate(_out.GetChild(i).gameObject);
        BuildOut();
    }

    /// <summary>For the prefab tool: the backdrop, the gecko and the bushes, from the project's art.</summary>
    public void UseArt(Sprite backdrop, Sprite gecko, Sprite bush)
    {
        Image background = transform.Find("Backdrop").GetComponent<Image>();
        background.sprite = backdrop;
        background.color = Color.white;
        foreach (Image image in _out.GetComponentsInChildren<Image>(true))
        {
            if (image.name == "Painting")
            {
                image.sprite = backdrop;
                image.color = Color.white;
            }
            else if (image.name == "Foliage")
            {
                image.sprite = bush; // keeps its tint: these are the shaded layers behind
            }
        }
        foreach (RectTransform rect in _bushes)
        {
            Image image = rect.GetComponent<Image>();
            image.sprite = bush;
            image.color = Color.white;
        }
        foreach (Image image in GetComponentsInChildren<Image>(true))
        {
            if (image.name != "Gecko") continue;
            image.sprite = gecko;
            image.color = Color.white;
        }
    }

    private void Build(RectTransform root)
    {
        // The bush fills the screen, and blocks clicks to the game behind it.
        RectTransform backdrop = GameUi.NewRect("Backdrop", root);
        GameUi.Stretch(backdrop);
        var backdropImage = backdrop.gameObject.AddComponent<Image>();
        backdropImage.color = new Color(0.25f, 0.29f, 0.2f);
        var fit = backdrop.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = 5f / 3f;

        _setup = Step(root, "Setup");
        _out = Step(root, "Out");
        _results = Step(root, "Results");
        BuildSetup();
        BuildOut();
        BuildResults();
        SetStep(_setup); // so the prefab opens on the first step; turn the others on to edit them
    }

    private void BuildSetup()
    {
        // The sheet: only as tall as what's in it, so sections that are hidden leave no hole.
        RectTransform sheet = Column(_setup, "Sheet", 26f, new RectOffset(52, 52, 40, 44), GameUi.Panel2);
        GameUi.Place(sheet, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(56f, 0f), new Vector2(1130f, 0f));
        sheet.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Text(sheet, "What are we after?", 84f, GameUi.Ink, "Title");

        _focusRow = GameUi.NewRect("Focus", sheet);
        var grid = _focusRow.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(334f, 100f);
        grid.spacing = new Vector2(14f, 24f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        RectTransform finds = Column(sheet, "Finds", 8f, null, null);
        _previewLabel = Text(finds, "You'll find more of these:", 31f, GameUi.Ink, "PreviewLabel");
        RectTransform previewArea = GameUi.NewRect("Preview", finds);
        previewArea.gameObject.AddComponent<LayoutElement>().preferredHeight = 180f;
        _preview = ItemGridView.Create(previewArea, true, new Vector2(184f, 170f), true);
        _preview.MakeCardsFlat();
        GameUi.AddPanel(_preview.GetComponent<RectTransform>(), GameUi.CardBack).raycastTarget = false;
        _previewNote = Text(finds, "And maybe something rare.", 27f, GameUi.InkSoft, "PreviewNote");

        RectTransform companions = Column(sheet, "Companions", 8f, null, null);
        _companionSection = companions.gameObject;
        _companionLabel = Text(companions, "Companions", 29f, GameUi.InkSoft, "CompanionLabel");
        _companionRow = ChoiceRow(companions);

        RectTransform perks = Column(sheet, "Perks", 8f, null, null);
        _perkSection = perks.gameObject;
        _perkLabel = Text(perks, "Perks", 29f, GameUi.InkSoft, "PerkLabel");
        _perkRow = ChoiceRow(perks);

        // Beside it: the gecko at the edge of the bush, ready to go.
        RectTransform side = Column(_setup, "Side", 20f, null, null);
        GameUi.Place(side, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50f, 40f), new Vector2(640f, 0f));
        side.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerCenter;
        side.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
        side.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform crew = GameUi.NewRect("Crew", side);
        HorizontalLayoutGroup crewRow = GameUi.AddLayout<HorizontalLayoutGroup>(crew, 8f);
        crewRow.childAlignment = TextAnchor.LowerCenter;
        Art(crew, "Gecko", new Vector2(310f, 390f));
        _companionArt = Art(crew, "Companion", new Vector2(200f, 240f));
        _companionArt.gameObject.SetActive(false);

        _setupPips = Pips(side);
        _go = GameUi.AddButton(side, "Head out", null, new Vector2(400f, 120f), GameUi.Apron, GameUi.Cream, 44f);

        RectTransform status = GameUi.NewRect("Status", side);
        GameUi.AddPanel(status, GameUi.PanelDark).raycastTarget = false;
        status.gameObject.AddComponent<LayoutElement>().preferredWidth = 620f;
        HorizontalLayoutGroup statusRow = GameUi.AddLayout<HorizontalLayoutGroup>(status, 0f, new RectOffset(18, 18, 10, 10));
        statusRow.childForceExpandWidth = true;
        _status = GameUi.AddText(status, "", 27f, GameUi.Cream, TextAlignmentOptions.Center, "StatusText");

        _close = GameUi.AddButton(_setup, "Back", null, new Vector2(170f, 80f), GameUi.PanelDark, GameUi.Cream, 30f);
        GameUi.Place((RectTransform)_close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(170f, 80f));

        // What's copied: a choice, with its note hidden, and a trip marker.
        _choiceTemplate = GameUi.AddButton(_setup, "Choice", null, new Vector2(240f, 84f), Color.white, Color.white, 28f);
        _choiceTemplate.gameObject.name = "ChoiceTemplate";
        RectTransform nudge = GameUi.NewRect("NudgeTag", _choiceTemplate.transform);
        GameUi.Place(nudge, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(330f, 42f));
        nudge.localRotation = Quaternion.Euler(0f, 0f, 2f);
        GameUi.AddPanel(nudge, new Color(0.89f, 0.75f, 0.29f)).raycastTarget = false;
        GameUi.AddText(nudge, "", 26f, GameUi.Ink, TextAlignmentOptions.Center, "Nudge");
        nudge.gameObject.SetActive(false);
        _choiceTemplate.gameObject.SetActive(false);

        RectTransform pip = GameUi.NewRect("PipTemplate", _setup);
        _pipTemplate = pip.gameObject.AddComponent<Image>();
        _pipTemplate.sprite = GeneratedSprites.Circle;
        _pipTemplate.color = new Color(0.89f, 0.75f, 0.29f);
        _pipTemplate.raycastTarget = false;
        var pipSize = pip.gameObject.AddComponent<LayoutElement>();
        pipSize.preferredWidth = pipSize.preferredHeight = 46f;
        pip.gameObject.SetActive(false);
    }

    private void BuildOut()
    {
        // A place of its own, from what's already painted: the misty green and the clear ground of the
        // backdrop, cropped in, with shaded layers of the Bush art hiding the join.
        Crop(_out, "Sky", new Rect(0f, 0.36f, 1f, 0.64f), new Rect(0.71f, 0.8f, 0.29f, 0.17f), new Color(0.3f, 0.38f, 0.28f));
        Crop(_out, "Ground", new Rect(0f, 0f, 1f, 0.38f), new Rect(0.05f, 0.17f, 0.65f, 0.23f), new Color(0.36f, 0.27f, 0.2f));

        _scenery = GameUi.NewRect("Scenery", _out);
        GameUi.Stretch(_scenery);
        FoliageRow(_scenery, 11, 230f, 300f, 340f, new Color(0.42f, 0.5f, 0.4f));
        FoliageRow(_scenery, 9, 300f, 230f, 460f, new Color(0.62f, 0.68f, 0.56f));

        _outText = GameUi.AddText(_out, "Out foraging...", 92f, GameUi.Cream, TextAlignmentOptions.Top, "OutText");
        _outText.rectTransform.offsetMax = new Vector2(0f, -44f);
        _peekNote = GameUi.AddText(_out, "", 38f, GameUi.Cream, TextAlignmentOptions.Top, "PeekNote");
        _peekNote.rectTransform.offsetMax = new Vector2(0f, -150f);

        // Three bushes to watch; tap the one that rustles.
        _bushes = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform bushRect = GameUi.NewRect($"Bush{i}", _out);
            GameUi.Place(bushRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((i - 1) * 560f, 90f), new Vector2(440f, 440f));
            Image bushImage = bushRect.gameObject.AddComponent<Image>();
            bushImage.sprite = GeneratedSprites.Circle;
            bushImage.color = new Color(0.33f + i * 0.04f, 0.52f, 0.28f);
            bushImage.preserveAspect = true;
            bushRect.gameObject.AddComponent<Button>().targetGraphic = bushImage;
            _bushes[i] = bushRect;
        }

        // In front, in the gap between the first two bushes.
        RectTransform gecko = GameUi.NewRect("Gecko", _out);
        GameUi.Place(gecko, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-280f, 24f), new Vector2(250f, 310f));
        Image geckoImage = gecko.gameObject.AddComponent<Image>();
        geckoImage.preserveAspect = true;
        geckoImage.raycastTarget = false; // never in the way of a bush
        geckoImage.color = new Color(0.61f, 0.61f, 0.3f);
        _gecko = gecko;

        _skip = GameUi.AddButton(_out, "Skip", null, new Vector2(170f, 80f), GameUi.PanelDark, GameUi.Cream, 30f);
        GameUi.Place((RectTransform)_skip.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 32f), new Vector2(170f, 80f));
    }

    /// <summary>Shows one part of a painting, given as fractions of it, in one part of the screen.</summary>
    private static void Crop(RectTransform parent, string name, Rect onScreen, Rect ofPainting, Color fallback)
    {
        RectTransform window = GameUi.NewRect(name, parent);
        window.anchorMin = onScreen.min;
        window.anchorMax = onScreen.max;
        window.offsetMin = window.offsetMax = Vector2.zero;
        window.gameObject.AddComponent<RectMask2D>();

        RectTransform painting = GameUi.NewRect("Painting", window);
        painting.anchorMin = new Vector2(-ofPainting.xMin / ofPainting.width, -ofPainting.yMin / ofPainting.height);
        painting.anchorMax = new Vector2((1f - ofPainting.xMin) / ofPainting.width, (1f - ofPainting.yMin) / ofPainting.height);
        painting.offsetMin = painting.offsetMax = Vector2.zero;
        Image image = painting.gameObject.AddComponent<Image>();
        image.color = fallback;
        image.raycastTarget = false;
    }

    /// <summary>A row of shaded bushes behind the ones you can tap, running off both sides of the screen.</summary>
    private static void FoliageRow(RectTransform parent, int count, float gap, float height, float size, Color tint)
    {
        for (int i = 0; i < count; i++)
        {
            RectTransform bush = GameUi.NewRect("Foliage", parent);
            // Uneven, so it doesn't read as a neat row of the same bush.
            float wobbleX = (i * 37 % 11 - 5) * 12f;
            float wobbleY = (i * 53 % 7 - 3) * 12f;
            float scale = 1f + (i * 29 % 5 - 2) * 0.07f;
            GameUi.Place(bush, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2((i - (count - 1) * 0.5f) * gap + wobbleX, height + wobbleY), new Vector2(size * scale, size * scale));
            Image image = bush.gameObject.AddComponent<Image>();
            image.sprite = GeneratedSprites.Circle;
            image.color = tint;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
    }

    private void BuildResults()
    {
        // The basket: finds land in the pile above its front, which draws over them.
        RectTransform shadow = GameUi.NewRect("BasketShadow", _results);
        GameUi.Place(shadow, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(530f, 5f), new Vector2(720f, 70f));
        Image shadowImage = shadow.gameObject.AddComponent<Image>();
        shadowImage.sprite = GeneratedSprites.Circle;
        shadowImage.color = new Color(0f, 0f, 0f, 0.28f);
        shadowImage.raycastTarget = false;

        _pile = GameUi.NewRect("Pile", _results);
        GameUi.Place(_pile, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(530f, 172f), new Vector2(570f, 300f));

        RectTransform basket = GameUi.NewRect("BasketFront", _results);
        GameUi.Place(basket, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(530f, 36f), new Vector2(760f, 200f));
        GameUi.AddPanel(basket, GameUi.Bark).raycastTarget = false;

        _foundTemplate = ItemCardView.Build(_results, "FoundTemplate");
        _foundTemplate.MakeFlat();
        _foundTemplate.MakeIconOnly();
        ((RectTransform)_foundTemplate.transform).sizeDelta = new Vector2(210f, 210f);
        _foundTemplate.gameObject.SetActive(false);

        // Beside it: what happened, and what to do next.
        RectTransform side = Column(_results, "Side", 20f, null, null);
        GameUi.Place(side, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50f, 40f), new Vector2(800f, 0f));
        side.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerCenter;
        side.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform summary = Column(side, "Summary", 10f, new RectOffset(34, 34, 22, 26), GameUi.Panel2);
        _resultsTitle = Text(summary, "5 finds, in your bag", 62f, GameUi.Ink, "Title");
        _resultsNote = Text(summary, "", 30f, GameUi.Ink, "ResultsNote");

        RectTransform quest = Column(side, "QuestCard", 10f, new RectOffset(34, 34, 18, 24), GameUi.Panel2);
        _questCard = quest.gameObject;
        _questLabel = Text(quest, "", 28f, GameUi.InkSoft, "QuestLabel");
        RectTransform bar = GameUi.NewRect("Bar", quest);
        bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;
        GameUi.AddPanel(bar, new Color(0f, 0f, 0f, 0.15f)).raycastTarget = false;
        RectTransform fill = GameUi.NewRect("Fill", bar);
        GameUi.Stretch(fill);
        _questBar = fill.gameObject.AddComponent<Image>();
        _questBar.sprite = GeneratedSprites.Square;
        _questBar.color = GameUi.Moss;
        _questBar.type = Image.Type.Filled;
        _questBar.fillMethod = Image.FillMethod.Horizontal;
        _questBar.raycastTarget = false;

        _resultPips = Pips(side);
        _nextMain = GameUi.AddButton(side, "Go again", null, new Vector2(800f, 120f), GameUi.Apron, GameUi.Cream, 44f);
        _nextOther = GameUi.AddButton(side, "Back to the cafe", null, new Vector2(800f, 84f), GameUi.PanelDark, GameUi.Cream, 30f);
    }

    private static RectTransform Step(RectTransform parent, string name)
    {
        RectTransform step = GameUi.NewRect(name, parent);
        GameUi.Stretch(step);
        return step;
    }

    /// <summary>A stack that fills its width, each thing as tall as it needs. With a colour, it's a panel.</summary>
    private static RectTransform Column(RectTransform parent, string name, float spacing, RectOffset padding, Color? panel)
    {
        RectTransform column = GameUi.NewRect(name, parent);
        if (panel.HasValue) GameUi.AddPanel(column, panel.Value).raycastTarget = false;
        VerticalLayoutGroup layout = GameUi.AddLayout<VerticalLayoutGroup>(column, spacing, padding);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childForceExpandWidth = true;
        return column;
    }

    private static TextMeshProUGUI Text(RectTransform parent, string text, float size, Color color, string name) =>
        GameUi.AddText(parent, text, size, color, TextAlignmentOptions.Left, name);

    private static RectTransform ChoiceRow(RectTransform parent)
    {
        RectTransform row = GameUi.NewRect("Row", parent);
        HorizontalLayoutGroup layout = GameUi.AddLayout<HorizontalLayoutGroup>(row, 12f);
        layout.childAlignment = TextAnchor.MiddleLeft;
        return row;
    }

    private static RectTransform Pips(RectTransform parent)
    {
        RectTransform row = GameUi.NewRect("Pips", parent);
        HorizontalLayoutGroup layout = GameUi.AddLayout<HorizontalLayoutGroup>(row, 14f);
        layout.childAlignment = TextAnchor.MiddleCenter;
        return row;
    }

    private static Image Art(RectTransform parent, string name, Vector2 size)
    {
        RectTransform rect = GameUi.NewRect(name, parent);
        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;
        Image image = rect.gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(0.61f, 0.61f, 0.3f);
        return image;
    }
}
