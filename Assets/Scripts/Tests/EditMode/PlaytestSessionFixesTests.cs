using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class PlaytestSessionFixesTests
    {
        [Test]
        public void Resolution_CyclesPresets_AndWraps()
        {
            int w = DemoSettings.ResolutionWidth;
            int h = DemoSettings.ResolutionHeight;
            try
            {
                DemoSettings.ResolutionWidth = 0;
                DemoSettings.ResolutionHeight = 0;
                Assert.AreEqual("DISPLAY", DemoSettings.ResolutionLabel);
                DemoSettings.StepResolution();
                Assert.AreEqual(1280, DemoSettings.ResolutionWidth);
                Assert.AreEqual(720, DemoSettings.ResolutionHeight);

                DemoSettings.StepResolution();
                Assert.AreEqual(1280, DemoSettings.ResolutionWidth);
                Assert.AreEqual(800, DemoSettings.ResolutionHeight);

                int last = DemoSettings.ResolutionWidths.Length - 1;
                DemoSettings.ResolutionWidth = DemoSettings.ResolutionWidths[last];
                DemoSettings.ResolutionHeight = DemoSettings.ResolutionHeights[last];
                DemoSettings.StepResolution();
                Assert.AreEqual(DemoSettings.ResolutionWidths[0], DemoSettings.ResolutionWidth);
                Assert.AreEqual(DemoSettings.ResolutionHeights[0], DemoSettings.ResolutionHeight);
                Assert.AreEqual(1920, DemoSettings.ResolutionWidths[4]);
                Assert.AreEqual(1080, DemoSettings.ResolutionHeights[4]);
            }
            finally
            {
                DemoSettings.ResolutionWidth = w;
                DemoSettings.ResolutionHeight = h;
            }
        }

        [Test]
        public void Resolution_PersistsAcrossLoad()
        {
            bool hadW = PlayerPrefs.HasKey(DemoSettings.ResolutionWKey);
            bool hadH = PlayerPrefs.HasKey(DemoSettings.ResolutionHKey);
            int prevW = PlayerPrefs.GetInt(DemoSettings.ResolutionWKey, 0);
            int prevH = PlayerPrefs.GetInt(DemoSettings.ResolutionHKey, 0);
            bool hadBoot = PlayerPrefs.HasKey(DemoSettings.BootPlayKey);
            int boot = PlayerPrefs.GetInt(DemoSettings.BootPlayKey, 0);
            try
            {
                DemoSettings.ResolutionWidth = 1920;
                DemoSettings.ResolutionHeight = 1080;
                DemoSettings.SaveSettings();
                DemoSettings.ResolutionWidth = 0;
                DemoSettings.ResolutionHeight = 0;
                DemoSettings.Load();
                Assert.AreEqual(1920, DemoSettings.ResolutionWidth);
                Assert.AreEqual(1080, DemoSettings.ResolutionHeight);
                Assert.AreEqual("1920×1080", DemoSettings.ResolutionLabel);
            }
            finally
            {
                if (hadW) PlayerPrefs.SetInt(DemoSettings.ResolutionWKey, prevW);
                else PlayerPrefs.DeleteKey(DemoSettings.ResolutionWKey);
                if (hadH) PlayerPrefs.SetInt(DemoSettings.ResolutionHKey, prevH);
                else PlayerPrefs.DeleteKey(DemoSettings.ResolutionHKey);
                if (hadBoot) PlayerPrefs.SetInt(DemoSettings.BootPlayKey, boot);
                else PlayerPrefs.DeleteKey(DemoSettings.BootPlayKey);
                PlayerPrefs.Save();
                DemoSettings.Load();
                if (hadBoot) PlayerPrefs.SetInt(DemoSettings.BootPlayKey, boot);
                else PlayerPrefs.DeleteKey(DemoSettings.BootPlayKey);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void EdgeScroll_DefaultsOn_AndKeepsAnExplicitOff()
        {
            Assert.IsTrue(DemoSettings.ResolveEdgeScroll(false, 0));
            Assert.IsFalse(DemoSettings.ResolveEdgeScroll(true, 0));
            Assert.IsTrue(DemoSettings.ResolveEdgeScroll(true, 1));
        }

        [Test]
        public void EdgeScroll_Load_UsesTheSavedChoiceWhenOneExists()
        {
            bool had = PlayerPrefs.HasKey(DemoSettings.EdgeScrollKey);
            int prev = PlayerPrefs.GetInt(DemoSettings.EdgeScrollKey, 0);
            bool hadBoot = PlayerPrefs.HasKey(DemoSettings.BootPlayKey);
            int boot = PlayerPrefs.GetInt(DemoSettings.BootPlayKey, 0);
            try
            {
                PlayerPrefs.DeleteKey(DemoSettings.EdgeScrollKey);
                DemoSettings.Load();
                Assert.IsTrue(DemoSettings.EdgeScroll, "no saved choice means edge scroll is on");

                PlayerPrefs.SetInt(DemoSettings.EdgeScrollKey, 0);
                DemoSettings.Load();
                Assert.IsFalse(DemoSettings.EdgeScroll, "an explicit off stays off");

                PlayerPrefs.SetInt(DemoSettings.EdgeScrollKey, 1);
                DemoSettings.Load();
                Assert.IsTrue(DemoSettings.EdgeScroll);
            }
            finally
            {
                if (had) PlayerPrefs.SetInt(DemoSettings.EdgeScrollKey, prev);
                else PlayerPrefs.DeleteKey(DemoSettings.EdgeScrollKey);
                if (hadBoot) PlayerPrefs.SetInt(DemoSettings.BootPlayKey, boot);
                else PlayerPrefs.DeleteKey(DemoSettings.BootPlayKey);
                PlayerPrefs.Save();
                DemoSettings.Load();
                if (hadBoot) PlayerPrefs.SetInt(DemoSettings.BootPlayKey, boot);
                else PlayerPrefs.DeleteKey(DemoSettings.BootPlayKey);
                PlayerPrefs.Save();
            }
        }
    }
}
