using Bush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The game's HUD, hidden while building. Top left: coins, Bag (the read-only inventory), Quests,
/// Forage (where ticked) and Build (only in areas with a <see cref="BuildArea"/>). Top right: the quest
/// tracker, the main story's current step; tap it for the quest log. Put it on Systems in each area scene.
/// </summary>
public class GameHud : MonoBehaviour
{
    [Tooltip("Show a Forage button in this area. For now it's in the cafe; later it can move to the garden.")]
    [SerializeField] private bool forageButton;

    private RectTransform _bar;
    private Button _buildButton;
    private TextMeshProUGUI _coins;
    private InventoryPopup _bag;
    private RectTransform _tracker;
    private TextMeshProUGUI _trackerText;
    private float _nextTrackerRefresh;

    private void Start()
    {
        GameUi ui = GameUi.Current;
        _bar = GameUi.NewRect("HudBar", ui.SafeRoot);
        GameUi.Place(_bar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(1120f, 110f));
        HorizontalLayoutGroup row = GameUi.AddLayout<HorizontalLayoutGroup>(_bar, 14f);
        row.childAlignment = TextAnchor.MiddleLeft;

        RectTransform coins = GameUi.NewRect("Coins", _bar);
        var coinsLayout = coins.gameObject.AddComponent<LayoutElement>();
        coinsLayout.preferredWidth = 200f;
        coinsLayout.preferredHeight = 90f;
        GameUi.AddPanel(coins, GameUi.PanelDark).raycastTarget = false;
        _coins = GameUi.AddText(coins, "", 32f, GameUi.Cream);

        GameUi.AddButton(_bar, "Bag", OpenBag, new Vector2(180f, 110f), GameUi.Cream, GameUi.Ink);
        GameUi.AddButton(_bar, "Quests", QuestLogPopup.Show, new Vector2(200f, 110f), GameUi.Cream, GameUi.Ink);
        if (forageButton) GameUi.AddButton(_bar, "Forage", ForagePopup.Show, new Vector2(200f, 110f), GameUi.Bark, GameUi.Cream);
        _buildButton = GameUi.AddButton(_bar, "Build", EnterBuild, new Vector2(200f, 110f), GameUi.Moss, GameUi.Cream);
        _buildButton.gameObject.SetActive(false);

        // The quest tracker: a small card, top right. The whole card opens the quest log.
        _tracker = GameUi.NewRect("QuestTracker", ui.SafeRoot);
        GameUi.Place(_tracker, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(560f, 150f));
        Image card = GameUi.AddPanel(_tracker, GameUi.Panel);
        var open = _tracker.gameObject.AddComponent<Button>();
        open.targetGraphic = card;
        open.onClick.AddListener(QuestLogPopup.Show);
        _trackerText = GameUi.AddText(_tracker, "", 28f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Text");
        _trackerText.rectTransform.offsetMin = new Vector2(22f, 12f);
        _trackerText.rectTransform.offsetMax = new Vector2(-22f, -12f);
        _trackerText.overflowMode = TextOverflowModes.Ellipsis;

        BuildMode.ActiveChanged += OnBuildModeChanged;
        GameSession.Ready += OnSessionReady;
        OnSessionReady();
    }

    private void OnDestroy()
    {
        BuildMode.ActiveChanged -= OnBuildModeChanged;
        GameSession.Ready -= OnSessionReady;
        if (GameSession.Current != null) GameSession.Current.Events.CurrencyChanged -= OnCurrencyChanged;
        if (_bar) Destroy(_bar.gameObject);
        if (_bag) Destroy(_bag.gameObject);
        if (_tracker) Destroy(_tracker.gameObject);
    }

    private void Update()
    {
        // Show Build once the area has loaded its placed things.
        BuildArea area = BuildArea.Current;
        bool canBuild = area && area.IsLoaded && !IsBuilding(area);
        if (_buildButton && _buildButton.gameObject.activeSelf != canBuild) _buildButton.gameObject.SetActive(canBuild);

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
        if (session == null || !_coins) return;
        session.Events.CurrencyChanged -= OnCurrencyChanged;
        session.Events.CurrencyChanged += OnCurrencyChanged;
        OnCurrencyChanged(session.Economy.Currency);
    }

    private void OnCurrencyChanged(int coins) => _coins.text = $"{coins} coins";

    /// <summary>The story's current step: what to do next, or who to talk to.</summary>
    private void RefreshTracker()
    {
        GameSession session = GameSession.Current;
        if (session == null || !_trackerText) return;
        QuestData main = session.Quests.CurrentMainQuest();
        _tracker.gameObject.SetActive(main != null && (!_bar || _bar.gameObject.activeSelf));
        if (main == null) return;

        string next;
        QuestStatus status = session.Quests.GetStatus(main.id);
        string giver = QuestLogPopup.NpcName(session.Design, main.giver);
        if (status == QuestStatus.NotStarted) next = $"Talk to {giver}";
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
        _trackerText.text = $"<b>{main.title}</b>\n{next}";
    }

    private void OnBuildModeChanged(bool building)
    {
        if (_bar) _bar.gameObject.SetActive(!building);
        if (_tracker) _tracker.gameObject.SetActive(!building);
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
