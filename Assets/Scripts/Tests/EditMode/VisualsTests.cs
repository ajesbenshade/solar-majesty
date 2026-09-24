using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Golden-hour / sky / diorama pass: the pure math behind the light cycle, sky panoramas and the
    /// perspective camera. The key contract: at mission start every world looks exactly as tuned.
    /// </summary>
    public class VisualsTests
    {
        private static readonly CelestialBodyId[] Bodies =
            { CelestialBodyId.Earth, CelestialBodyId.Luna, CelestialBodyId.Mars, CelestialBodyId.Belt, CelestialBodyId.Europa };

        private static CelestialBodyProfile Body(CelestialBodyId id) => CelestialBodyCatalog.Get(id);

        [Test]
        public void SunPath_StartsOnTheTunedLook()
        {
            foreach (var id in Bodies)
            {
                var body = Body(id);
                var s = SunPath.Evaluate(body, 0);
                Assert.AreEqual(body.SunEuler.x, s.Euler.x, 1e-3f, $"{id} elevation");
                Assert.AreEqual(body.SunEuler.y, s.Euler.y, 1e-3f, $"{id} azimuth");
                Assert.AreEqual(body.SunColor.r, s.Color.r, 1e-4f, $"{id} colour");
                Assert.AreEqual(body.SunColor.b, s.Color.b, 1e-4f, $"{id} colour");
                Assert.AreEqual(1f, s.Intensity, 1e-4f, $"{id} intensity");
                Assert.AreEqual(1f, s.Ambient, 1e-4f, $"{id} ambient");
                Assert.AreEqual(0f, s.Night, 1e-4f, $"{id} night");
            }
        }

        [Test]
        public void SunPath_IsPeriodicContinuousAndNeverSets()
        {
            foreach (var id in Bodies)
            {
                var body = Body(id);
                const int n = 2000;
                bool sawNight = false, sawGolden = false;
                var prev = SunPath.Evaluate(body, 0);
                for (int i = 1; i <= n; i++)
                {
                    double t = SunPath.CycleSeconds * i / (double)n;
                    var s = SunPath.Evaluate(body, t);
                    Assert.GreaterOrEqual(s.Euler.x, SunPath.MinElevation - 1e-3f, $"{id} sun below floor at {t}");
                    Assert.GreaterOrEqual(s.Intensity, SunPath.NightIntensity - 1e-3f);
                    Assert.Less(Mathf.Abs(s.Euler.x - prev.Euler.x), 1.5f, $"{id} elevation jump at {t}");
                    Assert.Less(Mathf.Abs(s.Euler.y - prev.Euler.y), 1.5f, $"{id} azimuth jump at {t}");
                    Assert.Less(Mathf.Abs(s.Intensity - prev.Intensity), 0.05f, $"{id} intensity jump at {t}");
                    sawNight |= s.Night > 0.9f;
                    sawGolden |= s.Golden > 0.9f && s.Night < 0.1f;
                    prev = s;
                }
                Assert.IsTrue(sawNight, $"{id} has a night");
                Assert.IsTrue(sawGolden, $"{id} has a golden hour");

                var a = SunPath.Evaluate(body, 123.0);
                var b = SunPath.Evaluate(body, 123.0 + SunPath.CycleSeconds);
                Assert.AreEqual(a.Euler.x, b.Euler.x, 1e-2f);
                Assert.AreEqual(a.Euler.y, b.Euler.y, 1e-2f);
            }
        }

        [Test]
        public void SunPath_GoldenHourColourPerWorld()
        {
            var earth = Body(CelestialBodyId.Earth);
            var mars = Body(CelestialBodyId.Mars);
            var luna = Body(CelestialBodyId.Luna);
            Assert.Greater(SunPath.GoldenColor(earth, earth.SunColor).r, SunPath.GoldenColor(earth, earth.SunColor).b, "Earth sunsets are amber");
            Assert.Greater(SunPath.GoldenColor(mars, mars.SunColor).b, SunPath.GoldenColor(mars, mars.SunColor).r, "Mars sunsets are blue");
            Assert.AreEqual(luna.SunColor, SunPath.GoldenColor(luna, luna.SunColor), "no air, no tint");
        }

        [Test]
        public void Sky_UVRoundTripsAndMatchesPanoramicLayout()
        {
            for (float u = 0.05f; u < 1f; u += 0.1f)
            for (float v = 0.05f; v < 1f; v += 0.1f)
            {
                var uv = SkyPainter.UVFromDir(SkyPainter.DirFromUV(u, v));
                Assert.AreEqual(u, uv.x, 1e-4f);
                Assert.AreEqual(v, uv.y, 1e-4f);
            }
            Assert.AreEqual(1f, SkyPainter.DirFromUV(0.5f, 1f).y, 1e-4f, "top row is the zenith");
            Assert.AreEqual(0f, SkyPainter.DirFromUV(0.3f, 0.5f).y, 1e-4f, "middle row is the horizon");
        }

        [Test]
        public void Sky_HorizonIsFogColourSoGroundMeltsIn()
        {
            foreach (var id in Bodies)
            {
                var body = Body(id);
                var h = SkyPainter.Gradient(body, 0.0001f);
                Assert.AreEqual(body.FogColor.r, h.r, 0.02f, $"{id}");
                Assert.AreEqual(body.FogColor.g, h.g, 0.02f, $"{id}");
                var zen = SkyPainter.Gradient(body, 1f);
                Assert.AreEqual(body.SkyTop.b, zen.b, 0.02f, $"{id} zenith");
            }
        }

        [Test]
        public void Sky_PlanetPhaseFollowsTheSun()
        {
            var earthOverLuna = SkyPainter.PlanetFor(Body(CelestialBodyId.Luna));
            Assert.AreEqual(SkyPlanetKind.Earth, earthOverLuna.Kind);
            Assert.AreEqual(SkyPlanetKind.Jupiter, SkyPainter.PlanetFor(Body(CelestialBodyId.Europa)).Kind);
            Assert.AreEqual(SkyPlanetKind.None, SkyPainter.PlanetFor(Body(CelestialBodyId.Earth)).Kind);

            SkyPainter.PlanetBasis(earthOverLuna, out Vector3 center, out _, out _);
            var full = SkyPainter.ShadePlanet(earthOverLuna, -center, 0f, 0f);  // sun behind the viewer
            var fresh = SkyPainter.ShadePlanet(earthOverLuna, center, 0f, 0f);  // sun behind Earth
            Assert.AreEqual(1f, full.a, 1e-4f);
            Assert.Greater(full.maxColorComponent, fresh.maxColorComponent * 5f, "full Earth is bright, new Earth dark");
            Assert.AreEqual(0f, SkyPainter.ShadePlanet(earthOverLuna, -center, 1.5f, 0f).a, 1e-4f, "outside the disc");
        }

        [Test]
        public void Diorama_ZoomKeepsOrthographicMeaningAndRevealsSky()
        {
            foreach (float z in new[] { 4.5f, 16f, 52f })
                Assert.AreEqual(z, DioramaRig.Distance(z) * Mathf.Tan(DioramaRig.FieldOfView * 0.5f * Mathf.Deg2Rad), 1e-3f);

            Assert.AreEqual(30f + DioramaRig.CloseTilt, DioramaRig.Pitch(30f, 0f), 1e-4f);
            Assert.AreEqual(30f + DioramaRig.CloseTilt, DioramaRig.Pitch(30f, DioramaRig.LiftStart), 1e-4f);
            Assert.AreEqual(DioramaRig.HorizonPitch, DioramaRig.Pitch(30f, 1f), 1e-4f);
            Assert.IsTrue(DioramaRig.ShowsSky(DioramaRig.Pitch(30f, 1f)), "full zoom-out shows sky");
            Assert.IsFalse(DioramaRig.ShowsSky(DioramaRig.Pitch(30f, 0f)), "close-up is ground only");

            DioramaRig.Fog(100f, out float start, out float end);
            Assert.Greater(start, 100f, "focus stays clear");
            Assert.Greater(end, start);
        }
    }
}
