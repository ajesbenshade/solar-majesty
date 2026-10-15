using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SolarMajesty.Tests
{
    public class VictoryScreenTests
    {
        [SetUp]
        public void SetUp() => CampaignProgress.ResetAllForTests();

        [TearDown]
        public void TearDown() => CampaignProgress.ResetAllForTests();

        [Test]
        public void AllGatesMet_LatchesVictory_Once()
        {
            Assert.IsFalse(VictoryOffer.ShouldLatch(false, false, false, false));
            Assert.IsFalse(VictoryOffer.ShouldLatch(false, true, true, false), "a loss is not a victory");
            Assert.IsTrue(VictoryOffer.ShouldLatch(false, false, true, false));
            Assert.IsFalse(VictoryOffer.ShouldLatch(true, false, true, true), "the latch does not re-fire");

            var host = new GameObject("VictoryTrigger");
            try
            {
                var mission = host.AddComponent<MissionController>();
                mission.ArmForTests(densCleared: true, sustainComplete: false, launchReady: true);
                mission.EvaluateVictoryForTests();
                Assert.IsFalse(mission.IsWon, "sustain still open");

                mission.ArmForTests(densCleared: true, sustainComplete: true, launchReady: true);
                Assert.IsTrue(mission.AllGatesMet);
                mission.EvaluateVictoryForTests();
                Assert.IsTrue(mission.IsWon);
                Assert.AreEqual(MissionState.Won, mission.State);

                mission.EvaluateVictoryForTests();
                Assert.IsTrue(mission.IsWon);
            }
            finally
            {
                // Object.Destroy is illegal in EditMode. DestroyImmediate is the edit-mode path.
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(host);
                else
                    UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EarthVictory_OffersLuna_AndHoldsTheSim()
        {
            Assert.IsTrue(VictoryOffer.IsBuilt(CelestialBodyId.Luna, 1));
            Assert.IsFalse(VictoryOffer.IsBuilt(CelestialBodyId.Luna, 0), "no scene in the build means Luna is not a level");
            Assert.Greater(SceneManager.sceneCountInBuildSettings, 0);
            Assert.IsTrue(VictoryOffer.IsBuilt(CelestialBodyId.Luna));
            Assert.AreEqual(VictoryOffer.CampaignScenePath, "Assets/Scenes/LunarOutpost_Sandbox.unity");

            var offer = VictoryOffer.For(CelestialBodyId.Earth, nextBuilt: true, endless: false);
            Assert.IsTrue(offer.HoldSim);
            Assert.AreEqual("EARTH SECURED", offer.Headline);
            Assert.AreEqual(VictoryOffer.ContinueToLuna, offer.ContinueLabel);
            Assert.IsNull(offer.ComingSoon);
            Assert.IsTrue(offer.ShowMainMenu);
            Assert.AreEqual(VictoryOffer.MainMenu, "Main Menu");
            AssertEuWording(offer.Headline);
            AssertEuWording(offer.Body);
            AssertEuWording(offer.ContinueLabel);
            StringAssert.Contains("central AI", offer.Body);
            StringAssert.Contains("worker bots", offer.Body.ToLowerInvariant());
            StringAssert.Contains("EU", offer.Body);
        }

        [Test]
        public void EarthVictory_WhenLunaIsNotBuilt_ShowsComingSoonAndMainMenu()
        {
            var offer = VictoryOffer.For(CelestialBodyId.Earth, nextBuilt: false, endless: false);
            Assert.IsTrue(offer.HoldSim);
            Assert.IsFalse(offer.CanContinue);
            Assert.IsNull(offer.ContinueLabel);
            Assert.AreEqual(VictoryOffer.LunaComingSoon, offer.ComingSoon);
            Assert.AreEqual("Luna coming soon", offer.ComingSoon);
            Assert.IsTrue(offer.ShowMainMenu);
            AssertEuWording(offer.ComingSoon);
            AssertEuWording(offer.Body);
        }

        [Test]
        public void FirstHourDemo_DoesNotBlockAVictoryDepart()
        {
            Assert.IsTrue(VictoryOffer.DemoBlocksDepart(firstHourDemo: true, fromVictory: false));
            Assert.IsFalse(VictoryOffer.DemoBlocksDepart(firstHourDemo: true, fromVictory: true));
            Assert.IsFalse(VictoryOffer.DemoBlocksDepart(firstHourDemo: false, fromVictory: false));

            bool prior = DemoSettings.FirstHourDemo;
            try
            {
                DemoSettings.FirstHourDemo = true;
                var offer = VictoryOffer.For(CelestialBodyId.Earth, nextBuilt: true, endless: false);
                Assert.AreEqual(VictoryOffer.ContinueToLuna, offer.ContinueLabel);
            }
            finally
            {
                DemoSettings.FirstHourDemo = prior;
            }
        }

        [Test]
        public void EarthVictory_PersistsTheExistingCampaignKey()
        {
            CampaignProgress.Ensure();
            Assert.IsFalse(CampaignProgress.IsUnlocked(CelestialBodyId.Luna));

            VictoryOffer.RecordProgress(CelestialBodyId.Earth);

            Assert.AreEqual(CelestialBodyId.Luna, CampaignProgress.HighestUnlocked);
            Assert.IsTrue(CampaignProgress.IsUnlocked(CelestialBodyId.Luna));
            Assert.AreEqual((int)CelestialBodyId.Luna, PlayerPrefs.GetInt(CampaignProgress.MaxKey));
            Assert.AreEqual("SM_CampaignMaxBody", CampaignProgress.MaxKey);
        }

        [Test]
        public void Panel_StaysInsideTheScaledView()
        {
            var atScale = VictoryOffer.PanelRectForScreen(1920, 1080, 1.5f);
            float viewW = 1920f / 1.5f;
            float viewH = 1080f / 1.5f;
            Assert.GreaterOrEqual(atScale.x, 0f);
            Assert.GreaterOrEqual(atScale.y, 0f);
            Assert.LessOrEqual(atScale.xMax, viewW + 0.01f);
            Assert.LessOrEqual(atScale.yMax, viewH + 0.01f);

            var atTwo = VictoryOffer.PanelRectForScreen(1280, 720, 2f);
            Assert.LessOrEqual(atTwo.xMax, 1280f / 2f + 0.01f);
            Assert.LessOrEqual(atTwo.yMax, 720f / 2f + 0.01f);
            Assert.Greater(atTwo.width, 32f);
            Assert.Greater(atTwo.height, 32f);
        }

        private static void AssertEuWording(string line)
        {
            Assert.IsFalse(string.IsNullOrEmpty(line));
            StringAssert.DoesNotContain("villager", line.ToLowerInvariant());
            StringAssert.DoesNotContain("gold", line.ToLowerInvariant());
            StringAssert.DoesNotContain("soul", line.ToLowerInvariant());
            StringAssert.DoesNotContain("ICE", line);
            StringAssert.DoesNotContain("REG", line);
            StringAssert.DoesNotContain("CRED", line);
        }
    }
}
