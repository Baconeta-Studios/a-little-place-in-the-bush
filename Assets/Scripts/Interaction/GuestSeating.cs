using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Sits visiting guests on the seats of furniture you've placed (<see cref="SeatPoint"/>). A seat
/// counts if the gecko can walk to a spot right beside it. Guests keep yesterday's seat if it's still
/// there and free; the rest take the next free seat. With no seat left, a guest uses their own spot
/// from the scene, or, if something's been built over it, the nearest open spot. Worked out again
/// each day, whenever someone arrives or leaves, and after building. Guests stay put while building.
/// Created automatically by the first <see cref="Npc"/> in a scene.
/// </summary>
public class GuestSeating : MonoBehaviour
{
    /// <summary>How far from a seat the gecko's spot beside it can be.</summary>
    private const float MaxStandDistance = 1.25f;

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
        if (Time.unscaledTime < _nextCheck) return;
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
        Seat(area, npcs);
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
        var waiting = new List<Npc>();
        foreach (Npc npc in npcs)
        {
            if (!npc.IsPresent || !npc.TakesSeats)
            {
                _seatOf.Remove(npc);
                SendHome(npc, pathfinder, origin);
                continue;
            }
            // Keep yesterday's seat if it's still there.
            if (_seatOf.TryGetValue(npc, out string kept) && seats.ContainsKey(kept) && taken.Add(kept)) Sit(npc, seats[kept]);
            else waiting.Add(npc);
        }

        foreach (Npc npc in waiting)
        {
            string free = order.FirstOrDefault(k => !taken.Contains(k));
            if (free != null)
            {
                taken.Add(free);
                _seatOf[npc] = free;
                Sit(npc, seats[free]);
            }
            else
            {
                _seatOf.Remove(npc);
                SendHome(npc, pathfinder, origin);
            }
        }
    }

    private static void Sit(Npc npc, (SeatPoint seat, Vector2 stand) spot) =>
        npc.PlaceAt(spot.seat.transform.position, spot.stand, spot.seat.FaceRight);

    /// <summary>Their own spot from the scene, or the nearest open spot if it's been built over.</summary>
    private static void SendHome(Npc npc, GridPathfinder pathfinder, Vector2 origin)
    {
        Vector2 home = npc.HomePosition, stand = npc.HomeStand;
        if (pathfinder)
        {
            bool homeBlocked = Physics2D.OverlapPoint(home, LayerMask.GetMask("Obstacle"));
            bool standOk = pathfinder.IsWalkable(stand) && pathfinder.IsReachable(origin, stand);
            if (homeBlocked || !standOk)
            {
                if (pathfinder.TryGetNearestWalkable(home, out Vector2 open)) home = open;
                if (pathfinder.TryGetNearestWalkable(stand, out Vector2 openStand) && pathfinder.IsReachable(origin, openStand)) stand = openStand;
                else stand = home;
            }
        }
        npc.PlaceAt(home, stand, npc.HomeFacesRight);
    }
}
