using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// What a player who already has the main-branch beta (9d99e9d) keeps when the Steam-readiness
    /// port lands: saved key bindings keep their meaning, and settings, progress flags and the
    /// machine-local display values (UI Scale, native resolution) survive the move from the
    /// registry to Profile/prefs.json.
    /// </summary>
    public class PrefsMigrationTests
    {
        // Every key this fixture writes. Each is put back exactly as it was found, because the
        // editor shares the developer's real registry key.
        private static readonly Dictionary<string, char> TouchedKeys = new Dictionary<string, char>
        {
            { DemoSettings.HudKey, 'f' }, { DemoSettings.HudAutoKey, 'i' },
            { DemoSettings.ResolutionWKey, 'i' }, { DemoSettings.ResolutionHKey, 'i' }, { DemoSettings.FullscreenKey, 'i' },
            { DemoSettings.MasterKey, 'f' }, { DemoSettings.TutorialKey, 'i' }, { DemoSettings.SaveFlagKey, 'i' },
            { DemoSettings.MarsJexLessonsKey, 'i' },
            { IntroLaunch.SeenKey, 'i' }, { "SM_Bindings", 's' },
        };
        private string _dir;
        private readonly Dictionary<string, object> _saved = new Dictionary<string, object>();

        [SetUp]
        public void SetUp()
        {
            foreach (var kv in TouchedKeys)
            {
                string k = kv.Key;
                if (!PlayerPrefs.HasKey(k)) { _saved[k] = null; continue; }
                _saved[k] = kv.Value == 'f' ? (object)PlayerPrefs.GetFloat(k)
                          : kv.Value == 'i' ? PlayerPrefs.GetInt(k)
                          : PlayerPrefs.GetString(k);
            }
            foreach (var kv in TouchedKeys) PlayerPrefs.DeleteKey(kv.Key);

            _dir = Path.Combine(Path.GetTempPath(), "sm-prefsmig-" + Guid.NewGuid().ToString("N"));
            GamePrefs.DirectoryOverride = _dir;
            GamePrefs.Reload();
        }

        [TearDown]
        public void TearDown()
        {
            GamePrefs.DirectoryOverride = null;
            GamePrefs.Reload();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            foreach (var kv in TouchedKeys) PlayerPrefs.DeleteKey(kv.Key);
            foreach (var kv in _saved)
            {
                if (kv.Value is string s) PlayerPrefs.SetString(kv.Key, s);
                else if (kv.Value is int i) PlayerPrefs.SetInt(kv.Key, i);
                else if (kv.Value is float f) PlayerPrefs.SetFloat(kv.Key, f);
            }
            PlayerPrefs.Save();
        }
        // ---- saved key bindings ---------------------------------------------------------------

        /// <summary>
        /// A saved binding is "actionNumber:keyCodeNumber". These numbers are what the 9d99e9d beta
        /// wrote, so they must never change meaning. New actions go on the end.
        /// </summary>
        [Test]
        public void GameAction_NumbersAreFrozenAtMainsNumbering()
        {
            var pinned = new (GameAction action, int value)[]
            {
                (GameAction.PanUp, 0), (GameAction.PanDown, 1), (GameAction.PanLeft, 2), (GameAction.PanRight, 3),
                (GameAction.ZoomIn, 4), (GameAction.ZoomOut, 5),
                (GameAction.TogglePause, 6), (GameAction.SpeedUp, 7), (GameAction.SpeedDown, 8),
                (GameAction.ToolBuild, 9), (GameAction.ToolFlag, 10), (GameAction.ToolTech, 11), (GameAction.ToolParty, 12),
                (GameAction.CycleOverlay, 13), (GameAction.JumpToAlert, 14),
                (GameAction.FocusCampusA, 15), (GameAction.FocusCampusB, 16),
                (GameAction.Cancel, 17), (GameAction.Confirm, 18),
                (GameAction.CycleTool, 19), (GameAction.ToolOrbital, 20), (GameAction.DisbandParty, 21),
                (GameAction.BuildSlot1, 22), (GameAction.BuildSlot2, 23), (GameAction.BuildSlot3, 24),
                (GameAction.BuildSlot4, 25), (GameAction.BuildSlot5, 26), (GameAction.BuildSlot6, 27),
                (GameAction.BuildSlot7, 28), (GameAction.BuildSlot8, 29), (GameAction.BuildSlot9, 30),
                (GameAction.BuildSlot10, 31),
                (GameAction.FlagExplore, 32), (GameAction.FlagClearThreat, 33), (GameAction.FlagBuild, 34),
                (GameAction.FlagExtract, 35), (GameAction.FlagDefend, 36), (GameAction.FlagResearchSite, 37),
                (GameAction.FlagOutpost, 38), (GameAction.FlagTerraform, 39),
                (GameAction.BountyUp, 40), (GameAction.BountyDown, 41),
                (GameAction.PromptYes, 42),
                (GameAction.DebugHud, 43), (GameAction.DebugSeedCampusB, 44),
                (GameAction.DebugNextWorld, 45), (GameAction.DebugFatigue, 46),
            };
            foreach (var p in pinned)
                Assert.AreEqual(p.value, (int)p.action, $"{p.action} changed number; saved bindings would move to another action");

            // Anything after the pinned block is a later addition, so it can only sit above 46.
            foreach (GameAction a in Enum.GetValues(typeof(GameAction)))
            {
                bool isPinned = false;
                foreach (var p in pinned) if (p.action == a) { isPinned = true; break; }
                if (!isPinned) Assert.Greater((int)a, 46, $"{a} is new and must be appended after the last shipped action");
            }
        }

        [Test]
        public void SavedBindings_FromTheShippedBeta_StillMapToTheSameActions()
        {
            // KeyCode numbers: F=102, O=111, X=120, Z=122, Alpha7=55, Keypad5=261, Backslash=92.
            // Written by the beta: Pause on X, CycleTool on F, ToolOrbital on O, BuildSlot1 on Z,
            // FlagDefend on Backslash, BountyUp on Keypad5, PromptYes on Alpha7.
            string saved = "6:120;19:102;20:111;22:122;36:92;40:261;42:55";
            Dictionary<GameAction, KeyCode> map = InputBindings.Merge(saved);

            Assert.AreEqual(KeyCode.X, map[GameAction.TogglePause]);
            Assert.AreEqual(KeyCode.F, map[GameAction.CycleTool]);
            Assert.AreEqual(KeyCode.O, map[GameAction.ToolOrbital]);
            Assert.AreEqual(KeyCode.Z, map[GameAction.BuildSlot1]);
            Assert.AreEqual(KeyCode.Backslash, map[GameAction.FlagDefend]);
            Assert.AreEqual(KeyCode.Keypad5, map[GameAction.BountyUp]);
            Assert.AreEqual(KeyCode.Alpha7, map[GameAction.PromptYes]);

            // Nothing that was not saved moved: Pan and Cancel keep their defaults.
            Assert.AreEqual(InputBindings.DefaultKey(GameAction.PanUp), map[GameAction.PanUp]);
            Assert.AreEqual(InputBindings.DefaultKey(GameAction.Cancel), map[GameAction.Cancel]);
        }

        [Test]
        public void SavedBindings_FromTheFirstTableThatShipped_StillMap()
        {
            // The first table (before #82) had 19 actions numbered 0..18. A save from it only ever
            // holds those numbers, and they must keep their meaning.
            string saved = "9:99;11:121"; // ToolBuild -> C, ToolTech -> Y
            Dictionary<GameAction, KeyCode> map = InputBindings.Merge(saved);
            Assert.AreEqual(KeyCode.C, map[GameAction.ToolBuild]);
            Assert.AreEqual(KeyCode.Y, map[GameAction.ToolTech]);
            // Untouched actions keep their defaults.
            Assert.AreEqual(InputBindings.DefaultKey(GameAction.ZoomOut), map[GameAction.ZoomOut]);
        }

        [Test]
        public void SavedBindings_MigrateFromTheRegistry_Unchanged()
        {
            const string saved = "6:120;19:102;22:122";
            PlayerPrefs.SetString("SM_Bindings", saved); // what the beta left in the registry

            Assert.AreEqual(saved, GamePrefs.GetString("SM_Bindings", ""), "first read must hand back the registry string untouched");
            GamePrefs.Save();

            PlayerPrefs.DeleteKey("SM_Bindings"); // a PC restored from Steam Cloud has only the file
            GamePrefs.Reload();
            Assert.AreEqual(saved, GamePrefs.GetString("SM_Bindings", ""));
            Dictionary<GameAction, KeyCode> map = InputBindings.Merge(GamePrefs.GetString("SM_Bindings", ""));
            Assert.AreEqual(KeyCode.X, map[GameAction.TogglePause]);
            Assert.AreEqual(KeyCode.F, map[GameAction.CycleTool]);
            Assert.AreEqual(KeyCode.Z, map[GameAction.BuildSlot1]);
        }

        // ---- the prefs move itself ------------------------------------------------------------

        [Test]
        public void Settings_AndProgressFlags_MigrateIntoTheProfileFile()
        {
            PlayerPrefs.SetFloat(DemoSettings.MasterKey, 0.4f);
            PlayerPrefs.SetInt(DemoSettings.TutorialKey, 1);
            PlayerPrefs.SetInt(DemoSettings.SaveFlagKey, 1);
            PlayerPrefs.SetInt(DemoSettings.MarsJexLessonsKey, 1); // the old Grok-era key name is still the key
            PlayerPrefs.SetInt(IntroLaunch.SeenKey, 1);

            Assert.AreEqual(0.4f, GamePrefs.GetFloat(DemoSettings.MasterKey, 1f), 1e-6f);
            Assert.AreEqual(1, GamePrefs.GetInt(DemoSettings.TutorialKey, 0));
            Assert.AreEqual(1, GamePrefs.GetInt(DemoSettings.SaveFlagKey, 0), "the 'a save exists' flag must survive");
            Assert.AreEqual(1, GamePrefs.GetInt(DemoSettings.MarsJexLessonsKey, 0));
            Assert.IsTrue(IntroLaunch.HasSeen, "a player who saw the intro must not see it again");
            GamePrefs.Save();

            string json = File.ReadAllText(GamePrefs.FilePath);
            foreach (string k in new[] { DemoSettings.MasterKey, DemoSettings.TutorialKey, DemoSettings.SaveFlagKey, DemoSettings.MarsJexLessonsKey, IntroLaunch.SeenKey })
                StringAssert.Contains(k, json, k + " should now live in prefs.json");

            // Cloud-restored PC: file only, no registry.
            foreach (string k in new[] { DemoSettings.MasterKey, DemoSettings.TutorialKey, DemoSettings.SaveFlagKey, DemoSettings.MarsJexLessonsKey, IntroLaunch.SeenKey })
                PlayerPrefs.DeleteKey(k);
            GamePrefs.Reload();
            Assert.AreEqual(0.4f, GamePrefs.GetFloat(DemoSettings.MasterKey, 1f), 1e-6f);
            Assert.AreEqual(1, GamePrefs.GetInt(DemoSettings.SaveFlagKey, 0));
            Assert.IsTrue(IntroLaunch.HasSeen);
        }

        [Test]
        public void UiScale_AndNativeResolution_StayMachineLocalAndSurvive()
        {
            // 5% steps: 1.25 is a value a player could really have chosen.
            PlayerPrefs.SetFloat(DemoSettings.HudKey, 1.25f);
            PlayerPrefs.SetInt(DemoSettings.HudAutoKey, 0);
            PlayerPrefs.SetInt(DemoSettings.ResolutionWKey, 0); // 0x0 means "native"
            PlayerPrefs.SetInt(DemoSettings.ResolutionHKey, 0);
            PlayerPrefs.SetInt(DemoSettings.FullscreenKey, 1);

            // Read exactly the way DemoSettings.Load does.
            Assert.AreEqual(0, GamePrefs.GetInt(DemoSettings.HudAutoKey, 1), "explicit UI Scale (AUTO off) must survive");
            Assert.AreEqual(1.25f, HudScaleMath.RoundToStep(GamePrefs.GetFloat(DemoSettings.HudKey, 1f)), 1e-6f);
            Assert.AreEqual(0, GamePrefs.GetInt(DemoSettings.ResolutionWKey, -1), "native resolution must stay native");
            Assert.AreEqual(0, GamePrefs.GetInt(DemoSettings.ResolutionHKey, -1));
            Assert.AreEqual(1, GamePrefs.GetInt(DemoSettings.FullscreenKey, 0));

            // Writing them back (SaveSettings) must not move them into the roaming file.
            GamePrefs.SetFloat(DemoSettings.HudKey, 1.5f);
            GamePrefs.SetInt(DemoSettings.ResolutionWKey, 2560);
            GamePrefs.SetInt(DemoSettings.ResolutionHKey, 1440);
            GamePrefs.Save();
            Assert.AreEqual(1.5f, PlayerPrefs.GetFloat(DemoSettings.HudKey), 1e-6f);
            Assert.AreEqual(2560, PlayerPrefs.GetInt(DemoSettings.ResolutionWKey));
            string json = File.Exists(GamePrefs.FilePath) ? File.ReadAllText(GamePrefs.FilePath) : "";
            foreach (string k in new[] { DemoSettings.HudKey, DemoSettings.HudAutoKey, DemoSettings.ResolutionWKey, DemoSettings.ResolutionHKey, DemoSettings.FullscreenKey })
                StringAssert.DoesNotContain(k, json, k + " must stay on this PC and never roam with Steam Cloud");

            GamePrefs.Reload();
            Assert.AreEqual(1.5f, GamePrefs.GetFloat(DemoSettings.HudKey, 1f), 1e-6f);
            Assert.AreEqual(2560, GamePrefs.GetInt(DemoSettings.ResolutionWKey, 0));
        }

        [Test]
        public void ProfileFile_LivesBesideTheSaves_NotInsideThem()
        {
            GamePrefs.DirectoryOverride = null;
            string profile = Path.GetFullPath(GamePrefs.Directory);
            string saves = Path.GetFullPath(SaveSystem.SaveDirectory);
            Assert.AreNotEqual(saves, profile);
            Assert.IsFalse(profile.StartsWith(saves + Path.DirectorySeparatorChar), "prefs.json must not be inside the Saves folder");
            Assert.IsFalse(saves.StartsWith(profile + Path.DirectorySeparatorChar), "Saves must not be inside Profile");
            GamePrefs.DirectoryOverride = _dir;
        }
    }
}
