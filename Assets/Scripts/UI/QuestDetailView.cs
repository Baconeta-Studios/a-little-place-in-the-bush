using Bush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One quest in full: who it's from, what it needs, what you'll get and whether it's ready. The story's sheet
/// on the quest screen, and the sheet that opens when you tap a pinned note, are both this.
/// </summary>
public class QuestDetailView : MonoBehaviour
{
    [SerializeField] private Image _portrait;
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _from;
    [SerializeField] private TextMeshProUGUI _summary;
    [SerializeField] private GameObject _neededHeading;
    [SerializeField] private RectTransform _rows;
    [SerializeField] private RectTransform _rowTemplate;
    [SerializeField] private RectTransform _pieces;
    [SerializeField] private RectTransform _pieceTemplate;
    [SerializeField] private Color _pieceFound = GameUi.CardBack;
    [SerializeField] private Color _pieceMissing = new(0.84f, 0.78f, 0.64f);
    [SerializeField] private GameObject _rewardsHeading;
    [SerializeField] private RectTransform _rewards;
    [SerializeField] private RectTransform _rewardTemplate;
    [SerializeField] private TextMeshProUGUI _status;

    /// <summary>When there's no quest to show: just a title and a line.</summary>
    public void ShowMessage(string title, string text)
    {
        Clear();
        _portrait.gameObject.SetActive(false);
        _title.text = title;
        _from.gameObject.SetActive(false);
        _summary.text = text;
        _neededHeading.SetActive(false);
        _rows.gameObject.SetActive(false);
        _pieces.gameObject.SetActive(false);
        _rewardsHeading.SetActive(false);
        _rewards.gameObject.SetActive(false);
        _status.gameObject.SetActive(false);
    }

    public void Show(GameSession session, QuestData quest)
    {
        Clear();
        DesignData design = session.Design;
        QuestService quests = session.Quests;
        Sprite face = session.Database.Find<NpcDefinition>(quest.giver)?.portrait;
        _portrait.gameObject.SetActive(face);
        _portrait.sprite = face;
        _title.text = quest.title;
        _from.gameObject.SetActive(true);
        _from.text = $"From {QuestLogPopup.NpcName(design, quest.giver)}";
        _summary.text = quest.summary;

        QuestStatus status = quests.GetStatus(quest.id);
        bool started = status != QuestStatus.NotStarted;
        _neededHeading.SetActive(started && quest.objectives.Count > 0);
        // Several things to gather: side by side, saying where each one is. Otherwise, a simple list.
        bool tray = quest.objectives.Count >= 3 && quest.objectives.TrueForAll(o => design.GetItem(o.target) != null);
        _rows.gameObject.SetActive(started && !tray);
        _pieces.gameObject.SetActive(started && tray);
        if (started)
        {
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                if (tray) AddPiece(session, quest, i);
                else AddRow(session, quest, i);
            }
        }

        _rewardsHeading.SetActive(quest.rewards.Count > 0);
        _rewards.gameObject.SetActive(quest.rewards.Count > 0);
        foreach (QuestReward reward in quest.rewards) AddReward(session, reward);

        string line = QuestLogPopup.StatusLine(session, quest, out bool good);
        _status.gameObject.SetActive(line.Length > 0);
        _status.text = line;
        _status.color = good ? new Color(0.17f, 0.29f, 0.13f) : GameUi.InkSoft;
    }

    private void Clear()
    {
        GameUi.ClearExcept(_rows, _rowTemplate);
        GameUi.ClearExcept(_pieces, _pieceTemplate);
        GameUi.ClearExcept(_rewards, _rewardTemplate);
    }

    private void AddRow(GameSession session, QuestData quest, int index)
    {
        RectTransform row = Instantiate(_rowTemplate, _rows);
        row.gameObject.SetActive(true);
        (int current, int required) = session.Quests.GetProgress(quest.id, index);
        QuestObjective objective = quest.objectives[index];
        SetIcon(row.Find("Icon").GetComponent<Image>(), QuestLogPopup.ObjectiveIcon(session, objective, out Color tint), tint);
        row.Find("Label").GetComponent<TextMeshProUGUI>().text = QuestText.Objective(session.Design, objective);
        var count = row.Find("Count").GetComponent<TextMeshProUGUI>();
        bool done = current >= required;
        count.text = done ? "done" : required > 1 ? $"{current} of {required}" : "";
        count.color = done ? new Color(0.17f, 0.29f, 0.13f) : GameUi.Ink;
    }

    private void AddPiece(GameSession session, QuestData quest, int index)
    {
        RectTransform piece = Instantiate(_pieceTemplate, _pieces);
        piece.gameObject.SetActive(true);
        (int current, int required) = session.Quests.GetProgress(quest.id, index);
        bool found = current >= required;
        QuestObjective objective = quest.objectives[index];
        piece.GetComponent<Image>().color = found ? _pieceFound : _pieceMissing;
        Image icon = piece.Find("Icon").GetComponent<Image>();
        SetIcon(icon, QuestLogPopup.ObjectiveIcon(session, objective, out Color tint), new Color(tint.r, tint.g, tint.b, found ? 1f : 0.45f));
        piece.Find("DishName").GetComponent<TextMeshProUGUI>().text = session.Design.GetItem(objective.target)?.name;
        ItemSource source = QuestHints.WhereToGet(session.Design, objective.target);
        piece.Find("Where").GetComponent<TextMeshProUGUI>().text = found ? "Found"
            : source.foraged ? "Find it foraging"
            : "Try talking to the visitors to your cafe";
    }

    private void AddReward(GameSession session, QuestReward reward)
    {
        RectTransform row = Instantiate(_rewardTemplate, _rewards);
        row.gameObject.SetActive(true);
        SetIcon(row.Find("Icon").GetComponent<Image>(), QuestLogPopup.RewardIcon(session, reward, out Color tint), tint);
        row.Find("Label").GetComponent<TextMeshProUGUI>().text = QuestText.Reward(session.Design, reward);
    }

    private static void SetIcon(Image icon, Sprite sprite, Color tint)
    {
        icon.gameObject.SetActive(sprite);
        icon.sprite = sprite;
        icon.color = tint;
    }

    /// <summary>The default layout: a column of what a quest needs.</summary>
    public static QuestDetailView Build(RectTransform parent)
    {
        var view = parent.gameObject.AddComponent<QuestDetailView>();
        VerticalLayoutGroup column = GameUi.AddLayout<VerticalLayoutGroup>(parent, 10f, new RectOffset(64, 56, 66, 60));
        column.childAlignment = TextAnchor.UpperLeft;
        column.childForceExpandWidth = true;

        RectTransform portrait = GameUi.NewRect("Portrait", parent);
        GameUi.Place(portrait, Vector2.one, Vector2.one, new Vector2(-56f, -50f), new Vector2(150f, 190f));
        portrait.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        view._portrait = portrait.gameObject.AddComponent<Image>();
        view._portrait.preserveAspect = true;
        view._portrait.raycastTarget = false;

        view._title = GameUi.AddText(parent, "", 54f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Title");
        view._title.margin = new Vector4(0f, 0f, 160f, 0f);
        view._from = GameUi.AddText(parent, "", 28f, GameUi.InkSoft, TextAlignmentOptions.TopLeft, "From");
        view._summary = GameUi.AddText(parent, "", 29f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Summary");
        view._summary.margin = new Vector4(0f, 0f, 160f, 0f);

        view._neededHeading = GameUi.AddText(parent, "What's needed", 25f, GameUi.InkSoft, TextAlignmentOptions.TopLeft, "Heading").gameObject;
        view._neededHeading.GetComponent<TextMeshProUGUI>().margin = new Vector4(0f, 16f, 0f, 0f);

        view._rows = GameUi.NewRect("Rows", parent);
        VerticalLayoutGroup rows = GameUi.AddLayout<VerticalLayoutGroup>(view._rows, 4f);
        rows.childForceExpandWidth = true;
        view._rowTemplate = RowTemplate(view._rows, 50f, 28f);
        var count = GameUi.AddText(view._rowTemplate, "", 28f, GameUi.Ink, TextAlignmentOptions.MidlineRight, "Count");
        count.textWrappingMode = TextWrappingModes.NoWrap;
        view._rowTemplate.gameObject.SetActive(false);

        view._pieces = GameUi.NewRect("Pieces", parent);
        var grid = view._pieces.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(172f, 190f);
        grid.spacing = new Vector2(10f, 10f);
        view._pieceTemplate = PieceTemplate(view._pieces);

        view._rewardsHeading = GameUi.AddText(parent, "You'll get", 25f, GameUi.InkSoft, TextAlignmentOptions.TopLeft, "Heading").gameObject;
        view._rewardsHeading.GetComponent<TextMeshProUGUI>().margin = new Vector4(0f, 16f, 0f, 0f);
        view._rewards = GameUi.NewRect("Rewards", parent);
        VerticalLayoutGroup rewards = GameUi.AddLayout<VerticalLayoutGroup>(view._rewards, 2f);
        rewards.childForceExpandWidth = true;
        view._rewardTemplate = RowTemplate(view._rewards, 38f, 26f);
        view._rewardTemplate.gameObject.SetActive(false);

        view._status = GameUi.AddText(parent, "", 28f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Status");
        return view;
    }

    private static RectTransform RowTemplate(RectTransform parent, float iconSize, float textSize)
    {
        RectTransform row = GameUi.NewRect("RowTemplate", parent);
        GameUi.AddLayout<HorizontalLayoutGroup>(row, 12f).childAlignment = TextAnchor.MiddleLeft;
        RectTransform icon = GameUi.NewRect("Icon", row);
        var image = icon.gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        var size = icon.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = size.preferredHeight = iconSize;
        var label = GameUi.AddText(row, "", textSize, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "Label");
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        return row;
    }

    private static RectTransform PieceTemplate(RectTransform parent)
    {
        RectTransform piece = GameUi.NewRect("PieceTemplate", parent);
        GameUi.AddPanel(piece, GameUi.CardBack).raycastTarget = false;
        VerticalLayoutGroup column = GameUi.AddLayout<VerticalLayoutGroup>(piece, 2f, new RectOffset(8, 8, 8, 8));
        column.childAlignment = TextAnchor.UpperCenter;
        column.childForceExpandWidth = true;
        RectTransform icon = GameUi.NewRect("Icon", piece);
        var image = icon.gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        icon.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
        GameUi.AddText(piece, "", 21f, GameUi.Ink, TextAlignmentOptions.Top, "DishName");
        GameUi.AddText(piece, "", 21f, GameUi.Ink, TextAlignmentOptions.Top, "Where");
        piece.gameObject.SetActive(false);
        return piece;
    }
}
