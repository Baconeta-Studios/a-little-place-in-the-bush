using TMPro;
using UnityEngine;

/// <summary>
/// Shows that something can be used, without relying on hover (touch screens have none): a small
/// bobbing marker appears when the gecko is nearby, and the object lights up and shows its prompt
/// text while it's hovered (mouse) or being walked to. Leave <see cref="marker"/> empty to get a
/// simple generated marker; swap in real art later.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class InteractionPrompt : MonoBehaviour
{
    [Tooltip("Shown above the object. Created automatically if empty.")]
    [SerializeField] private GameObject marker;
    [SerializeField] private Vector2 markerOffset = new(0f, 1.6f);
    [Tooltip("Show the marker when the gecko is within this distance. 0 = always show.")]
    [SerializeField, Min(0f)] private float showDistance = 3.5f;
    [Tooltip("Show the prompt text under the marker while hovered or targeted.")]
    [SerializeField] private bool showPromptText = true;
    [Tooltip("Colour the object is blended towards while hovered or targeted.")]
    [SerializeField] private Color highlightColor = new(1f, 0.9f, 0.55f, 1f);
    [SerializeField, Range(0f, 1f)] private float highlightAmount = 0.35f;

    private static Sprite _defaultMarkerSprite;

    private Interactable _interactable;
    private SpriteRenderer[] _renderers;
    private Color[] _baseColors;
    private TMP_Text _label;
    private Vector3 _markerBase;

    private void Awake()
    {
        _interactable = GetComponent<Interactable>();
        _renderers = GetComponentsInChildren<SpriteRenderer>();
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = _renderers[i].color;

        if (!marker) marker = CreateDefaultMarker();
        _markerBase = marker.transform.localPosition;
        _label = marker.GetComponentInChildren<TMP_Text>(true);
        if (_label) _label.text = _interactable.Prompt;
    }

    private void OnEnable() => _interactable.HighlightChanged += OnHighlightChanged;
    private void OnDisable() => _interactable.HighlightChanged -= OnHighlightChanged;

    private void Update()
    {
        bool focused = _interactable.IsHovered || _interactable.IsTargeted;
        bool near = showDistance <= 0f || IsPlayerNear();
        bool show = (near || focused) && _interactable.CanInteract(new InteractionContext(Interactor.Player));
        if (marker.activeSelf != show) marker.SetActive(show);
        if (!show) return;

        float bob = Mathf.Sin(Time.unscaledTime * 3f) * 0.06f;
        marker.transform.localPosition = _markerBase + new Vector3(0f, bob, 0f);
        Unflip(marker.transform); // a flipped object would mirror the marker and its text

        bool wantText = focused && showPromptText;
        if (_label && _label.gameObject.activeSelf != wantText)
        {
            _label.text = _interactable.Prompt;
            _label.gameObject.SetActive(wantText);
        }
    }

    /// <summary>Keeps a child the right way round when a parent is flipped (negative X scale).</summary>
    public static void Unflip(Transform child)
    {
        if (child.lossyScale.x >= 0f) return;
        Vector3 scale = child.localScale;
        scale.x = -scale.x;
        child.localScale = scale;
    }

    private bool IsPlayerNear()
    {
        Interactor player = Interactor.Player;
        return player && Vector2.Distance(player.transform.position, transform.position) <= showDistance;
    }

    private void OnHighlightChanged(bool hovered, bool targeted)
    {
        bool lit = hovered || targeted;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (!_renderers[i] || _renderers[i].transform.IsChildOf(marker.transform)) continue;
            // Blend rather than multiply, so white art visibly lights up too.
            Color highlighted = Color.Lerp(_baseColors[i], highlightColor, highlightAmount);
            highlighted.a = _baseColors[i].a;
            _renderers[i].color = lit ? highlighted : _baseColors[i];
        }
    }

    private GameObject CreateDefaultMarker()
    {
        var root = new GameObject("PromptMarker");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = markerOffset;

        // A cream dot with a dark rim, drawn in code so it needs no art or font glyphs.
        var dot = root.AddComponent<SpriteRenderer>();
        dot.sprite = DefaultMarkerSprite();
        dot.sortingLayerID = SortingLayer.NameToID("Foreground");
        dot.sortingOrder = 100;

        var labelObject = new GameObject("PromptText");
        labelObject.transform.SetParent(root.transform, false);
        // Above the dot (not over it, and not over the character's head), growing upwards.
        labelObject.transform.localPosition = new Vector3(0f, 0.28f, 0f);
        var text = labelObject.AddComponent<TextMeshPro>();
        text.fontSize = 3f;
        text.alignment = TextAlignmentOptions.Bottom;
        text.rectTransform.pivot = new Vector2(0.5f, 0f);
        text.color = new Color(1f, 0.96f, 0.85f);
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(40, 30, 20, 255);
        text.rectTransform.sizeDelta = new Vector2(5f, 1f);
        text.sortingLayerID = SortingLayer.NameToID("Foreground");
        text.sortingOrder = 101;
        labelObject.SetActive(false);

        root.SetActive(false);
        return root;
    }

    private static Sprite DefaultMarkerSprite()
    {
        if (_defaultMarkerSprite) return _defaultMarkerSprite;

        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var fill = new Color(1f, 0.95f, 0.8f, 1f);
        var rim = new Color(0.16f, 0.12f, 0.08f, 1f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                Color colour = distance < 11f ? fill : distance < 15f ? rim : Color.clear;
                if (distance >= 15f && distance < 16f) colour = new Color(rim.r, rim.g, rim.b, 16f - distance); // soft edge
                texture.SetPixel(x, y, colour);
            }
        }
        texture.Apply();
        _defaultMarkerSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _defaultMarkerSprite;
    }
}
