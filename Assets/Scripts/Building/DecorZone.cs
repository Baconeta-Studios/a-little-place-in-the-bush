using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground past the walkable floor where decorations can go too: the grass and edges the gecko can't reach.
/// Only things you just look at count as decorations (see <see cref="PlaceableDefinition.IsDecoration"/>):
/// seats and tables still need the floor, so guests and the gecko can get to them. Draw its shape with the
/// PolygonCollider2D. It doesn't block walking or clicks. Tools > Bush > Add Decor Zone To This Scene makes
/// one a little bigger than the floor, to adjust.
/// </summary>
[RequireComponent(typeof(PolygonCollider2D))]
public class DecorZone : MonoBehaviour
{
    private static readonly List<DecorZone> Zones = new();

    private PolygonCollider2D _shape;

    private PolygonCollider2D Shape => _shape ? _shape : _shape = GetComponent<PolygonCollider2D>();

    private void Reset() => GetComponent<PolygonCollider2D>().isTrigger = true;
    private void OnEnable() => Zones.Add(this);
    private void OnDisable() => Zones.Remove(this);

    /// <summary>Whether the point is inside any decor zone.</summary>
    public static bool Contains(Vector2 point)
    {
        foreach (DecorZone zone in Zones)
            if (zone && zone.Shape.OverlapPoint(point)) return true;
        return false;
    }

    /// <summary>Grows <paramref name="bounds"/> to take in every decor zone.</summary>
    public static void Encapsulate(ref Bounds bounds)
    {
        foreach (DecorZone zone in Zones)
            if (zone) bounds.Encapsulate(zone.Shape.bounds);
    }
}
