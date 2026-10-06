using System.Collections.Generic;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class KingdomThreatTests
    {
        private static KingdomThreatTuning One(KingdomSpawnEntry e) =>
            new KingdomThreatTuning { entries = new[] { e } };

        private static KingdomSpawnEntry Entry(int min = 5, int max = 999, float interval = 60f, int cap = 2,
            KingdomSpawnTrigger trigger = KingdomSpawnTrigger.Always) =>
            new KingdomSpawnEntry
            {
                id = "t", kind = FaunaKind.Mite, site = KingdomSpawnSite.Burrow, trigger = trigger,
                minValue = min, maxValue = max, intervalSeconds = interval, countMin = 1, countMax = 1, cap = cap
            };

        [Test]
        public void BelowKingdomValue_NeverSpawns()
        {
            var d = new KingdomThreatDirector(One(Entry(min: 5)));
            var into = new List<KingdomSpawnRequest>();
            for (int i = 0; i < 100; i++)
            {
                d.Tick(10f, 4, 0, _ => 0, into);
                Assert.AreEqual(0, into.Count);
            }
        }

        [Test]
        public void UnlockedTier_SpawnsAfterOneFullInterval()
        {
            var d = new KingdomThreatDirector(One(Entry(min: 5, interval: 60f)));
            var into = new List<KingdomSpawnRequest>();
            d.Tick(59f, 5, 0, _ => 0, into);
            Assert.AreEqual(0, into.Count);
            d.Tick(1f, 5, 0, _ => 0, into);
            Assert.AreEqual(1, into.Count);
            Assert.AreEqual(FaunaKind.Mite, into[0].Kind);
        }

        [Test]
        public void AtCap_Waits_ThenRespawnsAFullIntervalAfterAKill()
        {
            var d = new KingdomThreatDirector(One(Entry(interval: 30f, cap: 1)));
            var into = new List<KingdomSpawnRequest>();
            int alive = 1;
            d.Tick(100f, 10, 0, _ => alive, into);
            Assert.AreEqual(0, into.Count, "capped");

            alive = 0;
            d.Tick(29f, 10, 0, _ => alive, into);
            Assert.AreEqual(0, into.Count);
            d.Tick(1f, 10, 0, _ => alive, into);
            Assert.AreEqual(1, into.Count);
        }

        [Test]
        public void ValueBand_UpperBoundRetiresAnEntry()
        {
            var d = new KingdomThreatDirector(One(Entry(min: 1, max: 5, interval: 10f)));
            var into = new List<KingdomSpawnRequest>();
            d.Tick(10f, 6, 0, _ => 0, into);
            Assert.AreEqual(0, into.Count);
        }

        [Test]
        public void AfterHeroDeath_NeedsADeath()
        {
            var d = new KingdomThreatDirector(One(Entry(min: 0, interval: 10f, trigger: KingdomSpawnTrigger.AfterHeroDeath)));
            var into = new List<KingdomSpawnRequest>();
            d.Tick(10f, 10, 0, _ => 0, into);
            Assert.AreEqual(0, into.Count);
            d.Tick(10f, 10, 1, _ => 0, into);
            Assert.AreEqual(1, into.Count);
        }

        [Test]
        public void Count_NeverExceedsRemainingCap()
        {
            var e = Entry(interval: 1f, cap: 3);
            e.countMin = 3; e.countMax = 3;
            var d = new KingdomThreatDirector(One(e));
            var into = new List<KingdomSpawnRequest>();
            d.Tick(1f, 10, 0, _ => 2, into);
            Assert.AreEqual(1, into.Count);
            Assert.AreEqual(1, into[0].Count);
        }

        [Test]
        public void Disabled_SpawnsNothing()
        {
            var t = One(Entry(interval: 1f));
            t.enabled = false;
            var d = new KingdomThreatDirector(t);
            var into = new List<KingdomSpawnRequest>();
            d.Tick(100f, 50, 5, _ => 0, into);
            Assert.AreEqual(0, into.Count);
        }

        [Test]
        public void Worth_MatchesMajestyWeights_AndSafetyFallsOff()
        {
            var t = new KingdomThreatTuning();
            Assert.AreEqual(1f, t.WorthOf(BuildingCategory.Commons).kingdomValue, 1e-4f);
            Assert.AreEqual(0f, t.WorthOf(BuildingCategory.Habitat).kingdomValue, 1e-4f);
            Assert.AreEqual(0.5f, t.WorthOf(BuildingCategory.DefenseWorkshop).kingdomValue, 1e-4f);
            Assert.AreEqual(1f, t.SafetyFrom(BuildingCategory.Commons, 0f), 1e-4f);
            Assert.AreEqual(0.5f, t.SafetyFrom(BuildingCategory.Commons, 15f), 1e-4f);
            Assert.AreEqual(0f, t.SafetyFrom(BuildingCategory.Commons, 31f), 1e-4f);
        }

        [Test]
        public void DefaultTable_RaidersAreEliteAndNeedAnEstablishedColony()
        {
            var entries = KingdomThreatTuning.DefaultEntries();
            bool found = false;
            foreach (var e in entries)
            {
                if (e.site != KingdomSpawnSite.ColonyEdge) continue;
                found = true;
                Assert.IsTrue(e.elite);
                Assert.GreaterOrEqual(e.minValue, 3f);
            }
            Assert.IsTrue(found);
        }
    }
}
