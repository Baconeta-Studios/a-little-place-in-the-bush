using System;
using System.Collections.Generic;
using Bush.Core;
using UnityEngine;

[Serializable]
public struct ForageEntryDefinition
{
    public ItemDefinition item;
    [Min(0)] public double weight;
    [Min(1)] public int min;
    [Min(1)] public int max;
    public EntryTime time;
    [Tooltip("Only drops while this quest is active. Leave empty for always.")]
    public QuestDefinition requiresQuest;
}

[CreateAssetMenu(menuName = "Bush/Forage Table", fileName = "table.new")]
public class ForageTableDefinition : GameDefinition
{
    [Min(1)] public int rolls = 4;
    public List<ForageEntryDefinition> entries = new();

    public ForageTableData ToData()
    {
        var data = new ForageTableData { id = id, name = displayName, rolls = rolls };
        foreach (ForageEntryDefinition entry in entries)
            data.entries.Add(new ForageEntry
            {
                itemId = Definitions.Id(entry.item), weight = entry.weight, min = entry.min, max = entry.max,
                time = entry.time, requiresQuest = Definitions.Id(entry.requiresQuest)
            });
        return data;
    }
}
