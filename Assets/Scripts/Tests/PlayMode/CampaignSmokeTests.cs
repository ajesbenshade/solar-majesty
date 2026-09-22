using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Destructive session tests run only in the dedicated validation project or ephemeral CI.
    /// Ordinary editor Test Runner runs must never erase a developer's campaign/preferences.
    /// </summary>
    public class CampaignSmokeTests
    {
        [UnityTest]
        public IEnumerator FiveWorlds_BootAndRevisitWithIndependentSeedsAndColonies()
        {
            if (Application.companyName != "SolarMajestyValidation" &&
                Environment.GetEnvironmentVariable("CI") != "true")
                Assert.Ignore("Run in the isolated validation project; this test resets its campaign.");
            SaveSystem.DeleteAll();
            DemoSettings.ClearSave();
            CampaignProgress.ResetCampaign();
            DemoSettings.SetFirstHourDemo(false);
            CampaignProgress.UnlockThrough(CelestialBodyId.Europa);
            DemoSettings.RequestBootIntoPlay();
            yield return SceneManager.LoadSceneAsync("LunarOutpost_Sandbox");
            yield return null;
            var seeds = new int[5];
            var modules = new int[5];
            for (int i = 0; i < 5; i++)
            {
                var loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
                if (i > 0)
                {
                    loop.SelectBody((CelestialBodyId)i);
                    yield return null;
                    yield return null;
                    loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
                }
                loop.PausePlay();
                Assert.AreEqual((CelestialBodyId)i, BodySeed.Body);
                Assert.IsTrue(loop.Settlement.HasCommons, $"world {i} must be playable on arrival");
                var snapshot = loop.CaptureSave("world");
                seeds[i] = snapshot.seed;
                modules[i] = snapshot.buildings.Count;
            }
            for (int i = 3; i >= 0; i--)
            {
                UnityEngine.Object.FindFirstObjectByType<GameLoop>().SelectBody((CelestialBodyId)i);
                yield return null;
                yield return null;
                var loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
                loop.PausePlay();
                var restored = loop.CaptureSave("revisited");
                Assert.AreEqual(seeds[i], restored.seed);
                Assert.AreEqual(modules[i], restored.buildings.Count);
            }
        }

        [UnityTest]
        public IEnumerator EarthLunaReturn_ContinueAndRetry_KeepWorldsSeparate()
        {
            if (Application.companyName != "SolarMajestyValidation" &&
                Environment.GetEnvironmentVariable("CI") != "true")
                Assert.Ignore("Run in the isolated validation project; this test resets its campaign.");

            SaveSystem.DeleteAll();
            DemoSettings.ClearSave();
            CampaignProgress.ResetCampaign();
            DemoSettings.ResetTutorial();
            DemoSettings.SetFirstHourDemo(true);
            DemoSettings.RequestBootIntoPlay();
            yield return SceneManager.LoadSceneAsync("LunarOutpost_Sandbox");
            yield return null;
            var earth = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            Assert.IsNotNull(earth);
            earth.PausePlay();
            Assert.IsTrue(earth.Settlement.HasCommons, "Earth must start with Commons");
            var original = earth.CaptureSave("earth");
            Assert.GreaterOrEqual(original.buildings.Count, 3, "Earth demo shell includes Commons, airlock and HAB");
            earth.Resources.Set(ResourceId.Metals, 333);
            CampaignProgress.UnlockThrough(CelestialBodyId.Luna);
            earth.SelectBody(CelestialBodyId.Luna);
            yield return null;
            yield return null;
            var luna = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            Assert.AreEqual(CelestialBodyId.Luna, BodySeed.Body);
            luna.PausePlay();
            Assert.IsTrue(luna.Settlement.HasCommons, "first destination needs a free Commons");
            Assert.AreEqual(1, luna.CaptureSave("luna").buildings.Count, "Luna must not inherit Earth's shell");
            luna.Resources.Set(ResourceId.Metals, 222);
            luna.SelectBody(CelestialBodyId.Earth);
            yield return null;
            yield return null;
            earth = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            earth.PausePlay();
            var returned = earth.CaptureSave("returned");
            Assert.AreEqual(original.seed, returned.seed);
            Assert.AreEqual(original.buildings.Count, returned.buildings.Count);
            Assert.AreEqual(222, earth.Resources.Get(ResourceId.Metals), "stockpile follows the campaign, not the old Earth save");

            // A current-format save must stand on its own, even if old preferences are absent/stale.
            DemoSettings.WriteCampus(CelestialBodyId.Earth, "");
            DemoSettings.WriteRoster(CelestialBodyId.Earth, "");
            BodySeed.SetAndPersist(original.seed + 999);
            yield return SceneManager.LoadSceneAsync("LunarOutpost_Sandbox");
            yield return null;
            earth = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            earth.ContinueGame();
            earth.PausePlay();
            Assert.AreEqual(original.seed, BodySeed.Current, "terrain seed comes from the saved world");
            Assert.AreEqual(original.buildings.Count, earth.CaptureSave("continued").buildings.Count);
            Assert.AreEqual(222, earth.Resources.Get(ResourceId.Metals));
            earth.Resources.Set(ResourceId.Metals, 0);
            earth.RestartMission();
            yield return null;
            yield return null;
            earth = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            earth.PausePlay();
            Assert.IsTrue(earth.Settlement.HasCommons);
            Assert.Greater(earth.Resources.Get(ResourceId.Metals), 0, "retry starts a funded colony");
            Assert.IsTrue(SaveSystem.TryReadWorld(CelestialBodyId.Luna, out var retained));
            Assert.AreEqual(1, retained.buildings.Count, "retry must preserve the other world");
        }
    }
}
