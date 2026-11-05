using UnityEngine;

/// <summary>
/// For areas wider (or taller) than one screen: follows the gecko and never shows past the area's
/// bounds. Sideways, the camera stays still while the gecko walks around the middle of the view (the
/// dead zone) and only moves to keep it inside, which avoids the constant tiny corrections that read
/// as jitter. Up and down, it sits as high as it can, showing the tree and the path into the bush, and
/// only comes down to keep the gecko above the bottom of the screen. If the bounds fit on screen the
/// camera just stays centred on them.
/// Runs after <see cref="CameraFit"/> sets the zoom.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [Tooltip("The area the camera may show, usually the backdrop's size.")]
    [SerializeField] private Rect bounds = new(-12f, -7.2f, 24f, 14.4f);
    [Tooltip("How far (world units, each way from the middle) the gecko can walk sideways before the camera moves.")]
    [SerializeField, Min(0f)] private float deadZoneWidth = 3f;
    [Tooltip("How far (world units) the gecko's feet stay above the bottom of the screen. Lower keeps more of the top in view.")]
    [SerializeField, Min(0f)] private float bottomMargin = 2f;
    [Tooltip("How quickly the camera catches up once it does move. Lower is snappier.")]
    [SerializeField, Min(0.01f)] private float smoothTime = 0.3f;

    private Camera _camera;
    private Vector3 _velocity;
    private float _focusX;

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
        _focusX = target.position.x;
        transform.position = Goal();
        _velocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (!target) return;

        // Move the focus only as far as needed to keep the gecko inside the dead zone.
        float x = target.position.x;
        if (x > _focusX + deadZoneWidth) _focusX = x - deadZoneWidth;
        else if (x < _focusX - deadZoneWidth) _focusX = x + deadZoneWidth;

        // Game time, the same clock the gecko moves on, so the two never drift against each other.
        // (While the game is paused nothing moves, so neither does the camera.)
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;
        transform.position = Vector3.SmoothDamp(transform.position, Goal(), ref _velocity, smoothTime, Mathf.Infinity, deltaTime);
    }

    /// <summary>Where the camera wants to be: Clamp stops it at the top of the bounds, so this is as high as it can go with the gecko still in view.</summary>
    private Vector3 Goal() =>
        Clamp(new Vector3(_focusX, target.position.y + _camera.orthographicSize - bottomMargin, transform.position.z));

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
        Gizmos.color = new Color(1f, 0.85f, 0.3f);
        Vector3 centre = transform.position;
        if (Application.isPlaying) centre.x = _focusX;
        Gizmos.DrawWireCube(centre, new Vector3(deadZoneWidth * 2f, 0.1f, 0f));
    }
}
