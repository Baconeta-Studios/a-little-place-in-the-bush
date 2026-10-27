using UnityEngine;

/// <summary>
/// Where a guest will perch on a placeable (a chair has one, a bench two), and which way they face.
/// Guests aren't in yet: for now these only count seats, which quests and progression can use.
/// Put them as children of the placeable prefab's root (not inside Visual).
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
