using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Controller input through the legacy Input Manager (ProjectSettings/InputManager.asset,
    /// the <c>SM_Pad*</c> axes; layout in Docs/CONTROLS.md). Safe with no controller and with
    /// an Input Manager that lacks the axes: the first read probes every name once, and a
    /// missing one switches controller support off for the session instead of throwing every frame.
    ///
    /// Two modes share the pad. Camera mode is the original layout (left stick pans, A jumps to
    /// the alert). Pointer mode drives the real OS cursor so the IMGUI HUD works without a mouse:
    /// the left stick moves the pointer and A / R3 send genuine left / right clicks (Windows
    /// SendInput, which Proton implements on the Steam Deck). Pointer mode is always on outside
    /// play, in a placement tool and with research open; Back toggles it the rest of the time.
    ///
    /// "Controller mode" follows the last device used: touching the pad turns it on (hints name
    /// buttons), moving the mouse or typing turns it off. The Steam Deck starts in it.
    /// </summary>
    public static class GamepadInput
    {
        public const string PrefsKey = "SM_Set_Gamepad";
        public const string PointerSpeedKey = "SM_Set_PadPointerSpeed";

        public const string LeftX = "SM_PadLX";
        public const string LeftY = "SM_PadLY";
        public const string RightX = "SM_PadRX";
        public const string RightY = "SM_PadRY";
        public const string LeftTrigger = "SM_PadLT";
        public const string RightTrigger = "SM_PadRT";
        public const string DpadX = "SM_PadDX";
        public const string DpadY = "SM_PadDY";

        public const string ButtonA = "SM_PadA";
        public const string ButtonB = "SM_PadB";
        public const string ButtonX = "SM_PadX";
        public const string ButtonY = "SM_PadY";
        public const string ButtonLB = "SM_PadLB";
        public const string ButtonRB = "SM_PadRB";
        public const string ButtonBack = "SM_PadBack";
        public const string ButtonStart = "SM_PadStart";
        public const string ButtonL3 = "SM_PadL3";
        public const string ButtonR3 = "SM_PadR3";

        private static readonly string[] Axes = { LeftX, LeftY, RightX, RightY, LeftTrigger, RightTrigger, DpadX, DpadY };
        private static readonly string[] Buttons =
            { ButtonA, ButtonB, ButtonX, ButtonY, ButtonLB, ButtonRB, ButtonBack, ButtonStart, ButtonL3, ButtonR3 };

        private static bool _probed;
        private static bool _axesOk;
        private static bool _prefLoaded;
        private static bool _enabled = true;
        private static float _nextDeviceCheck;
        private static bool _connected;

        // Controller mode and pointer state.
        private static bool _pointerToggle;
        private static Vector2 _pointer;
        private static float _stickHeld;
        private static bool _leftDown, _rightDown;
        private static int _warpFrame = -100;
        private static Vector2 _lastMouse;
        private static bool _wasTyping;
        private static bool _deckStarted;
        private static int _dpadFrame = -1;
        private static Vector2 _dpadPrev, _dpadNow;
        private static GameLoop _loop;
        private static OverseerHud _hud;
        private static int _nextFind;

        /// <summary>Player preference (Settings → CONTROLLER). On by default.</summary>
        public static bool Enabled
        {
            get
            {
                if (!_prefLoaded)
                {
                    _prefLoaded = true;
                    _enabled = GamePrefs.GetInt(PrefsKey, 1) == 1;
                }
                return _enabled;
            }
            set
            {
                _prefLoaded = true;
                _enabled = value;
                GamePrefs.SetInt(PrefsKey, value ? 1 : 0);
                GamePrefs.Save();
                if (!value) ExitControllerMode();
            }
        }

        /// <summary>Pointer speed preset (Settings → POINTER SPEED): 0 slow, 1 normal, 2 fast.</summary>
        public static int PointerSpeed
        {
            get => Mathf.Clamp(GamePrefs.GetInt(PointerSpeedKey, 1), 0, GamepadMath.PointerSpeeds.Length - 1);
            set
            {
                GamePrefs.SetInt(PointerSpeedKey, Mathf.Clamp(value, 0, GamepadMath.PointerSpeeds.Length - 1));
                GamePrefs.Save();
            }
        }

        /// <summary>True when every SM_Pad axis and button exists in the Input Manager.</summary>
        public static bool AxesAvailable
        {
            get
            {
                if (Virtual != null) return true;
                Probe();
                return _axesOk;
            }
        }

        /// <summary>A joystick is plugged in. Re-checked at most once a second (the OS query allocates).</summary>
        public static bool Connected
        {
            get
            {
                if (Virtual != null) return true;
                float now = Time.unscaledTime;
                if (now < _nextDeviceCheck) return _connected;
                _nextDeviceCheck = now + 1f;
                _connected = false;
                try
                {
                    string[] names = Input.GetJoystickNames();
                    for (int i = 0; names != null && i < names.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(names[i]))
                        {
                            _connected = true;
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    _connected = false;
                }
                return _connected;
            }
        }

        /// <summary>Read the controller this frame? Off in batch mode, without axes, unplugged, or turned off.</summary>
        public static bool Active =>
            Virtual != null || (!Application.isBatchMode && Enabled && AxesAvailable && Connected);

        /// <summary>The pad was the last thing the player touched, so hints name its buttons.</summary>
        public static bool ControllerActive { get; private set; }

        /// <summary>
        /// The left stick moves the OS pointer and A clicks. Always on outside play, in a placement
        /// tool and with research open (those need aiming); Back toggles it the rest of the time.
        /// </summary>
        public static bool PointerMode => ControllerActive && Active && (_pointerToggle || PointerForced());

        /// <summary>Left stick, or the D-pad when the stick is at rest. Shaped; x right, y forward.</summary>
        public static Vector2 Pan()
        {
            if (!Active) return new Vector2(0f, 0f);
            if (PointerMode)
            {
                // The stick drives the pointer and the D-pad carries actions. Pushing the pointer
                // against an edge pans; with EDGE SCROLL on, the camera's own margin pan does it.
                if (DemoSettings.EdgeScroll) return new Vector2(0f, 0f);
                return GamepadMath.EdgePush(_pointer, Raw(LeftX), Raw(LeftY), Screen.width, Screen.height);
            }
            Vector2 stick = GamepadMath.Stick(Raw(LeftX), Raw(LeftY));
            if (stick.x != 0f || stick.y != 0f) return stick;
            float dx = Raw(DpadX);
            float dy = Raw(DpadY);
            return new Vector2(Mathf.Abs(dx) > 0.5f ? Mathf.Sign(dx) : 0f, Mathf.Abs(dy) > 0.5f ? Mathf.Sign(dy) : 0f);
        }

        /// <summary>Zoom drive, +1 = zoom out (same sign as Q).</summary>
        public static float Zoom() =>
            Active ? GamepadMath.Zoom(Raw(RightY), Raw(LeftTrigger), Raw(RightTrigger)) : 0f;

        /// <summary>Right stick X, shaped. Positive orbits the view clockwise.</summary>
        public static float Orbit() => Active ? GamepadMath.Axis(Raw(RightX)) : 0f;

        /// <summary>
        /// A button went down this frame. In pointer mode A and R3 belong to the pointer (clicks),
        /// and Back always toggles pointer mode, so game code never sees them.
        /// </summary>
        public static bool Pressed(string button)
        {
            if (!Active) return false;
            if (button == ButtonBack) return false;
            if ((button == ButtonA || button == ButtonR3) && PointerMode) return false;
            return RawDown(button);
        }

        /// <summary>Any stick off centre or any controller button down this frame.</summary>
        public static bool AnyInput()
        {
            if (!Active) return false;
            if (GamepadMath.IsDeflected(Raw(LeftX), Raw(LeftY)) ||
                GamepadMath.IsDeflected(Raw(RightX), Raw(RightY)) ||
                Raw(LeftTrigger) > GamepadMath.TriggerDeadzone ||
                Raw(RightTrigger) > GamepadMath.TriggerDeadzone ||
                Mathf.Abs(Raw(DpadX)) > 0.5f || Mathf.Abs(Raw(DpadY)) > 0.5f)
                return true;
            for (int i = 0; i < Buttons.Length; i++)
            {
                if (RawDown(Buttons[i])) return true;
            }
            return false;
        }

        // ---- actions through InputBindings ------------------------------------------------------------

        /// <summary>
        /// Pad buttons for rebindable actions that the camera does not already handle: L3 jumps to
        /// the alert, and in pointer mode the D-pad cycles the overlay and tools and moves the bounty.
        /// </summary>
        private static bool ActionDown(GameAction action)
        {
            if (!Active) return false;
            switch (action)
            {
                case GameAction.JumpToAlert: return RawDown(ButtonL3);
                case GameAction.CycleOverlay: return PointerMode && DpadDown(0, 1);
                case GameAction.CycleTool: return PointerMode && DpadDown(0, -1);
                case GameAction.BountyDown: return PointerMode && DpadDown(-1, 0);
                case GameAction.BountyUp: return PointerMode && DpadDown(1, 0);
                default: return false;
            }
        }

        /// <summary>The button for an action in hint text, while the controller is in use.</summary>
        private static string HintLabel(GameAction action) =>
            ControllerActive && Active ? GamepadMath.ButtonLabel(action, PointerMode) : null;

        /// <summary>D-pad edge this frame in the given direction (axes are not buttons, so edges are tracked).</summary>
        private static bool DpadDown(int x, int y)
        {
            SampleDpad();
            if (x != 0) return x * _dpadNow.x > 0.5f && x * _dpadPrev.x <= 0.5f;
            return y * _dpadNow.y > 0.5f && y * _dpadPrev.y <= 0.5f;
        }

        /// <summary>
        /// Once per frame, whoever asks first (the runner or a hotkey reader), so the edge is the
        /// same for every reader whatever the script order. The runner asks every frame.
        /// </summary>
        private static void SampleDpad()
        {
            int frame = Time.frameCount;
            if (_dpadFrame == frame) return;
            Vector2 now = new Vector2(Raw(DpadX), Raw(DpadY));
            _dpadPrev = _dpadFrame == frame - 1 ? _dpadNow : now;
            _dpadNow = now;
            _dpadFrame = frame;
        }

        // ---- per-frame update ---------------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ControllerActive = false;
            _pointerToggle = false;
            _leftDown = _rightDown = false;
            _deckStarted = false;
            _wasTyping = false;
            _loop = null;
            _hud = null;
            _dpadFrame = -1;
            Virtual = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            InputBindings.PadDown = ActionDown;
            InputBindings.PadLabel = HintLabel;
            var go = new GameObject("[Gamepad]");
            go.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        private static void Tick()
        {
            if (Active) SampleDpad();
            if (!_deckStarted && HandheldMode.OnSteamDeck)
            {
                // The Deck starts on its controls: hints name buttons from the first screen.
                _deckStarted = true;
                if (Active) EnterControllerMode(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            }

            if (!Active)
            {
                if (ControllerActive) ExitControllerMode();
                return;
            }

            DetectDevice();
            if (!ControllerActive) return;

            bool typing = InputBindings.TextEntryActive;
            if (typing && !_wasTyping) SteamService.ShowFloatingKeyboard();
            _wasTyping = typing;

            if (RawDown(ButtonBack)) _pointerToggle = !_pointerToggle;

            if (!PointerMode)
            {
                ReleaseButtons();
                _stickHeld = 0f;
                return;
            }

            MovePointer();
            Click(ButtonA, ref _leftDown, true);
            Click(ButtonR3, ref _rightDown, false);
        }

        private static void DetectDevice()
        {
            Vector2 mouse = MousePosition();
            bool mouseMoved = Time.frameCount - _warpFrame > 2 && (mouse - _lastMouse).sqrMagnitude > 9f;
            _lastMouse = mouse;

            if (AnyInput())
            {
                if (!ControllerActive) EnterControllerMode(mouse);
                return;
            }
            if (!ControllerActive) return;

            if (mouseMoved || KeyboardTyped())
                ExitControllerMode();
        }

        private static bool KeyboardTyped()
        {
            if (Virtual != null || InputBindings.TextEntryActive || !Input.anyKeyDown) return false;
            // anyKeyDown also counts mouse and joystick buttons (our own clicks among them).
            for (KeyCode k = KeyCode.Mouse0; k <= KeyCode.Mouse6; k++)
                if (Input.GetKeyDown(k)) return false;
            for (KeyCode k = KeyCode.JoystickButton0; k <= KeyCode.JoystickButton19; k++)
                if (Input.GetKeyDown(k)) return false;
            return true;
        }

        private static void EnterControllerMode(Vector2 pointer)
        {
            ControllerActive = true;
            bool inside = pointer.x >= 0f && pointer.y >= 0f && pointer.x < Screen.width && pointer.y < Screen.height;
            _pointer = inside ? pointer : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            _stickHeld = 0f;
        }

        private static void ExitControllerMode()
        {
            ReleaseButtons();
            ControllerActive = false;
            _stickHeld = 0f;
        }

        private static bool PointerForced()
        {
            if (Time.frameCount >= _nextFind && (_loop == null || _hud == null))
            {
                _nextFind = Time.frameCount + 30; // the lookups walk the scene; not every frame
                if (_loop == null) _loop = UnityEngine.Object.FindAnyObjectByType<GameLoop>();
                if (_hud == null) _hud = UnityEngine.Object.FindAnyObjectByType<OverseerHud>();
            }
            if (_loop == null || !_loop.IsPlaying) return true; // title, pause menu, settings, banners
            if (_loop.ActiveTool != OverseerTool.None) return true;
            return _hud != null && _hud.TechPanelOpen;
        }

        private static void MovePointer()
        {
            Vector2 step = GamepadMath.PointerStep(Raw(LeftX), Raw(LeftY), _stickHeld, Screen.height, PointerSpeed,
                Time.unscaledDeltaTime);
            if (step.x == 0f && step.y == 0f)
            {
                _stickHeld = 0f;
                return;
            }
            _stickHeld += Time.unscaledDeltaTime;
            _pointer += step;
            _pointer.x = Mathf.Clamp(_pointer.x, 0f, Mathf.Max(0f, Screen.width - 1f));
            _pointer.y = Mathf.Clamp(_pointer.y, 0f, Mathf.Max(0f, Screen.height - 1f));
            Warp(_pointer);
            _warpFrame = Time.frameCount;
        }

        private static void Click(string button, ref bool down, bool left)
        {
            if (!down && RawDown(button))
            {
                down = true;
                SendButton(left, true);
            }
            else if (down && !RawHeld(button))
            {
                down = false;
                SendButton(left, false);
            }
        }

        /// <summary>Never leave a synthetic button held down (focus loss, leaving pointer mode).</summary>
        private static void ReleaseButtons()
        {
            if (_leftDown) { _leftDown = false; SendButton(true, false); }
            if (_rightDown) { _rightDown = false; SendButton(false, false); }
        }

        private sealed class Runner : MonoBehaviour
        {
            private void Update() => Tick();
            private void OnApplicationFocus(bool focused)
            {
                if (!focused) ReleaseButtons();
            }
            private void OnDestroy() => ReleaseButtons();
        }

        // ---- raw reads ------------------------------------------------------------------------------------

        private static float Raw(string axis)
        {
            if (Virtual != null) return Virtual.Axis(axis);
            try
            {
                return Input.GetAxisRaw(axis);
            }
            catch (ArgumentException)
            {
                _axesOk = false;
                return 0f;
            }
        }

        private static bool RawDown(string button)
        {
            if (Virtual != null) return Virtual.Down(button);
            try
            {
                return Input.GetButtonDown(button);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool RawHeld(string button)
        {
            if (Virtual != null) return Virtual.Held(button);
            try
            {
                return Input.GetButton(button);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void Probe()
        {
            if (_probed) return;
            _probed = true;
            _axesOk = true;
            if (Application.isBatchMode) return;
            string missing = null;
            for (int i = 0; i < Axes.Length + Buttons.Length; i++)
            {
                string name = i < Axes.Length ? Axes[i] : Buttons[i - Axes.Length];
                try
                {
                    if (i < Axes.Length) Input.GetAxisRaw(name);
                    else Input.GetButton(name);
                }
                catch (ArgumentException)
                {
                    missing = name;
                    break;
                }
            }
            if (missing == null) return;
            _axesOk = false;
            Debug.LogWarning($"[Gamepad] Input Manager has no '{missing}' axis; controller support is off. See Docs/CONTROLS.md.");
        }

        // ---- virtual pad (tests) ----------------------------------------------------------------------------

        /// <summary>
        /// Test seam: while set, every read comes from this pad instead of the Input Manager (also in
        /// batch mode), and pointer moves and clicks are recorded on it instead of reaching the OS.
        /// </summary>
        internal static VirtualPad Virtual { get; set; }

        /// <summary>Tests: back to mouse-and-keyboard with pointer mode toggled off.</summary>
        internal static void ResetModeForTests()
        {
            ExitControllerMode();
            _pointerToggle = false;
            _dpadFrame = -1;
        }

        internal sealed class VirtualPad
        {
            private readonly Dictionary<string, float> _axes = new Dictionary<string, float>();
            private readonly Dictionary<string, int> _downFrame = new Dictionary<string, int>();
            private readonly HashSet<string> _held = new HashSet<string>();

            public readonly List<Vector2> Warps = new List<Vector2>();
            public readonly List<string> Clicks = new List<string>();

            public void SetAxis(string axis, float value) => _axes[axis] = value;

            /// <summary>Press a button; it reads as down on the next frame.</summary>
            public void Press(string button)
            {
                _held.Add(button);
                _downFrame[button] = Time.frameCount + 1;
            }

            public void Release(string button) => _held.Remove(button);

            public void Clear()
            {
                _axes.Clear();
                _held.Clear();
                _downFrame.Clear();
            }

            internal float Axis(string axis) => _axes.TryGetValue(axis, out float v) ? v : 0f;
            internal bool Held(string button) => _held.Contains(button);
            internal bool Down(string button) => _downFrame.TryGetValue(button, out int f) && f == Time.frameCount;
        }

        // ---- OS pointer and buttons --------------------------------------------------------------------------
        // IMGUI and the legacy Input class only see real OS mouse events, so the pointer is moved and
        // clicked at the OS level. Proton (Steam Deck) implements these calls as well.

        private static Vector2 MousePosition()
        {
            Vector3 m = Input.mousePosition;
            return new Vector2(m.x, m.y);
        }

        private static void Warp(Vector2 pointer)
        {
            if (Virtual != null)
            {
                Virtual.Warps.Add(pointer);
                return;
            }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                IntPtr window = GetActiveWindow();
                if (window == IntPtr.Zero) return;
                // Unity's origin is bottom left; the window's client area starts top left.
                var pt = new Point { X = Mathf.RoundToInt(pointer.x), Y = Mathf.RoundToInt(Screen.height - 1f - pointer.y) };
                if (!ClientToScreen(window, ref pt)) return;
                SetCursorPos(pt.X, pt.Y);
            }
            catch (Exception)
            {
                // A missing user32 entry point must never break input.
            }
#endif
        }

        private static void SendButton(bool left, bool down)
        {
            if (Virtual != null)
            {
                Virtual.Clicks.Add((left ? "left" : "right") + (down ? " down" : " up"));
                return;
            }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            uint flags = left ? (down ? LeftDownFlag : LeftUpFlag) : (down ? RightDownFlag : RightUpFlag);
            try
            {
                var input = new[] { new InputRecord { type = InputMouse, mi = new MouseInput { dwFlags = flags } } };
                SendInput(1, input, Marshal.SizeOf(typeof(InputRecord)));
            }
            catch (Exception)
            {
                // As above.
            }
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const uint InputMouse = 0;
        private const uint LeftDownFlag = 0x0002, LeftUpFlag = 0x0004, RightDownFlag = 0x0008, RightUpFlag = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct InputRecord
        {
            public uint type;
            public MouseInput mi; // the largest member of the INPUT union, so the size matches
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr window, ref Point point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, InputRecord[] inputs, int size);
#endif
    }
}
