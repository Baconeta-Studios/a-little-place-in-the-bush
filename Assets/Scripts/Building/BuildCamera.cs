using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pan and zoom while building, so you can get close to place things precisely.
/// - Touch: pinch with two fingers to zoom, drag with two fingers to pan.
/// - Mouse: scroll to zoom (towards the pointer), drag with the right or middle button to pan.
/// - Either: drag on empty ground (while not holding anything) to pan.
/// Never zooms out past the normal view or shows past the backdrop. On entering, the camera slides
/// down so the floor sits above the tray; on leaving it eases back to the normal view.
/// Added and driven by <see cref="BuildMode"/>.
/// </summary>
public class BuildCamera : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxZoom = 2.5f;
    [Tooltip("Zoom change per scroll-wheel notch.")]
    [SerializeField, Min(1.01f)] private float wheelStep = 1.15f;
    [SerializeField, Min(0f)] private float easeTime = 0.3f;

    private Camera _camera;
    private CameraFit _fit;
    private BuildArea _area;
    private BuildUi _ui;
    private Vector3 _restPosition;
    private float _restZoom = 1f;
    private CameraFollow _follow;
    private bool _followWasEnabled;
    private Coroutine _ease;
    private bool _returning;

    private float _bottomCover;
    private float _topCover;
    private Vector2Int _coverScreenSize;

    private bool _pinching;
    private float _pinchStartDistance;
    private float _pinchStartZoom;
    private Vector2 _pinchPreviousMid;
    private bool _buttonPanning;
    private Vector2 _buttonPrevious;
    private Vector2 _dragPrevious;

    public bool IsActive { get; private set; }

    public void Begin(BuildArea area, BuildUi ui)
    {
        _camera = Camera.main;
        if (!_camera) return;
        _fit = _camera.GetComponent<CameraFit>();
        _area = area;
        _ui = ui;
        IsActive = true;

        if (_returning)
        {
            // Re-entered while still easing back: keep the real resting view rather than the half-way point.
            StopEase();
            _returning = false;
        }
        else
        {
            _restPosition = _camera.transform.position;
            _restZoom = _fit ? _fit.Zoom : 1f;
            _follow = _camera.GetComponent<CameraFollow>();
            _followWasEnabled = _follow && _follow.enabled;
        }
        if (_follow) _follow.enabled = false;

        RefreshCovers();
        float bottomWorld = _bottomCover * _camera.orthographicSize * 2f;
        Vector3 target = Clamped(_restPosition + new Vector3(0f, -bottomWorld, 0f), _camera.orthographicSize);
        EaseTo(target, 1f, null);
    }

    public void End()
    {
        if (!IsActive) return;
        IsActive = false;
        _pinching = _buttonPanning = false;
        if (!_camera) return;
        _returning = true;
        EaseTo(_restPosition, _restZoom, () =>
        {
            _returning = false;
            if (_follow) _follow.enabled = _followWasEnabled;
        });
    }

    /// <summary>For unloading: put things back immediately, no easing.</summary>
    public void EndImmediately()
    {
        if (!IsActive && !_returning) return;
        IsActive = false;
        _returning = false;
        if (_ease != null) StopCoroutine(_ease);
        if (_camera) _camera.transform.position = _restPosition;
        if (_fit) _fit.Zoom = _restZoom;
        if (_follow) _follow.enabled = _followWasEnabled;
    }

    /// <summary>
    /// Pinch, two-finger pan, scroll and right/middle-button pan. Returns true while one of those is
    /// happening (and on the frame a pinch ends), so build mode leaves items alone.
    /// </summary>
    public bool HandleGestures()
    {
        if (!IsActive || !_camera) return false;
        if (Screen.width != _coverScreenSize.x || Screen.height != _coverScreenSize.y) RefreshCovers();

        // Two fingers: pinch to zoom, move both to pan.
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            int count = 0;
            Vector2 a = default, b = default;
            foreach (var touch in touchscreen.touches)
            {
                if (!touch.press.isPressed) continue;
                if (count == 0) a = touch.position.ReadValue();
                else if (count == 1) b = touch.position.ReadValue();
                count++;
            }

            if (count >= 2)
            {
                Vector2 mid = (a + b) * 0.5f;
                float distance = Mathf.Max(1f, Vector2.Distance(a, b));
                if (!_pinching)
                {
                    StopEase();
                    _pinching = true;
                    _pinchStartDistance = distance;
                    _pinchStartZoom = CurrentZoom;
                    _pinchPreviousMid = mid;
                }
                else
                {
                    ZoomAround(_pinchPreviousMid, mid, _pinchStartZoom * distance / _pinchStartDistance);
                    _pinchPreviousMid = mid;
                }
                return true;
            }

            if (_pinching)
            {
                _pinching = false;
                return true; // don't treat the end of a pinch as a tap
            }
        }

        // Mouse: scroll to zoom towards the pointer, right or middle button to pan.
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 position = mouse.position.ReadValue();
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !UiPointer.IsOverUi(position))
            {
                StopEase();
                float factor = scroll > 0f ? wheelStep : 1f / wheelStep; // browsers report very different amounts per notch
                ZoomAround(position, position, CurrentZoom * factor);
            }

            bool panButtonDown = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            bool panButtonPressed = mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame;
            if (panButtonPressed && !UiPointer.IsOverUi(position))
            {
                StopEase();
                _buttonPanning = true;
                _buttonPrevious = position;
            }
            if (_buttonPanning)
            {
                if (!panButtonDown) _buttonPanning = false;
                else
                {
                    PanBetween(_buttonPrevious, position);
                    _buttonPrevious = position;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Start panning with a one-finger or left-button drag on empty ground.</summary>
    public void BeginDragPan(Vector2 screen)
    {
        StopEase();
        _dragPrevious = screen;
    }

    public void DragPan(Vector2 screen)
    {
        PanBetween(_dragPrevious, screen);
        _dragPrevious = screen;
    }

    private float CurrentZoom => _fit ? _fit.Zoom : 1f;

    /// <summary>Zooms so the world point under <paramref name="before"/> ends up under <paramref name="after"/> (zoom and pan in one).</summary>
    private void ZoomAround(Vector2 before, Vector2 after, float zoom)
    {
        if (!_fit) return;
        Vector3 worldBefore = _camera.ScreenToWorldPoint(before);
        _fit.Zoom = Mathf.Clamp(zoom, 1f, maxZoom);
        _fit.ApplyNow();
        Vector3 worldAfter = _camera.ScreenToWorldPoint(after);
        MoveTo(_camera.transform.position + (worldBefore - worldAfter));
    }

    private void PanBetween(Vector2 fromScreen, Vector2 toScreen)
    {
        Vector3 delta = _camera.ScreenToWorldPoint(fromScreen) - _camera.ScreenToWorldPoint(toScreen);
        MoveTo(_camera.transform.position + delta);
    }

    private void MoveTo(Vector3 position)
    {
        Vector3 clamped = Clamped(position, _camera.orthographicSize);
        clamped.z = _camera.transform.position.z;
        _camera.transform.position = clamped;
    }

    /// <summary>
    /// Keeps the view inside the backdrop. The tray and the top bar cover parts of the screen, so the
    /// view may extend past the backdrop by that much (that part is hidden behind them anyway).
    /// </summary>
    private Vector3 Clamped(Vector3 position, float orthographicSize)
    {
        Rect bounds = _area ? _area.ViewBounds : new Rect(-12f, -7.2f, 24f, 14.4f);
        float halfHeight = orthographicSize;
        float halfWidth = orthographicSize * _camera.aspect;
        float bottom = _bottomCover * orthographicSize * 2f;
        float top = _topCover * orthographicSize * 2f;

        float xMin = bounds.xMin + halfWidth, xMax = bounds.xMax - halfWidth;
        float yMin = bounds.yMin - bottom + halfHeight, yMax = bounds.yMax + top - halfHeight;
        position.x = xMin > xMax ? (bounds.xMin + bounds.xMax) * 0.5f : Mathf.Clamp(position.x, xMin, xMax);
        position.y = yMin > yMax ? (yMin + yMax) * 0.5f : Mathf.Clamp(position.y, yMin, yMax);
        return position;
    }

    private void RefreshCovers()
    {
        _coverScreenSize = new Vector2Int(Screen.width, Screen.height);
        _bottomCover = _ui ? _ui.BottomCoverFraction : 0.3f;
        _topCover = _ui ? _ui.TopCoverFraction : 0.1f;
    }

    private void EaseTo(Vector3 position, float zoom, System.Action done)
    {
        StopEase();
        _ease = StartCoroutine(Ease(position, zoom, done));
    }

    private void StopEase()
    {
        if (_ease != null) StopCoroutine(_ease);
        _ease = null;
    }

    private IEnumerator Ease(Vector3 position, float zoom, System.Action done)
    {
        Vector3 fromPosition = _camera.transform.position;
        float fromZoom = CurrentZoom;
        for (float t = 0f; t < easeTime && _camera; t += Time.unscaledDeltaTime) // build mode pauses time
        {
            float k = Mathf.SmoothStep(0f, 1f, t / easeTime);
            if (_fit) _fit.Zoom = Mathf.Lerp(fromZoom, zoom, k);
            _camera.transform.position = Vector3.Lerp(fromPosition, position, k);
            yield return null;
        }
        if (_camera) _camera.transform.position = position;
        if (_fit) _fit.Zoom = zoom;
        _ease = null;
        done?.Invoke();
    }
}
