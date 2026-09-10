using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Orthographic isometric pan/zoom/orbit. Presentation only — never commands specialists.
    /// Suggested camera rotation: (30, 45, 0).
    /// WASD pans. Q zooms out, E zooms in. Wheel zooms. MMB drag orbits yaw (and a little pitch).
    /// LMB is world click. RMB is flag-cancel. Mouse does not pan.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Camera))]
    public class IsometricCameraController : MonoBehaviour
    {
        /// <summary>Shared with horizon floor sizing — keep in sync with serialized maxZoom default.</summary>
        public const float MaxOrthoSize = 52f;

        [Header("Pan")]
        [SerializeField] private float panSpeed = 36f;
        [SerializeField] private float panSmooth = 12f;
        [SerializeField] private Vector2 panBoundsMin = new Vector2(-5f, -5f);
        [SerializeField] private Vector2 panBoundsMax = new Vector2(70f, 70f);

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 14f;
        [SerializeField] private float minZoom = 4.5f;
        [SerializeField] private float maxZoom = MaxOrthoSize;
        [SerializeField] private float zoomSmooth = 10f;
        [SerializeField] private float wheelZoomScale = 18f;

        [Header("Orbit (MMB)")]
        [SerializeField] private float yawSensitivity = 0.22f;
        [SerializeField] private float pitchSensitivity = 0.12f;
        [SerializeField] private float minPitch = 22f;
        [SerializeField] private float maxPitch = 42f;

        private const float EdgeMargin = 6f;
        private const float MaxShake = 1.4f;
        private const float ShakeDecay = 2.2f;

        private Camera _cam;
        private Vector3 _targetPos;
        private Vector3 _smoothedPos;
        private Vector3 _shakeOffset;
        private float _shake;
        private float _targetZoom;
        private GameLoop _loop;
        private bool _orbiting;
        private Vector3 _lastMouse;
        private Vector3 _orbitFocus;
        private float _yaw = 45f;
        private float _pitch = 30f;

        /// <summary>True while middle-mouse orbit is held. LMB/RMB stay free.</summary>
        public bool IsDragging => _orbiting;

        /// <summary>LMB is always a world click — MMB orbit does not swallow it.</summary>
        public bool SuppressWorldClick => false;

        /// <summary>RMB is always flag-cancel — MMB orbit does not swallow it.</summary>
        public bool SuppressFlagCancel => false;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _targetPos = transform.position;
            _smoothedPos = transform.position;
            _targetZoom = _cam.orthographicSize;
            _loop = FindAnyObjectByType<GameLoop>();
            if (minZoom > 4.5f)
                minZoom = 4.5f;
            ReadAnglesFromTransform();
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
            _smoothedPos = _targetPos;

            if (orthoSize.HasValue)
            {
                _targetZoom = Mathf.Clamp(orthoSize.Value, minZoom, maxZoom);
                if (_cam != null)
                    _cam.orthographicSize = _targetZoom;
            }
            KeepViewAboveGround();
            DemoAtmosphere.SyncFog(_cam);
        }

        /// <summary>Hard-set transform to the current pan/zoom targets (skip smoothing).</summary>
        public void SnapToTarget()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            _smoothedPos = _targetPos;
            transform.position = _targetPos;
            if (_cam != null)
                _cam.orthographicSize = _targetZoom;
            KeepViewAboveGround();
            DemoAtmosphere.SyncFog(_cam);
        }

        private void Update()
        {
            var loop = _loop != null ? _loop : FindAnyObjectByType<GameLoop>();
            _loop = loop;
            if (loop != null && !loop.AllowsCamera) return;

            HandleMouseOrbit();
            HandleKeyboardPan();
            HandleEdgeScroll();
            HandleZoom();
            TickShake();
            Apply();
        }

        /// <summary>
        /// Edge scroll, off by default because it fights flag placement near the screen border.
        /// Enabled from Settings.
        /// </summary>
        private void HandleEdgeScroll()
        {
            if (!DemoSettings.EdgeScroll) return;

            Vector3 mouse = Input.mousePosition;
            if (mouse.x < 0f || mouse.y < 0f || mouse.x > Screen.width || mouse.y > Screen.height)
                return;

            float h = 0f;
            float v = 0f;
            if (mouse.x <= EdgeMargin) h -= 1f;
            else if (mouse.x >= Screen.width - EdgeMargin) h += 1f;
            if (mouse.y <= EdgeMargin) v -= 1f;
            else if (mouse.y >= Screen.height - EdgeMargin) v += 1f;

            if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f) return;
            PanBy(h, v);
        }

        /// <summary>
        /// Decaying positional shake. Applied as a render offset after smoothing so it never
        /// pollutes the pan target — a shake must not permanently move the camera.
        /// </summary>
        public void AddShake(float strength)
        {
            if (DemoSettings.ReduceMotion) return;
            _shake = Mathf.Min(_shake + Mathf.Abs(strength), MaxShake);
        }

        private void TickShake()
        {
            if (_shake <= 0f)
            {
                _shakeOffset = Vector3.zero;
                return;
            }

            _shake = Mathf.Max(0f, _shake - Time.unscaledDeltaTime * ShakeDecay);

            // Perlin rather than random keeps it a smooth rumble instead of a per-frame jitter.
            float t = Time.unscaledTime * 26f;
            float x = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f;
            float z = (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f;

            // Falls off with zoom so a shake at max zoom-out is not a screen-wide lurch.
            float scale = _shake * _shake * Mathf.Clamp(_targetZoom / 12f, 0.35f, 2.5f) * 0.22f;
            _shakeOffset = new Vector3(x, 0f, z) * scale;
        }

        private void ReadAnglesFromTransform()
        {
            Vector3 e = transform.eulerAngles;
            _pitch = e.x > 180f ? e.x - 360f : e.x;
            _yaw = e.y;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        private Vector3 GroundLookAt()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            var plane = new Plane(Vector3.up, Vector3.zero);
            var ray = new Ray(transform.position, transform.forward);
            if (plane.Raycast(ray, out float t) && t > 0f)
                return ray.GetPoint(t);
            return new Vector3(transform.position.x, 0f, transform.position.z);
        }

        private void HandleMouseOrbit()
        {
            if (Input.GetMouseButtonDown(2))
            {
                ReadAnglesFromTransform();
                _orbiting = true;
                _lastMouse = Input.mousePosition;
                _orbitFocus = GroundLookAt();
            }

            if (Input.GetMouseButton(2) && _orbiting)
            {
                Vector3 mouse = Input.mousePosition;
                Vector3 delta = mouse - _lastMouse;
                _lastMouse = mouse;
                if (delta.sqrMagnitude > 0.01f)
                {
                    _yaw += delta.x * yawSensitivity;
                    _pitch = Mathf.Clamp(
                        _pitch - delta.y * pitchSensitivity, minPitch, maxPitch);
                    ApplyOrbit();
                }
            }
            else
            {
                _orbiting = false;
            }
        }

        private void ApplyOrbit()
        {
            float dist = Vector3.Distance(transform.position, _orbitFocus);
            if (dist < 2f) dist = 28f;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.rotation = rot;
            transform.position = _orbitFocus - rot * Vector3.forward * dist;
            _targetPos = transform.position;
            _smoothedPos = transform.position;
            DemoAtmosphere.SyncFog(_cam);
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
            PanBy(h, v);
        }

        private void PanBy(float h, float v)
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            float scale = panSpeed * Time.unscaledDeltaTime * (_targetZoom / 12f);
            if (DemoSettings.InvertPan)
            {
                h = -h;
                v = -v;
            }
            Vector3 step = (right * h + forward * v) * scale;
            _targetPos += step;
            if (_orbiting)
            {
                _orbitFocus += step;
                ApplyOrbit();
            }
        }

        private void HandleZoom()
        {
            float dir = 0f;
            if (Input.GetKey(KeyCode.Q)) dir += 1f;
            if (Input.GetKey(KeyCode.E)) dir -= 1f;
            if (Mathf.Abs(dir) > 0.01f)
            {
                _targetZoom = Mathf.Clamp(
                    _targetZoom + dir * zoomSpeed * Time.unscaledDeltaTime, minZoom, maxZoom);
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                _targetZoom = Mathf.Clamp(
                    _targetZoom - scroll * wheelZoomScale, minZoom, maxZoom);
            }
        }

        private void Apply()
        {
            _targetPos.x = Mathf.Clamp(_targetPos.x, panBoundsMin.x, panBoundsMax.x);
            _targetPos.z = Mathf.Clamp(_targetPos.z, panBoundsMin.y, panBoundsMax.y);
            _targetPos.y = transform.position.y;

            float tPan = 1f - Mathf.Exp(-panSmooth * Time.unscaledDeltaTime);
            float tZoom = 1f - Mathf.Exp(-zoomSmooth * Time.unscaledDeltaTime);
            if (!_orbiting)
            {
                _smoothedPos = Vector3.Lerp(_smoothedPos, _targetPos, tPan);
                transform.position = _smoothedPos + _shakeOffset;
            }
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetZoom, tZoom);
            KeepViewAboveGround();
            DemoAtmosphere.SyncFog(_cam);
        }

        /// <summary>
        /// Ortho rays are parallel to forward. The bottom of the screen samples
        /// position + up * (-orthoSize). When that origin is under y=0 and forward.y &lt; 0,
        /// the ray never hits the ground and the skybox mustard band appears.
        /// Lift so the lowest sample stays above the playable ground plane.
        /// </summary>
        private void KeepViewAboveGround()
        {
            if (_cam == null) return;
            float upY = transform.up.y;
            if (upY < 0.05f) return;
            // Keep bottom-row ray origins at y >= 1.5 so they still hit GroundPlane / near skirt.
            float minCamY = _cam.orthographicSize * upY + 1.5f;
            Vector3 p = transform.position;
            if (p.y >= minCamY - 0.01f) return;
            p.y = minCamY;
            transform.position = p;
            _targetPos.y = minCamY;
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
