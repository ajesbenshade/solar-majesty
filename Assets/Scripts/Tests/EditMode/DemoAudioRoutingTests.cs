using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Which one-shots play in 3D, which stay 2D, how the 3D rolloff tracks the overseer camera,
    /// and that every event the game plays has authored clips (no synthesised fallthrough).
    /// </summary>
    public class DemoAudioRoutingTests
    {
        private static readonly string[] ColonyWide =
        {
            "ui_hover", "ui_click", "retry", "alert_warning", "alert_critical", "fail", "victory",
            "launch", "research",
        };

        private static readonly string[] Located =
        {
            "flag_post", "build_place", "build_complete", "construction_tick", "claim", "bite",
            "robot_hit", "robot_down", "level_up", "credits", "robot_spawn", "party_form", "repair",
            "heal", "equip", "extract", "stalker_growl", "stalker_death", "launch_ready",
        };

        private static readonly Vector3 Somewhere = new Vector3(20f, 0f, 30f);

        [Test]
        public void WorldEvents_WithPosition_GoSpatial()
        {
            foreach (string key in Located)
                Assert.AreEqual(SfxSpace.Spatial, SfxRouting.Route(key, true, Somewhere, true), key);
        }

        [Test]
        public void UiAlertsAndOutcomes_StayTwoD_EvenWithAPosition()
        {
            foreach (string key in ColonyWide)
                Assert.AreEqual(SfxSpace.TwoD, SfxRouting.Route(key, true, Somewhere, true), key);
        }

        [Test]
        public void WorldEvents_WithoutPosition_FallBackToTwoD()
        {
            foreach (string key in Located)
                Assert.AreEqual(SfxSpace.TwoD, SfxRouting.Route(key, false, default, true), key);
        }

        [Test]
        public void NonFinitePosition_FallsBackToTwoD()
        {
            Assert.AreEqual(SfxSpace.TwoD,
                SfxRouting.Route("bite", true, new Vector3(float.NaN, 0f, 0f), true));
            Assert.AreEqual(SfxSpace.TwoD,
                SfxRouting.Route("bite", true, new Vector3(0f, float.PositiveInfinity, 0f), true));
        }

        [Test]
        public void NoSpatialPlayer_FallsBackToTwoD()
        {
            Assert.AreEqual(SfxSpace.TwoD, SfxRouting.Route("extract", true, Somewhere, false));
        }

        [Test]
        public void UnknownOrNullKey_IsTwoD()
        {
            Assert.AreEqual(SfxSpace.TwoD, SfxRouting.Route("no_such_event", true, Somewhere, true));
            Assert.AreEqual(SfxSpace.TwoD, SfxRouting.Route(null, true, Somewhere, true));
        }

        /// <summary>Every event DemoAudio plays is classified on purpose, not by omission.</summary>
        [Test]
        public void EveryDemoAudioEvent_IsClassified()
        {
            var expected = new HashSet<string>(ColonyWide.Concat(Located));
            foreach (string key in DemoAudio.EventKeys)
                Assert.IsTrue(expected.Contains(key), $"'{key}' is neither in the 2D nor the 3D list");
            CollectionAssert.AreEquivalent(Located, SfxRouting.WorldEventKeys.ToArray());
        }

        // ----------------------------------------------------------------------------------------
        // Clip lookup
        // ----------------------------------------------------------------------------------------

        [TestCase("extract")]
        [TestCase("build_complete")]
        [TestCase("research")]
        public void FormerlyMissingClips_HaveThreeAuthoredTakes(string key)
        {
            Assert.AreEqual(3, DemoAudio.AuthoredTakeCount(key),
                $"Resources/Audio/Sfx/sfx_{key}_1..3 should load; otherwise DemoAudio synthesises a chord");
        }

        [Test]
        public void EveryEvent_HasAuthoredClips_NoSynthesisFallthrough()
        {
            var missing = DemoAudio.EventKeys.Where(k => DemoAudio.AuthoredTakeCount(k) == 0).ToList();
            CollectionAssert.IsEmpty(missing, "Events with no clip: " + string.Join(", ", missing));
        }

        [Test]
        public void AuthoredClips_AreMono44k_LikeTheirNeighbours()
        {
            foreach (string key in new[] { "extract", "build_complete", "research", "build_place" })
            {
                var clip = Resources.Load<AudioClip>("Audio/Sfx/sfx_" + key + "_1");
                Assert.IsNotNull(clip, key);
                Assert.AreEqual(44100, clip.frequency, key);
                Assert.AreEqual(1, clip.channels, key);
                Assert.That(clip.length, Is.InRange(0.3f, 4f), key);
            }
        }

        // ----------------------------------------------------------------------------------------
        // Rolloff relative to the camera
        // ----------------------------------------------------------------------------------------

        private const float Aspect = 16f / 9f;

        /// <summary>Default view: ortho 18 at 16:9, so 32 m of ground either side of centre.</summary>
        [Test]
        public void DefaultView_CentreFull_EdgeAudible_FarSilent()
        {
            var r = SfxRouting.ComputeRolloff(ColonyLayout.CameraOrthoSize, Aspect);
            float halfWidth = ColonyLayout.CameraOrthoSize * Aspect;
            Assert.AreEqual(1f, SfxRouting.Gain(r, 0f), 1e-4f, "screen centre");
            Assert.AreEqual(1f, SfxRouting.Gain(r, halfWidth * 0.5f), 1e-4f, "middle of the view");
            Assert.AreEqual(SfxRouting.EdgeGain, SfxRouting.Gain(r, halfWidth), 1e-3f, "side edge");
            Assert.Greater(SfxRouting.Gain(r, halfWidth + 10f), 0.3f, "just off screen");
            Assert.AreEqual(0f, SfxRouting.Gain(r, r.Far + 1f), 1e-4f, "far away");
        }

        [Test]
        public void GroundDistance_IgnoresHeight_AndDirection()
        {
            var focus = new Vector3(10f, 0f, 10f);
            Assert.AreEqual(20f, SfxRouting.GroundDistance(focus, new Vector3(30f, 5f, 10f)), 1e-4f);
            Assert.AreEqual(20f, SfxRouting.GroundDistance(focus, new Vector3(10f, -3f, -10f)), 1e-4f);
        }

        /// <summary>
        /// The real default pose: camera 22 m up at pitch 30, ortho 18. Measured from the listener,
        /// the top edge of the view is 77 m away (past the old 70 m cut-off, so silent) and the
        /// bottom edge 22 m. Measured on the ground from the focus, they are the same.
        /// </summary>
        [Test]
        public void TopAndBottomOfTheDefaultView_PlayAlike()
        {
            Quaternion rot = Quaternion.Euler(30f, 45f, 0f);
            Vector3 fwd = rot * Vector3.forward;
            Vector3 up = rot * Vector3.up;
            var cam = new Vector3(0f, 22f, 0f);
            Assert.IsTrue(IsometricCameraController.TryGroundFocus(cam, fwd, out Vector3 focus));

            float h = ColonyLayout.CameraOrthoSize;
            Vector3 top = Ground(cam + up * h, fwd);
            Vector3 bottom = Ground(cam - up * h, fwd);
            Assert.Greater(Vector3.Distance(cam, top), 70f, "precondition: old rolloff muted the top edge");

            var r = SfxRouting.ComputeRolloff(h, Aspect);
            float gTop = SfxRouting.Gain(r, SfxRouting.GroundDistance(focus, top));
            float gBottom = SfxRouting.Gain(r, SfxRouting.GroundDistance(focus, bottom));
            Assert.AreEqual(gTop, gBottom, 1e-3f);
            Assert.Greater(gTop, 0.4f);
        }

        private static Vector3 Ground(Vector3 origin, Vector3 forward)
        {
            Assert.IsTrue(IsometricCameraController.TryGroundFocus(origin, forward, out Vector3 p));
            return p;
        }

        /// <summary>The old fixed 6–70 m rolloff silenced on-screen sounds at max zoom-out.</summary>
        [Test]
        public void MaxZoomOut_OnScreenEdgeStaysAudible()
        {
            float halfWidth = IsometricCameraController.MaxOrthoSize * Aspect;
            var r = SfxRouting.ComputeRolloff(IsometricCameraController.MaxOrthoSize, Aspect);
            Assert.Greater(halfWidth, 70f, "precondition: the screen edge is beyond the old max distance");
            Assert.AreEqual(SfxRouting.EdgeGain, SfxRouting.Gain(r, halfWidth), 1e-3f);
        }

        /// <summary>The diorama lens keeps the zoom's meaning, so it gets the same rolloff.</summary>
        [Test]
        public void DioramaLens_SeesTheSameHalfHeightAsOrtho()
        {
            float zoom = ColonyLayout.CameraOrthoSize;
            float half = SfxRouting.VisibleHalfHeight(false, 0f, DioramaRig.FieldOfView, DioramaRig.Distance(zoom));
            Assert.AreEqual(zoom, half, 0.01f);
            Assert.AreEqual(zoom, SfxRouting.VisibleHalfHeight(true, zoom, 60f, 999f), 1e-5f);
        }

        [Test]
        public void CloseZoom_KeepsAnOffScreenTail()
        {
            var r = SfxRouting.ComputeRolloff(4.5f, Aspect);
            Assert.GreaterOrEqual(r.Far - r.Edge, SfxRouting.MinTail - 1e-3f);
        }

        [Test]
        public void Rolloff_IsMonotonic_AndSafeOnBadInput()
        {
            var r = SfxRouting.ComputeRolloff(float.NaN, 0f);
            Assert.Greater(r.Full, 0f);
            Assert.Greater(r.Edge, r.Full);
            Assert.Greater(r.Far, r.Edge);
            float prev = 1f;
            for (float d = 0f; d < r.Far + 20f; d += 0.5f)
            {
                float g = SfxRouting.Gain(r, d);
                Assert.LessOrEqual(g, prev + 1e-5f, $"gain rose at {d} m");
                prev = g;
            }
        }
    }
}
