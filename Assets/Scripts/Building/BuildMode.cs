using System;
using System.Collections;
using System.Collections.Generic;
using Bush.Core;
using Debugging;
using UI.Popups;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Build mode: place, move, flip and put away things in a <see cref="BuildArea"/>, with pan and
/// zoom (<see cref="BuildCamera"/>). Entering pauses
/// the game and stops the gecko taking clicks. Everything works by tap and drag (no hover): tap an
/// item in the tray to get a ghost, drag it (it snaps to the 0.25 grid and lifts above your finger on
/// touch), then Place. Tap something already placed to pick it up. The ghost turns red with a reason
/// when it can't go there (see <see cref="PlacementValidator"/>). Added automatically by BuildArea.
/// </summary>
public class BuildMode : MonoBehaviour
{
    [Tooltip("Optional; uses the Gameplay map's Point and Click. Pointer press works without it.")]
    [SerializeField] private InputActionAsset actions;
    [Tooltip("How far a tap can miss something placed and still pick it up.")]
    [SerializeField, Min(0f)] private float tapTolerance = 0.2f;
    [Tooltip("On touch, the item sits this far above your finger while dragging, so your thumb doesn't hide it.")]
    [SerializeField, Min(0f)] private float touchLift = 0.9f;
    [Tooltip("How close a small thing has to be dragged to a spot on a table to snap to it.")]
    [SerializeField, Min(0f)] private float surfaceSnapRadius = 0.8f;
    [Tooltip("How close fairy lights have to be dragged to another set's free end to snap to it.")]
    [SerializeField, Min(0f)] private float chainSnapRadius = 0.6f;
    [Tooltip("Hook up a sound or particles here.")]
    [SerializeField] private UnityEvent onPlaced = new();

    private class Candidate
    {
        public PlacedObject obj;
        public PlaceableDefinition definition;
        public bool isNew;
        public Vector2 position;
        public bool flipped;
        public Vector2 originalPosition;
        public bool originalFlipped;
        public Transform originalParent;
        public PlacementCheck check;
        public bool checkedOnce;
        public PlacementContext context;
    }

    private BuildArea _area;
    private BuildUi _ui;
    private Candidate _candidate;
    private InputActionAsset _actionsCopy;
    private InputAction _point;
    private InputAction _click;
    private bool _dragging;
    private Vector2 _dragOffset;
    private float _timeScaleBefore = 1f;
    private BuildCamera _view;
    private bool _panningEmptyGround;

    public static event Action<bool> ActiveChanged;

    public bool IsActive { get; private set; }
    public bool HasCandidate => _candidate != null;
    public bool CandidateIsNew => _candidate != null && _candidate.isNew;
    public bool CandidateCanFlip => _candidate != null && _candidate.definition && _candidate.definition.canFlip;
    public PlacementCheck CandidateCheck => _candidate?.check ?? PlacementCheck.Valid;
    public string CandidateName => _candidate?.obj ? _candidate.obj.DisplayName : null;
    /// <summary>World point just under the candidate's base, for floating buttons.</summary>
    public Vector2 CandidateAnchor => _candidate == null ? Vector2.zero : _candidate.position + new Vector2(0f, -0.25f);
    /// <summary>World point just above the candidate's art: floating buttons go here, clear of a dragging finger.</summary>
    public Vector2 CandidateTop
    {
        get
        {
            if (_candidate == null) return Vector2.zero;
            float top = _candidate.position.y + 1f;
            foreach (SpriteRenderer renderer in _candidate.obj.GetComponentsInChildren<SpriteRenderer>())
                if (renderer.sortingLayerName != "FloorDecals") top = Mathf.Max(top, renderer.bounds.max.y);
            return new Vector2(_candidate.position.x, top + 0.2f);
        }
    }
    public BuildArea Area => _area;

    private static GameSession Session => GameSession.Current;

    private void Awake()
    {
        _area = GetComponent<BuildArea>();
        if (actions)
        {
            _actionsCopy = Instantiate(actions);
            InputActionMap map = _actionsCopy.FindActionMap("Gameplay", true);
            _point = map.FindAction("Point", true);
            _click = map.FindAction("Click", true);
        }
        else
        {
            _point = new InputAction("Point", InputActionType.PassThrough, "<Pointer>/position");
            _click = new InputAction("Click", InputActionType.Button, "<Pointer>/press");
        }
    }

    private void OnEnable()
    {
        _point.Enable();
        _click.Enable();
    }

    private void OnDisable()
    {
        if (IsActive)
        {
            // Unloading (scene change, stopping Play): just undo what Enter did; other objects may be gone.
            IsActive = false;
            if (Time.timeScale == 0f) Time.timeScale = _timeScaleBefore;
            InputLock.Unlock(this);
            SetPauseMenuAllowed(true);
            if (_view) _view.EndImmediately();
            ActiveChanged?.Invoke(false);
        }
        _point.Disable();
        _click.Disable();
    }

    private void OnDestroy()
    {
        if (_actionsCopy) Destroy(_actionsCopy);
        else
        {
            _point?.Dispose();
            _click?.Dispose();
        }
        if (_ui) Destroy(_ui.gameObject);
    }

    // ----- Entering and leaving -----

    public bool CanEnter => _area && _area.IsLoaded && Session != null && !IsActive && !InputLock.IsLocked && !PopupManager.AnyOpen;

    public void Enter()
    {
        if (!CanEnter) return;
        IsActive = true;

        if (Interactor.Player) Interactor.Player.Cancel();
        InputLock.Lock(this);
        SetPauseMenuAllowed(false);
        _timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f; // per the GDD, build mode pauses the game

        if (!_ui) _ui = BuildUi.Create(this);
        _ui.Show();
        if (!_view) _view = GetComponent<BuildCamera>();
        if (!_view) _view = gameObject.AddComponent<BuildCamera>();
        _view.Begin(_area, _ui);
        ActiveChanged?.Invoke(true);
    }

    public void Exit()
    {
        if (!IsActive) return;
        Cancel();
        IsActive = false;

        if (Time.timeScale == 0f) Time.timeScale = _timeScaleBefore; // leave a cheat's time scale alone
        InputLock.Unlock(this);
        // Next frame: otherwise the same Escape press that left build mode would open the pause menu.
        StartCoroutine(AllowPauseMenuNextFrame());
        _dragging = false;
        _panningEmptyGround = false;
        if (_view) _view.End();

        // Make sure the gecko isn't standing in new furniture or boxed in: if it can't walk to the
        // spawn point any more, it's moved there.
        _area.Grid.Rebuild();
        Interactor player = Interactor.Player;
        if (player)
        {
            Vector2 origin = _area.PathOrigin();
            if (!_area.Grid.IsWalkable(player.transform.position)) player.Mover.Teleport(player.transform.position);
            if (!_area.Pathfinder.IsReachable(player.transform.position, origin)) player.Mover.Teleport(origin);
        }

        Session?.SaveNow();
        if (_ui) _ui.Hide();
        ActiveChanged?.Invoke(false);
    }

    private static IEnumerator AllowPauseMenuNextFrame()
    {
        yield return null;
        SetPauseMenuAllowed(true);
    }

    // ----- Actions (the tray and the floating buttons call these) -----

    /// <summary>Picks a new item from the tray: a ghost appears on a free spot in view.</summary>
    public void StartPlacing(PlaceableDefinition definition)
    {
        if (!IsActive || !definition) return;
        Cancel();
        if (!Session.Build.CanPlace(definition.id))
        {
            _ui.Toast($"You don't have any {definition.displayName} left.");
            return;
        }

        PlacedObject ghost = BuildArea.CreateInstance(definition, _area.PlacedParent);
        ghost.Setup(definition, 0);
        ghost.SetFootprintActive(false);
        ghost.name = "Ghost: " + definition.id;
        _candidate = new Candidate { obj = ghost, definition = definition, isNew = true, context = PlacementContext.For(_area) };
        SetCandidatePosition(FindStartSpot(definition));
        _ui.Refresh();
    }

    /// <summary>Picks up something already placed, to move, flip or put away.</summary>
    public void Select(PlacedObject placed)
    {
        if (!IsActive || !placed) return;
        Cancel();
        GameAudio.Play(GameSound.PickUpObject);
        placed.SetFootprintActive(false);
        _area.Grid.Rebuild(); // so its old spot doesn't count against it
        _candidate = new Candidate
        {
            obj = placed, definition = placed.Definition, isNew = false,
            position = placed.transform.position, flipped = placed.Flipped,
            originalPosition = placed.transform.position, originalFlipped = placed.Flipped,
            originalParent = placed.transform.parent,
            context = PlacementContext.For(_area)
        };
        SetCandidatePosition(_candidate.position);
        _ui.Refresh();
    }

    public void Confirm()
    {
        if (_candidate == null) return;
        if (!_candidate.check.ok)
        {
            _ui.Toast(_candidate.check.reason);
            return;
        }

        SurfacePoint surface = _candidate.definition.CanGoOnTable
            ? _area.FreeSurfaceNear(_candidate.position, 0.01f, _candidate.obj)
            : null;

        PlacedObject result;
        if (_candidate.isNew)
        {
            PlacedObjectData data = Session.Build.Place(_area.AreaId, _candidate.definition.id, _candidate.position.x, _candidate.position.y, _candidate.flipped);
            GameObject ghost = _candidate.obj.gameObject;
            ghost.transform.SetParent(null); // it's gone at the end of the frame, but its spot is free now
            Destroy(ghost);
            _candidate = null;
            if (data == null)
            {
                _ui.Toast("There are none left to place.");
                _ui.Refresh();
                return;
            }
            if (surface) Session.Build.Attach(data.instanceId, surface.Owner.InstanceId, surface.Index);
            result = _area.Spawn(data);
        }
        else
        {
            result = _candidate.obj;
            Session.Build.Move(result.InstanceId, _candidate.position.x, _candidate.position.y);
            Session.Build.SetFlipped(result.InstanceId, _candidate.flipped);
            if (surface) Session.Build.Attach(result.InstanceId, surface.Owner.InstanceId, surface.Index);
            else Session.Build.Detach(result.InstanceId);
            result.SetFootprintActive(true);
            result.ClearBuildState();
            _candidate = null;
        }

        _area.RefreshChains();
        _area.Grid.Rebuild();
        result.PlayPlacedFeedback();
        GameAudio.Play(GameSound.PlaceObject);
        onPlaced.Invoke();
        Session.RequestSave();
        _ui.Refresh();
    }

    public void Flip()
    {
        if (_candidate == null || !CandidateCanFlip) return;
        _candidate.flipped = !_candidate.flipped;
        _candidate.obj.SetFlipped(_candidate.flipped);
        Validate();
    }

    /// <summary>Drops the ghost, or puts a picked-up thing back where it was.</summary>
    public void Cancel()
    {
        if (_candidate == null) return;
        if (_candidate.isNew)
        {
            _candidate.obj.transform.SetParent(null); // it's gone at the end of the frame, but its spot is free now
            Destroy(_candidate.obj.gameObject);
        }
        else
        {
            if (_candidate.definition.CanGoOnTable)
                _area.SitOn(_candidate.obj, _candidate.originalParent ? _candidate.originalParent.GetComponent<SurfacePoint>() : null);
            _candidate.obj.transform.position = _candidate.originalPosition;
            _candidate.obj.SetFlipped(_candidate.originalFlipped);
            _candidate.obj.SetFootprintActive(true);
            _candidate.obj.ClearBuildState();
            _area.Grid.Rebuild();
        }
        _candidate = null;
        _dragging = false;
        _area.RefreshChains();
        if (_ui) _ui.Refresh();
    }

    /// <summary>Takes the picked-up thing out of the space and back into the bag.</summary>
    public void PutAway()
    {
        if (_candidate == null) return;
        if (_candidate.isNew)
        {
            Cancel();
            return;
        }

        string label = _candidate.obj.DisplayName;
        int id = _candidate.obj.InstanceId;
        if (!Session.Build.Remove(id))
        {
            _ui.Toast($"Couldn't put the {label.ToLowerInvariant()} away.");
            Cancel();
            return;
        }
        _candidate = null;
        _area.Despawn(id);
        _area.RefreshChains();
        _area.Grid.Rebuild();
        Session.RequestSave();
        _ui.Toast($"Put the {label.ToLowerInvariant()} away.");
        GameAudio.Play(GameSound.PutAwayObject);
        _ui.Refresh();
    }

    // ----- Pointer and keys -----

    private void Update()
    {
        if (!IsActive) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && !PopupManager.AnyOpen)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_candidate != null) Cancel();
                else Exit();
                return;
            }
            if (keyboard.fKey.wasPressedThisFrame) Flip();
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Confirm();
            if (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) PutAway();
        }

        if (PopupManager.AnyOpen || CheatMenu.IsOpen)
        {
            _dragging = _panningEmptyGround = false;
            return;
        }

        // Pinch, two-finger pan, scroll and right-drag move the camera, never items.
        if (_view && _view.HandleGestures())
        {
            _dragging = _panningEmptyGround = false;
            return;
        }

        Camera camera = Camera.main;
        if (!camera) return;
        Vector2 screen = _point.ReadValue<Vector2>();
        Vector2 world = camera.ScreenToWorldPoint(screen);

        if (_click.WasPressedThisFrame())
        {
            if (UiPointer.IsOverUi(screen)) return;
            if (!OnPress(world) && _view)
            {
                // Nothing there and nothing held: drag to look around.
                _panningEmptyGround = true;
                _view.BeginDragPan(screen);
            }
        }
        else if (_dragging)
        {
            if (!_click.IsPressed()) _dragging = false;
            else SetCandidatePosition(Snap(world + _dragOffset));
        }
        else if (_panningEmptyGround)
        {
            if (!_click.IsPressed()) _panningEmptyGround = false;
            else _view.DragPan(screen);
        }
    }

    /// <summary>Returns false if the press landed on nothing and nothing is held (so it can pan instead).</summary>
    private bool OnPress(Vector2 world)
    {
        bool touch = Pointer.current is Touchscreen;

        if (_candidate != null && Hits(_candidate.obj, world))
        {
            BeginDrag(world, touch);
            return true;
        }

        // Holding something for a table, a tap on the table means "put it here", not "pick up the table".
        bool holdingForSurface = _candidate != null && (_candidate.definition.placement == PlacementType.Surface
            || (_candidate.definition.alsoOnTables && _area.FreeSurfaceNear(world, surfaceSnapRadius, _candidate.obj)));
        PlacedObject placed = holdingForSurface ? null : FindPlacedAt(world);
        if (placed)
        {
            Select(placed);
            BeginDrag(world, touch);
            return true;
        }

        if (_candidate != null)
        {
            // Tap on open ground: move the held thing there (handy on phones), and keep dragging.
            SetCandidatePosition(Snap(world + (touch ? new Vector2(0f, touchLift) : Vector2.zero)));
            BeginDrag(world, touch);
            return true;
        }

        return false;
    }

    private void BeginDrag(Vector2 world, bool touch)
    {
        _dragging = true;
        _dragOffset = _candidate.position - world;
        if (touch) _dragOffset = new Vector2(_dragOffset.x, touchLift); // lift above the finger
    }

    private PlacedObject FindPlacedAt(Vector2 world)
    {
        PlacedObject best = null;
        float bestY = float.MaxValue;
        foreach (PlacedObject placed in _area.Spawned)
        {
            if (!placed || !Hits(placed, world)) continue;
            if (placed.IsOnSurface) return placed; // it's on top of whatever it's on
            float y = placed.transform.position.y; // lowest on screen is in front
            if (y < bestY)
            {
                bestY = y;
                best = placed;
            }
        }
        return best;
    }

    private bool Hits(PlacedObject placed, Vector2 world)
    {
        if (!placed) return false;
        Rect footprint = placed.FootprintRect;
        bool any = false;
        Bounds bounds = new(footprint.center, footprint.size);
        foreach (SpriteRenderer renderer in placed.GetComponentsInChildren<SpriteRenderer>())
        {
            if (renderer.sortingLayerName == "FloorDecals") continue; // the build markers
            if (!any) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            any = true;
        }
        bounds.Encapsulate(new Bounds(footprint.center, footprint.size));
        bounds.Expand(tapTolerance * 2f);
        return bounds.Contains(new Vector3(world.x, world.y, bounds.center.z));
    }

    // ----- Position and validity -----

    private Vector2 Snap(Vector2 world)
    {
        PlacementType placement = _candidate?.definition ? _candidate.definition.placement : PlacementType.Floor;
        if (placement == PlacementType.Surface)
        {
            SurfacePoint point = _area.FreeSurfaceNear(world, surfaceSnapRadius, _candidate?.obj);
            return point ? point.transform.position : world;
        }

        if (_candidate?.definition && _candidate.definition.alsoOnTables)
        {
            SurfacePoint point = _area.FreeSurfaceNear(world, surfaceSnapRadius, _candidate.obj);
            if (point) return point.transform.position;
        }

        Vector2 cell = _area.Grid.CellSize;
        float step = Mathf.Max(0.05f, Mathf.Min(cell.x, cell.y));
        Vector2 snapped = new(Mathf.Round(world.x / step) * step, Mathf.Round(world.y / step) * step);

        // Keep it near where it can go so it can't be dragged off into the sky.
        Bounds limits = placement == PlacementType.Wall ? WallBounds()
            : _area.Grid.WalkableArea ? _area.Grid.WalkableArea.bounds : new Bounds(Vector3.zero, Vector3.one * 100f);
        snapped.x = Mathf.Clamp(snapped.x, limits.min.x, limits.max.x);
        snapped.y = Mathf.Clamp(snapped.y, limits.min.y, limits.max.y);

        float span = _candidate?.definition ? _candidate.definition.chainSpan : 0f;
        if (span > 0f && ChainLights.TrySnap(span, snapped.x, snapped.y, _area.ChainPieces(_candidate.obj), chainSnapRadius, out float x, out float y))
            return new Vector2(x, y);
        return snapped;
    }

    /// <summary>Around every <see cref="PlacementZone"/>, for keeping Wall things near them.</summary>
    private Bounds WallBounds()
    {
        bool any = false;
        var bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
        foreach (PlacementZone zone in FindObjectsByType<PlacementZone>())
        {
            if (!any) bounds = zone.Bounds;
            else bounds.Encapsulate(zone.Bounds);
            any = true;
        }
        return bounds;
    }

    private void SetCandidatePosition(Vector2 position)
    {
        if (_candidate == null) return;
        bool moved = (position - _candidate.position).sqrMagnitude > 0.0001f;
        _candidate.position = position;
        if (_candidate.definition.CanGoOnTable) // sit it on the spot, to draw over the table
            _area.SitOn(_candidate.obj, _area.FreeSurfaceNear(position, 0.01f, _candidate.obj));
        _candidate.obj.transform.position = new Vector3(position.x, position.y, 0f);
        _candidate.obj.SetFlipped(_candidate.flipped);
        if (moved || !_candidate.checkedOnce) Validate(); // checking runs pathfinding, so only when it changes
    }

    private void Validate()
    {
        if (_candidate == null) return;
        _candidate.check = PlacementValidator.Check(_area, _candidate.definition, _candidate.position, _candidate.obj, _candidate.context);
        _candidate.checkedOnce = true;
        _area.RefreshChains(_candidate.obj);
        _candidate.obj.ShowBuildState(_candidate.check.ok);
        if (_ui) _ui.RefreshCandidate();
    }

    /// <summary>A free spot near the middle of the view, searched outwards. Falls back to the middle.</summary>
    private Vector2 FindStartSpot(PlaceableDefinition definition)
    {
        // The first free spot on a table, if that's what it needs.
        if (definition.placement == PlacementType.Surface)
        {
            SurfacePoint free = _area.FreeSurfaceNear(_area.Grid.WalkableArea ? _area.Grid.WalkableArea.bounds.center : Vector3.zero, float.MaxValue, _candidate?.obj);
            if (free) return free.transform.position;
        }

        // The middle of the floor or the tree (not the view: the camera slides while building).
        Bounds floor = definition.placement == PlacementType.Wall ? WallBounds()
            : _area.Grid.WalkableArea ? _area.Grid.WalkableArea.bounds : new Bounds(Vector3.zero, Vector3.one);
        Vector2 centre = Snap(floor.center);
        PlacementContext context = _candidate?.context ?? PlacementContext.For(_area);
        var tried = new HashSet<Vector2>();
        const float step = 0.5f;
        const int maxChecks = 120; // each check can run pathfinding; keep it quick on phones
        for (int ring = 0; ring <= 12 && tried.Count < maxChecks; ring++)
        {
            for (int dy = -ring; dy <= ring; dy++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    if (Mathf.Abs(dx) != ring && Mathf.Abs(dy) != ring) continue;
                    Vector2 spot = Snap(centre + new Vector2(dx, dy) * step);
                    if (!tried.Add(spot)) continue; // clamped onto a spot already tried
                    if (PlacementValidator.Check(_area, definition, spot, _candidate?.obj, context).ok) return spot;
                    if (tried.Count >= maxChecks) break;
                }
            }
        }
        return centre;
    }

    private static void SetPauseMenuAllowed(bool allowed)
    {
        // Escape cancels or leaves build mode instead of opening the pause menu.
        foreach (PauseInput pause in FindObjectsByType<PauseInput>()) pause.AllowPause = allowed;
    }
}
