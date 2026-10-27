using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The game's own screen-space UI (HUD buttons, build tray, bag), built in code for the prototype so
/// nothing needs wiring in the scene. One per scene, under the template popups (which draw at 100).
/// <see cref="SafeRoot"/> stays inside the phone's safe area. Restyle or swap for prefabs later.
/// </summary>
public class GameUi : MonoBehaviour
{
    // A small bush palette, shared by everything built here.
    public static readonly Color Panel = new(0.95f, 0.92f, 0.84f, 0.97f);
    public static readonly Color PanelDark = new(0.22f, 0.2f, 0.15f, 0.92f);
    public static readonly Color Ink = new(0.2f, 0.16f, 0.11f);
    public static readonly Color InkSoft = new(0.42f, 0.36f, 0.28f);
    public static readonly Color Moss = new(0.33f, 0.52f, 0.28f);
    public static readonly Color MossLight = new(0.86f, 0.92f, 0.8f);
    public static readonly Color Bark = new(0.55f, 0.4f, 0.26f);
    public static readonly Color Berry = new(0.75f, 0.27f, 0.22f);
    public static readonly Color Cream = new(1f, 0.98f, 0.92f);

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
        TextMeshProUGUI text = AddText(rect, label, fontSize, textColor);
        text.fontStyle = FontStyles.Bold;
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
