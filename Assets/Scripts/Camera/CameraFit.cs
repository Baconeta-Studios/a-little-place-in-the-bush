using UnityEngine;

[ExecuteAlways, RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
    [SerializeField] private Vector2 safeSize = new(19.2f, 10.8f);
    private Camera _camera;

    private void LateUpdate()
    {
        if (!_camera) _camera = GetComponent<Camera>();
        _camera.orthographicSize = Mathf.Max(safeSize.y * 0.5f, safeSize.x * 0.5f / _camera.aspect);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, safeSize);
    }
}