using System;
using System.Collections.Generic;
using Bush.Core;
using UnityEngine;

[Serializable]
public struct ModifierEntry
{
    public ModifierType type;
    [Tooltip("For TagWeight, TagQuantity and BonusRoll.")]
    public string tag;
    [Tooltip("For RarityWeight.")]
    public Rarity rarity;
    [Tooltip("Multiplier (weights), amount (quantity, rolls) or 0-1 chance (bonus roll).")]
    public double value;

    public ForageModifier ToData() => new(type, value, tag ?? "", rarity);
}

[CreateAssetMenu(menuName = "Bush/Companion", fileName = "companion.new")]
public class CompanionDefinition : GameDefinition
{
    public Sprite portrait;
    public bool unlockedByDefault;
    public List<ModifierEntry> modifiers = new();
    [Tooltip("Perk the player learns after enough trips together. Leave empty for none.")]
    public PerkDefinition teachesPerk;
    public int tripsToTeach = 3;

    public CompanionData ToData() => new()
    {
        id = id, name = displayName, description = description, unlockedByDefault = unlockedByDefault,
        modifiers = Definitions.ToData(modifiers), teachesPerk = Definitions.Id(teachesPerk), tripsToTeach = tripsToTeach
    };
}
