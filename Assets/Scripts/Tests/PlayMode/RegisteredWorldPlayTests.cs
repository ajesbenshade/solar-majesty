using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// A world that is not one of the five: registered as data, booted, played and saved under
    /// its own key. Destructive like the campaign smoke tests, so it runs only in the validation
    /// project or CI.
    /// </summary>
    public class RegisteredWorldPlayTests
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
            CelestialBodyCatalog.ClearRegistered();
            DeveloperPrefsShield.Restore();
            SaveSystem.DirectoryOverride = null;
            PlaytestTelemetry.DirectoryOverride = null;
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(_saveRoot) && System.IO.Directory.Exists(_saveRoot))
                System.IO.Directory.Delete(_saveRoot, true);
        }

        [UnityTest]
        public IEnumerator RegisteredWorld_BootsPlaysAndSavesUnderItsOwnKey()
        {
            if (Application.companyName != "SolarMajestyValidation" &&
                Environment.GetEnvironmentVariable("CI") != "true")
                Assert.Ignore("Run in the isolated validation project; this test resets its campaign.");

            SaveSystem.DeleteAll();
            DemoSettings.ClearSave();
            CampaignProgress.ResetCampaign(); // Mars is locked: a registered world must not need it
            DemoSettings.SetFirstHourDemo(false);

            CelestialBodyProfile ares = CelestialBodyCatalog.Mars().Copy();
            ares.Key = "test.ares";
            ares.DisplayName = "Ares";
            ares.ShortCode = "ARES";
            ares.Seed = 424242;
            ares.ResourceNodeCount = 9;
            Assert.IsTrue(CelestialBodyCatalog.Register(ares));

            BodySeed.SetWorld(ares);
            DemoSettings.RequestBootIntoPlay();
            yield return SceneManager.LoadSceneAsync("LunarOutpost_Sandbox");
            yield return null;

            var loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            Assert.AreSame(ares, loop.ActiveWorld, "the registered profile is the world being played");
            Assert.AreEqual(CelestialBodyId.Mars, loop.ActiveBody, "authored content comes from the world it borrows");
            Assert.AreEqual(424242, BodySeed.Current);
            Assert.AreEqual("test.ares", loop.Sim.BodyKey);
            Assert.IsTrue(loop.Settlement.HasCommons, "the world must be playable on arrival");

            // Let the fixed step run: robots decide, the economy beats, nothing throws.
            loop.ChangeSpeed(SimSpeedChange.Set, 4);
            yield return new WaitForSeconds(3f);
            Assert.Greater(loop.Sim.Clock.TotalSteps, 0L);
            Assert.AreNotEqual(0UL, loop.WorldHash());

            SaveGame snapshot = loop.CaptureSave("ares");
            Assert.AreEqual("test.ares", snapshot.bodyKey);
            Assert.AreEqual(424242, snapshot.seed);
            Assert.IsTrue(snapshot.MatchesWorld(ares, 424242));
            Assert.IsFalse(snapshot.MatchesWorld(CelestialBodyCatalog.Mars(), 424242), "not Mars, even with Mars's look");

            BodySeed.SetBody(CelestialBodyId.Earth);
            Assert.AreEqual("", BodySeed.LoadSavedKey(), "choosing a Sol world leaves the registered one");
        }
    }
}
