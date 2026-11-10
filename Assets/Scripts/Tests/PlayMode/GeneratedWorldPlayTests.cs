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

        /// <summary>
        /// Winning a world that wears Mars must not do what winning Mars does: unlock the Belt,
        /// mark Mars conquered, or offer to fly there.
        /// </summary>
        [UnityTest]
        public IEnumerator Victory_LeavesTheSolCampaignAlone()
        {
            SystemBody body = FirstOf(WorldArchetype.Volcanic);
            yield return Boot(body);
            var loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            Assert.IsTrue(loop.OutsideCampaign);
            Assert.AreEqual(CelestialBodyId.Mars, loop.ActiveBody);
            CelestialBodyId unlockedBefore = CampaignProgress.HighestUnlocked;
            int conqueredBefore = CampaignProgress.ConqueredMask;
            Assert.IsTrue(loop.Research.HasLaunchUnlockFor(loop.ActiveWorld), "no Sol launch tech stands in the way");

            loop.Mission.ArmForTests(densCleared: true, sustainComplete: true, launchReady: true, keepColony: true);
            loop.Mission.EvaluateVictoryForTests();
            yield return null;

            Assert.IsTrue(loop.Mission.IsWon);
            Assert.AreEqual(unlockedBefore, CampaignProgress.HighestUnlocked, "nothing in Sol was unlocked");
            Assert.AreEqual(conqueredBefore, CampaignProgress.ConqueredMask, "Mars was not conquered");
            Assert.AreEqual(1, loop.Stats.BodiesConquered, "the win still counts for the run");
            StringAssert.Contains(body.Name, loop.Mission.ObjectiveLabel);

            VictoryOffer.View offer = VictoryOffer.For(loop.ActiveWorld, VictoryOffer.NextIsBuilt(loop.ActiveWorld), endless: false);
            Assert.IsFalse(offer.CanContinue);
            StringAssert.Contains(body.Name.ToUpperInvariant(), offer.Headline);

            // The continue button's handler and the campaign advance both stay on this world.
            loop.ContinueAfterVictory();
            loop.AdvanceCampaign();
            yield return null;
            yield return null;
            var still = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            Assert.AreSame(loop, still, "no hop was started");
            Assert.AreEqual(body.Key, BodySeed.LoadSavedKey());
            Assert.AreEqual(unlockedBefore, CampaignProgress.HighestUnlocked);
        }

        /// <summary>
        /// A slot saved on a generated world loads back onto that world from a session that has
        /// never heard of it, and does not write its seed over the Sol world it wears.
        /// </summary>
        [UnityTest]
        public IEnumerator SlotSave_LoadsBackOntoItsOwnWorld()
        {
            SystemBody body = FirstOf(WorldArchetype.Volcanic);
            yield return Boot(body);
            var loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            loop.ChangeSpeed(SimSpeedChange.Set, 4);
            yield return new WaitForSeconds(2f);
            Assert.IsTrue(loop.SaveToSlot(2));
            Assert.IsTrue(SaveSystem.TryRead(2, out SaveGame written));
            Assert.AreEqual(body.Key, written.bodyKey);
            Assert.AreEqual(body.Name, written.bodyName);
            StringAssert.StartsWith(body.Name, written.Describe());

            // As a fresh process sitting on a Sol world: nothing registered, no world selected.
            CelestialBodyCatalog.ClearRegistered();
            BodySeed.SetBody(CelestialBodyId.Earth);
            const string marsSeedKey = "SM_BodySeed_Mars";
            GamePrefs.SetInt(marsSeedKey, 20011);
            CelestialBodyId unlockedBefore = CampaignProgress.HighestUnlocked;

            Assert.IsTrue(loop.LoadFromSlot(2));
            GameLoop loaded = null;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                loaded = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
                if (loaded != null && loaded != loop && loaded.Screen == DemoScreen.Playing) break;
            }
            Assert.IsNotNull(loaded);
            Assert.AreNotSame(loop, loaded);
            Assert.AreEqual(body.Key, loaded.ActiveWorld.Key, "the save came back on its own world");
            Assert.AreEqual(body.Seed, BodySeed.Current);
            Assert.AreEqual(body.Key, BodySeed.LoadSavedKey());
            Assert.Greater(loaded.Sim.Clock.TotalSteps, 0L, "the colony was restored, not started again");
            Assert.AreEqual(20011, GamePrefs.GetInt(marsSeedKey, -1), "Mars keeps its own seed");
            Assert.AreEqual(unlockedBefore, CampaignProgress.HighestUnlocked, "loading it unlocked nothing in Sol");
        }

        /// <summary>
        /// Review shots of every archetype in play, for a person to look at. Runs only when
        /// SM_SHOT_DIR names a folder, and only draws when Unity runs with a window (no -batchmode).
        /// </summary>
        [UnityTest]
        public IEnumerator ReviewShots_EveryArchetype()
        {
            string dir = Environment.GetEnvironmentVariable("SM_SHOT_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("Set SM_SHOT_DIR to write review shots.");
            System.IO.Directory.CreateDirectory(dir);
            foreach (WorldArchetype archetype in Enum.GetValues(typeof(WorldArchetype)))
            {
                SystemBody body = FirstOf(archetype);
                yield return Boot(body);
                yield return new WaitForSecondsRealtime(4f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "world-" + archetype + ".png"));
                yield return new WaitForSecondsRealtime(0.5f);
                CelestialBodyCatalog.ClearRegistered();
            }
        }

        // Boot a generated world from its key alone, as after a restart.
        private static IEnumerator Boot(SystemBody body)
        {
            if (Application.companyName != "SolarMajestyValidation" &&
                Environment.GetEnvironmentVariable("CI") != "true")
                Assert.Ignore("Run in the isolated validation project; this test resets its campaign.");

            SaveSystem.DeleteAll();
            DemoSettings.ClearSave();
            CampaignProgress.ResetCampaign();
            DemoSettings.SetFirstHourDemo(false);

            Assert.IsNotNull(body);
            StarSystemGenerator.EnsureRegistered(body.Key);
            Assert.IsTrue(CelestialBodyCatalog.TryGetRegistered(body.Key, out CelestialBodyProfile chosen));
            BodySeed.SetWorld(chosen);
            CelestialBodyCatalog.ClearRegistered();
            DemoSettings.RequestBootIntoPlay();
            yield return SceneManager.LoadSceneAsync("LunarOutpost_Sandbox");
            yield return null;
        }

        private static IEnumerator BootsAndPlays(WorldArchetype archetype)
        {
            SystemBody body = FirstOf(archetype);
            Assert.IsNotNull(body, "an early seed has a " + archetype);
            string key = body.Key;
            yield return Boot(body);

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
