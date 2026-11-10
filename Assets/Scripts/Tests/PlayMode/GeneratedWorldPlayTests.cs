using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// A world from the star system generator must boot, raise its commons and run the fixed
    /// step in a real scene, and come back from its key alone. Destructive like the campaign
    /// smoke tests, so it runs only in the validation project or CI.
    /// </summary>
    public class GeneratedWorldPlayTests
    {
        private string _saveRoot;

        [SetUp]
        public void UseTempSaveRoot()
        {
            _saveRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sm-saves-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_saveRoot);
            SaveSystem.DirectoryOverride = _saveRoot;
            PlaytestTelemetry.DirectoryOverride = System.IO.Path.Combine(_saveRoot, "Playtest");
            DeveloperPrefsShield.Capture();
            CelestialBodyCatalog.ClearRegistered();
        }

        [TearDown]
        public void ClearTempSaveRoot()
        {
            BodySeed.SetBody(CelestialBodyId.Earth);
            DeveloperPrefsShield.Restore();
            SaveSystem.DirectoryOverride = null;
            PlaytestTelemetry.DirectoryOverride = null;
            CelestialBodyCatalog.ClearRegistered();
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(_saveRoot) && System.IO.Directory.Exists(_saveRoot))
                System.IO.Directory.Delete(_saveRoot, true);
        }

        // One archetype that mixes two Sol worlds, one that repaints a Sol world, and one on water.
        [UnityTest]
        public IEnumerator GasGiantMoon_BootsAndPlays() => BootsAndPlays(WorldArchetype.GasGiantMoon);

        [UnityTest]
        public IEnumerator Volcanic_BootsAndPlays() => BootsAndPlays(WorldArchetype.Volcanic);

        [UnityTest]
        public IEnumerator OceanArchipelago_BootsAndPlays() => BootsAndPlays(WorldArchetype.OceanArchipelago);

        private static IEnumerator BootsAndPlays(WorldArchetype archetype)
        {
            if (Application.companyName != "SolarMajestyValidation" &&
                Environment.GetEnvironmentVariable("CI") != "true")
                Assert.Ignore("Run in the isolated validation project; this test resets its campaign.");

            SaveSystem.DeleteAll();
            DemoSettings.ClearSave();
            CampaignProgress.ResetCampaign();
            DemoSettings.SetFirstHourDemo(false);

            SystemBody body = FirstOf(archetype);
            Assert.IsNotNull(body, "an early seed has a " + archetype);
            string key = body.Key;

            // Only the key is stored, as after a restart: the boot must build the system again.
            StarSystemGenerator.EnsureRegistered(key);
            Assert.IsTrue(CelestialBodyCatalog.TryGetRegistered(key, out CelestialBodyProfile chosen));
            BodySeed.SetWorld(chosen);
            CelestialBodyCatalog.ClearRegistered();
            DemoSettings.RequestBootIntoPlay();
            yield return SceneManager.LoadSceneAsync("LunarOutpost_Sandbox");
            yield return null;

            var loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            Assert.IsNotNull(loop.ActiveWorld);
            Assert.AreEqual(key, loop.ActiveWorld.Key, "the generated world is the one being played");
            Assert.AreEqual(key, loop.Sim.BodyKey);
            Assert.AreEqual(body.Profile.DisplayName, loop.ActiveWorld.DisplayName);
            Assert.AreEqual(body.Seed, BodySeed.Current, "terrain comes from the body seed");
            Assert.AreEqual(WorldArchetypes.BaseOf(archetype), loop.ActiveBody);
            Assert.IsTrue(loop.Settlement.HasCommons, "the world must be playable on arrival");

            loop.ChangeSpeed(SimSpeedChange.Set, 4);
            yield return new WaitForSeconds(3f);
            Assert.Greater(loop.Sim.Clock.TotalSteps, 0L);
            Assert.AreNotEqual(0UL, loop.WorldHash());

            SaveGame snapshot = loop.CaptureSave("generated");
            Assert.AreEqual(key, snapshot.bodyKey);
            Assert.IsTrue(snapshot.MatchesWorld(loop.ActiveWorld, body.Seed));
        }

        private static SystemBody FirstOf(WorldArchetype archetype)
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                foreach (SystemBody body in StarSystemGenerator.Generate(seed).Bodies)
                {
                    if (body.Archetype == archetype) return body;
                }
            }
            return null;
        }
    }
}
