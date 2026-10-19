using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Drives a virtual gamepad through the real <see cref="GamepadInput"/>. A and B are left
    /// alone: they send genuine OS clicks, which would land on whatever is under the cursor.
    /// </summary>
    public class GamepadPlayTests
    {
        private Gamepad _pad;

        [SetUp]
        public void SetUp() => _pad = InputSystem.AddDevice<Gamepad>();

        [TearDown]
        public void TearDown()
        {
            if (_pad != null) InputSystem.RemoveDevice(_pad);
            _pad = null;
        }

        [UnityTest]
        public IEnumerator TouchingThePad_EntersControllerMode_AndHintsNameButtons()
        {
            Assert.IsNotNull(GamepadInput.Instance, "GamepadInput boots itself before the first scene");

            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.West));
            yield return null;
            yield return null;
            Assert.IsTrue(GamepadInput.ControllerActive);
            Assert.AreEqual("X", InputBindings.Label(GameAction.ToolFlag));

            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator BoundButton_FiresItsAction()
        {
            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            bool fired = false;
            for (int i = 0; i < 4 && !fired; i++)
            {
                yield return null;
                fired |= InputBindings.Down(GameAction.SpeedUp);
            }
            Assert.IsTrue(fired, "RB raises the game speed");
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator RightStick_Pans_AndTriggers_Zoom()
        {
            InputSystem.QueueStateEvent(_pad, new GamepadState { rightStick = new Vector2(1f, 0f), rightTrigger = 1f });
            bool panned = false, zoomed = false;
            for (int i = 0; i < 4; i++)
            {
                yield return null;
                panned |= GamepadInput.Pan.x > 0.5f;
                zoomed |= GamepadInput.Zoom > 0.5f;
            }
            Assert.IsTrue(panned, "right stick pans the camera");
            Assert.IsTrue(zoomed, "right trigger zooms in");
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
        }
    }
}
