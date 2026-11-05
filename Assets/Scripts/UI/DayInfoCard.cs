using System.Collections.Generic;
using Bush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What tapping the day dial shows: the part of the day, the trips left, today's special event and who's
/// around. Tap anywhere to put it away. Part of the HUD prefab (<see cref="HudView"/>).
/// </summary>
public class DayInfoCard : MonoBehaviour
{
    [SerializeField] private Button _dismiss;
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _next;
    [SerializeField] private RectTransform _pips;
    [SerializeField] private Image _pipTemplate;
    [SerializeField] private float _usedPipAlpha = 0.3f;
    [SerializeField] private GameObject _eventTape;
    [SerializeField] private TextMeshProUGUI _eventName;
    [SerializeField] private TextMeshProUGUI _eventText;
    [SerializeField] private TextMeshProUGUI _visitors;

    private void Awake() => _dismiss.onClick.AddListener(Hide);

    public void Toggle()
    {
        if (gameObject.activeSelf) Hide();
        else Show();
    }

    public void Hide() => gameObject.SetActive(false);

    private void Show()
    {
        GameSession session = GameSession.Current;
        if (session == null) return;
        gameObject.SetActive(true);
        GameState state = session.State;
        DesignData design = session.Design;

        string part = DayDial.PartName(state, design.rules, Mathf.Max(1, design.rules.TripsPerDay(state)));
        _title.text = string.IsNullOrEmpty(part) ? $"Day {state.day}" : $"Day {state.day}, {part.ToLowerInvariant()}";
        _next.text = DayDial.Hint(session);

        GameUi.ClearExcept(_pips, _pipTemplate.transform);
        int total = state.isNight ? design.rules.nightTrips : design.rules.TripsPerDay(state);
        int left = ForagingService.TripsLeftToday(design, state);
        _pips.gameObject.SetActive(total > 0 && left >= 0);
        for (int i = 0; i < total; i++)
        {
            Image pip = Instantiate(_pipTemplate, _pips);
            pip.gameObject.SetActive(true);
            Color color = pip.color;
            color.a = i < left ? 1f : _usedPipAlpha;
            pip.color = color;
        }

        DayEventData today = DayEvents.Today(design, state);
        _eventTape.SetActive(today != null);
        if (today != null) _eventName.text = today.name;
        _eventText.text = today == null ? "Nothing special today."
            : string.IsNullOrEmpty(today.line) ? today.effect : $"{today.line}\n{today.effect}";

        var names = new List<string>();
        foreach (string id in Visitors.Present(design, state, session.Quests))
            names.Add(QuestLogPopup.NpcName(design, id));
        _visitors.text = names.Count > 0 ? $"Here now: {string.Join(", ", names)}." : "Nobody's dropped by yet.";
    }

    public static DayInfoCard Build(Transform parent)
    {
        RectTransform root = GameUi.NewRect("DayInfo", parent);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // the dial opens it
        var card = root.gameObject.AddComponent<DayInfoCard>();

        // Clear, over the whole screen: a tap anywhere puts it away, and doesn't walk the gecko.
        Image catcher = GameUi.AddPanel(root, Color.clear, false);
        card._dismiss = root.gameObject.AddComponent<Button>();
        card._dismiss.targetGraphic = catcher;
        card._dismiss.transition = Selectable.Transition.None;

        RectTransform paper = GameUi.NewRect("PaperDayInfo", root);
        GameUi.Place(paper, Vector2.one, Vector2.one, new Vector2(-12f, -176f), new Vector2(680f, 0f));
        GameUi.AddPanel(paper, GameUi.Panel2).raycastTarget = false;
        VerticalLayoutGroup column = GameUi.AddLayout<VerticalLayoutGroup>(paper, 8f, new RectOffset(60, 60, 62, 58));
        column.childForceExpandWidth = true;
        column.childAlignment = TextAnchor.UpperLeft;
        paper.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        card._title = GameUi.AddText(paper, "", 46f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Title");
        card._next = GameUi.AddText(paper, "", 28f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Next");

        card._pips = GameUi.NewRect("Pips", paper);
        card._pips.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;
        GameUi.AddLayout<HorizontalLayoutGroup>(card._pips, 8f).childAlignment = TextAnchor.MiddleLeft;
        RectTransform pip = GameUi.NewRect("PipTemplate", card._pips);
        card._pipTemplate = pip.gameObject.AddComponent<Image>();
        card._pipTemplate.sprite = GeneratedSprites.Circle;
        card._pipTemplate.color = new Color(0.89f, 0.75f, 0.29f);
        card._pipTemplate.preserveAspect = true;
        card._pipTemplate.raycastTarget = false;
        var pipSize = pip.gameObject.AddComponent<LayoutElement>();
        pipSize.preferredWidth = pipSize.preferredHeight = 44f;
        pip.gameObject.SetActive(false);

        RectTransform tape = GameUi.NewRect("EventTape", paper);
        tape.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
        GameUi.AddPanel(tape, GameUi.Tape).raycastTarget = false;
        card._eventTape = tape.gameObject;
        card._eventName = GameUi.AddText(tape, "", 26f, GameUi.Ink, TextAlignmentOptions.Center, "Nudge");

        card._eventText = GameUi.AddText(paper, "", 27f, GameUi.Ink, TextAlignmentOptions.TopLeft, "EventText");
        card._visitors = GameUi.AddText(paper, "", 26f, GameUi.InkSoft, TextAlignmentOptions.TopLeft, "Visitors");
        return card;
    }
}
