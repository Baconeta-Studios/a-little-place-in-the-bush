using UnityEngine;

/// <summary>
/// Where the gecko appears when it enters this area. An <see cref="AreaExit"/> in another area
/// names the spawn ID to arrive at (e.g. "from_garden"); the default one is used otherwise.
/// Put it on the walkable floor, next to the matching exit.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private string id = "default";
    [SerializeField] private bool isDefault;
    [SerializeField] private bool faceRight = true;

    public string Id => id;
    public bool IsDefault => isDefault;
    public bool FaceRight => faceRight;

    /// <summary>The spawn with this ID in the loaded scenes, else the default, else any.</summary>
    public static SpawnPoint Find(string spawnId)
    {
        SpawnPoint[] all = FindObjectsByType<SpawnPoint>();
        SpawnPoint fallback = null;
        foreach (SpawnPoint spawn in all)
        {
            if (!string.IsNullOrEmpty(spawnId) && spawn.id == spawnId) return spawn;
            if (spawn.isDefault || !fallback) fallback = spawn;
        }
        return fallback;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isDefault ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.6f, 0.2f);
        Vector3 position = transform.position;
        Gizmos.DrawWireSphere(position, 0.25f);
        Gizmos.DrawLine(position, position + new Vector3(faceRight ? 0.5f : -0.5f, 0f, 0f));
    }
}
