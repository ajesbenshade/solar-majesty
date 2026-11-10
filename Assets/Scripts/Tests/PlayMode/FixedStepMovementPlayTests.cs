using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Robots and fauna run on the fixed simulation step. In a real scene they must still walk
    /// to a posted flag and work it, and be drawn without jumps between steps. Destructive like
    /// the campaign smoke tests, so it runs only in the validation project or CI.
    /// </summary>
    public class FixedStepMovementPlayTests
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
        }

        [TearDown]
        public void ClearTempSaveRoot()
        {
            DeveloperPrefsShield.Restore();
            SaveSystem.DirectoryOverride = null;
            PlaytestTelemetry.DirectoryOverride = null;
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(_saveRoot) && System.IO.Directory.Exists(_saveRoot))
                System.IO.Directory.Delete(_saveRoot, true);
        }

        [UnityTest]
        public IEnumerator Robots_WalkToAPostedFlagAndWorkIt_OnTheFixedStep()
        {
            if (Application.companyName != "SolarMajestyValidation" &&
                Environment.GetEnvironmentVariable("CI") != "true")
                Assert.Ignore("Run in the isolated validation project; this test resets its campaign.");

            SaveSystem.DeleteAll();
            DemoSettings.ClearSave();
            CampaignProgress.ResetCampaign();
            DemoSettings.SetFirstHourDemo(false);
            BodySeed.SetBody(CelestialBodyId.Earth);
            DemoSettings.RequestBootIntoPlay();
            yield return SceneManager.LoadSceneAsync("LunarOutpost_Sandbox");
            yield return null;

            var loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            Assert.IsTrue(loop.DrivesAgentSim, "play runs robots and fauna on the fixed step");
            // A new colony starts empty; stand a crew up the way the store-shot harness does.
            Assert.Greater(loop.SpawnShowcaseCrew(joinColony: true), 0);
            yield return null;
            Assert.Greater(loop.Agents.Count, 0);

            var start = new Dictionary<SpecialistAgent, Vector3>();
            var last = new Dictionary<SpecialistAgent, Vector3>();
            Vector3 centre = Vector3.zero;
            foreach (SpecialistAgent a in loop.Agents)
            {
                start[a] = a.transform.position;
                last[a] = a.transform.position;
                centre += a.transform.position;
            }
            centre /= loop.Agents.Count;

            // A generous bounty a short walk away, so somebody takes it.
            Vector3 flagAt = centre + new Vector3(14f, 0f, 6f);
            loop.Commands.Enqueue(OverseerCommand.PostFlag(FlagType.Explore, flagAt, 600f));
            loop.ChangeSpeed(SimSpeedChange.Set, 8);
            yield return null;
            yield return null;
            Assert.AreEqual(1, loop.Flags.Flags.Count, "the flag was posted");
            FlagHandle flag = loop.Flags.Flags[0];
            float posted = loop.Flags.GetWorkRemaining(flag);

            // An animal out on the map: it has no NavMeshAgent, so every metre it covers is stepped.
            DustStalkerAgent stalker = loop.SpawnStalkerAt(centre + new Vector3(-30f, 0f, -24f));
            Vector3 stalkerStart = stalker.transform.position;
            Vector3 stalkerLast = stalkerStart;
            float stalkerFarthest = 0f;
            float stalkerJump = 0f;

            float farthest = 0f;
            float worstJump = 0f;
            bool worked = false;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                foreach (SpecialistAgent a in loop.Agents)
                {
                    if (a == null || !start.ContainsKey(a)) continue;
                    Vector3 p = a.transform.position;
                    farthest = Mathf.Max(farthest, Flat(p, start[a]));
                    worstJump = Mathf.Max(worstJump, Flat(p, last[a]));
                    last[a] = p;
                }
                if (stalker != null)
                {
                    Vector3 sp = stalker.transform.position;
                    stalkerFarthest = Mathf.Max(stalkerFarthest, Flat(sp, stalkerStart));
                    stalkerJump = Mathf.Max(stalkerJump, Flat(sp, stalkerLast));
                    stalkerLast = sp;
                }
                worked = !loop.Flags.TryGet(flag.RuntimeId, out _) || loop.Flags.GetWorkRemaining(flag) < posted - 0.01f;
                if (worked && farthest > 3f && stalkerFarthest > 1f) break;
            }

            Assert.Greater(loop.Sim.Clock.TotalSteps, 20L, "the fixed step ran");
            Assert.Greater(farthest, 3f, "a robot travelled");
            Assert.IsTrue(worked, "a robot reached the flag and worked it");
            // At 4x a robot covers well under a metre a frame. A larger jump is a snap, not travel.
            Assert.Less(worstJump, 4f, "robots are drawn without teleporting between steps");
            Assert.Greater(stalkerFarthest, 1f, "an animal travelled");
            Assert.Less(stalkerJump, 2f, "and was drawn between its steps, not hopped from one to the next");
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
