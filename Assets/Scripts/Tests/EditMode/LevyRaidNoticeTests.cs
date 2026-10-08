using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class LevyRaidNoticeTests
    {
        [Test]
        public void Compose_NamesTheAttackerAndTheAmount()
        {
            Assert.AreEqual(
                "A Dust Stalker mugged Haul for 30 EU.",
                LevyRaidNotice.Compose(30, LevyLossCause.Mugged, "Dust Stalker", "Haul", false));
            Assert.AreEqual(
                "Haul was hit and dropped 12 EU.",
                LevyRaidNotice.Compose(12, LevyLossCause.Hit, null, "Haul", false));
            Assert.AreEqual(
                "Haul was hit by a Dust Stalker and dropped 12 EU.",
                LevyRaidNotice.Compose(12, LevyLossCause.Hit, "Dust Stalker", "Haul", false));
            Assert.AreEqual(
                "Haul was destroyed; 45 EU lost.",
                LevyRaidNotice.Compose(45, LevyLossCause.Destroyed, "Dust Stalker", null, false));
        }

        [Test]
        public void Compose_AddsTheDefendHintOnlyWhenAsked()
        {
            string hinted = LevyRaidNotice.Compose(30, LevyLossCause.Mugged, "Dust Stalker", "Haul", true);
            StringAssert.StartsWith("A Dust Stalker mugged Haul for 30 EU.", hinted);
            StringAssert.Contains("Guard Haul's route with a Defend flag.", hinted);

            string plain = LevyRaidNotice.Compose(30, LevyLossCause.Mugged, "Dust Stalker", "Haul", false);
            StringAssert.DoesNotContain("Defend", plain);
            StringAssert.DoesNotContain("charity", plain);
            StringAssert.DoesNotContain("Junk", plain);
        }

        [Test]
        public void ThreeHits_FlushOnce_WithTheSummedAmount()
        {
            var raid = new LevyRaidNotice();
            float hintAt = 0f;
            raid.Note(10, LevyLossCause.Hit, "Dust Stalker", "Haul", 0f);
            raid.Note(12, LevyLossCause.Hit, "Dust Stalker", "Haul", 5f);
            raid.Note(8, LevyLossCause.Hit, "Dust Stalker", "Haul", 10f);

            Assert.IsFalse(raid.TryFlush(10f, false, ref hintAt, out string early));
            Assert.IsNull(early);

            Assert.IsTrue(raid.TryFlush(20f, false, ref hintAt, out string message));
            Assert.AreEqual("Haul was hit by a Dust Stalker and dropped 30 EU.", message);

            Assert.IsFalse(raid.TryFlush(21f, true, ref hintAt, out string again));
            Assert.IsNull(again);
        }

        [Test]
        public void ZeroAmount_ProducesNoMessage()
        {
            var raid = new LevyRaidNotice();
            float hintAt = -1f;
            raid.Note(0, LevyLossCause.Mugged, "Dust Stalker", "Haul", 0f);
            Assert.IsFalse(raid.TryFlush(0f, true, ref hintAt, out string message));
            Assert.IsNull(message);
            Assert.IsNull(LevyRaidNotice.Compose(0, LevyLossCause.Destroyed, "Dust Stalker", "Haul", true));
        }

        [Test]
        public void FriendlyOrEmptyHit_DoesNotCountAsARobbery()
        {
            Assert.IsFalse(LevyRaidNotice.ShouldRob(false, 10f, 20));
            Assert.IsFalse(LevyRaidNotice.ShouldRob(true, 0f, 20));
            Assert.IsFalse(LevyRaidNotice.ShouldRob(true, 5f, 0));
            Assert.IsTrue(LevyRaidNotice.ShouldRob(true, 0.2f, 12));
        }
    }
}
