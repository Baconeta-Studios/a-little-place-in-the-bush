using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Bush > Add Decor Zone To This Scene: a <see cref="DecorZone"/> beside the walkable floor, the same shape
/// grown outwards a little and kept inside the backdrop, so decorations can go on the grass around the edges.
/// Select it and edit its PolygonCollider2D's points to fit the art.
/// </summary>
public static class DecorZoneTools
{
    private const float Grow = 1.4f;
    private static readonly Rect Backdrop = new(-11.9f, -7.1f, 23.8f, 14.2f);

    [MenuItem("Tools/Bush/Add Decor Zone To This Scene", priority = 31)]
    private static void AddFromMenu()
    {
        if (Object.FindAnyObjectByType<DecorZone>())
        {
            EditorUtility.DisplayDialog("Decor zone", "This scene already has one. Select it to change its shape.", "OK");
            return;
        }
        GameObject zone = Add();
        if (!zone) EditorUtility.DisplayDialog("Decor zone", "There's no walkable floor (a WalkableGrid with a polygon) in this scene to start from.", "OK");
        else Selection.activeGameObject = zone;
    }

    /// <returns>The new zone, or null if there's no floor to start from.</returns>
    public static GameObject Add()
    {
        var grid = Object.FindAnyObjectByType<WalkableGrid>();
        if (!grid || grid.WalkableArea is not PolygonCollider2D floor) return null;

        Vector2[] points = floor.points.Select(p => (Vector2)floor.transform.TransformPoint(p + floor.offset)).ToArray();
        Vector2 centre = points.Aggregate(Vector2.zero, (sum, p) => sum + p) / points.Length;

        var zone = new GameObject("DecorZone");
        zone.transform.SetParent(floor.transform.parent, false);
        zone.transform.position = Vector3.zero;
        zone.layer = floor.gameObject.layer;
        var shape = zone.AddComponent<PolygonCollider2D>();
        shape.isTrigger = true;
        shape.points = points.Select(p =>
        {
            Vector2 grown = p + (p - centre).normalized * Grow;
            return (Vector2)zone.transform.InverseTransformPoint(new Vector2(
                Mathf.Clamp(grown.x, Backdrop.xMin, Backdrop.xMax), Mathf.Clamp(grown.y, Backdrop.yMin, Backdrop.yMax)));
        }).ToArray();
        zone.AddComponent<DecorZone>();
        Undo.RegisterCreatedObjectUndo(zone, "Add Decor Zone");
        EditorSceneManager.MarkSceneDirty(zone.scene);
        return zone;
    }
}
