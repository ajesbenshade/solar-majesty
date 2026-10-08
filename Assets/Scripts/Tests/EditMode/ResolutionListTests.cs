using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class ResolutionListTests
    {
        [Test]
        public void Collect_IncludesNativeAndReportedModes_DedupedBySize()
        {
            var reported = new[]
            {
                new Resolution { width = 1920, height = 1080 },
                new Resolution { width = 1920, height = 1080 },
                new Resolution { width = 2560, height = 1440 },
                new Resolution { width = 3440, height = 1440 }
            };
            var modes = DemoSettings.CollectResolutions(3440, 1440, 3440, 1440, reported);

            Assert.AreEqual(1, Count(modes, 1920, 1080));
            Assert.AreEqual(1, Count(modes, 2560, 1440));
            Assert.AreEqual(1, Count(modes, 3440, 1440));
            Assert.Greater(Mathf.Abs(3440f / 1440f - 16f / 9f), 0.02f, "ultrawide is not a 16:9 mode");
        }

        [Test]
        public void Collect_KeepsSystemSizeWhenItIsNotInTheReportedList()
        {
            var reported = new[] { new Resolution { width = 1920, height = 1080 } };
            var modes = DemoSettings.CollectResolutions(1920, 1080, 3440, 1440, reported);
            Assert.AreEqual(1, Count(modes, 3440, 1440));
            Assert.AreEqual(1, Count(modes, 1920, 1080));
        }

        [Test]
        public void EmptyReport_FallsBackToPresets()
        {
            var modes = DemoSettings.CollectResolutions(0, 0, 0, 0, null);
            Assert.Greater(modes.Count, 0);
            Assert.AreEqual(1, Count(modes, 1920, 1080));
            Assert.AreEqual(1, Count(modes, 3840, 2160));
        }

        [Test]
        public void Collect_DropsModesBelow720p()
        {
            var modes = DemoSettings.CollectResolutions(3840, 2160, 3840, 2160, new[]
            {
                new Resolution { width = 640, height = 480 },
                new Resolution { width = 800, height = 600 },
                new Resolution { width = 1920, height = 1080 },
                new Resolution { width = 3840, height = 2160 }
            });
            Assert.AreEqual(0, Count(modes, 640, 480));
            Assert.AreEqual(0, Count(modes, 800, 600));
            Assert.AreEqual(1, Count(modes, 1920, 1080));
            Assert.AreEqual(1, Count(modes, 3840, 2160));
            Assert.IsFalse(DemoSettings.IsListedResolution(640, 480));
            Assert.IsTrue(DemoSettings.IsListedResolution(1280, 720));
        }

        [Test]
        public void LeavingDisplay_PrefersTheNativeSize()
        {
            var modes = DemoSettings.CollectResolutions(3440, 1440, 3440, 1440, new[]
            {
                new Resolution { width = 1280, height = 720 },
                new Resolution { width = 1920, height = 1080 },
                new Resolution { width = 3440, height = 1440 }
            });
            int first = DemoSettings.FirstExplicitIndex(modes, 3440, 1440);
            Assert.AreEqual(3440, modes[first].Width);
            Assert.AreEqual(1440, modes[first].Height);
        }

        static int Count(System.Collections.Generic.List<DemoSettings.DisplayMode> modes, int w, int h)
        {
            int n = 0;
            for (int i = 0; i < modes.Count; i++)
                if (modes[i].Width == w && modes[i].Height == h) n++;
            return n;
        }
    }
}
