using Bush.Core;
using UnityEngine;

[CreateAssetMenu(menuName = "Bush/Placeable", fileName = "placeable.new")]
public class PlaceableDefinition : GameDefinition
{
    [Tooltip("The inventory item that this places.")]
    public ItemDefinition item;
    [Tooltip("Prefab built the usual way: parent at the floor contact point with an Obstacle footprint, child Visual.")]
    public GameObject prefab;

    public PlaceableData ToData() => new() { id = id, itemId = Definitions.Id(item) };
}
