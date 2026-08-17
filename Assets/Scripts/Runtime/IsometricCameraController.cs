using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Orthographic isometric pan/zoom. Presentation only — never commands specialists.
    /// Suggested camera rotation: (30, 45, 0).
    /// WASD pans. Q zooms out, E zooms in. Mouse does not pan or zoom.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Camera))]
    public class IsometricCameraController : MonoBehaviour
    {
        [Header("Pan")]
        [SerializeField] private float panSpeed = 36f;
        [SerializeField] private float panSmooth = 12f;
        [SerializeField] private Vector2 panBoundsMin = new Vector2(-5f, -5f);
        [SerializeField] private Vector2 panBoundsMax = new Vector2(70f, 70f);

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 14f;
        [SerializeField] private float minZoom = 4.5f;
        [SerializeField] private float maxZoom = 52f;
        [SerializeField] private float zoomSmooth = 10f;

        private Camera _cam;
        private Vector3 _targetPos;
        private float _targetZoom;
        private GameLoop _loop;

        /// <summary>Mouse never pans this camera.</summary>
        public bool IsDragging => false;

        /// <summary>LMB is always a world click — no drag-pan to swallow it.</summary>
        public bool SuppressWorldClick => false;

        /// <summary>RMB is always flag-cancel — no drag-pan to swallow it.</summary>
        public bool SuppressFlagCancel => false;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _targetPos = transform.position;
            _targetZoom = _cam.orthographicSize;
            _loop = FindAnyObjectByType<GameLoop>();
            if (minZoom > 4.5f)
                minZoom = 4.5f;
        }

        /// <summary>Clamp pan to sandbox / showcase extents (XZ → Vector2 x/y).</summary>
        public void SetPanBounds(Vector2 min, Vector2 max)
        {
            panBoundsMin = min;
            panBoundsMax = max;
        }

        /// <summary>Smooth pan toward a ground point (does not snap this frame).</summary>
        public void GlanceAt(Vector3 groundPoint, float? orthoSize = null)
        {
            if (_cam == null) _cam = GetComponent<Camera>();

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

            if (orthoSize.HasValue)
                _targetZoom = Mathf.Clamp(orthoSize.Value, minZoom, maxZoom);
        }

        /// <summary>Snap look-at focus on the ground plane; keeps current iso pitch/yaw.</summary>
        public void FocusOn(Vector3 groundPoint, float? orthoSize = null)
        {
            if (_cam == null) _cam = GetComponent<Camera>();

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
            var loop = _loop != null ? _loop : FindAnyObjectByType<GameLoop>();
            _loop = loop;
            if (loop != null && !loop.AllowsCamera) return;

            HandleKeyboardPan();
            HandleZoom();
            Apply();
        }

        private void HandleKeyboardPan()
        {
            float h = 0f;
            float v = 0f;
            if (Input.GetKey(KeyCode.D)) h += 1f;
            if (Input.GetKey(KeyCode.A)) h -= 1f;
            if (Input.GetKey(KeyCode.W)) v += 1f;
            if (Input.GetKey(KeyCode.S)) v -= 1f;
            if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f) return;

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            float scale = panSpeed * Time.unscaledDeltaTime * (_targetZoom / 12f);
            if (DemoSettings.InvertPan)
            {
                h = -h;
                v = -v;
            }
            _targetPos += (right * h + forward * v) * scale;
        }

        private void HandleZoom()
        {
            float dir = 0f;
            if (Input.GetKey(KeyCode.Q)) dir += 1f;
            if (Input.GetKey(KeyCode.E)) dir -= 1f;
            if (Mathf.Abs(dir) < 0.01f) return;
            _targetZoom = Mathf.Clamp(
                _targetZoom + dir * zoomSpeed * Time.unscaledDeltaTime, minZoom, maxZoom);
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
