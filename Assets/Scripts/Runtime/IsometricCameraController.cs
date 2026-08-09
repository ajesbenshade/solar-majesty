using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Orthographic isometric pan/zoom. Presentation only — never commands specialists.
    /// Suggested camera rotation: (30, 45, 0).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class IsometricCameraController : MonoBehaviour
    {
        [Header("Pan")]
        [SerializeField] private float panSpeed = 22f;
        [SerializeField] private float panSmooth = 12f;
        [SerializeField] private bool edgePan = true;
        [SerializeField] private float edgeBorder = 14f;
        [SerializeField] private Vector2 panBoundsMin = new Vector2(-5f, -5f);
        [SerializeField] private Vector2 panBoundsMax = new Vector2(70f, 70f);

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 6f;
        [SerializeField] private float minZoom = 6f;
        [SerializeField] private float maxZoom = 30f;
        [SerializeField] private float zoomSmooth = 10f;

        [Header("Drag")]
        [SerializeField] private bool middleMouseDrag = true;
        [SerializeField] private bool rightMouseDrag = true;

        private Camera _cam;
        private Vector3 _targetPos;
        private float _targetZoom;
        private bool _dragging;
        private Vector3 _dragOrigin;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _targetPos = transform.position;
            _targetZoom = _cam.orthographicSize;
        }

        /// <summary>Clamp pan to sandbox / showcase extents (XZ → Vector2 x/y).</summary>
        public void SetPanBounds(Vector2 min, Vector2 max)
        {
            panBoundsMin = min;
            panBoundsMax = max;
        }

        /// <summary>Snap look-at focus on the ground plane; keeps current iso pitch/yaw.</summary>
        public void FocusOn(Vector3 groundPoint, float? orthoSize = null)
        {
            if (_cam == null) _cam = GetComponent<Camera>();

            // Preserve camera offset from ground focus (iso look direction).
            Vector3 forward = transform.forward;
            var plane = new Plane(Vector3.up, Vector3.zero);
            var ray = new Ray(transform.position, forward);
            if (plane.Raycast(ray, out float t) && t > 0f)
            {
                Vector3 currentFocus = ray.GetPoint(t);
                Vector3 delta = groundPoint - currentFocus;
                _targetPos = transform.position + delta;
            }
            else
            {
                float y = Mathf.Max(12f, transform.position.y);
                _targetPos = groundPoint + new Vector3(-22f, y, -22f);
            }

            transform.position = _targetPos;

            if (orthoSize.HasValue)
            {
                _targetZoom = Mathf.Clamp(orthoSize.Value, minZoom, maxZoom);
                if (_cam != null)
                    _cam.orthographicSize = _targetZoom;
            }
        }

        /// <summary>Hard-set transform to the current pan/zoom targets (skip smoothing).</summary>
        public void SnapToTarget()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            transform.position = _targetPos;
            if (_cam != null)
                _cam.orthographicSize = _targetZoom;
        }

        private void Update()
        {
            HandleKeyboardPan();
            HandleEdgePan();
            HandleDragPan();
            HandleZoom();
            Apply();
        }

        private void HandleKeyboardPan()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f) return;

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            float scale = panSpeed * Time.unscaledDeltaTime * (_targetZoom / 12f);
            _targetPos += (right * h + forward * v) * scale;
        }

        private void HandleEdgePan()
        {
            if (!edgePan || !Application.isFocused) return;

            Vector3 m = Input.mousePosition;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            float scale = panSpeed * Time.unscaledDeltaTime * (_targetZoom / 12f);

            if (m.x <= edgeBorder) _targetPos -= right * scale;
            if (m.x >= Screen.width - edgeBorder) _targetPos += right * scale;
            if (m.y <= edgeBorder) _targetPos -= forward * scale;
            if (m.y >= Screen.height - edgeBorder) _targetPos += forward * scale;
        }

        private void HandleDragPan()
        {
            bool want = (middleMouseDrag && Input.GetMouseButton(2)) ||
                        (rightMouseDrag && Input.GetMouseButton(1));

            if (want)
            {
                if (!_dragging)
                {
                    _dragging = true;
                    _dragOrigin = GroundPoint(Input.mousePosition);
                }
                else
                {
                    Vector3 cur = GroundPoint(Input.mousePosition);
                    _targetPos += _dragOrigin - cur;
                }
            }
            else
            {
                _dragging = false;
            }
        }

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                _targetZoom = Mathf.Clamp(_targetZoom - scroll * zoomSpeed * 0.35f, minZoom, maxZoom);
        }

        private void Apply()
        {
            _targetPos.x = Mathf.Clamp(_targetPos.x, panBoundsMin.x, panBoundsMax.x);
            _targetPos.z = Mathf.Clamp(_targetPos.z, panBoundsMin.y, panBoundsMax.y);
            _targetPos.y = transform.position.y;

            float tPan = 1f - Mathf.Exp(-panSmooth * Time.unscaledDeltaTime);
            float tZoom = 1f - Mathf.Exp(-zoomSmooth * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, _targetPos, tPan);
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetZoom, tZoom);
        }

        public bool TryGetMouseGroundPoint(out Vector3 world)
        {
            world = GroundPoint(Input.mousePosition);
            return true;
        }

        private Vector3 GroundPoint(Vector3 screen)
        {
            Ray ray = _cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
                return ray.GetPoint(enter);
            return _targetPos;
        }
    }
}
