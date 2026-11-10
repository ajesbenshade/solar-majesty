using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// The title screen's Beyond Sol dialog: a typed seed shows its system, and landing on a
    /// body starts a colony there. Destructive like the campaign smoke tests, so it runs only
    /// in the validation project or CI.
    /// </summary>
    public class BeyondSolTitlePlayTests
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

        [UnityTest]
        public IEnumerator TypedSeed_ShowsItsSystem_AndLandingStartsAColony()
        {
            if (Application.companyName != "SolarMajestyValidation" &&
                Environment.GetEnvironmentVariable("CI") != "true")
                Assert.Ignore("Run in the isolated validation project; this test resets its campaign.");

            SaveSystem.DeleteAll();
            DemoSettings.ClearSave();
            CampaignProgress.ResetCampaign();
            DemoSettings.SetFirstHourDemo(false);
            yield return SceneManager.LoadSceneAsync("LunarOutpost_Sandbox");
            yield return null;

            var loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            Assert.AreEqual(DemoScreen.Title, loop.Screen, "the game opens on the title");
            var hud = UnityEngine.Object.FindFirstObjectByType<OverseerHud>();
            Assert.IsFalse(loop.TitleConfirmOpen);

            const string typed = "my homeworld";
            hud.PromptBeyondSol(typed);
            Assert.IsTrue(loop.TitleConfirmOpen, "the orrery ignores clicks while the dialog is up");
            // The dialog builds the system when it draws.
            StarSystem shown = null;
            for (int i = 0; i < 30 && shown == null; i++)
            {
                yield return null;
                shown = hud.BeyondSolSystem;
            }
            Assert.IsNotNull(shown, "the dialog drew");
            Assert.AreEqual(SystemSeedCode.FromText(typed), shown.Seed);
            Assert.AreEqual(StarSystemGenerator.Homeliest(shown).Index, hud.BeyondSolPick, "the most Earth-like body is picked first");

            // Land on the picked body, as the dialog's button does.
            string key = shown.Bodies[hud.BeyondSolPick].Key;
            int seed = shown.Bodies[hud.BeyondSolPick].Seed;
            Assert.IsTrue(loop.StartNewGameOnWorld(key, 1));
            yield return null;
            float deadline = Time.realtimeSinceStartup + 30f;
            GameLoop playing = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                playing = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
                if (playing != null && playing != loop && playing.Screen == DemoScreen.Playing) break;
            }
            Assert.IsNotNull(playing);
            Assert.AreEqual(DemoScreen.Playing, playing.Screen, "the colony is in play");
            Assert.AreEqual(key, playing.ActiveWorld.Key);
            Assert.AreEqual(seed, BodySeed.Current);
            Assert.IsTrue(playing.Settlement.HasCommons);
            Assert.IsFalse(playing.TitleConfirmOpen, "the dialog closed");
        }
    }
}
