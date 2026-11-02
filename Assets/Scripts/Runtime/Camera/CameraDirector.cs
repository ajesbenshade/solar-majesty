using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Short scripted camera beats: the launch (frame the pad, follow the craft up, come home)
    /// and victory (slow pull-back and part orbit over the Commons). Presentation only.
    ///
    /// Rules: skipped under Reduce Motion, in batch mode, with <c>-nobeats</c> and with no camera
    /// rig; any player input after a short grace window ends the beat at once; the rig's exact pose
    /// (position, rotation, zoom and its internal pan/orbit targets) is put back when the beat ends,
    /// however it ends. Timing and curves are in <see cref="CameraBeatMath"/>.
    ///
    /// While a beat plays the gameplay HUD is hidden (<see cref="HidesHud"/>) and a letterbox,
    /// a caption and a skip hint replace it; world input stands down (<see cref="BlocksInput"/>), so
    /// the key or click that skips the beat never also pauses, selects or places anything.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraDirector : MonoBehaviour
    {
        private enum Beat
        {
            None = 0,
            Launch = 1,
            Victory = 2
        }

        private const float PlumeInterval = 0.32f;

        private IsometricCameraController _rig;
        private Camera _cam;
        private GameLoop _loop;
        private Beat _beat;
        private float _elapsed;

        private Quaternion _fromRot;
        private float _fromZoom;
        private Vector3 _fromFocus;
        private float _fromDistance;
        private Vector3 _target;

        private Transform _craft;
        private Vector3 _craftStart;
        private float _nextPlume;
        private string _caption;
        private GUIStyle _captionStyle, _hintStyle;

        /// <summary>Tests and capture harnesses can force beats on or off; null follows the defaults.</summary>
        public static bool? EnabledOverride;

        private static CameraDirector _current;
        private static int _blockThroughFrame = -1;
        private static bool _blockUntilRelease;
        private static bool? _argDisabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _current = null;
            _blockThroughFrame = -1;
            _blockUntilRelease = false;
            _argDisabled = null;
        }

        /// <summary>A beat is playing somewhere.</summary>
        public static bool Active => _current != null && _current._beat != Beat.None;

        /// <summary>
        /// World input must stand down: a beat is playing, it ended this frame or the last, or the
        /// mouse button that skipped it is still held.
        /// </summary>
        public static bool BlocksInput
        {
            get
            {
                if (_blockUntilRelease && !(Input.GetMouseButton(0) || Input.GetMouseButton(1)))
                    _blockUntilRelease = false;
                return CameraBeatMath.BlocksInput(Active, Time.frameCount, _blockThroughFrame, _blockUntilRelease);
            }
        }

        /// <summary>
        /// The gameplay HUD should not draw. Same window as <see cref="BlocksInput"/>: a HUD button
        /// redrawn under the skip click would otherwise take that click.
        /// </summary>
        public static bool HidesHud => BlocksInput;

        /// <summary>Beats play unless overridden, or the game was started with <c>-nobeats</c>.</summary>
        public static bool Enabled
        {
            get
            {
                if (EnabledOverride.HasValue) return EnabledOverride.Value;
                if (!_argDisabled.HasValue)
                    _argDisabled = CameraBeatMath.ArgsDisableBeats(System.Environment.GetCommandLineArgs());
                return !_argDisabled.Value;
            }
        }

        /// <summary>True while a beat owns <paramref name="rig"/>.</summary>
        public static bool IsPlaying(IsometricCameraController rig) =>
            rig != null && rig.TryGetComponent(out CameraDirector d) && d._beat != Beat.None;

        /// <summary>Beats run only in a live, windowed game with motion allowed.</summary>
        public static bool CanPlay(IsometricCameraController rig) =>
            rig != null && rig.isActiveAndEnabled && Application.isPlaying && !Application.isBatchMode &&
            Enabled && !DemoSettings.ReduceMotion && rig.GetComponent<Camera>() != null;

        /// <summary>
        /// Launch beat over <paramref name="pad"/>. <paramref name="craft"/> (may be null) is lifted
        /// along the ascent. <paramref name="caption"/> (may be null) shows in the letterbox.
        /// Returns false and does nothing when a beat is not allowed.
        /// </summary>
        public static bool TryPlayLaunch(IsometricCameraController rig, Vector3 pad, Transform craft,
            string caption = null)
        {
            // Frame the craft itself: a reused parked ship need not sit exactly on the pad marker.
            Vector3 from = craft != null ? craft.position : pad;
            var d = For(rig);
            if (d == null || !d.Begin(Beat.Launch, from)) return false;
            d._craft = craft;
            d._craftStart = from;
            d._nextPlume = CameraBeatMath.LaunchFrameSeconds;
            d._caption = caption;
            return true;
        }

        /// <summary>Victory beat over <paramref name="commons"/>. Returns false when not allowed.</summary>
        public static bool TryPlayVictory(IsometricCameraController rig, Vector3 commons, string caption = null)
        {
            var d = For(rig);
            if (d == null || !d.Begin(Beat.Victory, commons)) return false;
            d._caption = caption;
            return true;
        }

        private static CameraDirector For(IsometricCameraController rig)
        {
            if (!CanPlay(rig)) return null;
            if (!rig.TryGetComponent(out CameraDirector d))
                d = rig.gameObject.AddComponent<CameraDirector>();
            return d;
        }

        private bool Begin(Beat beat, Vector3 target)
        {
            if (_beat != Beat.None) Stop();
            _rig = GetComponent<IsometricCameraController>();
            _cam = GetComponent<Camera>();
            if (_rig == null || _cam == null) return false;
            _loop = _loop != null ? _loop : FindAnyObjectByType<GameLoop>();
            if (_loop != null && !_loop.AllowsCamera) return false;
            if (!_rig.BeginDirected()) return false;

            Transform t = transform;
            _fromRot = t.rotation;
            _fromZoom = _cam.orthographicSize;
            Vector3 fwd = t.forward;
            if (IsometricCameraController.TryGroundFocus(t.position, fwd, out Vector3 focus))
            {
                _fromFocus = focus;
                _fromDistance = Vector3.Distance(t.position, focus);
            }
            else
            {
                _fromDistance = 30f;
                _fromFocus = t.position + fwd * _fromDistance;
            }

            _beat = beat;
            _elapsed = 0f;
            _target = target;
            _craft = null;
            _caption = null;
            _current = this;
            Debug.Log($"[CameraBeat] {beat} beat started.");
            return true;
        }

        private void LateUpdate()
        {
            if (_beat == Beat.None) return;
            if (_rig == null || _cam == null || !_rig.IsDirected)
            {
                // Someone else took the rig back (a hard camera snap); just forget the beat.
                _beat = Beat.None;
                _blockThroughFrame = Time.frameCount + 1;
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            float duration = _beat == Beat.Launch ? CameraBeatMath.LaunchDuration : CameraBeatMath.VictoryDuration;
            bool blocked = _loop != null && !_loop.AllowsCamera;
            if (blocked || CameraBeatMath.ShouldCancel(_elapsed, PlayerInput()) || _elapsed >= duration)
            {
                Stop();
                return;
            }

            if (_beat == Beat.Launch) PoseLaunch();
            else PoseVictory();
        }

        private void PoseLaunch()
        {
            float height = CameraBeatMath.CraftHeight(_elapsed);
            if (_craft != null)
                _craft.position = _craftStart + Vector3.up * height;
            if (height > 0.01f && _elapsed >= _nextPlume && _elapsed < CameraBeatMath.LaunchFrameSeconds + CameraBeatMath.LaunchAscentSeconds)
            {
                _nextPlume = _elapsed + PlumeInterval;
                DemoVfx.LaunchPlume(new Vector3(_craftStart.x, _craftStart.y + height, _craftStart.z));
            }

            Vector3 focus = CameraBeatMath.LaunchFocus(_elapsed, _fromFocus, _target);
            float zoom = CameraBeatMath.LaunchZoom(_elapsed, _fromZoom);
            Place(focus, _fromRot, zoom);
        }

        private void PoseVictory()
        {
            Vector3 focus = CameraBeatMath.VictoryFocus(_elapsed, _fromFocus, _target);
            float zoom = CameraBeatMath.VictoryZoom(_elapsed, _fromZoom, _rig.MaxZoomSize);
            Quaternion rot = Quaternion.AngleAxis(CameraBeatMath.VictoryYaw(_elapsed), Vector3.up) * _fromRot;
            Place(focus, rot, zoom);
        }

        /// <summary>Camera on the line through <paramref name="focus"/>. A perspective lens zooms by distance.</summary>
        private void Place(Vector3 focus, Quaternion rot, float zoom)
        {
            float distance = _fromDistance;
            if (!_cam.orthographic && _fromZoom > 0.01f)
                distance *= zoom / _fromZoom;
            Vector3 pos = focus - rot * Vector3.forward * distance;
            _rig.ApplyDirectedPose(pos, rot, zoom);
        }

        private static bool PlayerInput()
        {
            if (Input.anyKey || Input.anyKeyDown) return true;
            if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) return true;
            if (DemoSettings.EdgeScroll && HudPointerAtEdge()) return true;
            return GamepadInput.AnyInput();
        }

        private static bool HudPointerAtEdge()
        {
            Vector3 m = Input.mousePosition;
            if (m.x < 0f || m.y < 0f || m.x > Screen.width || m.y > Screen.height) return false;
            const float margin = 6f;
            return m.x <= margin || m.y <= margin || m.x >= Screen.width - margin || m.y >= Screen.height - margin;
        }

        /// <summary>End the beat now and give the player back their exact camera.</summary>
        public void Stop()
        {
            if (_beat == Beat.None) return;
            if (_beat == Beat.Launch && _craft != null && CameraBeatMath.CraftHeight(_elapsed) > 1f)
            {
                // The craft has left; the scene reload that follows a launch rebuilds the pad.
                _craft.gameObject.SetActive(false);
            }
            Debug.Log($"[CameraBeat] {_beat} beat ended after {_elapsed:F1}s.");
            _beat = Beat.None;
            _craft = null;
            if (_rig != null) _rig.EndDirected();
            // Swallow this frame's and the next frame's input: the skip key must not also pause,
            // and a held skip click must not land on the map or a HUD button once it redraws.
            _blockThroughFrame = Time.frameCount + 1;
            _blockUntilRelease = Input.GetMouseButton(0) || Input.GetMouseButton(1);
        }

        private void OnDisable() => Stop();

        private void OnDestroy()
        {
            if (_current == this) _current = null;
        }

        // ---- overlay: letterbox, caption, skip hint -----------------------------------------------

        private void OnGUI()
        {
            if (_beat == Beat.None) return;
            GUI.depth = -100;
            float w = Screen.width, h = Screen.height;
            float bar = CameraBeatMath.LetterboxHeight(_elapsed, h);
            Color prev = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, w, bar), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, h - bar, w, bar), Texture2D.whiteTexture);
            GUI.color = prev;

            EnsureStyles();
            float s = Mathf.Max(0.6f, DemoSettings.HudScale); // the player's UI Scale
            _captionStyle.fontSize = Mathf.RoundToInt(26f * s);
            _hintStyle.fontSize = Mathf.RoundToInt(15f * s);
            if (!string.IsNullOrEmpty(_caption))
                GUI.Label(new Rect(0f, h - bar, w, bar), _caption, _captionStyle);
            GUI.Label(new Rect(0f, h - bar, w - 28f * s, bar), CameraBeatMath.SkipHint(GamepadInput.Active), _hintStyle);
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
