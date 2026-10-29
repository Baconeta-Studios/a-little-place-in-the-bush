using Bush.Core;
using UnityEngine;

/// <summary>
/// Something that can be put down in build mode. The prefab follows the prop pattern: its root is
/// the floor contact point, a child called Visual holds the art (and any scale), and SeatPoint
/// children mark where guests will perch. The footprint collider is sized from this asset when the
/// object is placed, so you don't have to keep the two in sync by hand.
/// </summary>
[CreateAssetMenu(menuName = "Bush/Placeable", fileName = "placeable.new")]
public class PlaceableDefinition : GameDefinition
{
    [Tooltip("The inventory item that this places (category Placeable).")]
    public ItemDefinition item;
    [Tooltip("Root at the floor contact point, art in a child called Visual, optional SeatPoint children.")]
    public GameObject prefab;
    [Tooltip("Only Floor works so far. Wall, Rug and Surface are reserved for later.")]
    public PlacementType placement;
    [Tooltip("The base that blocks walking, in world units: x = width, y = depth (front to back).")]
    public Vector2 footprintSize = new(1f, 0.4f);
    public bool canFlip = true;
    [Tooltip("Tray tab, e.g. Furniture, Plants, Decor.")]
    public string category = "Furniture";
    [Tooltip("How many guests can perch on it. Should match the SeatPoints in the prefab.")]
    [Min(0)] public int seats;
    [Tooltip("How much it adds to the cafe's cosiness (more visitors, more lucky days).")]
    [Min(0)] public int cosiness = 1;
    [Tooltip("Its look, for the cafe sign: garden, rustic, glow, fancy...")]
    public string style;

    public PlaceableData ToData() => new()
    {
        id = id, itemId = Definitions.Id(item), placement = placement, footprintWidth = footprintSize.x,
        footprintDepth = footprintSize.y, canFlip = canFlip, category = category, seats = seats,
        cosiness = cosiness, style = style
    };

    /// <summary>The seat points in the prefab, for checking against <see cref="seats"/>.</summary>
    public int CountSeatPointsInPrefab() => prefab ? prefab.GetComponentsInChildren<SeatPoint>(true).Length : 0;
}
