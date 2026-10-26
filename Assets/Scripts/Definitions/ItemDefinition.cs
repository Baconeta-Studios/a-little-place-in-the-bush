using System.Collections.Generic;
using Bush.Core;
using UnityEngine;

[CreateAssetMenu(menuName = "Bush/Item", fileName = "item.new")]
public class ItemDefinition : GameDefinition
{
    public Sprite icon;
    public ItemCategory category;
    public Rarity rarity;
    [Tooltip("Lower-case tags that perks, companions and focuses look for, e.g. bark, insect, shiny.")]
    public List<string> tags = new();
    public int sellValue;
    [Tooltip("Only one can ever be owned, and it never drops again once found (recipe scraps).")]
    public bool unique;

    public ItemData ToData() => new()
    {
        id = id, name = displayName, description = description, category = category, rarity = rarity,
        tags = new List<string>(tags), sellValue = sellValue, unique = unique
    };
}
