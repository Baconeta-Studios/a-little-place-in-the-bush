using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One card in an <see cref="ItemGridView"/>: a background, an icon (or a coloured dot with a letter until
/// there's art), a name and a corner badge. A grid can have one of these as a template to restyle in a
/// prefab; otherwise each card is built with <see cref="Build"/>.
/// </summary>
public class ItemCardView : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Button button;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI letter;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private GameObject badge;
    [SerializeField] private TextMeshProUGUI badgeText;
    [SerializeField] private Color normalColor = GameUi.CardBack;
    [SerializeField] private Color selectedColor = GameUi.CardSelected;

    /// <summary>For cards that are only there to look at: no background, and they don't look tappable.</summary>
    public void MakeFlat()
    {
        normalColor.a = 0f;
        background.color = normalColor;
        button.interactable = false;
    }

    /// <summary>For cards with no background over something dark.</summary>
    public void SetNameColor(Color color) => title.color = color;

    public void Fill(ItemCard card)
    {
        background.color = card.selected ? selectedColor : normalColor;
        if (card.onClick != null) button.onClick.AddListener(() => card.onClick());
        group.alpha = card.dimmed ? 0.45f : 1f;

        // No art yet: a coloured dot with the first letter.
        icon.sprite = card.icon ? card.icon : GeneratedSprites.Circle;
        icon.color = card.iconTint;
        if (letter)
        {
            letter.gameObject.SetActive(!card.icon);
            letter.text = string.IsNullOrEmpty(card.title) ? "?" : card.title.Substring(0, 1).ToUpperInvariant();
        }

        title.text = card.title;
        bool hasBadge = !string.IsNullOrEmpty(card.badge);
        if (badge) badge.SetActive(hasBadge);
        if (badgeText && hasBadge) badgeText.text = card.badge;
    }

    public static ItemCardView Build(Transform parent, string name)
    {
        RectTransform rect = GameUi.NewRect(name, parent);
        var view = rect.gameObject.AddComponent<ItemCardView>();

        view.background = GameUi.AddPanel(rect, GameUi.CardBack);
        view.button = rect.gameObject.AddComponent<Button>();
        view.button.targetGraphic = view.background;
        view.group = rect.gameObject.AddComponent<CanvasGroup>();

        // Icon area: the top two thirds.
        RectTransform iconRect = GameUi.NewRect("Icon", rect);
        iconRect.anchorMin = new Vector2(0.12f, 0.36f);
        iconRect.anchorMax = new Vector2(0.88f, 0.92f);
        iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
        view.icon = iconRect.gameObject.AddComponent<Image>();
        view.icon.raycastTarget = false;
        view.icon.preserveAspect = true;
        view.letter = GameUi.AddText(iconRect, "", 54f, GameUi.Cream, TextAlignmentOptions.Center, "Letter");
        view.letter.fontStyle = FontStyles.Bold;

        view.title = GameUi.AddText(rect, "", 24f, GameUi.Ink, TextAlignmentOptions.Center, "Name");
        RectTransform titleRect = view.title.rectTransform;
        titleRect.anchorMin = new Vector2(0.05f, 0.02f);
        titleRect.anchorMax = new Vector2(0.95f, 0.36f);
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
        view.title.enableAutoSizing = true;
        view.title.fontSizeMin = 16f;
        view.title.fontSizeMax = 26f;

        RectTransform badge = GameUi.NewRect("Badge", rect);
        GameUi.Place(badge, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-2f, 6f), new Vector2(72f, 32f));
        GameUi.AddPanel(badge, GameUi.Moss).raycastTarget = false;
        view.badge = badge.gameObject;
        view.badgeText = GameUi.AddText(badge, "", 20f, GameUi.Cream, TextAlignmentOptions.Center, "BadgeText");
        view.badgeText.fontStyle = FontStyles.Bold;
        return view;
    }
}
