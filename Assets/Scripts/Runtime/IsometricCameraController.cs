using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Orthographic isometric pan/zoom/orbit. Presentation only — never commands specialists.
    /// Optional diorama mode (<see cref="DemoSettings.DioramaCamera"/>) renders the same rig through
    /// a perspective lens: the orthographic pose stays the source of truth for pan, bounds, focus
    /// and orbit, and the perspective camera is derived from it each frame (<see cref="DioramaRig"/>).
    /// Suggested camera rotation: (30, 45, 0).
    /// WASD pans. Q zooms out, E zooms in. The wheel zooms toward the ground point under the cursor.
    /// MMB drag orbits yaw (and a little pitch).
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

        // Diorama (perspective) mode. _smoothedPos/_targetPos stay the orthographic pose.
        private bool _diorama;
        private bool _hasApplied;
        private Vector3 _lastAppliedPos;
        private Quaternion _lastAppliedRot;
        private float _focusDistance = 30f;

        /// <summary>True while the perspective diorama camera is live.</summary>
        public static bool DioramaActive { get; private set; }

        /// <summary>Camera-to-ground distance at screen centre (depth-of-field focus).</summary>
        public float FocusDistance => _focusDistance;

        /// <summary>0 = closest zoom, 1 = farthest.</summary>
        public float Zoom01 => Mathf.InverseLerp(minZoom, maxZoom, _targetZoom);

        private Quaternion BaseRotation => Quaternion.Euler(_pitch, _yaw, 0f);
        private Vector3 PosePosition => _diorama ? _smoothedPos : transform.position;
        private Vector3 PoseForward => _diorama ? BaseRotation * Vector3.forward : transform.forward;

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
            AdoptExternalTransform();

            Vector3 forward = PoseForward;
            var plane = new Plane(Vector3.up, Vector3.zero);
            var ray = new Ray(PosePosition, forward);
            if (plane.Raycast(ray, out float t) && t > 0f)
            {
                Vector3 currentFocus = ray.GetPoint(t);
                Vector3 delta = groundPoint - currentFocus;
                _targetPos = PosePosition + delta;
            }
            else
            {
                float y = Mathf.Max(12f, PosePosition.y);
                _targetPos = groundPoint + new Vector3(-22f, y, -22f);
            }

            if (orthoSize.HasValue)
                _targetZoom = Mathf.Clamp(orthoSize.Value, minZoom, maxZoom);
        }

        /// <summary>Snap look-at focus on the ground plane; keeps current iso pitch/yaw.</summary>
        public void FocusOn(Vector3 groundPoint, float? orthoSize = null)
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            AdoptExternalTransform();

            Vector3 forward = PoseForward;
            var plane = new Plane(Vector3.up, Vector3.zero);
            var ray = new Ray(PosePosition, forward);
            if (plane.Raycast(ray, out float t) && t > 0f)
            {
                Vector3 currentFocus = ray.GetPoint(t);
                Vector3 delta = groundPoint - currentFocus;
                _targetPos = PosePosition + delta;
            }
            else
            {
                float y = Mathf.Max(12f, PosePosition.y);
                _targetPos = groundPoint + new Vector3(-22f, y, -22f);
            }

            _smoothedPos = _targetPos;
            if (!_diorama) transform.position = _targetPos;

            if (orthoSize.HasValue)
            {
                _targetZoom = Mathf.Clamp(orthoSize.Value, minZoom, maxZoom);
                if (_cam != null)
                    _cam.orthographicSize = _targetZoom;
            }
            FinishPose();
        }

        /// <summary>Hard-set transform to the current pan/zoom targets (skip smoothing).</summary>
        public void SnapToTarget()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            AdoptExternalTransform();
            _smoothedPos = _targetPos;
            if (!_diorama) transform.position = _targetPos;
            if (_cam != null)
                _cam.orthographicSize = _targetZoom;
            FinishPose();
        }

        /// <summary>Ortho: keep the view above ground. Diorama: derive the perspective pose. Then fog.</summary>
        private void FinishPose()
        {
            if (_diorama) ApplyDioramaPose();
            else
            {
                KeepViewAboveGround();
                _focusDistance = GroundDistance(transform.position, transform.forward);
            }
            DemoAtmosphere.SyncFog(_cam);
        }

        private void Update()
        {
            var loop = _loop != null ? _loop : FindAnyObjectByType<GameLoop>();
            _loop = loop;
            if (loop != null && !loop.AllowsCamera) return;

            AdoptExternalTransform();
            if (DemoSettings.DioramaCamera != _diorama) SetDiorama(DemoSettings.DioramaCamera, loop);
            if (_diorama)
            {
                // Mission setup forces the classic ortho lens; keep the diorama lens while it is on.
                if (_cam.orthographic)
                {
                    _cam.orthographic = false;
                    _cam.fieldOfView = DioramaRig.FieldOfView;
                    _cam.nearClipPlane = 0.5f;
                }
                if (loop != null) SkyPanorama.EnsureShowing(_cam, loop.BodyProfile);
            }

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
            var ray = new Ray(PosePosition, PoseForward);
            if (plane.Raycast(ray, out float t) && t > 0f)
                return ray.GetPoint(t);
            return new Vector3(PosePosition.x, 0f, PosePosition.z);
        }

        private void HandleMouseOrbit()
        {
            if (Input.GetMouseButtonDown(2))
            {
                if (!_diorama) ReadAnglesFromTransform(); // diorama pitch is derived, not the orbit pitch
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
            float dist = Vector3.Distance(PosePosition, _orbitFocus);
            if (dist < 2f) dist = 28f;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pos = _orbitFocus - rot * Vector3.forward * dist;
            _targetPos = pos;
            _smoothedPos = pos;
            if (_diorama)
            {
                ApplyDioramaPose();
            }
            else
            {
                transform.rotation = rot;
                transform.position = pos;
            }
            DemoAtmosphere.SyncFog(_cam);
        }

        private void HandleKeyboardPan()
        {
            if (InputBindings.TextEntryActive) return; // typing flag orders
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
            if (!InputBindings.TextEntryActive)
            {
                if (Input.GetKey(KeyCode.Q)) dir += 1f;
                if (Input.GetKey(KeyCode.E)) dir -= 1f;
            }
            if (Mathf.Abs(dir) > 0.01f)
            {
                _targetZoom = Mathf.Clamp(
                    _targetZoom + dir * zoomSpeed * Time.unscaledDeltaTime, minZoom, maxZoom);
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            // The wheel over a HUD panel scrolls that panel's list, not the map.
            if (Mathf.Abs(scroll) > 0.0001f && !(_loop != null && _loop.PointerOverHud))
            {
                float before = _targetZoom;
                _targetZoom = Mathf.Clamp(
                    _targetZoom - scroll * wheelZoomScale, minZoom, maxZoom);
                ZoomTowardCursor(before, _targetZoom);
            }
        }

        /// <summary>
        /// Keep the ground point under the cursor under the cursor while zoom goes from
        /// <paramref name="before"/> to <paramref name="after"/>. In an orthographic view a ground
        /// point's offset from the view centre scales with the zoom size, so the pan that holds it
        /// still is that offset × (before − after) / before. Diorama mode uses the same rule on its
        /// perspective ray; it is close enough that the spot under the mouse stays put.
        /// </summary>
        private void ZoomTowardCursor(float before, float after) =>
            ZoomTowardPoint(Input.mousePosition, before, after);

        /// <summary>Zoom to <paramref name="orthoSize"/> keeping the ground under <paramref name="screenPoint"/> fixed.</summary>
        public void ZoomAtScreenPoint(Vector2 screenPoint, float orthoSize)
        {
            float before = _targetZoom;
            _targetZoom = Mathf.Clamp(orthoSize, minZoom, maxZoom);
            ZoomTowardPoint(screenPoint, before, _targetZoom);
        }

        private void ZoomTowardPoint(Vector3 mouse, float before, float after)
        {
            if (_cam == null || before <= 0.001f || Mathf.Approximately(before, after)) return;
            if (mouse.x < 0f || mouse.y < 0f || mouse.x > Screen.width || mouse.y > Screen.height) return;

            var ground = new Plane(Vector3.up, Vector3.zero);
            Ray cursorRay = _cam.ScreenPointToRay(mouse);
            if (!ground.Raycast(cursorRay, out float tc) || tc <= 0f) return;
            Vector3 underCursor = cursorRay.GetPoint(tc);

            // Measure from the live view centre, scaled into the target pose's zoom, so a second
            // wheel tick during the smoothing still lands on the right spot.
            Ray centreRay = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!ground.Raycast(centreRay, out float tm) || tm <= 0f) return;
            Vector3 centre = centreRay.GetPoint(tm);

            float live = _diorama ? before : Mathf.Max(0.001f, _cam.orthographicSize);
            Vector3 offset = underCursor - centre;
            offset.y = 0f;
            _targetPos += offset * ((before - after) / live);
        }

        private void Apply()
        {
            _targetPos.x = Mathf.Clamp(_targetPos.x, panBoundsMin.x, panBoundsMax.x);
            _targetPos.z = Mathf.Clamp(_targetPos.z, panBoundsMin.y, panBoundsMax.y);
            _targetPos.y = PosePosition.y;

            float tPan = 1f - Mathf.Exp(-panSmooth * Time.unscaledDeltaTime);
            float tZoom = 1f - Mathf.Exp(-zoomSmooth * Time.unscaledDeltaTime);
            if (!_orbiting)
            {
                _smoothedPos = Vector3.Lerp(_smoothedPos, _targetPos, tPan);
                if (!_diorama) transform.position = _smoothedPos + _shakeOffset;
            }
            // Diorama keeps writing orthographicSize: it is the zoom value other systems read.
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetZoom, tZoom);
            FinishPose();
        }

        private void SetDiorama(bool on, GameLoop loop)
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            var body = loop != null ? loop.BodyProfile : null;
            _diorama = on;
            DioramaActive = on;
            if (on)
            {
                _smoothedPos = transform.position - _shakeOffset;
                _targetPos = _smoothedPos;
                _cam.orthographic = false;
                _cam.fieldOfView = DioramaRig.FieldOfView;
                _cam.nearClipPlane = 0.5f;
                SkyPanorama.Install(_cam, body);
                ApplyDioramaPose();
            }
            else
            {
                transform.SetPositionAndRotation(_smoothedPos, BaseRotation);
                _cam.orthographic = true;
                _cam.nearClipPlane = 0.3f;
                SkyPanorama.Uninstall(_cam, body);
                _hasApplied = false;
                KeepViewAboveGround();
            }
            DemoAtmosphere.SyncFog(_cam);
        }

        /// <summary>
        /// Perspective pose from the orthographic one: same ground focus and yaw, distance from zoom,
        /// pitch lifting toward the horizon as the player zooms out.
        /// </summary>
        private void ApplyDioramaPose()
        {
            if (_cam == null) return;
            Vector3 fwd = BaseRotation * Vector3.forward;
            Vector3 focus;
            var plane = new Plane(Vector3.up, Vector3.zero);
            var ray = new Ray(_smoothedPos, fwd);
            focus = plane.Raycast(ray, out float t) && t > 0f
                ? ray.GetPoint(t)
                : new Vector3(_smoothedPos.x, 0f, _smoothedPos.z);

            float zoom = _cam.orthographicSize;
            float zoom01 = Mathf.InverseLerp(minZoom, maxZoom, zoom);
            Quaternion rot = Quaternion.Euler(DioramaRig.Pitch(_pitch, zoom01), _yaw, 0f);
            float dist = DioramaRig.Distance(zoom);
            Vector3 pos = focus - rot * Vector3.forward * dist + _shakeOffset;
            float ground = TerrainDataBake.GroundHeight(pos.x, pos.z);
            pos.y = Mathf.Max(pos.y, ground + DioramaRig.MinClearance);

            transform.SetPositionAndRotation(pos, rot);
            _lastAppliedPos = pos;
            _lastAppliedRot = rot;
            _hasApplied = true;
            _focusDistance = Vector3.Distance(pos, focus);
        }

        /// <summary>
        /// Other systems (GameLoop setup, cinematics) position this camera as if it were the
        /// orthographic rig. In diorama mode, treat such a write as a new orthographic pose.
        /// </summary>
        private void AdoptExternalTransform()
        {
            if (!_diorama || !_hasApplied) return;
            if (transform.position == _lastAppliedPos && transform.rotation == _lastAppliedRot) return;
            bool rotated = transform.rotation != _lastAppliedRot;
            _smoothedPos = transform.position;
            _targetPos = transform.position;
            if (rotated) ReadAnglesFromTransform(); // a position-only write keeps the orbit angles
            _hasApplied = false;
        }

        private static float GroundDistance(Vector3 origin, Vector3 forward)
        {
            var plane = new Plane(Vector3.up, Vector3.zero);
            var ray = new Ray(origin, forward);
            return plane.Raycast(ray, out float t) && t > 0f ? t : 30f;
        }

        /// <summary>
        /// Ortho rays are parallel to forward. The bottom of the screen samples
        /// position + up * (-orthoSize). When that origin is under y=0 and forward.y &lt; 0,
        /// the ray never hits the ground and the skybox mustard band appears.
        /// Lift so the lowest sample stays above the playable ground plane.
        /// </summary>
        private void KeepViewAboveGround()
        {
            if (_cam == null || _diorama) return;
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
