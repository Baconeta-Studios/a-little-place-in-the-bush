using Debugging;
using UnityEngine;

/// <summary>
/// Cheat menu entries (` or F1, or triple-tap top-left on a phone) for testing movement and areas,
/// plus the "get unstuck" safety net. Compiled out of release builds with the rest of the cheat menu.
/// Put it on Systems in each area scene.
/// </summary>
public class GameplayCheats : MonoBehaviour
{
    private bool _fastWalk;

    private void Start()
    {
        CheatMenu.AddButton("Gecko: back to the spawn point", () => AreaTravel.PlacePlayer(null), this);
        CheatMenu.AddToggle("Gecko: walk 3x faster", () => _fastWalk, SetFastWalk, this);
        CheatMenu.AddButton("Area: return to the cafe", () => AreaTravel.Instance.ReturnToHub(), this);
        CheatMenu.AddButton("Area: rebuild the walkable grid", RebuildGrid, this);
        CheatMenu.AddButton("Input: clear stuck input locks", ClearInputLocks, this);
    }

    private void SetFastWalk(bool fast)
    {
        _fastWalk = fast;
        if (Interactor.Player) Interactor.Player.Mover.SpeedMultiplier = fast ? 3f : 1f;
    }

    private static void RebuildGrid()
    {
        foreach (WalkableGrid grid in FindObjectsByType<WalkableGrid>()) grid.Rebuild();
        Debug.Log("[Cheats] Walkable grid rebuilt.");
    }

    private static void ClearInputLocks()
    {
        InputLock.ClearAll();
        Debug.Log("[Cheats] Input locks cleared.");
    }
}
