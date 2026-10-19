using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SolarMajesty
{
    /// <summary>
    /// Controller support, including the Steam Deck (which presents its controls to the game as an
    /// Xbox pad by default).
    ///
    /// The HUD is IMGUI and the map is clicked, so the pad drives the real OS cursor: the left
    /// stick warps it and A/B send genuine left/right clicks. Every existing button, placement and
    /// selection therefore works unchanged. Hotkey actions come from <see cref="GamepadMap"/>
    /// through <see cref="InputBindings"/>, the right stick pans and the triggers zoom.
    ///
    /// "Controller mode" follows the last device used: touching the pad turns it on (hints show
    /// button names), moving the mouse or typing turns it off.
    /// </summary>
    public sealed class GamepadInput : MonoBehaviour
    {
        public const string SpeedKey = "SM_Set_PadCursorSpeed";

        public static GamepadInput Instance { get; private set; }

        /// <summary>The pad was the last thing the player touched.</summary>
        public static bool ControllerActive { get; private set; }

        /// <summary>Right-stick pan this frame, in keyboard-pan units (−1..1 per axis).</summary>
        public static Vector2 Pan { get; private set; }

        /// <summary>Trigger zoom this frame: positive zooms in.</summary>
        public static float Zoom { get; private set; }

        public static int CursorSpeed
        {
            get => Mathf.Clamp(GamePrefs.GetInt(SpeedKey, 1), 0, GamepadMap.SpeedPresets.Length - 1);
            set
            {
                GamePrefs.SetInt(SpeedKey, Mathf.Clamp(value, 0, GamepadMap.SpeedPresets.Length - 1));
                GamePrefs.Save();
            }
        }

        private Vector2 _cursor;
        private float _held;
        private bool _leftDown, _rightDown;
        private bool _wasTyping;
        private int _warpedFrame = -1;
        private int _syntheticFrame = -100;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            ControllerActive = false;
            Pan = Vector2.zero;
            Zoom = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("[Gamepad]");
            DontDestroyOnLoad(go);
            go.AddComponent<GamepadInput>();
        }

        private void Awake()
        {
            Instance = this;
            InputBindings.PadDown = a => ActionDown(a);
            InputBindings.PadHeld = a => ActionHeld(a);
            InputBindings.PadHints = () => ControllerActive;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            ReleaseButtons();
            InputBindings.PadDown = null;
            InputBindings.PadHeld = null;
            InputBindings.PadHints = null;
            Instance = null;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ReleaseButtons(); // never leave a synthetic button held down
        }

        private bool _deckChecked;

        private void Update()
        {
            if (!_deckChecked && SteamService.Ready)
            {
                _deckChecked = true;
                if (SteamService.OnSteamDeck)
                {
                    ControllerActive = true; // the Deck starts on its controls
                    _cursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                }
            }
            var pad = Gamepad.current;
            Pan = Vector2.zero;
            Zoom = 0f;

            DetectDevice(pad);
            if (pad == null || !ControllerActive)
            {
                ReleaseButtons();
                return;
            }

            bool typing = InputBindings.TextEntryActive;
            if (typing && !_wasTyping) SteamService.ShowFloatingKeyboard();
            _wasTyping = typing;

            MoveCursor(pad.leftStick.ReadValue());
            if (Click(pad.buttonSouth, ref _leftDown, MouseLeftDown, MouseLeftUp) |
                Click(pad.buttonEast, ref _rightDown, MouseRightDown, MouseRightUp))
                _syntheticFrame = Time.frameCount;

            if (typing) return;
            Pan = GamepadMap.ApplyDeadZone(pad.rightStick.ReadValue());
            Zoom = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
        }

        // ---- device switching ------------------------------------------------------------------------

        private void DetectDevice(Gamepad pad)
        {
            if (pad != null && PadTouched(pad))
            {
                if (!ControllerActive)
                {
                    ControllerActive = true;
                    _cursor = Mouse.current != null ? Mouse.current.position.ReadValue()
                                                    : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                }
                return;
            }

            if (!ControllerActive) return;
            bool typed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame && !InputBindings.TextEntryActive;
            bool moved = Mouse.current != null && _warpedFrame < Time.frameCount - 1 &&
                         Mouse.current.delta.ReadValue().sqrMagnitude > 9f;
            // Our own synthetic clicks reach the mouse device a frame or two later; they are not the player.
            bool clicked = Mouse.current != null && !_leftDown && !_rightDown && Time.frameCount - _syntheticFrame > 3 &&
                           (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame);
            if (typed || moved || clicked)
            {
                ControllerActive = false;
                ReleaseButtons();
            }
        }

        private static bool PadTouched(Gamepad pad)
        {
            if (pad.leftStick.ReadValue().magnitude > 0.35f || pad.rightStick.ReadValue().magnitude > 0.35f) return true;
            if (pad.leftTrigger.ReadValue() > 0.3f || pad.rightTrigger.ReadValue() > 0.3f) return true;
            foreach (var c in pad.allControls)
            {
                if (c is ButtonControl b && !(c is StickControl) && b.wasPressedThisFrame) return true;
            }
            return false;
        }

        // ---- cursor and clicks -------------------------------------------------------------------------

        private void MoveCursor(Vector2 stick)
        {
            Vector2 step = GamepadMap.CursorStep(stick, _held, Screen.height, CursorSpeed, Time.unscaledDeltaTime);
            if (step == Vector2.zero)
            {
                _held = 0f;
                return;
            }
            _held += Time.unscaledDeltaTime;
            _cursor += step;
            _cursor.x = Mathf.Clamp(_cursor.x, 0f, Screen.width - 1f);
            _cursor.y = Mathf.Clamp(_cursor.y, 0f, Screen.height - 1f);
            Mouse.current?.WarpCursorPosition(_cursor);
            _warpedFrame = Time.frameCount;
        }

        private static bool Click(ButtonControl button, ref bool down, Action press, Action release)
        {
            if (button.wasPressedThisFrame && !down)
            {
                down = true;
                press();
                return true;
            }
            if (button.wasReleasedThisFrame && down)
            {
                down = false;
                release();
                return true;
            }
            return false;
        }

        private void ReleaseButtons()
        {
            if (_leftDown) { _leftDown = false; MouseLeftUp(); }
            if (_rightDown) { _rightDown = false; MouseRightUp(); }
        }

        // ---- actions ----------------------------------------------------------------------------------

        private static ButtonControl Control(Gamepad pad, PadButton b)
        {
            switch (b)
            {
                case PadButton.South: return pad.buttonSouth;
                case PadButton.East: return pad.buttonEast;
                case PadButton.West: return pad.buttonWest;
                case PadButton.North: return pad.buttonNorth;
                case PadButton.LeftShoulder: return pad.leftShoulder;
                case PadButton.RightShoulder: return pad.rightShoulder;
                case PadButton.DpadUp: return pad.dpad.up;
                case PadButton.DpadDown: return pad.dpad.down;
                case PadButton.DpadLeft: return pad.dpad.left;
                case PadButton.DpadRight: return pad.dpad.right;
                case PadButton.Start: return pad.startButton;
                case PadButton.Select: return pad.selectButton;
                case PadButton.LeftStickPress: return pad.leftStickButton;
                case PadButton.RightStickPress: return pad.rightStickButton;
                default: return null;
            }
        }

        private static bool ActionDown(GameAction action)
        {
            var pad = Gamepad.current;
            if (pad == null || !ControllerActive) return false;
            var c = Control(pad, GamepadMap.ButtonFor(action));
            return c != null && c.wasPressedThisFrame;
        }

        private static bool ActionHeld(GameAction action)
        {
            var pad = Gamepad.current;
            if (pad == null || !ControllerActive) return false;
            var c = Control(pad, GamepadMap.ButtonFor(action));
            return c != null && c.isPressed;
        }

        // ---- OS mouse buttons ----------------------------------------------------------------------------
        // IMGUI and the legacy Input class only see real OS mouse events, so the clicks are synthesised
        // at the OS level. Proton (Steam Deck) implements SendInput, so this covers the Deck as well.

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const uint InputMouse = 0;
        private const uint LeftDown = 0x0002, LeftUp = 0x0004, RightDown = 0x0008, RightUp = 0x0010;

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
            public MouseInput mi;
            // Pads the union to the size of the largest member (KEYBDINPUT/HARDWAREINPUT are smaller).
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, InputRecord[] inputs, int size);

        private static void Send(uint flags)
        {
            var input = new[] { new InputRecord { type = InputMouse, mi = new MouseInput { dwFlags = flags } } };
            SendInput(1, input, Marshal.SizeOf(typeof(InputRecord)));
        }

        private static void MouseLeftDown() => Send(LeftDown);
        private static void MouseLeftUp() => Send(LeftUp);
        private static void MouseRightDown() => Send(RightDown);
        private static void MouseRightUp() => Send(RightUp);
#else
        private static void MouseLeftDown() { }
        private static void MouseLeftUp() { }
        private static void MouseRightDown() { }
        private static void MouseRightUp() { }
#endif
    }
}
