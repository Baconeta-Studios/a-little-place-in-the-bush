using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stops the player clicking to walk or interact while something else is in charge (area
/// transitions, cutscenes, build mode). Each owner locks and unlocks separately, so two systems
/// can't unlock each other by accident.
/// </summary>
public static class InputLock
{
    private static readonly HashSet<object> Owners = new();

    public static bool IsLocked => Owners.Count > 0;

    public static void Lock(object owner) => Owners.Add(owner);

    public static void Unlock(object owner) => Owners.Remove(owner);

    /// <summary>Debug escape hatch (cheat menu) if something forgot to unlock.</summary>
    public static void ClearAll() => Owners.Clear();

    // Static state survives play mode restarts when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Owners.Clear();
}
