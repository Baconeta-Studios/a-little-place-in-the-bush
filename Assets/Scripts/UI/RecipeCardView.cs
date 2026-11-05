using System.Collections.Generic;
using Bush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A recipe on the menu board: what it needs, what it earns and how many you can make. Tap it to put it on the
/// menu or take it off, or drag it onto a page (<see cref="CafePopup"/> does the rest).
/// </summary>
public class RecipeCardView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI dishName;
    [SerializeField] private RectTransform ingredients;
    [SerializeField] private RectTransform ingredientTemplate;
    [SerializeField] private TextMeshProUGUI coins;
    [SerializeField] private TextMeshProUGUI canMake;
    [SerializeField] private GameObject wanted;
    [SerializeField] private TextMeshProUGUI wantedText;
    [SerializeField] private GameObject onMenuTag;
    [SerializeField] private Color normalColor = GameUi.CardBack;
    [SerializeField] private Color onMenuColor = GameUi.CardSelected;
    [SerializeField] private Color cantMakeColor = new(0.67f, 0.59f, 0.45f);
    [SerializeField] private Color shortColor = new(0.49f, 0.12f, 0.09f);
    [SerializeField] private Color enoughColor = new(0.17f, 0.29f, 0.13f);

    private CafePopup _owner;
    private string _recipeId;

    public void Fill(CafePopup owner, GameSession session, RecipeData recipe)
    {
        _owner = owner;
        _recipeId = recipe.id;
        int makes = session.Cafe.CanMakeCount(recipe.id);
        bool onMenu = session.Cafe.IsOnMenu(recipe.id);

        background.color = onMenu ? onMenuColor : makes > 0 ? normalColor : cantMakeColor;
        dishName.text = recipe.name;
        coins.text = $"{recipe.coins} coins";
        canMake.text = makes > 0 ? $"makes {makes}" : "not enough";
        canMake.color = makes > 0 ? enoughColor : shortColor;
        onMenuTag.SetActive(onMenu);

        QuestData quest = session.Quests.DishWantedBy(recipe.id, out int still);
        wanted.SetActive(quest != null);
        if (quest != null) wantedText.text = $"{QuestLogPopup.NpcName(session.Design, quest.giver)} wants {still}";

        for (int i = ingredients.childCount - 1; i >= 0; i--)
        {
            Transform child = ingredients.GetChild(i);
            if (child != ingredientTemplate) Destroy(child.gameObject);
        }
        foreach (ItemAmount ingredient in recipe.ingredients)
        {
            RectTransform row = Instantiate(ingredientTemplate, ingredients);
            row.gameObject.SetActive(true);
            int have = session.Economy.Count(ingredient.itemId);
            ItemDefinition item = session.Database.Find<ItemDefinition>(ingredient.itemId);
            var icon = row.GetComponentInChildren<Image>();
            icon.sprite = ItemGridView.IconFor(item, null, out Color tint);
            icon.color = tint;
            var amount = row.GetComponentInChildren<TextMeshProUGUI>();
            amount.text = $"{have}/{ingredient.qty}";
            amount.color = have >= ingredient.qty ? GameUi.Ink : shortColor;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!eventData.dragging) _owner.ToggleOnMenu(_recipeId);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        eventData.eligibleForClick = false;
        _owner.BeginDrag(_recipeId, -1, eventData);
    }

    public void OnDrag(PointerEventData eventData) => _owner.Drag(eventData);

    public void OnEndDrag(PointerEventData eventData) => _owner.EndDrag(eventData);

    /// <summary>A hidden card to copy. The one the default layout is built from.</summary>
    public static RecipeCardView Build(Transform parent)
    {
        RectTransform rect = GameUi.NewRect("CardTemplate", parent);
        var view = rect.gameObject.AddComponent<RecipeCardView>();
        view.background = GameUi.AddPanel(rect, GameUi.CardBack);

        view.dishName = GameUi.AddText(rect, "", 27f, GameUi.Ink, TextAlignmentOptions.TopLeft, "DishName");
        view.dishName.fontStyle = FontStyles.Bold;
        Offsets(view.dishName.rectTransform, new Vector2(0f, 0.68f), Vector2.one, new Vector2(14f, 0f), new Vector2(-14f, -14f));

        view.ingredients = GameUi.NewRect("Ingredients", rect);
        Offsets(view.ingredients, new Vector2(0f, 0.28f), new Vector2(1f, 0.68f), new Vector2(12f, 0f), new Vector2(-8f, 0f));
        HorizontalLayoutGroup row = GameUi.AddLayout<HorizontalLayoutGroup>(view.ingredients, 4f);
        row.childAlignment = TextAnchor.MiddleLeft;

        view.ingredientTemplate = GameUi.NewRect("IngredientTemplate", view.ingredients);
        GameUi.AddLayout<HorizontalLayoutGroup>(view.ingredientTemplate, 3f);
        RectTransform icon = GameUi.NewRect("Icon", view.ingredientTemplate);
        var iconImage = icon.gameObject.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        var iconSize = icon.gameObject.AddComponent<LayoutElement>();
        iconSize.preferredWidth = iconSize.preferredHeight = 38f;
        GameUi.AddText(view.ingredientTemplate, "", 23f, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "Amount").textWrappingMode = TextWrappingModes.NoWrap;
        view.ingredientTemplate.gameObject.SetActive(false);

        view.coins = GameUi.AddText(rect, "", 23f, GameUi.Ink, TextAlignmentOptions.BottomLeft, "Coins");
        Offsets(view.coins.rectTransform, Vector2.zero, new Vector2(0.55f, 0.28f), new Vector2(14f, 12f), Vector2.zero);
        view.canMake = GameUi.AddText(rect, "", 23f, GameUi.Ink, TextAlignmentOptions.BottomRight, "CanMake");
        view.canMake.fontStyle = FontStyles.Bold;
        Offsets(view.canMake.rectTransform, new Vector2(0.45f, 0f), new Vector2(1f, 0.28f), new Vector2(0f, 12f), new Vector2(-14f, 0f));

        RectTransform tape = GameUi.NewRect("Wanted", rect);
        GameUi.Place(tape, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(4f, 2f), new Vector2(150f, 34f));
        tape.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
        GameUi.AddPanel(tape, new Color(0.89f, 0.75f, 0.29f)).raycastTarget = false;
        view.wanted = tape.gameObject;
        view.wantedText = GameUi.AddText(tape, "", 22f, GameUi.Ink, TextAlignmentOptions.Center, "Nudge");
        view.wantedText.enableAutoSizing = true;
        view.wantedText.fontSizeMin = 15f;
        view.wantedText.fontSizeMax = 22f;
        view.wantedText.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform tag = GameUi.NewRect("OnMenuTag", rect);
        GameUi.Place(tag, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(6f, 0f), new Vector2(94f, 32f));
        GameUi.AddPanel(tag, new Color(0.18f, 0.32f, 0.19f)).raycastTarget = false;
        GameUi.AddText(tag, "on menu", 19f, GameUi.Cream, TextAlignmentOptions.Center, "BadgeText");
        view.onMenuTag = tag.gameObject;

        rect.gameObject.SetActive(false);
        return view;
    }

    private static void Offsets(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
