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

        foreach (PlacedObjectData placed in session.Build.GetPlaced(AreaId)) Spawn(placed);
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
        instance.SetFlipped(placed.flipped);
        _spawned[placed.instanceId] = instance;
        return instance;
    }

    public void Despawn(int instanceId)
    {
        if (!_spawned.TryGetValue(instanceId, out PlacedObject instance)) return;
        _spawned.Remove(instanceId);
        if (instance) Destroy(instance.gameObject);
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
