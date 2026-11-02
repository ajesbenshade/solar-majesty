using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Drives a virtual pad through the real <see cref="GamepadInput"/> and its per-frame runner.
    /// The test scene has no GameLoop, which reads as "outside play", so pointer mode is on. Pointer
    /// moves and clicks land on the virtual pad's record instead of the OS cursor.
    /// </summary>
    public class GamepadPlayTests
    {
        private GamepadInput.VirtualPad _pad;

        [SetUp]
        public void SetUp()
        {
            GamepadInput.ResetModeForTests();
            _pad = new GamepadInput.VirtualPad();
            GamepadInput.Virtual = _pad;
        }

        [TearDown]
        public void TearDown()
        {
            GamepadInput.Virtual = null;
            GamepadInput.ResetModeForTests();
            _pad = null;
        }

        [UnityTest]
        public IEnumerator TouchingThePad_EntersControllerMode_AndHintsNameButtons()
        {
            Assert.IsNotNull(InputBindings.PadLabel, "GamepadInput hooks InputBindings before the first scene");
            yield return null;
            Assert.IsFalse(GamepadInput.ControllerActive, "nothing touched yet");
            Assert.AreEqual(KeyHints.Fill("{TogglePause}", InputBindings.Key), KeyHints.Fill("{TogglePause}"));

            _pad.Press(GamepadInput.ButtonX);
            yield return null;
            _pad.Release(GamepadInput.ButtonX);
            yield return null;
            Assert.IsTrue(GamepadInput.ControllerActive);
            Assert.IsTrue(GamepadInput.PointerMode, "no game in play: menus need the pointer");
            Assert.AreEqual("X", KeyHints.Fill("{TogglePause}"));
            Assert.AreEqual("D-pad Up", InputBindings.PadHint(GameAction.CycleOverlay));

            GamepadInput.Virtual = null; // unplugged
            yield return null;
            Assert.IsFalse(GamepadInput.ControllerActive, "controller mode ends without a pad");
            Assert.AreEqual(KeyHints.Fill("{TogglePause}", InputBindings.Key), KeyHints.Fill("{TogglePause}"));
        }

        [UnityTest]
        public IEnumerator PointerMode_LeftStickMovesThePointer_AndAClicks()
        {
            _pad.SetAxis(GamepadInput.LeftX, 1f);
            for (int i = 0; i < 10; i++) yield return null;
            _pad.SetAxis(GamepadInput.LeftX, 0f);

            Assert.IsTrue(GamepadInput.PointerMode);
            if (Screen.height > 0 && Screen.width > 1)
            {
                Assert.Greater(_pad.Warps.Count, 3, "the stick warps the pointer every frame");
                Assert.Greater(_pad.Warps[_pad.Warps.Count - 1].x, _pad.Warps[0].x, "stick right moves it right");
            }

            _pad.Press(GamepadInput.ButtonA);
            yield return null;
            Assert.IsFalse(GamepadInput.Pressed(GamepadInput.ButtonA), "A belongs to the pointer, not the camera");
            yield return null;
            CollectionAssert.AreEqual(new[] { "left down" }, _pad.Clicks, "held A keeps the button down");

            _pad.Release(GamepadInput.ButtonA);
            yield return null;
            CollectionAssert.AreEqual(new[] { "left down", "left up" }, _pad.Clicks);

            _pad.Press(GamepadInput.ButtonR3);
            yield return null;
            _pad.Release(GamepadInput.ButtonR3);
            yield return null;
            CollectionAssert.AreEqual(new[] { "left down", "left up", "right down", "right up" }, _pad.Clicks);
        }

        [UnityTest]
        public IEnumerator PadButtons_FireTheirActionsThroughInputBindings()
        {
            _pad.Press(GamepadInput.ButtonL3);
            yield return null;
            Assert.IsTrue(InputBindings.Down(GameAction.JumpToAlert), "L3 jumps to the alert");
            _pad.Release(GamepadInput.ButtonL3);
            yield return null;
            Assert.IsFalse(InputBindings.Down(GameAction.JumpToAlert), "once per press");

            // D-pad up while in pointer mode: one CycleOverlay per press, however long it is held.
            _pad.SetAxis(GamepadInput.DpadY, 1f);
            int fired = 0;
            for (int i = 0; i < 6; i++)
            {
                yield return null;
                if (InputBindings.Down(GameAction.CycleOverlay)) fired++;
            }
            _pad.SetAxis(GamepadInput.DpadY, 0f);
            yield return null;
            Assert.AreEqual(1, fired);
            Assert.AreEqual(Vector2.zero, GamepadInput.Pan(), "in pointer mode the D-pad does not pan");

            InputBindings.TextEntryActive = true;
            try
            {
                _pad.Press(GamepadInput.ButtonL3);
                yield return null;
                Assert.IsFalse(InputBindings.Down(GameAction.JumpToAlert), "typing silences pad actions too");
            }
            finally
            {
                InputBindings.TextEntryActive = false;
            }
        }
    }
}
