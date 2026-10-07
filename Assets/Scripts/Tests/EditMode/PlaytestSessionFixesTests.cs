using System.IO;
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
        public void NewCampaign_ReplacesOnlyTheChosenSlot()
        {
            string root = Path.Combine(Path.GetTempPath(), "sm-saves-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string previous = SaveSystem.DirectoryOverride;
            try
            {
                SaveSystem.DirectoryOverride = root;
                for (int i = 0; i < SaveSystem.SlotCount; i++)
                {
                    Assert.IsTrue(SaveSystem.Write(i, new SaveGame
                    {
                        body = (int)CelestialBodyId.Earth,
                        seed = 1000 + i,
                        label = "slot" + i
                    }));
                }
                Assert.IsTrue(SaveSystem.WriteWorld(new SaveGame
                {
                    body = (int)CelestialBodyId.Earth,
                    seed = 50,
                    label = "earth"
                }));

                Assert.AreEqual(2, CampaignSlots.SlotReplacedByNewCampaign(2));
                Assert.AreEqual(1, CampaignSlots.SlotReplacedByNewCampaign(0));
                Assert.AreEqual(1, CampaignSlots.SlotReplacedByNewCampaign(99));
                CampaignSlots.ReplaceChosenSlot(2);

                Assert.IsFalse(SaveSystem.Exists(2), "the chosen slot is the one that is replaced");
                Assert.IsTrue(SaveSystem.Exists(0));
                Assert.IsTrue(SaveSystem.Exists(1));
                Assert.IsTrue(SaveSystem.Exists(3));
                Assert.IsTrue(SaveSystem.TryReadWorld(CelestialBodyId.Earth, out var world));
                Assert.AreEqual("earth", world.label);
                Assert.IsTrue(CampaignSlots.StampsMatch(null, "legacy"));
                Assert.IsTrue(CampaignSlots.StampsMatch("", "legacy"));
                Assert.IsTrue(CampaignSlots.StampsMatch("abc", "abc"));
                Assert.IsFalse(CampaignSlots.StampsMatch("abc", ""));
                Assert.IsFalse(CampaignSlots.StampsMatch("abc", "def"));
            }
            finally
            {
                SaveSystem.DirectoryOverride = previous;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void GameLoop_NewCampaignDoesNotDeleteEverySave()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Runtime/GameLoop.cs");
            string text = File.ReadAllText(path);
            Assert.IsFalse(text.Contains("SaveSystem.DeleteAll("),
                "New Campaign must not wipe every slot");
        }

        [Test]
        public void Escape_CancelsBuildOrFlagPlacement_AndPausesWhenNothingIsArmed()
        {
            Assert.AreEqual(SessionHotkeys.EscapeAction.CancelPlacement,
                SessionHotkeys.OnEscape(false, false, true, true));
            Assert.AreEqual(SessionHotkeys.EscapeAction.TogglePause,
                SessionHotkeys.OnEscape(false, false, true, false));
            Assert.AreEqual(SessionHotkeys.EscapeAction.TogglePause,
                SessionHotkeys.OnEscape(false, false, false, true),
                "Esc on the pause screen still resumes, even if a tool was armed");
            Assert.AreEqual(SessionHotkeys.EscapeAction.CloseSettings,
                SessionHotkeys.OnEscape(true, false, false, true));
            Assert.AreEqual(SessionHotkeys.EscapeAction.Ignore,
                SessionHotkeys.OnEscape(false, true, false, false));
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
