using System.Collections;
using UnityEngine;

/// <summary>
/// On the root of every placed thing (and on build mode's ghost). Keeps its footprint collider in
/// line with its <see cref="PlaceableDefinition"/>, flips it, and tints it for build mode.
/// </summary>
public class PlacedObject : MonoBehaviour
{
    private SpriteRenderer[] _renderers;
    private Color[] _baseColors;
    private BoxCollider2D _footprint;
    private GameObject _footprintMarker;
    private SpriteRenderer _footprintMarkerRenderer;
    private GameObject _shadow;
    private Coroutine _plop;

    public int InstanceId { get; private set; }
    public PlaceableDefinition Definition { get; private set; }
    public bool Flipped { get; private set; }
    public string DisplayName => Definition && !string.IsNullOrEmpty(Definition.displayName) ? Definition.displayName : name;

    /// <summary>The footprint as a world rectangle (the base that blocks walking).</summary>
    public Rect FootprintRect => FootprintAt(Definition, transform.position);

    public static Rect FootprintAt(PlaceableDefinition definition, Vector2 basePosition)
    {
        Vector2 size = definition ? definition.footprintSize : new Vector2(1f, 0.4f);
        return new Rect(basePosition.x - size.x * 0.5f, basePosition.y, size.x, size.y);
    }

    public void Setup(PlaceableDefinition definition, int instanceId)
    {
        Definition = definition;
        InstanceId = instanceId;
        name = $"{(definition ? definition.id : "placeable")} #{instanceId}";

        int obstacle = LayerMask.NameToLayer("Obstacle");
        if (obstacle >= 0) gameObject.layer = obstacle;

        _footprint = GetComponent<BoxCollider2D>();
        if (!_footprint) _footprint = gameObject.AddComponent<BoxCollider2D>();
        Vector2 size = definition ? definition.footprintSize : new Vector2(1f, 0.4f);
        _footprint.size = size;
        _footprint.offset = new Vector2(0f, size.y * 0.5f);
        _footprint.isTrigger = false;

        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = _renderers[i].color;
    }

    public void SetFlipped(bool flipped)
    {
        Flipped = flipped;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (flipped ? -1f : 1f);
        transform.localScale = scale;
    }

    /// <summary>Off while it's being dragged, so it doesn't block itself.</summary>
    public void SetFootprintActive(bool active)
    {
        if (_footprint) _footprint.enabled = active;
    }

    /// <summary>The ghost look: a tint, a footprint outline on the floor and a soft shadow.</summary>
    public void ShowBuildState(bool valid)
    {
        Color tint = valid ? new Color(0.75f, 1f, 0.75f, 0.9f) : new Color(1f, 0.55f, 0.5f, 0.85f);
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (!_renderers[i]) continue;
            Color color = Color.Lerp(_baseColors[i], tint, 0.4f);
            color.a = _baseColors[i].a * tint.a;
            _renderers[i].color = color;
        }

        EnsureMarkers();
        _footprintMarker.SetActive(true);
        _shadow.SetActive(true);
        _footprintMarkerRenderer.color = valid ? new Color(0.35f, 0.85f, 0.4f, 0.45f) : new Color(0.95f, 0.3f, 0.25f, 0.5f);
    }

    public void ClearBuildState()
    {
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i]) _renderers[i].color = _baseColors[i];
        if (_footprintMarker) _footprintMarker.SetActive(false);
        if (_shadow) _shadow.SetActive(false);
    }

    /// <summary>A quick squash and settle when it's put down.</summary>
    public void PlayPlacedFeedback()
    {
        Transform visual = transform.Find("Visual");
        if (!visual) return;
        if (_plop != null) StopCoroutine(_plop);
        _plop = StartCoroutine(Plop(visual));
    }

    private IEnumerator Plop(Transform visual)
    {
        Vector3 rest = visual.localScale;
        const float duration = 0.25f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            float squash = Mathf.Sin(k * Mathf.PI) * 0.18f * (1f - k);
            visual.localScale = new Vector3(rest.x * (1f + squash), rest.y * (1f - squash), rest.z);
            yield return null;
        }
        visual.localScale = rest;
        _plop = null;
    }

    private void EnsureMarkers()
    {
        if (_footprintMarker) return;
        Vector2 size = Definition ? Definition.footprintSize : new Vector2(1f, 0.4f);

        _footprintMarker = new GameObject("FootprintMarker");
        _footprintMarker.transform.SetParent(transform, false);
        _footprintMarker.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
        _footprintMarker.transform.localScale = new Vector3(size.x, size.y, 1f);
        _footprintMarkerRenderer = _footprintMarker.AddComponent<SpriteRenderer>();
        _footprintMarkerRenderer.sprite = GeneratedSprites.Square;
        _footprintMarkerRenderer.sortingLayerName = "FloorDecals";
        _footprintMarkerRenderer.sortingOrder = 10;

        _shadow = new GameObject("Shadow");
        _shadow.transform.SetParent(transform, false);
        _shadow.transform.localPosition = new Vector3(0f, size.y * 0.35f, 0f);
        _shadow.transform.localScale = new Vector3(size.x * 1.15f, Mathf.Max(0.2f, size.y * 0.9f), 1f);
        var shadowRenderer = _shadow.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = GeneratedSprites.Circle;
        shadowRenderer.color = new Color(0.1f, 0.08f, 0.05f, 0.35f);
        shadowRenderer.sortingLayerName = "FloorDecals";
        shadowRenderer.sortingOrder = 11;
    }
}
