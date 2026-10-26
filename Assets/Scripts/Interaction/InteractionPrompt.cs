using TMPro;
using UnityEngine;

/// <summary>
/// Shows that something can be used, without relying on hover (touch screens have none): a small
/// bobbing marker appears when the gecko is nearby, and the object brightens while it's hovered
/// (mouse) or being walked to. Leave <see cref="marker"/> empty to get a simple text marker.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class InteractionPrompt : MonoBehaviour
{
    [Tooltip("Shown above the object. Created automatically if empty.")]
    [SerializeField] private GameObject marker;
    [SerializeField] private Vector2 markerOffset = new(0f, 1.6f);
    [Tooltip("Show the marker when the gecko is within this distance. 0 = always show.")]
    [SerializeField, Min(0f)] private float showDistance = 3.5f;
    [Tooltip("Write the prompt text under the marker while hovered or targeted.")]
    [SerializeField] private bool showPromptText = true;
    [SerializeField] private Color highlightTint = new(1.15f, 1.15f, 1.05f, 1f);

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
        _label = marker.GetComponentInChildren<TMP_Text>();
    }

    private void OnEnable() => _interactable.HighlightChanged += OnHighlightChanged;
    private void OnDisable() => _interactable.HighlightChanged -= OnHighlightChanged;

    private void Update()
    {
        bool focused = _interactable.IsHovered || _interactable.IsTargeted;
        bool near = showDistance <= 0f || IsPlayerNear();
        bool show = _interactable.InteractionEnabled && (near || focused);
        if (marker.activeSelf != show) marker.SetActive(show);
        if (!show) return;

        float bob = Mathf.Sin(Time.unscaledTime * 3f) * 0.06f;
        marker.transform.localPosition = _markerBase + new Vector3(0f, bob, 0f);
        if (_label) _label.text = focused && showPromptText ? $"▼\n<size=60%>{_interactable.Prompt}</size>" : "▼";
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
            _renderers[i].color = lit ? _baseColors[i] * highlightTint : _baseColors[i];
        }
    }

    private GameObject CreateDefaultMarker()
    {
        var root = new GameObject("PromptMarker");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = markerOffset;

        var text = root.AddComponent<TextMeshPro>();
        text.text = "▼";
        text.fontSize = 4f;
        text.alignment = TextAlignmentOptions.Bottom;
        text.color = new Color(1f, 0.96f, 0.85f);
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(40, 30, 20, 255);
        text.rectTransform.sizeDelta = new Vector2(4f, 1.5f);
        text.sortingLayerID = SortingLayer.NameToID("Foreground");
        text.sortingOrder = 100;
        root.SetActive(false);
        return root;
    }
}
