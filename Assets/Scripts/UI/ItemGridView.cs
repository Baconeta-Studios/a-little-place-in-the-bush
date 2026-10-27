using System;
using System.Collections.Generic;
using Bush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One card in an <see cref="ItemGridView"/>.</summary>
public struct ItemCard
{
    public string key;
    public string title;
    /// <summary>Small pill in the corner, e.g. "x3" or "∞". Empty for none.</summary>
    public string badge;
    public Sprite icon;
    public Color iconTint;
    /// <summary>Shown faded (e.g. none left to place).</summary>
    public bool dimmed;
    public bool selected;
    public Action onClick;
}

/// <summary>
/// A scrolling list of item cards: a single row (the build tray) or a grid (the bag). Rebuilt from a
/// list of <see cref="ItemCard"/>s whenever the contents change.
/// </summary>
public class ItemGridView : MonoBehaviour
{
    private RectTransform _content;
    private bool _horizontal;
    private Vector2 _cardSize;

    public static ItemGridView Create(RectTransform parent, bool horizontal, Vector2 cardSize)
    {
        RectTransform root = GameUi.NewRect("ItemGrid", parent);
        GameUi.Stretch(root);
        var view = root.gameObject.AddComponent<ItemGridView>();
        view._horizontal = horizontal;
        view._cardSize = cardSize;

        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = horizontal;
        scroll.vertical = !horizontal;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        RectTransform viewport = GameUi.NewRect("Viewport", root);
        GameUi.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        var catcher = viewport.gameObject.AddComponent<Image>(); // lets drags on empty space scroll
        catcher.color = Color.clear;
        scroll.viewport = viewport;

        view._content = GameUi.NewRect("Content", viewport);
        var fitter = view._content.gameObject.AddComponent<ContentSizeFitter>();
        if (horizontal)
        {
            view._content.anchorMin = new Vector2(0f, 0f);
            view._content.anchorMax = new Vector2(0f, 1f);
            view._content.pivot = new Vector2(0f, 0.5f);
            view._content.offsetMin = view._content.offsetMax = Vector2.zero;
            HorizontalLayoutGroup row = GameUi.AddLayout<HorizontalLayoutGroup>(view._content, 16f, new RectOffset(16, 16, 8, 8));
            row.childAlignment = TextAnchor.MiddleLeft;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        else
        {
            view._content.anchorMin = new Vector2(0f, 1f);
            view._content.anchorMax = new Vector2(1f, 1f);
            view._content.pivot = new Vector2(0.5f, 1f);
            view._content.offsetMin = view._content.offsetMax = Vector2.zero;
            var grid = view._content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cardSize;
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(16, 16, 16, 16);
            grid.childAlignment = TextAnchor.UpperLeft;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        scroll.content = view._content;
        return view;
    }

    public void SetItems(IReadOnlyList<ItemCard> cards)
    {
        for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
        foreach (ItemCard card in cards) AddCard(card);
    }

    private void AddCard(ItemCard card)
    {
        RectTransform rect = GameUi.NewRect(card.key, _content);
        rect.sizeDelta = _cardSize;
        if (_horizontal)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = _cardSize.x;
            element.preferredHeight = _cardSize.y;
        }

        Image background = GameUi.AddPanel(rect, card.selected ? GameUi.MossLight : GameUi.Cream);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        Action onClick = card.onClick;
        if (onClick != null) button.onClick.AddListener(() => onClick());

        var group = rect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = card.dimmed ? 0.45f : 1f;

        // Icon area: the top two thirds.
        RectTransform iconRect = GameUi.NewRect("Icon", rect);
        iconRect.anchorMin = new Vector2(0.12f, 0.36f);
        iconRect.anchorMax = new Vector2(0.88f, 0.92f);
        iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
        var icon = iconRect.gameObject.AddComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        if (card.icon)
        {
            icon.sprite = card.icon;
            icon.color = card.iconTint;
        }
        else
        {
            // No art yet: a coloured dot with the first letter.
            icon.sprite = GeneratedSprites.Circle;
            icon.color = card.iconTint;
            string letter = string.IsNullOrEmpty(card.title) ? "?" : card.title.Substring(0, 1).ToUpperInvariant();
            GameUi.AddText(iconRect, letter, 54f, GameUi.Cream).fontStyle = FontStyles.Bold;
        }

        TextMeshProUGUI title = GameUi.AddText(rect, card.title, 24f, GameUi.Ink, TextAlignmentOptions.Center, "Title");
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.05f, 0.02f);
        titleRect.anchorMax = new Vector2(0.95f, 0.36f);
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
        title.enableAutoSizing = true;
        title.fontSizeMin = 16f;
        title.fontSizeMax = 26f;

        if (!string.IsNullOrEmpty(card.badge))
        {
            RectTransform badge = GameUi.NewRect("Badge", rect);
            GameUi.Place(badge, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(64f, 40f));
            GameUi.AddPanel(badge, GameUi.Moss).raycastTarget = false;
            GameUi.AddText(badge, card.badge, 26f, GameUi.Cream).fontStyle = FontStyles.Bold;
        }
    }

    /// <summary>The art to show for an item: its icon, or the placeable's sprite, or nothing (a lettered dot is drawn).</summary>
    public static Sprite IconFor(ItemDefinition item, PlaceableDefinition placeable, out Color tint)
    {
        tint = Color.white;
        if (item && item.icon) return item.icon;
        if (placeable && placeable.prefab)
        {
            var renderer = placeable.prefab.GetComponentInChildren<SpriteRenderer>();
            if (renderer && renderer.sprite)
            {
                tint = renderer.color;
                return renderer.sprite;
            }
        }
        tint = RarityColor(item ? item.rarity : Rarity.Common);
        return null;
    }

    public static Color RarityColor(Rarity rarity) => rarity switch
    {
        Rarity.Uncommon => new Color(0.36f, 0.6f, 0.3f),
        Rarity.Rare => new Color(0.3f, 0.45f, 0.75f),
        Rarity.Legendary => new Color(0.85f, 0.65f, 0.2f),
        _ => new Color(0.55f, 0.48f, 0.38f)
    };
}
