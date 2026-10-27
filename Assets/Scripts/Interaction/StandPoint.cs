using UnityEngine;

/// <summary>
/// Where the gecko stands to use an interactable. Put one or more as children of the
/// interactable, on the walkable floor (a green cell); the one closest to the gecko is used.
/// The gecko turns to face the interactable on arrival.
/// </summary>
public class StandPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);
        if (transform.parent) Gizmos.DrawLine(transform.position, transform.parent.position);
    }
}
