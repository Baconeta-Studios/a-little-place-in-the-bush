using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Whether a screen point is over UI, so world clicks (walking, building) can ignore it.</summary>
public static class UiPointer
{
    private static readonly List<RaycastResult> Hits = new();

    /// <summary>True if the point is over any UI graphic that blocks raycasts (buttons, panels, popups).</summary>
    public static bool IsOverUi(Vector2 screen)
    {
        EventSystem eventSystem = EventSystem.current;
        if (!eventSystem) return false;

        var data = new PointerEventData(eventSystem) { position = screen };
        Hits.Clear();
        eventSystem.RaycastAll(data, Hits);
        foreach (RaycastResult hit in Hits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }
}
