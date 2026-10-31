using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Sits visiting guests on the seats of furniture you've placed (<see cref="SeatPoint"/>). A seat
/// counts if the gecko can walk to a spot right beside it. Guests keep yesterday's seat if it's still
/// there and free; the rest take the next free seat that isn't right next to another guest (so a
/// bench seats one, and two chairs side by side seat one, unless they're spaced out). With no seat left, a guest uses their own spot
/// from the scene, or, if it's been built over or another guest is right there, the nearest open spot. Worked out again
/// each day, whenever someone arrives or leaves, and after building, and then the walkable grid is rebuilt so
/// the gecko walks around guests (their footprints, see <see cref="Npc"/>). Guests stay put while building.
/// Created automatically by the first <see cref="Npc"/> in a scene.
/// </summary>
public class GuestSeating : MonoBehaviour
{
    /// <summary>How far from a seat the gecko's spot beside it can be.</summary>
    private const float MaxStandDistance = 1.25f;
    /// <summary>Guests don't sit closer together than this (world units), so they don't crowd each other.</summary>
    private const float MinGuestSpacing = 1.1f;

    private static GuestSeating _instance;

    private readonly Dictionary<Npc, string> _seatOf = new();
    private string _signature;
    private float _nextCheck;

    public static void Ensure()
    {
        if (_instance) return;
        _instance = FindAnyObjectByType<GuestSeating>();
        if (!_instance) _instance = new GameObject("[GuestSeating]").AddComponent<GuestSeating>();
    }

    private void OnEnable() => BuildMode.ActiveChanged += OnBuildModeChanged;
    private void OnDisable() => BuildMode.ActiveChanged -= OnBuildModeChanged;

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void OnBuildModeChanged(bool building)
    {
        if (!building) _signature = null; // things may have moved: seat everyone again
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextCheck || DayTransition.Holding) return;
        _nextCheck = Time.unscaledTime + 0.5f;

        BuildArea area = BuildArea.Current;
        GameSession session = GameSession.Current;
        if (session == null || (area && !area.IsLoaded)) return;
        BuildMode mode = area ? area.GetComponent<BuildMode>() : null;
        if (mode && mode.IsActive) return;

        Npc[] npcs = FindObjectsByType<Npc>().OrderBy(n => n.name).ToArray();
        string signature = Signature(session, area, npcs);
        if (signature == _signature) return;
        _signature = signature;

        // Work out seats against the furniture only (guests' own footprints would block their spots), then put
        // the footprints back and rebuild the walkable floor so the gecko walks around everyone.
        GridPathfinder pathfinder = area ? area.Pathfinder : FindAnyObjectByType<GridPathfinder>();
        foreach (Npc npc in npcs) npc.SetFootprintActive(false);
        if (pathfinder) pathfinder.Grid.Rebuild();
        Seat(area, npcs);
        foreach (Npc npc in npcs) npc.SetFootprintActive(true);
        if (pathfinder) pathfinder.Grid.Rebuild();
    }

    /// <summary>Changes when the day, who's in, or what's placed changes.</summary>
    private static string Signature(GameSession session, BuildArea area, Npc[] npcs)
    {
        var parts = new List<string> { session.State.day.ToString() };
        parts.AddRange(npcs.Where(n => n.IsPresent).Select(n => n.name));
        if (area) parts.AddRange(area.Spawned.Where(p => p).Select(p => $"{p.InstanceId}@{p.transform.position.x:F2},{p.transform.position.y:F2}"));
        return string.Join("|", parts);
    }

    private void Seat(BuildArea area, Npc[] npcs)
    {
        GridPathfinder pathfinder = area ? area.Pathfinder : FindAnyObjectByType<GridPathfinder>();
        Vector2 origin = area ? area.PathOrigin() : Vector2.zero;
        if (pathfinder && !pathfinder.Grid.IsBuilt) pathfinder.Grid.Rebuild();

        // Every seat with a reachable spot beside it, in a stable order.
        var seats = new Dictionary<string, (SeatPoint seat, Vector2 stand)>();
        var order = new List<string>();
        if (area && pathfinder)
        {
            foreach (PlacedObject placed in area.Spawned.Where(p => p).OrderBy(p => p.InstanceId))
            {
                SeatPoint[] points = placed.GetComponentsInChildren<SeatPoint>();
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 seat = points[i].transform.position;
                    if (!pathfinder.TryGetNearestWalkable(seat, out Vector2 stand)) continue;
                    if (Vector2.Distance(stand, seat) > MaxStandDistance || !pathfinder.IsReachable(origin, stand)) continue;
                    string key = $"{placed.InstanceId}:{i}";
                    seats[key] = (points[i], stand);
                    order.Add(key);
                }
            }
        }

        var taken = new HashSet<string>();
        var occupied = new List<Vector2>(); // where guests already are, to keep the rest a little apart
        var waiting = new List<Npc>();
        foreach (Npc npc in npcs)
        {
            if (!npc.IsPresent || !npc.TakesSeats)
            {
                _seatOf.Remove(npc);
                if (npc.IsPresent)
                {
                    SendHome(npc, pathfinder, origin, occupied);
                    occupied.Add(npc.transform.position);
                }
                else npc.PlaceAt(npc.HomePosition, npc.HomeStand, npc.HomeFacesRight); // hidden: just park them at home
                continue;
            }
            // Keep yesterday's seat if it's still there and nobody's crowding it.
            if (_seatOf.TryGetValue(npc, out string kept) && seats.ContainsKey(kept) && !Crowded(seats[kept].seat, occupied) && taken.Add(kept))
            {
                Sit(npc, seats[kept]);
                occupied.Add(seats[kept].seat.transform.position);
            }
            else waiting.Add(npc);
        }

        foreach (Npc npc in waiting)
        {
            string free = order.FirstOrDefault(k => !taken.Contains(k) && !Crowded(seats[k].seat, occupied));
            if (free != null)
            {
                taken.Add(free);
                _seatOf[npc] = free;
                Sit(npc, seats[free]);
                occupied.Add(seats[free].seat.transform.position);
            }
            else
            {
                _seatOf.Remove(npc);
                SendHome(npc, pathfinder, origin, occupied);
                occupied.Add(npc.transform.position);
            }
        }
    }

    private static bool Crowded(SeatPoint seat, List<Vector2> occupied)
    {
        Vector2 position = seat.transform.position;
        return occupied.Exists(o => Vector2.Distance(o, position) < MinGuestSpacing);
    }

    private static void Sit(Npc npc, (SeatPoint seat, Vector2 stand) spot) =>
        npc.PlaceAt(spot.seat.transform.position, spot.stand, spot.seat.FaceRight);

    /// <summary>
    /// Their own spot from the scene, if it's clear: not built over, reachable, and not right next to
    /// another guest. Otherwise the nearest open spot that is, searching outwards from home.
    /// </summary>
    private static void SendHome(Npc npc, GridPathfinder pathfinder, Vector2 origin, List<Vector2> occupied)
    {
        Vector2 home = npc.HomePosition, stand = npc.HomeStand;
        if (!pathfinder || SpotIsClear(npc, home, stand, pathfinder, origin, occupied))
        {
            npc.PlaceAt(home, stand, npc.HomeFacesRight);
            return;
        }

        Vector2 standOffset = stand - home;
        for (float radius = 0.5f; radius <= 6f; radius += 0.5f)
        {
            for (int step = 0; step < 16; step++)
            {
                float angle = step * Mathf.PI * 2f / 16f;
                Vector2 spot = home + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f) * radius; // flatter: the floor is wide, not deep
                Vector2 spotStand = spot + standOffset;
                if (!pathfinder.IsWalkable(spotStand) && pathfinder.TryGetNearestWalkable(spotStand, out Vector2 nearby)) spotStand = nearby;
                if (!SpotIsClear(npc, spot, spotStand, pathfinder, origin, occupied)) continue;
                npc.PlaceAt(spot, spotStand, npc.HomeFacesRight);
                return;
            }
        }
        npc.PlaceAt(home, stand, npc.HomeFacesRight); // nowhere better nearby: stay home
    }

    private static bool SpotIsClear(Npc npc, Vector2 spot, Vector2 stand, GridPathfinder pathfinder, Vector2 origin, List<Vector2> occupied) =>
        !BlockedByOthers(npc, spot) &&
        !occupied.Exists(o => Vector2.Distance(o, spot) < MinGuestSpacing) &&
        pathfinder.IsWalkable(stand) && pathfinder.IsReachable(origin, stand);

    /// <summary>Something else (furniture, another guest) is on this spot. The guest's own footprint doesn't count.</summary>
    private static bool BlockedByOthers(Npc npc, Vector2 spot)
    {
        foreach (Collider2D hit in Physics2D.OverlapPointAll(spot, LayerMask.GetMask("Obstacle")))
            if (!hit.transform.IsChildOf(npc.transform)) return true;
        return false;
    }
}
