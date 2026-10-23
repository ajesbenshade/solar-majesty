using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Stick and trigger shaping for controller camera control. Pure, so it is unit tested.
    /// A radial deadzone keeps a resting stick from drifting the map; the remaining travel is
    /// rescaled to 0..1 and bent by a power curve so small tilts give fine control and a full
    /// tilt gives full speed.
    /// </summary>
    public static class GamepadMath
    {
        public const float StickDeadzone = 0.2f;
        public const float TriggerDeadzone = 0.08f;
        public const float CurveExponent = 1.8f;

        /// <summary>Shape one axis: deadzone, rescale, curve. Sign is kept. Output in -1..1.</summary>
        public static float Axis(float value, float deadzone = StickDeadzone, float exponent = CurveExponent)
        {
            float mag = Mathf.Abs(value);
            if (mag <= deadzone || deadzone >= 1f) return 0f;
            float t = Mathf.Clamp01((mag - deadzone) / (1f - deadzone));
            return Mathf.Pow(t, Mathf.Max(0.01f, exponent)) * (value < 0f ? -1f : 1f);
        }

        /// <summary>
        /// Shape a stick as a vector: radial deadzone on its length, then the curve on the length,
        /// direction preserved. Diagonals are clamped to length 1, so they are not faster.
        /// </summary>
        public static Vector2 Stick(float x, float y, float deadzone = StickDeadzone, float exponent = CurveExponent)
        {
            float mag = Mathf.Sqrt(x * x + y * y);
            if (mag <= deadzone || mag <= 0.0001f || deadzone >= 1f) return new Vector2(0f, 0f);
            float shaped = Axis(Mathf.Min(mag, 1f), deadzone, exponent);
            float scale = shaped / mag;
            return new Vector2(x * scale, y * scale);
        }

        /// <summary>
        /// Zoom drive in -1..1 where +1 zooms out (the same sign as the Q key). Right trigger and
        /// right stick up zoom in; left trigger and stick down zoom out.
        /// </summary>
        public static float Zoom(float rightStickY, float leftTrigger, float rightTrigger)
        {
            float stick = -Axis(rightStickY);
            float triggers = Axis(Mathf.Clamp01(leftTrigger), TriggerDeadzone, 1.4f)
                             - Axis(Mathf.Clamp01(rightTrigger), TriggerDeadzone, 1.4f);
            return Mathf.Clamp(stick + triggers, -1f, 1f);
        }

        /// <summary>Speed step from the shoulder buttons: -1, 0 or +1. Both at once cancel out.</summary>
        public static int SpeedStep(bool leftBumperDown, bool rightBumperDown)
        {
            if (leftBumperDown == rightBumperDown) return 0;
            return rightBumperDown ? 1 : -1;
        }

        /// <summary>True once a raw stick is clearly off centre (used to hand control back from a camera beat).</summary>
        public static bool IsDeflected(float x, float y, float deadzone = StickDeadzone) =>
            x * x + y * y > deadzone * deadzone;
    }
}
