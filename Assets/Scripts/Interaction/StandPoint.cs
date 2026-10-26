using UnityEngine;

/// <summary>
/// Where the gecko stands to use an interactable. Put one or more as children of the
/// interactable, on the walkable floor; the closest reachable one is used.
/// </summary>
public class StandPoint : MonoBehaviour
{
    [Tooltip("Turn the gecko to face the interactable on arrival.")]
    [SerializeField] private bool faceInteractable = true;

    public bool FaceInteractable => faceInteractable;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);
        if (transform.parent) Gizmos.DrawLine(transform.position, transform.parent.position);
    }
}
