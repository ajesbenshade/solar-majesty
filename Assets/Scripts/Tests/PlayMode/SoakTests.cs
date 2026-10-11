using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Long-run stability per world: boot, stand up a starter colony, then fast-forward
    /// <c>SM_SOAK_MINUTES</c> of sim time (default 10) at the fastest player speed. Fails on any
    /// error/exception/assert in the log, a NaN pose, a robot stuck on a live path, or a runaway
    /// GameObject count. Runs only in the validation profile or CI (it resets the campaign).
    /// </summary>
    [Category("Soak")]
    public class SoakTests
    {
        private readonly StabilityHarness.TempRoots _roots = new StabilityHarness.TempRoots();

        private static IEnumerable<CelestialBodyId> Worlds => StabilityHarness.Worlds;

        [SetUp]
        public void SetUp() => _roots.SetUp();

        [TearDown]
        public void TearDown() => _roots.TearDown();

        [UnityTest, Timeout(3600000)]
        public IEnumerator World_RunsAtSpeedWithoutErrorsNaNsStuckRobotsOrLeaks(
            [ValueSource(nameof(Worlds))] CelestialBodyId body)
        {
            StabilityHarness.RequireValidationProfile();
            float minutes = Mathf.Max(0.5f, StabilityHarness.EnvFloat("SM_SOAK_MINUTES", 10f));
            float stuckSeconds = Mathf.Max(30f, StabilityHarness.EnvFloat("SM_SOAK_STUCK_SECONDS", 120f));
            float growthRatio = Mathf.Max(1.1f, StabilityHarness.EnvFloat("SM_SOAK_MAX_OBJECT_GROWTH", 2f));
            int growthSlack = Mathf.Max(0, StabilityHarness.EnvInt("SM_SOAK_OBJECT_SLACK", 1500));
            const float sampleEvery = 5f;
            const float countEvery = 30f;

            using var log = new StabilityHarness.LogWatch();
            var boot = new StabilityHarness.Booted();
            yield return StabilityHarness.BootWorld(body, boot);
            var loop = boot.Loop;
            int robots = StabilityHarness.EnsureStarterColony(loop);
            StabilityHarness.StartFastForward(loop);

            // Let construction, nav mesh and first spawns settle before the object baseline.
            float warmEnd = Time.time + 10f;
            for (int guard = 0; Time.time < warmEnd && guard < 2000; guard++)
                yield return null;
            int baseline = StabilityHarness.CountGameObjects();
            int peak = baseline;
            int bound = Mathf.CeilToInt(baseline * growthRatio) + growthSlack;

            var stuck = new StabilityHarness.StuckWatch(stuckSeconds);
            var nanHits = new List<string>();
            float target = minutes * 60f;
            float start = Time.time;
            float nextSample = 0f, nextCount = countEvery;
            bool endedAtTitle = false;
            int frames = 0, resumes = 0, reloads = 0, peakRobots = robots, peakFauna = 0;
            float desiredScale = StabilityHarness.DesiredTimeScale();
            float realStart = Time.realtimeSinceStartup;
            // Generous frame cap: a frame advances at least 0.05 s × scale unless the game holds itself.
            int frameCap = Mathf.CeilToInt(target / (0.05f * desiredScale) * 3f) + 5000;

            while (Time.time - start < target && frames < frameCap)
            {
                yield return null;
                frames++;
                if (loop == null)
                {
                    // Mission retry or world change reloaded the scene; carry on with the new loop.
                    loop = Object.FindFirstObjectByType<GameLoop>();
                    if (loop == null) continue;
                    reloads++;
                    StabilityHarness.StartFastForward(loop);
                }
                if (loop.Screen == DemoScreen.Title)
                {
                    // Defeat or a flow back to the title: game flow, not a stability fault. Stop here.
                    endedAtTitle = true;
                    break;
                }
                if (!loop.IsPlaying || SimSpeed.IsPaused)
                {
                    resumes++;
                    StabilityHarness.StartFastForward(loop);
                }
                else if (!Mathf.Approximately(Time.timeScale, desiredScale))
                    Time.timeScale = desiredScale;

                float t = Time.time - start;
                if (t >= nextSample)
                {
                    nextSample = t + sampleEvery;
                    stuck.Sample(loop, t);
                    if (nanHits.Count < 10) nanHits.AddRange(StabilityHarness.NonFinitePoses(loop));
                    peakRobots = Mathf.Max(peakRobots, StabilityHarness.CountLiving(loop));
                    peakFauna = Mathf.Max(peakFauna, loop.Stalkers.Count);
                }
                if (t >= nextCount)
                {
                    nextCount = t + countEvery;
                    peak = Mathf.Max(peak, StabilityHarness.CountGameObjects());
                }
            }

            float simulated = Time.time - start;
            int final = StabilityHarness.CountGameObjects();
            peak = Mathf.Max(peak, final);
            StabilityHarness.StopFastForward(loop);
            float real = Time.realtimeSinceStartup - realStart;

            string summary =
                $"[Soak] {body}: sim {simulated / 60f:F1} min in {real:F0} s real over {frames} frames " +
                $"(×{desiredScale:F2}); robots start {robots} peak {peakRobots}; fauna peak {peakFauna}; " +
                $"objects base {baseline} peak {peak} final {final} (bound {bound}); " +
                $"errors {log.ErrorCount}; warnings {log.WarningCount}; stuck {stuck.Stuck.Count}; " +
                $"NaN {nanHits.Count}; off-mesh samples {stuck.OffMeshSamples}; self-holds {resumes}; reloads {reloads}" +
                (endedAtTitle ? "; ended early at the title screen" : "");
            Debug.Log(summary);
            TestContext.WriteLine(summary);

            var failures = new List<string>();
            if (!endedAtTitle && simulated < target * 0.95f)
                failures.Add($"only {simulated:F0}s of the {target:F0}s soak ran before the frame cap ({frameCap} frames)");
            if (log.ErrorCount > 0)
                failures.Add("errors logged:\n" + log.Summary());
            if (nanHits.Count > 0)
                failures.Add("non-finite poses:\n  - " + string.Join("\n  - ", nanHits));
            if (stuck.Stuck.Count > 0)
                failures.Add("robots stuck on a live path:\n  - " + string.Join("\n  - ", stuck.Stuck));
            if (peak > bound)
                failures.Add($"GameObject count grew from {baseline} to {peak} (bound {bound}): something is leaking");
            if (failures.Count > 0)
                Assert.Fail($"{body} soak failed:\n" + string.Join("\n", failures) + "\n" + summary);
        }
    }
}
