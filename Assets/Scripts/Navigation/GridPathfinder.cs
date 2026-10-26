using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* over a <see cref="WalkableGrid"/> with 8-way movement (no cutting corners), then smoothed by
/// skipping waypoints the gecko can see past. Clicking a blocked or unreachable spot walks to the
/// closest point it can reach.
/// </summary>
[RequireComponent(typeof(WalkableGrid))]
public class GridPathfinder : MonoBehaviour, IPathfinder
{
    [SerializeField] private bool smoothPath = true;
    [SerializeField] private bool drawLastPath = true;

    private const float Diagonal = 1.41421356f;

    private static readonly Vector2Int[] Neighbours =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
    };

    private WalkableGrid _grid;
    private float[] _cost;
    private int[] _parent;
    private int[] _visitedStamp;
    private int _stamp;
    private readonly MinHeap _open = new();
    private readonly List<Vector2> _lastPath = new();

    public WalkableGrid Grid => _grid ? _grid : _grid = GetComponent<WalkableGrid>();

    public bool IsWalkable(Vector2 point) => Grid.IsWalkable(point);

    public bool TryGetNearestWalkable(Vector2 point, out Vector2 nearest)
    {
        if (Grid.IsWalkable(point))
        {
            nearest = point;
            return true;
        }

        bool found = Grid.TryFindNearestWalkable(point, out Vector2Int cell);
        nearest = found ? Grid.CellToWorld(cell) : point;
        return found;
    }

    /// <summary>For build mode: can the gecko get from one point to the other? A target within about a cell of the floor counts.</summary>
    public bool IsReachable(Vector2 from, Vector2 to)
    {
        if (!Grid.IsBuilt) Grid.Rebuild();
        if (!Grid.TryFindNearestWalkable(from, out Vector2Int start)) return false;
        if (!Grid.TryFindNearestWalkable(to, out Vector2Int goal)) return false;
        if (Vector2.Distance(Grid.CellToWorld(goal), to) > Mathf.Max(Grid.CellSize.x, Grid.CellSize.y) * 1.5f) return false;
        return Search(start, goal, out _);
    }

    public bool TryFindPath(Vector2 from, Vector2 to, List<Vector2> path)
    {
        path.Clear();
        if (!Grid.IsBuilt) Grid.Rebuild();
        if (!Grid.TryFindNearestWalkable(from, out Vector2Int start)) return false;

        // Aim for the exact point if it's walkable, otherwise the closest walkable cell.
        Vector2 goalPoint = to;
        Vector2Int goal = Grid.WorldToCell(to);
        if (!Grid.IsWalkable(goal))
        {
            // Prefer the gecko's side of whatever was clicked.
            if (!Grid.TryFindNearestWalkable(to, out goal, from)) return false;
            goalPoint = Grid.CellToWorld(goal);
        }

        bool startWasBlocked = !Grid.IsWalkable(from);
        Vector2 startPoint = startWasBlocked ? Grid.CellToWorld(start) : from;

        if (!Search(start, goal, out int reached))
        {
            // Unreachable (e.g. a fenced-off spot): walk to the closest cell we could reach.
            goal = CellOf(reached);
            goalPoint = Grid.CellToWorld(goal);
        }

        if (goal == start)
        {
            // Already as close as it gets: step onto the floor if off it, otherwise stay put
            // (exact points within the start cell are still walked to).
            if (startWasBlocked) path.Add(startPoint);
            else if (Grid.IsWalkable(to) && Grid.WorldToCell(to) == start) path.Add(to);
            RememberPath(from, path);
            return true;
        }

        var cells = new List<Vector2>();
        for (int index = IndexOf(goal); index != -1; index = _parent[index]) cells.Add(Grid.CellToWorld(CellOf(index)));
        cells.Reverse(); // start ... goal
        cells[cells.Count - 1] = goalPoint;
        if (cells.Count > 1 || startWasBlocked) cells[0] = startPoint;

        // cells[0] is the start; if the gecko was off the grid, walking onto it is the first step.
        if (startWasBlocked) path.Add(startPoint);
        if (smoothPath) Smooth(cells, path);
        else path.AddRange(cells.GetRange(1, cells.Count - 1));
        if (path.Count == 0) path.Add(goalPoint);

        RememberPath(from, path);
        return true;
    }

    private void RememberPath(Vector2 from, List<Vector2> path)
    {
        _lastPath.Clear();
        _lastPath.Add(from);
        _lastPath.AddRange(path);
    }

    /// <summary>String pulling: keep only the waypoints needed to walk around corners.</summary>
    private void Smooth(List<Vector2> cells, List<Vector2> path)
    {
        Vector2 anchor = cells[0];
        for (int i = 1; i < cells.Count - 1; i++)
        {
            if (!Grid.HasLineOfSight(anchor, cells[i + 1]))
            {
                path.Add(cells[i]);
                anchor = cells[i];
            }
        }
        path.Add(cells[cells.Count - 1]);
    }

    /// <summary>A*. Returns true if the goal was reached; otherwise <paramref name="closest"/> is the reached cell nearest the goal.</summary>
    private bool Search(Vector2Int start, Vector2Int goal, out int closest)
    {
        int count = Grid.Width * Grid.Height;
        if (_cost == null || _cost.Length != count)
        {
            _cost = new float[count];
            _parent = new int[count];
            _visitedStamp = new int[count];
        }

        _stamp++;
        _open.Clear();
        int startIndex = IndexOf(start);
        int goalIndex = IndexOf(goal);
        Visit(startIndex, 0f, -1);
        _open.Push(startIndex, Heuristic(start, goal));

        closest = startIndex;
        float closestH = Heuristic(start, goal);

        while (_open.Count > 0)
        {
            int current = _open.Pop();
            if (current == goalIndex) return true;

            Vector2Int cell = CellOf(current);
            float h = Heuristic(cell, goal);
            if (h < closestH)
            {
                closestH = h;
                closest = current;
            }

            for (int i = 0; i < Neighbours.Length; i++)
            {
                Vector2Int offset = Neighbours[i];
                Vector2Int next = cell + offset;
                if (!Grid.IsWalkable(next)) continue;
                bool diagonal = offset.x != 0 && offset.y != 0;
                if (diagonal && (!Grid.IsWalkable(new Vector2Int(cell.x + offset.x, cell.y)) ||
                                 !Grid.IsWalkable(new Vector2Int(cell.x, cell.y + offset.y))))
                    continue; // don't cut corners

                int nextIndex = IndexOf(next);
                float cost = _cost[current] + (diagonal ? Diagonal : 1f);
                if (_visitedStamp[nextIndex] == _stamp && cost >= _cost[nextIndex]) continue;

                Visit(nextIndex, cost, current);
                _open.Push(nextIndex, cost + Heuristic(next, goal));
            }
        }

        return false;
    }

    private void Visit(int index, float cost, int parent)
    {
        _visitedStamp[index] = _stamp;
        _cost[index] = cost;
        _parent[index] = parent;
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
        return Mathf.Max(dx, dy) + (Diagonal - 1f) * Mathf.Min(dx, dy); // octile distance
    }

    private int IndexOf(Vector2Int cell) => cell.y * Grid.Width + cell.x;
    private Vector2Int CellOf(int index) => new(index % Grid.Width, index / Grid.Width);

    private void OnDrawGizmos()
    {
        if (!drawLastPath || _lastPath.Count < 2) return;
        Gizmos.color = Color.cyan;
        for (int i = 1; i < _lastPath.Count; i++) Gizmos.DrawLine(_lastPath[i - 1], _lastPath[i]);
    }

    /// <summary>Small binary min-heap of cell indices. Stale entries are allowed and skipped by cost checks.</summary>
    private class MinHeap
    {
        private readonly List<(int index, float priority)> _items = new();
        public int Count => _items.Count;

        public void Clear() => _items.Clear();

        public void Push(int index, float priority)
        {
            _items.Add((index, priority));
            int i = _items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_items[parent].priority <= _items[i].priority) break;
                (_items[parent], _items[i]) = (_items[i], _items[parent]);
                i = parent;
            }
        }

        public int Pop()
        {
            int top = _items[0].index;
            int last = _items.Count - 1;
            _items[0] = _items[last];
            _items.RemoveAt(last);

            int i = 0;
            while (true)
            {
                int left = i * 2 + 1, right = left + 1, smallest = i;
                if (left < _items.Count && _items[left].priority < _items[smallest].priority) smallest = left;
                if (right < _items.Count && _items[right].priority < _items[smallest].priority) smallest = right;
                if (smallest == i) break;
                (_items[smallest], _items[i]) = (_items[i], _items[smallest]);
                i = smallest;
            }

            return top;
        }
    }
}
