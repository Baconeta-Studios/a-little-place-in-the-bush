using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Walks a character along paths from the area's <see cref="IPathfinder"/>, facing the way it's
/// going. Used by the gecko, and can be used by NPCs too. The object's position is its feet.
/// </summary>
public class CharacterMover : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float speed = 3.5f;
    [Tooltip("Child that holds the sprite. Its X scale is flipped to face left or right.")]
    [SerializeField] private Transform visual;
    [Tooltip("Whether the sprite art faces right when its X scale is positive.")]
    [SerializeField] private bool artFacesRight = true;
    [Tooltip("How sideways a step must be (0-1) before the character turns to face it. Higher stops flip-flopping when walking up or down.")]
    [SerializeField, Range(0f, 1f)] private float turnThreshold = 0.3f;
    [Tooltip("Optional. Gets a float \"Speed\" and a bool \"Walking\" if it has those parameters.")]
    [SerializeField] private Animator animator;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int WalkingParam = Animator.StringToHash("Walking");

    private readonly List<Vector2> _path = new();
    private int _nextWaypoint;
    private Action _onArrived;
    private IPathfinder _pathfinder;
    private bool _hasSpeedParam, _hasWalkingParam;

    /// <summary>Raised when a walk ends without arriving: a new destination, Stop() or Teleport().</summary>
    public event Action Cancelled;
    public event Action Arrived;

    public bool IsMoving => _nextWaypoint < _path.Count;
    public bool FacingRight { get; private set; } = true;
    public float SpeedMultiplier { get; set; } = 1f;
    public Vector2 Position => transform.position;
    public IReadOnlyList<Vector2> CurrentPath => _path;

    public IPathfinder Pathfinder
    {
        get
        {
            if (_pathfinder == null) _pathfinder = FindAnyObjectByType<GridPathfinder>();
            return _pathfinder;
        }
        set => _pathfinder = value;
    }

    private void Awake()
    {
        if (!visual && transform.childCount > 0) visual = transform.GetChild(0);
        if (animator)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == SpeedParam) _hasSpeedParam = true;
                if (parameter.nameHash == WalkingParam) _hasWalkingParam = true;
            }
        }
        FacingRight = !visual || (visual.localScale.x >= 0) == artFacesRight;
    }

    /// <summary>
    /// Walks towards <paramref name="destination"/>, or as close as it can get. Replaces any current
    /// walk. <paramref name="onArrived"/> runs on arrival, not if the walk is cancelled.
    /// </summary>
    public bool MoveTo(Vector2 destination, Action onArrived = null)
    {
        CancelCurrent();
        if (Pathfinder == null)
        {
            Debug.LogWarning("[CharacterMover] No pathfinder in the scene.", this);
            return false;
        }

        if (!Pathfinder.TryFindPath(Position, destination, _path)) return false;

        _nextWaypoint = 0;
        _onArrived = onArrived;
        // Already there: arrive straight away so interactions still happen.
        if (_path.Count == 0 || (_path.Count == 1 && Vector2.Distance(Position, _path[0]) < 0.01f))
        {
            _path.Clear();
            Arrive();
        }
        return true;
    }

    public void Stop() => CancelCurrent();

    /// <summary>Jumps to a point (snapped onto the walkable floor), cancelling any walk.</summary>
    public void Teleport(Vector2 point)
    {
        CancelCurrent();
        if (Pathfinder != null && Pathfinder.TryGetNearestWalkable(point, out Vector2 nearest)) point = nearest;
        transform.position = new Vector3(point.x, point.y, transform.position.z);
    }

    public void FaceTowards(Vector2 point)
    {
        float dx = point.x - Position.x;
        if (Mathf.Abs(dx) > 0.01f) SetFacing(dx > 0);
    }

    public void SetFacing(bool right)
    {
        FacingRight = right;
        if (!visual) return;
        Vector3 scale = visual.localScale;
        float magnitude = Mathf.Abs(scale.x);
        scale.x = right == artFacesRight ? magnitude : -magnitude;
        visual.localScale = scale;
    }

    private void Update()
    {
        if (!IsMoving)
        {
            UpdateAnimator(0f);
            return;
        }

        float step = speed * SpeedMultiplier * Time.deltaTime;
        Vector2 position = Position;
        while (step > 0f && IsMoving)
        {
            Vector2 target = _path[_nextWaypoint];
            float distance = Vector2.Distance(position, target);
            // Only turn round when clearly heading sideways; walking (nearly) straight up or down
            // would otherwise flip the sprite back and forth on every tiny wobble in the path.
            if (distance > 0.0001f && Mathf.Abs(target.x - position.x) > distance * turnThreshold) FaceTowards(target);

            if (distance <= step)
            {
                position = target;
                step -= distance;
                _nextWaypoint++;
            }
            else
            {
                position = Vector2.MoveTowards(position, target, step);
                step = 0f;
            }
        }

        transform.position = new Vector3(position.x, position.y, transform.position.z);
        UpdateAnimator(speed * SpeedMultiplier);

        if (!IsMoving)
        {
            _path.Clear();
            Arrive();
        }
    }

    private void Arrive()
    {
        Action callback = _onArrived;
        _onArrived = null;
        UpdateAnimator(0f);
        Arrived?.Invoke();
        callback?.Invoke();
    }

    private void CancelCurrent()
    {
        bool wasWalking = IsMoving || _onArrived != null;
        _path.Clear();
        _nextWaypoint = 0;
        _onArrived = null;
        if (wasWalking) Cancelled?.Invoke();
    }

    private void UpdateAnimator(float currentSpeed)
    {
        if (!animator) return;
        if (_hasSpeedParam) animator.SetFloat(SpeedParam, currentSpeed);
        if (_hasWalkingParam) animator.SetBool(WalkingParam, currentSpeed > 0f);
    }

    private void OnDrawGizmosSelected()
    {
        if (_path.Count == 0) return;
        Gizmos.color = Color.yellow;
        Vector2 previous = Position;
        for (int i = _nextWaypoint; i < _path.Count; i++)
        {
            Gizmos.DrawLine(previous, _path[i]);
            previous = _path[i];
        }
    }
}
