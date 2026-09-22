using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class StabilizationTests
    {
        [Test]
        public void HiddenResearch_RestoresProgressAndBank_WithoutSpendingAgain()
        {
            bool demo = DemoSettings.FirstHourDemo;
            const string key = "SM_CampaignResearch";
            bool had = PlayerPrefs.HasKey(key);
            string previous = PlayerPrefs.GetString(key);
            try
            {
                DemoSettings.FirstHourDemo = true;
                var research = new ResearchManager(new ResourceManager());
                var def = TechCatalog.Get(TechId.MarsShip);
                var prereqs = new List<int>();
                foreach (var id in def.Prerequisites) prereqs.Add((int)id);
                research.RestoreFrom(prereqs, TechId.MarsShip, 17.5f, 8f);
                Assert.AreEqual(TechId.MarsShip, research.ActiveTech);
                Assert.AreEqual(17.5f, research.ActiveProgress);
                Assert.AreEqual(8f, research.BankedScience);
                DemoSettings.FirstHourDemo = false;
                Assert.IsTrue(research.TrySelect(TechId.MarsShip));
                Assert.AreEqual(25.5f, research.ActiveProgress);
                Assert.AreEqual(0f, research.BankedScience);
                research.RestoreFrom(prereqs, TechId.None, 0f, 0f);
                Assert.IsTrue(research.TrySelect(TechId.MarsShip));
                Assert.AreEqual(0f, research.ActiveProgress, "a second load must not inherit old progress");
            }
            finally
            {
                DemoSettings.FirstHourDemo = demo;
                if (had) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void SwitchedResearch_RoundTripsAllProjects_WithoutDuplicatingScience()
        {
            const string key = "SM_CampaignResearch";
            bool had = PlayerPrefs.HasKey(key);
            string previous = PlayerPrefs.GetString(key);
            bool demo = DemoSettings.FirstHourDemo;
            try
            {
                DemoSettings.FirstHourDemo = false;
                var source = new ResearchManager(new ResourceManager());
                var unlocked = new[] { (int)TechId.FieldSurvey };
                source.RestoreFrom(unlocked, TechId.HabOps, 0, 0);
                source.AddScience(8);
                Assert.IsTrue(source.TrySelect(TechId.ExtractBasics));
                source.AddScience(5);
                var restored = new ResearchManager(new ResourceManager());
                restored.RestoreFrom(unlocked, source.ActiveTech, source.ActiveProgress, source.BankedScience, source.CaptureProgress());
                Assert.AreEqual(5, restored.ActiveProgress);
                Assert.IsTrue(restored.TrySelect(TechId.HabOps));
                Assert.AreEqual(8, restored.ActiveProgress);
                Assert.IsTrue(restored.TrySelect(TechId.ExtractBasics));
                Assert.AreEqual(5, restored.ActiveProgress);
            }
            finally
            {
                DemoSettings.FirstHourDemo = demo;
                if (had) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void WorldSnapshots_RemainSeparateFromLatestAutosave()
        {
            var primary = new[] { SaveSystem.WorldPath(CelestialBodyId.Earth), SaveSystem.WorldPath(CelestialBodyId.Luna), SaveSystem.SlotPath(0) };
            var paths = new List<string>();
            foreach (string path in primary) { paths.Add(path); paths.Add(path + ".bak"); }

            var backups = new byte[paths.Count][];
            for (int i = 0; i < paths.Count; i++) backups[i] = File.Exists(paths[i]) ? File.ReadAllBytes(paths[i]) : null;
            try
            {
                var earth = new SaveGame { body = (int)CelestialBodyId.Earth, seed = 123, roster = "1|", label = "Earth" };
                earth.buildings.Add(new SaveBuilding { category = (int)BuildingCategory.Commons, x = 2, y = 3, w = 6, h = 6, progressMilli = 1000 });
                var luna = new SaveGame { body = (int)CelestialBodyId.Luna, seed = 456, label = "Luna" };
                Assert.IsTrue(SaveSystem.WriteWorld(earth));
                Assert.IsTrue(SaveSystem.WriteWorld(luna));
                Assert.IsTrue(SaveSystem.Write(0, luna));
                Assert.IsTrue(SaveSystem.TryReadWorld(CelestialBodyId.Earth, out var restored));
                Assert.IsTrue(restored.MatchesWorld(CelestialBodyId.Earth, 123));
                Assert.IsFalse(restored.MatchesWorld(CelestialBodyId.Luna, 123));
                Assert.IsFalse(restored.MatchesWorld(CelestialBodyId.Earth, 124));
                Assert.AreEqual(1, restored.buildings.Count);
                Assert.AreEqual(2, restored.buildings[0].x);
                Assert.AreEqual("1|", restored.roster);
                Assert.IsTrue(SaveSystem.TryRead(0, out var latest));
                Assert.AreEqual("Luna", latest.label);
                luna.label = "Luna newer";
                Assert.IsTrue(SaveSystem.Write(0, luna));
                File.WriteAllText(SaveSystem.SlotPath(0), "invalid JSON");
                Assert.IsTrue(SaveSystem.TryRead(0, out var recovered));
                Assert.AreEqual("Luna", recovered.label, "corrupt current save recovers the previous complete version");
                SaveSystem.DeleteWorld(CelestialBodyId.Luna);
                Assert.IsTrue(SaveSystem.TryReadWorld(CelestialBodyId.Earth, out _));
                Assert.IsFalse(SaveSystem.TryReadWorld(CelestialBodyId.Luna, out _));
            }
            finally
            {
                for (int i = 0; i < paths.Count; i++)
                    if (backups[i] != null) File.WriteAllBytes(paths[i], backups[i]);
                    else if (File.Exists(paths[i])) File.Delete(paths[i]);
            }
        }

        [Serializable] private class TelemetryEvent { public float t; public string e; public string value; }

        [Test]
        public void Telemetry_UsesValidJsonUnderCommaDecimalLocale()
        {
            var culture = CultureInfo.CurrentCulture;
            bool enabled = PlaytestTelemetry.Enabled;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                PlaytestTelemetry.Enabled = true;
                PlaytestTelemetry.Begin("regression");
                PlaytestTelemetry.Record("culture_test", "value", 1.25f);
                PlaytestTelemetry.Record("escape_test", "value", "tab\tquote\"line\n");
                PlaytestTelemetry.Flush();
                foreach (string line in File.ReadAllLines(PlaytestTelemetry.SessionPath))
                {
                    var row = JsonUtility.FromJson<TelemetryEvent>(line);
                    Assert.GreaterOrEqual(row.t, 0f);
                    if (row.e == "culture_test") Assert.AreEqual("1.25", row.value);
                    if (row.e == "escape_test") StringAssert.Contains("tab\tquote\"", row.value);
                }
            }
            finally { CultureInfo.CurrentCulture = culture; PlaytestTelemetry.Enabled = enabled; }
        }
    }
}
