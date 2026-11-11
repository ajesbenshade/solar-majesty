using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// The look contract behind the lighting / post pass: each campaign world grades distinctly,
    /// bloom stays subtle, and the cheap tiers stay cheap. Pure data; no rendering.
    /// </summary>
    public class WorldLookTests
    {
        private static readonly CelestialBodyId[] Bodies =
            { CelestialBodyId.Earth, CelestialBodyId.Luna, CelestialBodyId.Mars, CelestialBodyId.Belt, CelestialBodyId.Europa };

        [Test]
        public void Tiers_FollowTheQualityLevelsInProjectSettings()
        {
            // Very Low, Low, Medium, High, Very High, Ultra (see QualityTiersSetup.LevelAssets).
            Assert.AreEqual(LookTier.Low, WorldLook.TierFor(0, 6));
            Assert.AreEqual(LookTier.Low, WorldLook.TierFor(1, 6));
            Assert.AreEqual(LookTier.Medium, WorldLook.TierFor(2, 6));
            Assert.AreEqual(LookTier.High, WorldLook.TierFor(3, 6));
            Assert.AreEqual(LookTier.High, WorldLook.TierFor(4, 6));
            Assert.AreEqual(LookTier.Ultra, WorldLook.TierFor(5, 6));
            Assert.AreEqual(LookTier.Ultra, WorldLook.TierFor(0, 1), "a single level is the look target");
        }

        [Test]
        public void Bloom_IsSubtleOnEveryWorld()
        {
            foreach (var id in Bodies)
            {
                var g = WorldLook.For(id);
                Assert.GreaterOrEqual(g.BloomThreshold, 1f, $"{id}: lit surfaces below white must not bloom");
                Assert.LessOrEqual(g.BloomThreshold, 1.3f, $"{id}");
                Assert.Greater(g.BloomIntensity, 0f, $"{id}");
                // SunCycle multiplies bloom by up to 2.6 at night; keep the night peak under 0.7.
                Assert.Less(g.BloomIntensity * 2.6f, 0.7f, $"{id} night bloom");
                Assert.That(g.BloomScatter, Is.InRange(0.4f, 0.75f), $"{id}");
                Assert.That(g.VignetteIntensity, Is.InRange(0.1f, 0.3f), $"{id}: light vignette");
                Assert.IsTrue(g.Aces, $"{id}: world materials are tuned against ACES");
            }
        }

        [Test]
        public void Worlds_GradeDistinctly()
        {
            var earth = WorldLook.For(CelestialBodyId.Earth);
            var luna = WorldLook.For(CelestialBodyId.Luna);
            var mars = WorldLook.For(CelestialBodyId.Mars);

            // Luna: cool, desaturated, deepest blacks.
            Assert.Less(luna.Saturation, 0f);
            Assert.Less(luna.Saturation, mars.Saturation);
            Assert.Less(mars.Saturation, earth.Saturation);
            Assert.Greater(luna.Contrast, earth.Contrast);
            Assert.Greater(luna.Contrast, mars.Contrast);
            Assert.Less(luna.Temperature, 0f);
            Assert.Greater(WorldLook.SunShadowStrength(CelestialBodyId.Luna), WorldLook.SunShadowStrength(CelestialBodyId.Earth));

            // Earth: warm-neutral.
            Assert.Greater(earth.Temperature, 0f);
            var earthFilter = CelestialBodyCatalog.Default(CelestialBodyId.Earth).GradeFilter;
            Assert.GreaterOrEqual(earthFilter.r, earthFilter.b, "Earth's filter is not cool");
            var lunaFilter = CelestialBodyCatalog.Default(CelestialBodyId.Luna).GradeFilter;
            Assert.Greater(lunaFilter.b, lunaFilter.r, "Luna's filter is cool");

            // Mars: dust in the air scatters widest; soft shadows hold the round-6 gate.
            Assert.Greater(mars.BloomScatter, earth.BloomScatter);
            Assert.Greater(earth.BloomScatter, luna.BloomScatter);
            Assert.Greater(mars.BloomThreshold, earth.BloomThreshold, "sunlit cream hulls must not go milky");
            Assert.AreEqual(0.46f, WorldLook.SunShadowStrength(CelestialBodyId.Mars), 1e-4f);
            Assert.Greater(mars.VignetteColor.r, mars.VignetteColor.b, "warm vignette");
            Assert.Greater(luna.VignetteColor.b, luna.VignetteColor.r, "cold vignette");
        }

        [Test]
        public void Luna_SunIsWhiterAndAmbientThinnerThanEarth()
        {
            var earth = CelestialBodyCatalog.Default(CelestialBodyId.Earth);
            var luna = CelestialBodyCatalog.Default(CelestialBodyId.Luna);
            Assert.Greater(luna.SunColor.b, earth.SunColor.b, "no atmosphere to warm the sun");
            Assert.Less(luna.AmbientSky.maxColorComponent, earth.AmbientSky.maxColorComponent);
            Assert.Less(luna.AmbientGround.maxColorComponent, 0.1f, "shadows go deep");
            Assert.Greater(luna.SunEuler.x, SunPath.MinElevation + 14f, "tuned look stays above the golden window");
        }

        [Test]
        public void CheapTiers_StayCheap()
        {
            Assert.Less(WorldLook.BloomIterations(LookTier.Low), WorldLook.BloomIterations(LookTier.Medium));
            Assert.Less(WorldLook.BloomIterations(LookTier.Medium), WorldLook.BloomIterations(LookTier.High));
            Assert.IsTrue(WorldLook.BloomQuarterRes(LookTier.Low));
            Assert.IsFalse(WorldLook.BloomQuarterRes(LookTier.Medium));
            Assert.IsFalse(WorldLook.BloomHighQuality(LookTier.High));
            Assert.IsTrue(WorldLook.BloomHighQuality(LookTier.Ultra));
            Assert.IsFalse(WorldLook.TitleDepthOfField(LookTier.Low));
            Assert.IsTrue(WorldLook.TitleDepthOfField(LookTier.Medium));
        }

        [Test]
        public void Title_KeepsTheInnerSystemSharp()
        {
            var title = WorldLook.Title;
            Assert.IsFalse(title.Aces, "unlit orrery textures keep their authored colour");
            Assert.AreEqual(0f, title.Temperature);
            Assert.AreEqual(0f, title.PostExposure);

            // Camera to the sun, and to the far side of Mars' orbit: both in front of the blur.
            float toSun = SolarSystemOrbits.CameraLocal.magnitude;
            Assert.Less(toSun + SolarSystemOrbits.EarthOrbit + SolarSystemOrbits.LunaOrbit, WorldLook.TitleDofStart);
            Assert.Less(toSun + SolarSystemOrbits.MarsOrbit, WorldLook.TitleDofStart);
            Assert.Less(WorldLook.TitleDofStart, WorldLook.TitleDofEnd);
            Assert.LessOrEqual(WorldLook.TitleDofMaxRadius, 1f);
        }
    }
}
