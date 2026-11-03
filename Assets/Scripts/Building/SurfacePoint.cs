using UnityEngine;

/// <summary>
/// A spot on a placeable where a small thing can sit (a table top, for a pot plant or a little umbrella).
/// Things with Surface placement snap to these. Put them as children of the placeable prefab's root (not
/// inside Visual), where the small thing's base should touch: one per spot.
/// </summary>
public class SurfacePoint : MonoBehaviour
{
    public PlacedObject Owner => GetComponentInParent<PlacedObject>();

    /// <summary>Which spot this is on its owner, as saved. By order in the prefab, so reordering them moves what's on them.</summary>
    public int Index => System.Array.IndexOf(Owner.GetComponentsInChildren<SurfacePoint>(true), this);

    /// <summary>Whether something other than <paramref name="ignore"/> is sitting here.</summary>
    public bool IsTaken(PlacedObject ignore = null)
    {
        foreach (PlacedObject placed in GetComponentsInChildren<PlacedObject>(true))
            if (placed != ignore) return true;
        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f);
        Gizmos.DrawWireSphere(transform.position, 0.08f);
    }
}
