using System;
using System.Collections;
using System.Collections.Generic;
using Bush.Core;
using UnityEngine;

/// <summary>
/// Makes an area buildable (only the cafe for now): spawns what the save says is placed here, puts
/// down the starting layout on a new save, and gives build mode the floor, the grid and the spots that
/// must stay reachable. Its floor is the area's walkable floor, so reshaping that grows the build space.
/// Put it on World, next to the WalkableGrid.
/// </summary>
[RequireComponent(typeof(WalkableGrid))]
public class BuildArea : MonoBehaviour
{
    [Serializable]
    public struct StartingPlacement
    {
        public PlaceableDefinition placeable;
        public Vector2 position;
        public bool flipped;
    }

    [Tooltip("Saved with each placed object. Defaults to the scene name.")]
    [SerializeField] private string areaId;
    [Tooltip("Where placed objects go (World/Placed).")]
    [SerializeField] private Transform placedParent;
    [Tooltip("What a brand new save starts with here. Only used once per save.")]
    [SerializeField] private List<StartingPlacement> startingLayout = new();
    [Tooltip("Placed things must keep at least this far from stand points and spawn points.")]
    [SerializeField, Min(0f)] private float keepClearRadius = 0.35f;
    [Tooltip("How far the build camera may pan. Uses the size of a child called Backdrop if there is one.")]
    [SerializeField] private Rect viewBounds = new(-12f, -7.2f, 24f, 14.4f);

    private readonly Dictionary<int, PlacedObject> _spawned = new();
    private WalkableGrid _grid;
    private GridPathfinder _pathfinder;

    public static BuildArea Current { get; private set; }
    public static event Action<BuildArea> Loaded;

    public string AreaId => string.IsNullOrEmpty(areaId) ? gameObject.scene.name : areaId;
    public WalkableGrid Grid => _grid ? _grid : _grid = GetComponent<WalkableGrid>();
    public GridPathfinder Pathfinder => _pathfinder ? _pathfinder : _pathfinder = GetComponent<GridPathfinder>();
    public Transform PlacedParent => placedParent ? placedParent : transform;
    public float KeepClearRadius => keepClearRadius;

    /// <summary>What the build camera may show: the backdrop's edges, so you never see past the art.</summary>
    public Rect ViewBounds
    {
        get
        {
            Transform backdrop = transform.Find("Backdrop");
            var renderer = backdrop ? backdrop.GetComponent<SpriteRenderer>() : null;
            if (!renderer) return viewBounds;
            Bounds bounds = renderer.bounds;
            return new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        }
    }
    public IReadOnlyCollection<PlacedObject> Spawned => _spawned.Values;
    public bool IsLoaded { get; private set; }

    private void Awake()
    {
        Current = this;
        // Build mode and its UI come with the area, so a scene only needs this one component.
        if (!GetComponent<BuildMode>()) gameObject.AddComponent<BuildMode>();
    }

    private IEnumerator Start()
    {
        // The session is created by a bootstrap in the same scene; wait a frame if it isn't ready yet.
        while (!GameSession.Current) yield return null;
        GameSession session = GameSession.Current;

        var layout = new List<(string, float, float, bool)>();
        foreach (StartingPlacement start in startingLayout)
            if (start.placeable) layout.Add((start.placeable.id, start.position.x, start.position.y, start.flipped));
        session.Build.InitialiseArea(AreaId, layout);

        // Things on the ground first, so what sits on them has somewhere to go. Anything whose spot is gone
        // (the table's prefab changed) goes back to the bag rather than floating.
        var placedHere = new List<PlacedObjectData>(session.Build.GetPlaced(AreaId));
        foreach (PlacedObjectData placed in placedHere) if (placed.parentId == 0) Spawn(placed);
        foreach (PlacedObjectData placed in placedHere)
        {
            if (placed.parentId == 0) continue;
            if (Spawn(placed).transform.GetComponentInParent<SurfacePoint>()) continue;
            Despawn(placed.instanceId);
            session.Build.Remove(placed.instanceId);
        }
        RefreshChains();
        Grid.Rebuild();
        IsLoaded = true;
        Loaded?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    /// <summary>Creates the object for a saved placement.</summary>
    public PlacedObject Spawn(PlacedObjectData placed)
    {
        PlaceableDefinition definition = GameSession.Current.Database.Find<PlaceableDefinition>(placed.placeableId);
        PlacedObject instance = CreateInstance(definition, PlacedParent);
        instance.Setup(definition, placed.instanceId);
        instance.transform.position = new Vector3(placed.x, placed.y, 0f);
        if (placed.parentId != 0) SitOn(instance, FindSurface(placed.parentId, placed.slot));
        instance.SetFlipped(placed.flipped);
        _spawned[placed.instanceId] = instance;
        return instance;
    }

    /// <summary>Takes it out of the world, with anything sitting on it.</summary>
    public void Despawn(int instanceId)
    {
        if (!_spawned.TryGetValue(instanceId, out PlacedObject instance)) return;
        _spawned.Remove(instanceId);
        if (!instance) return;
        foreach (PlacedObject onTop in instance.GetComponentsInChildren<PlacedObject>(true))
            if (onTop != instance) _spawned.Remove(onTop.InstanceId);
        Destroy(instance.gameObject);
    }

    // ----- Surfaces -----

    /// <summary>The nearest spot within <paramref name="radius"/> that nothing but <paramref name="ignore"/> is on. Null if none.</summary>
    public SurfacePoint FreeSurfaceNear(Vector2 position, float radius, PlacedObject ignore = null)
    {
        SurfacePoint best = null;
        float bestDistance = radius;
        foreach (PlacedObject placed in Spawned)
        {
            if (!placed || placed == ignore) continue;
            foreach (SurfacePoint point in placed.GetComponentsInChildren<SurfacePoint>(true))
            {
                float distance = Vector2.Distance(position, point.transform.position);
                if (distance > bestDistance || point.IsTaken(ignore)) continue;
                best = point;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>Puts it on the spot, moving with it from then on; or, with no spot, loose in the world where it is.</summary>
    public void SitOn(PlacedObject placed, SurfacePoint point)
    {
        Vector3 position = placed.transform.position;
        placed.transform.SetParent(point ? point.transform : PlacedParent, false);
        placed.transform.position = point ? point.transform.position : position;
        placed.SetOnSurface(point);
    }

    private SurfacePoint FindSurface(int parentId, int slot)
    {
        PlacedObject parent = Get(parentId);
        if (!parent) return null;
        SurfacePoint[] points = parent.GetComponentsInChildren<SurfacePoint>(true);
        return slot >= 0 && slot < points.Length ? points[slot] : null;
    }

    // ----- Fairy lights -----

    /// <summary>Every run of fairy lights here, without <paramref name="ignore"/>.</summary>
    public List<ChainPiece> ChainPieces(PlacedObject ignore = null)
    {
        var pieces = new List<ChainPiece>();
        foreach (PlacedObject placed in Spawned)
            if (placed && placed != ignore && placed.Definition && placed.Definition.chainSpan > 0f)
                pieces.Add(PlacedObject.ChainPieceAt(placed.Definition, placed.transform.position));
        return pieces;
    }

    /// <summary>Shows and hides poles to match where everything is, counting a ghost that isn't placed yet.</summary>
    public void RefreshChains(PlacedObject ghost = null)
    {
        var all = new List<PlacedObject>(_spawned.Values);
        if (ghost && !all.Contains(ghost)) all.Add(ghost);
        foreach (PlacedObject placed in all)
        {
            if (!placed || !placed.Definition || placed.Definition.chainSpan <= 0f) continue;
            var others = new List<ChainPiece>();
            foreach (PlacedObject other in all)
                if (other && other != placed && other.Definition && other.Definition.chainSpan > 0f)
                    others.Add(PlacedObject.ChainPieceAt(other.Definition, other.transform.position));
            placed.SetChainJoins(ChainLights.Joins(PlacedObject.ChainPieceAt(placed.Definition, placed.transform.position), others).left);
        }
    }

    public PlacedObject Get(int instanceId) => _spawned.TryGetValue(instanceId, out PlacedObject instance) ? instance : null;

    /// <summary>A copy of the placeable's prefab (or a plain box if it has none yet), not yet saved.</summary>
    public static PlacedObject CreateInstance(PlaceableDefinition definition, Transform parent)
    {
        GameObject root;
        if (definition && definition.prefab) root = Instantiate(definition.prefab, parent);
        else root = CreateFallback(definition, parent);

        var placed = root.GetComponent<PlacedObject>();
        if (!placed) placed = root.AddComponent<PlacedObject>();
        return placed;
    }

    /// <summary>Stand points (the door, the counter...) and spawn points that placing must not cover or cut off.</summary>
    public IEnumerable<(Vector2 position, string label)> KeepClearSpots()
    {
        foreach (StandPoint stand in FindObjectsByType<StandPoint>())
        {
            Transform owner = stand.transform.parent;
            if (owner && owner.GetComponentInParent<PlacedObject>()) continue; // placed things don't protect themselves
            Npc guest = stand.GetComponentInParent<Npc>();
            if (guest && !guest.IsPresent) continue; // guests who aren't in today don't take up room
            if (!IsInThisArea(stand.transform.position)) continue;
            yield return (stand.transform.position, owner ? $"the {Friendly(owner.name).ToLowerInvariant()}" : "a stand point");
        }
        foreach (SpawnPoint spawn in FindObjectsByType<SpawnPoint>())
            if (IsInThisArea(spawn.transform.position)) yield return (spawn.transform.position, "where the gecko arrives");
    }

    /// <summary>Where paths are checked from: the default spawn point, else any spawn point, else the gecko, else the floor's middle.</summary>
    public Vector2 PathOrigin()
    {
        SpawnPoint fallback = null;
        foreach (SpawnPoint spawn in FindObjectsByType<SpawnPoint>())
        {
            if (!IsInThisArea(spawn.transform.position)) continue;
            if (spawn.IsDefault) return spawn.transform.position;
            if (!fallback) fallback = spawn;
        }
        if (fallback) return fallback.transform.position;
        if (Interactor.Player && IsInThisArea(Interactor.Player.transform.position)) return Interactor.Player.transform.position;
        return Grid.WalkableArea ? (Vector2)Grid.WalkableArea.bounds.center : (Vector2)transform.position;
    }

    /// <summary>"CafeDoor" or "Exit_Garden" as words a player would read: "Cafe Door", "Exit Garden".</summary>
    public static string Friendly(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return objectName;
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < objectName.Length; i++)
        {
            char c = objectName[i];
            if (c == '_' || c == '-') { text.Append(' '); continue; }
            if (i > 0 && char.IsUpper(c) && char.IsLower(objectName[i - 1])) text.Append(' ');
            text.Append(c);
        }
        return text.ToString().Trim();
    }

    /// <summary>Near this area's floor (exits sit just off its edge).</summary>
    private bool IsInThisArea(Vector2 position)
    {
        if (!Grid.WalkableArea) return true;
        Bounds bounds = Grid.WalkableArea.bounds;
        bounds.Expand(2f);
        return bounds.Contains(new Vector3(position.x, position.y, bounds.center.z));
    }

    private static GameObject CreateFallback(PlaceableDefinition definition, Transform parent)
    {
        var root = new GameObject(definition ? definition.id : "placeable");
        root.transform.SetParent(parent, false);
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        Vector2 size = definition ? definition.footprintSize : Vector2.one;
        visual.transform.localScale = new Vector3(size.x, Mathf.Max(0.6f, size.x), 1f);
        visual.transform.localPosition = new Vector3(0f, Mathf.Max(0.6f, size.x) * 0.5f, 0f);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = GeneratedSprites.Square;
        renderer.color = new Color(0.55f, 0.42f, 0.3f);
        renderer.sortingLayerName = "World";
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        return root;
    }
}
