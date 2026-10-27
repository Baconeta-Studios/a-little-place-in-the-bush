using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finds walking paths on an area's floor. <see cref="GridPathfinder"/> is the jam version; this
/// interface is here so it can be swapped for something else (e.g. NavMeshPlus) later.
/// </summary>
public interface IPathfinder
{
    /// <summary>
    /// Fills <paramref name="path"/> with waypoints from <paramref name="from"/> (not included) towards
    /// <paramref name="to"/>. If the target is blocked or unreachable, the path ends at the closest
    /// walkable point instead. Returns false only if there's nowhere to walk at all.
    /// </summary>
    bool TryFindPath(Vector2 from, Vector2 to, List<Vector2> path);

    /// <summary>True if the gecko could walk all the way from one point to the other.</summary>
    bool IsReachable(Vector2 from, Vector2 to);

    bool IsWalkable(Vector2 point);

    /// <summary>The closest point the gecko can stand on.</summary>
    bool TryGetNearestWalkable(Vector2 point, out Vector2 nearest);
}
