using UnityEngine;

/// <summary>
/// For areas wider (or taller) than one screen: follows the gecko and never shows past the area's
/// bounds. The camera stays still while the gecko walks around the middle of the view (the dead
/// zone) and only moves to keep it inside, which avoids the constant tiny corrections that read
/// as jitter. If the bounds fit on screen the camera just stays centred on them.
/// Runs after <see cref="CameraFit"/> sets the zoom.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [Tooltip("The area the camera may show, usually the backdrop's size.")]
    [SerializeField] private Rect bounds = new(-12f, -7.2f, 24f, 14.4f);
    [Tooltip("How far (world units, each way from the middle) the gecko can walk before the camera moves.")]
    [SerializeField] private Vector2 deadZone = new(3f, 1.5f);
    [Tooltip("How quickly the camera catches up once it does move. Lower is snappier.")]
    [SerializeField, Min(0.01f)] private float smoothTime = 0.3f;
    [Tooltip("The gecko's feet are its position; look a little above them.")]
    [SerializeField] private Vector2 offset = new(0f, 1.5f);

    private Camera _camera;
    private Vector3 _velocity;
    private Vector2 _focus;

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
        _focus = Watched();
        transform.position = Clamp(new Vector3(_focus.x, _focus.y, transform.position.z));
        _velocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (!target) return;

        // Move the focus only as far as needed to keep the gecko inside the dead zone.
        Vector2 watched = Watched();
        if (watched.x > _focus.x + deadZone.x) _focus.x = watched.x - deadZone.x;
        else if (watched.x < _focus.x - deadZone.x) _focus.x = watched.x + deadZone.x;
        if (watched.y > _focus.y + deadZone.y) _focus.y = watched.y - deadZone.y;
        else if (watched.y < _focus.y - deadZone.y) _focus.y = watched.y + deadZone.y;

        Vector3 goal = Clamp(new Vector3(_focus.x, _focus.y, transform.position.z));
        // Game time, the same clock the gecko moves on, so the two never drift against each other.
        // (While the game is paused nothing moves, so neither does the camera.)
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref _velocity, smoothTime, Mathf.Infinity, deltaTime);
    }

    private Vector2 Watched() => (Vector2)target.position + offset;

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
        Vector3 centre = Application.isPlaying ? (Vector3)_focus : transform.position;
        Gizmos.DrawWireCube(centre, new Vector3(deadZone.x * 2f, deadZone.y * 2f, 0f));
    }
}
