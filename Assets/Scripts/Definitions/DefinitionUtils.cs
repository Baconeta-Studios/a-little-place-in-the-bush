using System.Collections.Generic;
using Bush.Core;
using UnityEngine;

internal static class Definitions
{
    public static string Id(GameDefinition definition) => definition ? definition.id : "";

    public static List<ForageModifier> ToData(List<ModifierEntry> modifiers)
    {
        var data = new List<ForageModifier>();
        foreach (ModifierEntry modifier in modifiers) data.Add(modifier.ToData());
        return data;
    }
}
