using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The game's own screen-space UI (HUD buttons, build tray, bag), built in code for the prototype so
/// nothing needs wiring in the scene. One per scene, under the template popups (which draw at 100).
/// <see cref="SafeRoot"/> stays inside the phone's safe area. The HUD and the forage screen can be
/// restyled as prefabs (see docs/UI.md); the helpers here make their default layouts.
/// </summary>
public class GameUi : MonoBehaviour
{
    // A small bush palette, shared by everything built here.
    public static readonly Color Panel = new(0.95f, 0.92f, 0.84f, 0.97f);
    public static readonly Color Panel2 = new(0.91f, 0.86f, 0.75f);
    public static readonly Color PanelDark = new(0.22f, 0.2f, 0.15f, 0.92f);
    public static readonly Color Apron = new(0.64f, 0.24f, 0.11f);
    public static readonly Color Ink = new(0.2f, 0.16f, 0.11f);
    public static readonly Color InkSoft = new(0.42f, 0.36f, 0.28f);
    public static readonly Color Moss = new(0.33f, 0.52f, 0.28f);
    public static readonly Color MossLight = new(0.86f, 0.92f, 0.8f);
    public static readonly Color Bark = new(0.55f, 0.4f, 0.26f);
    public static readonly Color Berry = new(0.75f, 0.27f, 0.22f);
    public static readonly Color Cream = new(1f, 0.98f, 0.92f);
    // Dark enough for the pale line work on the item icons to show.
    public static readonly Color CardBack = new(0.73f, 0.63f, 0.47f);
    public static readonly Color CardSelected = new(0.55f, 0.68f, 0.45f);
    public static readonly Color Tape = new(0.89f, 0.75f, 0.29f);

    private static GameUi _current;
    private RectTransform _safeRoot;
    private Rect _appliedSafeArea;

    public static GameUi Current => _current ? _current : _current = Create();
    public Canvas Canvas { get; private set; }
    /// <summary>Full screen minus notches and rounded corners. Put HUD things here.</summary>
    public RectTransform SafeRoot => _safeRoot;

    private static GameUi Create()
    {
        var root = new GameObject("[GameUI]", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var ui = root.AddComponent<GameUi>();
        ui.Canvas = root.GetComponent<Canvas>();
        ui.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        ui.Canvas.sortingOrder = 50;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        ui._safeRoot = NewRect("SafeArea", root.transform);
        Stretch(ui._safeRoot);
        ui.ApplySafeArea();
        return ui;
    }

    private void Update() => ApplySafeArea();

    private void OnDestroy()
    {
        if (_current == this) _current = null;
    }

    private void ApplySafeArea()
    {
        Rect safe = Screen.safeArea;
        if (safe == _appliedSafeArea || Screen.width == 0 || Screen.height == 0) return;
        _appliedSafeArea = safe;
        _safeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        _safeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        _safeRoot.offsetMin = _safeRoot.offsetMax = Vector2.zero;
    }

    // ----- Small builders, so the screens read clearly -----

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public static Image AddPanel(RectTransform rect, Color color, bool rounded = true)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        if (rounded)
        {
            image.sprite = GeneratedSprites.RoundedPanel;
            image.type = Image.Type.Sliced;
        }
        return image;
    }

    public static TextMeshProUGUI AddText(Transform parent, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center, string name = "Text")
    {
        RectTransform rect = NewRect(name, parent);
        Stretch(rect);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        UiFonts.Apply(label);
        return label;
    }

    /// <summary>A rounded button with a text label. Big enough for thumbs by default (110 px tall at 1080p).</summary>
    public static Button AddButton(Transform parent, string label, UnityAction onClick, Vector2 size, Color background, Color textColor, float fontSize = 34f)
    {
        RectTransform rect = NewRect(label + "Button", parent);
        rect.sizeDelta = size;
        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;
        Image image = AddPanel(rect, background);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(onClick);
        AddText(rect, label, fontSize, textColor);
        return button;
    }

    public static void SetLabel(Button button, string label)
    {
        var text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text) text.text = label;
    }

    /// <summary>A row or column that lays its children out with gaps.</summary>
    public static T AddLayout<T>(RectTransform rect, float spacing, RectOffset padding = null) where T : HorizontalOrVerticalLayoutGroup
    {
        var layout = rect.gameObject.AddComponent<T>();
        layout.spacing = spacing;
        layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return layout;
    }

    /// <summary>Copies a screen's prefab, closed, or builds its default layout if there's no prefab.</summary>
    public static T LoadPopup<T>(string resourcePath, Func<Transform, T> buildDefault) where T : Component
    {
        Transform safeRoot = Current.SafeRoot;
        var prefab = Resources.Load<T>(resourcePath);
        if (!prefab) return buildDefault(safeRoot);

        // A popup is "open" while active, and the prefab is saved on so it can be seen and edited:
        // copy it under something switched off, so it arrives closed without ever opening.
        var closed = new GameObject("Closed");
        closed.SetActive(false);
        T popup = Instantiate(prefab, closed.transform);
        popup.gameObject.SetActive(false);
        popup.transform.SetParent(safeRoot, false);
        Destroy(closed);
        return popup;
    }

    /// <summary>The painted backdrop with a dim over it, behind a full screen.</summary>
    public static void AddBackdrop(RectTransform root)
    {
        RectTransform backdrop = NewRect("Backdrop", root);
        Stretch(backdrop);
        AddPanel(backdrop, new Color(0.3f, 0.27f, 0.18f), false);
        RectTransform dim = NewRect("Dim", root);
        Stretch(dim);
        AddPanel(dim, new Color(0.08f, 0.07f, 0.05f, 0.42f), false);
    }

    /// <summary>The way out of a screen: always Back, always bottom right.</summary>
    public static Button AddBack(Transform parent)
    {
        Button back = AddButton(parent, "Back", null, new Vector2(190f, 84f), PanelDark, Cream, 30f);
        back.GetComponent<LayoutElement>().ignoreLayout = true;
        Place((RectTransform)back.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 18f), new Vector2(190f, 84f));
        return back;
    }

    /// <summary>The hanging sign that names a board, at the top of its column.</summary>
    public static void AddSign(RectTransform column, string title)
    {
        RectTransform sign = NewRect("Sign", column);
        Place(sign, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 34f), new Vector2(456f, 456f));
        AddPanel(sign, Bark, false).raycastTarget = false;
        TextMeshProUGUI text = AddText(column, title, 50f, Ink, TextAlignmentOptions.Center, "SignTitle");
        Anchor(text.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -262f), new Vector2(0f, -112f));
    }

    /// <summary>A vertical scrolling area, which drags on empty space too. Add the layout to what it hands back.</summary>
    public static ScrollRect AddScroll(RectTransform area, out RectTransform content)
    {
        var scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        RectTransform viewport = NewRect("Viewport", area);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        scroll.viewport = viewport;
        content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        scroll.content = content;
        return scroll;
    }

    public static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    /// <summary>Empties something made from a template, leaving the template.</summary>
    public static void ClearExcept(Transform parent, Transform keep)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child == keep) continue;
            // Off the layout now, rather than at the end of the frame.
            child.gameObject.SetActive(false);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    /// <summary>Converts a world point to a position inside <paramref name="rect"/>'s parent space.</summary>
    public Vector2 WorldToCanvas(Vector3 world, Camera camera, RectTransform parent)
    {
        Vector2 screen = camera.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 local);
        return local;
    }
}
