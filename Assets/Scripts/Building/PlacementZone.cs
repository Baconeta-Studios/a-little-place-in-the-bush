using UnityEngine;

/// <summary>
/// An area of the backdrop that Wall placeables can go on (the tree, for windows). Draw its shape with the
/// PolygonCollider2D: a thing's whole rectangle has to fit inside. Put it anywhere in the scene; it doesn't
/// block walking or clicks.
/// </summary>
[RequireComponent(typeof(PolygonCollider2D))]
public class PlacementZone : MonoBehaviour
{
    [Tooltip("What the player's told when something doesn't fit: \"It needs to fit on the tree.\"")]
    [SerializeField] private string label = "the tree";

    private PolygonCollider2D _shape;

    public string Label => label;
    public Bounds Bounds => Shape.bounds;

    private PolygonCollider2D Shape => _shape ? _shape : _shape = GetComponent<PolygonCollider2D>();

    private void Reset() => GetComponent<PolygonCollider2D>().isTrigger = true;

    /// <summary>
    /// The rectangle is wholly inside the shape, checked at points along its edges and across its middle
    /// (corners alone would miss a notch in the outline).
    /// </summary>
    public bool Fits(Rect rect)
    {
        const float inset = 0.01f;
        const float spacing = 0.1f;
        int columns = Mathf.Max(1, Mathf.CeilToInt(rect.width / spacing));
        int rows = Mathf.Max(1, Mathf.CeilToInt(rect.height / spacing));
        for (int row = 0; row <= rows; row++)
        {
            for (int column = 0; column <= columns; column++)
            {
                Vector2 point = new(
                    Mathf.Lerp(rect.xMin + inset, rect.xMax - inset, column / (float)columns),
                    Mathf.Lerp(rect.yMin + inset, rect.yMax - inset, row / (float)rows));
                if (!Shape.OverlapPoint(point)) return false;
            }
        }
        return true;
    }
}
