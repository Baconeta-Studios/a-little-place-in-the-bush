using UnityEngine;

/// <summary>
/// A soft shadow on the floor. Every floor placeable gets one by itself (set on its asset, or in Tools > Bush >
/// Placeable Shadows and Bases). Add this to anything else that should have one: the gecko, a character, a scene prop.
/// A placeable with its own child called Shadow keeps that instead.
/// </summary>
public class GroundShadow : MonoBehaviour
{
    public const string ChildName = "GroundShadow";
    private const string SortingLayer = "FloorDecals";
    private const int SortingOrder = 5;

    [Tooltip("In world units, whatever this object's scale is.")]
    [SerializeField] private Vector2 size = new(1f, 0.35f);
    [Tooltip("From this object's position, in world units.")]
    [SerializeField] private Vector2 offset;
    [SerializeField, Range(0f, 1f)] private float strength = 0.45f;

    private void Awake() => Create(transform, size, offset, strength);

    public static GameObject Create(Transform parent, Vector2 size, Vector2 offset, float strength)
    {
        var shadow = new GameObject(ChildName);
        shadow.transform.SetParent(parent, false);
        Vector3 scale = parent.lossyScale;
        float x = Mathf.Max(0.0001f, Mathf.Abs(scale.x)), y = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
        shadow.transform.localPosition = new Vector3(offset.x / x, offset.y / y, 0f);
        shadow.transform.localScale = new Vector3(size.x / x, size.y / y, 1f);
        var renderer = shadow.AddComponent<SpriteRenderer>();
        renderer.sprite = GeneratedSprites.SoftShadow;
        renderer.color = new Color(0.1f, 0.08f, 0.05f, strength);
        renderer.sortingLayerName = SortingLayer;
        renderer.sortingOrder = SortingOrder;
        return shadow;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(transform.position + (Vector3)offset, size);
    }
}
