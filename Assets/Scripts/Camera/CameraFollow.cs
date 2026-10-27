using UnityEngine;

/// <summary>
/// For areas wider (or taller) than one screen: follows the gecko smoothly and never shows past
/// the area's bounds. Single-screen areas like the cafe don't need it; if the bounds fit on screen
/// the camera just stays centred on them. Runs after <see cref="CameraFit"/> sets the zoom.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [Tooltip("The area the camera may show, usually the backdrop's bleed size.")]
    [SerializeField] private Rect bounds = new(-12f, -7.2f, 24f, 14.4f);
    [SerializeField, Min(0f)] private float smoothTime = 0.25f;
    [Tooltip("The gecko's feet are its position; look a little above them.")]
    [SerializeField] private Vector2 offset = new(0f, 1.5f);

    private Camera _camera;
    private Vector3 _velocity;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (!target && Interactor.Player) target = Interactor.Player.transform;
    }

    private void Start() => SnapToTarget();

    public void SnapToTarget()
    {
        if (!target && Interactor.Player) target = Interactor.Player.transform;
        if (!target) return;
        transform.position = Clamp(Desired());
        _velocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (!target) return;
        // Unscaled, so the camera still settles while a paused popup is open.
        transform.position = Vector3.SmoothDamp(transform.position, Clamp(Desired()), ref _velocity, smoothTime,
            Mathf.Infinity, Time.unscaledDeltaTime);
    }

    private Vector3 Desired() => new(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);

    private Vector3 Clamp(Vector3 position)
    {
        float halfHeight = _camera.orthographicSize;
        float halfWidth = halfHeight * _camera.aspect;
        position.x = ClampAxis(position.x, bounds.xMin, bounds.xMax, halfWidth);
        position.y = ClampAxis(position.y, bounds.yMin, bounds.yMax, halfHeight);
        return position;
    }

    private static float ClampAxis(float value, float min, float max, float halfView)
    {
        // Bounds narrower than the view: centre on them.
        if (max - min <= halfView * 2f) return (min + max) * 0.5f;
        return Mathf.Clamp(value, min + halfView, max - halfView);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
