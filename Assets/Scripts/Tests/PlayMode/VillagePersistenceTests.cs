using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    public class VillagePersistenceTests
    {
        [UnityTest]
        public IEnumerator VillageAndCollectors_SurviveTwoDiskAndSceneRoundTrips()
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
            var data = loop.VillageData(BuildingCategory.Habitat);
            Assert.IsTrue(loop.TryFindVillagePlot(data, 0, out var cell, out var site));
            loop.Village.RestoreGrowth(new SaveVillageGrowth
            {
                hasProject = true, category = (int)BuildingCategory.Habitat,
                x = cell.x, y = cell.y, progress = 40f, cooldown = 7f, plotSalt = 4
            });
            var director = loop.Village.Collectors;
            director.Tick(0.01f);
            Assert.AreEqual(2, director.Count);
            // Give one collector cargo on its return trip, then injure it through real combat.
            var roster = director.Capture();
            roster.agents[0].carry = 600;
            roster.agents[0].state = 2;
            roster.agents[0].px = site.x;
            roster.agents[0].py = site.y;
            roster.agents[0].pz = site.z;
            director.Restore(roster);
            director.Collectors[0].TakeHit(0.1f);
            director.Collectors[1].TakeHit(100f);
            var expected = loop.CaptureSave("village fixture");
            Assert.AreEqual(1, expected.collectors.agents.Count);
            Assert.Greater(expected.collectors.agents[0].carry, 0);
            Assert.Less(expected.collectors.agents[0].health, MajestyEconomy.CollectorHp);
            int treasury = expected.stockpile.metals;

            for (int round = 0; round < 2; round++)
            {
                Assert.IsTrue(SaveSystem.WriteWorld(expected));
                Assert.IsTrue(SaveSystem.Write(0, expected));
                yield return SceneManager.LoadSceneAsync("LunarOutpost_Sandbox");
                yield return null;
                loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
                loop.ContinueGame();
                loop.PausePlay();
                var actual = loop.CaptureSave("roundtrip");
                Assert.IsTrue(actual.villageGrowth.hasProject);
                Assert.AreEqual(cell.x, actual.villageGrowth.x);
                Assert.AreEqual(cell.y, actual.villageGrowth.y);
                Assert.AreEqual(40f, actual.villageGrowth.progress, 0.001f);
                Assert.AreEqual(7f, actual.villageGrowth.cooldown, 0.001f);
                Assert.AreEqual(4, actual.villageGrowth.plotSalt);
                Assert.IsFalse(loop.Placer.CanFitRect(cell, data.footprintWidth, data.footprintHeight), "unfinished plot stays reserved");
                Assert.AreEqual(expected.buildings.Count, actual.buildings.Count, "restoring must not complete the village building");
                Assert.AreEqual(1, actual.collectors.agents.Count);
                var collector = actual.collectors.agents[0];
                var original = expected.collectors.agents[0];
                Assert.AreEqual(original.health, collector.health, 0.001f);
                Assert.AreEqual(original.carry, collector.carry);
                Assert.AreEqual(original.state, collector.state);
                Assert.AreEqual(original.robberyCooldown, collector.robberyCooldown, 0.001f);
                Assert.AreEqual(original.px, collector.px, 0.1f);
                Assert.AreEqual(original.pz, collector.pz, 0.1f);
                Assert.AreEqual(1, actual.collectors.replacementSeconds.Count);
                Assert.AreEqual(MajestyEconomy.CollectorRespawnSeconds, actual.collectors.replacementSeconds[0], 0.001f);
                Assert.AreEqual(treasury, actual.stockpile.metals, "restoration must neither deposit nor duplicate cargo");
                expected = actual;
            }

            // Restored projects still obey danger and finish through the village path.
            foreach (var enemy in loop.Stalkers)
                if (enemy != null && enemy.IsAlive) enemy.transform.position = new Vector3(10000f, 0f, 10000f);
            var pest = loop.SpawnFaunaAt(FaunaKind.Creeper, site);
            Assert.IsNotNull(pest);
            pest.transform.position = site;
            loop.Village.Tick(1f);
            Assert.IsTrue(loop.Village.VillageHalted);
            Assert.AreEqual(40f, loop.Village.CaptureGrowth().progress, 0.001f);
            pest.transform.position = new Vector3(10000f, 0f, 10000f);
            loop.Village.Tick(5f);
            Assert.IsFalse(loop.Village.VillageBuilding);
            Assert.Greater(loop.CaptureSave("finished").buildings.Count, expected.buildings.Count);

            // No replacement until the saved delay has actually elapsed.
            Assert.AreEqual(1, loop.Village.Collectors.Count);
            loop.Village.Collectors.Tick(MajestyEconomy.CollectorRespawnSeconds);
            Assert.AreEqual(2, loop.Village.Collectors.Count);
        }
    }
}
