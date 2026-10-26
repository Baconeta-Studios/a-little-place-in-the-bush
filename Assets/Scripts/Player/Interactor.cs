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

    private CharacterMover _mover;
    private IInteractable _target;
    private bool _startingWalk;

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
        ClearTarget();

        _target = target;
        target.SetTargeted(true);

        Vector2 stand = target.GetStandPosition(transform.position);
        float range = target is Interactable interactable ? interactable.InteractRange : 0.35f;
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
        if (target.CanInteract(context))
        {
            target.Interact(context);
            Interacted?.Invoke(target);
        }
        else
        {
            InteractionRefused?.Invoke(target);
        }
    }

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
