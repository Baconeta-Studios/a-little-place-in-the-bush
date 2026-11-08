using System.Collections.Generic;
using System.Text;
using Bush.Core;
using TMPro;
using UI.Popups;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Conversations with characters (<see cref="Npc"/>), in a box along the bottom of the screen so the
/// cafe stays in view: portrait, name, what they say, and replies on the right. What they say comes
/// from <see cref="QuestService.GetTopic"/>: offering a quest (Sure! / Not now), asking how it's going
/// (with your progress), or taking what you've brought and giving the rewards. A template popup, so
/// it pauses the game and Escape closes it.
///
/// How it looks is in Resources/UI/DialogueBox.prefab (see docs/UI.md), made from <see cref="BuildDefault"/>,
/// which is also used if there's no prefab.
/// </summary>
public class DialogueBox : Popup
{
    public const string ResourcePath = "UI/DialogueBox";

    private static DialogueBox _instance;

    private NpcDefinition _npc;

    [SerializeField] private Image _portrait;
    [Tooltip("Shown instead of the portrait for anyone with no art yet.")]
    [SerializeField] private GameObject _noPortrait;
    [SerializeField] private TextMeshProUGUI _portraitLetter;
    [SerializeField] private TextMeshProUGUI _name;
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private RectTransform _replies;
    [Tooltip("The first reply: what the conversation is heading for.")]
    [SerializeField] private Button _mainReply;
    [Tooltip("Any other reply.")]
    [SerializeField] private Button _otherReply;
    [Tooltip("A reply that ends the conversation.")]
    [SerializeField] private Button _leaveReply;

    /// <summary>Starts a conversation with this character.</summary>
    public static void Show(NpcDefinition npc)
    {
        if (!npc || GameSession.Current == null) return;
        if (DateScene.TryStart(npc)) return; // the date plays as a scene of its own
        if (!_instance) _instance = GameUi.LoadPopup(ResourcePath, BuildDefault);
        _instance._npc = npc;
        _instance.Open();
        _instance.Begin();
    }

    /// <summary>Opens the box for a scene that says its own lines (<see cref="Script"/>), with no quest talk.</summary>
    public static DialogueBox ShowScripted(NpcDefinition npc)
    {
        if (!npc || GameSession.Current == null) return null;
        if (!_instance) _instance = GameUi.LoadPopup(ResourcePath, BuildDefault);
        _instance._npc = npc;
        _instance.Open();
        _instance.ShowSpeaker();
        return _instance;
    }

    /// <summary>A line from a scene, with its replies.</summary>
    public void Script(string line, string detail, params (string label, UnityAction onClick)[] replies)
    {
        Say(line, detail);
        Replies(replies);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private static GameSession Session => GameSession.Current;
    private string NpcName => string.IsNullOrEmpty(_npc.displayName) ? _npc.id : _npc.displayName;

    private void Begin()
    {
        Session.Quests.BeginConversation(_npc.id); // counts for "talk to" objectives
        Session.RequestSave();
        ShowSpeaker();
        ShowTopic();
    }

    private void ShowSpeaker()
    {
        // Species like "Ruru (morepork)" already have the name in them.
        _name.text = string.IsNullOrEmpty(_npc.species) ? NpcName
            : _npc.species.StartsWith(NpcName) ? _npc.species
            : $"{NpcName}  <size=70%><color={Hex(GameUi.InkSoft)}>{_npc.species}</color></size>";
        _portrait.sprite = _npc.portrait;
        _portrait.enabled = _npc.portrait;
        _noPortrait.SetActive(!_npc.portrait);
        _portraitLetter.text = NpcName.Substring(0, 1);
    }

    private void ShowTopic()
    {
        QuestService quests = Session.Quests;
        NpcTopic topic = quests.GetTopic(_npc.id);
        QuestData quest = Session.Design.GetQuest(topic.questId);

        switch (topic.kind)
        {
            case TopicKind.Offer:
                Say(Line(topic.line, "I could use a hand with something."),
                    $"<b>{quest.title}</b>" + (string.IsNullOrEmpty(quest.summary) ? "" : $": {quest.summary}"));
                Replies(("Sure!", () => Accept(quest.id)), ("Not now", Close));
                break;

            case TopicKind.InProgress:
                Say(Line(topic.line, "How's it going?"), Objectives(quest));
                if (HasShop) Replies(("Recipes", OpenShop), ("Bye", Close));
                else Replies(("Bye", Close));
                break;

            case TopicKind.HandIn:
                Say(Line(topic.line, "Oh, you did it!"), Objectives(quest));
                bool handsOver = quest.objectives.Exists(o => o.type == ObjectiveType.DeliverItems);
                Replies((handsOver ? "Here you go" : "Thanks!", () => HandIn(quest.id)));
                break;

            default:
                Say(Line(topic.line, HasShop ? "Care to look at my recipes?" : DialogueText.Narration($"{NpcName} gives you a friendly nod.")), null);
                if (HasShop) Replies(("Recipes", OpenShop), ("Bye", Close));
                else Replies(("Bye", Close));
                break;
        }
    }

    private bool HasShop
    {
        get
        {
            NpcData npc = Session.Design.GetNpc(_npc.id);
            if (npc == null || npc.shop.Count == 0) return false;
            return string.IsNullOrEmpty(npc.shopAfterQuest) || Session.Quests.GetStatus(npc.shopAfterQuest) == QuestStatus.Completed;
        }
    }

    private void OpenShop()
    {
        Close();
        RecipeShopPopup.Show(_npc);
    }

    private void Accept(string questId)
    {
        Session.Quests.Accept(questId);
        Session.SaveNow();
        ShowTopic(); // ready straight away for a "come and say hello" quest, otherwise how it's going
    }

    private void HandIn(string questId)
    {
        QuestData quest = Session.Design.GetQuest(questId);
        if (!Session.Quests.Claim(questId, out string error))
        {
            Debug.LogWarning($"[Dialogue] Couldn't hand in {questId}: {error}");
            Say("Hmm, not quite yet.", Objectives(quest));
            Replies(("Bye", Close));
            return;
        }
        Session.SaveNow();

        var rewards = new StringBuilder();
        foreach (QuestReward reward in quest.rewards)
            rewards.Append($"<color={Hex(GameUi.Moss)}>{QuestText.Reward(Session.Design, reward)}</color>\n");
        Say(Line(quest.dialogue?.done, "Thank you!"), rewards.Length > 0 ? "<b>You got</b>\n" + rewards : null);

        // Straight on to what's next with them (the next story step, say), or goodbye.
        // Straight on to what's next with them, unless they're off now (a night owl after unlocking night:
        // the next step waits until you find them at night).
        TopicKind next = Session.Quests.GetTopic(_npc.id).kind;
        NpcData data = Session.Design.GetNpc(_npc.id);
        bool stillAbout = data == null || Visitors.AboutNow(data, Session.State);
        if (stillAbout && next is TopicKind.Offer or TopicKind.HandIn) Replies(("Anything else?", ShowTopic), ("Bye", Close));
        else Replies(("Bye", Close));
    }

    private string Objectives(QuestData quest)
    {
        if (quest == null || quest.objectives.Count == 0) return null;
        var text = new StringBuilder($"<b>{quest.title}</b>\n");
        for (int i = 0; i < quest.objectives.Count; i++)
            text.Append(ObjectiveLine(Session, quest, i)).Append('\n');
        return text.ToString().TrimEnd();
    }

    /// <summary>"Cook Leaf salad  0/1", green when done. Shared with the quest log and tracker.</summary>
    public static string ObjectiveLine(GameSession session, QuestData quest, int index)
    {
        (int current, int required) = session.Quests.GetProgress(quest.id, index);
        string label = QuestText.Objective(session.Design, quest.objectives[index]);
        bool done = current >= required;
        string count = required > 1 ? $"  {current}/{required}" : "";
        return done ? $"<color={Hex(GameUi.Moss)}>{label}{count}  (done)</color>" : $"{label}{count}";
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        GameAudio.StopVoice();
    }

    private void Say(string line, string detail)
    {
        // Their voice plays for speech, not narration like "Gus gives you a friendly nod." (see DialogueText).
        if (DialogueText.IsSpoken(line)) GameAudio.Say(_npc);
        else GameAudio.StopVoice();
        _text.text = string.IsNullOrEmpty(detail) ? line : $"{line}\n\n<size=85%>{detail}</size>";
    }

    private static string Line(string line, string fallback) => string.IsNullOrEmpty(line) ? fallback : line;

    private void Replies(params (string label, UnityAction onClick)[] replies)
    {
        for (int i = _replies.childCount - 1; i >= 0; i--)
        {
            Transform child = _replies.GetChild(i);
            if (child != _mainReply.transform && child != _otherReply.transform && child != _leaveReply.transform)
            {
                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }
        for (int i = 0; i < replies.Length; i++)
        {
            bool leaves = replies[i].label is "Bye" or "Not now";
            Button reply = Instantiate(leaves ? _leaveReply : i == 0 ? _mainReply : _otherReply, _replies);
            reply.gameObject.SetActive(true);
            GameUi.SetLabel(reply, replies[i].label);
            reply.onClick.AddListener(replies[i].onClick);
        }
    }

    private static string Hex(Color colour) => "#" + ColorUtility.ToHtmlStringRGB(colour);

    // ----- The default layout -----

    public static DialogueBox BuildDefault(Transform parent)
    {
        RectTransform root = GameUi.NewRect("DialogueBox", parent);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var box = root.gameObject.AddComponent<DialogueBox>();

        // Clear, but catches clicks so the gecko doesn't walk off mid-conversation.
        GameUi.AddPanel(root, new Color(0f, 0f, 0f, 0.15f), false);

        RectTransform panel = GameUi.NewRect("PaperBox", root);
        GameUi.Anchor(panel, Vector2.zero, new Vector2(1f, 0f), new Vector2(-4f, -8f), new Vector2(4f, 470f));
        GameUi.AddPanel(panel, GameUi.Panel2);

        // The character stands in front of the paper. Anyone with no art yet gets a circle with their initial.
        RectTransform portrait = GameUi.NewRect("Portrait", root);
        GameUi.Place(portrait, Vector2.zero, Vector2.zero, new Vector2(60f, 20f), new Vector2(420f, 560f));
        box._portrait = portrait.gameObject.AddComponent<Image>();
        box._portrait.raycastTarget = false;
        box._portrait.preserveAspect = true;
        RectTransform circle = GameUi.NewRect("NoPortrait", portrait);
        GameUi.Place(circle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(260f, 260f));
        var circleImage = circle.gameObject.AddComponent<Image>();
        circleImage.sprite = GeneratedSprites.Circle;
        circleImage.color = GameUi.Bark;
        circleImage.raycastTarget = false;
        box._noPortrait = circle.gameObject;
        box._portraitLetter = GameUi.AddText(circle, "", 120f, GameUi.Cream, TextAlignmentOptions.CenterGeoAligned, "Letter");

        box._name = GameUi.AddText(panel, "", 42f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Name");
        GameUi.Anchor(box._name.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(490f, -116f), new Vector2(-400f, -58f));

        box._text = GameUi.AddText(panel, "", 34f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Text");
        GameUi.Anchor(box._text.rectTransform, Vector2.zero, Vector2.one, new Vector2(490f, 62f), new Vector2(-400f, -118f));
        box._text.enableAutoSizing = true;
        box._text.fontSizeMin = 24f;
        box._text.fontSizeMax = 34f;
        box._text.overflowMode = TextOverflowModes.Ellipsis;

        box._replies = GameUi.NewRect("Replies", panel);
        GameUi.Anchor(box._replies, new Vector2(1f, 0f), Vector2.one, new Vector2(-380f, 62f), new Vector2(-60f, -62f));
        VerticalLayoutGroup column = GameUi.AddLayout<VerticalLayoutGroup>(box._replies, 14f);
        column.childAlignment = TextAnchor.LowerRight;
        box._mainReply = Reply(box._replies, "MainReply", GameUi.Apron, GameUi.Cream);
        box._otherReply = Reply(box._replies, "OtherReply", GameUi.Cream, GameUi.Ink);
        box._leaveReply = Reply(box._replies, "LeaveReply", GameUi.PanelDark, GameUi.Cream);
        return box;
    }

    private static Button Reply(Transform parent, string name, Color background, Color text)
    {
        Button reply = GameUi.AddButton(parent, name, null, new Vector2(300f, 96f), background, text, 32f);
        reply.name = name;
        reply.gameObject.SetActive(false);
        return reply;
    }
}
