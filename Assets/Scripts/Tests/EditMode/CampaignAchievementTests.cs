using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Solar Conquest used to read the per-scene RunStats, which reset on every world's scene load,
    /// so it could never unlock. These pin the campaign-wide version and the saved per-world stats.
    /// </summary>
    public class CampaignAchievementTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "sm-campaign-ach-" + System.Guid.NewGuid().ToString("N"));
            GamePrefs.DirectoryOverride = _dir;
            Achievements.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            Achievements.ResetAll();
            GamePrefs.DirectoryOverride = null;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void SolarConquest_NeedsEveryWorldInTheCampaign()
        {
            var stats = new RunStats { BodiesConquered = 1 };
            var all = CelestialBodyCatalog.All;
            for (int i = 0; i < all.Length - 1; i++)
            {
                CampaignProgress.MarkConquered(all[i]);
                Achievements.Evaluate(stats);
                Assert.That(Achievements.IsUnlocked(AchievementId.SolarConquest), Is.False, "after " + all[i]);
            }

            CampaignProgress.MarkConquered(all[all.Length - 1]);
            Achievements.Evaluate(stats);
            Assert.That(Achievements.IsUnlocked(AchievementId.SolarConquest), Is.True);
        }

        [Test]
        public void NewCampaign_ClearsConquests()
        {
            CampaignProgress.MarkConquered(CelestialBodyId.Earth);
            Assert.That(CampaignProgress.HasConquered(CelestialBodyId.Earth), Is.True);

            CampaignProgress.BeginNewGame(CelestialBodyId.Earth);
            Assert.That(CampaignProgress.HasConquered(CelestialBodyId.Earth), Is.False);
            Assert.That(CampaignProgress.ConqueredEveryWorld, Is.False);
        }

        [Test]
        public void RunStats_RoundTripThroughASave()
        {
            var save = new SaveGame
            {
                stats = new RunStats { RobotsFabricated = 7, RobotsScrapped = 4, DensCleared = 9, HighestBounty = 2400 }
            };
            var back = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            Assert.That(back.stats.RobotsFabricated, Is.EqualTo(7));
            Assert.That(back.stats.RobotsScrapped, Is.EqualTo(4));
            Assert.That(back.stats.DensCleared, Is.EqualTo(9));
            Assert.That(back.stats.HighestBounty, Is.EqualTo(2400));
        }

        [Test]
        public void OldSaveWithoutStats_ReadsAsZeros()
        {
            var back = JsonUtility.FromJson<SaveGame>("{\"version\":7,\"body\":2}");
            Assert.That(back.stats, Is.Not.Null);
            Assert.That(back.stats.RobotsFabricated, Is.EqualTo(0));
        }
    }
}
