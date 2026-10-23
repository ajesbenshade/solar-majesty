using System;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Controller input through the legacy Input Manager (ProjectSettings/InputManager.asset,
    /// the <c>SM_Pad*</c> axes; layout in Docs/CONTROLS.md). Safe with no controller and with
    /// an Input Manager that lacks the axes: the first read probes every name once, and a
    /// missing one switches controller support off for the session instead of throwing every frame.
    /// </summary>
    public static class GamepadInput
    {
        public const string PrefsKey = "SM_Set_Gamepad";

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

        private static readonly string[] Axes = { LeftX, LeftY, RightX, RightY, LeftTrigger, RightTrigger, DpadX, DpadY };
        private static readonly string[] Buttons = { ButtonA, ButtonB, ButtonX, ButtonY, ButtonLB, ButtonRB, ButtonBack, ButtonStart };

        private static bool _probed;
        private static bool _axesOk;
        private static bool _prefLoaded;
        private static bool _enabled = true;
        private static float _nextDeviceCheck;
        private static bool _connected;

        /// <summary>Player preference (Settings → CONTROLLER). On by default.</summary>
        public static bool Enabled
        {
            get
            {
                if (!_prefLoaded)
                {
                    _prefLoaded = true;
                    _enabled = PlayerPrefs.GetInt(PrefsKey, 1) == 1;
                }
                return _enabled;
            }
            set
            {
                _prefLoaded = true;
                _enabled = value;
                PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>True when every SM_Pad axis and button exists in the Input Manager.</summary>
        public static bool AxesAvailable
        {
            get
            {
                Probe();
                return _axesOk;
            }
        }

        /// <summary>A joystick is plugged in. Re-checked at most once a second (the OS query allocates).</summary>
        public static bool Connected
        {
            get
            {
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
        public static bool Active => !Application.isBatchMode && Enabled && AxesAvailable && Connected;

        /// <summary>Left stick, or the D-pad when the stick is at rest. Shaped; x right, y forward.</summary>
        public static Vector2 Pan()
        {
            if (!Active) return new Vector2(0f, 0f);
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

        public static bool Pressed(string button)
        {
            if (!Active) return false;
            try
            {
                return Input.GetButtonDown(button);
            }
            catch (ArgumentException)
            {
                return false;
            }
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
                if (Pressed(Buttons[i])) return true;
            }
            return false;
        }

        private static float Raw(string axis)
        {
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
    }
}
