using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Build mode's screen: a strip along the top saying what to do, with Done, the tray of placeables along the
/// bottom (with category tabs), buttons that follow the held item (Place, Flip, Put away or Cancel), and a red
/// label saying why it can't go somewhere. Driven by <see cref="BuildMode"/>.
///
/// How it looks is in Resources/UI/BuildMode.prefab (see docs/UI.md), made from <see cref="BuildDefault"/>,
/// which is also used if there's no prefab. Move and restyle anything: the camera keeps the floor clear of
/// whatever <see cref="_topBar"/> and <see cref="_tray"/> cover, wherever they are.
/// </summary>
public class BuildUi : MonoBehaviour
{
    public const string ResourcePath = "UI/BuildMode";
    private const string AllTab = "All";

    [Header("Along the top")]
    [SerializeField] private RectTransform _topBar;
    [SerializeField] private TextMeshProUGUI _hint;
    [SerializeField] private Button _doneButton;

    [Header("The tray")]
    [SerializeField] private RectTransform _tray;
    [SerializeField] private ItemGridView _grid;
    [Tooltip("Copied for each category with something in it.")]
    [SerializeField] private RectTransform _tabRow;
    [SerializeField] private Button _tabTemplate;
    [SerializeField] private Color _tabColor = GameUi.Cream;
    [SerializeField] private Color _tabTextColor = GameUi.Ink;
    [SerializeField] private Color _chosenTabColor = new(0.18f, 0.32f, 0.19f);
    [SerializeField] private Color _chosenTabTextColor = GameUi.Cream;

    [Header("With something held")]
    [Tooltip("Follows the held thing, above it (or below it at the back).")]
    [SerializeField] private RectTransform _actions;
    [SerializeField] private Button _placeButton;
    [SerializeField] private Button _flipButton;
    [SerializeField] private Button _removeButton;
    [Tooltip("Why it can't go there. Sits just above the buttons.")]
    [SerializeField] private RectTransform _reason;
    [SerializeField] private TextMeshProUGUI _reasonText;

    [Header("Short messages")]
    [SerializeField] private RectTransform _toast;
    [SerializeField] private TextMeshProUGUI _toastText;

    private BuildMode _mode;
    private Coroutine _toastRoutine;
    private string _tab = AllTab;

    private RectTransform Root => (RectTransform)transform;

    public static BuildUi Create(BuildMode mode)
    {
        BuildUi ui = GameUi.LoadPopup(ResourcePath, BuildDefault);
        ui._mode = mode;
        ui._doneButton.onClick.AddListener(mode.Exit);
        ui._placeButton.onClick.AddListener(mode.Confirm);
        ui._flipButton.onClick.AddListener(mode.Flip);
        ui._removeButton.onClick.AddListener(() =>
        {
            if (mode.CandidateIsNew) mode.Cancel();
            else mode.PutAway();
        });
        ui._toast.gameObject.SetActive(false);
        return ui;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        if (GameSession.Current != null)
        {
            GameSession.Current.Events.InventoryChanged -= Refresh;
            GameSession.Current.Events.InventoryChanged += Refresh;
        }
        Refresh();
    }

    public void Hide()
    {
        if (GameSession.Current != null) GameSession.Current.Events.InventoryChanged -= Refresh;
        if (_toastRoutine != null) StopCoroutine(_toastRoutine);
        _toastRoutine = null;
        _toast.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    /// <summary>How much of the screen's height the top strip (plus a margin) covers, from the top: 0-1.</summary>
    public float TopCoverFraction => CoverFraction(_topBar, true, 10f, 0.1f);

    /// <summary>How much of the screen's height the tray (plus a margin) covers, from the bottom: 0-1.</summary>
    public float BottomCoverFraction => CoverFraction(_tray, false, 30f, 0.3f);

    /// <summary>From the screen's edge to the far edge of <paramref name="part"/>, as a share of the screen's height.</summary>
    private static float CoverFraction(RectTransform part, bool fromTop, float margin, float fallback)
    {
        Canvas.ForceUpdateCanvases();
        var canvas = (RectTransform)GameUi.Current.Canvas.transform;
        float canvasHeight = canvas.rect.height;
        if (!part || canvasHeight <= 0f) return fallback;
        // The safe area can be inset from the screen's edges (phones), so measure from the canvas.
        float edge = canvas.TransformPoint(new Vector3(0f, fromTop ? canvas.rect.yMax : canvas.rect.yMin, 0f)).y;
        float farSide = part.TransformPoint(new Vector3(0f, fromTop ? part.rect.yMin : part.rect.yMax, 0f)).y;
        float covered = Mathf.Abs(edge - farSide) / canvas.lossyScale.y;
        return Mathf.Clamp01((covered + margin) / canvasHeight);
    }

    private void OnDestroy()
    {
        if (GameSession.Current != null) GameSession.Current.Events.InventoryChanged -= Refresh;
    }

    public void Refresh()
    {
        if (!_mode || !gameObject.activeSelf) return;
        RefreshTabs();
        RefreshTray();
        RefreshCandidate();
    }

    public void RefreshCandidate()
    {
        if (!_mode) return;
        bool holding = _mode.HasCandidate;
        _actions.gameObject.SetActive(holding);
        if (!holding)
        {
            _reason.gameObject.SetActive(false);
            _hint.text = "Tap something below to place it, or tap something already out to move it. Pinch or scroll to zoom, and drag empty ground to look around.";
            return;
        }

        PlacementCheck check = _mode.CandidateCheck;
        GameUi.SetLabel(_placeButton, _mode.CandidateIsNew ? "Place" : "Done");
        _placeButton.interactable = check.ok;
        _flipButton.gameObject.SetActive(_mode.CandidateCanFlip);
        GameUi.SetLabel(_removeButton, _mode.CandidateIsNew ? "Cancel" : "Put away");
        _reason.gameObject.SetActive(!check.ok);
        _reasonText.text = check.reason ?? "";
        _hint.text = $"Drag the {(_mode.CandidateName ?? "item").ToLowerInvariant()} where you'd like it.";
    }

    public void Toast(string message)
    {
        if (string.IsNullOrEmpty(message) || !gameObject.activeInHierarchy) return;
        if (_toastRoutine != null) StopCoroutine(_toastRoutine);
        _toastRoutine = StartCoroutine(ShowToast(message));
    }

    private IEnumerator ShowToast(string message)
    {
        _toastText.text = message;
        _toast.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(2.2f);
        _toast.gameObject.SetActive(false);
        _toastRoutine = null;
    }

    private void LateUpdate()
    {
        if (!_mode || !_mode.HasCandidate) return;
        Camera camera = Camera.main;
        if (!camera) return;

        // Buttons go above the held item, so they're never under a dragging finger (which is below it).
        // If there's no room above (it's at the back), they go just below its base instead.
        RectTransform root = Root;
        Rect bounds = root.rect;
        Vector2 half = _actions.rect.size * 0.5f;
        float trayTop = LocalEdge(_tray, true);
        float topLimit = LocalEdge(_topBar, false) - 14f; // under the top strip
        Vector2 above = GameUi.Current.WorldToCanvas(_mode.CandidateTop, camera, root) + new Vector2(0f, half.y + 70f);
        Vector2 local = above;
        if (above.y + half.y > topLimit)
            local = GameUi.Current.WorldToCanvas(_mode.CandidateAnchor, camera, root) - new Vector2(0f, half.y + 20f);
        local.x = Mathf.Clamp(local.x, bounds.xMin + half.x + 16f, bounds.xMax - half.x - 16f);
        local.y = Mathf.Clamp(local.y, trayTop + half.y + 10f, topLimit - half.y - 70f);
        SetCentre(_actions, local);
        SetCentre(_reason, local + new Vector2(0f, half.y + _reason.rect.height * 0.5f + 10f));
    }

    /// <summary>The top (or bottom) edge of <paramref name="part"/>, in this screen's own space.</summary>
    private float LocalEdge(RectTransform part, bool top)
    {
        Vector3 world = part.TransformPoint(new Vector3(0f, top ? part.rect.yMax : part.rect.yMin, 0f));
        return Root.InverseTransformPoint(world).y;
    }

    /// <summary>Puts something's middle at a point in this screen's own space, however it's anchored.</summary>
    private void SetCentre(RectTransform part, Vector2 local)
    {
        Vector3 world = Root.TransformPoint(local);
        Vector3 middle = part.TransformPoint(part.rect.center);
        part.position += world - middle;
    }

    // ----- Contents -----

    private void RefreshTabs()
    {
        GameSession session = GameSession.Current;
        if (session == null) return;
        // Only tabs with something you can place in them.
        var categories = new List<string> { AllTab };
        foreach (PlaceableDefinition placeable in session.Database.placeables)
            if (placeable && IsShown(session, placeable) && !string.IsNullOrEmpty(placeable.category) && !categories.Contains(placeable.category))
                categories.Add(placeable.category);
        if (!categories.Contains(_tab)) _tab = AllTab;

        GameUi.ClearExcept(_tabRow, _tabTemplate.transform);
        foreach (string category in categories)
        {
            bool chosen = category == _tab;
            string tab = category;
            Button button = Instantiate(_tabTemplate, _tabRow);
            button.gameObject.SetActive(true);
            button.name = category;
            button.targetGraphic.color = chosen ? _chosenTabColor : _tabColor;
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.text = category;
            text.color = chosen ? _chosenTabTextColor : _tabTextColor;
            button.onClick.AddListener(() =>
            {
                _tab = tab;
                Refresh();
            });
        }
    }

    private void RefreshTray()
    {
        GameSession session = GameSession.Current;
        if (session == null) return;
        var cards = new List<ItemCard>();
        foreach (PlaceableDefinition placeable in session.Database.placeables.Where(p => p))
        {
            if (_tab != AllTab && placeable.category != _tab) continue;
            if (!IsShown(session, placeable)) continue;
            int available = session.Build.CountAvailable(placeable.id);
            bool unlimited = session.Build.UnlimitedItems;
            Sprite icon = ItemGridView.IconFor(placeable.item, placeable, out Color tint);
            PlaceableDefinition chosen = placeable;
            cards.Add(new ItemCard
            {
                key = placeable.id,
                title = string.IsNullOrEmpty(placeable.displayName) ? placeable.id : placeable.displayName,
                badge = unlimited ? "free" : "x" + available,
                icon = icon,
                iconTint = tint,
                dimmed = available <= 0,
                onClick = () => _mode.StartPlacing(chosen)
            });
        }
        if (cards.Count == 0)
            cards.Add(new ItemCard { key = "none", title = "Nothing to place yet", badge = "", dimmed = true, iconTint = GameUi.InkSoft });
        _grid.SetItems(cards);
    }

    /// <summary>
    /// The tray only shows what's in your bag, so unknown and rare things stay a surprise and the tray
    /// stays short as the list of placeables grows. (Placed things come back when put away.)
    /// Everything shows in unlimited mode, for testing.
    /// </summary>
    private static bool IsShown(GameSession session, PlaceableDefinition placeable) =>
        session.Build.UnlimitedItems || session.Build.CountAvailable(placeable.id) > 0;

    // ----- The default layout -----

    /// <summary>The shared pieces: torn paper for the strip and the tray, tan cards, Apron for the main action, dark for leaving.</summary>
    public static BuildUi BuildDefault(Transform parent)
    {
        RectTransform root = GameUi.NewRect("BuildMode", parent);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false);
        var ui = root.gameObject.AddComponent<BuildUi>();

        // Along the top: what to do, and Done.
        ui._topBar = GameUi.NewRect("PaperTop", root);
        GameUi.Anchor(ui._topBar, new Vector2(0f, 1f), Vector2.one, new Vector2(8f, -168f), new Vector2(-8f, 6f));
        GameUi.AddPanel(ui._topBar, GameUi.Panel2);
        ui._hint = GameUi.AddText(ui._topBar, "", 28f, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "Hint");
        GameUi.Anchor(ui._hint.rectTransform, Vector2.zero, Vector2.one, new Vector2(56f, 40f), new Vector2(-270f, -46f));
        ui._doneButton = GameUi.AddButton(ui._topBar, "Done", null, new Vector2(190f, 84f), GameUi.PanelDark, GameUi.Cream, 30f);
        ui._doneButton.name = "DoneButton";
        GameUi.Place((RectTransform)ui._doneButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-50f, -4f), new Vector2(190f, 84f));

        // The tray along the bottom.
        ui._tray = GameUi.NewRect("PaperTray", root);
        GameUi.Anchor(ui._tray, Vector2.zero, new Vector2(1f, 0f), new Vector2(8f, -6f), new Vector2(-8f, 380f));
        GameUi.AddPanel(ui._tray, GameUi.Panel2);

        ui._tabRow = GameUi.NewRect("Tabs", ui._tray);
        GameUi.Anchor(ui._tabRow, new Vector2(0f, 1f), Vector2.one, new Vector2(56f, -116f), new Vector2(-56f, -48f));
        GameUi.AddLayout<HorizontalLayoutGroup>(ui._tabRow, 8f).childAlignment = TextAnchor.MiddleLeft;
        ui._tabTemplate = GameUi.AddButton(ui._tabRow, "Tab", null, new Vector2(190f, 64f), GameUi.Cream, GameUi.Ink, 24f);
        ui._tabTemplate.name = "TabTemplate";
        ui._tabTemplate.gameObject.SetActive(false);

        RectTransform list = GameUi.NewRect("List", ui._tray);
        GameUi.Anchor(list, Vector2.zero, Vector2.one, new Vector2(40f, 40f), new Vector2(-40f, -122f));
        ui._grid = ItemGridView.Create(list, true, new Vector2(170f, 196f), true);

        // Buttons that follow the held item.
        ui._actions = GameUi.NewRect("Actions", root);
        GameUi.Place(ui._actions, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 100f));
        GameUi.AddLayout<HorizontalLayoutGroup>(ui._actions, 14f).childAlignment = TextAnchor.MiddleCenter;
        ui._placeButton = GameUi.AddButton(ui._actions, "Place", null, new Vector2(190f, 96f), GameUi.Apron, GameUi.Cream);
        ui._placeButton.name = "PlaceButton";
        ui._flipButton = GameUi.AddButton(ui._actions, "Flip", null, new Vector2(150f, 96f), GameUi.Cream, GameUi.Ink);
        ui._flipButton.name = "FlipButton";
        ui._removeButton = GameUi.AddButton(ui._actions, "Put away", null, new Vector2(210f, 96f), GameUi.Cream, GameUi.Ink);
        ui._removeButton.name = "RemoveButton";
        ui._actions.gameObject.SetActive(false);

        // Why it can't go here: red, for a problem.
        ui._reason = GameUi.NewRect("Reason", root);
        GameUi.Place(ui._reason, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 70f));
        GameUi.AddPanel(ui._reason, GameUi.Berry).raycastTarget = false;
        ui._reasonText = GameUi.AddText(ui._reason, "", 28f, GameUi.Cream);
        ui._reason.gameObject.SetActive(false);

        // Short messages, just above the tray.
        ui._toast = GameUi.NewRect("Toast", root);
        GameUi.Place(ui._toast, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 400f), new Vector2(900f, 70f));
        GameUi.AddPanel(ui._toast, GameUi.PanelDark).raycastTarget = false;
        ui._toastText = GameUi.AddText(ui._toast, "", 28f, GameUi.Cream);
        ui._toast.gameObject.SetActive(false);

        foreach (Button button in ui.GetComponentsInChildren<Button>(true)) UiFonts.UseBold(button.GetComponentInChildren<TextMeshProUGUI>());
        return ui;
    }
}
