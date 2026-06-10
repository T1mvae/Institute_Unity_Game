using UnityEngine;

namespace Institute.World
{
    /// <summary>
    /// Simple orthographic pan/zoom for the world map. Drag with right/middle mouse or use
    /// WASD/arrows; scroll to zoom. Clamped to the generated map bounds.
    /// </summary>
    public class WorldCameraController : MonoBehaviour
    {
        Camera _camera;
        Bounds _bounds;
        float _minSize = 3f;
        float _maxSize = 40f;
        float _panSpeed = 12f;

        Vector3 _dragOrigin;
        bool _dragging;

        public void Initialize(Camera camera, Bounds bounds)
        {
            _camera = camera;
            _bounds = bounds;
            _maxSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.1f;
            _minSize = Mathf.Max(2f, _maxSize * 0.12f);
            _camera.orthographic = true;
            _camera.orthographicSize = Mathf.Clamp(_maxSize * 0.6f, _minSize, _maxSize);
            FocusCenter();
        }

        public void FocusCenter()
        {
            if (_camera == null) return;
            Vector3 c = _bounds.center;
            _camera.transform.position = new Vector3(c.x, c.y, -10f);
        }

        void Update()
        {
            if (_camera == null) return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
                _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize - scroll * _camera.orthographicSize, _minSize, _maxSize);

            Vector3 move = Vector3.zero;
            move.x += Input.GetAxisRaw("Horizontal");
            move.y += Input.GetAxisRaw("Vertical");
            if (move != Vector3.zero)
                _camera.transform.position += move.normalized * (_panSpeed * _camera.orthographicSize * 0.02f * Time.unscaledDeltaTime * 60f * Time.unscaledDeltaTime);

            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                _dragging = true;
                _dragOrigin = _camera.ScreenToWorldPoint(Input.mousePosition);
            }
            if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2)) _dragging = false;

            if (_dragging)
            {
                Vector3 current = _camera.ScreenToWorldPoint(Input.mousePosition);
                Vector3 delta = _dragOrigin - current;
                delta.z = 0f;
                _camera.transform.position += delta;
            }

            ClampToBounds();
        }

        void ClampToBounds()
        {
            Vector3 p = _camera.transform.position;
            float pad = _camera.orthographicSize;
            p.x = Mathf.Clamp(p.x, _bounds.min.x - pad, _bounds.max.x + pad);
            p.y = Mathf.Clamp(p.y, _bounds.min.y - pad, _bounds.max.y + pad);
            p.z = -10f;
            _camera.transform.position = p;
        }
    }
}
