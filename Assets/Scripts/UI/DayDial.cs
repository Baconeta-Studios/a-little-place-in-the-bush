using Bush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The HUD's day dial: a ring split into the parts of the day (one per forage trip, then evening,
/// then night once it's unlocked), the day number in the middle, and a hand that turns to the
/// current part. Parts already passed are dimmed. Tap it for what's next. Part of the HUD prefab
/// (<see cref="HudView"/>): the parts are made at run time from <c>Segment Sprite</c>, one per part of the day.
/// </summary>
public class DayDial : MonoBehaviour
{
    private static readonly Color[] DayColors =
    {
        new(0.98f, 0.78f, 0.46f), new(0.94f, 0.62f, 0.15f), new(0.85f, 0.45f, 0.19f), new(0.72f, 0.36f, 0.2f)
    };
    private static readonly Color EveningColor = new(0.6f, 0.4f, 0.55f);
    private static readonly Color NightColor = new(0.24f, 0.2f, 0.54f);

    [SerializeField] private RectTransform _ring;
    [SerializeField] private Button _ringButton;
    [SerializeField] private RectTransform _hand;
    [SerializeField] private TextMeshProUGUI _day;
    [SerializeField] private TextMeshProUGUI _label;
    [Tooltip("What each part of the ring is cut from: a white circle, filled radially and tinted.")]
    [SerializeField] private Sprite _segmentSprite;
    [Tooltip("An outline on the label, added when the game runs: it needs its own material, which a prefab can't keep. 0 for none.")]
    [SerializeField, Min(0f)] private float _labelOutlineWidth = 0.2f;
    [SerializeField] private Color32 _labelOutlineColor = new(40, 30, 20, 255);
    private Image[] _segments = new Image[0];
    private string _layout;
    private float _handAngle;
    private float _nextRefresh;
    private int _current;

    public static DayDial Create(Transform parent)
    {
        RectTransform root = GameUi.NewRect("DayDial", parent);
        root.gameObject.SetActive(false); // so Awake runs once it's built
        var dial = root.gameObject.AddComponent<DayDial>();
        dial.Build(root);
        root.gameObject.SetActive(true);
        return dial;
    }

    private void Awake()
    {
        if (_ringButton) _ringButton.onClick.AddListener(ShowHint);
    }

    private void Start()
    {
        if (_labelOutlineWidth <= 0f) return;
        _label.outlineWidth = _labelOutlineWidth;
        _label.outlineColor = _labelOutlineColor;
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextRefresh && !DayTransition.Holding)
        {
            _nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }
        // Turn the hand smoothly to the middle of the current part.
        float target = -(_current + 0.5f) * 360f / Mathf.Max(1, _segments.Length);
        _handAngle = Mathf.LerpAngle(_handAngle, target, Mathf.Clamp01(Time.unscaledDeltaTime * 4f));
        _hand.localRotation = Quaternion.Euler(0f, 0f, _handAngle);
    }

    private void Refresh()
    {
        GameSession session = GameSession.Current;
        if (session == null) return;
        GameState state = session.State;
        DayRules rules = session.Design.rules;

        int trips = Mathf.Max(1, rules.TripsPerDay(state));
        bool night = state.nightUnlocked;
        string layout = $"{trips}:{night}";
        if (layout != _layout) Rebuild(trips, night);
        _layout = layout;

        _current = state.isNight ? trips + 1 : rules.IsEvening(state) && rules.TripsPerDay(state) > 0 ? trips : rules.PartsPassed(state);
        for (int i = 0; i < _segments.Length; i++)
        {
            Color c = _segments[i].color;
            c.a = i < _current ? 0.35f : 1f;
            _segments[i].color = c;
        }

        _day.text = $"<size=55%>Day</size>\n{state.day}";
        DayEventData today = DayEvents.Today(session.Design, state);
        _label.text = PartName(state, rules, trips) + (today != null ? $"\n<size=80%><color=#{ColorUtility.ToHtmlStringRGB(new Color(1f, 0.85f, 0.45f))}>{today.name}</color></size>" : "");
    }

    private static string PartName(GameState state, DayRules rules, int trips)
    {
        if (state.isNight) return "Night";
        if (rules.TripsPerDay(state) <= 0) return "";
        if (rules.IsEvening(state)) return "Evening";
        int part = rules.PartsPassed(state);
        return trips == 3 ? new[] { "Morning", "Midday", "Afternoon" }[part] : part == 0 ? "Morning" : "Afternoon";
    }

    private void Rebuild(int trips, bool night)
    {
        foreach (Image segment in _segments) if (segment) Destroy(segment.gameObject);
        int count = trips + 1 + (night ? 1 : 0);
        _segments = new Image[count];
        for (int i = 0; i < count; i++)
        {
            RectTransform rect = GameUi.NewRect($"Part{i}", _ring);
            GameUi.Stretch(rect);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = _segmentSprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = 1f / count - 0.004f; // a hairline gap between parts
            image.raycastTarget = false;
            image.color = i < trips ? DayColors[Mathf.Min(i * DayColors.Length / trips, DayColors.Length - 1)] : i == trips ? EveningColor : NightColor;
            rect.localRotation = Quaternion.Euler(0f, 0f, -i * 360f / count);
            _segments[i] = image;
        }
        _hand.SetAsLastSibling();
        _ring.Find("Middle")?.SetAsLastSibling();
    }

    private void ShowHint()
    {
        GameSession session = GameSession.Current;
        if (session == null) return;
        GameState state = session.State;
        int left = ForagingService.TripsLeftToday(session.Design, state);
        string hint = state.isNight
            ? left > 0 ? "Night. One forage trip tonight, then bed." : "Night. Time for bed."
            : session.Design.rules.IsEvening(state) ? "Evening. Close the cafe for the day."
            : $"{left} forage trip{(left == 1 ? "" : "s")} left today.";
        DayEventData today = DayEvents.Today(session.Design, state);
        if (today != null) hint += $"\n{today.name}: {today.effect}";
        Debug.Log($"[Day] {hint}");
        _label.text = hint;
        _nextRefresh = Time.unscaledTime + 3f; // leave the hint up for a moment
    }

    private void Build(RectTransform root)
    {
        // The ring is a button: tap for what's next.
        _ring = GameUi.NewRect("Ring", root);
        GameUi.Place(_ring, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(140f, 140f));
        var back = _ring.gameObject.AddComponent<Image>();
        back.sprite = GeneratedSprites.Circle;
        back.color = GameUi.PanelDark;
        _ringButton = _ring.gameObject.AddComponent<Button>();
        _ringButton.targetGraphic = back;
        _segmentSprite = GeneratedSprites.Circle;

        _hand = GameUi.NewRect("Hand", _ring);
        GameUi.Place(_hand, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(8f, 66f));
        var handImage = _hand.gameObject.AddComponent<Image>();
        handImage.color = GameUi.Cream;
        handImage.raycastTarget = false;

        RectTransform middle = GameUi.NewRect("Middle", _ring);
        GameUi.Place(middle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));
        var middleImage = middle.gameObject.AddComponent<Image>();
        middleImage.sprite = GeneratedSprites.Circle;
        middleImage.color = GameUi.Cream;
        middleImage.raycastTarget = false;
        _day = GameUi.AddText(middle, "", 30f, GameUi.Ink, TextAlignmentOptions.Center, "Day");
        _day.lineSpacing = -8f;

        _label = GameUi.AddText(root, "", 24f, GameUi.Cream, TextAlignmentOptions.Top, "Label");
        _label.rectTransform.anchorMin = new Vector2(0f, 0f);
        _label.rectTransform.anchorMax = new Vector2(1f, 0f);
        _label.rectTransform.pivot = new Vector2(0.5f, 1f);
        _label.rectTransform.offsetMin = new Vector2(-60f, -60f);
        _label.rectTransform.offsetMax = new Vector2(60f, 0f);
    }
}
