using System.Text;
using Bush.Core;
using TMPro;
using UI.Popups;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The quest log: the main story's current step at the top, then side quests under way (no limit),
/// then who might have something for you. Opened from the HUD's Quests button or the tracker.
/// A template popup, so it pauses the game and Escape closes it. Built in code.
/// </summary>
public class QuestLogPopup : Popup
{
    private static QuestLogPopup _instance;

    private TextMeshProUGUI _text;
    private ScrollRect _scroll;

    public static void Show()
    {
        if (GameSession.Current == null) return;
        if (!_instance) _instance = Create();
        _instance.Open();
    }

    private static QuestLogPopup Create()
    {
        RectTransform root = GameUi.NewRect("Quests", GameUi.Current.SafeRoot);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var popup = root.gameObject.AddComponent<QuestLogPopup>();
        popup.Build(root);
        return popup;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Refresh();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Refresh()
    {
        GameSession session = GameSession.Current;
        if (session == null || !_text) return;
        QuestService quests = session.Quests;
        DesignData design = session.Design;
        var text = new StringBuilder();

        text.Append(Heading("The story"));
        QuestData main = quests.CurrentMainQuest();
        if (main == null) text.Append(quests.IsMainStoryComplete() ? "You've finished the story. The cafe is yours!\n" : "Nothing yet.\n");
        else AppendQuest(text, session, main);

        text.Append('\n').Append(Heading("Side quests"));
        var sides = quests.ActiveSideQuests();
        if (sides.Count == 0) text.Append(Soft("None under way.\n"));
        foreach (QuestData side in sides)
        {
            AppendQuest(text, session, side);
            text.Append('\n');
        }

        var available = quests.AvailableSideQuests();
        if (available.Count > 0)
        {
            text.Append('\n').Append(Heading("Ask around"));
            foreach (QuestData quest in available)
                text.Append($"{NpcName(design, quest.giver)} might have something for you.\n");
        }

        _text.text = text.ToString();
        _scroll.verticalNormalizedPosition = 1f;
    }

    private static void AppendQuest(StringBuilder text, GameSession session, QuestData quest)
    {
        QuestStatus status = session.Quests.GetStatus(quest.id);
        string from = NpcName(session.Design, quest.giver);
        text.Append($"<b>{quest.title}</b>  {Soft("from " + from)}\n");
        if (!string.IsNullOrEmpty(quest.summary)) text.Append(Soft(quest.summary)).Append('\n');

        if (status == QuestStatus.NotStarted)
        {
            text.Append($"Talk to {from}{Visitors.WhenAbout(session.Design, session.State, quest.giver)} to begin.\n");
            return;
        }
        for (int i = 0; i < quest.objectives.Count; i++)
            text.Append("  - ").Append(DialogueBox.ObjectiveLine(session, quest, i)).Append('\n');
        if (status == QuestStatus.ReadyToClaim)
            text.Append($"<color=#{ColorUtility.ToHtmlStringRGB(GameUi.Berry)}>Ready! Go and see {from}.</color>\n");
    }

    public static string NpcName(DesignData design, string npcId) => design.GetNpc(npcId)?.name ?? "someone";

    private static string Heading(string text) => $"<size=120%><b>{text}</b></size>\n";
    private static string Soft(string text) => $"<color=#{ColorUtility.ToHtmlStringRGB(GameUi.InkSoft)}>{text}</color>";

    private void Build(RectTransform root)
    {
        GameUi.AddPanel(root, new Color(0.08f, 0.07f, 0.05f, 0.55f), false);

        RectTransform panel = GameUi.NewRect("Panel", root);
        panel.anchorMin = new Vector2(0.15f, 0.04f);
        panel.anchorMax = new Vector2(0.85f, 0.96f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        GameUi.AddPanel(panel, GameUi.Panel);

        TextMeshProUGUI title = GameUi.AddText(panel, "Quests", 54f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Title");
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.offsetMin = new Vector2(40f, 0f);
        title.rectTransform.offsetMax = new Vector2(-260f, -28f);

        Button close = GameUi.AddButton(panel, "Close", Close, new Vector2(190f, 84f), GameUi.Bark, GameUi.Cream);
        GameUi.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(190f, 84f));

        // A scrolling column of text: there's no limit on side quests.
        RectTransform area = GameUi.NewRect("Scroll", panel);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(40f, 32f);
        area.offsetMax = new Vector2(-40f, -130f);
        _scroll = area.gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 30f;

        RectTransform viewport = GameUi.NewRect("Viewport", area);
        GameUi.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = Color.clear; // lets drags on empty space scroll
        _scroll.viewport = viewport;

        RectTransform content = GameUi.NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        GameUi.AddLayout<VerticalLayoutGroup>(content, 0f);
        _scroll.content = content;

        _text = GameUi.AddText(content, "", 34f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Text");
    }
}
