using System.Collections.Generic;
using Debugging;
using UI.Popups;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Click or tap to walk and interact (per the GDD). Reads the Gameplay map of BushInput.inputactions.
/// A press on UI does nothing here. A press on an interactable walks over and uses it (the front-most
/// one wins, with a little tolerance for fingers). Anywhere else walks to the nearest point the gecko
/// can reach. Holding the button keeps steering, which feels better on long walks.
/// </summary>
[RequireComponent(typeof(Interactor))]
public class PlayerPointerInput : MonoBehaviour
{
    [SerializeField] private InputActionAsset actions;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask interactableMask;
    [Tooltip("Extra radius (world units) around a tap that still counts as touching an interactable.")]
    [SerializeField, Min(0f)] private float tapTolerance = 0.25f;
    [Tooltip("Keep walking towards the pointer while the button or finger is held down.")]
    [SerializeField] private bool holdToSteer = true;
    [SerializeField, Min(0.05f)] private float steerInterval = 0.15f;
    [Tooltip("Optional marker spawned where the player clicks the floor.")]
    [SerializeField] private GameObject clickMarkerPrefab;

    private static readonly List<RaycastResult> UiHits = new();
    private readonly Collider2D[] _overlaps = new Collider2D[16];

    private InputAction _point;
    private InputAction _click;
    private Interactor _interactor;
    private CharacterMover _mover;
    private IInteractable _hovered;
    private bool _steering;
    private float _nextSteerTime;
    private Vector2 _lastSteerTarget;
    private GameObject _clickMarker;

    private void Awake()
    {
        _interactor = GetComponent<Interactor>();
        _mover = GetComponent<CharacterMover>();
        if (!worldCamera) worldCamera = Camera.main;
        if (interactableMask == 0) interactableMask = LayerMask.GetMask("Interactable");

        if (actions)
        {
            InputActionMap map = actions.FindActionMap("Gameplay", true);
            _point = map.FindAction("Point", true);
            _click = map.FindAction("Click", true);
        }
        else
        {
            // Works without the asset, e.g. in a quick test scene.
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
        _point.Disable();
        _click.Disable();
        SetHovered(null);
        _steering = false;
    }

    private void Update()
    {
        if (!worldCamera) worldCamera = Camera.main;
        if (!worldCamera) return;

        bool blocked = InputLock.IsLocked || PopupManager.AnyOpen || CheatMenu.IsOpen;
        Vector2 screen = _point.ReadValue<Vector2>();
        bool overUi = IsOverUi(screen);
        Vector2 world = worldCamera.ScreenToWorldPoint(screen);

        UpdateHover(blocked || overUi ? null : FindInteractable(world, 0f));

        if (blocked)
        {
            _steering = false;
            return;
        }

        if (_click.WasPressedThisFrame())
        {
            _steering = false;
            if (overUi) return;

            IInteractable target = FindInteractable(world, tapTolerance);
            if (target != null)
            {
                _interactor.Request(target);
                return;
            }

            WalkTo(world);
            _steering = holdToSteer;
            _nextSteerTime = Time.unscaledTime + steerInterval;
            _lastSteerTarget = world;
            return;
        }

        if (_steering)
        {
            if (!_click.IsPressed() || overUi)
            {
                _steering = false;
                return;
            }

            if (Time.unscaledTime >= _nextSteerTime && Vector2.Distance(world, _lastSteerTarget) > 0.3f)
            {
                _nextSteerTime = Time.unscaledTime + steerInterval;
                _lastSteerTarget = world;
                WalkTo(world, showMarker: false);
            }
        }
    }

    private void WalkTo(Vector2 world, bool showMarker = true)
    {
        _interactor.Cancel();
        if (!_mover.MoveTo(world)) return;
        if (showMarker && clickMarkerPrefab) ShowClickMarker(_mover.CurrentPath.Count > 0 ? _mover.CurrentPath[^1] : world);
    }

    /// <summary>The front-most interactable at a point (lowest on screen = closest to the camera).</summary>
    private IInteractable FindInteractable(Vector2 world, float tolerance)
    {
        int count = Physics2D.OverlapPoint(world, ContactFilter(), _overlaps);
        if (count == 0 && tolerance > 0f) count = Physics2D.OverlapCircle(world, tolerance, ContactFilter(), _overlaps);

        IInteractable best = null;
        float bestY = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var interactable = _overlaps[i].GetComponentInParent<IInteractable>();
            if (interactable == null) continue;
            float y = interactable.transform.position.y;
            if (y < bestY)
            {
                bestY = y;
                best = interactable;
            }
        }
        return best;
    }

    private ContactFilter2D ContactFilter()
    {
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(interactableMask);
        return filter;
    }

    private void UpdateHover(IInteractable hovered)
    {
        // Hover highlights are a desktop nicety; touch has no hover, so the prompt covers that.
        if (Pointer.current is not Mouse) hovered = null;
        SetHovered(hovered);
    }

    private void SetHovered(IInteractable hovered)
    {
        if (ReferenceEquals(hovered, _hovered)) return;
        if (_hovered is Object old && old) _hovered.SetHovered(false);
        _hovered = hovered;
        _hovered?.SetHovered(true);
    }

    /// <summary>True if the pointer is over any UI graphic that blocks raycasts (buttons, panels, popups).</summary>
    private static bool IsOverUi(Vector2 screen)
    {
        EventSystem eventSystem = EventSystem.current;
        if (!eventSystem) return false;

        var data = new PointerEventData(eventSystem) { position = screen };
        UiHits.Clear();
        eventSystem.RaycastAll(data, UiHits);
        foreach (RaycastResult hit in UiHits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }

    private void ShowClickMarker(Vector2 position)
    {
        if (!_clickMarker) _clickMarker = Instantiate(clickMarkerPrefab);
        _clickMarker.transform.position = position;
        _clickMarker.SetActive(false);
        _clickMarker.SetActive(true); // restart any animation on it
    }
}
