using System;
using UnityEngine;

/// <summary>
/// Splits the World <see cref="Grid"/> into cells and works out which ones the gecko can stand on:
/// inside the WalkableArea polygon and not overlapping an Obstacle footprint. Green/red gizmos show
/// the result when the object is selected. Call <see cref="Rebuild"/> after placing or moving props.
/// </summary>
[RequireComponent(typeof(Grid))]
public class WalkableGrid : MonoBehaviour
{
    [Tooltip("Trigger PolygonCollider2D outlining the floor (World/Floor/WalkableArea).")]
    [SerializeField] private Collider2D walkableArea;
    [SerializeField] private LayerMask obstacleMask;
    [Tooltip("Extra space kept around obstacle footprints, so the gecko doesn't clip through props.")]
    [SerializeField, Min(0)] private float clearance = 0.1f;
    [SerializeField] private bool alwaysDrawGizmos;

    private Grid _grid;
    private bool[] _walkable;
    private Vector3Int _minCell;

    public event Action Rebuilt;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public Vector2 CellSize => GridComponent.cellSize;
    public bool IsBuilt => _walkable != null;
    public Collider2D WalkableArea => walkableArea;

    private Grid GridComponent => _grid ? _grid : _grid = GetComponent<Grid>();

    private void Reset()
    {
        int obstacle = LayerMask.NameToLayer("Obstacle");
        if (obstacle >= 0) obstacleMask = 1 << obstacle;
    }

    private void Awake()
    {
        if (Application.isPlaying) Rebuild();
    }

    public void Rebuild()
    {
        if (!walkableArea)
        {
            Debug.LogWarning($"[WalkableGrid] No walkable area set on {name}.", this);
            _walkable = null;
            return;
        }

        Physics2D.SyncTransforms();
        Bounds bounds = walkableArea.bounds;
        _minCell = GridComponent.WorldToCell(bounds.min);
        Vector3Int maxCell = GridComponent.WorldToCell(bounds.max);
        Width = maxCell.x - _minCell.x + 1;
        Height = maxCell.y - _minCell.y + 1;
        _walkable = new bool[Width * Height];

        Vector2 probe = CellSize + Vector2.one * (clearance * 2f);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                Vector2 center = CellToWorld(new Vector2Int(x, y));
                bool inside = walkableArea.OverlapPoint(center);
                bool blocked = inside && Physics2D.OverlapBox(center, probe, 0f, obstacleMask);
                _walkable[y * Width + x] = inside && !blocked;
            }
        }

        Rebuilt?.Invoke();
    }

    public bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;

    public bool IsWalkable(Vector2Int cell) => IsBuilt && InBounds(cell) && _walkable[cell.y * Width + cell.x];

    public bool IsWalkable(Vector2 world) => IsWalkable(WorldToCell(world));

    /// <summary>Grid-local cell (0,0 is the bottom-left of the walkable area's bounds). May be out of bounds.</summary>
    public Vector2Int WorldToCell(Vector2 world)
    {
        Vector3Int cell = GridComponent.WorldToCell(world);
        return new Vector2Int(cell.x - _minCell.x, cell.y - _minCell.y);
    }

    public Vector2 CellToWorld(Vector2Int cell) =>
        GridComponent.GetCellCenterWorld(new Vector3Int(cell.x + _minCell.x, cell.y + _minCell.y, 0));

    /// <summary>
    /// The walkable cell closest to <paramref name="world"/>. Cells within about one cell of the closest
    /// count as a tie, and ties go to the one nearest <paramref name="preferNear"/> (pass the gecko's
    /// position), so clicking the base of a prop stops on the gecko's side of it rather than behind it.
    /// Points far outside the floor (e.g. a tap near the top of a tall phone screen) still work.
    /// </summary>
    public bool TryFindNearestWalkable(Vector2 world, out Vector2Int nearest, Vector2? preferNear = null)
    {
        nearest = default;
        if (!IsBuilt) return false;

        Vector2Int origin = WorldToCell(world);
        if (IsWalkable(origin))
        {
            nearest = origin;
            return true;
        }

        // A plain scan: areas are a few thousand cells at most, and this is exact for any point,
        // including ones far outside the floor (a tap near the top of a tall phone screen).
        float best = float.MaxValue;
        for (int i = 0; i < _walkable.Length; i++)
        {
            if (!_walkable[i]) continue;
            float distance = Vector2.Distance(CellToWorld(new Vector2Int(i % Width, i / Width)), world);
            if (distance < best) best = distance;
        }
        if (best == float.MaxValue) return false;

        // Cells within about a cell of the closest count as a tie; ties go to the one nearest the gecko.
        float tieMargin = Mathf.Min(CellSize.x, CellSize.y) * 1.1f;
        float bestScore = float.MaxValue;
        for (int i = 0; i < _walkable.Length; i++)
        {
            if (!_walkable[i]) continue;
            var cell = new Vector2Int(i % Width, i / Width);
            Vector2 center = CellToWorld(cell);
            float distance = Vector2.Distance(center, world);
            if (distance > best + tieMargin) continue;
            float score = preferNear.HasValue ? Vector2.Distance(center, preferNear.Value) : distance;
            if (score < bestScore)
            {
                bestScore = score;
                nearest = cell;
            }
        }
        return true;
    }

    /// <summary>True if a straight walk from a to b only crosses walkable cells.</summary>
    public bool HasLineOfSight(Vector2 a, Vector2 b)
    {
        float step = Mathf.Min(CellSize.x, CellSize.y) * 0.25f;
        float distance = Vector2.Distance(a, b);
        int samples = Mathf.Max(1, Mathf.CeilToInt(distance / step));
        for (int i = 0; i <= samples; i++)
        {
            if (!IsWalkable(Vector2.Lerp(a, b, i / (float)samples))) return false;
        }
        return true;
    }

    private float _lastEditorBuild = -1f;

    private void OnValidate()
    {
        if (Application.isPlaying && IsBuilt) Rebuild(); // e.g. tweaking Clearance while playing
        else _walkable = null;
    }

    private void OnDrawGizmos()
    {
        if (alwaysDrawGizmos) DrawCells();
    }

    private void OnDrawGizmosSelected()
    {
        if (!alwaysDrawGizmos) DrawCells();
    }

    private void DrawCells()
    {
        if (!walkableArea) return;
        // In edit mode, rebuild at most twice a second so the gizmo follows props as they're moved.
        if (!Application.isPlaying && (!IsBuilt || Time.realtimeSinceStartup - _lastEditorBuild > 0.5f))
        {
            _lastEditorBuild = Time.realtimeSinceStartup;
            Rebuild();
        }
        if (!IsBuilt) return;

        Vector3 size = new Vector3(CellSize.x, CellSize.y, 0f) * 0.9f;
        var walkable = new Color(0.2f, 0.9f, 0.3f, 0.25f);
        var blocked = new Color(0.95f, 0.2f, 0.2f, 0.25f);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var cell = new Vector2Int(x, y);
                Vector2 center = CellToWorld(cell);
                bool isWalkable = _walkable[y * Width + x];
                if (!isWalkable && !walkableArea.OverlapPoint(center)) continue; // only show cells inside the floor
                Gizmos.color = isWalkable ? walkable : blocked;
                Gizmos.DrawCube(center, size);
            }
        }
    }
}
