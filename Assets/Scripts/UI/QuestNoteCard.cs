using System;
using Bush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A side quest pinned to the quest screen: what it needs at a glance. Tap it for the whole thing.</summary>
public class QuestNoteCard : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _from;
    [SerializeField] private RectTransform _needs;
    [SerializeField] private RectTransform _needTemplate;
    [SerializeField] private TextMeshProUGUI _ready;

    public void Fill(GameSession session, QuestData quest, Action onClick)
    {
        _button.onClick.AddListener(() => onClick());
        string giver = QuestLogPopup.NpcName(session.Design, quest.giver);
        _title.text = quest.title;
        _from.text = giver;

        GameUi.ClearExcept(_needs, _needTemplate);
        for (int i = 0; i < quest.objectives.Count; i++)
        {
            (int current, int required) = session.Quests.GetProgress(quest.id, i);
            RectTransform need = Instantiate(_needTemplate, _needs);
            need.gameObject.SetActive(true);
            Image icon = need.GetChild(0).GetComponent<Image>();
            Sprite sprite = QuestLogPopup.ObjectiveIcon(session, quest.objectives[i], out Color tint);
            icon.gameObject.SetActive(sprite);
            icon.sprite = sprite;
            icon.color = tint;
            var count = need.GetComponentInChildren<TextMeshProUGUI>();
            count.text = $"{current}/{required}";
            count.color = current >= required ? new Color(0.17f, 0.29f, 0.13f) : GameUi.Ink;
        }

        bool ready = session.Quests.GetStatus(quest.id) == QuestStatus.ReadyToClaim;
        bool here = Visitors.IsPresent(session.Design, session.State, session.Quests, quest.giver);
        _ready.text = !ready ? "" : here ? $"Ready. {giver}'s here." : $"Ready. {giver}'s away.";
        _ready.color = here ? new Color(0.17f, 0.29f, 0.13f) : GameUi.Ink;
    }

    public static QuestNoteCard Build(Transform parent)
    {
        RectTransform rect = GameUi.NewRect("NoteTemplate", parent);
        var card = rect.gameObject.AddComponent<QuestNoteCard>();
        Image back = GameUi.AddPanel(rect, GameUi.CardBack);
        card._button = rect.gameObject.AddComponent<Button>();
        card._button.targetGraphic = back;
        VerticalLayoutGroup column = GameUi.AddLayout<VerticalLayoutGroup>(rect, 4f, new RectOffset(16, 14, 16, 12));
        column.childAlignment = TextAnchor.UpperLeft;
        column.childForceExpandWidth = true;

        RectTransform pin = GameUi.NewRect("Pin", rect);
        GameUi.Place(pin, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
        pin.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var pinImage = pin.gameObject.AddComponent<Image>();
        pinImage.sprite = GeneratedSprites.Circle;
        pinImage.color = new Color(0.48f, 0.24f, 0.14f);
        pinImage.raycastTarget = false;

        card._title = GameUi.AddText(rect, "", 28f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Name");
        card._from = GameUi.AddText(rect, "", 24f, GameUi.InkSoft, TextAlignmentOptions.TopLeft, "From");

        card._needs = GameUi.NewRect("Needs", rect);
        var needsSize = card._needs.gameObject.AddComponent<LayoutElement>();
        needsSize.flexibleHeight = 1f;
        GameUi.AddLayout<HorizontalLayoutGroup>(card._needs, 12f).childAlignment = TextAnchor.MiddleLeft;
        card._needTemplate = GameUi.NewRect("NeedTemplate", card._needs);
        GameUi.AddLayout<HorizontalLayoutGroup>(card._needTemplate, 5f);
        RectTransform icon = GameUi.NewRect("Icon", card._needTemplate);
        var image = icon.gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        var iconSize = icon.gameObject.AddComponent<LayoutElement>();
        iconSize.preferredWidth = iconSize.preferredHeight = 44f;
        GameUi.AddText(card._needTemplate, "", 26f, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "Count").textWrappingMode = TextWrappingModes.NoWrap;
        card._needTemplate.gameObject.SetActive(false);

        card._ready = GameUi.AddText(rect, "", 24f, GameUi.Ink, TextAlignmentOptions.BottomLeft, "Nudge");
        rect.gameObject.SetActive(false);
        return card;
    }
}
