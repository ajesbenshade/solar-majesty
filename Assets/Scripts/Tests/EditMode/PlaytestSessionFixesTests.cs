using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class PlaytestSessionFixesTests
    {
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
