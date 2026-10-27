using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Build mode's screen: a top bar with Done, the tray of placeables along the bottom (with category
/// tabs), buttons that follow the held item (Place, Flip, Put away or Cancel), and a red label saying
/// why it can't go somewhere. Built in code; driven by <see cref="BuildMode"/>.
/// </summary>
public class BuildUi : MonoBehaviour
{
    private const string AllTab = "All";
    private const float TrayHeight = 330f;

    private BuildMode _mode;
    private RectTransform _root;
    private ItemGridView _tray;
    private RectTransform _tabs;
    private TextMeshProUGUI _hint;
    private RectTransform _actions;
    private Button _placeButton;
    private Button _flipButton;
    private Button _removeButton;
    private RectTransform _reason;
    private TextMeshProUGUI _reasonText;
    private RectTransform _toast;
    private TextMeshProUGUI _toastText;
    private Coroutine _toastRoutine;
    private string _tab = AllTab;

    public static BuildUi Create(BuildMode mode)
    {
        GameUi ui = GameUi.Current;
        RectTransform root = GameUi.NewRect("BuildMode", ui.SafeRoot);
        GameUi.Stretch(root);
        var buildUi = root.gameObject.AddComponent<BuildUi>();
        buildUi._mode = mode;
        buildUi._root = root;
        buildUi.Build();
        root.gameObject.SetActive(false);
        return buildUi;
    }

    public void Show()
    {
        _root.gameObject.SetActive(true);
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
        _root.gameObject.SetActive(false);
    }

    /// <summary>How much of the screen's height the tray (plus a margin) covers, from the bottom: 0-1.</summary>
    public float BottomCoverFraction
    {
        get
        {
            Canvas.ForceUpdateCanvases();
            var canvas = (RectTransform)GameUi.Current.Canvas.transform;
            float canvasHeight = canvas.rect.height;
            if (canvasHeight <= 0f) return 0.3f;
            // The safe area can start above the screen's bottom edge (phones), so measure from the canvas.
            float safeBottom = _root.TransformPoint(new Vector3(0f, _root.rect.yMin, 0f)).y;
            float canvasBottom = canvas.TransformPoint(new Vector3(0f, canvas.rect.yMin, 0f)).y;
            float safeOffset = (safeBottom - canvasBottom) / canvas.lossyScale.y;
            return Mathf.Clamp01((safeOffset + 16f + TrayHeight + 30f) / canvasHeight);
        }
    }

    private void OnDestroy()
    {
        if (GameSession.Current != null) GameSession.Current.Events.InventoryChanged -= Refresh;
    }

    public void Refresh()
    {
        if (!_root || !_root.gameObject.activeSelf) return;
        RefreshTabs();
        RefreshTray();
        RefreshCandidate();
    }

    public void RefreshCandidate()
    {
        bool holding = _mode.HasCandidate;
        _actions.gameObject.SetActive(holding);
        if (!holding)
        {
            _reason.gameObject.SetActive(false);
            _hint.text = "Build mode. Tap an item below to place it, or tap something already out to move it.";
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
        if (string.IsNullOrEmpty(message)) return;
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
        if (!_mode.HasCandidate) return;
        Camera camera = Camera.main;
        if (!camera) return;

        // Buttons go above the held item, so they're never under a dragging finger (which is below it).
        // If there's no room above (it's at the back), they go just below its base instead.
        Rect bounds = _root.rect;
        Vector2 half = _actions.sizeDelta * 0.5f;
        float trayTop = bounds.yMin + 16f + TrayHeight;
        float topLimit = bounds.yMax - 140f; // under the top bar
        Vector2 above = GameUi.Current.WorldToCanvas(_mode.CandidateTop, camera, _root) + new Vector2(0f, half.y + 70f);
        Vector2 local = above;
        if (above.y + half.y > topLimit)
            local = GameUi.Current.WorldToCanvas(_mode.CandidateAnchor, camera, _root) - new Vector2(0f, half.y + 20f);
        local.x = Mathf.Clamp(local.x, bounds.xMin + half.x + 16f, bounds.xMax - half.x - 16f);
        local.y = Mathf.Clamp(local.y, trayTop + half.y + 10f, topLimit - half.y - 70f);
        _actions.anchoredPosition = local;
        _reason.anchoredPosition = local + new Vector2(0f, half.y + 45f);
    }

    // ----- Contents -----

    private void RefreshTabs()
    {
        GameSession session = GameSession.Current;
        if (session == null) return;
        var categories = new List<string> { AllTab };
        foreach (PlaceableDefinition placeable in session.Database.placeables)
            if (placeable && !string.IsNullOrEmpty(placeable.category) && !categories.Contains(placeable.category)) categories.Add(placeable.category);
        if (!categories.Contains(_tab)) _tab = AllTab;

        for (int i = _tabs.childCount - 1; i >= 0; i--) Destroy(_tabs.GetChild(i).gameObject);
        foreach (string category in categories)
        {
            bool selected = category == _tab;
            string tab = category;
            GameUi.AddButton(_tabs, category, () => { _tab = tab; Refresh(); }, new Vector2(200f, 64f),
                selected ? GameUi.Moss : GameUi.Cream, selected ? GameUi.Cream : GameUi.Ink, 28f);
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
        _tray.SetItems(cards);
    }

    // ----- Layout -----

    private void Build()
    {
        // Top bar: what to do, and Done.
        RectTransform top = GameUi.NewRect("TopBar", _root);
        top.anchorMin = new Vector2(0f, 1f);
        top.anchorMax = new Vector2(1f, 1f);
        top.pivot = new Vector2(0.5f, 1f);
        top.offsetMin = new Vector2(16f, -126f);
        top.offsetMax = new Vector2(-16f, -16f);
        GameUi.AddPanel(top, GameUi.PanelDark);
        _hint = GameUi.AddText(top, "", 30f, GameUi.Cream, TextAlignmentOptions.MidlineLeft, "Hint");
        _hint.rectTransform.offsetMin = new Vector2(32f, 0f);
        _hint.rectTransform.offsetMax = new Vector2(-240f, 0f);
        Button done = GameUi.AddButton(top, "Done", () => _mode.Exit(), new Vector2(200f, 84f), GameUi.Moss, GameUi.Cream, 34f);
        GameUi.Place((RectTransform)done.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(200f, 84f));

        // The tray along the bottom.
        RectTransform tray = GameUi.NewRect("Tray", _root);
        tray.anchorMin = new Vector2(0f, 0f);
        tray.anchorMax = new Vector2(1f, 0f);
        tray.pivot = new Vector2(0.5f, 0f);
        tray.offsetMin = new Vector2(16f, 16f);
        tray.offsetMax = new Vector2(-16f, 16f + TrayHeight);
        GameUi.AddPanel(tray, GameUi.Panel);

        _tabs = GameUi.NewRect("Tabs", tray);
        _tabs.anchorMin = new Vector2(0f, 1f);
        _tabs.anchorMax = new Vector2(1f, 1f);
        _tabs.pivot = new Vector2(0.5f, 1f);
        _tabs.offsetMin = new Vector2(16f, -78f);
        _tabs.offsetMax = new Vector2(-16f, -10f);
        HorizontalLayoutGroup tabRow = GameUi.AddLayout<HorizontalLayoutGroup>(_tabs, 12f);
        tabRow.childAlignment = TextAnchor.MiddleLeft;

        RectTransform list = GameUi.NewRect("List", tray);
        list.anchorMin = Vector2.zero;
        list.anchorMax = Vector2.one;
        list.offsetMin = new Vector2(8f, 8f);
        list.offsetMax = new Vector2(-8f, -84f);
        _tray = ItemGridView.Create(list, true, new Vector2(190f, 210f));

        // Buttons that follow the held item.
        _actions = GameUi.NewRect("Actions", _root);
        GameUi.Place(_actions, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 110f));
        HorizontalLayoutGroup row = GameUi.AddLayout<HorizontalLayoutGroup>(_actions, 14f);
        row.childAlignment = TextAnchor.MiddleCenter;
        _placeButton = GameUi.AddButton(_actions, "Place", () => _mode.Confirm(), new Vector2(190f, 100f), GameUi.Moss, GameUi.Cream);
        _flipButton = GameUi.AddButton(_actions, "Flip", () => _mode.Flip(), new Vector2(160f, 100f), GameUi.Cream, GameUi.Ink);
        _removeButton = GameUi.AddButton(_actions, "Cancel", () =>
        {
            if (_mode.CandidateIsNew) _mode.Cancel();
            else _mode.PutAway();
        }, new Vector2(220f, 100f), GameUi.Cream, GameUi.Berry);

        // Why it can't go here.
        _reason = GameUi.NewRect("Reason", _root);
        GameUi.Place(_reason, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 70f));
        GameUi.AddPanel(_reason, GameUi.Berry).raycastTarget = false;
        _reasonText = GameUi.AddText(_reason, "", 28f, GameUi.Cream);

        // Short messages above the tray.
        _toast = GameUi.NewRect("Toast", _root);
        GameUi.Place(_toast, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f + TrayHeight), new Vector2(900f, 70f));
        GameUi.AddPanel(_toast, GameUi.PanelDark).raycastTarget = false;
        _toastText = GameUi.AddText(_toast, "", 28f, GameUi.Cream);
        _toast.gameObject.SetActive(false);
    }
}
