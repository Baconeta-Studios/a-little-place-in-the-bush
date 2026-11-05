using Bush.Core;
using TMPro;
using UI.Popups;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The quest screen. The story on one big sheet, with what it needs and what you'll get. Side quests are notes
/// pinned on the right: tap one for the whole thing. Opened from the HUD's Quests button or the job note.
/// A template popup, so it pauses the game and Escape closes it.
///
/// How it looks is in Resources/UI/QuestLogPopup.prefab (see docs/UI.md), made from <see cref="BuildDefault"/>,
/// which is also used if there's no prefab.
/// </summary>
public class QuestLogPopup : Popup
{
    public const string ResourcePath = "UI/QuestLogPopup";

    private static QuestLogPopup _instance;

    [SerializeField] private QuestDetailView _story;
    [SerializeField] private TextMeshProUGUI _finished;
    [SerializeField] private RectTransform _notes;
    [SerializeField] private QuestNoteCard _noteTemplate;
    [SerializeField] private TextMeshProUGUI _noNotes;
    [SerializeField] private GameObject _askAround;
    [SerializeField] private TextMeshProUGUI _askAroundText;
    [SerializeField] private Button _back;
    [SerializeField] private GameObject _detail;
    [SerializeField] private QuestDetailView _detailView;
    [SerializeField] private Button _detailBack;

    public static void Show()
    {
        if (GameSession.Current == null) return;
        if (!_instance) _instance = GameUi.LoadPopup(ResourcePath, BuildDefault);
        _instance.Open();
    }

    protected override void Awake()
    {
        base.Awake();
        _back.onClick.AddListener(Close);
        _detailBack.onClick.AddListener(() => _detail.SetActive(false));
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _detail.SetActive(false);
        Refresh();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Refresh()
    {
        GameSession session = GameSession.Current;
        if (session == null) return;
        QuestService quests = session.Quests;
        DesignData design = session.Design;

        QuestData main = quests.CurrentMainQuest();
        QuestData waiting = main == null ? quests.WaitingMainQuest() : null;
        if (main != null) _story.Show(session, main);
        else if (waiting != null) _story.ShowMessage(waiting.title, $"{NpcName(design, waiting.giver)} will have something for you tomorrow.");
        else _story.ShowMessage("The story", quests.IsMainStoryComplete() ? "You've finished the story. The cafe is yours!" : "Nothing yet.");

        int finished = design.quests.FindAll(q => quests.GetStatus(q.id) == QuestStatus.Completed).Count;
        _finished.text = finished > 0 ? $"{finished} quest{(finished == 1 ? "" : "s")} finished" : "";

        GameUi.ClearExcept(_notes, _noteTemplate.transform);
        var sides = quests.ActiveSideQuests();
        foreach (QuestData side in sides)
        {
            QuestNoteCard note = Instantiate(_noteTemplate, _notes);
            note.gameObject.SetActive(true);
            QuestData quest = side;
            note.Fill(session, quest, () =>
            {
                _detailView.Show(session, quest);
                _detail.SetActive(true);
            });
        }
        _noNotes.gameObject.SetActive(sides.Count == 0);

        var available = quests.AvailableSideQuests();
        _askAround.SetActive(available.Count > 0);
        if (available.Count > 0)
        {
            var names = available.ConvertAll(quest => NpcName(design, quest.giver) + Visitors.WhenAbout(design, session.State, quest.giver));
            string who = names.Count == 1 ? names[0] : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[^1];
            _askAroundText.text = $"Ask around: {who} might have something for you.";
        }
    }

    public static string NpcName(DesignData design, string npcId) => design.GetNpc(npcId)?.name ?? "someone";

    /// <summary>The next thing to do on a quest, in a few words: for the job note. Only says to go and see someone who's here.</summary>
    public static string NextStep(GameSession session, QuestData quest)
    {
        QuestService quests = session.Quests;
        DesignData design = session.Design;
        string giver = NpcName(design, quest.giver);
        string when = Visitors.WhenAbout(design, session.State, quest.giver);
        QuestStatus status = quests.GetStatus(quest.id);
        if (status == QuestStatus.NotStarted) return $"Talk to {giver}{when}";
        if (status == QuestStatus.ReadyToClaim)
            return Visitors.IsPresent(design, session.State, quests, quest.giver) ? $"Go and see {giver}"
                : when.Length > 0 ? $"See {giver}{when}" : $"{giver} isn't here today";
        for (int i = 0; i < quest.objectives.Count; i++)
        {
            (int current, int required) = quests.GetProgress(quest.id, i);
            if (current < required) return DialogueBox.ObjectiveLine(session, quest, i);
        }
        return "";
    }

    /// <summary>A line about where a quest has got to, or nothing if it's simply under way.</summary>
    public static string StatusLine(GameSession session, QuestData quest, out bool good)
    {
        good = false;
        QuestService quests = session.Quests;
        DesignData design = session.Design;
        string giver = NpcName(design, quest.giver);
        string when = Visitors.WhenAbout(design, session.State, quest.giver);
        QuestStatus status = quests.GetStatus(quest.id);
        if (status == QuestStatus.NotStarted) return $"Talk to {giver}{when} to begin.";
        if (status != QuestStatus.ReadyToClaim) return "";
        if (Visitors.IsPresent(design, session.State, quests, quest.giver))
        {
            good = true;
            return $"All done. {giver}'s here, so go and see them.";
        }
        return when.Length > 0 ? $"All done. You can hand this in to {giver}{when}." : $"All done. {giver} isn't here today, so it can wait until they visit.";
    }

    public static Sprite ObjectiveIcon(GameSession session, QuestObjective objective, out Color tint)
    {
        tint = Color.white;
        switch (objective.type)
        {
            case ObjectiveType.HaveItems:
            case ObjectiveType.DeliverItems:
            case ObjectiveType.PlaceObject:
            case ObjectiveType.FindItems:
            case ObjectiveType.HavePlaced:
                ItemDefinition item = session.Database.Find<ItemDefinition>(objective.target);
                if (!item) return null;
                PlaceableDefinition placeable = session.Database.placeables.Find(p => p && p.item == item);
                return ItemGridView.IconFor(item, placeable, out tint);
            case ObjectiveType.CookRecipe:
            case ObjectiveType.CloseWithOnMenu:
            case ObjectiveType.HaveOnMenu:
                return session.Database.recipeIcon;
            case ObjectiveType.TalkTo:
                return session.Database.Find<NpcDefinition>(objective.target)?.portrait;
            default:
                return null;
        }
    }

    public static Sprite RewardIcon(GameSession session, QuestReward reward, out Color tint)
    {
        tint = Color.white;
        switch (reward.type)
        {
            case RewardType.Item:
                ItemDefinition item = session.Database.Find<ItemDefinition>(reward.target);
                if (!item) return null;
                PlaceableDefinition placeable = session.Database.placeables.Find(p => p && p.item == item);
                return ItemGridView.IconFor(item, placeable, out tint);
            case RewardType.UnlockRecipe:
                return session.Database.recipeIcon;
            default:
                return null;
        }
    }

    // ----- The default layout -----

    public static QuestLogPopup BuildDefault(Transform parent)
    {
        RectTransform root = GameUi.NewRect("QuestLogPopup", parent);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var popup = root.gameObject.AddComponent<QuestLogPopup>();
        GameUi.AddBackdrop(root);

        RectTransform paper = GameUi.NewRect("PaperStory", root);
        GameUi.Anchor(paper, Vector2.zero, new Vector2(0.46f, 1f), new Vector2(-4f, -8f), new Vector2(0f, 8f));
        GameUi.AddPanel(paper, GameUi.Panel2);
        popup._story = QuestDetailView.Build(paper);
        popup._finished = GameUi.AddText(paper, "", 26f, GameUi.InkSoft, TextAlignmentOptions.BottomLeft, "Finished");
        popup._finished.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        GameUi.Anchor(popup._finished.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(64f, 56f), new Vector2(-64f, 100f));

        RectTransform side = GameUi.NewRect("Jobs", root);
        GameUi.Anchor(side, new Vector2(0.46f, 0f), Vector2.one, new Vector2(16f, 112f), new Vector2(-24f, 0f));
        GameUi.AddSign(side, "Odd jobs");
        RectTransform list = GameUi.NewRect("List", side);
        GameUi.Anchor(list, Vector2.zero, Vector2.one, new Vector2(0f, 100f), new Vector2(0f, -262f));
        ScrollRect scroll = GameUi.AddScroll(list, out RectTransform content);
        popup._notes = content;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(316f, 250f);
        grid.spacing = new Vector2(14f, 24f);
        grid.padding = new RectOffset(4, 4, 16, 8);
        grid.childAlignment = TextAnchor.UpperCenter;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        popup._noteTemplate = QuestNoteCard.Build(content);
        popup._noNotes = GameUi.AddText(list, "Nobody's asked for help yet.", 34f, GameUi.Cream, TextAlignmentOptions.Center, "NoNotes");
        popup._noNotes.raycastTarget = false;

        RectTransform ask = GameUi.NewRect("PaperAskAround", side);
        GameUi.Anchor(ask, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, -12f), new Vector2(0f, 112f));
        GameUi.AddPanel(ask, GameUi.Panel2).raycastTarget = false;
        popup._askAround = ask.gameObject;
        popup._askAroundText = GameUi.AddText(ask, "", 26f, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "AskAround");
        GameUi.Anchor(popup._askAroundText.rectTransform, Vector2.zero, Vector2.one, new Vector2(44f, 30f), new Vector2(-44f, -30f));
        popup._askAroundText.enableAutoSizing = true;
        popup._askAroundText.fontSizeMin = 24f;
        popup._askAroundText.fontSizeMax = 26f;

        popup._back = GameUi.AddBack(root);

        // A pinned note, opened up.
        RectTransform detail = GameUi.NewRect("Detail", root);
        GameUi.Stretch(detail);
        GameUi.AddPanel(detail, new Color(0.08f, 0.07f, 0.05f, 0.55f), false);
        popup._detail = detail.gameObject;
        RectTransform detailPaper = GameUi.NewRect("PaperDetail", detail);
        GameUi.Place(detailPaper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 0f));
        GameUi.AddPanel(detailPaper, GameUi.Panel2);
        popup._detailView = QuestDetailView.Build(detailPaper);
        detailPaper.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        popup._detailBack = GameUi.AddBack(detail);
        detail.gameObject.SetActive(false);
        return popup;
    }
}
