using UnityEngine;

/// <summary>
/// Keeps the 16:9 safe area fully on screen at any aspect ratio by setting the orthographic size.
/// <see cref="Zoom"/> lets build mode zoom in: 1 is the normal fitted view, 2 is twice as close.
/// </summary>
[ExecuteAlways, RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
    [SerializeField] private Vector2 safeSize = new(19.2f, 10.8f);
    private Camera _camera;
    private float _zoom = 1f;

    /// <summary>1 = the whole safe area fits. Higher zooms in. Never below 1 here, so the safe area can't be lost.</summary>
    public float Zoom
    {
        get => _zoom;
        set => _zoom = Mathf.Max(1f, value);
    }

    /// <summary>The orthographic size that fits the safe area, before zooming.</summary>
    public float FitSize
    {
        get
        {
            if (!_camera) _camera = GetComponent<Camera>();
            return Mathf.Max(safeSize.y * 0.5f, safeSize.x * 0.5f / _camera.aspect);
        }
    }

    private void LateUpdate()
    {
        if (!_camera) _camera = GetComponent<Camera>();
        _camera.orthographicSize = FitSize / (Application.isPlaying ? _zoom : 1f);
    }

    /// <summary>Applies the zoom straight away, for code that needs the new size this frame.</summary>
    public void ApplyNow() => LateUpdate();

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, safeSize);
    }
}
