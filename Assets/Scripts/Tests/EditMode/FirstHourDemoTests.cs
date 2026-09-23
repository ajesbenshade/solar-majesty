using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class FirstHourDemoTests
    {
        private bool _priorDemo;
        private ColonyRunMode _mode;
        private ChallengeId _challenge;
        private DoctrineStance _stance;

        [SetUp]
        public void SetUp()
        {
            _priorDemo = DemoSettings.FirstHourDemo;
            _mode = ReplayRules.Mode;
            _challenge = ReplayRules.Challenge;
            _stance = ReplayRules.Stance;
            DemoSettings.FirstHourDemo = true;
        }

        [TearDown]
        public void TearDown()
        {
            DemoSettings.FirstHourDemo = _priorDemo;
            ReplayRules.Mode = _mode;
            ReplayRules.Challenge = _challenge;
            ReplayRules.Stance = _stance;
        }

        [Test]
        public void WorkshopBeat_WaitsForALivingEngineer()
        {
            Assert.AreEqual(0, FirstHourTutorial.Advance(0, false, true, false, false));
            Assert.AreEqual(1, FirstHourTutorial.Advance(0, true, false, false, false));
        }

        [Test]
        public void ExploreFlag_DoesNotOpenThePriceLesson()
        {
            // refusedBuild is only a Build flag the Engineer ignored. Explore is not that.
            Assert.AreEqual(1, FirstHourTutorial.Advance(1, true, false, false, false));
        }

        [Test]
        public void AcceptedBuild_CompletesThePriceLessonWithoutReposting()
        {
            Assert.AreEqual(FirstHourTutorial.PestStep, FirstHourTutorial.Advance(1, true, false, true, false));
            StringAssert.DoesNotContain("Right-click", FirstHourTutorial.Beat(1, highBuildAlready: true));
        }

        [Test]
        public void RefusedBuild_ThenRaisedPrice_ThenDefend()
        {
            Assert.AreEqual(2, FirstHourTutorial.Advance(1, true, true, false, false));
            Assert.AreEqual(2, FirstHourTutorial.Advance(2, true, true, false, false));
            Assert.AreEqual(FirstHourTutorial.PestStep, FirstHourTutorial.Advance(2, true, true, true, false));
            Assert.AreEqual(FirstHourTutorial.PestStep, FirstHourTutorial.Advance(3, true, true, true, false));
            Assert.AreEqual(FirstHourTutorial.Goal, FirstHourTutorial.Advance(3, true, true, true, true));
        }

        [Test]
        public void CancellingTheBuildFlag_ReturnsToTheCheapBeat()
        {
            Assert.AreEqual(1, FirstHourTutorial.Advance(2, true, false, false, false));
        }

        [Test]
        public void DemoCatalog_HidesGuildsAndLeadsWithTheEngineer()
        {
            Assert.IsFalse(DemoSlice.ShowBuilding(BuildingCategory.GuildHall));
            Assert.IsFalse(DemoSlice.ShowBuilding(BuildingCategory.FobotYard));
            Assert.IsFalse(DemoSlice.ShowBuilding(BuildingCategory.AidStation));
            Assert.IsFalse(DemoSlice.ShowBuilding(BuildingCategory.ClimateLoom));
            Assert.IsTrue(DemoSlice.ShowBuilding(BuildingCategory.EngineerWorkshop));
            Assert.IsTrue(DemoSlice.ShowBuilding(BuildingCategory.Watchtower), "collectors need towers");
            Assert.IsTrue(DemoSlice.ShowBuilding(BuildingCategory.Market));

            var catalog = new List<BuildingData>
            {
                Building(BuildingCategory.GuildHall),
                Building(BuildingCategory.Utility),
                Building(BuildingCategory.EngineerWorkshop),
                Building(BuildingCategory.Habitat)
            };
            var visible = new List<int>();
            DemoSlice.CollectVisible(catalog, visible);
            Assert.AreEqual(2, visible.Count);
            Assert.AreEqual(BuildingCategory.EngineerWorkshop, catalog[visible[0]].category);
            Assert.AreEqual(BuildingCategory.Habitat, catalog[visible[1]].category);
        }

        [Test]
        public void BuildMenu_NeverShowsRetiredGridAirlockOrRegolithPieces([Values(true, false)] bool demo)
        {
            DemoSettings.FirstHourDemo = demo;
            try
            {
                Assert.IsFalse(DemoSlice.ShowBuilding(BuildingCategory.Utility), "airlock");
                Assert.IsFalse(DemoSlice.ShowBuilding(BuildingCategory.Power), "power node / solar array");
                Assert.IsFalse(DemoSlice.ShowBuilding(BuildingCategory.RegolithCamp));
                Assert.IsFalse(DemoSlice.ShowBuilding(BuildingCategory.Mining), "OPS drop-off");
                Assert.IsFalse(DemoSlice.ShowBuilding(BuildingCategory.Commons), "placed for the player");
            }
            finally
            {
                DemoSettings.FirstHourDemo = true;
            }
        }

        [Test]
        public void DemoTechAndBodies_HideTheSecondHour()
        {
            Assert.IsTrue(DemoSlice.ShowTech(TechId.LunarRocket));
            Assert.IsFalse(DemoSlice.ShowTech(TechId.GuildCharter));
            Assert.IsFalse(DemoSlice.ShowTech(TechId.MarsShip));
            Assert.IsFalse(DemoSlice.ShowTech(TechId.BeltHauler));
            Assert.IsFalse(DemoSlice.ShowTech(TechId.ClimateLoom));
            Assert.IsTrue(DemoSlice.ShowBody(CelestialBodyId.Earth));
            Assert.IsTrue(DemoSlice.ShowBody(CelestialBodyId.Luna));
            Assert.IsFalse(DemoSlice.ShowBody(CelestialBodyId.Belt));
            Assert.IsFalse(DemoSlice.ShowBody(CelestialBodyId.Europa));
        }

        [Test]
        public void FullCampaign_ShowsTheHiddenBuilding()
        {
            DemoSettings.FirstHourDemo = false;
            Assert.IsTrue(DemoSlice.ShowBuilding(BuildingCategory.GuildHall));
            Assert.IsTrue(DemoSlice.ShowTech(TechId.GuildCharter));
            Assert.IsTrue(DemoSlice.ShowBody(CelestialBodyId.Europa));
        }

        private static BuildingData Building(BuildingCategory cat)
        {
            var data = ScriptableObject.CreateInstance<BuildingData>();
            data.category = cat;
            data.displayName = cat.ToString();
            return data;
        }
    }
}
