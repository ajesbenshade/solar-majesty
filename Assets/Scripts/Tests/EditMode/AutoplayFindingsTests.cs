using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Regressions for bugs the full-campaign autoplay (-autoplay, Tools/qa/run_autoplay.ps1)
    /// found in a release build.
    /// </summary>
    public class AutoplayFindingsTests
    {
        private readonly List<Object> _created = new List<Object>();
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Path.GetTempPath(), "sm-autoplay-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveSystem.DirectoryOverride = _saveRoot;
            PlaytestTelemetry.DirectoryOverride = Path.Combine(_saveRoot, "Playtest");
            StalkerLair.ResetDenPrefabLoaderForTests();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }
            _created.Clear();
            StalkerLair.ResetDenPrefabLoaderForTests();
            SaveSystem.DirectoryOverride = null;
            PlaytestTelemetry.DirectoryOverride = null;
            if (!string.IsNullOrEmpty(_saveRoot) && Directory.Exists(_saveRoot))
                Directory.Delete(_saveRoot, true);
        }

        // ---------------------------------------------------------------- dens that go quiet

        [Test]
        public void QuietDen_ClosesItsClearThreatPole_AndRefundsTheEscrowWhenNobodyEarnedIt()
        {
            var loop = MakeLoop(out var resources, out var flags, metals: 1000);
            var lair = MakeLair(loop, out var resident);
            var pole = FlagBountySync.TryPost(flags, loop.Economy, ClearData(), lair.WorldPosition, 600f);
            Assert.IsNotNull(pole);
            Assert.AreEqual(400, resources.Get(ResourceId.Metals), "posting escrows the bounty");

            resident.SetHealthForTests(0f); // the last resident dies to a hunt, not to the bounty
            lair.Tick();

            Assert.IsTrue(lair.IsCleared);
            Assert.AreEqual(0, flags.Flags.Count, "a pole on a quiet den has nothing left to clear");
            Assert.AreEqual(1000, resources.Get(ResourceId.Metals), "nobody earned it: the escrow comes back");
            Assert.AreEqual(0, loop.Economy.EscrowedMetals);
        }

        [Test]
        public void QuietDen_PaysTheHeroWhoBrokeIn_AndKeepsTheBountySpent()
        {
            var loop = MakeLoop(out var resources, out var flags, metals: 1000);
            var lair = MakeLair(loop, out var resident);
            FlagBountySync.TryPost(flags, loop.Economy, ClearData(), lair.WorldPosition, 600f);
            var hero = MakeHero(flags);
            float before = hero.Credits;

            Assert.IsFalse(lair.ApplyStructureDamage(10f, hero), "a chip, not a break");
            resident.SetHealthForTests(0f);
            lair.Tick();

            Assert.IsTrue(lair.IsCleared);
            Assert.AreEqual(0, flags.Flags.Count);
            Assert.Greater(hero.Credits, before, "the hero on the den is paid");
            Assert.AreEqual(400, resources.Get(ResourceId.Metals), "a paid bounty stays spent");
        }

        [Test]
        public void QuietDen_LeavesPolesOnOtherDensAlone()
        {
            var loop = MakeLoop(out _, out var flags, metals: 2000);
            var lair = MakeLair(loop, out var resident);
            FlagBountySync.TryPost(flags, loop.Economy, ClearData(), lair.WorldPosition, 600f);
            var far = FlagBountySync.TryPost(flags, loop.Economy, ClearData(),
                lair.WorldPosition + new Vector3(80f, 0f, 0f), 600f);

            resident.SetHealthForTests(0f);
            lair.Tick();

            Assert.AreEqual(1, flags.Flags.Count);
            Assert.AreSame(far, flags.Flags[0]);
        }

        [Test]
        public void Objective_AfterTheLastDen_NamesTheFaunaStillHoldingTheGate()
        {
            var go = new GameObject("mission");
            _created.Add(go);
            var mission = go.AddComponent<MissionController>();
            mission.ArmForTests(densCleared: false, sustainComplete: false, launchReady: false);

            mission.SetDenCountsForTests(lairs: 3, uncleared: 1, stalkers: 6);
            StringAssert.StartsWith("Clear dens — 1 left", mission.ObjectiveLabel);

            mission.SetDenCountsForTests(lairs: 3, uncleared: 0, stalkers: 4);
            Assert.IsFalse(mission.DensCleared, "loose fauna still hold the gate");
            StringAssert.DoesNotContain("0 left", mission.ObjectiveLabel, "no den is left to find");
            StringAssert.StartsWith("Dens quiet — hunt the last 4 fauna", mission.ObjectiveLabel);

            mission.SetDenCountsForTests(lairs: 3, uncleared: 0, stalkers: 0);
            Assert.IsTrue(mission.DensCleared);
            StringAssert.StartsWith("Sustain", mission.ObjectiveLabel, "the next gate once the plaza is clear");
        }

        // ---------------------------------------------------------------- loading holds the world

        [Test]
        public void SceneReload_HoldsTheWorldUntilTheNewColonyPicksItsSpeed()
        {
            float saved = Time.timeScale;
            try
            {
                Time.timeScale = 2f; // a colony running at 4x when the player loads a slot
                GameLoop.HoldWorldForSceneLoad();
                Assert.AreEqual(0f, Time.timeScale,
                    "the load frame must not tick research, economy or fauna before Continue holds the world");
            }
            finally
            {
                Time.timeScale = saved;
            }
        }

        // ---------------------------------------------------------------- helpers

        private GameLoop MakeLoop(out ResourceManager resources, out FlagManager flags, int metals)
        {
            var go = new GameObject("autoplay-findings-loop");
            go.SetActive(false); // Awake never runs: no save folder, no GamePrefs
            _created.Add(go);
            var loop = go.AddComponent<GameLoop>();
            resources = new ResourceManager();
            resources.Set(ResourceId.Metals, metals);
            flags = new FlagManager();
            loop.ArmEconomyForTests(resources, flags);
            loop.ArmThreat(new ThreatPressure());
            return loop;
        }

        private StalkerLair MakeLair(GameLoop loop, out DustStalkerAgent resident)
        {
            var go = new GameObject("den");
            _created.Add(go);
            go.transform.position = new Vector3(40f, 0f, 40f);
            var lair = go.AddComponent<StalkerLair>();
            lair.Configure(loop, 1, 8f);
            resident = new GameObject("resident").AddComponent<DustStalkerAgent>();
            _created.Add(resident.gameObject);
            resident.SetHealthForTests(40f);
            lair.BindRestored(resident);
            return lair;
        }

        private SpecialistAgent MakeHero(FlagManager flags)
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            _created.Add(data);
            data.specialistClass = SpecialistClass.DefenseMech;
            SpecialistPersonality.Apply(data);
            var go = new GameObject("Hero");
            _created.Add(go);
            var hero = go.AddComponent<SpecialistAgent>();
            hero.BindForTravelTest(data, flags, new SpecialistBrain());
            return hero;
        }

        private FlagData ClearData()
        {
            var data = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(data);
            data.flagType = FlagType.ClearThreat;
            data.displayName = "Clear Threat";
            data.minBounty = 0;
            data.maxBounty = 100000;
            data.defaultBounty = 800;
            data.workRequired = 80f;
            data.baseRisk = 0.4f;
            return data;
        }
    }
}
