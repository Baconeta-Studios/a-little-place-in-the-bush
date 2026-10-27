using Bush.Core;
using UnityEngine;

[CreateAssetMenu(menuName = "Bush/NPC", fileName = "npc.new")]
public class NpcDefinition : GameDefinition
{
    public Sprite portrait;
    public string species;

    public NpcData ToData() => new() { id = id, name = displayName, species = species, description = description };
}
