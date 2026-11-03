using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Works around a bug in Unity's UI package that floods the Console with IndexOutOfRangeExceptions from
/// Selectable.OnEnable and OnDisable. Entering Play mode resets Selectable's list of active buttons, but
/// buttons in a prefab left open stay active, so turning them on or off afterwards takes the count below
/// zero. This rebuilds the list from the buttons that really are active. Editor only; delete it if a Unity
/// update fixes the bug. If the package's private names change, it just does nothing.
/// </summary>
[InitializeOnLoad]
public static class SelectableCountRepair
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    static SelectableCountRepair()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state is PlayModeStateChange.EnteredEditMode or PlayModeStateChange.EnteredPlayMode) Repair();
        };
    }

    private static void Repair()
    {
        Type type = typeof(Selectable);
        FieldInfo list = type.GetField("s_Selectables", Hidden);
        FieldInfo count = type.GetField("s_SelectableCount", Hidden);
        FieldInfo enabled = type.GetField("m_EnableCalled", Hidden);
        FieldInfo index = type.GetField("m_CurrentIndex", Hidden);
        if (list == null || count == null || enabled == null || index == null) return;

        Selectable[] all = Resources.FindObjectsOfTypeAll<Selectable>();
        var active = new Selectable[Mathf.Max(10, all.Length)];
        int activeCount = 0;
        foreach (Selectable selectable in all)
        {
            if (!(bool)enabled.GetValue(selectable)) continue;
            index.SetValue(selectable, activeCount);
            active[activeCount++] = selectable;
        }
        list.SetValue(null, active);
        count.SetValue(null, activeCount);
    }
}
