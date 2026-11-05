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
    [Tooltip("Floor: on the ground. Surface: on a SurfacePoint (a spot on a table). Wall: on a PlacementZone (the tree). Rug isn't built yet.")]
    public PlacementType placement;
    [Tooltip("Floor: the base that blocks walking, in world units: x = width, y = depth (front to back). Wall: the rectangle that has to fit on the zone, with the root at its bottom middle. Surface: not used.")]
    public Vector2 footprintSize = new(1f, 0.4f);
    [Tooltip("Fairy lights: above 0 makes it snap end to end. The distance between its two poles, in world units. The footprint is then each pole's. Children called PoleLeft and PoleRight hide when they'd double up at a join.")]
    [Min(0f)] public float chainSpan;
    public bool canFlip = true;
    [Tooltip("Tray tab, e.g. Furniture, Plants, Decor.")]
    public string category = "Furniture";
    [Tooltip("How many guests can perch on it. Should match the SeatPoints in the prefab.")]
    [Min(0)] public int seats;
    [Tooltip("How much it adds to the cafe's cosiness (more visitors, more lucky days).")]
    [Min(0)] public int cosiness = 1;
    [Tooltip("Its look, for the cafe sign: garden, rustic, glow, fancy...")]
    public string style;

    [Header("Shadow (easiest in Tools > Bush > Placeable Shadows and Bases)")]
    [Tooltip("A soft shadow on the floor. Floor things only. A child called Shadow in the prefab is used instead, if there is one.")]
    public bool groundShadow = true;
    [Tooltip("In world units. 0 follows the base.")]
    public Vector2 shadowSize;
    [Tooltip("Moves it from where it would sit by itself.")]
    public Vector2 shadowOffset;
    [Range(0f, 1f)] public float shadowStrength = 0.45f;

    public Vector2 ShadowSize => new(
        shadowSize.x > 0f ? shadowSize.x : footprintSize.x * 1.45f,
        shadowSize.y > 0f ? shadowSize.y : Mathf.Max(0.35f, footprintSize.y * 1.3f));

    public Vector2 ShadowCentre => shadowOffset + new Vector2(0f, footprintSize.y * 0.1f);

    public PlaceableData ToData() => new()
    {
        id = id, itemId = Definitions.Id(item), placement = placement, footprintWidth = footprintSize.x,
        footprintDepth = footprintSize.y, chainSpan = chainSpan, canFlip = canFlip, category = category, seats = seats,
        cosiness = cosiness, style = style
    };

    /// <summary>The seat points in the prefab, for checking against <see cref="seats"/>.</summary>
    public int CountSeatPointsInPrefab() => prefab ? prefab.GetComponentsInChildren<SeatPoint>(true).Length : 0;
}
