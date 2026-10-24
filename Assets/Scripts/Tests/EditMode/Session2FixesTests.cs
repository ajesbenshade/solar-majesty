using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class Session2FixesTests
    {
        [Test]
        public void LunarRocket_IsResearchable_WhenFullCampaignIsOn()
        {
            bool prior = DemoSettings.FirstHourDemo;
            try
            {
                DemoSettings.FirstHourDemo = false;
                Assert.IsTrue(DemoSlice.ShowTech(TechId.LunarRocket));
                var def = TechCatalog.Get(TechId.LunarRocket);
                Assert.IsNotNull(def);
                Assert.IsNotNull(def.Prerequisites);
                Assert.Greater(def.Prerequisites.Length, 0);
                for (int i = 0; i < def.Prerequisites.Length; i++)
                    Assert.IsTrue(DemoSlice.ShowTech(def.Prerequisites[i]), def.Prerequisites[i].ToString());

                var unlocked = new HashSet<TechId>(def.Prerequisites);
                Assert.IsTrue(LaunchPath.CanResearch(TechId.LunarRocket, unlocked));
                Assert.IsFalse(LaunchPath.CanResearch(TechId.LunarRocket, new HashSet<TechId>()));
                string missing = LaunchPath.UnmetPrerequisites(TechId.LunarRocket, new HashSet<TechId>());
                StringAssert.Contains("Life Support", missing);
                StringAssert.Contains("Ore Refining", missing);
                StringAssert.Contains("Power Systems", missing);
            }
            finally
            {
                DemoSettings.FirstHourDemo = prior;
            }
        }

        [Test]
        public void LaunchObjective_NamesTheChainAndToggle_WhenFullCampaignIsOff()
        {
            string text = LaunchPath.ObjectiveText(false, false, "Lunar Rocket");
            StringAssert.Contains("Lunar Rocket", text);
            StringAssert.Contains("research", text);
            StringAssert.Contains("launch", text);
            StringAssert.Contains("Settings → Guided Start → Skip", text);
            StringAssert.Contains("Clear Earth to open Luna", text);

            string ready = LaunchPath.ObjectiveText(false, true, "Lunar Rocket");
            StringAssert.Contains("Settings → Guided Start → Skip", ready);

            Assert.AreEqual("Lunar Rocket research → launch", LaunchPath.ObjectiveText(true, false, "Lunar Rocket"));
            Assert.AreEqual("ready on pad", LaunchPath.ObjectiveText(true, true, "Lunar Rocket"));
        }

        [Test]
        public void IdenticalNotices_CollapseInsideTheWindow()
        {
            Assert.IsTrue(NoticeDedupe.IsRepeat("purse", 10f, "purse", 11f, NoticeDedupe.WindowSeconds));
            Assert.IsFalse(NoticeDedupe.IsRepeat("purse", 10f, "purse", 10f + NoticeDedupe.WindowSeconds, NoticeDedupe.WindowSeconds));
            Assert.IsFalse(NoticeDedupe.IsRepeat("purse", 10f, "other", 11f, NoticeDedupe.WindowSeconds));
            Assert.IsFalse(NoticeDedupe.IsRepeat(null, 10f, "purse", 11f, NoticeDedupe.WindowSeconds));
        }

        [Test]
        public void Continue_HoldsAndResumesAtOneX_WithoutClearingTheSavedSpeed()
        {
            const string key = "SM_GameSpeed_v2";
            bool had = GamePrefs.HasKey(key);
            int prev = GamePrefs.GetInt(key, 0);
            try
            {
                int fast = System.Array.IndexOf(SimSpeed.Multipliers, 4f);
                Assert.GreaterOrEqual(fast, 0);
                SimSpeed.Set(fast);
                Assert.AreEqual(4f, SimSpeed.Multiplier);

                SimSpeed.HoldAfterRestore();
                Assert.IsTrue(SimSpeed.IsPaused);
                SimSpeed.Resume();
                Assert.AreEqual(1f, SimSpeed.Multiplier, "Space after Continue resumes at 1×");

                SimSpeed.Load();
                Assert.AreEqual(4f, SimSpeed.Multiplier, "the saved pace is still there for a new campaign");
            }
            finally
            {
                if (had) GamePrefs.SetInt(key, prev);
                else GamePrefs.DeleteKey(key);
                GamePrefs.Save();
                SimSpeed.Load();
            }
        }

        [Test]
        public void PrefsShield_RestoresCampaignAndDemoFlags()
        {
            bool hadDemo = GamePrefs.HasKey(DemoSettings.FirstHourKey);
            int demo = GamePrefs.GetInt(DemoSettings.FirstHourKey, 1);
            bool hadMax = GamePrefs.HasKey("SM_CampaignMaxBody");
            int max = GamePrefs.GetInt("SM_CampaignMaxBody", 0);
            try
            {
                DeveloperPrefsShield.Capture();
                GamePrefs.SetInt(DemoSettings.FirstHourKey, demo == 1 ? 0 : 1);
                GamePrefs.SetInt("SM_CampaignMaxBody", (int)CelestialBodyId.Europa);
                GamePrefs.Save();
                DemoSettings.FirstHourDemo = false;
                CampaignProgress.UnlockThrough(CelestialBodyId.Europa);

                DeveloperPrefsShield.Restore();

                if (hadDemo) Assert.AreEqual(demo, GamePrefs.GetInt(DemoSettings.FirstHourKey));
                else Assert.IsFalse(GamePrefs.HasKey(DemoSettings.FirstHourKey));
                if (hadMax) Assert.AreEqual(max, GamePrefs.GetInt("SM_CampaignMaxBody"));
                else Assert.IsFalse(GamePrefs.HasKey("SM_CampaignMaxBody"));
                Assert.AreEqual(hadDemo ? demo == 1 : true, DemoSettings.FirstHourDemo);
            }
            finally
            {
                if (hadDemo) GamePrefs.SetInt(DemoSettings.FirstHourKey, demo);
                else GamePrefs.DeleteKey(DemoSettings.FirstHourKey);
                if (hadMax) GamePrefs.SetInt("SM_CampaignMaxBody", max);
                else GamePrefs.DeleteKey("SM_CampaignMaxBody");
                GamePrefs.Save();
                DemoSettings.ReloadFlagsFromPrefs();
                CampaignProgress.ReloadFromPrefs();
            }
        }
    }
}
