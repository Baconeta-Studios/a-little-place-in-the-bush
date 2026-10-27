using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Base for everything the gecko can use. Needs a Collider2D on the Interactable layer (on this
/// object or a child) covering what the player sees, so it can be clicked or tapped. On its own it
/// logs and fires <see cref="onInteract"/>; subclasses (AreaExit, CafeCounter, Npc...) do more.
/// </summary>
public class Interactable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Use";
    [SerializeField] private bool interactionEnabled = true;
    [Tooltip("If the gecko is already this close to a stand point, interact without walking.")]
    [SerializeField, Min(0f)] private float interactRange = 0.35f;
    [SerializeField] private UnityEvent onInteract = new();

    private StandPoint[] _standPoints;

    public event Action<bool, bool> HighlightChanged;

    public virtual string Prompt => prompt;
    public bool IsHovered { get; private set; }
    public bool IsTargeted { get; private set; }
    public float InteractRange => interactRange;

    public bool InteractionEnabled
    {
        get => interactionEnabled;
        set => interactionEnabled = value;
    }

    protected virtual void Awake()
    {
        _standPoints = GetComponentsInChildren<StandPoint>();
    }

    public virtual bool CanInteract(in InteractionContext context) => interactionEnabled && isActiveAndEnabled;

    public virtual void Interact(in InteractionContext context)
    {
        Debug.Log($"[Interactable] {name}: {Prompt}", this);
        onInteract.Invoke();
    }

    /// <summary>The stand point closest to the gecko, or the nearest walkable spot to this object if it has none.</summary>
    public virtual Vector2 GetStandPosition(Vector2 from)
    {
        if (_standPoints == null || _standPoints.Length == 0) return transform.position;

        StandPoint best = null;
        float bestDistance = float.MaxValue;
        foreach (StandPoint point in _standPoints)
        {
            if (!point || !point.isActiveAndEnabled) continue;
            float distance = ((Vector2)point.transform.position - from).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = point;
            }
        }

        return best ? best.transform.position : transform.position;
    }

    public void SetHovered(bool hovered)
    {
        if (hovered == IsHovered) return;
        IsHovered = hovered;
        HighlightChanged?.Invoke(IsHovered, IsTargeted);
    }

    public void SetTargeted(bool targeted)
    {
        if (targeted == IsTargeted) return;
        IsTargeted = targeted;
        HighlightChanged?.Invoke(IsHovered, IsTargeted);
    }

    protected virtual void OnValidate()
    {
        int layer = LayerMask.NameToLayer("Interactable");
        if (layer < 0) return;
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true))
            if (collider.gameObject.layer == layer) return;
        Debug.LogWarning($"[Interactable] {name} has no Collider2D on the Interactable layer, so it can't be clicked.", this);
    }
}
