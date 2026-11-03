using Bush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The game's HUD, hidden while building. Top left: coins, Bag (the read-only inventory), Quests,
/// Forage (where ticked), Build (only in areas with a <see cref="BuildArea"/>) and, at night, Go to bed.
/// Top right: the day dial (<see cref="DayDial"/>) and the quest tracker, the main story's current
/// step; tap it for the quest log. Also sets up the day and night lighting. Put it on Systems in each area scene.
/// How it looks is in <see cref="HudView"/>; this is what it does.
/// </summary>
public class GameHud : MonoBehaviour
{
    [Tooltip("Show a Forage button in this area. For now it's in the cafe; later it can move to the garden.")]
    [SerializeField] private bool forageButton;

    private HudView _view;
    private InventoryPopup _bag;
    private float _nextTrackerRefresh;

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
        _view.tracker.gameObject.SetActive((main != null || waiting != null) && (!_view.bar || _view.bar.gameObject.activeSelf));
        if (waiting != null)
        {
            _view.trackerText.text = $"<b>{waiting.title}</b>\n{QuestLogPopup.NpcName(session.Design, waiting.giver)} will have something for you tomorrow.";
            return;
        }
        if (main == null) return;

        string next;
        QuestStatus status = session.Quests.GetStatus(main.id);
        string giver = QuestLogPopup.NpcName(session.Design, main.giver);
        if (status == QuestStatus.NotStarted) next = $"Talk to {giver}{Visitors.WhenAbout(session.Design, session.State, main.giver)}";
        else if (status == QuestStatus.ReadyToClaim) next = $"Go and see {giver}";
        else
        {
            next = "";
            for (int i = 0; i < main.objectives.Count; i++)
            {
                (int current, int required) = session.Quests.GetProgress(main.id, i);
                if (current >= required) continue;
                next = DialogueBox.ObjectiveLine(session, main, i);
                break;
            }
        }
        _view.trackerText.text = $"<b>{main.title}</b>\n{next}";
    }

    private void OnBuildModeChanged(bool building)
    {
        if (_view.bar) _view.bar.gameObject.SetActive(!building);
        if (_view.tracker) _view.tracker.gameObject.SetActive(!building);
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
