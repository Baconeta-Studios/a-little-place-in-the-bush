using UnityEngine;

/// <summary>
/// Base for every design asset (items, recipes, companions, quests...). The <see cref="id"/>
/// never changes once players have saves, even if the name or art does: saves and other data
/// only refer to things by ID. Edit these in the inspector, or in the web sandbox and then
/// Tools > Bush > Import Design Data.
/// </summary>
public abstract class GameDefinition : ScriptableObject
{
    [Tooltip("Stable ID, e.g. item.worm. Never change it once it's in a build.")]
    public string id;
    public string displayName;
    [TextArea(2, 5)] public string description;

    public override string ToString() => string.IsNullOrEmpty(id) ? name : id;
}
