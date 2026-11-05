using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One page on the menu board: a dish, a free space to drop one, or a slot you haven't bought yet.
/// Drag a dish off to take it down, or onto another page to swap them (<see cref="CafePopup"/> does the rest).
/// </summary>
public class MenuSlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image background;
    [SerializeField] private Image page;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Button remove;
    [SerializeField] private Button locked;
    [SerializeField] private Color dishColor = new(0.83f, 0.78f, 0.7f);
    [SerializeField] private Color freeColor = new(0.08f, 0.07f, 0.05f, 0.45f);
    [SerializeField] private Color lockedColor = new(0.08f, 0.07f, 0.05f, 0.28f);
    [SerializeField] private Color hotColor = new(0.89f, 0.75f, 0.29f);

    private CafePopup _owner;
    private int _slot;
    private string _recipeId;
    private Color _restColor;

    public bool IsLocked { get; private set; }
    public RectTransform Rect => (RectTransform)transform;

    private void Awake()
    {
        remove.onClick.AddListener(() => _owner.TakeOffMenu(_recipeId));
        locked.onClick.AddListener(() => _owner.OpenUpgrades());
    }

    public void ShowDish(CafePopup owner, int slot, string recipeId, string name, Sprite pageSprite)
    {
        Set(owner, slot, recipeId, false, dishColor);
        page.gameObject.SetActive(pageSprite);
        page.sprite = pageSprite;
        label.text = name;
        label.color = GameUi.Ink;
        label.fontStyle = FontStyles.Bold;
        remove.gameObject.SetActive(true);
    }

    public void ShowFree(CafePopup owner, int slot)
    {
        Set(owner, slot, null, false, freeColor);
        page.gameObject.SetActive(false);
        label.text = "Drop a dish here";
        label.color = GameUi.Cream;
        label.fontStyle = FontStyles.Normal;
        remove.gameObject.SetActive(false);
    }

    public void ShowLocked(CafePopup owner, int slot, string text)
    {
        Set(owner, slot, null, true, lockedColor);
        page.gameObject.SetActive(false);
        label.text = text;
        label.color = new Color(1f, 0.98f, 0.92f, 0.75f);
        label.fontStyle = FontStyles.Normal;
        remove.gameObject.SetActive(false);
    }

    private void Set(CafePopup owner, int slot, string recipeId, bool isLocked, Color color)
    {
        _owner = owner;
        _slot = slot;
        _recipeId = recipeId;
        IsLocked = isLocked;
        _restColor = color;
        background.color = color;
        locked.enabled = isLocked; // it's on the page itself
    }

    public void SetHot(bool hot) => background.color = hot ? hotColor : _restColor;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(_recipeId)) return;
        _owner.BeginDrag(_recipeId, _slot, eventData);
    }

    public void OnDrag(PointerEventData eventData) => _owner.Drag(eventData);

    public void OnEndDrag(PointerEventData eventData) => _owner.EndDrag(eventData);

    public static MenuSlotView Build(Transform parent, int index)
    {
        RectTransform rect = GameUi.NewRect("Slot" + index, parent);
        rect.gameObject.AddComponent<LayoutElement>().preferredWidth = 190f;
        var view = rect.gameObject.AddComponent<MenuSlotView>();
        view.background = GameUi.AddPanel(rect, view.freeColor);

        RectTransform string_ = GameUi.NewRect("String", rect);
        GameUi.Place(string_, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, -2f), new Vector2(3f, 26f));
        string_.gameObject.AddComponent<Image>().color = new Color(0.8f, 0.72f, 0.58f);
        string_.GetComponent<Image>().raycastTarget = false;

        RectTransform pageRect = GameUi.NewRect("Page", rect);
        pageRect.anchorMin = new Vector2(0.2f, 0.42f);
        pageRect.anchorMax = new Vector2(0.8f, 0.94f);
        pageRect.offsetMin = pageRect.offsetMax = Vector2.zero;
        view.page = pageRect.gameObject.AddComponent<Image>();
        view.page.preserveAspect = true;
        view.page.raycastTarget = false;

        view.label = GameUi.AddText(rect, "", 25f, GameUi.Ink, TextAlignmentOptions.Center, "DishName");
        view.label.rectTransform.anchorMin = new Vector2(0f, 0.04f);
        view.label.rectTransform.anchorMax = new Vector2(1f, 0.42f);
        view.label.rectTransform.offsetMin = new Vector2(10f, 0f);
        view.label.rectTransform.offsetMax = new Vector2(-10f, 0f);

        view.locked = rect.gameObject.AddComponent<Button>();
        view.locked.transition = Selectable.Transition.None;
        view.remove = GameUi.AddButton(rect, "x", null, new Vector2(46f, 46f), new Color(0.22f, 0.2f, 0.16f), GameUi.Cream, 24f);
        GameUi.Place((RectTransform)view.remove.transform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-4f, -4f), new Vector2(46f, 46f));
        return view;
    }
}
