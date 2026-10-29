using UnityEngine;

/// <summary>
/// Where a guest perches on a placeable (a chair has one, a bench two), and which way they face.
/// Visiting guests sit on free seats the gecko can reach (<see cref="GuestSeating"/>). Put them as
/// children of the placeable prefab's root (not inside Visual), at the height a guest's feet should be.
/// </summary>
public class SeatPoint : MonoBehaviour
{
    [SerializeField] private bool faceRight = true;

    /// <summary>Facing in the world, taking a flipped placeable into account.</summary>
    public bool FaceRight => transform.lossyScale.x >= 0 ? faceRight : !faceRight;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.25f);
        Vector3 position = transform.position;
        Gizmos.DrawWireCube(position, new Vector3(0.3f, 0.15f, 0f));
        Gizmos.DrawLine(position, position + new Vector3(FaceRight ? 0.35f : -0.35f, 0f, 0f));
    }
}
