using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// One frame of "did the player ask to skip?" Split out so tests can pass a sample
    /// without a keyboard, mouse, or pad.
    /// </summary>
    public readonly struct IntroInputSample
    {
        public readonly bool AnyKeyDown;
        public readonly bool MouseButtonDown;
        public readonly bool GamepadButtonDown;

        public IntroInputSample(bool anyKeyDown, bool mouseButtonDown, bool gamepadButtonDown)
        {
            AnyKeyDown = anyKeyDown;
            MouseButtonDown = mouseButtonDown;
            GamepadButtonDown = gamepadButtonDown;
        }
    }

    public static class IntroSkip
    {
        public static bool ShouldSkip(IntroInputSample sample) =>
            sample.AnyKeyDown || sample.MouseButtonDown || sample.GamepadButtonDown;

        public static IntroInputSample Sample()
        {
            bool mouse = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
            bool pad = false;
            for (int joy = 0; joy < 4 && !pad; joy++)
            {
                int first = (int)KeyCode.Joystick1Button0 + joy * 20;
                for (int button = 0; button < 16; button++)
                {
                    if (Input.GetKeyDown((KeyCode)(first + button)))
                    {
                        pad = true;
                        break;
                    }
                }
            }

            return new IntroInputSample(Input.anyKeyDown, mouse, pad);
        }
    }
}
