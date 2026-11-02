using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class SteamConfigTests
    {
        [Test]
        public void AppId_IsValvesSpacewarTestApp_NotARealId()
        {
            // 480 is Valve's public test app. A real App ID must come from the partner site and is
            // not committed to this repo; swapping it in means updating this test on purpose.
            Assert.That(SteamConfig.AppId, Is.EqualTo(480u));
            Assert.That(SteamConfig.IsTestApp, Is.True);
        }

        [Test]
        public void TestApp_NeverRelaunchesThroughSteam()
        {
            // RestartAppIfNecessary(480) would hand the player to Spacewar.
            Assert.That(SteamConfig.RelaunchThroughSteam, Is.False);
        }

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

        [Test]
        public void HandheldArg_IsCaseInsensitive()
        {
            Assert.That(SteamConfig.HandheldByArgs(new[] { "game.exe", "-HANDHELD" }), Is.True);
            Assert.That(SteamConfig.HandheldByArgs(new[] { "game.exe", "-nosteam" }), Is.False);
            Assert.That(SteamConfig.HandheldByArgs(null), Is.False);
        }

        [Test]
        public void SteamDeck_IsRecognisedFromTheEnvironmentOrTheGpu()
        {
            Assert.That(SteamConfig.LooksLikeSteamDeck("NVIDIA GeForce RTX 3080", "1"), Is.True, "Steam sets SteamDeck=1");
            Assert.That(SteamConfig.LooksLikeSteamDeck("AMD Custom GPU 0405", null), Is.True, "LCD");
            Assert.That(SteamConfig.LooksLikeSteamDeck("AMD Custom GPU 0932", ""), Is.True, "OLED");
            Assert.That(SteamConfig.LooksLikeSteamDeck("AMD VANGOGH (van gogh, LLVM)", null), Is.True);
            Assert.That(SteamConfig.LooksLikeSteamDeck("AMD Radeon(TM) Graphics", "0"), Is.False);
            Assert.That(SteamConfig.LooksLikeSteamDeck(null, null), Is.False);
        }

        [Test]
        public void HandheldQuality_StartsOnMedium_ButASavedChoiceWins()
        {
            Assert.That(HandheldMode.StartingQuality(false, 0, 5, true, 6), Is.EqualTo(2), "Deck, first launch: Medium");
            Assert.That(HandheldMode.StartingQuality(false, 0, 5, false, 6), Is.EqualTo(5), "desktop: project default");
            Assert.That(HandheldMode.StartingQuality(true, 4, 5, true, 6), Is.EqualTo(4), "saved choice wins on the Deck");
            Assert.That(HandheldMode.StartingQuality(false, 0, 5, true, 2), Is.EqualTo(1), "never past the last tier");
            Assert.That(QualitySettings.names[HandheldMode.DefaultQualityLevel], Is.EqualTo("Medium"));
        }

        [Test]
        public void HandheldMode_ConfirmedDeck_TurnsOnTheHandheldHudScale()
        {
            bool wasHandheld = HudScaleMath.Handheld;
            try
            {
                HandheldMode.ResetForTests();
                Assert.That(HandheldMode.Active, Is.False);
                HandheldMode.ConfirmSteamDeck();
                Assert.That(HandheldMode.OnSteamDeck, Is.True);
                Assert.That(HandheldMode.Active, Is.True);
                Assert.That(HudScaleMath.AutoForHeight(800), Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                HandheldMode.ResetForTests();
                HudScaleMath.Handheld = wasHandheld;
            }
        }
    }
}
