using System;
using System.IO;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// The Steam demo (SM_DEMO player, BuildCommands.BuildWindowsDemo): Earth only, ends on the
    /// end-of-demo screen, no campaign-only achievements, its own saves. The editor never compiles
    /// with SM_DEMO, so these drive the rules through <see cref="DemoBuild.Override"/>.
    /// </summary>
    public class DemoBuildTests
    {
        private string _dir;
        private bool _firstHour;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "sm-demo-build-" + Guid.NewGuid().ToString("N"));
            GamePrefs.DirectoryOverride = _dir;
            CampaignProgress.ResetAllForTests();
            Achievements.ResetAll();
            _firstHour = DemoSettings.FirstHourDemo;
        }

        [TearDown]
        public void TearDown()
        {
            DemoBuild.Override(null);
            DemoSettings.FirstHourDemo = _firstHour;
            Achievements.IronmanActive = false;
            Achievements.ResetAll();
            CampaignProgress.ResetAllForTests();
            GamePrefs.DirectoryOverride = null;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void EditorAndFullGame_AreNotTheDemo()
        {
            // SM_DEMO must only ever reach the demo player through extraScriptingDefines.
            Assert.That(DemoBuild.CompiledAsDemo, Is.False);
            Assert.That(DemoBuild.Active, Is.False);
            Assert.That(SteamConfig.AppId, Is.EqualTo(SteamConfig.FullGameAppId), "the editor is the full game");
#if UNITY_EDITOR
            string defines = UnityEditor.PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Standalone);
            Assert.That(defines, Does.Not.Contain(DemoBuild.Define), "SM_DEMO leaked into ProjectSettings");
            Assert.That(UnityEditor.PlayerSettings.productName, Is.Not.EqualTo(DemoBuild.ProductName),
                "the demo product name leaked into ProjectSettings");
            Assert.That(UnityEditor.PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone),
                Is.Not.EqualTo(DemoBuild.BundleId), "the demo bundle id leaked into ProjectSettings");
#endif
        }

        [Test]
        public void Demo_IsEarthOnly()
        {
            foreach (var id in CelestialBodyCatalog.All)
            {
                Assert.That(DemoBuild.BodyInDemo(false, id), Is.True, id.ToString());
                Assert.That(DemoBuild.BodyInDemo(true, id), Is.EqualTo(id == CelestialBodyId.Earth), id.ToString());
                Assert.That(DemoBuild.ClampBody(true, id), Is.EqualTo(CelestialBodyId.Earth));
                Assert.That(DemoBuild.ClampBody(false, id), Is.EqualTo(id));
            }
        }

        [Test]
        public void Demo_TitleShowsEarthOnly_FullGameStillShowsEarthLunaMars()
        {
            DemoBuild.Override(true);
            foreach (CelestialBodyId id in Enum.GetValues(typeof(CelestialBodyId)))
            {
                Assert.That(DemoSlice.ShowBody(id), Is.EqualTo(id == CelestialBodyId.Earth), id.ToString());
                Assert.That(DemoSlice.ShowBodyInFullGame(id),
                    Is.EqualTo(id == CelestialBodyId.Earth || id == CelestialBodyId.Luna || id == CelestialBodyId.Mars),
                    id.ToString());
            }
            Assert.That(CampaignProgress.NextAfter(CelestialBodyId.Earth).HasValue, Is.False, "no Continue to Luna");
        }

        [Test]
        public void Demo_LocksOtherWorlds_EvenWithAnUnlockedProfile()
        {
            CampaignProgress.DebugUnlockAll();
            Assert.That(CampaignProgress.IsUnlocked(CelestialBodyId.Mars), Is.True, "full game");

            DemoBuild.Override(true);
            Assert.That(CampaignProgress.IsUnlocked(CelestialBodyId.Earth), Is.True);
            foreach (var id in CelestialBodyCatalog.All)
            {
                if (id == CelestialBodyId.Earth) continue;
                Assert.That(CampaignProgress.IsUnlocked(id), Is.False, id.ToString());
                Assert.That(DemoSlice.ShowBody(id), Is.False, id.ToString());
                Assert.That(VictoryOffer.IsBuilt(id, 1), Is.False, id.ToString());
            }
        }

        [Test]
        public void Demo_WinningEarthUnlocksNothing_ButRecordsTheWin()
        {
            DemoBuild.Override(true);
            CampaignProgress.Ensure();
            CampaignProgress.MarkConquered(CelestialBodyId.Earth);
            VictoryOffer.RecordProgress(CelestialBodyId.Earth);
            CampaignProgress.UnlockThrough(CelestialBodyId.Mars);

            Assert.That(CampaignProgress.HighestUnlocked, Is.EqualTo(CelestialBodyId.Earth));
            Assert.That(CampaignProgress.HasConquered(CelestialBodyId.Earth), Is.True,
                "the Earth bit is what a full-game import would read");
            Assert.That(VictoryOffer.NextIsBuilt(CelestialBodyId.Earth), Is.False);
        }

        [Test]
        public void Demo_SavedBodyFallsBackToEarth()
        {
            BodySeed.SetBody(CelestialBodyId.Mars);
            Assert.That(BodySeed.LoadSavedBody(), Is.EqualTo(CelestialBodyId.Mars));

            DemoBuild.Override(true);
            Assert.That(BodySeed.LoadSavedBody(), Is.EqualTo(CelestialBodyId.Earth));
        }

        [Test]
        public void Demo_CannotLeaveTheGuidedEarthStart()
        {
            DemoBuild.Override(true);
            DemoSettings.FirstHourDemo = false;
            Assert.That(DemoSettings.FirstHourDemo, Is.True);
            DemoSettings.SetFirstHourDemo(false);
            Assert.That(DemoSettings.FirstHourDemo, Is.True);

            DemoBuild.Override(false);
            Assert.That(DemoSettings.FirstHourDemo, Is.False, "the full game keeps the player's choice");
        }

        [Test]
        public void EarthWin_EndsTheDemo_OnlyInTheDemo()
        {
            Assert.That(DemoEndOffer.Applies(true, CelestialBodyId.Earth, won: true, endless: false), Is.True);
            Assert.That(DemoEndOffer.Applies(false, CelestialBodyId.Earth, won: true, endless: false), Is.False);
            Assert.That(DemoEndOffer.Applies(true, CelestialBodyId.Earth, won: false, endless: false), Is.False);
            Assert.That(DemoEndOffer.Applies(true, CelestialBodyId.Earth, won: true, endless: true), Is.False);
            Assert.That(DemoEndOffer.Applies(true, CelestialBodyId.Luna, won: true, endless: false), Is.False);
        }

        [Test]
        public void EndScreen_ShowsOnlyWhileTheWinIsHeld()
        {
            // Main's rule for the campaign verdict: a won save that was continued plays on.
            Assert.That(DemoEndOffer.Show(true, CelestialBodyId.Earth, won: true, held: true, endless: false, dismissed: false), Is.True);
            Assert.That(DemoEndOffer.Show(true, CelestialBodyId.Earth, won: true, held: false, endless: false, dismissed: false), Is.False,
                "KEEP PLAYING EARTH or a continued won save: no end screen");
            Assert.That(DemoEndOffer.Show(true, CelestialBodyId.Earth, won: true, held: true, endless: false, dismissed: true), Is.False);
            Assert.That(DemoEndOffer.Show(true, CelestialBodyId.Earth, won: true, held: true, endless: true, dismissed: false), Is.False);
            Assert.That(DemoEndOffer.Show(false, CelestialBodyId.Earth, won: true, held: true, endless: false, dismissed: false), Is.False,
                "the full game shows the campaign verdict instead");
            Assert.That(VictoryOffer.HoldOnRestore(won: true, endless: false), Is.False, "loading a won Earth never re-holds it");
        }

        [Test]
        public void EndScreen_OffersWishlistKeepPlayingAndQuit()
        {
            Assert.That(DemoEndOffer.Buttons, Is.EqualTo(new[]
            {
                DemoEndOffer.Choice.Wishlist, DemoEndOffer.Choice.KeepPlaying, DemoEndOffer.Choice.Quit
            }));
            foreach (var choice in DemoEndOffer.Buttons)
                Assert.That(DemoEndOffer.Label(choice), Is.Not.Empty);
            Assert.That(DemoEndOffer.Headline, Is.Not.Empty);
            Assert.That(DemoEndOffer.Body, Does.Contain("Luna").And.Contain("Mars"));
            Assert.That(DemoEndOffer.WorldLocked("Luna"), Does.Contain("Luna"));
            Assert.That(DemoEndOffer.WorldLocked(null), Is.Not.Empty);
        }

        [Test]
        public void Demo_LeavesOutCampaignOnlyAchievements()
        {
            Assert.That(DemoBuild.AchievementInDemo(true, AchievementId.SolarConquest), Is.False);
            Assert.That(DemoBuild.AchievementInDemo(true, AchievementId.Ironman), Is.False);
            Assert.That(DemoBuild.AchievementInDemo(true, AchievementId.FirstContract), Is.True);
            foreach (AchievementId id in Enum.GetValues(typeof(AchievementId)))
                Assert.That(DemoBuild.AchievementInDemo(false, id), Is.True, id.ToString());
            Assert.That(DemoBuild.PushAchievementsToSteam(true), Is.False);
            Assert.That(DemoBuild.PushAchievementsToSteam(false), Is.True);
        }

        [Test]
        public void Demo_NeverUnlocksCampaignComplete()
        {
            foreach (var id in CelestialBodyCatalog.All)
                CampaignProgress.MarkConquered(id); // a doctored profile
            Achievements.IronmanActive = true;
            DemoBuild.Override(true);
            Achievements.Evaluate(new RunStats { BodiesConquered = 1, FlagsCompleted = 1 });
            Achievements.IronmanActive = false;

            Assert.That(Achievements.IsUnlocked(AchievementId.SolarConquest), Is.False);
            Assert.That(Achievements.IsUnlocked(AchievementId.Ironman), Is.False);
            Assert.That(Achievements.IsUnlocked(AchievementId.FirstContract), Is.True);
        }

        [Test]
        public void Demo_HasItsOwnSaveAndProfileFolders()
        {
            Assert.That(DemoBuild.SaveFolder(false), Is.EqualTo("Saves"));
            Assert.That(DemoBuild.ProfileFolder(false), Is.EqualTo("Profile"));
            Assert.That(DemoBuild.SaveFolder(true), Is.Not.EqualTo(DemoBuild.SaveFolder(false)));
            Assert.That(DemoBuild.ProfileFolder(true), Is.Not.EqualTo(DemoBuild.ProfileFolder(false)));
            Assert.That(DemoBuild.ProfileFolder(true), Is.Not.EqualTo("Profile-Editor"));
        }

        [Test]
        public void DemoPlayer_HasItsOwnNames()
        {
            Assert.That(DemoBuild.ProductName, Is.EqualTo("Solar Majesty Demo"));
            Assert.That(DemoBuild.ExeName, Is.Not.EqualTo("SolarMajesty.exe"));
            Assert.That(DemoBuild.ExeName, Does.EndWith(".exe"));
            Assert.That(DemoBuild.BundleId, Does.EndWith(".demo"));
        }

        [Test]
        public void BuildInfo_NamesTheFlavour()
        {
            var when = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
            string demo = BuildVersion.ToJson("0.9.0", "abc1234", "StandaloneWindows64", when, DemoBuild.Flavour(true));
            string full = BuildVersion.ToJson("0.9.0", "abc1234", "StandaloneWindows64", when, DemoBuild.Flavour(false));
            Assert.That(BuildVersion.ReadField(demo, "flavour"), Is.EqualTo("demo"));
            Assert.That(BuildVersion.ReadField(full, "flavour"), Is.EqualTo("full"));
            Assert.That(BuildVersion.ReadField(demo, "version"), Is.EqualTo("0.9.0"));
            Assert.That(BuildVersion.ReadField(demo, "builtUtc"), Is.EqualTo("2026-11-01T12:00:00Z"));
            Assert.That(BuildVersion.ReadField(
                BuildVersion.ToJson("0.9.0", "abc1234", "StandaloneWindows64", when), "flavour"), Is.Null);
        }
    }
}
