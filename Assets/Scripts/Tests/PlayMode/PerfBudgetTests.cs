using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Frame-time and GC budget on a developed Mars colony at the fastest player speed. Budgets are
    /// deliberately loose (CI runners have no GPU) and exist to catch regressions, not to certify a
    /// frame rate; the numbers are logged on every run so trends show. Override with
    /// <c>SM_PERF_FRAMES</c>, <c>SM_PERF_AVG_MS</c>, <c>SM_PERF_P95_MS</c>, <c>SM_PERF_GC_KB</c>;
    /// <c>SM_PERF_OUT</c> appends a JSON line to that file.
    /// </summary>
    [Category("Perf")]
    public class PerfBudgetTests
    {
        private readonly StabilityHarness.TempRoots _roots = new StabilityHarness.TempRoots();

        [SetUp]
        public void SetUp() => _roots.SetUp();

        [TearDown]
        public void TearDown() => _roots.TearDown();

        [UnityTest, Timeout(1800000)]
        public IEnumerator DevelopedMars_FrameTimeAndGcStayInBudget()
        {
            StabilityHarness.RequireValidationProfile();
            int frames = Mathf.Max(60, StabilityHarness.EnvInt("SM_PERF_FRAMES", 300));
            float avgBudget = StabilityHarness.EnvFloat("SM_PERF_AVG_MS", 50f);
            float p95Budget = StabilityHarness.EnvFloat("SM_PERF_P95_MS", 120f);
            float gcBudgetKb = StabilityHarness.EnvFloat("SM_PERF_GC_KB", 512f);

            using var log = new StabilityHarness.LogWatch();
            var boot = new StabilityHarness.Booted();
            yield return StabilityHarness.BootWorld(CelestialBodyId.Mars, boot);
            var loop = boot.Loop;
            int robots = StabilityHarness.EnsureStarterColony(loop);

            // Grow the colony for a minute of sim time, then hold a 30 fps step for the measurement.
            StabilityHarness.StartFastForward(loop);
            float end = Time.time + 60f;
            for (int guard = 0; Time.time < end && guard < 20000 && loop != null; guard++)
                yield return null;
            Assert.IsTrue(loop != null, "the scene reloaded while the colony grew");
            StabilityHarness.StartFastForward(loop, 1f / 30f);
            for (int i = 0; i < 30; i++)
                yield return null;

            var gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            var frameMs = new List<double>(frames);
            var gcKb = new List<double>(frames);
            bool gcFromRecorder = true;
            long lastHeap = GC.GetTotalMemory(false);
            var watch = Stopwatch.StartNew();
            try
            {
                yield return null; // the recorder reports the previous complete frame
                watch.Restart();
                for (int i = 0; i < frames; i++)
                {
                    yield return null;
                    frameMs.Add(watch.Elapsed.TotalMilliseconds);
                    watch.Restart();
                    if (gcRecorder.Valid)
                        gcKb.Add(gcRecorder.LastValue / 1024.0);
                    else
                    {
                        // Approximate: heap growth between frames, floored at zero across a collection.
                        gcFromRecorder = false;
                        long heap = GC.GetTotalMemory(false);
                        gcKb.Add(Math.Max(0L, heap - lastHeap) / 1024.0);
                        lastHeap = heap;
                    }
                }
            }
            finally
            {
                gcRecorder.Dispose();
            }

            int objects = StabilityHarness.CountGameObjects();
            int living = StabilityHarness.CountLiving(loop);
            int fauna = loop.Stalkers.Count;
            StabilityHarness.StopFastForward(loop);

            double avg = Average(frameMs), p95 = Percentile(frameMs, 0.95), max = Percentile(frameMs, 1.0);
            double gcAvg = Average(gcKb), gcP95 = Percentile(gcKb, 0.95);
            string line = string.Format(CultureInfo.InvariantCulture,
                "[Perf] Mars frames={0} avgMs={1:F2} p95Ms={2:F2} maxMs={3:F2} gcKbAvg={4:F1} gcKbP95={5:F1} gcSource={6} " +
                "robots={7} fauna={8} objects={9} timeScale={10:F2} budgets(avg={11} p95={12} gcKb={13})",
                frames, avg, p95, max, gcAvg, gcP95, gcFromRecorder ? "recorder" : "heap-delta",
                living, fauna, objects, Time.timeScale, avgBudget, p95Budget, gcBudgetKb);
            Debug.Log(line);
            TestContext.WriteLine(line);
            WriteTrendLine(avg, p95, max, gcAvg, gcP95, living, fauna, objects, robots);

            Assert.AreEqual(0, log.ErrorCount, "errors logged during the perf run:\n" + log.Summary());
            Assert.LessOrEqual(avg, avgBudget, "average frame time over budget. " + line);
            Assert.LessOrEqual(p95, p95Budget, "95th percentile frame time over budget. " + line);
            Assert.LessOrEqual(gcAvg, gcBudgetKb, "average GC allocation per frame over budget. " + line);
        }

        private static void WriteTrendLine(double avg, double p95, double max, double gcAvg, double gcP95,
            int robots, int fauna, int objects, int starterRobots)
        {
            string path = Environment.GetEnvironmentVariable("SM_PERF_OUT");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                string json = string.Format(CultureInfo.InvariantCulture,
                    "{{\"test\":\"DevelopedMars\",\"utc\":\"{0:yyyy-MM-ddTHH:mm:ssZ}\",\"version\":\"{1}\",\"unity\":\"{2}\"," +
                    "\"avgMs\":{3:F3},\"p95Ms\":{4:F3},\"maxMs\":{5:F3},\"gcKbAvg\":{6:F2},\"gcKbP95\":{7:F2}," +
                    "\"robots\":{8},\"fauna\":{9},\"objects\":{10},\"starterRobots\":{11}}}\n",
                    DateTime.UtcNow, Application.version, Application.unityVersion,
                    avg, p95, max, gcAvg, gcP95, robots, fauna, objects, starterRobots);
                System.IO.File.AppendAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Perf] Could not append to {path}: {e.Message}");
            }
        }

        private static double Average(List<double> values)
        {
            if (values.Count == 0) return 0;
            double sum = 0;
            foreach (double v in values) sum += v;
            return sum / values.Count;
        }

        private static double Percentile(List<double> values, double p)
        {
            if (values.Count == 0) return 0;
            var sorted = new List<double>(values);
            sorted.Sort();
            int index = (int)Math.Ceiling(p * sorted.Count) - 1;
            return sorted[Mathf.Clamp(index, 0, sorted.Count - 1)];
        }
    }
}
