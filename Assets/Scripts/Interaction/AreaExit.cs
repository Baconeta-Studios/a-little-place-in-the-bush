using Eflatun.SceneReference;
using UnityEngine;

/// <summary>
/// A path at the edge of an area that leads to another (cafe to garden, garden to cafe...).
/// Click it, the gecko walks to its stand point at the edge, then <see cref="AreaTravel"/> takes over.
/// </summary>
public class AreaExit : Interactable
{
    [SerializeField] private SceneReference targetScene;
    [Tooltip("SpawnPoint id in the target scene, e.g. from_cafe. Empty for its default spawn.")]
    [SerializeField] private string targetSpawnId;
    [SerializeField] private string destinationName = "the garden";

    public override string Prompt => $"Go to {destinationName}";

    public override bool CanInteract(in InteractionContext context) =>
        base.CanInteract(context) && HasTarget && !AreaTravel.Instance.IsTravelling;

    public override void Interact(in InteractionContext context)
    {
        base.Interact(context);
        AreaTravel.Instance.Travel(targetScene.Name, targetSpawnId);
    }

    private bool HasTarget
    {
        get
        {
            bool valid = targetScene != null && targetScene.State == SceneReferenceState.Regular;
            if (!valid) Debug.LogWarning($"[AreaExit] {name} has no target scene yet, or it isn't in the build list.", this);
            return valid;
        }
    }
}
