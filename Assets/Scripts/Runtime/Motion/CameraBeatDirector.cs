using System;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Scripted camera beats: the Colony Lifter's launch and the victory orbit over a conquered
    /// colony. Timing and shot geometry live in <see cref="CameraBeatTuning"/>; the state machine is
    /// <see cref="CameraBeatSequence"/>. This component reads the scene, writes the camera, and
    /// hands control back.
    ///
    /// While a beat plays:
    /// - The player's camera controller is disabled and every camera setting is snapshotted, then
    ///   written back exactly when the beat ends (completed, skipped, or its subject destroyed).
    /// - The HUD and name tags are hidden; a letterbox, a caption and a skip hint replace them.
    /// - World input stands down (<see cref="BlocksInput"/>): Esc skips the beat instead of pausing,
    ///   and the skip click never reaches the map, even on the frame the beat ends.
    /// - Skip: Esc, Cancel (gamepad Menu), a mouse click, or the pad's A/B (which arrive as clicks).
    /// - The world runs at normal (1x) speed whatever speed or hold the player had, so robots move
    ///   the same at every game speed; the beat's own clock (camera, climb, exhaust) is unscaled time.
    ///   Both beats start after the outcome is latched and saved, so this cannot change a verdict.
    ///   The player's speed (or the victory hold) is re-applied on handoff.
    /// - Reduce motion: no camera travel or shake; a still wide shot, shorter.
    /// - Collision: the pad shot picks a heading with a clear line to the lifter; every frame the
    ///   camera stays above the terrain and is lifted over any building between it and its subject.
    /// - The beat lives in the scene: a scene change mid-beat destroys it, which aborts the beat
    ///   and restores what still exists. Nothing waits on it, so nothing can softlock.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class CameraBeatDirector : MonoBehaviour
    {
        /// <summary>Command-line switch that turns beats off (automation, capture rigs).</summary>
        public const string DisableArg = "-nobeats";

        /// <summary>Tests and harnesses can force beats on or off; null follows the defaults.</summary>
        public static bool? EnabledOverride;

        private static CameraBeatDirector _current;
        private static int _blockThroughFrame = -1;
        private static bool _blockUntilRelease;
        private static bool? _argDisabled;

        /// <summary>A beat is playing.</summary>
        public static bool Active => _current != null && _current._seq.Active;

        /// <summary>Gameplay HUD should not draw.</summary>
        public static bool HidesHud => Active;

        /// <summary>
        /// World input (hotkeys, clicks, Esc-to-pause) must stand down: a beat is playing, it ended
        /// this frame or the last, or the click that skipped it is still held.
        /// </summary>
        public static bool BlocksInput
        {
            get
            {
                if (Active) return true;
                if (Time.frameCount <= _blockThroughFrame) return true;
                if (_blockUntilRelease)
                {
                    if (Input.GetMouseButton(0) || Input.GetMouseButton(1)) return true;
                    _blockUntilRelease = false;
                }
                return false;
            }
        }

        /// <summary>Beats play unless overridden, in batch mode, or started with <see cref="DisableArg"/>.</summary>
        public static bool Enabled
        {
            get
            {
                if (EnabledOverride.HasValue) return EnabledOverride.Value;
                if (Application.isBatchMode) return false;
                if (!_argDisabled.HasValue)
                {
                    _argDisabled = false;
                    foreach (string a in Environment.GetCommandLineArgs())
                        if (string.Equals(a, DisableArg, StringComparison.OrdinalIgnoreCase))
                            _argDisabled = true;
                }
                return !_argDisabled.Value;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _current = null;
            _blockThroughFrame = -1;
            _blockUntilRelease = false;
            _argDisabled = null;
        }

        private readonly CameraBeatSequence _seq = new CameraBeatSequence();
        public CameraBeatSequence Sequence => _seq;

        private Camera _cam;
        private IsometricCameraController _ctrl;
        private bool _ctrlWasEnabled;
        private CameraSnapshot _snap;
        private BeatContext _ctx;
        private GameLoop _loop;

        private Transform _target;
        private ColonyStructure _targetHome;
        private bool _needsTarget;
        private bool _needsLoop;
        private Vector3 _lifterStart;
        private float _lifterBaseOffset;
        private bool _lifterMoved;
        private LaunchExhaust _exhaust;

        private Action _onIgnition;
        private Action _onDone;
        private bool _ignited;
        private string _caption;

        private float _clearLift;

        private bool _fogSaved;
        private bool _fog;
        private FogMode _fogMode;
        private float _fogStart, _fogEnd, _fogDensity;
        private bool _skyInstalled;
        private bool _skyFallback;
        private bool _tagsWereHidden;

        private float _blackUntil = -1f;
        private GUIStyle _captionStyle, _hintStyle;

        /// <summary>Heading the pad shot was filmed from (degrees off the player's view). For tests and logs.</summary>
        public float LaunchYaw => _ctx.LaunchYaw;

        // ---- entry points -------------------------------------------------------------------------

        /// <summary>
        /// Launch beat. <paramref name="onIgnition"/> fires when the engines light (departure audio
        /// and plume), <paramref name="onDone"/> once when the beat ends for any reason except a scene
        /// change. False when beats are off or nothing can be filmed: the caller departs at once.
        /// </summary>
        public static bool PlayLaunch(GameLoop loop, Transform lifter, Vector3 pad, string caption,
            Action onIgnition, Action onDone)
        {
            if (_current != null && _current._seq.Active && _current._seq.Kind == CameraBeatKind.Launch)
                return true; // already departing; the first call's handoff stands
            if (!Enabled || lifter == null) return false;
            var d = Ensure();
            if (d == null) return false;
            var ctrl = FindCtrl();
            return d.Begin(CameraBeatKind.Launch, CamFor(ctrl), ctrl, loop, lifter, pad, DemoSettings.ReduceMotion,
                caption, onIgnition, onDone);
        }

        /// <summary>Victory beat over the colony. The verdict draws once it ends (the HUD is hidden until then).</summary>
        public static bool PlayVictory(GameLoop loop, Vector3 colony, string caption, Action onDone = null)
        {
            if (Active || !Enabled || StillCaptureHold.Active) return false;
            var d = Ensure();
            if (d == null) return false;
            var ctrl = FindCtrl();
            return d.Begin(CameraBeatKind.Victory, CamFor(ctrl), ctrl, loop, null, colony, DemoSettings.ReduceMotion,
                caption, null, onDone);
        }

        /// <summary>End the running beat as a skip (the same path as the player's Esc).</summary>
        public static void SkipCurrent()
        {
            if (_current != null) _current.Step(0f, skip: true, ignoreGrace: true);
        }

        private static CameraBeatDirector Ensure()
        {
            if (_current != null) return _current;
            var existing = FindAnyObjectByType<CameraBeatDirector>();
            if (existing != null) return existing;
            return new GameObject("[CameraBeat]").AddComponent<CameraBeatDirector>();
        }

        private static IsometricCameraController FindCtrl()
        {
            var cam = Camera.main;
            var ctrl = cam != null ? cam.GetComponent<IsometricCameraController>() : null;
            return ctrl != null ? ctrl : FindAnyObjectByType<IsometricCameraController>();
        }

        private static Camera CamFor(IsometricCameraController ctrl)
        {
            var cam = ctrl != null ? ctrl.GetComponent<Camera>() : null;
            return cam != null ? cam : Camera.main;
        }

        private void Awake()
        {
            if (_current == null) _current = this;
        }

        private void OnDestroy()
        {
            if (_seq.Active)
            {
                _seq.Abort();
                Restore(applySpeed: false);
            }
            if (_current == this) _current = null;
        }

        // ---- lifecycle -----------------------------------------------------------------------------

        /// <summary>Start a beat on <paramref name="cam"/>. Public for tests; gameplay uses PlayLaunch / PlayVictory.</summary>
        public bool Begin(CameraBeatKind kind, Camera cam, IsometricCameraController ctrl, GameLoop loop,
            Transform target, Vector3 subject, bool reduceMotion, string caption, Action onIgnition, Action onDone)
        {
            if (cam == null) return false;
            if (_seq.Active)
            {
                _seq.Abort();
                Restore(applySpeed: false);
            }
            _current = this;
            _cam = cam;
            _ctrl = ctrl;
            _loop = loop;
            _target = target;
            _targetHome = target != null ? target.GetComponentInParent<ColonyStructure>() : null;
            _needsTarget = target != null;
            _needsLoop = loop != null;
            _caption = caption;
            _onIgnition = onIgnition;
            _onDone = onDone;
            _ignited = false;
            _clearLift = 0f;
            _blackUntil = -1f;
            _lifterBaseOffset = 0f;

            _snap = CameraSnapshot.Capture(cam);
            _ctrlWasEnabled = ctrl != null && ctrl.enabled;
            if (ctrl != null) ctrl.enabled = false;

            _ctx = new BeatContext
            {
                Start = CameraBeatShots.FromCamera(_snap.Position, _snap.Rotation, _snap.Orthographic,
                    _snap.OrthographicSize, _snap.FieldOfView, CameraBeatTuning.BeatFov),
                ViewFlat = CameraBeatShots.FlatForward(_snap.Rotation * Vector3.forward),
                Subject = subject,
                LifterHeight = 3f,
                LaunchYaw = CameraBeatTuning.LaunchYawDefault
            };

            if (kind == CameraBeatKind.Launch && target != null)
            {
                MeasureLifter(target);
                _lifterStart = target.position;
                _ctx.Subject = _lifterStart + Vector3.up * _lifterBaseOffset;
                _ctx.LifterBase = _ctx.Subject;
                _ctx.LaunchYaw = ChooseLaunchYaw(reduceMotion);
                _exhaust = new LaunchExhaust(target, _ctx.LifterHeight, _lifterBaseOffset,
                    loop != null ? TerrainDataBake.SplatFor(loop.ActiveBody, 0.4f, 0f) : new Color(0.6f, 0.55f, 0.5f));
            }

            SaveFog();
            _tagsWereHidden = SpecialistStatusDisplay.HideForCapture;
            SpecialistStatusDisplay.HideForCapture = true;
            if (loop != null && Application.isPlaying && !SkyPanorama.Installed)
            {
                _skyInstalled = SkyPanorama.Install(cam, loop.BodyProfile);
                if (!_skyInstalled)
                {
                    // The snapshot puts the clear mode and colour back on handoff. The day cycle tints
                    // the clear colour every frame from its own base, so hand it the sky as that base.
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = SkyPainter.Gradient(loop.BodyProfile, CameraBeatTuning.SkyFallbackElevation);
                    SunCycle.RebaseClearColor(cam);
                    _skyFallback = true;
                }
            }

            _seq.Begin(kind, reduceMotion);
            Debug.Log($"[CameraBeat] {kind} beat ({(reduceMotion ? "reduced" : "full")}, {_seq.Profile.Total:F1}s" +
                      (kind == CameraBeatKind.Launch ? $", lifter {_ctx.LifterHeight:F1} m, shot heading {_ctx.LaunchYaw:F0}°)." : ")."));
            ApplyCamera(0f);
            return true;
        }

        private void Update()
        {
            if (!_seq.Active) return;
            // The beat's clock holds while a modal screen is up (it should not be, but never run under one).
            if (_loop != null && _loop.Screen != DemoScreen.Playing) return;
            bool skip = Input.GetKeyDown(KeyCode.Escape) || InputBindings.Down(GameAction.Cancel) ||
                        Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) ||
                        Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
            Step(Time.unscaledDeltaTime, skip);
        }

        private void LateUpdate()
        {
            if (!_seq.Active) return;
            if (Application.isPlaying && (_loop == null || _loop.Screen == DemoScreen.Playing))
                Time.timeScale = SimSpeed.BasePace; // normal game speed for the length of the beat
            ApplyCamera(Time.unscaledDeltaTime);
        }

        /// <summary>One beat tick. Public so EditMode tests can drive the state machine without a player loop.</summary>
        public void Step(float dt, bool skip, bool ignoreGrace = false)
        {
            if (!_seq.Active) return;
            bool alive = _cam != null && (!_needsTarget || _target != null) && (!_needsLoop || _loop != null);
            _seq.Tick(dt, alive);
            if (skip && _seq.Active)
            {
                if (ignoreGrace && _seq.Elapsed < CameraBeatTuning.SkipGrace)
                    _seq.Tick(CameraBeatTuning.SkipGrace - _seq.Elapsed, alive);
                if (_seq.Skip())
                    _blockUntilRelease = Input.GetMouseButton(0) || Input.GetMouseButton(1);
            }

            if (_seq.Active)
            {
                if (_seq.Kind == CameraBeatKind.Launch) TickLaunch(dt);
                return;
            }

            Restore(applySpeed: true);
            if (_seq.TakeCompletion())
            {
                try { _onDone?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        private void TickLaunch(float dt)
        {
            if (_target == null) return;
            bool lit = _seq.Phase == CameraBeatPhase.Hold || _seq.Phase == CameraBeatPhase.Main;
            if (lit && !_ignited)
            {
                _ignited = true;
                try { _onIgnition?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
                if (Application.isPlaying) _exhaust?.Ignite();
            }

            if (_seq.Phase == CameraBeatPhase.Main)
            {
                float alt = CameraBeatTuning.ClimbAltitude(_seq.MainT, _ctx.LifterHeight);
                _target.position = _lifterStart + Vector3.up * alt;
                _lifterMoved = true;
            }
            _ctx.LifterBase = _target.position + Vector3.up * _lifterBaseOffset;

            if (lit && _exhaust != null)
            {
                bool hold = _seq.Phase == CameraBeatPhase.Hold;
                float thrust = hold ? Mathf.Lerp(0.3f, 1f, _seq.PhaseT) : 1f;
                bool cloud = hold || _seq.MainT < CameraBeatTuning.GroundCloudUntil;
                _exhaust.Tick(dt, thrust, cloud, _ctx.Subject.y);
            }
        }

        // ---- camera --------------------------------------------------------------------------------

        private void ApplyCamera(float dt)
        {
            if (_cam == null) return;
            BeatPose pose = CameraBeatShots.Evaluate(_seq, _ctx);
            Vector3 pos = Clear(pose.Position, pose.LookAt, dt);
            pos += Shake();

            _cam.orthographic = false;
            _cam.fieldOfView = pose.Fov;
            _cam.nearClipPlane = 0.3f;
            if (_cam.farClipPlane < 600f) _cam.farClipPlane = 600f;
            Vector3 look = pose.LookAt - pos;
            if (look.sqrMagnitude < 1e-4f) look = _cam.transform.forward;
            _cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look.normalized, Vector3.up));

            if (_fogSaved && _fog)
            {
                // Same perspective haze the diorama lens uses: clear through the subject, soft at the horizon.
                DioramaRig.Fog(Vector3.Distance(pos, pose.LookAt), out float fs, out float fe);
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = fs;
                RenderSettings.fogEndDistance = fe;
            }
        }

        /// <summary>
        /// The first candidate heading whose pad shot has a clear line to the lifter: no building in
        /// the way or around the camera, no hill between. Falls back to the default heading.
        /// </summary>
        private float ChooseLaunchYaw(bool still)
        {
            var yaws = CameraBeatTuning.LaunchYawCandidates;
            for (int i = 0; i < yaws.Length; i++)
            {
                BeatPose p = CameraBeatShots.LaunchPad(_ctx.Subject, _ctx.LifterHeight, _ctx.ViewFlat, still, yaws[i]);
                Vector3 cam = CameraBeatShots.AboveGround(p.Position, TerrainDataBake.GroundHeight,
                    CameraBeatTuning.GroundClearance);
                // Lifted far off the planned height means the camera would sit on a hill.
                if (cam.y - p.Position.y > 0.5f * _ctx.LifterHeight) continue;
                if (!CameraBeatShots.TerrainClear(p.LookAt, cam, TerrainDataBake.GroundHeight, 0.2f)) continue;
                // The pad itself must show too, not only the top half of the lifter over a dune.
                Vector3 baseView = _ctx.Subject + Vector3.up * (CameraBeatTuning.LaunchBaseCheck * _ctx.LifterHeight);
                if (!CameraBeatShots.TerrainClear(baseView, cam, TerrainDataBake.GroundHeight, 0f)) continue;
                if (Blocked(cam, p.LookAt) || Blocked(cam, baseView)) continue;
                return yaws[i];
            }
            return CameraBeatTuning.LaunchYawDefault;
        }

        /// <summary>
        /// Terrain clearance from the height field, then buildings: if a structure sits between the
        /// subject and the camera (or around the camera), lift the camera over it. The lift eases in
        /// fast and out slowly so the shot never pops.
        /// </summary>
        private Vector3 Clear(Vector3 pos, Vector3 look, float dt)
        {
            pos = CameraBeatShots.AboveGround(pos, TerrainDataBake.GroundHeight, CameraBeatTuning.GroundClearance);
            float need = 0f;
            for (int i = 0; i < 24; i++)
            {
                if (!Blocked(pos + Vector3.up * need, look)) break;
                need += 0.75f;
            }
            float rate = need > _clearLift ? 14f : 3.5f;
            _clearLift = dt <= 0f ? need : Mathf.Lerp(_clearLift, need, 1f - Mathf.Exp(-rate * dt));
            if (_clearLift < need - 1.5f) _clearLift = need - 1.5f; // never lag far enough to sit inside a wall
            return pos + Vector3.up * _clearLift;
        }

        private static readonly Collider[] Overlaps = new Collider[16];
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private bool Blocked(Vector3 pos, Vector3 look)
        {
            float r = CameraBeatTuning.ProbeRadius;
            int n = Physics.OverlapSphereNonAlloc(pos, r, Overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (IsObstacle(Overlaps[i])) return true;

            Vector3 d = pos - look;
            float len = d.magnitude;
            if (len < 0.01f) return false;
            int h = Physics.SphereCastNonAlloc(look, r * 0.5f, d / len, Hits, len, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < h; i++)
                if (Hits[i].distance > 0.01f && IsObstacle(Hits[i].collider)) return true;
            return false;
        }

        /// <summary>
        /// Buildings count. The ground (handled by the height field), units, and the pad the lifter
        /// stands on (its pick box would hide the subject from every angle) do not.
        /// </summary>
        private bool IsObstacle(Collider c)
        {
            if (c == null) return false;
            var home = c.GetComponentInParent<ColonyStructure>();
            return home != null && home != _targetHome;
        }

        private Vector3 Shake()
        {
            if (_seq.Profile == null || !_seq.Profile.Shake || DemoSettings.ReduceMotion || _seq.Kind != CameraBeatKind.Launch)
                return Vector3.zero;
            float amp = 0f;
            if (_seq.Phase == CameraBeatPhase.Hold) amp = CameraBeatTuning.IgnitionShake * _seq.PhaseT;
            else if (_seq.Phase == CameraBeatPhase.Main) amp = CameraBeatTuning.ClimbShake * (1f - _seq.MainT);
            if (amp <= 0f) return Vector3.zero;
            float t = Time.unscaledTime * 24f;
            return new Vector3(Mathf.PerlinNoise(t, 0.37f) - 0.5f, Mathf.PerlinNoise(0.71f, t) - 0.5f,
                Mathf.PerlinNoise(t, 2.1f) - 0.5f) * (2f * amp * _ctx.LifterHeight * 0.3f);
        }

        private void MeasureLifter(Transform lifter)
        {
            var rends = lifter.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0)
            {
                _ctx.LifterHeight = 3f;
                _lifterBaseOffset = 0f;
                return;
            }
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            _ctx.LifterHeight = Mathf.Max(0.5f, b.size.y);
            _lifterBaseOffset = b.min.y - lifter.position.y;
        }

        // ---- handoff -------------------------------------------------------------------------------

        private void Restore(bool applySpeed)
        {
            bool launch = _seq.Kind == CameraBeatKind.Launch;
            if (_cam != null) _snap.Apply(_cam);
            if (_skyFallback)
            {
                _skyFallback = false;
                // Give the day cycle back its untinted void-fill base, then show exactly what was on screen.
                if (_cam != null && _loop != null && _loop.BodyProfile != null)
                {
                    Color shown = _cam.backgroundColor;
                    _cam.backgroundColor = PlanetaryMapDressing.VoidFillColor(_loop.BodyProfile);
                    SunCycle.RebaseClearColor(_cam);
                    _cam.backgroundColor = shown;
                }
            }
            if (_ctrl != null) _ctrl.enabled = _ctrlWasEnabled;
            RestoreFog();
            if (_skyInstalled)
            {
                _skyInstalled = false;
                SkyPanorama.Uninstall(_cam, _loop != null ? _loop.BodyProfile : null);
                if (_snap.Valid && _cam != null) _cam.clearFlags = _snap.ClearFlags;
            }
            SpecialistStatusDisplay.HideForCapture = _tagsWereHidden;

            _exhaust?.Dispose();
            _exhaust = null;
            if (_lifterMoved && _target != null) _target.position = _lifterStart;
            _lifterMoved = false;

            if (_cam != null) DemoAtmosphere.SyncFog(_cam);
            // Swallow this frame's and the next frame's input: the skip key must not also pause.
            _blockThroughFrame = Time.frameCount + 1;
            // A completed launch hands off to a scene load; stay black until it lands.
            if (launch && _seq.End != CameraBeatEnd.Aborted && _onDone != null)
                _blackUntil = Time.realtimeSinceStartup + 1.5f;

            if (applySpeed && Application.isPlaying)
            {
                if (_loop != null) _loop.ApplySimSpeed();
                else Time.timeScale = SimSpeed.TimeScale;
            }
            Debug.Log($"[CameraBeat] {_seq.Kind} beat ended: {_seq.End}.");
        }

        private void SaveFog()
        {
            _fogSaved = true;
            _fog = RenderSettings.fog;
            _fogMode = RenderSettings.fogMode;
            _fogStart = RenderSettings.fogStartDistance;
            _fogEnd = RenderSettings.fogEndDistance;
            _fogDensity = RenderSettings.fogDensity;
        }

        private void RestoreFog()
        {
            if (!_fogSaved) return;
            _fogSaved = false;
            RenderSettings.fog = _fog;
            RenderSettings.fogMode = _fogMode;
            RenderSettings.fogStartDistance = _fogStart;
            RenderSettings.fogEndDistance = _fogEnd;
            RenderSettings.fogDensity = _fogDensity;
        }

        // ---- overlay -------------------------------------------------------------------------------

        private void OnGUI()
        {
            bool black = _blackUntil > 0f && Time.realtimeSinceStartup < _blackUntil;
            if (!_seq.Active && !black) return;
            GUI.depth = -100;
            float w = Screen.width, h = Screen.height;
            var prev = GUI.color;

            if (_seq.Active)
            {
                // Letterbox eases in over the first half second.
                float bar = h * 0.1f * Mathf.Clamp01(_seq.Elapsed / 0.5f);
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0f, 0f, w, bar), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0f, h - bar, w, bar), Texture2D.whiteTexture);
                GUI.color = prev;

                EnsureStyles();
                float s = Mathf.Max(0.6f, DemoSettings.HudScale); // the player's UI Scale (auto-by-height by default)
                // Captions are subtitles: they follow the player's TEXT SIZE as well as the UI scale.
                _captionStyle.fontSize = Mathf.RoundToInt(HudTextScale.Font(26) * s);
                _hintStyle.fontSize = Mathf.RoundToInt(HudTextScale.Font(15) * s);
                if (!string.IsNullOrEmpty(_caption))
                    GUI.Label(new Rect(0f, h - bar, w, bar), _caption, _captionStyle);
                GUI.Label(new Rect(0f, h - bar, w - 28f * s, bar), SkipHint(), _hintStyle);
            }

            float fade = black ? 1f : _seq.Active ? _seq.Fade : 0f;
            if (fade > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, fade);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
                GUI.color = prev;
            }
        }

        private static string SkipHint()
        {
            if (GamepadInput.ControllerActive)
                return Loc.F("{0} / {1} / {2}  ·  SKIP",
                    GamepadMap.Label(PadButton.South), GamepadMap.Label(PadButton.East), GamepadMap.Label(PadButton.Start));
            return Loc.S("ESC / CLICK  ·  SKIP");
        }

        private void EnsureStyles()
        {
            if (_captionStyle != null) return;
            _captionStyle = HudSkin.Text(HudSkin.Display, 26, FontStyle.Bold, HudSkin.GoldBright, TextAnchor.MiddleCenter);
            _hintStyle = HudSkin.Text(HudSkin.Body, 15, FontStyle.Normal, HudSkin.WithAlpha(HudSkin.TextPrimary, 0.7f),
                TextAnchor.MiddleRight);
        }
    }
}
