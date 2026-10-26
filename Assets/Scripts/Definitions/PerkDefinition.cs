using System.Collections.Generic;
using Bush.Core;
using UnityEngine;

[CreateAssetMenu(menuName = "Bush/Perk", fileName = "perk.new")]
public class PerkDefinition : GameDefinition
{
    public Sprite icon;
    public bool unlockedByDefault;
    public List<ModifierEntry> modifiers = new();

    public PerkData ToData() => new()
    {
        id = id, name = displayName, description = description, unlockedByDefault = unlockedByDefault,
        modifiers = Definitions.ToData(modifiers)
    };
}
