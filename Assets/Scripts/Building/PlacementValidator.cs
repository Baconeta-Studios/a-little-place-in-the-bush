using System.Collections.Generic;
using Bush.Core;
using UnityEngine;

/// <summary>The answer to "can this go here?", with a reason the player can read when it can't.</summary>
public readonly struct PlacementCheck
{
    public readonly bool ok;
    public readonly string reason;

    private PlacementCheck(bool ok, string reason)
    {
        this.ok = ok;
        this.reason = reason;
    }

    public static PlacementCheck Valid => new(true, null);
    public static PlacementCheck Invalid(string reason) => new(false, reason);
}

/// <summary>
/// What the checks need to know about the area, worked out once when something is picked up rather
/// than on every drag: the spots to keep clear, where paths start, and which spots can be reached
/// right now (so a spot that's already unreachable doesn't make every placement fail).
/// </summary>
public class PlacementContext
{
    public readonly List<(Vector2 position, string label)> keepClear = new();
    public readonly List<(Vector2 position, string label)> mustStayReachable = new();
    public Vector2 pathOrigin;

    public static PlacementContext For(BuildArea area)
    {
        var context = new PlacementContext { pathOrigin = area.PathOrigin() };
        context.keepClear.AddRange(area.KeepClearSpots());
        GridPathfinder pathfinder = area.Pathfinder;
        foreach ((Vector2 position, string label) spot in context.keepClear)
        {
            if (Vector2.Distance(spot.position, context.pathOrigin) < 0.01f) continue;
            if (pathfinder && pathfinder.IsReachable(context.pathOrigin, spot.position)) context.mustStayReachable.Add(spot);
        }
        return context;
    }
}

/// <summary>
/// Build mode's rules for where a placeable can go: Floor placement only for now, on the open ground,
/// not overlapping anything, not covering a stand point or spawn point, and not cutting off the gecko's
/// path to any of them.
/// </summary>
public static class PlacementValidator
{
    private static readonly Collider2D[] Overlaps = new Collider2D[16];

    /// <param name="ignore">The object being moved (its own footprint is switched off while dragging).</param>
    /// <param name="context">From <see cref="PlacementContext.For"/>; worked out here if not given.</param>
    public static PlacementCheck Check(BuildArea area, PlaceableDefinition definition, Vector2 basePosition,
        PlacedObject ignore = null, PlacementContext context = null)
    {
        if (!area || !definition) return PlacementCheck.Invalid("Nothing to place.");
        if (definition.placement != PlacementType.Floor)
            return PlacementCheck.Invalid($"{definition.placement} placement isn't built yet.");

        context ??= PlacementContext.For(area);
        WalkableGrid grid = area.Grid;
        Rect footprint = PlacedObject.FootprintAt(definition, basePosition);

        // 1. On the open ground: every corner and the middle of the base inside the floor.
        Collider2D floor = grid.WalkableArea;
        const float inset = 0.02f;
        Vector2[] corners =
        {
            new(footprint.xMin + inset, footprint.yMin + inset), new(footprint.xMax - inset, footprint.yMin + inset),
            new(footprint.xMin + inset, footprint.yMax - inset), new(footprint.xMax - inset, footprint.yMax - inset),
            footprint.center
        };
        foreach (Vector2 corner in corners)
            if (!floor || !floor.OverlapPoint(corner)) return PlacementCheck.Invalid("It needs to go on the open ground.");

        // 2. Not overlapping another footprint.
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(LayerMask.GetMask("Obstacle"));
        int count = Physics2D.OverlapBox(footprint.center, footprint.size - Vector2.one * 0.02f, 0f, filter, Overlaps);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = Overlaps[i];
            if (ignore && hit.transform.IsChildOf(ignore.transform)) continue;
            var other = hit.GetComponentInParent<PlacedObject>();
            string label = other ? other.DisplayName : BuildArea.Friendly(hit.gameObject.name);
            return PlacementCheck.Invalid($"It's too close to the {label.ToLowerInvariant()}.");
        }

        // 3. Not covering somewhere the gecko needs to stand or arrive.
        float clear = area.KeepClearRadius;
        Rect keepClear = new(footprint.xMin - clear, footprint.yMin - clear, footprint.width + clear * 2f, footprint.height + clear * 2f);
        foreach ((Vector2 position, string label) in context.keepClear)
            if (keepClear.Contains(position)) return PlacementCheck.Invalid($"It would be in the way of {label}.");

        // 4. Not cutting off a path: spots the gecko can reach now must stay reachable from the spawn point.
        GridPathfinder pathfinder = area.Pathfinder;
        if (pathfinder && context.mustStayReachable.Count > 0)
        {
            grid.SetTemporaryBlock(footprint);
            try
            {
                foreach ((Vector2 position, string label) in context.mustStayReachable)
                    if (!pathfinder.IsReachable(context.pathOrigin, position)) return PlacementCheck.Invalid($"It would block the path to {label}.");
            }
            finally
            {
                grid.SetTemporaryBlock(null);
            }
        }

        return PlacementCheck.Valid;
    }
}
