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
/// it pauses the game and Escape closes it. Built in code.
/// </summary>
public class DialogueBox : Popup
{
    private static DialogueBox _instance;

    private NpcDefinition _npc;

    private Image _portrait;
    private TextMeshProUGUI _portraitLetter;
    private TextMeshProUGUI _name;
    private TextMeshProUGUI _text;
    private RectTransform _replies;

    /// <summary>Starts a conversation with this character.</summary>
    public static void Show(NpcDefinition npc)
    {
        if (!npc || GameSession.Current == null) return;
        if (!_instance) _instance = Create();
        _instance._npc = npc;
        _instance.Open();
        _instance.Begin();
    }

    private static DialogueBox Create()
    {
        RectTransform root = GameUi.NewRect("Dialogue", GameUi.Current.SafeRoot);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var box = root.gameObject.AddComponent<DialogueBox>();
        box.Build(root);
        return box;
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

        string name = string.IsNullOrEmpty(_npc.species) ? NpcName : $"{NpcName}  <size=70%><color={Hex(GameUi.InkSoft)}>{_npc.species}</color></size>";
        _name.text = name;
        _portrait.sprite = _npc.portrait ? _npc.portrait : GeneratedSprites.Circle;
        _portrait.color = _npc.portrait ? Color.white : GameUi.Bark;
        _portrait.preserveAspect = true;
        _portraitLetter.text = _npc.portrait ? "" : NpcName.Substring(0, 1);

        ShowTopic();
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
                Say(Line(topic.line, HasShop ? "Care to look at my recipes?" : $"{NpcName} gives you a friendly nod."), null);
                if (HasShop) Replies(("Recipes", OpenShop), ("Bye", Close));
                else Replies(("Bye", Close));
                break;
        }
    }

    private bool HasShop => Session.Design.GetNpc(_npc.id)?.shop.Count > 0;

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
        TopicKind next = Session.Quests.GetTopic(_npc.id).kind;
        if (next is TopicKind.Offer or TopicKind.HandIn) Replies(("Anything else?", ShowTopic), ("Bye", Close));
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

    private void Say(string line, string detail)
    {
        _text.text = string.IsNullOrEmpty(detail) ? line : $"{line}\n\n<size=85%>{detail}</size>";
    }

    private static string Line(string line, string fallback) => string.IsNullOrEmpty(line) ? fallback : line;

    private void Replies(params (string label, UnityAction onClick)[] replies)
    {
        for (int i = _replies.childCount - 1; i >= 0; i--) Destroy(_replies.GetChild(i).gameObject);
        for (int i = 0; i < replies.Length; i++)
        {
            bool primary = i == 0;
            GameUi.AddButton(_replies, replies[i].label, replies[i].onClick, new Vector2(300f, 96f),
                primary ? GameUi.Moss : GameUi.Cream, primary ? GameUi.Cream : GameUi.Ink, 32f);
        }
    }

    private static string Hex(Color colour) => "#" + ColorUtility.ToHtmlStringRGB(colour);

    private void Build(RectTransform root)
    {
        // Clear, but catches clicks so the gecko doesn't walk off mid-conversation.
        GameUi.AddPanel(root, new Color(0f, 0f, 0f, 0.15f), false);

        RectTransform panel = GameUi.NewRect("Box", root);
        panel.anchorMin = new Vector2(0.02f, 0f);
        panel.anchorMax = new Vector2(0.98f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.offsetMin = new Vector2(0f, 24f);
        panel.offsetMax = new Vector2(0f, 24f + 400f);
        GameUi.AddPanel(panel, GameUi.Panel);

        RectTransform portrait = GameUi.NewRect("Portrait", panel);
        GameUi.Place(portrait, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(300f, 300f));
        _portrait = portrait.gameObject.AddComponent<Image>();
        _portrait.raycastTarget = false;
        _portraitLetter = GameUi.AddText(portrait, "", 140f, GameUi.Cream, TextAlignmentOptions.Center, "Letter");
        _portraitLetter.fontStyle = FontStyles.Bold;

        _name = GameUi.AddText(panel, "", 44f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Name");
        _name.fontStyle = FontStyles.Bold;
        _name.rectTransform.offsetMin = new Vector2(360f, 0f);
        _name.rectTransform.offsetMax = new Vector2(-370f, -24f);

        _text = GameUi.AddText(panel, "", 36f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Text");
        _text.rectTransform.offsetMin = new Vector2(360f, 24f);
        _text.rectTransform.offsetMax = new Vector2(-370f, -86f);
        _text.overflowMode = TextOverflowModes.Ellipsis;

        _replies = GameUi.NewRect("Replies", panel);
        _replies.anchorMin = new Vector2(1f, 0f);
        _replies.anchorMax = new Vector2(1f, 1f);
        _replies.pivot = new Vector2(1f, 0.5f);
        _replies.offsetMin = new Vector2(-340f, 24f);
        _replies.offsetMax = new Vector2(-24f, -24f);
        VerticalLayoutGroup column = GameUi.AddLayout<VerticalLayoutGroup>(_replies, 16f);
        column.childAlignment = TextAnchor.LowerRight;
    }
}
