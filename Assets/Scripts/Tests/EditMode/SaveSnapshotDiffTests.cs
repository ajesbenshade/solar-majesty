using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class SaveSnapshotDiffTests
    {
        private static SaveGame Developed()
        {
            var s = new SaveGame { body = (int)CelestialBodyId.Mars, seed = 77, simSteps = 4000, campaignStamp = "c1" };
            s.stockpile.metals = 420;
            s.stockpile.waterIce = 30;
            s.settlement.population = 6;
            s.research.unlocked = new List<int> { 3, 1 };
            s.research.activeTech = 5;
            s.research.progress.Add(new SaveResearchProgress { tech = 5, science = 12f });
            s.mission.elapsed = 300f;
            s.buildings.Add(new SaveBuilding { category = (int)BuildingCategory.Commons, x = 10, y = 10, w = 4, h = 4, progressMilli = 1000, health = 1f });
            s.buildings.Add(new SaveBuilding { category = (int)BuildingCategory.EngineerWorkshop, x = 20, y = 10, w = 4, h = 4, progressMilli = 1000, health = 0.6f });
            s.buildings.Add(new SaveBuilding { category = (int)BuildingCategory.Habitat, x = 30, y = 10, w = 4, h = 4, progressMilli = 400, health = 1f });
            s.flags.Add(new SaveFlag { flagType = (int)FlagType.Build, px = 8, pz = 8, bounty = 50, escrowMetals = 50, workDone = 3f, claimCount = 1 });
            s.agents.Add(new SaveAgent { specialistClass = (int)SpecialistClass.EngineerBot, px = 5, pz = 5, health = 0.8f, credits = 40, level = 2, claimedFlagIndex = 0 });
            s.agents.Add(new SaveAgent { specialistClass = (int)SpecialistClass.DefenseMech, px = 6, pz = 5, health = 0.3f, credits = 12, downed = true, downedTimer = 4f });
            s.fauna.Add(new SaveFauna { kind = (int)FaunaKind.Stalker, px = 40, pz = 40, health = 0.5f, lairIndex = 0 });
            s.fauna.Add(new SaveFauna { kind = (int)FaunaKind.Creeper, px = 44, pz = 40, health = 1f });
            s.lairs.Add(new SaveLair { px = 40, pz = 42, scouted = true });
            s.lairs.Add(new SaveLair { px = 60, pz = 42, cleared = true });
            s.nodes.Add(new SaveNode { nodeType = 1, px = 15, pz = 25, remaining = 70 });
            return s;
        }

        [Test]
        public void Identical_HasNoDifferences()
        {
            CollectionAssert.IsEmpty(SaveSnapshotDiff.Compare(Developed(), Developed()));
        }

        [Test]
        public void ListOrderAndMetadata_AreIgnored()
        {
            var a = Developed();
            var b = Developed();
            b.buildings.Reverse();
            b.agents.Reverse();
            b.fauna.Reverse();
            b.research.unlocked.Reverse();
            b.label = "other";
            b.savedAtUtc = "2030-01-01";
            b.playSeconds = 999;
            CollectionAssert.IsEmpty(SaveSnapshotDiff.Compare(a, b));
        }

        [Test]
        public void SmallPoseDrift_IsTolerated_LargeIsNot()
        {
            var a = Developed();
            var b = Developed();
            b.agents[0].px += 0.4f;
            b.fauna[1].pz -= 0.5f;
            CollectionAssert.IsEmpty(SaveSnapshotDiff.Compare(a, b));
            b.agents[0].px += 5f;
            var diffs = SaveSnapshotDiff.Compare(a, b);
            Assert.AreEqual(1, diffs.Count, string.Join("\n", diffs));
            StringAssert.Contains(".pos", diffs[0]);
        }

        [Test]
        public void LostState_IsReported()
        {
            var a = Developed();
            var b = Developed();
            b.stockpile.metals = 0;
            b.buildings[1].health = 1f;
            b.buildings[2].progressMilli = 1000;
            b.agents[1].downed = false;
            b.fauna.RemoveAt(0);
            b.lairs[1].cleared = false;
            b.nodes[0].remaining = 100;
            b.flags[0].workDone = 0f;
            var diffs = SaveSnapshotDiff.Compare(a, b);
            string all = string.Join("\n", diffs);
            StringAssert.Contains("stockpile.metals", all);
            StringAssert.Contains(".health", all);
            StringAssert.Contains(".progressMilli", all);
            StringAssert.Contains(".downed", all);
            StringAssert.Contains("fauna.count", all);
            StringAssert.Contains("lairs[1].cleared", all);
            StringAssert.Contains(".remaining", all);
            StringAssert.Contains(".workDone", all);
        }

        [Test]
        public void MovedBuilding_ListsMissingAndUnexpected()
        {
            var a = Developed();
            var b = Developed();
            b.buildings.RemoveAt(2);
            b.buildings.Add(new SaveBuilding { category = (int)BuildingCategory.Habitat, x = 31, y = 10, w = 4, h = 4, progressMilli = 400, health = 1f });
            b.buildings.Add(new SaveBuilding { category = (int)BuildingCategory.Habitat, x = 50, y = 10, w = 4, h = 4, progressMilli = 1000, health = 1f });
            var diffs = SaveSnapshotDiff.Compare(a, b);
            Assert.IsTrue(diffs.Any(d => d.StartsWith("buildings.count")));
            Assert.IsTrue(diffs.Any(d => d.Contains("missing") && d.Contains("0030,0010")));
            Assert.AreEqual(2, diffs.Count(d => d.Contains("unexpected")));
        }

        [Test]
        public void NaNHealth_IsADifference()
        {
            var a = Developed();
            var b = Developed();
            b.agents[0].health = float.NaN;
            Assert.IsNotEmpty(SaveSnapshotDiff.Compare(a, b));
        }

        [Test]
        public void OptionalSections_CompareWhenPresent()
        {
            SaveGame Make()
            {
                var g = Developed();
                g.villageGrowth = new SaveVillageGrowth { hasProject = true, category = 3, x = 4, y = 5, progress = 10f };
                g.collectors = new SaveCollectors();
                g.collectors.agents.Add(new SaveCollector { carry = 30, state = 2, health = 20f });
                g.orbital = new SaveOrbital { cooldowns = new[] { 0f, 12f } };
                return g;
            }
            var a = Make();
            CollectionAssert.IsEmpty(SaveSnapshotDiff.Compare(a, Make()));
            var b = Make();
            b.orbital.cooldowns[1] = 0f;
            b.villageGrowth.hasProject = false;
            Assert.AreEqual(2, SaveSnapshotDiff.Compare(a, b).Count, string.Join("\n", SaveSnapshotDiff.Compare(a, b)));
            var c = Make();
            c.collectors = null;
            Assert.AreEqual(1, SaveSnapshotDiff.Compare(a, c).Count);
        }
    }
}
