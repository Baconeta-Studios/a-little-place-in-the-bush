using Bush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The game's HUD, hidden while building. Top left: coins. Bottom left: Bag (the read-only inventory), Quests,
/// Forage (where ticked), Build (only in areas with a <see cref="BuildArea"/>) and, at night, Go to bed.
/// Top right: the day dial (<see cref="DayDial"/>; tap it for what's on today) and the job note, the main
/// story's current step. Tap the note for the quests, or its x to tuck it away, which is remembered. Tucked away,
/// it peeks out for a moment when the step changes. Also sets up the day and night lighting. Put it on Systems in
/// each area scene.
/// How it looks is in <see cref="HudView"/>; this is what it does.
/// </summary>
public class GameHud : MonoBehaviour
{
    [Tooltip("Show a Forage button in this area. For now it's in the cafe; later it can move to the garden.")]
    [SerializeField] private bool forageButton;

    private HudView _view;
    private InventoryPopup _bag;
    private const string TuckedKey = "hud.jobTucked";
    private const float PeekSeconds = 4f;

    private float _nextTrackerRefresh;
    private bool _tucked;
    private float _peekUntil;
    private string _lastNote;

    private void Start()
    {
        Transform safeRoot = GameUi.Current.SafeRoot;
        var prefab = Resources.Load<HudView>(HudView.ResourcePath);
        _view = prefab ? Instantiate(prefab, safeRoot) : HudView.Build(safeRoot);

        Wire(_view.bag, OpenBag);
        Wire(_view.quests, QuestLogPopup.Show);
        Wire(_view.forage, ForagePopup.Show);
        Wire(_view.build, EnterBuild);
        Wire(_view.bed, DayFlow.GoToBed);
        Wire(_view.trackerButton, QuestLogPopup.Show);
        Wire(_view.trackerTuck, TuckNote);
        Wire(_view.trackerTab, UntuckNote);
        _tucked = PlayerPrefs.GetInt(TuckedKey, 0) == 1;
        if (_view.dial && _view.dayInfo) _view.dial.Tapped += _view.dayInfo.Toggle;
        if (_view.forage) _view.forage.gameObject.SetActive(forageButton);
        if (_view.build) _view.build.gameObject.SetActive(false);
        if (_view.bed) _view.bed.gameObject.SetActive(false);
        DayNightLighting.Ensure();

        BuildMode.ActiveChanged += OnBuildModeChanged;
        GameSession.Ready += OnSessionReady;
        OnSessionReady();
    }

    private static void Wire(Button button, UnityEngine.Events.UnityAction onClick)
    {
        if (button) button.onClick.AddListener(onClick);
    }

    private void OnDestroy()
    {
        BuildMode.ActiveChanged -= OnBuildModeChanged;
        GameSession.Ready -= OnSessionReady;
        if (GameSession.Current != null) GameSession.Current.Events.CurrencyChanged -= OnCurrencyChanged;
        if (_view) Destroy(_view.gameObject);
        if (_bag) Destroy(_bag.gameObject);
    }

    private void Update()
    {
        // Show Build once the area has loaded its placed things.
        BuildArea area = BuildArea.Current;
        bool canBuild = area && area.IsLoaded && !IsBuilding(area);
        if (_view.build && _view.build.gameObject.activeSelf != canBuild) _view.build.gameObject.SetActive(canBuild);
        bool night = GameSession.Current != null && GameSession.Current.State.isNight && !DayTransition.Holding;
        if (_view.bed && _view.bed.gameObject.activeSelf != night) _view.bed.gameObject.SetActive(night);

        // A few times a second is plenty for the tracker, and simpler than listening to every event.
        if (Time.unscaledTime >= _nextTrackerRefresh)
        {
            _nextTrackerRefresh = Time.unscaledTime + 0.3f;
            RefreshTracker();
        }
    }

    private void OnSessionReady()
    {
        GameSession session = GameSession.Current;
        if (session == null || !_view || !_view.coins) return;
        session.Events.CurrencyChanged -= OnCurrencyChanged;
        session.Events.CurrencyChanged += OnCurrencyChanged;
        OnCurrencyChanged(session.Economy.Currency);
    }

    private void OnCurrencyChanged(int coins) => _view.coins.text = $"{coins} coins";

    /// <summary>The story's current step: what to do next, or who to talk to.</summary>
    private void RefreshTracker()
    {
        GameSession session = GameSession.Current;
        if (session == null || !_view.trackerText || !_view.tracker) return;
        QuestData main = session.Quests.CurrentMainQuest();
        QuestData waiting = main == null ? session.Quests.WaitingMainQuest() : null;

        string note = waiting != null
            ? $"<b>{waiting.title}</b>\n{QuestLogPopup.NpcName(session.Design, waiting.giver)} will have something for you tomorrow."
            : main != null ? $"<b>{main.title}</b>\n{QuestLogPopup.NextStep(session, main)}" : "";
        _view.trackerText.text = note;

        // Tucked away, it still says when something changes.
        if (_lastNote != null && note != _lastNote && note.Length > 0 && _tucked) _peekUntil = Time.unscaledTime + PeekSeconds;
        _lastNote = note;

        bool showing = note.Length > 0 && (!_view.bar || _view.bar.gameObject.activeSelf);
        bool peeking = Time.unscaledTime < _peekUntil;
        _view.tracker.gameObject.SetActive(showing && (!_tucked || peeking));
        if (_view.trackerTab) _view.trackerTab.gameObject.SetActive(showing && _tucked && !peeking);

        bool mainReady = main != null && IsReadyAndHere(session, main);
        if (_view.trackerTabMarker) _view.trackerTabMarker.SetActive(mainReady);
        if (_view.questsMarker) _view.questsMarker.SetActive(mainReady || session.Quests.ActiveSideQuests().Exists(q => IsReadyAndHere(session, q)));
        if (_view.buildMarker) _view.buildMarker.SetActive(BuildPrompt.IsWanted(session.Design, session.Quests, session.Build));
    }

    private static bool IsReadyAndHere(GameSession session, QuestData quest) =>
        session.Quests.GetStatus(quest.id) == QuestStatus.ReadyToClaim && Visitors.IsPresent(session.Design, session.State, session.Quests, quest.giver);

    private void TuckNote()
    {
        _tucked = true;
        _peekUntil = 0f;
        PlayerPrefs.SetInt(TuckedKey, 1);
        RefreshTracker();
    }

    private void UntuckNote()
    {
        _tucked = false;
        PlayerPrefs.SetInt(TuckedKey, 0);
        RefreshTracker();
    }

    private void OnBuildModeChanged(bool building)
    {
        if (_view.bar) _view.bar.gameObject.SetActive(!building);
        if (_view.tracker) _view.tracker.gameObject.SetActive(!building);
        if (_view.trackerTab && building) _view.trackerTab.gameObject.SetActive(false);
        if (_view.dayInfo && building) _view.dayInfo.Hide();
        if (_view.coins) _view.coins.gameObject.SetActive(!building);
        if (_view.dial) _view.dial.gameObject.SetActive(!building);
    }

    private void OpenBag()
    {
        if (!_bag) _bag = InventoryPopup.Create();
        _bag.Open();
    }

    private static void EnterBuild()
    {
        BuildArea area = BuildArea.Current;
        if (area) area.GetComponent<BuildMode>().Enter();
    }

    private static bool IsBuilding(BuildArea area)
    {
        var mode = area.GetComponent<BuildMode>();
        return mode && mode.IsActive;
    }
}
