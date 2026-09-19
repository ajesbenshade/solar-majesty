using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class SolarSystemTitleTests
    {
        private const string MaxKey = "SM_CampaignMaxBody";
        private const string FreshKey = "SM_CampaignInitialized";
        private bool _hadMax;
        private bool _hadFresh;
        private int _savedMax;
        private int _savedFresh;

        [SetUp]
        public void SetUp()
        {
            _hadMax = PlayerPrefs.HasKey(MaxKey);
            _hadFresh = PlayerPrefs.HasKey(FreshKey);
            _savedMax = PlayerPrefs.GetInt(MaxKey, (int)CelestialBodyId.Earth);
            _savedFresh = PlayerPrefs.GetInt(FreshKey, 0);
            PlayerPrefs.SetInt(MaxKey, (int)CelestialBodyId.Earth);
            PlayerPrefs.SetInt(FreshKey, 1);
            PlayerPrefs.Save();
            CampaignProgress.Ensure();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadMax) PlayerPrefs.SetInt(MaxKey, _savedMax);
            else PlayerPrefs.DeleteKey(MaxKey);
            if (_hadFresh) PlayerPrefs.SetInt(FreshKey, _savedFresh);
            else PlayerPrefs.DeleteKey(FreshKey);
            PlayerPrefs.Save();
            CampaignProgress.Ensure();
        }

        [Test]
        public void Orbits_AreNestedSunward()
        {
            Assert.Greater(SolarSystemOrbits.MarsOrbit, SolarSystemOrbits.EarthOrbit);
            Assert.Greater(SolarSystemOrbits.BeltOrbit, SolarSystemOrbits.MarsOrbit);
            Assert.Greater(SolarSystemOrbits.JupiterOrbit, SolarSystemOrbits.BeltOrbit);
            Assert.Greater(SolarSystemOrbits.EarthOrbit, SolarSystemOrbits.LunaOrbit);
            Assert.Greater(SolarSystemOrbits.JupiterOrbit, SolarSystemOrbits.EuropaOrbit);
        }

        [Test]
        public void OnOrbit_PlacesAlongXZ()
        {
            Vector3 east = SolarSystemOrbits.OnOrbit(8f, 0f);
            Assert.AreEqual(8f, east.x, 1e-4f);
            Assert.AreEqual(0f, east.y, 1e-4f);
            Assert.AreEqual(0f, east.z, 1e-4f);

            Vector3 west = SolarSystemOrbits.OnOrbit(8f, Mathf.PI);
            Assert.AreEqual(-8f, west.x, 1e-4f);
            Assert.AreEqual(0f, west.z, 1e-3f);
        }

        [Test]
        public void UnlockThrough_OpensTheSpineInclusive()
        {
            CampaignProgress.UnlockThrough(CelestialBodyId.Mars);

            Assert.IsTrue(CampaignProgress.IsUnlocked(CelestialBodyId.Earth));
            Assert.IsTrue(CampaignProgress.IsUnlocked(CelestialBodyId.Luna));
            Assert.IsTrue(CampaignProgress.IsUnlocked(CelestialBodyId.Mars));
            Assert.IsFalse(CampaignProgress.IsUnlocked(CelestialBodyId.Belt));
            Assert.IsFalse(CampaignProgress.IsUnlocked(CelestialBodyId.Europa));
            Assert.AreEqual(CelestialBodyId.Mars, CampaignProgress.HighestUnlocked);
        }

        [Test]
        public void UnlockThrough_DoesNotLowerTheSpine()
        {
            CampaignProgress.UnlockThrough(CelestialBodyId.Europa);
            CampaignProgress.UnlockThrough(CelestialBodyId.Luna);
            Assert.AreEqual(CelestialBodyId.Europa, CampaignProgress.HighestUnlocked);
        }

        [Test]
        public void Pick_LockedWorldStaysMenuOnly()
        {
            var outcome = SolarSystemTitlePick.Resolve(
                CelestialBodyId.Mars,
                CelestialBodyId.Earth,
                saveExists: false,
                unlocked: false,
                cheatUnlock: false);

            Assert.AreEqual(SolarSystemTitlePick.Outcome.Locked, outcome);
        }

        [Test]
        public void Pick_FreshClickStartsOnThatBody()
        {
            var outcome = SolarSystemTitlePick.Resolve(
                CelestialBodyId.Mars,
                CelestialBodyId.Earth,
                saveExists: false,
                unlocked: true,
                cheatUnlock: false);

            Assert.AreEqual(SolarSystemTitlePick.Outcome.StartNewOnBody, outcome);
        }

        [Test]
        public void Pick_ShiftClickOnLockedStartsFresh()
        {
            var outcome = SolarSystemTitlePick.Resolve(
                CelestialBodyId.Europa,
                CelestialBodyId.Earth,
                saveExists: false,
                unlocked: false,
                cheatUnlock: true);

            Assert.AreEqual(SolarSystemTitlePick.Outcome.StartNewOnBody, outcome);
        }

        [Test]
        public void Pick_SaveOnSameBodyContinues()
        {
            var outcome = SolarSystemTitlePick.Resolve(
                CelestialBodyId.Earth,
                CelestialBodyId.Earth,
                saveExists: true,
                unlocked: true,
                cheatUnlock: false);

            Assert.AreEqual(SolarSystemTitlePick.Outcome.ContinueCurrent, outcome);
        }

        [Test]
        public void Pick_SaveOnOtherBodySwitches()
        {
            var outcome = SolarSystemTitlePick.Resolve(
                CelestialBodyId.Luna,
                CelestialBodyId.Earth,
                saveExists: true,
                unlocked: true,
                cheatUnlock: false);

            Assert.AreEqual(SolarSystemTitlePick.Outcome.SwitchBody, outcome);
        }

        [Test]
        public void RayHitsSphere_FollowsAMovingCenter()
        {
            Vector3 origin = new Vector3(0f, 0f, -10f);
            Vector3 dir = Vector3.forward;

            Assert.IsTrue(SolarSystemTitlePick.RayHitsSphere(origin, dir, Vector3.zero, 1f, out float hitAtRest));
            Assert.Greater(hitAtRest, 0f);

            Assert.IsFalse(
                SolarSystemTitlePick.RayHitsSphere(origin, dir, new Vector3(5f, 0f, 0f), 1f, out _),
                "a parked collider at the origin would still hit; the pick must use the moved center");

            Assert.IsTrue(
                SolarSystemTitlePick.RayHitsSphere(origin, dir, new Vector3(0f, 0f, 4f), 1.1f, out float hitMoved));
            Assert.Greater(hitMoved, 0f);
            Assert.Greater(Mathf.Abs(hitMoved - hitAtRest), 0.5f);
        }

        [Test]
        public void OrreryMaps_ArePresentForEveryGlobe()
        {
            string[] maps =
            {
                "World/Orrery/Earth",
                "World/Orrery/EarthClouds",
                "World/Orrery/Luna",
                "World/Orrery/Mars",
                "World/Orrery/Jupiter",
                "World/Orrery/Sun",
                "World/Orrery/Europa",
                "World/Orrery/Ceres"
            };
            for (int i = 0; i < maps.Length; i++)
            {
                var tex = Resources.Load<Texture2D>(maps[i]);
                Assert.IsNotNull(tex, maps[i]);
                Assert.Greater(tex.width, 256, maps[i]);
            }
        }

        [Test]
        public void Catalog_EveryPlayableBodyIsInTheOrrerySpine()
        {
            Assert.AreEqual(5, CelestialBodyCatalog.All.Length);
            Assert.AreEqual(CelestialBodyId.Earth, CelestialBodyCatalog.All[0]);
            Assert.AreEqual(CelestialBodyId.Europa, CelestialBodyCatalog.Last);
        }
    }
}
