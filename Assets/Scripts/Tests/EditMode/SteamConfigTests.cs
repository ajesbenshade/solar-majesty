using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class SteamConfigTests
    {
        [TestCase(AchievementId.FirstContract, "ACH_FIRST_CONTRACT")]
        [TestCase(AchievementId.SelfSufficient, "ACH_SELF_SUFFICIENT")]
        [TestCase(AchievementId.Ironman, "ACH_IRONMAN")]
        [TestCase(AchievementId.SolarConquest, "ACH_SOLAR_CONQUEST")]
        public void AchievementApiName_IsUpperSnake(AchievementId id, string expected)
        {
            Assert.That(SteamConfig.AchievementApiName(id), Is.EqualTo(expected));
        }

        [Test]
        public void EveryAchievement_HasAUniqueSteamName()
        {
            var seen = new HashSet<string>();
            foreach (AchievementId id in Enum.GetValues(typeof(AchievementId)))
            {
                string api = SteamConfig.AchievementApiName(id);
                Assert.That(api, Does.Match("^ACH_[A-Z0-9_]+$"), api);
                Assert.That(seen.Add(api), Is.True, "duplicate " + api);
            }
        }

        [Test]
        public void EveryAchievement_HasADefinition()
        {
            foreach (AchievementId id in Enum.GetValues(typeof(AchievementId)))
                Assert.That(Achievements.Get(id), Is.Not.Null, id.ToString());
        }

        [TestCase("HTTPServer", "HTTP_SERVER")]
        [TestCase("Phase2Exit", "PHASE2_EXIT")]
        [TestCase("A", "A")]
        [TestCase("", "")]
        public void UpperSnake_HandlesAcronymsAndDigits(string input, string expected)
        {
            Assert.That(SteamConfig.UpperSnake(input), Is.EqualTo(expected));
        }

        [Test]
        public void TestApp_NeverRelaunchesThroughSteam()
        {
            // Relaunching under app 480 would start Valve's Spacewar instead of the game.
            if (SteamConfig.IsTestApp)
                Assert.That(SteamConfig.RelaunchThroughSteam, Is.False);
        }

        [Test]
        public void NoSteamArg_IsCaseInsensitive()
        {
            Assert.That(SteamConfig.SteamDisabledByArgs(new[] { "Game.exe", "-NoSteam" }), Is.True);
            Assert.That(SteamConfig.SteamDisabledByArgs(new[] { "Game.exe" }), Is.False);
            Assert.That(SteamConfig.SteamDisabledByArgs(null), Is.False);
        }

        [Test]
        public void PresenceTokens_MatchTheLocalizationFile()
        {
            Assert.That(SteamConfig.PresenceToken(false, false), Is.EqualTo("#AtTitle"));
            Assert.That(SteamConfig.PresenceToken(true, false), Is.EqualTo("#Governing"));
            Assert.That(SteamConfig.PresenceToken(true, true), Is.EqualTo("#Paused"));
        }
    }
}
