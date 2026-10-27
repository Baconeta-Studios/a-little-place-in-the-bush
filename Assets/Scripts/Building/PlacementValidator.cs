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
/// Build mode's rules for where a placeable can go:
/// on the open ground, not overlapping anything, not covering a stand point or spawn point, and not
/// cutting off the gecko's path to any of them. Floor placement only, for now.
/// </summary>
public static class PlacementValidator
{
    private static readonly Collider2D[] Overlaps = new Collider2D[16];

    /// <param name="ignore">The object being moved (its own footprint is switched off while dragging).</param>
    public static PlacementCheck Check(BuildArea area, PlaceableDefinition definition, Vector2 basePosition, PlacedObject ignore = null)
    {
        if (!area || !definition) return PlacementCheck.Invalid("Nothing to place.");
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
            string label = other ? other.DisplayName : hit.gameObject.name;
            return PlacementCheck.Invalid($"It's too close to the {label.ToLowerInvariant()}.");
        }

        // 3. Not covering somewhere the gecko needs to stand or arrive.
        Rect keepClear = new(footprint.xMin - area.KeepClearRadius, footprint.yMin - area.KeepClearRadius,
            footprint.width + area.KeepClearRadius * 2f, footprint.height + area.KeepClearRadius * 2f);
        foreach ((Vector2 position, string label) in area.KeepClearSpots())
            if (keepClear.Contains(position)) return PlacementCheck.Invalid($"It would be in the way of {label}.");

        // 4. Not cutting off a path: from the spawn point, every stand point must still be reachable.
        GridPathfinder pathfinder = area.Pathfinder;
        if (pathfinder)
        {
            Vector2 origin = area.PathOrigin();
            grid.SetTemporaryBlock(footprint);
            try
            {
                foreach ((Vector2 position, string label) in area.KeepClearSpots())
                {
                    if (Vector2.Distance(position, origin) < 0.01f) continue;
                    if (!pathfinder.IsReachable(origin, position)) return PlacementCheck.Invalid($"It would block the path to {label}.");
                }
            }
            finally
            {
                grid.SetTemporaryBlock(null);
            }
        }

        return PlacementCheck.Valid;
    }
}
