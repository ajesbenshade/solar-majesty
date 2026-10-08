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
            // SaveSettings also writes the UI-scale auto flag; put it back too.
            bool hadHudAuto = PlayerPrefs.HasKey(DemoSettings.HudAutoKey);
            int prevHudAuto = PlayerPrefs.GetInt(DemoSettings.HudAutoKey, 1);
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
                if (hadHudAuto) PlayerPrefs.SetInt(DemoSettings.HudAutoKey, prevHudAuto);
                else PlayerPrefs.DeleteKey(DemoSettings.HudAutoKey);
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
            Assert.AreEqual(SessionHotkeys.EscapeAction.CloseResearch,
                SessionHotkeys.OnEscape(false, false, true, true, researchOpen: true),
                "Esc closes research before it cancels a placement or pauses");
            Assert.AreEqual(SessionHotkeys.EscapeAction.CancelPlacement,
                SessionHotkeys.OnEscape(false, false, true, true, researchOpen: false));
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

        [Test]
        public void PlaytestNotes_MatchCurrentControls()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Editor/PlaytestHandoff.cs");
            string text = File.ReadAllText(path);
            Assert.That(text, Does.Contain("While placing a building or flag"));
            Assert.That(text, Does.Contain("Space"));
            Assert.That(text, Does.Contain(", / ."));
            Assert.That(text, Does.Contain("speed buttons"));
            Assert.That(text, Does.Contain("selected flag's bounty"));
            Assert.That(text, Does.Contain("These are not speed"));
        }

        [Test]
        public void Hud_LabelsStayApart_AtPlaytestResolutions()
        {
            AssertHud(1280f, 800f);
            AssertHud(1920f, 1080f);
            AssertHud(3440f, 1440f);
            AssertHud(3840f, 2160f);
            // 1.25 HUD scale on 1280×800 shrinks the dock; the catalog still clears the crest.
            AssertHud(1280f / 1.25f, 800f / 1.25f);
        }

        static void AssertHud(float viewW, float viewH)
        {
            Rect crest = HudLayout.Crest(viewW, viewH);
            var plate = new Rect(
                crest.x - HudLayout.CrestPad, crest.y - HudLayout.CrestPad,
                crest.width + HudLayout.CrestPad * 2f, crest.height + HudLayout.CrestPad * 2f);
            Rect catalog = HudLayout.ToolPopup(viewW, viewH, 320f, 400f);
            AssertLeftOf(catalog, plate, HudLayout.PanelGap);
            Assert.LessOrEqual(catalog.yMax + HudLayout.PanelGap, plate.yMin + 0.01f);

            HudLayout.Tutorial(viewW, 60f, out var bar, out var text, out var skip);
            AssertLeftOf(text, skip, HudLayout.PanelGap);
            AssertLeftOf(bar, HudLayout.Objectives(viewW), 12f);

            var chip = new Rect(0f, 0f, 119f, 28f);
            HudLayout.ChipText(chip, out var chipLabel, out var chipNumber);
            AssertLeftOf(chipLabel, chipNumber, HudLayout.TextGap);
            Assert.Greater(chipLabel.width, 8f);

            HudLayout.SplitRow(0f, 0f, 240f, 24f, 26f, 80f, out var rowName, out var rowCost);
            AssertLeftOf(rowName, rowCost, HudLayout.PanelGap);

            var clock = new Rect(0f, 40f, 186f, 76f);
            HudLayout.ClockHeader(clock, out var sol, out var tag, out var speed);
            Assert.AreEqual(116f, speed.width);
            AssertLeftOf(sol, speed, HudLayout.TextGap);
            AssertLeftOf(tag, speed, HudLayout.TextGap);

            HudLayout.StatLine(new Rect(0f, 0f, 186f, 13f), out var statLabel, out var statValue);
            AssertLeftOf(statLabel, statValue, HudLayout.TextGap);

            HudLayout.Stake(new Rect(0f, 0f, 276f, 20f), out var stakeLabel, out var stakeValue);
            AssertLeftOf(stakeLabel, stakeValue, HudLayout.TextGap);

            string levy = HudLayout.LevyLine(20, 2, 0, 230);
            Assert.LessOrEqual(levy.Length, HudLayout.LevyBudget);
            Assert.That(levy, Does.Contain("TILLS"));
            Assert.LessOrEqual(HudLayout.LevyLine(12345, 12, 999, 99999).Length, HudLayout.LevyBudget);

            string treasury = HudLayout.TreasuryLine(455f, 460, false);
            Assert.LessOrEqual(treasury.Length, HudLayout.TreasuryBudget);
            Assert.That(treasury, Does.Contain("455"));
            Assert.LessOrEqual(HudLayout.TreasuryLine(455f, 460, true).Length, HudLayout.TreasuryBudget);
            Assert.LessOrEqual(HudLayout.TreasuryLine(100000f, 100000, true).Length, HudLayout.TreasuryBudget);
        }

        static void AssertLeftOf(Rect left, Rect right, float gap)
        {
            Assert.LessOrEqual(left.xMax + gap, right.xMin + 0.01f,
                $"expected {left} to clear {right} by {gap}");
        }
    }
}
