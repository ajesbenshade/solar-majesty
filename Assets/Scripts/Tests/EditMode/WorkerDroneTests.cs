using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Charging stations house the economy's worker drones at the old HAB bed count (3 each), with
    /// the old starter crew (2) and growth (one every 18 s while a berth is free). Homesteader reads
    /// the drone peak; the census survives a save.
    /// </summary>
    public class WorkerDroneTests
    {
        private static Settlement Make(int chargers)
        {
            var s = new Settlement(new ResourceManager());
            for (int i = 0; i < chargers; i++)
                s.RegisterPlaced(BuildingCategory.Habitat);
            return s;
        }

        private static void Run(Settlement s, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.5f)
                s.Tick(0.5f);
        }

        // ---------------------------------------------------------------- capacity

        [Test]
        public void Capacity_IsThreeBerthsPerChargingStation()
        {
            Assert.AreEqual(3, Settlement.DronesPerCharger, "the old HousingPerHab");
            Assert.AreEqual(0, Settlement.DroneCapacityFor(0));
            Assert.AreEqual(3, Settlement.DroneCapacityFor(1));
            Assert.AreEqual(21, Settlement.DroneCapacityFor(7));
            Assert.AreEqual(21, Settlement.DroneCapacityFor(6, 3), "Gene Vault adds 3 berths");
            Assert.AreEqual(0, Settlement.DroneCapacityFor(0, 3), "no station, no berths");

            var s = Make(4);
            s.AddVillageHab();
            Assert.AreEqual(15, s.DroneCapacity, "player-built and village-built stations both count");
        }

        [Test]
        public void FirstChargingStation_BringsTheStarterCrew()
        {
            var s = Make(0);
            Assert.AreEqual(0, s.Drones);
            s.RegisterPlaced(BuildingCategory.Habitat);
            Assert.AreEqual(Settlement.StarterDrones, s.Drones);
            s.RegisterPlaced(BuildingCategory.Habitat);
            Assert.AreEqual(Settlement.StarterDrones, s.Drones, "only the first station seeds a crew");

            var v = Make(0);
            v.AddVillageHab();
            Assert.AreEqual(Settlement.StarterDrones, v.Drones, "a village-built first station seeds it too");
        }

        [Test]
        public void Drones_ArriveEveryInterval_UntilTheBerthsAreFull()
        {
            var s = Make(1);
            Run(s, Settlement.BaseDroneInterval - 1f);
            Assert.AreEqual(2, s.Drones);
            Run(s, 1.5f);
            Assert.AreEqual(3, s.Drones);
            Run(s, Settlement.BaseDroneInterval * 5f);
            Assert.AreEqual(3, s.Drones, "a full station takes no more drones");
            Assert.AreEqual(0, s.FreeBerths);

            s.RegisterPlaced(BuildingCategory.Habitat);
            Run(s, Settlement.BaseDroneInterval + 1f);
            Assert.AreEqual(4, s.Drones, "a new station opens berths");
        }

        [Test]
        public void LosingAChargingStation_LosesTheDronesThatNoLongerFit()
        {
            var s = Make(2);
            s.RestoreDrones(6);
            Assert.AreEqual(6, s.Drones);
            s.Unregister(BuildingCategory.Habitat);
            Assert.AreEqual(3, s.Drones);
            s.Unregister(BuildingCategory.Habitat);
            Assert.AreEqual(0, s.Drones);
            Assert.AreEqual(0, s.DroneCapacity);

            var v = Make(1);
            v.AddVillageHab();
            v.RestoreDrones(6);
            v.LoseVillageHab();
            Assert.AreEqual(3, v.Drones);
        }

        [Test]
        public void GeneVault_AddsBerthsAndShortensTheInterval()
        {
            var research = new ResearchManager(new ResourceManager());
            research.RestoreFrom(new System.Collections.Generic.List<int> { (int)TechId.GeneVault }, TechId.None, 0f, 0f);
            var fx = TechEffects.From(research);
            Assert.AreEqual(3, fx.ExtraBeds);
            Assert.Less(fx.GrowIntervalScale, 1f);

            var s = Make(1);
            s.BonusBerths = fx.ExtraBeds;
            s.DroneIntervalScale = fx.GrowIntervalScale;
            Assert.AreEqual(6, s.DroneCapacity);
            Assert.Less(s.DroneInterval, Settlement.BaseDroneInterval);
        }

        // ---------------------------------------------------------------- HUD

        [Test]
        public void ChargersChip_ShowsDronesHousedOverBerths()
        {
            Assert.AreEqual("12/18 drones", HudLayout.ChargersRate(12, 18, 3));
            Assert.AreEqual("2 solar", HudLayout.ChargersRate(0, 0, 2), "no berths yet: the solar count as before");
            Assert.IsNull(HudLayout.ChargersRate(0, 0, 0));
            CollectionAssert.AreEqual(new[] { "12/18 drones", "12/18 dron.", "12/18" },
                HudLayout.ChipRateVariants("12/18 drones"), "fits the chip like the other counts");
        }

        // ---------------------------------------------------------------- Homesteader

        [Test]
        public void Homesteader_NeedsTwentyDronesHousedAtOnce()
        {
            var def = Achievements.Get(AchievementId.Homesteader);
            Assert.AreEqual(20, Achievements.HomesteaderDrones);
            Assert.IsFalse(def.Test(new RunStats { PeakPopulation = 19 }));
            Assert.IsTrue(def.Test(new RunStats { PeakPopulation = 20 }));
            StringAssert.Contains("drones", def.Description);
            StringAssert.DoesNotContain("population", def.Description);
        }

        [Test]
        public void Homesteader_IsEarnedBySevenChargingStationsWithinMinutes()
        {
            var s = Make(7);
            var stats = new RunStats();
            float t = 0f;
            while (s.Drones < Achievements.HomesteaderDrones && t < 3600f)
            {
                s.Tick(0.5f);
                t += 0.5f;
                stats.NotePopulation(s.Drones);
            }
            Assert.IsTrue(Achievements.Get(AchievementId.Homesteader).Test(stats));
            // 18 drones after the starter crew, one every 18 s.
            Assert.AreEqual(18 * Settlement.BaseDroneInterval, t, 1f);

            var six = Make(6);
            Run(six, 3600f);
            Assert.AreEqual(18, six.Drones, "six stations alone top out at 18");
        }

        [Test]
        public void Homesteader_SteamSheetMatchesTheGame()
        {
            string csv = File.ReadAllText(Path.Combine(Application.dataPath, "..", "Tools", "steam", "achievements.csv"));
            StringAssert.Contains(
                "ACH_HOMESTEADER,\"Homesteader\",\"" + Achievements.Get(AchievementId.Homesteader).Description + "\"", csv);
        }

        // ---------------------------------------------------------------- save

        [Test]
        public void SaveRoundTrip_KeepsTheDronesAndTheirTimer()
        {
            var s = Make(5);
            s.RestoreDrones(11);
            Run(s, 5f);
            var save = new SaveGame { body = 0, seed = 9001 };
            save.settlement.population = s.Drones;
            save.settlement.droneSeconds = s.DroneTimer;

            var copy = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            var loaded = Make(5);
            loaded.RestoreDrones(copy.settlement.population, copy.settlement.droneSeconds);
            Assert.AreEqual(11, loaded.Drones);
            Assert.AreEqual(s.DroneTimer, loaded.DroneTimer, 0.001f);

            var fewer = Make(2);
            fewer.RestoreDrones(copy.settlement.population, copy.settlement.droneSeconds);
            Assert.AreEqual(6, fewer.Drones, "clamped to the berths standing");
        }

        [Test]
        public void LegacySave_WithNoDroneCensus_GetsTheStarterCrew()
        {
            var s = Make(4);
            s.RestoreDrones(0, 0f);
            Assert.AreEqual(Settlement.StarterDrones, s.Drones);
            Assert.AreEqual(Settlement.BaseDroneInterval, s.DroneTimer, 0.001f);

            var empty = Make(0);
            empty.RestoreDrones(5);
            Assert.AreEqual(0, empty.Drones, "no station, no drones");
        }

        [Test]
        public void SaveVersion_CoversTheDroneCensus()
        {
            Assert.GreaterOrEqual(SaveGame.CurrentVersion, 8);
        }
    }
}
