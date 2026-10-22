using NUnit.Framework;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Session 18: a Courier carrying levy stood 22 m from the Commons for 27 minutes (the chest
    /// centre is inside the carved footprint), and an alpha raider from the kingdom spawn table
    /// kept the dens gate open after all three dens were cleared.
    /// </summary>
    public class CourierChestAndDensGateTests
    {
        [Test]
        public void AtChest_CountsTheWalkableApproachBesideACarvedChest()
        {
            // Commons centre ~9 m inside its footprint: the centre alone can never be reached.
            Assert.IsFalse(LevyRun.AtChest(9.2f, 9.2f), "still walking");
            Assert.IsTrue(LevyRun.AtChest(9.2f, 0.4f), "standing on the approach point is at the chest");
            Assert.IsTrue(LevyRun.AtChest(LevyRun.DepositArrive, 20f), "a small chest is reached at its centre");
            Assert.Greater(LevyRun.ChestApproachSample, 9.2f, "the approach search must reach past the Commons footprint");
        }

        [Test]
        public void DensGate_IgnoresKingdomSpawns_YardPests_AndJunk()
        {
            Assert.IsTrue(MissionController.HoldsDens(FaunaKind.Stalker, true, false), "a den stalker holds the gate");
            Assert.IsFalse(MissionController.HoldsDens(FaunaKind.Stalker, false, false), "a dead one does not");
            Assert.IsFalse(MissionController.HoldsDens(FaunaKind.Stalker, true, true),
                "an ALPHA STALKER from the colony edge does not hold back the victory");
            Assert.IsFalse(MissionController.HoldsDens(FaunaKind.Leech, true, false), "yard pests are campus ecology");
            Assert.IsFalse(MissionController.HoldsDens(FaunaKind.Mite, true, false));
            Assert.IsFalse(MissionController.HoldsDens(FaunaKind.JunkBot, true, false), "scrapyard junk");
        }
    }
}
