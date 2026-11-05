using Bush.Core;
using TMPro;
using UI.Popups;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What the sign by the cafe door says: what kind of cafe you've made (from the styles of what's
/// placed), how cosy it is, and what that cosiness does. See <see cref="CafeStyle"/>. Opened by a
/// <see cref="CafeSign"/>. A template popup, so it pauses the game and Escape closes it.
///
/// How it looks is in Resources/UI/CafeSignPopup.prefab (see docs/UI.md), made from <see cref="BuildDefault"/>,
/// which is also used if there's no prefab.
/// </summary>
public class CafeSignPopup : Popup
{
    public const string ResourcePath = "UI/CafeSignPopup";

    private static CafeSignPopup _instance;

    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _blurb;
    [SerializeField] private RectTransform _pips;
    [SerializeField] private Image _pipTemplate;
    [SerializeField] private float _unreachedPipAlpha = 0.25f;
    [SerializeField] private TextMeshProUGUI _level;
    [SerializeField] private TextMeshProUGUI _next;
    [SerializeField] private TextMeshProUGUI _things;
    [SerializeField] private TextMeshProUGUI _thingsLabel;
    [SerializeField] private TextMeshProUGUI _seats;
    [SerializeField] private TextMeshProUGUI _seatsLabel;
    [SerializeField] private TextMeshProUGUI _visitors;
    [SerializeField] private TextMeshProUGUI _luck;
    [SerializeField] private Button _back;

    public static void Show()
    {
        if (GameSession.Current == null) return;
        if (!_instance) _instance = GameUi.LoadPopup(ResourcePath, BuildDefault);
        _instance.Open();
        _instance.Refresh();
    }

    protected override void Awake()
    {
        base.Awake();
        _back.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Refresh()
    {
        GameSession session = GameSession.Current;
        CafeDescription cafe = CafeStyle.Describe(session.Design, session.State);
        _title.text = cafe.title;
        _blurb.text = cafe.blurb;

        GameUi.ClearExcept(_pips, _pipTemplate.transform);
        int reached = CafeStyle.CosinessStep(cafe.cosiness);
        for (int i = 0; i < CafeStyle.CosinessSteps; i++)
        {
            Image pip = Instantiate(_pipTemplate, _pips);
            pip.gameObject.SetActive(true);
            Color color = pip.color;
            color.a = i <= reached ? 1f : _unreachedPipAlpha;
            pip.color = color;
        }
        _level.text = cafe.level;
        _next.text = CafeStyle.TryNextLevel(cafe.cosiness, out string nextLevel, out int more)
            ? $"Cosiness {cafe.cosiness}. {more} more for {nextLevel}."
            : $"Cosiness {cafe.cosiness}. It doesn't get cosier than this.";

        _things.text = cafe.things.ToString();
        _thingsLabel.text = cafe.things == 1 ? "thing out" : "things out";
        _seats.text = cafe.seats.ToString();
        _seatsLabel.text = cafe.seats == 1 ? "seat" : "seats";
        _visitors.text = $"+{Mathf.RoundToInt((float)(CafeStyle.VisitorBoost(session.Design, session.State) - 1) * 100f)}%";
        _luck.text = $"{Mathf.RoundToInt((float)CafeStyle.LuckyDayChance(session.Design, session.State) * 100f)}%";
    }

    // ----- The default layout -----

    public static CafeSignPopup BuildDefault(Transform parent)
    {
        RectTransform root = GameUi.NewRect("CafeSignPopup", parent);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var popup = root.gameObject.AddComponent<CafeSignPopup>();

        // No painted backdrop: it's about the cafe, so the cafe stays in view behind it.
        RectTransform dim = GameUi.NewRect("Dim", root);
        GameUi.Stretch(dim);
        GameUi.AddPanel(dim, new Color(0.08f, 0.07f, 0.05f, 0.55f), false);

        // A screen's height tall and kept in the middle, so it sits the same on any shape of screen.
        RectTransform group = GameUi.NewRect("Group", root);
        GameUi.Place(group, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 1080f));

        RectTransform paper = GameUi.NewRect("PaperCafe", group);
        GameUi.Place(paper, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(1060f, 0f));
        GameUi.AddPanel(paper, GameUi.Panel2).raycastTarget = false;
        VerticalLayoutGroup column = GameUi.AddLayout<VerticalLayoutGroup>(paper, 14f, new RectOffset(84, 84, 104, 70));
        column.childForceExpandWidth = true;
        column.childAlignment = TextAnchor.UpperCenter;
        paper.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        popup._blurb = GameUi.AddText(paper, "", 34f, GameUi.Ink, TextAlignmentOptions.Top, "Nudge");

        RectTransform cosy = GameUi.NewRect("Cosiness", paper);
        cosy.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
        HorizontalLayoutGroup cosyRow = GameUi.AddLayout<HorizontalLayoutGroup>(cosy, 18f);
        cosyRow.childAlignment = TextAnchor.MiddleCenter;
        popup._pips = GameUi.NewRect("Pips", cosy);
        GameUi.AddLayout<HorizontalLayoutGroup>(popup._pips, 4f).childAlignment = TextAnchor.MiddleCenter;
        RectTransform pip = GameUi.NewRect("PipTemplate", popup._pips);
        popup._pipTemplate = pip.gameObject.AddComponent<Image>();
        popup._pipTemplate.sprite = GeneratedSprites.Circle;
        popup._pipTemplate.color = GameUi.Moss;
        popup._pipTemplate.preserveAspect = true;
        popup._pipTemplate.raycastTarget = false;
        var pipSize = pip.gameObject.AddComponent<LayoutElement>();
        pipSize.preferredWidth = pipSize.preferredHeight = 56f;
        pip.gameObject.SetActive(false);
        popup._level = GameUi.AddText(cosy, "", 36f, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "Heading");

        popup._next = GameUi.AddText(paper, "", 27f, GameUi.InkSoft, TextAlignmentOptions.Top, "Next");

        RectTransform stats = GameUi.NewRect("Stats", paper);
        stats.gameObject.AddComponent<LayoutElement>().preferredHeight = 148f;
        HorizontalLayoutGroup statRow = GameUi.AddLayout<HorizontalLayoutGroup>(stats, 14f);
        statRow.childForceExpandWidth = true;
        statRow.childForceExpandHeight = true;
        popup._things = Stat(stats, "Things", "things out", out popup._thingsLabel);
        popup._seats = Stat(stats, "Seats", "seats", out popup._seatsLabel);
        popup._visitors = Stat(stats, "Visitors", "more visitors", out _);
        popup._luck = Stat(stats, "Luck", "chance of a lucky day", out _);

        GameUi.AddText(paper, "More things, and more kinds of things, make it cosier.", 27f, GameUi.InkSoft, TextAlignmentOptions.Top, "Hint");

        // The sign hangs in front of the top of the paper.
        RectTransform sign = GameUi.NewRect("Sign", group);
        GameUi.Place(sign, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 44f), new Vector2(660f, 660f));
        GameUi.AddPanel(sign, GameUi.Bark, false).raycastTarget = false;
        popup._title = GameUi.AddText(group, "", 50f, GameUi.Ink, TextAlignmentOptions.CenterGeoAligned, "SignTitle");
        GameUi.Place(popup._title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -268f), new Vector2(500f, 150f));
        popup._title.enableAutoSizing = true;
        popup._title.fontSizeMin = 34f;
        popup._title.fontSizeMax = 50f;

        popup._back = GameUi.AddBack(root);
        return popup;
    }

    private static TextMeshProUGUI Stat(Transform parent, string name, string label, out TextMeshProUGUI labelText)
    {
        RectTransform card = GameUi.NewRect(name, parent);
        GameUi.AddPanel(card, GameUi.CardBack).raycastTarget = false;
        TextMeshProUGUI number = GameUi.AddText(card, "", 50f, GameUi.Ink, TextAlignmentOptions.CenterGeoAligned, "Title");
        GameUi.Anchor(number.rectTransform, new Vector2(0f, 0.46f), Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, -6f));
        labelText = GameUi.AddText(card, label, 24f, GameUi.Ink, TextAlignmentOptions.Top, "Label");
        GameUi.Anchor(labelText.rectTransform, Vector2.zero, new Vector2(1f, 0.46f), new Vector2(10f, 6f), new Vector2(-10f, 0f));
        return number;
    }
}
