using System;
using System.Collections.Generic;
using Bush.Core;
using UnityEngine;

[Serializable]
public struct GiftEntryDefinition
{
    public ItemDefinition item;
    [Min(1)] public int qty;
    [Min(0f)] public float weight;
    [Tooltip("Something special: only given on an exclusive roll, which a cosier cafe makes likelier.")]
    public bool exclusive;
}

/// <summary>
/// A nice thing about today, announced in the morning (see <see cref="DayEvents"/>). Edit these directly
/// in the Inspector: how likely (weight, rarity, minimum cosiness) and what it does. They're listed on the
/// GameDatabase, with the overall tuning (how often events happen, the doorstep's exclusive chance).
/// **Tools > Bush > Create Default Day Events** makes the starting set.
/// </summary>
[CreateAssetMenu(menuName = "Bush/Day Event", fileName = "event.new")]
public class DayEventDefinition : GameDefinition
{
    [Header("Announcement")]
    [Tooltip("The morning line, e.g. \"Something glints in the bush today.\"")]
    [TextArea(1, 3)] public string line;
    [Tooltip("What it does, in a few words, for the HUD and screens.")]
    public string effect;

    [Header("How likely")]
    [Tooltip("How likely, against the other events.")]
    [Min(0f)] public float weight = 1f;
    [Tooltip("Rarer events get likelier as the cafe gets cosier.")]
    public Rarity rarity;
    [Tooltip("Only happens once the cafe is at least this cosy.")]
    [Min(0)] public int minCosiness;
    [Tooltip("Only once night is unlocked (for night events).")]
    public bool needsNight;

    [Header("What it does (any mix)")]
    [Tooltip("Added to forage trips today.")]
    public List<ModifierEntry> forage = new();
    [Tooltip("Which trips the forage bonus is for.")]
    public ActiveTime forageAt = ActiveTime.Both;
    [Tooltip("Added to the peek-a-boo find today (e.g. rarer things).")]
    public List<ModifierEntry> peek = new();
    [Tooltip("Peek-a-boo rustles for longer today.")]
    public bool peekLonger;
    [Tooltip("Extra dishes the cafe sells today.")]
    [Min(0)] public int extraSales;
    [Tooltip("The first dish sold today pays double.")]
    public bool critic;
    [Tooltip("A guest with a quest of yours under way is sure to drop by.")]
    public bool familiarFace;
    [Tooltip("Something left on the doorstep: one of these, straight into the bag.")]
    public List<GiftEntryDefinition> gift = new();

    public DayEventData ToData() => new()
    {
        id = id, name = displayName, line = line, effect = effect, weight = weight, rarity = rarity, minCosiness = minCosiness,
        needsNight = needsNight, forage = Definitions.ToData(forage), forageAt = forageAt, peek = Definitions.ToData(peek),
        peekLonger = peekLonger, extraSales = extraSales, critic = critic, familiarFace = familiarFace,
        gift = gift.FindAll(g => g.item).ConvertAll(g => new GiftEntry { itemId = g.item.id, qty = Mathf.Max(1, g.qty), weight = g.weight, exclusive = g.exclusive })
    };
}
