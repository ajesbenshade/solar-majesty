using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>Command log files, world keys, architecture by trait and several overseers in one process.</summary>
    public class Phase6AFoundationTests
    {
        private readonly List<Object> _created = new List<Object>();
        private FlagData _explore;
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _explore = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(_explore);
            _explore.flagType = FlagType.Explore;
            _explore.minBounty = 10;
            _explore.maxBounty = 2000;
            _dir = Path.Combine(Path.GetTempPath(), "sm-commandlog-" + System.Guid.NewGuid().ToString("N"));
            CelestialBodyCatalog.ClearRegistered();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
                Object.DestroyImmediate(_created[i]);
            _created.Clear();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            CelestialBodyCatalog.ClearRegistered();
        }

        private ColonySim NewSim(int seed = 5)
        {
            var sim = new ColonySim(seed) { FlagCatalog = _ => _explore };
            sim.Resources.Set(ResourceId.Metals, 5000);
            return sim;
        }

        // ---- Command log ------------------------------------------------------------------

        [Test]
        public void CommandLog_IsRequestedOnlyByItsLaunchArgument()
        {
            Assert.IsTrue(CommandLog.Requested(new[] { "SolarMajesty.exe", "-RecordCommands" }));
            Assert.IsFalse(CommandLog.Requested(new[] { "SolarMajesty.exe", "-devtools" }));
            Assert.IsFalse(CommandLog.Requested(null));
        }

        /// <summary>A log written by one session, read back from disk, replays to the hash it recorded.</summary>
        [Test]
        public void CommandLog_WrittenToDisk_ReplaysToItsRecordedHash()
        {
            ColonySim live = NewSim();
            live.Commands.Record = true;
            live.Commands.Enqueue(OverseerCommand.PostFlag(FlagType.Explore, new Vector3(3f, 0f, 9f), 300f));
            live.RunSteps(50);
            live.Commands.Enqueue(OverseerCommand.StepBounty(1, 100f));
            live.RunSteps(450);

            string path = CommandLog.From(live, "MARS", 5, "0.9.0").Write(_dir, "session.json");
            Assert.IsNotNull(path);
            Assert.IsTrue(CommandLog.TryRead(path, out CommandLog read));

            ColonySim replayed = NewSim(read.seed);
            replayed.Replay(read.commands, read.steps);

            Assert.AreEqual(2, read.commands.Count);
            Assert.AreEqual("MARS", read.body);
            Assert.AreEqual(500, read.steps);
            Assert.AreEqual(read.stateHash, replayed.StateHash().ToString("x16"));
        }

        /// <summary>A session that began with Continue replays from the colony it resumed.</summary>
        [Test]
        public void CommandLog_OfAResumedSession_ReplaysFromItsStartingSave()
        {
            ColonySim earlier = NewSim();
            earlier.Commands.Enqueue(OverseerCommand.PostFlag(FlagType.Explore, new Vector3(3f, 0f, 9f), 300f));
            earlier.RunSteps(200);
            var resumedFrom = new SaveGame();
            earlier.CaptureInto(resumedFrom);

            // The next session: Continue, then play on with recording switched on.
            ColonySim live = NewSim();
            live.RestoreFrom(resumedFrom);
            Assert.AreEqual(earlier.StateHash(), live.StateHash(), "a restored sim stands where the save was taken");
            live.Commands.Record = true;
            live.Commands.Enqueue(OverseerCommand.StepBounty(1, 100f));
            live.RunSteps(300);

            CommandLog log = CommandLog.From(live, "MARS", 5, "0.9.0");
            log.BeginFrom(resumedFrom);
            string path = log.Write(_dir, "resumed.json");
            Assert.IsTrue(CommandLog.TryRead(path, out CommandLog read));
            Assert.IsTrue(read.resumed);

            ColonySim replayed = read.NewSim(_ => _explore);
            replayed.Replay(read.commands, read.steps);
            Assert.AreEqual(read.stateHash, replayed.StateHash().ToString("x16"));

            // From the bare seed the same commands land on a different colony.
            ColonySim fromSeed = NewSim(read.seed);
            fromSeed.Clock.Restore(resumedFrom.simSteps);
            fromSeed.Replay(read.commands, read.steps);
            Assert.AreNotEqual(read.stateHash, fromSeed.StateHash().ToString("x16"));
        }

        [Test]
        public void SaveSystem_GivesARegisteredWorldItsOwnFilesAndPrefs()
        {
            CelestialBodyProfile ares = CelestialBodyCatalog.Mars().Copy();
            ares.Key = "test.ares/2";

            Assert.AreEqual("2", CelestialBodyCatalog.StorageId(CelestialBodyCatalog.Mars()), "a Sol world keeps its number");
            Assert.AreEqual("-test_ares_2", CelestialBodyCatalog.StorageId(ares));
            Assert.AreEqual(SaveSystem.WorldPath(CelestialBodyId.Mars), SaveSystem.WorldPath(CelestialBodyCatalog.Mars()));
            Assert.AreNotEqual(SaveSystem.WorldPath(CelestialBodyId.Mars), SaveSystem.WorldPath(ares));
            Assert.AreEqual(DemoSettings.CampusKey(CelestialBodyId.Mars), DemoSettings.CampusKey(CelestialBodyCatalog.Mars()));
            Assert.AreNotEqual(DemoSettings.RosterKey(CelestialBodyId.Mars), DemoSettings.RosterKey(ares));

            var onAres = new SaveGame { body = (int)CelestialBodyId.Mars, bodyKey = ares.Key };
            Assert.IsTrue(onAres.IsWorld(ares));
            Assert.IsFalse(onAres.IsWorld(CelestialBodyCatalog.Mars()));
        }

        [Test]
        public void CommandLog_TryRead_OfAMissingFile_IsFalse()
        {
            Assert.IsFalse(CommandLog.TryRead(Path.Combine(_dir, "nope.json"), out CommandLog log));
            Assert.IsNull(log);
        }

        [Test]
        public void CommandLog_FileName_NamesTheBodyAndSeed()
        {
            string name = CommandLog.FileName("MARS", 10007, new System.DateTime(2026, 11, 9, 20, 4, 3));

            Assert.AreEqual("commands-mars-10007-20261109-200403.json", name);
        }

        // ---- Run rules and scene-free commands ----------------------------------------------

        [Test]
        public void StateHash_SeesTheRunRules_AndTheLogCarriesThem()
        {
            ColonySim plain = NewSim();
            ColonySim ironman = NewSim();
            ironman.Rules = new RunRules { ironman = true };
            ironman.BodyKey = "sol.mars";

            Assert.AreNotEqual(plain.StateHash(), ironman.StateHash());

            CommandLog log = CommandLog.From(ironman, "MARS", ironman.Seed, "0.9.0");
            Assert.AreEqual("sol.mars", log.bodyKey);
            Assert.IsTrue(log.rules.ironman);
            Assert.AreEqual(5, log.seed);
        }

        [Test]
        public void GuildBenefit_WithoutAHall_ChargesNothing()
        {
            ColonySim sim = NewSim();
            sim.Commands.Enqueue(OverseerCommand.ActivateGuildBenefit((RobotGuildId)0));

            sim.RunSteps(1);

            Assert.AreEqual(5000, sim.Resources.Get(ResourceId.Metals));
        }

        [Test]
        public void OrbitalCharge_OfALockedPower_ChargesNothing()
        {
            ColonySim sim = NewSim();
            sim.Commands.Enqueue(OverseerCommand.CastOrbital((OrbitalPowerId)0, Vector3.zero));

            sim.RunSteps(1);

            Assert.AreEqual(5000, sim.Resources.Get(ResourceId.Metals));
        }

        [Test]
        public void RunRules_CarryTheirOwnEffects_WhateverTheProcessSettingsSay()
        {
            ReplayRules.ResetForTests();
            var swarm = new RunRules { mode = (int)ColonyRunMode.Endless, challenge = (int)ChallengeId.Swarm, stance = (int)DoctrineStance.SurveyFirst };

            Assert.IsTrue(swarm.IsEndless);
            Assert.AreEqual(1.50f, swarm.FaunaCapScale);
            Assert.AreEqual(1.50f, swarm.ConsiderRangeScale);
            Assert.IsFalse(ReplayRules.IsEndless, "the process-wide settings are untouched");
            Assert.AreEqual(1f, ReplayRules.FaunaCapScale);

            ReplayRules.Challenge = ChallengeId.TightPurse;
            Assert.AreEqual(80, ReplayRules.ExtraDockFee, "the static view reads the same definitions");
            Assert.AreEqual(80, RunRules.Current().ExtraDockFee);
            ReplayRules.ResetForTests();
        }

        // ---- World keys -------------------------------------------------------------------

        [Test]
        public void SolWorlds_HaveUniqueKeys_ThatFindThemAgain()
        {
            var seen = new HashSet<string>();
            foreach (CelestialBodyId id in CelestialBodyCatalog.All)
            {
                string key = CelestialBodyCatalog.KeyOf(id);

                Assert.IsTrue(key.StartsWith(CelestialBodyCatalog.SolPrefix), key);
                Assert.IsTrue(seen.Add(key), "duplicate key " + key);
                Assert.IsTrue(CelestialBodyCatalog.TryGet(key, out CelestialBodyProfile found));
                Assert.AreEqual(id, found.Id);
            }
        }

        [Test]
        public void Register_AddsAGeneratedWorld_FoundByKey()
        {
            var world = new CelestialBodyProfile { Key = "kepler-442.b", DisplayName = "Kepler-442 b" };

            Assert.IsTrue(CelestialBodyCatalog.Register(world));

            Assert.IsTrue(CelestialBodyCatalog.TryGet("kepler-442.b", out CelestialBodyProfile found));
            Assert.AreSame(world, found);
            Assert.AreEqual(1, CelestialBodyCatalog.RegisteredCount);
        }

        [Test]
        public void Register_RefusesAMissingKey_ATakenKey_AndTheSolNamespace()
        {
            Assert.IsFalse(CelestialBodyCatalog.Register(null));
            Assert.IsFalse(CelestialBodyCatalog.Register(new CelestialBodyProfile()));
            Assert.IsFalse(CelestialBodyCatalog.Register(new CelestialBodyProfile { Key = "sol.mars" }));
            Assert.IsFalse(CelestialBodyCatalog.Register(new CelestialBodyProfile { Key = "sol.vulcan" }));

            Assert.IsTrue(CelestialBodyCatalog.Register(new CelestialBodyProfile { Key = "tau-ceti.e" }));
            Assert.IsFalse(CelestialBodyCatalog.Register(new CelestialBodyProfile { Key = "tau-ceti.e" }));

            Assert.IsFalse(CelestialBodyCatalog.TryGet("", out _));
            Assert.IsFalse(CelestialBodyCatalog.TryGet("nowhere", out _));
        }

        [Test]
        public void SaveMatchesWorld_ByKey_SoABorrowedLookIsNotTheSolWorld()
        {
            CelestialBodyProfile ares = CelestialBodyCatalog.Mars().Copy();
            ares.Key = "test.ares";
            var onAres = new SaveGame { body = (int)CelestialBodyId.Mars, seed = 7, bodyKey = "test.ares" };
            var legacyMars = new SaveGame { body = (int)CelestialBodyId.Mars, seed = 7 };

            Assert.IsTrue(onAres.MatchesWorld(ares, 7));
            Assert.IsFalse(onAres.MatchesWorld(CelestialBodyCatalog.Mars(), 7));
            Assert.IsFalse(onAres.MatchesWorld(ares, 8));
            Assert.IsTrue(legacyMars.MatchesWorld(CelestialBodyCatalog.Mars(), 7), "a save from before keys is the Sol world");
            Assert.IsFalse(legacyMars.MatchesWorld(ares, 7));
            Assert.AreSame(CelestialBodyCatalog.Mars().GroundCover, ares.GroundCover, "a copy shares arrays");
            Assert.AreEqual("sol.mars", CelestialBodyCatalog.Mars().Key, "copying does not rename the original");
        }

        // ---- Catalogs as assets -----------------------------------------------------------

        [Test]
        public void TechCatalog_ReadsADatabase_AndGoesBackToTheBuiltInTree()
        {
            int builtIn = TechCatalog.All.Count;
            var db = ScriptableObject.CreateInstance<TechDatabase>();
            _created.Add(db);
            db.CopyFrom(TechCatalog.All);
            db.entries.RemoveRange(2, db.entries.Count - 2);
            db.entries[0].scienceCost = 999f;

            try
            {
                TechCatalog.Reload(db);

                Assert.AreEqual(2, TechCatalog.All.Count);
                Assert.AreEqual(999f, TechCatalog.All[0].ScienceCost);
                Assert.AreEqual(db.entries[1].displayName, TechCatalog.Get(db.entries[1].id).DisplayName);
            }
            finally
            {
                TechCatalog.Reload();
            }

            Assert.AreEqual(builtIn, TechCatalog.All.Count);
        }

        [Test]
        public void TechDatabase_RoundTripsTheBuiltInTree()
        {
            TechCatalog.Reload(useAsset: false);
            var db = ScriptableObject.CreateInstance<TechDatabase>();
            _created.Add(db);
            db.CopyFrom(TechCatalog.All);

            TechDef[] back = db.ToDefs();

            Assert.AreEqual(TechCatalog.All.Count, back.Length);
            for (int i = 0; i < back.Length; i++)
            {
                TechDef a = TechCatalog.All[i];
                Assert.AreEqual(a.Id, back[i].Id);
                Assert.AreEqual(a.DisplayName, back[i].DisplayName);
                Assert.AreEqual(a.ScienceCost, back[i].ScienceCost);
                Assert.AreEqual(a.Prerequisites.Length, back[i].Prerequisites.Length);
                Assert.AreEqual(a.UnlocksLaunch, back[i].UnlocksLaunch);
                Assert.AreEqual(a.CompleteCost == null ? 0 : a.CompleteCost.Length,
                    back[i].CompleteCost == null ? 0 : back[i].CompleteCost.Length);
            }
            TechCatalog.Reload();
        }

        [Test]
        public void ShopCatalog_ReadsADatabase_AndGoesBackToTheShippedCatalog()
        {
            int shipped = ShopCatalog.All.Count;
            var db = ScriptableObject.CreateInstance<ShopDatabase>();
            _created.Add(db);
            db.CopyFrom(ShopCatalog.All);
            db.entries.RemoveRange(3, db.entries.Count - 3);
            db.entries[0].cost = 12345;

            try
            {
                ShopCatalog.Reload(db);

                Assert.AreEqual(3, ShopCatalog.All.Count);
                Assert.AreEqual(12345, ShopCatalog.Get(db.entries[0].id).Cost);
            }
            finally
            {
                ShopCatalog.Reload();
            }

            Assert.AreEqual(shipped, ShopCatalog.All.Count);
        }

        [Test]
        public void ShopDatabase_RoundTripsTheBuiltInCatalog()
        {
            ShopCatalog.Reload(useAsset: false);
            try
            {
                var db = ScriptableObject.CreateInstance<ShopDatabase>();
                _created.Add(db);
                db.CopyFrom(ShopCatalog.All);
                List<ShopItemDef> back = db.ToDefs();

                Assert.AreEqual(ShopCatalog.All.Count, back.Count);
                for (int i = 0; i < back.Count; i++)
                    Assert.AreEqual(JsonUtility.ToJson(ShopDatabase.Entry.From(ShopCatalog.All[i])),
                        JsonUtility.ToJson(ShopDatabase.Entry.From(back[i])));
                Assert.IsTrue(back.Exists(d => d.ForClass == null), "an item for any class stays for any class");
                Assert.IsTrue(back.Exists(d => d.ForClass == SpecialistClass.ScoutDrone));
            }
            finally
            {
                ShopCatalog.Reload();
            }
        }

        /// <summary>The shipped databases carry everything the code does, so no entry silently falls back.</summary>
        [Test]
        public void ShippedDatabases_CoverTheBuiltInCatalogs()
        {
            var tech = Resources.Load<TechDatabase>(TechDatabase.ResourcePath);
            var shop = Resources.Load<ShopDatabase>(ShopDatabase.ResourcePath);
            var worlds = Resources.Load<WorldDatabase>(WorldDatabase.ResourcePath);
            Assert.IsNotNull(tech, "Resources/Data/TechDatabase.asset");
            Assert.IsNotNull(shop, "Resources/Data/ShopDatabase.asset");
            Assert.IsNotNull(worlds, "Resources/Data/WorldDatabase.asset");

            TechCatalog.Reload(useAsset: false);
            ShopCatalog.Reload(useAsset: false);
            try
            {
                foreach (TechDef def in TechCatalog.All)
                    Assert.IsTrue(tech.entries.Exists(e => e.id == def.Id), "tech " + def.Id);
                foreach (ShopItemDef def in ShopCatalog.All)
                    Assert.IsTrue(shop.entries.Exists(e => e.id == def.Id), "shop item " + def.Id);
            }
            finally
            {
                TechCatalog.Reload();
                ShopCatalog.Reload();
            }

            foreach (CelestialBodyId id in CelestialBodyCatalog.All)
            {
                CelestialBodyProfile builtIn = CelestialBodyCatalog.BuiltIn(id);
                CelestialBodyProfile shipped = worlds.FindSol(builtIn.Key);
                Assert.IsNotNull(shipped, builtIn.Key);
                Assert.AreEqual(id, shipped.Id);
                Assert.AreEqual(builtIn.Key, CelestialBodyCatalog.Get(id).Key, "the catalog serves the shipped world");
            }
        }

        [Test]
        public void SolWorld_FromTheAsset_IsTheBuiltInWorld_UntilSomeoneTunesIt()
        {
            var db = ScriptableObject.CreateInstance<WorldDatabase>();
            _created.Add(db);
            db.sol.Add(CelestialBodyCatalog.BuiltIn(CelestialBodyId.Mars));

            CelestialBodyProfile back = db.FindSol("sol.mars");

            Assert.AreEqual(JsonUtility.ToJson(CelestialBodyCatalog.BuiltIn(CelestialBodyId.Mars)), JsonUtility.ToJson(back));
            Assert.AreNotSame(db.sol[0], back, "play gets a copy, never the asset's own object");
            Assert.IsNull(db.FindSol("sol.europa"));
        }

        [Test]
        public void WorldDatabase_RegistersItsWorlds_Once()
        {
            var db = ScriptableObject.CreateInstance<WorldDatabase>();
            _created.Add(db);
            CelestialBodyProfile ares = CelestialBodyCatalog.Mars().Copy();
            ares.Key = "test.ares";
            db.worlds.Add(ares);
            db.worlds.Add(new CelestialBodyProfile { Key = "sol.fake" });

            Assert.AreEqual(1, db.RegisterAll(), "the Sol namespace is refused");
            Assert.AreEqual(0, db.RegisterAll(), "a second boot adds nothing");
            Assert.IsTrue(CelestialBodyCatalog.TryGetRegistered("test.ares", out _));
        }

        // ---- Architecture by trait --------------------------------------------------------

        /// <summary>The five authored worlds must land on their own sets, or Sol would change its look.</summary>
        [Test]
        public void PresetFor_TheSolTraits_IsEachWorldsOwnSet()
        {
            foreach (CelestialBodyId id in CelestialBodyCatalog.All)
            {
                EnvironmentTraits traits = CelestialBodyCatalog.Get(id).Traits;

                Assert.AreEqual(PlanetArchitecture.PresetOf(id), PlanetArchitecture.PresetFor(traits), id.ToString());
            }
        }

        [Test]
        public void PresetFor_GeneratedWorlds_FollowsWhatWouldBreakABuildingFirst()
        {
            // A warm, breathable asteroid still needs anchoring before it needs gardens.
            Assert.AreEqual(ArchPreset.AnchoredStation,
                PlanetArchitecture.PresetFor(new EnvironmentTraits(Atmosphere.Breathable, 0.02f, 20f, 0f)));
            // A heavy garden world.
            Assert.AreEqual(ArchPreset.Arcology,
                PlanetArchitecture.PresetFor(new EnvironmentTraits(Atmosphere.Breathable, 1.6f, 31f, 0.1f)));
            // Thin air does not help at -170 °C.
            Assert.AreEqual(ArchPreset.CryoBase,
                PlanetArchitecture.PresetFor(new EnvironmentTraits(Atmosphere.Thin, 0.3f, -170f, 0.2f)));
            // A hot, airless rock.
            Assert.AreEqual(ArchPreset.ShieldedOutpost,
                PlanetArchitecture.PresetFor(new EnvironmentTraits(Atmosphere.None, 0.4f, 180f, 0.9f)));
            // A dusty cold desert.
            Assert.AreEqual(ArchPreset.PrintedColony,
                PlanetArchitecture.PresetFor(new EnvironmentTraits(Atmosphere.Thin, 0.7f, -40f, 0.3f)));
        }

        [Test]
        public void Adapt_ByPreset_MatchesTheSolWorldItCameFrom()
        {
            List<ArchPart> byId = PlanetArchitecture.Adapt(CelestialBodyId.Europa, ArchArchetype.Hub, 8f, 8f, 3f, 11);
            List<ArchPart> byPreset = PlanetArchitecture.Adapt(ArchPreset.CryoBase, ArchArchetype.Hub, 8f, 8f, 3f, 11);

            Assert.Greater(byId.Count, 0);
            Assert.AreEqual(byId.Count, byPreset.Count);
            Assert.AreEqual(PlanetArchitecture.Style(CelestialBodyId.Europa).Trim, PlanetArchitecture.Style(ArchPreset.CryoBase).Trim);
        }

        // ---- Several overseers ------------------------------------------------------------

        [Test]
        public void Session_RoutesEachCommandToItsOwnOverseersColony()
        {
            var session = new OverseerSession();
            ColonySim player = NewSim(1);
            ColonySim rival = NewSim(2);
            Assert.IsTrue(session.Add(0, player));
            Assert.IsTrue(session.Add(1, rival));

            OverseerCommand rivalFlag = OverseerCommand.PostFlag(FlagType.Explore, new Vector3(40f, 0f, 40f), 500f);
            rivalFlag.overseer = 1;
            Assert.IsTrue(session.Submit(rivalFlag));
            session.RunSteps(10);

            Assert.AreEqual(0, player.Flags.Flags.Count);
            Assert.AreEqual(5000, player.Resources.Get(ResourceId.Metals), "the player's treasury is untouched");
            Assert.AreEqual(1, rival.Flags.Flags.Count);
            Assert.AreEqual(4500, rival.Resources.Get(ResourceId.Metals));
            Assert.AreEqual(10, player.Clock.TotalSteps);
            Assert.AreEqual(10, rival.Clock.TotalSteps);
        }

        [Test]
        public void Session_RefusesASecondColonyForOneOverseer_AndCommandsForStrangers()
        {
            var session = new OverseerSession();
            Assert.IsTrue(session.Add(0, NewSim()));
            Assert.IsFalse(session.Add(0, NewSim()));
            Assert.IsFalse(session.Add(1, null));

            OverseerCommand stray = OverseerCommand.CancelFlag(1);
            stray.overseer = 7;
            Assert.IsFalse(session.Submit(stray));
        }

        /// <summary>The whole game's fingerprint must not depend on the order players joined.</summary>
        [Test]
        public void Session_StateHash_IgnoresJoinOrder()
        {
            var first = new OverseerSession();
            first.Add(0, NewSim(1));
            first.Add(1, NewSim(2));
            var second = new OverseerSession();
            second.Add(1, NewSim(2));
            second.Add(0, NewSim(1));

            first.RunSteps(100);
            second.RunSteps(100);

            Assert.AreEqual(0, second.Sims[0].OverseerId);
            Assert.AreEqual(first.StateHash(), second.StateHash());
        }
    }
}
