using UnityEngine;

/// <summary>
/// A soft shadow on the floor. Every floor placeable gets one by itself (set on its asset, or in Tools > Bush >
/// Placeable Shadows and Bases). Add this to anything else that should have one: the gecko, a character, a scene prop.
/// A placeable with its own child called Shadow keeps that instead. Characters' shadows turn round with them
/// (see <see cref="FacingShadow"/>), and highlighting leaves them alone.
/// </summary>
public class GroundShadow : MonoBehaviour
{
    public const string ChildName = "GroundShadow";
    public const string OwnArtName = "Shadow";
    private const string SortingLayer = "FloorDecals";
    private const int SortingOrder = 5;

    [Tooltip("In world units, whatever this object's scale is.")]
    [SerializeField] private Vector2 size = new(1f, 0.35f);
    [Tooltip("From this object's position, in world units.")]
    [SerializeField] private Vector2 offset;
    [SerializeField, Range(0f, 1f)] private float strength = 0.45f;

    private void Awake() => Create(transform, size, offset, strength);

    /// <summary>Whether this is a shadow on the floor, rather than part of what's casting it.</summary>
    public static bool IsShadow(Component renderer) => renderer.name is ChildName or OwnArtName;

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

/// <summary>
/// Turns a character's shadow round with their art. It sits beside the art rather than inside it (the art holds the
/// scale and animation), so it's mirrored here. Finds a child called Shadow or GroundShadow by itself.
/// </summary>
public class FacingShadow
{
    private readonly Transform _owner;
    private readonly bool _artStartedFlipped;
    private Transform _shadow;
    private float _restX;
    private float _restScaleX;

    public FacingShadow(Transform owner, Transform art)
    {
        _owner = owner;
        _artStartedFlipped = art && art.localScale.x < 0f;
    }

    /// <summary>Call after flipping the art.</summary>
    public void Match(Transform art)
    {
        if (!art) return;
        if (!_shadow) // looked for here because a GroundShadow only makes its child when it wakes up
        {
            _shadow = _owner.Find(GroundShadow.OwnArtName);
            if (!_shadow) _shadow = _owner.Find(GroundShadow.ChildName);
            if (!_shadow) return;
            _restX = _shadow.localPosition.x;
            _restScaleX = _shadow.localScale.x;
        }

        float side = art.localScale.x < 0f != _artStartedFlipped ? -1f : 1f;
        Vector3 position = _shadow.localPosition;
        position.x = _restX * side;
        _shadow.localPosition = position;
        Vector3 scale = _shadow.localScale;
        scale.x = _restScaleX * side;
        _shadow.localScale = scale;
    }
}
