using UnityEngine;

/// <summary>Who is interacting. Passed to every interactable so they don't need to look the player up.</summary>
public readonly struct InteractionContext
{
    public readonly Interactor interactor;

    public InteractionContext(Interactor interactor)
    {
        this.interactor = interactor;
    }

    public GameObject Player => interactor ? interactor.gameObject : null;
}

/// <summary>
/// Anything the gecko can walk up to and use: cafe stations, NPCs, the mailbox, the forage path,
/// area exits. Click or tap it, the gecko walks to a stand point, then <see cref="Interact"/> runs.
/// Most things should extend <see cref="Interactable"/> rather than implement this directly.
/// </summary>
public interface IInteractable
{
    /// <summary>Short verb phrase shown in the prompt, e.g. "Cook" or "Talk to Ruru".</summary>
    string Prompt { get; }

    Transform transform { get; }

    bool CanInteract(in InteractionContext context);

    void Interact(in InteractionContext context);

    /// <summary>Where the gecko should stand to use this, given where it's coming from.</summary>
    Vector2 GetStandPosition(Vector2 from);

    /// <summary>Set while the mouse pointer is over it (desktop only).</summary>
    void SetHovered(bool hovered);

    /// <summary>Set while the gecko is walking over to use it.</summary>
    void SetTargeted(bool targeted);
}
