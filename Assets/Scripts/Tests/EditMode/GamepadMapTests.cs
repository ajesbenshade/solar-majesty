using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class GamepadMapTests
    {
        [TearDown]
        public void TearDown() => InputBindings.PadHints = null;

        [Test]
        public void EachButton_DoesOneThing()
        {
            var seen = new HashSet<PadButton>();
            foreach (var kv in GamepadMap.Bindings)
            {
                Assert.That(kv.Value, Is.Not.EqualTo(PadButton.None), kv.Key.ToString());
                Assert.That(seen.Add(kv.Value), Is.True, "two actions on " + kv.Value);
                Assert.That(kv.Value, Is.Not.EqualTo(GamepadMap.LeftClick), "A is always the left click");
                Assert.That(kv.Value, Is.Not.EqualTo(GamepadMap.RightClick), "B is always the right click");
            }
        }

        [Test]
        public void CoreLoop_IsReachableWithoutTheCursor()
        {
            foreach (var a in new[] { GameAction.ToolFlag, GameAction.ToolBuild, GameAction.Cancel,
                         GameAction.SpeedUp, GameAction.SpeedDown, GameAction.TogglePause, GameAction.ToolTech })
                Assert.That(GamepadMap.ButtonFor(a), Is.Not.EqualTo(PadButton.None), a.ToString());
        }

        [Test]
        public void Hints_NameButtons_WhileTheControllerIsInUse()
        {
            InputBindings.PadHints = () => true;
            Assert.That(InputBindings.Label(GameAction.ToolFlag), Is.EqualTo("X"));
            Assert.That(InputBindings.Label(GameAction.Cancel), Is.EqualTo("Menu"));
            InputBindings.PadHints = () => false;
            Assert.That(InputBindings.Label(GameAction.ToolFlag), Is.EqualTo(InputBindings.KeyLabel(InputBindings.Key(GameAction.ToolFlag))));
        }

        [Test]
        public void ActionsWithoutAButton_KeepTheirKeyHint()
        {
            InputBindings.PadHints = () => true;
            Assert.That(GamepadMap.ButtonFor(GameAction.PanUp), Is.EqualTo(PadButton.None));
            Assert.That(InputBindings.Label(GameAction.PanUp), Is.EqualTo(InputBindings.KeyLabel(InputBindings.Key(GameAction.PanUp))));
        }

        [Test]
        public void DeadZone_SwallowsDrift_AndRescalesTheRest()
        {
            Assert.That(GamepadMap.ApplyDeadZone(new Vector2(0.1f, 0.1f)), Is.EqualTo(Vector2.zero));
            Assert.That(GamepadMap.ApplyDeadZone(Vector2.right).magnitude, Is.EqualTo(1f).Within(1e-5f));
            float justPast = GamepadMap.ApplyDeadZone(new Vector2(GamepadMap.DeadZone + 0.01f, 0f)).magnitude;
            Assert.That(justPast, Is.LessThan(0.05f), "movement starts from zero, not with a jump");
        }

        [Test]
        public void CursorStep_IsGentleForSmallTilts_AndCrossesTheScreenAtFullTilt()
        {
            float h = 800f;
            float small = GamepadMap.CursorStep(new Vector2(0.4f, 0f), 0f, h, 1, 1f / 60f).magnitude;
            float full = GamepadMap.CursorStep(Vector2.right, 1f, h, 1, 1f / 60f).magnitude;
            Assert.That(small, Is.LessThan(full * 0.15f));
            float secondsToCross = 1280f / (full * 60f);
            Assert.That(secondsToCross, Is.InRange(0.8f, 3f), "full tilt crosses a Deck screen in a second or two");
            Assert.That(GamepadMap.CursorStep(Vector2.zero, 5f, h, 1, 0.016f), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void FasterPreset_MovesFurther()
        {
            float slow = GamepadMap.CursorStep(Vector2.up, 1f, 1080f, 0, 0.016f).magnitude;
            float fast = GamepadMap.CursorStep(Vector2.up, 1f, 1080f, 2, 0.016f).magnitude;
            Assert.That(fast, Is.GreaterThan(slow * 2f));
        }

        [Test]
        public void Handheld_KeepsTheHudAtFullSize_At1280x800()
        {
            Assert.That(HudScaleMath.AutoForHeight(800, false), Is.EqualTo(0.75f).Within(0.001f), "desktop rule");
            Assert.That(HudScaleMath.AutoForHeight(800, true), Is.EqualTo(1f).Within(0.001f), "9 px text stays 9 px");
            Assert.That(HudScaleMath.ClampToFit(1f, 1280, 800), Is.EqualTo(1f).Within(0.001f), "and it fits the Deck");
            Assert.That(HudScaleMath.AutoForHeight(1440, true), Is.EqualTo(HudScaleMath.AutoForHeight(1440, false)),
                "big handheld screens scale as usual");
        }

        [TestCase("AMD Custom GPU 0405")]
        [TestCase("AMD Custom GPU 0932")]
        public void SteamDeck_GetsMedium(string gpu)
        {
            Assert.That(QualityAutoDetect.Pick(gpu, 8192, 15000, 6), Is.EqualTo(QualityAutoDetect.Medium));
        }
    }
}
