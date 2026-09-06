using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class SimClockTests
    {
        [Test]
        public void Advance_EmitsNoStepBeforeAFullStepHasElapsed()
        {
            var clock = new SimClock(0.05f);

            Assert.AreEqual(0, clock.Advance(0.02f));
            Assert.AreEqual(0, clock.Advance(0.02f));
            Assert.AreEqual(1, clock.Advance(0.02f), "0.06s of accumulation is one 0.05s step");
        }

        [Test]
        public void Advance_EmitsWholeStepsAndKeepsTheRemainder()
        {
            var clock = new SimClock(0.05f);

            Assert.AreEqual(3, clock.Advance(0.16f));
            Assert.AreEqual(0.01f / 0.05f, clock.Alpha, 1e-4f);
        }

        /// <summary>
        /// The point of the fixed step: the same wall-clock time produces the same number of
        /// simulation steps whether the game ran at 30 fps or 240 fps.
        /// </summary>
        [Test]
        public void TotalSteps_MatchAcrossFrameRates()
        {
            var slow = new SimClock(0.05f) { MaxStepsPerFrame = 1000 };
            var fast = new SimClock(0.05f) { MaxStepsPerFrame = 1000 };

            for (int i = 0; i < 30; i++) slow.Advance(1f / 30f);
            for (int i = 0; i < 240; i++) fast.Advance(1f / 240f);

            Assert.AreEqual(20, slow.TotalSteps, "one second at 20 Hz is 20 steps");
            Assert.AreEqual(slow.TotalSteps, fast.TotalSteps);
        }

        [Test]
        public void Advance_CapsCatchUpAndFlagsDroppedTime()
        {
            var clock = new SimClock(0.05f, maxStepsPerFrame: 4);

            int steps = clock.Advance(10f);

            Assert.AreEqual(4, steps, "a long stall must not queue a backlog it cannot clear");
            Assert.IsTrue(clock.DroppedTime);
            Assert.AreEqual(0f, clock.Alpha, 1e-4f, "the backlog is discarded, not carried");
        }

        [Test]
        public void Advance_ZeroOrNegativeDelta_IsInert()
        {
            var clock = new SimClock(0.05f);
            clock.Advance(0.04f);

            Assert.AreEqual(0, clock.Advance(0f));
            Assert.AreEqual(0, clock.Advance(-5f), "a negative delta must not rewind the clock");
            Assert.AreEqual(0, clock.TotalSteps);
        }

        [Test]
        public void Reset_ClearsAccumulationAndCount()
        {
            var clock = new SimClock(0.05f);
            clock.Advance(1f);

            clock.Reset();

            Assert.AreEqual(0, clock.TotalSteps);
            Assert.AreEqual(0f, clock.Alpha, 1e-4f);
        }

        [Test]
        public void ElapsedSeconds_TracksSteps()
        {
            var clock = new SimClock(0.05f) { MaxStepsPerFrame = 1000 };

            clock.Advance(2f);

            Assert.AreEqual(2.0, clock.ElapsedSeconds, 1e-6);
        }
    }

    public class SimSpeedTests
    {
        [SetUp]
        public void SetUp() => SimSpeed.ResetToNormal();

        [TearDown]
        public void TearDown() => SimSpeed.ResetToNormal();

        [Test]
        public void StartsAtNormalSpeed()
        {
            Assert.AreEqual(1f, SimSpeed.Multiplier);
            Assert.IsFalse(SimSpeed.IsPaused);
        }

        [Test]
        public void Faster_ClampsAtTheTopSpeed()
        {
            for (int i = 0; i < 10; i++) SimSpeed.Faster();

            Assert.AreEqual(3f, SimSpeed.Multiplier);
        }

        [Test]
        public void Slower_BottomsOutAtPause()
        {
            for (int i = 0; i < 10; i++) SimSpeed.Slower();

            Assert.IsTrue(SimSpeed.IsPaused);
            Assert.AreEqual(0f, SimSpeed.Multiplier);
        }

        /// <summary>Unpausing must return to the speed the player chose, not snap back to 1x.</summary>
        [Test]
        public void TogglePause_RestoresThePreviousSpeed()
        {
            SimSpeed.Set(3);

            SimSpeed.TogglePause();
            Assert.IsTrue(SimSpeed.IsPaused);

            SimSpeed.TogglePause();
            Assert.AreEqual(3f, SimSpeed.Multiplier);
        }

        [Test]
        public void Label_ReadsAsHoldWhenPaused()
        {
            SimSpeed.Set(0);
            Assert.AreEqual("HOLD", SimSpeed.Label);

            SimSpeed.Set(2);
            Assert.AreEqual("2x", SimSpeed.Label);
        }

        [Test]
        public void Set_ClampsOutOfRangeIndices()
        {
            SimSpeed.Set(99);
            Assert.AreEqual(SimSpeed.Multipliers.Length - 1, SimSpeed.Index);

            SimSpeed.Set(-5);
            Assert.AreEqual(0, SimSpeed.Index);
        }
    }

    public class SaveGameTests
    {
        /// <summary>
        /// The legacy continue slot dropped flags, fauna, and specialist health. The whole reason
        /// this format exists is that those survive a round trip.
        /// </summary>
        [Test]
        public void RoundTrip_PreservesWhatTheLegacySlotDropped()
        {
            var save = new SaveGame
            {
                body = (int)CelestialBodyId.Mars,
                seed = 20011,
                playSeconds = 1234.5
            };
            save.stockpile.metals = 170;
            save.flags.Add(new SaveFlag
            {
                flagType = (int)FlagType.ClearThreat,
                px = 12f, py = 0f, pz = -4f,
                bounty = 95f,
                escrowMetals = 95,
                postedWork = 40f,
                workDone = 12.5f,
                claimCount = 2
            });
            save.agents.Add(new SaveAgent
            {
                specialistClass = (int)SpecialistClass.EngineerBot,
                health = 0.42f,
                fatigue = 0.66f,
                credits = 210,
                downed = true,
                downedTimer = 7.5f,
                claimedFlagIndex = 0
            });
            save.fauna.Add(new SaveFauna { kind = (int)FaunaKind.Stalker, health = 0.3f });

            var copy = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));

            Assert.AreEqual((int)CelestialBodyId.Mars, copy.body);
            Assert.AreEqual(170, copy.stockpile.metals);
            Assert.AreEqual(1234.5, copy.playSeconds, 1e-3);

            Assert.AreEqual(1, copy.flags.Count);
            Assert.AreEqual((int)FlagType.ClearThreat, copy.flags[0].flagType);
            Assert.AreEqual(95, copy.flags[0].escrowMetals);
            Assert.AreEqual(12.5f, copy.flags[0].workDone, 1e-4f);
            Assert.AreEqual(2, copy.flags[0].claimCount);

            Assert.AreEqual(1, copy.agents.Count);
            Assert.AreEqual(0.42f, copy.agents[0].health, 1e-4f);
            Assert.AreEqual(210, copy.agents[0].credits);
            Assert.IsTrue(copy.agents[0].downed);
            Assert.AreEqual(0, copy.agents[0].claimedFlagIndex);

            Assert.AreEqual(1, copy.fauna.Count);
            Assert.AreEqual(0.3f, copy.fauna[0].health, 1e-4f);
        }

        [Test]
        public void NewSave_CarriesTheCurrentVersion()
        {
            Assert.AreEqual(SaveGame.CurrentVersion, new SaveGame().version);
        }

        [Test]
        public void Describe_MentionsBodyAndScale()
        {
            var save = new SaveGame { body = (int)CelestialBodyId.Europa };
            save.buildings.Add(new SaveBuilding());
            save.agents.Add(new SaveAgent());

            string text = save.Describe();

            StringAssert.Contains("Europa", text);
            StringAssert.Contains("1 modules", text);
            StringAssert.Contains("1 robots", text);
        }

        [Test]
        public void EmptySave_HasNoNullCollections()
        {
            var save = new SaveGame();

            Assert.IsNotNull(save.buildings);
            Assert.IsNotNull(save.flags);
            Assert.IsNotNull(save.agents);
            Assert.IsNotNull(save.fauna);
            Assert.IsNotNull(save.research.unlocked);
        }

        [Test]
        public void SlotPath_IsDistinctPerSlotAndClamped()
        {
            Assert.AreNotEqual(SaveSystem.SlotPath(0), SaveSystem.SlotPath(1));
            Assert.AreEqual(SaveSystem.SlotPath(SaveSystem.SlotCount - 1), SaveSystem.SlotPath(999));
            Assert.AreEqual(SaveSystem.SlotPath(0), SaveSystem.SlotPath(-3));
        }

        [Test]
        public void NewAgent_DefaultsClaimedFlagToNone()
        {
            Assert.AreEqual(-1, new SaveAgent().claimedFlagIndex);
        }
    }

    public class FlagManagerTests
    {
        private static FlagData MakeFlag(FlagType type, float work)
        {
            var data = ScriptableObject.CreateInstance<FlagData>();
            data.flagType = type;
            data.workRequired = work;
            data.minBounty = 10;
            data.maxBounty = 500;
            data.defaultBounty = 50;
            return data;
        }

        [Test]
        public void RestoreProgress_KeepsPostedWorkAndRemainingLabor()
        {
            var flags = new FlagManager();
            var handle = flags.Post(MakeFlag(FlagType.ClearThreat, 40f), Vector3.zero, 95f);

            flags.RestoreProgress(handle, 40f, 27.5f);

            Assert.AreEqual(40f, handle.PostedWork, 1e-4f);
            Assert.AreEqual(27.5f, flags.GetWorkRemaining(handle), 1e-4f);
        }

        [Test]
        public void RestoreProgress_UsesExistingPostedWorkWhenSaveOmitsIt()
        {
            var flags = new FlagManager();
            var handle = flags.Post(MakeFlag(FlagType.Extract, 12f), Vector3.right, 40f);

            flags.RestoreProgress(handle, 0f, 4f);

            Assert.AreEqual(12f, handle.PostedWork, 1e-4f);
            Assert.AreEqual(4f, flags.GetWorkRemaining(handle), 1e-4f);
        }

        [Test]
        public void ApplyWork_AfterRestore_CompletesAtRemainingLabor()
        {
            var flags = new FlagManager();
            var handle = flags.Post(MakeFlag(FlagType.Explore, 10f), Vector3.forward, 30f);
            flags.RestoreProgress(handle, 10f, 2f);

            Assert.IsFalse(flags.ApplyWork(handle, 1.5f));
            Assert.IsTrue(flags.ApplyWork(handle, 1f));
            Assert.AreEqual(0, flags.Flags.Count);
        }
    }
}
