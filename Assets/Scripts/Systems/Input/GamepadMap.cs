using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Gamepad buttons the game uses, independent of the Input System package.</summary>
    public enum PadButton
    {
        None,
        South, East, West, North,
        LeftShoulder, RightShoulder,
        DpadUp, DpadDown, DpadLeft, DpadRight,
        Start, Select,
        LeftStickPress, RightStickPress
    }

    /// <summary>
    /// The controller layout and the maths behind the virtual cursor. Pure so it is unit-tested;
    /// <c>GamepadInput</c> (Runtime) reads the pad and feeds it in.
    ///
    /// The game is mouse-driven (an IMGUI HUD, clicks on the map), so the controller drives the real
    /// cursor: left stick moves it, A and B are left and right clicks. Everything with a hotkey also
    /// gets a button here, so the common loop never needs the cursor at all.
    /// </summary>
    public static class GamepadMap
    {
        /// <summary>Clicks are not actions; these two buttons are always the mouse buttons.</summary>
        public const PadButton LeftClick = PadButton.South;
        public const PadButton RightClick = PadButton.East;

        private static readonly Dictionary<GameAction, PadButton> Defaults = new Dictionary<GameAction, PadButton>
        {
            { GameAction.ToolFlag, PadButton.West },
            { GameAction.ToolBuild, PadButton.North },
            { GameAction.ToolTech, PadButton.Select },
            { GameAction.Cancel, PadButton.Start },
            { GameAction.SpeedDown, PadButton.LeftShoulder },
            { GameAction.SpeedUp, PadButton.RightShoulder },
            { GameAction.TogglePause, PadButton.RightStickPress },
            { GameAction.JumpToAlert, PadButton.LeftStickPress },
            { GameAction.CycleOverlay, PadButton.DpadUp },
            { GameAction.CycleTool, PadButton.DpadDown },
            { GameAction.BountyDown, PadButton.DpadLeft },
            { GameAction.BountyUp, PadButton.DpadRight },
        };

        public static IReadOnlyDictionary<GameAction, PadButton> Bindings => Defaults;

        public static PadButton ButtonFor(GameAction action) =>
            Defaults.TryGetValue(action, out PadButton b) ? b : PadButton.None;

        /// <summary>Xbox-style names, which is also what the Steam Deck prints on its buttons.</summary>
        public static string Label(PadButton b)
        {
            switch (b)
            {
                case PadButton.South: return "A";
                case PadButton.East: return "B";
                case PadButton.West: return "X";
                case PadButton.North: return "Y";
                case PadButton.LeftShoulder: return "LB";
                case PadButton.RightShoulder: return "RB";
                case PadButton.DpadUp: return "D-pad ↑";
                case PadButton.DpadDown: return "D-pad ↓";
                case PadButton.DpadLeft: return "D-pad ←";
                case PadButton.DpadRight: return "D-pad →";
                case PadButton.Start: return "Menu";
                case PadButton.Select: return "View";
                case PadButton.LeftStickPress: return "L3";
                case PadButton.RightStickPress: return "R3";
                default: return "—";
            }
        }

        /// <summary>One-line layout summary for the settings screen.</summary>
        public static string Summary => Loc.S(
            "Left stick: cursor · A: click · B: cancel / right-click · Right stick: pan · LT/RT: zoom · X: flags · Y: build · LB/RB: speed · R3: hold · Menu: pause · View: research · D-pad: overlay, tools, bounty");

        // ---- virtual cursor -------------------------------------------------------------------------

        /// <summary>Stick values below this are noise.</summary>
        public const float DeadZone = 0.18f;

        /// <summary>Cursor speed presets, as a fraction of the screen height per second at full tilt.</summary>
        public static readonly float[] SpeedPresets = { 0.55f, 0.85f, 1.25f };
        public static readonly string[] SpeedNames = { "SLOW", "NORMAL", "FAST" };

        /// <summary>A speed preset's name in the current language.</summary>
        public static string SpeedName(int preset) =>
            preset <= 0 ? Loc.S("SLOW") : preset >= SpeedPresets.Length - 1 ? Loc.S("FAST") : Loc.S("NORMAL");

        /// <summary>Radial dead zone, rescaled so movement starts smoothly at the edge of it.</summary>
        public static Vector2 ApplyDeadZone(Vector2 stick)
        {
            float m = stick.magnitude;
            if (m <= DeadZone) return Vector2.zero;
            float scaled = Mathf.Clamp01((m - DeadZone) / (1f - DeadZone));
            return stick / m * scaled;
        }

        /// <summary>
        /// Pixels the cursor moves this frame. A curve on deflection gives fine control for small
        /// tilts, and a short ramp while the stick is held lets long moves cross the screen quickly.
        /// </summary>
        public static Vector2 CursorStep(Vector2 stick, float heldSeconds, float screenHeight, int speedPreset, float dt)
        {
            Vector2 v = ApplyDeadZone(stick);
            float m = v.magnitude;
            if (m <= 0f) return Vector2.zero;
            float preset = SpeedPresets[Mathf.Clamp(speedPreset, 0, SpeedPresets.Length - 1)];
            float curve = Mathf.Pow(m, 2.2f);
            float ramp = Mathf.Lerp(0.55f, 1.25f, Mathf.Clamp01(heldSeconds / 0.6f));
            float pixelsPerSecond = preset * screenHeight * curve * ramp;
            return v / m * pixelsPerSecond * dt;
        }
    }
}
