using System;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Walks the gecko to an interactable's stand point, then uses it. Clicking somewhere else on the
/// way cancels it. Lives on the gecko next to <see cref="CharacterMover"/>.
/// </summary>
[RequireComponent(typeof(CharacterMover))]
public class Interactor : MonoBehaviour
{
    [Tooltip("The player's interactor. Prompts measure distance to it.")]
    [SerializeField] private bool isPlayer = true;

    [Tooltip("Ignore a second interaction with the same thing for this long (stops double taps using it twice).")]
    [SerializeField, Min(0f)] private float repeatCooldown = 0.4f;

    private CharacterMover _mover;
    private IInteractable _target;
    private Vector2 _targetStand;
    private bool _startingWalk;
    private IInteractable _lastUsed;
    private float _lastUsedTime = -10f;

    public static Interactor Player { get; private set; }

    public event Action<IInteractable> Interacted;
    /// <summary>The gecko got there but the thing couldn't be used (e.g. locked, or nothing to do yet).</summary>
    public event Action<IInteractable> InteractionRefused;

    public IInteractable Target => _target;
    public CharacterMover Mover => _mover ? _mover : _mover = GetComponent<CharacterMover>();

    private void Awake()
    {
        _mover = GetComponent<CharacterMover>();
        if (isPlayer) Player = this;
    }

    private void OnEnable() => Mover.Cancelled += OnWalkCancelled;
    private void OnDisable() => Mover.Cancelled -= OnWalkCancelled;

    private void OnDestroy()
    {
        if (Player == this) Player = null;
    }

    /// <summary>Walk to <paramref name="target"/> and use it on arrival.</summary>
    public void Request(IInteractable target)
    {
        if (target == null) return;
        if (ReferenceEquals(target, _lastUsed) && Time.unscaledTime - _lastUsedTime < repeatCooldown) return;
        ClearTarget();

        _target = target;
        target.SetTargeted(true);

        Vector2 stand = target.GetStandPosition(transform.position);
        _targetStand = stand;
        float range = RangeOf(target);
        if (Vector2.Distance(transform.position, stand) <= range)
        {
            // Stopping fires Cancelled; don't let that clear the target we just set.
            _startingWalk = true;
            Mover.Stop();
            _startingWalk = false;
            Arrive(target);
            return;
        }

        _startingWalk = true;
        bool walking = Mover.MoveTo(stand, () => Arrive(target));
        _startingWalk = false;
        if (!walking) ClearTarget();
    }

    public void Cancel()
    {
        Mover.Stop();
        ClearTarget();
    }

    private void Arrive(IInteractable target)
    {
        if (target != _target) return; // replaced while walking
        ClearTarget();
        if (target is Object unityObject && !unityObject) return; // destroyed on the way

        var context = new InteractionContext(this);
        Mover.FaceTowards(target.transform.position);

        // The path may have stopped short (the stand point is fenced off or off the floor).
        float slack = RangeOf(target) + 0.3f;
        bool reached = Vector2.Distance(transform.position, _targetStand) <= slack;
        if (reached && target.CanInteract(context))
        {
            _lastUsed = target;
            _lastUsedTime = Time.unscaledTime;
            target.Interact(context);
            Interacted?.Invoke(target);
        }
        else
        {
            InteractionRefused?.Invoke(target);
        }
    }

    private static float RangeOf(IInteractable target) => target is Interactable interactable ? interactable.InteractRange : 0.35f;

    private void OnWalkCancelled()
    {
        if (!_startingWalk) ClearTarget();
    }

    private void ClearTarget()
    {
        // The target may have been destroyed while the gecko walked over.
        if (_target is Object unityObject && unityObject) _target.SetTargeted(false);
        _target = null;
    }
}
