using UnityEngine;

/// <summary>
/// Keeps the 16:9 safe area fully on screen at any aspect ratio by setting the orthographic size, then steps back a
/// little further where the art has room, without ever showing past its edges.
/// <see cref="Zoom"/> lets build mode zoom: 1 is the normal view, 2 is twice as close, and <see cref="BuildZoom"/> is
/// as far out as building goes.
/// </summary>
[ExecuteAlways, RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
    [SerializeField] private Vector2 safeSize = new(19.2f, 10.8f);
    [Tooltip("The size of the backdrop. The view never shows past this.")]
    [SerializeField] private Vector2 artSize = new(24f, 14.4f);
    [Tooltip("How far past the safe area the normal view steps back, where the art has room. 1.15 is 15%.")]
    [SerializeField, Min(1f)] private float normalZoomOut = 1.15f;
    [Tooltip("The same, in build mode.")]
    [SerializeField, Min(1f)] private float buildZoomOut = 1.25f;
    private Camera _camera;
    private float _zoom = 1f;

    /// <summary>1 = the normal view. Higher zooms in. Never further out than <see cref="BuildZoom"/>.</summary>
    public float Zoom
    {
        get => _zoom;
        set => _zoom = Mathf.Max(BuildZoom, value);
    }

    /// <summary>The orthographic size of the normal view, before zooming.</summary>
    public float FitSize => SizeSteppedBackBy(normalZoomOut);

    /// <summary>The zoom for build mode's wider view: 1 or a little under.</summary>
    public float BuildZoom => FitSize / Mathf.Max(FitSize, SizeSteppedBackBy(buildZoomOut));

    private float SizeSteppedBackBy(float amount)
    {
        if (!_camera) _camera = GetComponent<Camera>();
        float safe = Mathf.Max(safeSize.y * 0.5f, safeSize.x * 0.5f / _camera.aspect);
        float mostTheArtAllows = Mathf.Min(artSize.y * 0.5f, artSize.x * 0.5f / _camera.aspect);
        return Mathf.Max(safe, Mathf.Min(safe * amount, mostTheArtAllows));
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
