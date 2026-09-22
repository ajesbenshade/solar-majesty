using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    public class DevelopedColonySaveTests
    {
        [UnityTest]
        public IEnumerator DevelopedSnapshot_RoundTripsThroughSceneAndDiskTwice()
        {
            if (Application.companyName != "SolarMajestyValidation" && Environment.GetEnvironmentVariable("CI") != "true")
                Assert.Ignore("Requires the isolated validation profile; resets that profile's campaign.");
            SaveSystem.DeleteAll();
            DemoSettings.ClearSave();
            CampaignProgress.ResetCampaign();
            DemoSettings.SetFirstHourDemo(false);
            DemoSettings.RequestBootIntoPlay();
            yield return SceneManager.LoadSceneAsync("LunarOutpost_Sandbox");
            yield return null;
            var loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            loop.PausePlay();
            var saved = loop.CaptureSave("developed fixture");
            saved.tutorialDone = true;
            saved.replay.stance = (int)DoctrineStance.AegisWatch;
            saved.replay.ironman = true;
            saved.stockpile.metals = 456;
            saved.buildings[0].health = 0.63f;
            saved.buildings.Add(Building(BuildingCategory.EngineerWorkshop, 30, 30, 1000));
            saved.buildings.Add(Building(BuildingCategory.EngineerWorkshop, 30, 40, 1000));
            saved.buildings.Add(Building(BuildingCategory.DefenseWorkshop, 40, 30, 1000));
            saved.buildings.Add(Building(BuildingCategory.Habitat, 50, 30, 375));
            saved.research.unlocked = new List<int> { (int)TechId.FieldSurvey };
            saved.research.activeTech = (int)TechId.HabOps;
            saved.research.activeProgress = 7f;
            saved.research.bankedScience = 11f;
            saved.research.progress = new List<SaveResearchProgress>
            {
                new SaveResearchProgress { tech = (int)TechId.HabOps, science = 7f },
                new SaveResearchProgress { tech = (int)TechId.ExtractBasics, science = 9f }
            };
            saved.rosterBlob = SpecialistRoster.Encode(new[]
            {
                new SpecialistRecord { Class = SpecialistClass.EngineerBot, Level = 3, Xp = 90, Credits = 67,
                    ReviveCount = 2, Suit = ShopItemId.SuitHardplate, Accessory = ShopItemId.AnvilRig, Weapon = ShopItemId.AnvilSledge },
                new SpecialistRecord { Class = SpecialistClass.DefenseMech, Level = 2, Xp = 45, Credits = 32, ReviveCount = 1 }
            });
            saved.flags.Add(new SaveFlag { flagType = (int)FlagType.Build, px = 8, pz = 8,
                bounty = 120, escrowMetals = 120, postedWork = 80, workDone = 23 });
            saved.agents.Add(new SaveAgent { specialistClass = (int)SpecialistClass.EngineerBot, px = 8, pz = 8,
                health = 0.71f, fatigue = 0.22f, credits = 67, claimedFlagIndex = 0 });
            saved.agents.Add(new SaveAgent { specialistClass = (int)SpecialistClass.DefenseMech, px = 9, pz = 8,
                health = 0.1f, fatigue = 0.8f, credits = 32, downed = true, downedTimer = 9.5f });
            saved.agents.Add(new SaveAgent { specialistClass = (int)SpecialistClass.EngineerBot, px = 10, pz = 8,
                health = 0.91f, credits = 99, hasVeteranRecord = true,
                veteran = new SpecialistRecord { Class = SpecialistClass.EngineerBot, Level = 6, Xp = 310,
                    Credits = 99, ReviveCount = 4, Suit = ShopItemId.SuitFieldShell } });
            int firstEngineer = saved.agents.FindIndex(a => a.credits == 67);
            int secondEngineer = saved.agents.FindIndex(a => a.credits == 99);
            saved.parties.Add(new SaveParty { id = 7, leaderIndex = secondEngineer,
                memberIndices = new List<int> { firstEngineer, secondEngineer } });
            // The first Engineer exercises old-save fallback on the first load, then its own record on the second.
            if (saved.nodes.Count > 0) saved.nodes[0].remaining = 0;
            if (saved.lairs.Count > 0) { saved.lairs[0].cleared = true; saved.lairs[0].scouted = true; }
            Assert.GreaterOrEqual(saved.lairs.Count, 2, "fixture needs both cleared and occupied dens");
            var occupied = saved.lairs[1];
            occupied.expansionSpawned = true;
            saved.fauna.Clear();
            saved.fauna.Add(new SaveFauna { kind = (int)FaunaKind.Stalker, px = occupied.px, pz = occupied.pz,
                health = 0.62f, lairIndex = 1 });
            saved.mission.elapsed = 123f;
            saved.mission.sustainHold = 7f;
            saved.simSteps = 2460;
            saved.playSeconds = 123;
            Assert.IsTrue(SaveSystem.WriteWorld(saved));
            Assert.IsTrue(SaveSystem.Write(0, saved));
            DemoSettings.WriteCampus(CelestialBodyId.Earth, "");
            DemoSettings.WriteRoster(CelestialBodyId.Earth, "");
            PlayerPrefs.SetInt(ReplayRules.StanceKey, (int)DoctrineStance.OpenHands);

            for (int round = 0; round < 2; round++)
            {
                yield return SceneManager.LoadSceneAsync("LunarOutpost_Sandbox");
                yield return null;
                loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
                loop.ContinueGame();
                loop.PausePlay();
                var actual = loop.CaptureSave("roundtrip");
                Assert.IsTrue(ReplayRules.BlocksManualSavesAndReloads, "Continue preserves the saved Ironman latch");
                Assert.AreEqual(1, actual.parties.Count);
                Assert.AreEqual(7, actual.parties[0].id);
                Assert.AreEqual(2, actual.parties[0].memberIndices.Count);
                Assert.AreEqual(99, actual.agents[actual.parties[0].leaderIndex].credits,
                    "Same-class party members retain the correct leader");
                Assert.AreEqual(saved.buildings.Count, actual.buildings.Count);
                Assert.AreEqual(375, actual.buildings.Single(b => b.category == (int)BuildingCategory.Habitat).progressMilli);
                Assert.AreEqual(0.63f, actual.buildings.Single(b => b.category == (int)BuildingCategory.Commons).health, 0.001f);
                Assert.AreEqual((int)DoctrineStance.AegisWatch, actual.replay.stance);
                Assert.AreEqual(456, actual.stockpile.metals, "loading must not pay escrow or fabrication twice");
                Assert.AreEqual(3, actual.agents.Count);
                var engineer = actual.agents.Single(a => a.specialistClass == (int)SpecialistClass.EngineerBot && a.credits == 67);
                Assert.AreEqual(0.71f, engineer.health, 0.001f);
                Assert.AreEqual(0.22f, engineer.fatigue, 0.001f);
                Assert.AreEqual(67, engineer.credits);
                Assert.AreEqual(0, engineer.claimedFlagIndex);
                var defense = actual.agents.Single(a => a.specialistClass == (int)SpecialistClass.DefenseMech);
                Assert.IsTrue(defense.downed);
                Assert.AreEqual(9.5f, defense.downedTimer, 0.001f);
                Assert.AreEqual(1, actual.flags.Count);
                Assert.AreEqual(23f, actual.flags[0].workDone, 0.001f);
                Assert.AreEqual(120, actual.flags[0].escrowMetals);
                Assert.AreEqual(1, actual.flags[0].claimCount);
                var roster = new List<SpecialistRecord>();
                Assert.IsTrue(SpecialistRoster.TryDecode(actual.rosterBlob, roster));
                var veteran = engineer.veteran;
                Assert.IsTrue(engineer.hasVeteranRecord);
                var second = actual.agents.Single(a => a.specialistClass == (int)SpecialistClass.EngineerBot && a.credits == 99);
                Assert.AreEqual(6, second.veteran.Level);
                Assert.AreEqual(310, second.veteran.Xp);
                Assert.AreEqual(4, second.veteran.ReviveCount);
                Assert.AreEqual(ShopItemId.SuitFieldShell, second.veteran.Suit);
                Assert.AreEqual(3, veteran.Level);
                Assert.AreEqual(90, veteran.Xp);
                Assert.AreEqual(2, veteran.ReviveCount);
                Assert.AreEqual(ShopItemId.SuitHardplate, veteran.Suit);
                Assert.AreEqual(ShopItemId.AnvilRig, veteran.Accessory);
                Assert.AreEqual(ShopItemId.AnvilSledge, veteran.Weapon);
                Assert.AreEqual(7f, actual.research.activeProgress);
                Assert.AreEqual(11f, actual.research.bankedScience);
                Assert.AreEqual(9f, actual.research.progress.Single(p => p.tech == (int)TechId.ExtractBasics).science);
                Assert.AreEqual(123f, actual.mission.elapsed, 0.001f);
                Assert.AreEqual(7f, actual.mission.sustainHold, 0.001f);
                Assert.AreEqual(2460, actual.simSteps);
                if (saved.nodes.Count > 0) Assert.AreEqual(0, actual.nodes[0].remaining);
                Assert.IsTrue(actual.lairs[0].cleared);
                Assert.IsTrue(actual.lairs[0].scouted);
                Assert.IsTrue(actual.lairs[1].expansionSpawned);
                Assert.AreEqual(1, actual.fauna.Count);
                Assert.AreEqual(0.62f, actual.fauna[0].health, 0.001f);
                Assert.AreEqual(1, actual.fauna[0].lairIndex);
                UnityEngine.Object.FindFirstObjectByType<PlanetaryWorldGen>().TickLairs(0);
                Assert.IsFalse(loop.CaptureSave("after den tick").lairs[1].cleared,
                    "recreated live fauna must remain attached to its den after Continue");
            }
        }

        [UnityTest]
        public IEnumerator Continue_UnsupportedOrUnreadableSaveStaysOnTitleAndPreservesFile()
        {
            if (Application.companyName != "SolarMajestyValidation" && Environment.GetEnvironmentVariable("CI") != "true")
                Assert.Ignore("Requires the isolated validation profile; resets that profile's campaign.");
            SaveSystem.DeleteAll();
            DemoSettings.ClearSave();
            CampaignProgress.ResetCampaign();
            DemoSettings.SetFirstHourDemo(false);
            DemoSettings.RequestBootIntoPlay();
            yield return SceneManager.LoadSceneAsync("LunarOutpost_Sandbox");
            yield return null;
            var loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            loop.PausePlay();
            var future = loop.CaptureSave("future");
            future.version = SaveGame.CurrentVersion + 1;
            string json = JsonUtility.ToJson(future);
            System.IO.File.WriteAllText(SaveSystem.SlotPath(0), json);
            loop.ContinueGame();
            Assert.AreEqual(DemoScreen.Title, loop.Screen);
            StringAssert.Contains("newer game version", DemoSettings.ContinueDetail());
            Assert.AreEqual(json, System.IO.File.ReadAllText(SaveSystem.SlotPath(0)));
            SaveSystem.Delete(0);
            System.IO.File.WriteAllText(SaveSystem.SlotPath(0), "unreadable snapshot");
            loop.ContinueGame();
            Assert.AreEqual(DemoScreen.Title, loop.Screen);
            StringAssert.Contains("couldn't read", DemoSettings.ContinueDetail());
            Assert.AreEqual("unreadable snapshot", System.IO.File.ReadAllText(SaveSystem.SlotPath(0)));
        }

        private static SaveBuilding Building(BuildingCategory category, int x, int y, int progress) =>
            new SaveBuilding { category = (int)category, x = x, y = y, w = 4, h = 4, progressMilli = progress, health = 1f };
    }
}
