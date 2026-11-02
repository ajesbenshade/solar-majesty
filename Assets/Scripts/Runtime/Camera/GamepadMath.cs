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

        // ---- pointer mode (virtual cursor) ----------------------------------------------------------

        /// <summary>Pointer speed presets, as a fraction of the screen height per second at full tilt.</summary>
        public static readonly float[] PointerSpeeds = { 0.55f, 0.85f, 1.25f };
        public static readonly string[] PointerSpeedNames = { "SLOW", "NORMAL", "FAST" };

        /// <summary>
        /// Pixels the pointer moves this frame. The stick curve gives fine control for small tilts,
        /// and a short ramp while the stick is held lets a long move cross the screen quickly.
        /// </summary>
        public static Vector2 PointerStep(float x, float y, float heldSeconds, float screenHeight, int speedPreset, float dt)
        {
            Vector2 v = Stick(x, y, StickDeadzone, 2.2f);
            float m = Mathf.Sqrt(v.x * v.x + v.y * v.y);
            if (m <= 0f || screenHeight <= 0f || dt <= 0f) return new Vector2(0f, 0f);
            float preset = PointerSpeeds[Mathf.Clamp(speedPreset, 0, PointerSpeeds.Length - 1)];
            float ramp = Mathf.Lerp(0.55f, 1.25f, Mathf.Clamp01(heldSeconds / 0.6f));
            float scale = preset * screenHeight * ramp * dt;
            return new Vector2(v.x * scale, v.y * scale);
        }

        /// <summary>
        /// Camera pan from a pointer held against the screen edge while the stick keeps pushing
        /// outward, for when Settings → EDGE SCROLL is off. Screen coordinates, origin bottom left.
        /// </summary>
        public static Vector2 EdgePush(Vector2 pointer, float stickX, float stickY, float width, float height, float margin = 2f)
        {
            Vector2 s = Stick(stickX, stickY);
            float h = 0f, v = 0f;
            if (pointer.x <= margin && s.x < 0f) h = s.x;
            else if (pointer.x >= width - 1f - margin && s.x > 0f) h = s.x;
            if (pointer.y <= margin && s.y < 0f) v = s.y;
            else if (pointer.y >= height - 1f - margin && s.y > 0f) v = s.y;
            return new Vector2(h, v);
        }

        /// <summary>
        /// The controller button shown in hints for an action, or null when it has none in this
        /// mode (the hint then names the key). The D-pad only carries actions in pointer mode;
        /// in camera mode it pans.
        /// </summary>
        public static string ButtonLabel(GameAction action, bool pointerMode)
        {
            switch (action)
            {
                case GameAction.TogglePause: return "X";
                case GameAction.SpeedDown: return "LB";
                case GameAction.SpeedUp: return "RB";
                case GameAction.ToolTech: return "Y";
                case GameAction.JumpToAlert: return "L3";
                case GameAction.CycleOverlay: return pointerMode ? "D-pad Up" : null;
                case GameAction.CycleTool: return pointerMode ? "D-pad Down" : null;
                case GameAction.BountyDown: return pointerMode ? "D-pad Left" : null;
                case GameAction.BountyUp: return pointerMode ? "D-pad Right" : null;
                default: return null;
            }
        }

        /// <summary>True once a raw stick is clearly off centre (used to hand control back from a camera beat).</summary>
        public static bool IsDeflected(float x, float y, float deadzone = StickDeadzone) =>
            x * x + y * y > deadzone * deadzone;
    }
}
