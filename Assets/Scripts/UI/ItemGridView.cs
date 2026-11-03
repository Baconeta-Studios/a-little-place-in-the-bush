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
    [SerializeField] private RectTransform _content;
    [SerializeField] private bool _horizontal;
    [SerializeField] private Vector2 _cardSize;
    [Tooltip("Copied for each card, so restyling this restyles them all. Its size is the card size in a row; a grid uses the Grid Layout Group's cell size.")]
    [SerializeField] private ItemCardView _cardTemplate;

    /// <param name="withTemplate">Make a hidden card to copy, for a screen that's a prefab, so the cards can be restyled there.</param>
    public static ItemGridView Create(RectTransform parent, bool horizontal, Vector2 cardSize, bool withTemplate = false)
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

        if (withTemplate)
        {
            view._cardTemplate = ItemCardView.Build(root, "CardTemplate");
            ((RectTransform)view._cardTemplate.transform).sizeDelta = cardSize;
            view._cardTemplate.gameObject.SetActive(false);
        }
        return view;
    }

    /// <summary>Cards in this grid are only there to look at (it needs a template).</summary>
    public void MakeCardsFlat()
    {
        if (_cardTemplate) _cardTemplate.MakeFlat();
    }

    public void SetItems(IReadOnlyList<ItemCard> cards)
    {
        for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
        foreach (ItemCard card in cards) AddCard(card);
    }

    private void AddCard(ItemCard card)
    {
        ItemCardView view = _cardTemplate ? Instantiate(_cardTemplate, _content) : ItemCardView.Build(_content, card.key);
        view.gameObject.SetActive(true);
        view.name = card.key;
        var rect = (RectTransform)view.transform;
        Vector2 size = _cardTemplate ? rect.sizeDelta : _cardSize;
        rect.sizeDelta = size;
        if (_horizontal)
        {
            var element = view.GetComponent<LayoutElement>();
            if (!element) element = view.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;
        }
        view.Fill(card);
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
