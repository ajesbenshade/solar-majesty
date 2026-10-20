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

        [Test]
        public void PressureWaves_TopUpTheLooseFauna_ButNeverPastWhatContinueKeeps()
        {
            Assert.AreEqual(OverseerRules.MaxFaunaPerDen, OverseerRules.MaxPressureFauna,
                "live pressure and the Continue cap (FaunaRestoreCap) must agree");
            Assert.AreEqual(2, OverseerRules.PressureWaveSize(2, 0));
            Assert.AreEqual(2, OverseerRules.PressureWaveSize(2, OverseerRules.MaxPressureFauna - 2));
            Assert.AreEqual(1, OverseerRules.PressureWaveSize(2, OverseerRules.MaxPressureFauna - 1));
            Assert.AreEqual(0, OverseerRules.PressureWaveSize(2, OverseerRules.MaxPressureFauna));
            Assert.AreEqual(0, OverseerRules.PressureWaveSize(2, 80), "the 80-stalker pile-up a slow Earth reached");

            // An hour of waves on a colony that never fights back stays at the cap.
            int loose = 0;
            for (int wave = 0; wave < 48; wave++)
                loose += OverseerRules.PressureWaveSize(2, loose);
            Assert.AreEqual(OverseerRules.MaxPressureFauna, loose);
        }

        [Test]
        public void RaidingPests_SitOnTheWall_WhereARobotCanStrikeThem()
        {
            var raided = new[]
            {
                BuildingCategory.Farm, BuildingCategory.Mine, BuildingCategory.RegolithCamp,
                BuildingCategory.Power, BuildingCategory.Habitat
            };
            const float agentRadius = 0.4f; // SpecialistAgent's NavMeshAgent
            foreach (var cat in raided)
            {
                float half = ColonyLayout.FootprintMeters(cat) * 0.5f;
                float standOff = DustStalkerAgent.RaidStandOff(cat);
                Assert.Greater(standOff, half,
                    $"{cat}: a raider inside the carved footprint is out of every robot's reach");
                Assert.LessOrEqual(Mathf.Abs(standOff - (half + agentRadius)), KingdomLife.HuntRange,
                    $"{cat}: a robot at the wall must have the raider in melee");
            }
        }

        // ---------------------------------------------------------------- the way home across a river

        [Test]
        public void WalkRoute_HomeAcrossARiver_FordsInsteadOfStoppingAtTheBank()
        {
            // The hero forded east to a den; home is back across a 10 m river (no dry way round).
            System.Func<Vector3, bool> river = p => p.x > 20f && p.x < 30f;
            Vector3 hero = new Vector3(45f, 0f, 3f), home = new Vector3(0f, 0f, 0f);

            Assert.IsTrue(NavCornerPath.TryWalkRoute(hero, home, river, out var corners),
                "a hero across the river must still have a way home");
            bool crosses = false;
            for (int i = 1; i < corners.Length; i++)
                if (corners[i - 1].x >= 30f && corners[i].x <= 20f) crosses = true;
            Assert.IsTrue(crosses, "the route steps from the far bank to the near bank");
            Assert.Less(NavCornerPath.Flat(corners[corners.Length - 1], home), 6f, "and ends at home");
        }

        [Test]
        public void WalkRoute_TooWideToFord_HasNoRoute()
        {
            System.Func<Vector3, bool> lake = p => p.x > 10f && p.x < 10f + NavCornerPath.MaxFordMeters + 12f;
            Assert.IsFalse(NavCornerPath.TryWalkRoute(new Vector3(70f, 0f, 0f), Vector3.zero, lake, out _));
        }

        // ---------------------------------------------------------------- a pole inside a building

        [Test]
        public void FlagApproach_AtTheWallOfABuilding_WorksThePoleFromThere()
        {
            var watch = new FlagApproachWatch();
            float t = 0f;
            bool work = false;
            // Walks in from 40 m, then stops at the Commons wall 3 m from a pole inside it.
            for (float d = 40f; d > 3f; d -= 0.5f) Assert.IsFalse(watch.Note(d, 0.1f), "still closing");
            for (int i = 0; i < 100 && !work; i++) { t += 0.1f; work = watch.Note(3f, 0.1f); }
            Assert.IsTrue(work, "a hero at the wall works the pole instead of walking into it forever");
            Assert.LessOrEqual(t, FlagApproachWatch.Seconds + 0.2f);
        }

        [Test]
        public void FlagApproach_FarFromThePole_KeepsWalking()
        {
            var watch = new FlagApproachWatch();
            watch.Note(60f, 0.1f);
            for (int i = 0; i < 200; i++)
                Assert.IsFalse(watch.Note(60f, 0.1f), "a stuck hero 60 m out does not count as at the pole");
        }

        // ---------------------------------------------------------------- the walk to the guild till

        [Test]
        public void DutyWalk_ThatStopsClosing_GivesUp_KeepsTheTax_AndTriesAgainLater()
        {
            var t = new EconomyTuning();
            var purse = new DutyPurse();
            purse.Add(300);
            Assert.IsTrue(purse.WantsToPay(t, 0f));

            float now = 0f;
            bool gaveUp = false;
            purse.NoteWalk(6f, 0.5f, now); // first sample: the hero is parked at a boxed-in till
            for (int i = 0; i < 40 && !gaveUp; i++)
            {
                now += 0.5f;
                gaveUp = purse.NoteWalk(6f, 0.5f, now);
            }

            Assert.IsTrue(gaveUp, "a walk that cannot finish must end");
            Assert.LessOrEqual(now, DutyPurse.WalkPatience + 1f);
            Assert.AreEqual(300, purse.Carry, "giving up keeps the tax");
            Assert.IsFalse(purse.WantsToPay(t, now + 1f), "back to bounties and hunting for a while");
            Assert.IsTrue(purse.WantsToPay(t, now + DutyPurse.RetrySeconds + 0.1f), "then it walks again");
        }

        [Test]
        public void DutyWalk_ThatKeepsClosing_NeverGivesUp()
        {
            var purse = new DutyPurse();
            purse.Add(300);
            float distance = 120f, now = 0f;
            for (int i = 0; i < 200; i++) // 100 s of a long walk home
            {
                now += 0.5f;
                distance -= 0.55f;
                Assert.IsFalse(purse.NoteWalk(distance, 0.5f, now), $"gave up at {now}s while closing");
            }
        }

        [Test]
        public void DutyWalk_Paying_ClearsTheRetry()
        {
            var t = new EconomyTuning();
            var purse = new DutyPurse();
            purse.Add(300);
            purse.NoteWalk(6f, 1f, 0f);
            Assert.IsTrue(purse.NoteWalk(6f, DutyPurse.WalkPatience + 1f, 20f));
            Assert.IsFalse(purse.WantsToPay(t, 21f));
            Assert.AreEqual(300, purse.Pay());
            purse.Add(300);
            Assert.IsTrue(purse.WantsToPay(t, 21f), "a hero who paid is not still waiting out an old give-up");
        }

        [Test]
        public void DutyWalk_ToTheCommons_FindsGroundAtItsWalls_AndPaysThere()
        {
            var t = new EconomyTuning();
            float half = ColonyLayout.FootprintMeters(BuildingCategory.Commons) * 0.5f;
            float sample = SpecialistAgent.TillSampleRadius(BuildingCategory.Commons);
            // The ordinary 5 m sample from the castle's centre lands inside its walls: no mesh, no path.
            Assert.Greater(half, 5f, "the Commons is wider than an ordinary walk samples");
            Assert.Greater(sample, half + 1f, "the tax walk looks past the walls for ground");
            foreach (var till in new[]
                     {
                         BuildingCategory.Commons, BuildingCategory.DefenseWorkshop,
                         BuildingCategory.EngineerWorkshop, BuildingCategory.GuildHall
                     })
            {
                Assert.Greater(SpecialistAgent.TillReach(till, t.taxPayArrive), SpecialistAgent.TillSampleRadius(till),
                    $"{till}: wherever the walk can end, the tax is handed in");
            }
        }

        // ---------------------------------------------------------------- the run tallies

        [Test]
        public void RunStats_DenBrokenWithAHeroOnIt_CountsTheDenAndThePaidBounty()
        {
            var loop = MakeLoop(out _, out var flags, metals: 1000);
            var lair = MakeLair(loop, out var resident);
            FlagBountySync.TryPost(flags, loop.Economy, ClearData(), lair.WorldPosition, 600f);
            var hero = MakeHero(flags);
            lair.ApplyStructureDamage(10f, hero);
            resident.SetHealthForTests(0f);
            lair.Tick();

            Assert.AreEqual(1, loop.Stats.DensCleared);
            Assert.AreEqual(1, loop.Stats.FlagsCompleted, "Terms Accepted reads this");
            Assert.AreEqual(600, loop.Stats.BountyPaid);
        }

        [Test]
        public void RunStats_QuietDenNobodyWorked_CountsTheDenButNoBounty()
        {
            var loop = MakeLoop(out _, out var flags, metals: 1000);
            var lair = MakeLair(loop, out var resident);
            FlagBountySync.TryPost(flags, loop.Economy, ClearData(), lair.WorldPosition, 600f);
            resident.SetHealthForTests(0f);
            lair.Tick();

            Assert.AreEqual(1, loop.Stats.DensCleared);
            Assert.AreEqual(0, loop.Stats.FlagsCompleted, "the escrow went back; nobody earned it");
            Assert.AreEqual(0, loop.Stats.BountyPaid);
        }

        [Test]
        public void RunStats_EveryKill_IsCounted()
        {
            var loop = MakeLoop(out _, out _, metals: 0);
            loop.OnFaunaKilled(FaunaKind.Stalker, Vector3.zero);
            loop.OnFaunaKilled(FaunaKind.JunkBot, Vector3.zero, 0f);
            Assert.AreEqual(2, loop.Stats.FaunaKilled);
        }

        /// <summary>
        /// The summary screen, the verdict and the achievements read these tallies; only four were
        /// ever recorded. Every bounty run ended "You posted nothing", and Terms Accepted, Hard Bargain,
        /// Skinflint, Full Crew, Den Breaker and Rebuilder could never be earned while Clean Sheet came
        /// free with any conquest (autoplay: the verdict after 73 bounties).
        /// </summary>
        [Test]
        public void RunStats_EveryTallyTheGameReads_IsRecordedSomewhere()
        {
            var sources = new System.Text.StringBuilder();
            foreach (var dir in new[] { "Scripts/Runtime", "Scripts/Systems" })
                foreach (var file in Directory.GetFiles(Path.Combine(Application.dataPath, dir), "*.cs", SearchOption.AllDirectories))
                    if (!file.EndsWith("RunStats.cs"))
                        sources.Append(File.ReadAllText(file)).Append('\n');
            string code = sources.ToString();

            var recordedBy = new Dictionary<string, string>
            {
                { "FlagsPosted", "Stats.FlagsPosted++" },
                { "FlagsCompleted", "Stats.FlagsCompleted++" },
                { "FlagsRefused", "Stats.FlagsRefused++" },
                { "HighestBounty", "Stats.NoteBounty(" },
                { "BountyPaid", "Stats.BountyPaid +=" },
                { "TitheCollected", "Stats.TitheCollected +=" },
                { "ModulesBuilt", "Stats.ModulesBuilt++" },
                { "ModulesLost", "Stats.ModulesLost++" },
                { "RobotsFabricated", "Stats.RobotsFabricated++" },
                { "RobotsScrapped", "Stats.RobotsScrapped++" },
                { "FaunaKilled", "Stats.FaunaKilled++" },
                { "DensCleared", "Stats.DensCleared++" },
                { "TechUnlocked", "Stats.TechUnlocked++" },
                { "BodiesConquered", "Stats.BodiesConquered++" },
            };
            foreach (var kv in recordedBy)
                StringAssert.Contains(kv.Value, code, $"RunStats.{kv.Key} is read by the summary, verdict or an achievement but never recorded");
            // Not checked: PeakPopulation (Homesteader, "Reach a population of 20"). The gold-only
            // economy took population out of the game; that achievement needs a design decision.
        }

        // ---------------------------------------------------------------- a hire or re-fab across a save

        [Test]
        public void Load_WorkshopStillWaitingForItsHire_GoesBackInTheHireQueue()
        {
            var loop = MakeLoop(out _, out var flags, metals: 0);
            var shop = MakeWorkshop(BuildingCategory.DefenseWorkshop);
            // The campus restore fabricated a robot there; the save had none, so the roster drops it.
            var dropped = MakeHero(flags);
            shop.MarkRobotFabricated();
            Assert.IsTrue(shop.TryClockIn(dropped));

            loop.ReturnToHireQueue(dropped, new[] { shop }, new Dictionary<SpecialistClass, int>());

            Assert.IsFalse(shop.RobotFabricated, "nobody is there: the workshop does not say its robot is online");
            Assert.IsTrue(loop.IsAwaitingHire(shop), "it hires as soon as the treasury can pay, as it would have before the save");
            Assert.AreEqual(1, loop.AwaitingHireCount);
            CollectionAssert.DoesNotContain(shop.Workers, dropped);
        }

        [Test]
        public void Load_ReFabUnderWay_BringsTheRobotBackWithoutASecondHire()
        {
            var loop = MakeLoop(out _, out var flags, metals: 0);
            var shop = MakeWorkshop(BuildingCategory.DefenseWorkshop);
            var dropped = MakeHero(flags);
            shop.MarkRobotFabricated();
            Assert.IsTrue(shop.TryClockIn(dropped));
            var refabs = new Dictionary<SpecialistClass, int> { { SpecialistClass.DefenseMech, 1 } };

            loop.ReturnToHireQueue(dropped, new[] { shop }, refabs);

            Assert.IsFalse(loop.IsAwaitingHire(shop), "the re-fab brings this robot back; no hire on top of it");
            Assert.IsFalse(shop.RobotFabricated, "the re-fab finishing fabricates there");
            Assert.AreEqual(0, refabs[SpecialistClass.DefenseMech], "one re-fab covers one robot");
        }

        [Test]
        public void Save_ReFabsUnderWay_RoundTripThroughTheSaveFile()
        {
            var save = new SaveGame();
            save.refabs.Add(new SaveRefab
            {
                specialistClass = (int)SpecialistClass.DefenseMech, px = 12.5f, pz = -3f, progressSeconds = 17f
            });
            var back = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            Assert.AreEqual(1, back.refabs.Count);
            Assert.AreEqual((int)SpecialistClass.DefenseMech, back.refabs[0].specialistClass);
            Assert.AreEqual(12.5f, back.refabs[0].px);
            Assert.AreEqual(-3f, back.refabs[0].pz);
            Assert.AreEqual(17f, back.refabs[0].progressSeconds);

            var older = JsonUtility.FromJson<SaveGame>("{\"version\":7,\"body\":2}");
            Assert.IsNotNull(older.refabs, "a save from before re-fabs were kept still loads");
            Assert.AreEqual(0, older.refabs.Count);
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

        private ColonyStructure MakeWorkshop(BuildingCategory category)
        {
            var go = new GameObject("workshop");
            _created.Add(go);
            var st = go.AddComponent<ColonyStructure>();
            st.Configure(StructureRole.Workshop, null, 48f, category);
            return st;
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
