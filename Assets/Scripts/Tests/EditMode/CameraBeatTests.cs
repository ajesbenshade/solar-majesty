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

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.001f);
            Assert.AreEqual(expected.y, actual.y, 0.001f);
            Assert.AreEqual(expected.z, actual.z, 0.001f);
        }
    }
}
