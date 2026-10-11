using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Save→load→save on a colony grown by play rather than a hand-written fixture
    /// (<see cref="DevelopedColonySaveTests"/> covers the fixture fields). Every field the snapshot
    /// carries is compared by <see cref="SaveSnapshotDiff"/>, twice, and the restored colony must
    /// then keep running cleanly.
    /// </summary>
    public class SaveRoundTripTests
    {
        private readonly StabilityHarness.TempRoots _roots = new StabilityHarness.TempRoots();

        [SetUp]
        public void SetUp() => _roots.SetUp();

        [TearDown]
        public void TearDown() => _roots.TearDown();

        [UnityTest, Timeout(1800000)]
        public IEnumerator PlayedMarsColony_RoundTripsEveryFieldTwice_AndKeepsRunning()
        {
            StabilityHarness.RequireValidationProfile();
            const CelestialBodyId body = CelestialBodyId.Mars;
            float growSeconds = Mathf.Max(30f, StabilityHarness.EnvFloat("SM_ROUNDTRIP_SECONDS", 180f));

            using var log = new StabilityHarness.LogWatch();
            var boot = new StabilityHarness.Booted();
            yield return StabilityHarness.BootWorld(body, boot);
            var loop = boot.Loop;
            int robots = StabilityHarness.EnsureStarterColony(loop);
            Assert.GreaterOrEqual(robots, 1, "starter colony needs at least one robot to make the snapshot interesting");

            StabilityHarness.StartFastForward(loop);
            float end = Time.time + growSeconds;
            for (int guard = 0; Time.time < end && guard < 100000 && loop != null && loop.Screen != DemoScreen.Title; guard++)
            {
                yield return null;
                if (!loop.IsPlaying) StabilityHarness.StartFastForward(loop);
            }
            Assert.IsTrue(loop != null, "the scene reloaded while the colony grew");
            StabilityHarness.StopFastForward(loop);
            Assert.AreEqual(DemoScreen.Paused, loop.Screen);

            var expected = loop.CaptureSave("roundtrip");
            Assert.AreEqual((int)body, expected.body);
            Assert.Greater(expected.buildings.Count, 1, "the grown colony has more than its Commons");
            Assert.Greater(expected.simSteps, 0, "the sim advanced");
            string shape = $"{expected.buildings.Count} buildings, {expected.agents.Count} robots, " +
                           $"{expected.fauna.Count} fauna, {expected.lairs.Count} dens, {expected.flags.Count} flags, " +
                           $"{expected.nodes.Count} nodes, metals {expected.stockpile.metals}, steps {expected.simSteps}";
            Debug.Log("[RoundTrip] grown snapshot: " + shape);
            TestContext.WriteLine("[RoundTrip] grown snapshot: " + shape);

            for (int round = 1; round <= 2; round++)
            {
                Assert.IsTrue(SaveSystem.WriteWorld(expected), $"round {round}: world write");
                Assert.IsTrue(SaveSystem.Write(SaveSystem.AutosaveSlot, expected), $"round {round}: autosave write");
                yield return SceneManager.LoadSceneAsync(StabilityHarness.Scene);
                yield return null;
                loop = Object.FindFirstObjectByType<GameLoop>();
                Assert.IsNotNull(loop);
                loop.ContinueGame();
                Assert.AreNotEqual(DemoScreen.Title, loop.Screen, $"round {round}: Continue stayed on the title ({DemoSettings.ContinueDetail()})");
                loop.PausePlay();
                Assert.AreEqual(body, BodySeed.Body, $"round {round}: Continue landed on the saved world");

                var actual = loop.CaptureSave("roundtrip");
                List<string> diffs = SaveSnapshotDiff.Compare(ExpectedAfterLoad(expected), actual);
                if (diffs.Count > 0)
                    Assert.Fail($"round {round}: {diffs.Count} field(s) changed across save→load:\n  - " +
                                string.Join("\n  - ", diffs.GetRange(0, Mathf.Min(40, diffs.Count))));
                expected = actual;
            }

            // The restored colony must keep running: no errors, no NaN poses.
            StabilityHarness.StartFastForward(loop);
            end = Time.time + 30f;
            for (int guard = 0; Time.time < end && guard < 20000 && loop != null; guard++)
                yield return null;
            Assert.IsTrue(loop != null, "the scene reloaded after restore");
            var nan = StabilityHarness.NonFinitePoses(loop);
            StabilityHarness.StopFastForward(loop);
            CollectionAssert.IsEmpty(nan, "non-finite poses after restore");
            if (log.ErrorCount > 0)
                Assert.Fail("errors logged during the round trip:\n" + log.Summary());
        }

        /// <summary>
        /// The snapshot as Continue is designed to restore it. Fauna already dead at save time and
        /// fauna beyond <see cref="OverseerRules.MaxFaunaPerDen"/> per den are not respawned
        /// (<see cref="FaunaRestoreCap"/>), and a downed robot does not rebind its soft claim.
        /// </summary>
        private static SaveGame ExpectedAfterLoad(SaveGame saved)
        {
            var copy = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(saved));
            // JsonUtility turns null sections into empty ones; keep "absent" absent for the compare.
            if (saved.villageGrowth == null) copy.villageGrowth = null;
            if (saved.collectors == null) copy.collectors = null;
            if (saved.orbital == null) copy.orbital = null;
            int n = copy.fauna.Count;
            var lairIndex = new int[n];
            var health = new float[n];
            for (int i = 0; i < n; i++)
            {
                lairIndex[i] = copy.fauna[i].lairIndex;
                health[i] = copy.fauna[i].health;
            }
            bool[] keep = FaunaRestoreCap.Keep(lairIndex, health, OverseerRules.MaxFaunaPerDen);
            for (int i = n - 1; i >= 0; i--)
                if (!keep[i] || copy.fauna[i].health <= 0.001f)
                    copy.fauna.RemoveAt(i);

            foreach (var agent in copy.agents)
            {
                if (!agent.downed || agent.claimedFlagIndex < 0) continue;
                if (agent.claimedFlagIndex < copy.flags.Count)
                    copy.flags[agent.claimedFlagIndex].claimCount =
                        Mathf.Max(0, copy.flags[agent.claimedFlagIndex].claimCount - 1);
                agent.claimedFlagIndex = -1;
            }
            return copy;
        }
    }
}
