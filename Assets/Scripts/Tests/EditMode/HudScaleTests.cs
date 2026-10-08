using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class HudScaleTests
    {
        [Test]
        public void AutoForHeight_Maps1080_1440_2160_And720()
        {
            Assert.AreEqual(1f, HudScaleMath.AutoForHeight(1080), 0.001f);
            Assert.AreEqual(1.35f, HudScaleMath.AutoForHeight(1440), 0.001f);
            Assert.AreEqual(2f, HudScaleMath.AutoForHeight(2160), 0.001f);
            Assert.AreEqual(0.75f, HudScaleMath.AutoForHeight(720), 0.001f);
            Assert.AreEqual(1f, HudScaleMath.AutoForHeight(0), 0.001f);
        }

        [Test]
        public void Effective_Clamps200PercentOn1080p_AndKeepsItOn4KAndUltrawide()
        {
            float fit = HudScaleMath.Effective(false, 2f, 1920, 1080);
            Assert.Less(fit, 2f);
            Assert.LessOrEqual(fit, 1080f / HudScaleMath.MinViewHeight + 0.001f);
            Assert.Greater(fit, 1.6f);

            Assert.AreEqual(2f, HudScaleMath.Effective(false, 2f, 3840, 2160), 0.001f);
            Assert.AreEqual(2f, HudScaleMath.Effective(false, 2f, 3440, 1440), 0.001f);
        }

        [Test]
        public void ExplicitChoice_PersistsApartFromAuto()
        {
            bool hadAuto = PlayerPrefs.HasKey(DemoSettings.HudAutoKey);
            int prevAuto = PlayerPrefs.GetInt(DemoSettings.HudAutoKey, 1);
            bool hadHud = PlayerPrefs.HasKey(DemoSettings.HudKey);
            float prevHud = PlayerPrefs.GetFloat(DemoSettings.HudKey, 1f);
            bool hadBoot = PlayerPrefs.HasKey(DemoSettings.BootPlayKey);
            int boot = PlayerPrefs.GetInt(DemoSettings.BootPlayKey, 0);
            try
            {
                PlayerPrefs.DeleteKey(DemoSettings.HudAutoKey);
                PlayerPrefs.Save();
                DemoSettings.Load();
                Assert.IsTrue(DemoSettings.HudScaleAuto, "A missing auto key stays on auto.");

                DemoSettings.SetHudScaleExplicit(1.5f);
                DemoSettings.SaveSettings();
                DemoSettings.HudScaleAuto = true;
                DemoSettings.HudScaleExplicit = 1f;
                DemoSettings.HudScale = 1f;
                DemoSettings.Load();
                Assert.IsFalse(DemoSettings.HudScaleAuto);
                Assert.AreEqual(1.5f, DemoSettings.HudScaleExplicit, 0.001f);
            }
            finally
            {
                if (hadAuto) PlayerPrefs.SetInt(DemoSettings.HudAutoKey, prevAuto);
                else PlayerPrefs.DeleteKey(DemoSettings.HudAutoKey);
                if (hadHud) PlayerPrefs.SetFloat(DemoSettings.HudKey, prevHud);
                else PlayerPrefs.DeleteKey(DemoSettings.HudKey);
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
