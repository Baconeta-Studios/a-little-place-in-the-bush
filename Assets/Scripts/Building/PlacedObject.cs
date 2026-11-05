using System.Collections;
using System.Collections.Generic;
using Bush.Core;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// On the root of every placed thing (and on build mode's ghost). Keeps its footprint collider in
/// line with its <see cref="PlaceableDefinition"/>, flips it, and tints it for build mode. Only Floor
/// things have a footprint. Things that have <see cref="SurfacePoint"/>s sort as one group, so what's
/// on them always draws on top of them.
/// </summary>
public class PlacedObject : MonoBehaviour
{
    private SpriteRenderer[] _renderers;
    private Color[] _baseColors;
    private int[] _baseOrders;
    private readonly List<BoxCollider2D> _footprints = new();
    private readonly List<GameObject> _footprintMarkers = new();
    private readonly List<SpriteRenderer> _footprintMarkerRenderers = new();
    private GameObject _ghostShadow;
    private bool _hasGroundShadow;
    private Transform _poleLeft;
    private Transform _poleRight;
    private Coroutine _plop;
    private Transform _visual;
    private Vector3 _visualRestScale;

    public int InstanceId { get; private set; }
    public PlaceableDefinition Definition { get; private set; }
    public bool Flipped { get; private set; }
    public string DisplayName => Definition && !string.IsNullOrEmpty(Definition.displayName) ? Definition.displayName : name;

    /// <summary>The footprint as a world rectangle (the base that blocks walking; for fairy lights, from pole to pole).</summary>
    public Rect FootprintRect => FootprintAt(Definition, transform.position);

    public static Rect FootprintAt(PlaceableDefinition definition, Vector2 basePosition)
    {
        Vector2 size = definition ? definition.footprintSize : new Vector2(1f, 0.4f);
        float width = size.x + (definition ? definition.chainSpan : 0f);
        return new Rect(basePosition.x - width * 0.5f, basePosition.y, width, size.y);
    }

    /// <summary>What blocks walking: the base, or for fairy lights one at each pole (left, then right).</summary>
    public static Rect[] FootprintsAt(PlaceableDefinition definition, Vector2 basePosition)
    {
        float span = definition ? definition.chainSpan : 0f;
        if (span <= 0f) return new[] { FootprintAt(definition, basePosition) };
        Vector2 size = definition.footprintSize;
        return new[]
        {
            new Rect(basePosition.x - span * 0.5f - size.x * 0.5f, basePosition.y, size.x, size.y),
            new Rect(basePosition.x + span * 0.5f - size.x * 0.5f, basePosition.y, size.x, size.y)
        };
    }

    public static ChainPiece ChainPieceAt(PlaceableDefinition definition, Vector2 basePosition) =>
        new(basePosition.x, basePosition.y, definition ? definition.chainSpan : 0f);

    public void Setup(PlaceableDefinition definition, int instanceId)
    {
        Definition = definition;
        InstanceId = instanceId;
        name = $"{(definition ? definition.id : "placeable")} #{instanceId}";

        SetUpFootprints(definition);

        _visual = transform.Find("Visual");
        if (_visual) _visualRestScale = _visual.localScale;
        _poleLeft = transform.Find("PoleLeft");
        _poleRight = transform.Find("PoleRight");

        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _baseColors = new Color[_renderers.Length];
        _baseOrders = new int[_renderers.Length];
        bool onWall = definition && definition.placement == PlacementType.Wall;
        for (int i = 0; i < _renderers.Length; i++)
        {
            _baseColors[i] = _renderers[i].color;
            if (onWall) // the tree is part of the backdrop: draw just above it, behind everything in the world
            {
                _renderers[i].sortingLayerName = "Background";
                _renderers[i].sortingOrder += WallSortingOrder;
            }
            _baseOrders[i] = _renderers[i].sortingOrder;
        }

        SetUpShadow(definition);

        if (GetComponentInChildren<SurfacePoint>(true) && !GetComponent<SortingGroup>())
            gameObject.AddComponent<SortingGroup>().sortingLayerName = "World";
    }

    /// <summary>After the renderers are collected, so build mode's tint leaves it alone.</summary>
    private void SetUpShadow(PlaceableDefinition definition)
    {
        if (!definition || definition.placement != PlacementType.Floor || definition.chainSpan > 0f) return;
        _hasGroundShadow = true;
        if (transform.Find("Shadow")) return; // its own art
        bool hasArt = System.Array.Exists(_renderers, r => r && r.sprite);
        if (definition.groundShadow && hasArt) GroundShadow.Create(transform, definition.ShadowSize, definition.ShadowCentre, definition.shadowStrength);
        else _hasGroundShadow = false;
    }

    private const int WallSortingOrder = 10;
    private const int OnSurfaceSortingOrder = 10;

    private void SetUpFootprints(PlaceableDefinition definition)
    {
        var boxes = new List<BoxCollider2D>(GetComponents<BoxCollider2D>());
        _footprints.Clear();
        if (definition && definition.placement != PlacementType.Floor)
        {
            foreach (BoxCollider2D box in boxes) box.enabled = false;
            return;
        }

        int obstacle = LayerMask.NameToLayer("Obstacle");
        if (obstacle >= 0) gameObject.layer = obstacle;
        foreach (Rect rect in FootprintsAt(definition, Vector2.zero))
        {
            int index = _footprints.Count;
            BoxCollider2D box = index < boxes.Count ? boxes[index] : gameObject.AddComponent<BoxCollider2D>();
            box.enabled = true;
            box.size = rect.size;
            box.offset = rect.center;
            box.isTrigger = false;
            _footprints.Add(box);
        }
        for (int i = _footprints.Count; i < boxes.Count; i++) boxes[i].enabled = false;
    }

    /// <summary>A Surface thing draws over what it's sitting on.</summary>
    public void SetOnSurface(bool onSurface)
    {
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i]) _renderers[i].sortingOrder = _baseOrders[i] + (onSurface ? OnSurfaceSortingOrder : 0);
    }

    /// <summary>Hides the pole at an end that shares a neighbour's: the piece on the left keeps the shared pole.</summary>
    public void SetChainJoins(bool joinedOnLeft)
    {
        Transform left = Flipped ? _poleRight : _poleLeft;
        Transform right = Flipped ? _poleLeft : _poleRight;
        if (left) left.gameObject.SetActive(!joinedOnLeft);
        if (right) right.gameObject.SetActive(true);
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
        foreach (BoxCollider2D footprint in _footprints) footprint.enabled = active;
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

        if (_footprints.Count == 0) return; // the tint is enough for things off the floor

        EnsureMarkers();
        foreach (GameObject marker in _footprintMarkers) marker.SetActive(true);
        if (_ghostShadow) _ghostShadow.SetActive(true);
        foreach (SpriteRenderer marker in _footprintMarkerRenderers)
            marker.color = valid ? new Color(0.35f, 0.85f, 0.4f, 0.45f) : new Color(0.95f, 0.3f, 0.25f, 0.5f);
    }

    public void ClearBuildState()
    {
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i]) _renderers[i].color = _baseColors[i];
        foreach (GameObject marker in _footprintMarkers) if (marker) marker.SetActive(false);
        if (_ghostShadow) _ghostShadow.SetActive(false);
    }

    /// <summary>A quick squash and settle when it's put down.</summary>
    public void PlayPlacedFeedback()
    {
        if (!_visual) return;
        if (_plop != null) StopCoroutine(_plop);
        _plop = StartCoroutine(Plop(_visual));
    }

    private IEnumerator Plop(Transform visual)
    {
        Vector3 rest = _visualRestScale;
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
        if (_footprintMarkers.Count > 0) return;
        Vector2 size = Definition ? Definition.footprintSize : new Vector2(1f, 0.4f);

        foreach (Rect rect in FootprintsAt(Definition, Vector2.zero))
        {
            var marker = new GameObject("FootprintMarker");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = rect.center;
            marker.transform.localScale = new Vector3(rect.width, rect.height, 1f);
            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = GeneratedSprites.Square;
            renderer.sortingLayerName = "FloorDecals";
            renderer.sortingOrder = 10;
            _footprintMarkers.Add(marker);
            _footprintMarkerRenderers.Add(renderer);
        }

        if (_hasGroundShadow) return; // it already has one
        _ghostShadow = new GameObject("GhostShadow");
        _ghostShadow.transform.SetParent(transform, false);
        _ghostShadow.transform.localPosition = new Vector3(0f, size.y * 0.35f, 0f);
        _ghostShadow.transform.localScale = new Vector3(size.x * 1.15f, Mathf.Max(0.2f, size.y * 0.9f), 1f);
        var shadowRenderer = _ghostShadow.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = GeneratedSprites.Circle;
        shadowRenderer.color = new Color(0.1f, 0.08f, 0.05f, 0.35f);
        shadowRenderer.sortingLayerName = "FloorDecals";
        shadowRenderer.sortingOrder = 11;
    }
}
