using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>Scripted camera beat curves (pure math).</summary>
    public class CameraBeatTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void Easing_HitsItsEndpoints_AndIsMonotonic()
        {
            Assert.AreEqual(0f, CameraBeatMath.EaseInOutCubic(0f), Eps);
            Assert.AreEqual(0.5f, CameraBeatMath.EaseInOutCubic(0.5f), Eps);
            Assert.AreEqual(1f, CameraBeatMath.EaseInOutCubic(1f), Eps);
            Assert.AreEqual(0f, CameraBeatMath.EaseInOutCubic(-3f), Eps);
            Assert.AreEqual(1f, CameraBeatMath.EaseInOutCubic(7f), Eps);
            float prev = -1f;
            for (int i = 0; i <= 100; i++)
            {
                float v = CameraBeatMath.EaseInOutCubic(i / 100f);
                Assert.GreaterOrEqual(v, prev);
                prev = v;
            }
        }

        [Test]
        public void Envelope_StartsAndEndsAtThePlayersPose()
        {
            Assert.AreEqual(0f, CameraBeatMath.Envelope(0f, 1f, 2f, 1f), Eps);
            Assert.AreEqual(0.5f, CameraBeatMath.Envelope(0.5f, 1f, 2f, 1f), Eps);
            Assert.AreEqual(1f, CameraBeatMath.Envelope(1f, 1f, 2f, 1f), Eps);
            Assert.AreEqual(1f, CameraBeatMath.Envelope(3f, 1f, 2f, 1f), Eps);
            Assert.AreEqual(0.5f, CameraBeatMath.Envelope(3.5f, 1f, 2f, 1f), Eps);
            Assert.AreEqual(0f, CameraBeatMath.Envelope(4f, 1f, 2f, 1f), Eps);
            Assert.AreEqual(0f, CameraBeatMath.Envelope(99f, 1f, 2f, 1f), Eps);
        }

        [Test]
        public void LaunchBeat_FramesThePadThenFollowsTheCraftUpThenReturns()
        {
            var start = new Vector3(10f, 0f, 10f);
            var pad = new Vector3(40f, 0f, 30f);
            const float startZoom = 30f;

            // t = 0: exactly the player's view.
            AssertNear(start, CameraBeatMath.LaunchFocus(0f, start, pad));
            Assert.AreEqual(startZoom, CameraBeatMath.LaunchZoom(0f, startZoom), Eps);

            // End of framing: on the pad, craft not yet moving, framing zoom.
            float framed = CameraBeatMath.LaunchFrameSeconds;
            Assert.AreEqual(0f, CameraBeatMath.CraftHeight(framed), Eps);
            AssertNear(pad, CameraBeatMath.LaunchFocus(framed, start, pad));
            Assert.AreEqual(CameraBeatMath.LaunchFrameZoom, CameraBeatMath.LaunchZoom(framed, startZoom), Eps);

            // Ascent: the craft climbs, faster and faster, and the camera rides with it.
            float apexAt = framed + CameraBeatMath.LaunchAscentSeconds;
            float mid = framed + CameraBeatMath.LaunchAscentSeconds * 0.5f;
            Assert.AreEqual(CameraBeatMath.LaunchApex * 0.25f, CameraBeatMath.CraftHeight(mid), 0.01f);
            Assert.AreEqual(CameraBeatMath.LaunchApex, CameraBeatMath.CraftHeight(apexAt), Eps);
            Assert.AreEqual(CameraBeatMath.CraftHeight(mid), CameraBeatMath.LaunchFocus(mid, start, pad).y, Eps);
            Assert.Greater(CameraBeatMath.LaunchZoom(apexAt, startZoom), CameraBeatMath.LaunchFrameZoom);

            // End: home again.
            float end = CameraBeatMath.LaunchDuration;
            AssertNear(start, CameraBeatMath.LaunchFocus(end, start, pad));
            Assert.AreEqual(startZoom, CameraBeatMath.LaunchZoom(end, startZoom), Eps);
            Assert.AreEqual(4.6f, CameraBeatMath.LaunchDuration, 0.001f);
        }

        [Test]
        public void VictoryBeat_PullsBackAndOrbits_ThenUnwindsToZero()
        {
            var start = new Vector3(5f, 0f, 5f);
            var commons = new Vector3(30f, 0f, 30f);
            const float startZoom = 12f;
            const float maxZoom = 52f;

            Assert.AreEqual(0f, CameraBeatMath.VictoryYaw(0f), Eps);
            Assert.AreEqual(startZoom, CameraBeatMath.VictoryZoom(0f, startZoom, maxZoom), Eps);
            AssertNear(start, CameraBeatMath.VictoryFocus(0f, start, commons));

            float held = CameraBeatMath.VictoryInSeconds + CameraBeatMath.VictoryHoldSeconds;
            AssertNear(commons, CameraBeatMath.VictoryFocus(held, start, commons));
            Assert.AreEqual(CameraBeatMath.VictoryOrbitDegrees, CameraBeatMath.VictoryYaw(held), Eps);
            Assert.AreEqual(Mathf.Max(startZoom * CameraBeatMath.VictoryPullBack, CameraBeatMath.VictoryMinZoom),
                CameraBeatMath.VictoryZoom(held, startZoom, maxZoom), Eps);

            float end = CameraBeatMath.VictoryDuration;
            Assert.AreEqual(0f, CameraBeatMath.VictoryYaw(end), Eps);
            Assert.AreEqual(startZoom, CameraBeatMath.VictoryZoom(end, startZoom, maxZoom), Eps);
            AssertNear(start, CameraBeatMath.VictoryFocus(end, start, commons));

            // Yaw never overshoots the orbit.
            for (float t = 0f; t <= end; t += 0.05f)
                Assert.LessOrEqual(CameraBeatMath.VictoryYaw(t), CameraBeatMath.VictoryOrbitDegrees + Eps);
        }

        [Test]
        public void VictoryPullBack_NeverExceedsTheRigsMaxZoom()
        {
            float held = CameraBeatMath.VictoryInSeconds + 1f;
            Assert.AreEqual(52f, CameraBeatMath.VictoryZoom(held, 48f, 52f), Eps);
            // Already zoomed out past max (diorama or setup): hold, never zoom in.
            Assert.AreEqual(60f, CameraBeatMath.VictoryZoom(held, 60f, 52f), Eps);
        }

        [Test]
        public void Cancel_IgnoresTheStartingClick_ThenAnyInputWins()
        {
            Assert.IsFalse(CameraBeatMath.ShouldCancel(0.1f, true));
            Assert.IsFalse(CameraBeatMath.ShouldCancel(2f, false));
            Assert.IsTrue(CameraBeatMath.ShouldCancel(CameraBeatMath.InputGrace, true));
            Assert.IsTrue(CameraBeatMath.ShouldCancel(3f, true));
        }

        [Test]
        public void NoBeatsSwitch_IsFoundInAnyCase()
        {
            Assert.IsFalse(CameraBeatMath.ArgsDisableBeats(null));
            Assert.IsFalse(CameraBeatMath.ArgsDisableBeats(new[] { "SolarMajesty.exe", "-saveDir", "x" }));
            Assert.IsTrue(CameraBeatMath.ArgsDisableBeats(new[] { "SolarMajesty.exe", "-nobeats" }));
            Assert.IsTrue(CameraBeatMath.ArgsDisableBeats(new[] { "SolarMajesty.exe", "-NoBeats", "-screen-width", "1600" }));
            Assert.IsFalse(CameraBeatMath.ArgsDisableBeats(new[] { "-nobeatsx" }), "whole argument only");
        }

        [Test]
        public void InputBlock_CoversTheBeat_TheSkipFrames_AndAHeldSkipClick()
        {
            Assert.IsTrue(CameraBeatMath.BlocksInput(true, 100, -1, false), "while the beat plays");
            // The beat ended on frame 100 (block through 101).
            Assert.IsTrue(CameraBeatMath.BlocksInput(false, 100, 101, false), "the frame the skip lands");
            Assert.IsTrue(CameraBeatMath.BlocksInput(false, 101, 101, false), "and the next");
            Assert.IsFalse(CameraBeatMath.BlocksInput(false, 102, 101, false), "then the world has input back");
            Assert.IsTrue(CameraBeatMath.BlocksInput(false, 140, 101, true), "until the skip click is released");
        }

        [Test]
        public void Letterbox_EasesIn_ThenHolds()
        {
            Assert.AreEqual(0f, CameraBeatMath.LetterboxHeight(0f, 1000f), Eps);
            Assert.AreEqual(50f, CameraBeatMath.LetterboxHeight(CameraBeatMath.LetterboxInSeconds * 0.5f, 1000f), 0.01f);
            Assert.AreEqual(100f, CameraBeatMath.LetterboxHeight(CameraBeatMath.LetterboxInSeconds, 1000f), 0.01f);
            Assert.AreEqual(100f, CameraBeatMath.LetterboxHeight(30f, 1000f), 0.01f);
        }

        [Test]
        public void Captions_NameTheNextWorld()
        {
            Assert.AreEqual("LIFTOFF", CameraBeatMath.LaunchCaption(null));
            Assert.AreEqual("LIFTOFF", CameraBeatMath.LaunchCaption(""));
            Assert.AreEqual("LIFTOFF  ·  BOUND FOR MARS", CameraBeatMath.LaunchCaption("Mars"));
            StringAssert.Contains("SKIP", CameraBeatMath.SkipHint(false));
            StringAssert.Contains("BUTTON", CameraBeatMath.SkipHint(true));
        }

        [Test]
        public void Director_AtRest_NeitherHidesTheHud_NorBlocksInput()
        {
            Assert.IsFalse(CameraDirector.Active);
            Assert.IsFalse(CameraDirector.HidesHud);
            Assert.IsFalse(CameraDirector.BlocksInput);
        }

        [Test]
        public void Director_EnabledOverride_WinsOverTheCommandLine()
        {
            bool? prior = CameraDirector.EnabledOverride;
            try
            {
                CameraDirector.EnabledOverride = false;
                Assert.IsFalse(CameraDirector.Enabled);
                CameraDirector.EnabledOverride = true;
                Assert.IsTrue(CameraDirector.Enabled);
            }
            finally
            {
                CameraDirector.EnabledOverride = prior;
            }
        }

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.001f);
            Assert.AreEqual(expected.y, actual.y, 0.001f);
            Assert.AreEqual(expected.z, actual.z, 0.001f);
        }
    }
}
