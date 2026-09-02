using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class RunStatsTests
    {
        [Test]
        public void CompletionRate_IsZeroWithNoPostings()
        {
            Assert.AreEqual(0f, new RunStats().CompletionRate);
        }

        [Test]
        public void CompletionRate_IsCompletedOverPosted()
        {
            var s = new RunStats { FlagsPosted = 10, FlagsCompleted = 7 };

            Assert.AreEqual(0.7f, s.CompletionRate, 1e-4f);
        }

        [Test]
        public void CompletionRate_ClampsWhenCompletedExceedsPosted()
        {
            var s = new RunStats { FlagsPosted = 2, FlagsCompleted = 5 };

            Assert.AreEqual(1f, s.CompletionRate);
        }

        [Test]
        public void Peaks_OnlyEverRise()
        {
            var s = new RunStats();

            s.NotePopulation(12);
            s.NotePopulation(4);
            s.NoteModules(9);
            s.NoteModules(2);
            s.NoteBounty(120);
            s.NoteBounty(40);

            Assert.AreEqual(12, s.PeakPopulation);
            Assert.AreEqual(9, s.PeakModules);
            Assert.AreEqual(120, s.HighestBounty);
        }

        [Test]
        public void Duration_SwitchesToHoursPastSixtyMinutes()
        {
            Assert.AreEqual("2m 5s", new RunStats { Seconds = 125f }.Duration);
            StringAssert.Contains("h", new RunStats { Seconds = 4000f }.Duration);
        }

        [Test]
        public void SummaryRows_CoverEveryHeadlineStat()
        {
            var rows = new RunStats().SummaryRows();

            Assert.Greater(rows.Count, 10);
            Assert.AreEqual("Time served", rows[0].Label);
        }

        [Test]
        public void PlaystyleVerdict_CallsOutHeavyLosses()
        {
            var s = new RunStats { FlagsPosted = 10, RobotsFabricated = 10, RobotsScrapped = 6 };

            StringAssert.Contains("ammunition", s.PlaystyleVerdict());
        }

        [Test]
        public void PlaystyleVerdict_CallsOutARefusedWorkforce()
        {
            var s = new RunStats { FlagsPosted = 10, FlagsCompleted = 2, FlagsRefused = 8 };

            StringAssert.Contains("turned down", s.PlaystyleVerdict());
        }
    }

    public class SpecialistIdentityTests
    {
        [SetUp]
        public void SetUp() => SpecialistIdentity.Reset();

        [TearDown]
        public void TearDown() => SpecialistIdentity.Reset();

        [Test]
        public void Create_GivesEachRobotADistinctName()
        {
            var names = new System.Collections.Generic.HashSet<string>();

            for (int i = 0; i < 20; i++)
            {
                var record = SpecialistIdentity.Create(SpecialistClass.EngineerBot);
                Assert.IsTrue(names.Add(record.Name), $"duplicate name: {record.Name}");
            }
        }

        /// <summary>A long Endless run must not run out of names or start silently repeating.</summary>
        [Test]
        public void Create_KeepsGoingPastTheNamePool()
        {
            var names = new System.Collections.Generic.HashSet<string>();

            for (int i = 0; i < 90; i++)
            {
                var record = SpecialistIdentity.Create(SpecialistClass.ScoutDrone);
                Assert.IsFalse(string.IsNullOrEmpty(record.Name));
                Assert.IsTrue(names.Add(record.Name), $"duplicate name at {i}: {record.Name}");
            }
        }

        [Test]
        public void Rank_ClimbsWithService()
        {
            var record = SpecialistIdentity.Create(SpecialistClass.DefenseMech);
            Assert.AreEqual("Fresh", record.Rank);

            record.FlagsCompleted = 3;
            Assert.AreEqual("Serving", record.Rank);

            record.FlagsCompleted = 10;
            Assert.AreEqual("Proven", record.Rank);

            record.FlagsCompleted = 22;
            Assert.AreEqual("Seasoned", record.Rank);

            record.FlagsCompleted = 30;
            record.Kills = 8;
            Assert.AreEqual("Veteran", record.Rank);
        }

        [Test]
        public void Citation_ReadsEmptyForANewUnit()
        {
            var record = SpecialistIdentity.Create(SpecialistClass.Medic);

            Assert.AreEqual("no record yet", record.Citation);
        }

        [Test]
        public void Citation_SingularisesCorrectly()
        {
            var record = SpecialistIdentity.Create(SpecialistClass.Medic);
            record.FlagsCompleted = 1;
            record.Kills = 1;

            StringAssert.Contains("1 contract ", record.Citation + " ");
            StringAssert.Contains("1 kill", record.Citation);
            Assert.IsFalse(record.Citation.Contains("kills"));
        }

        [Test]
        public void MostDecorated_PrefersKillsOverContracts()
        {
            var worker = SpecialistIdentity.Create(SpecialistClass.EngineerBot);
            worker.FlagsCompleted = 10;

            var fighter = SpecialistIdentity.Create(SpecialistClass.DefenseMech);
            fighter.Kills = 8;   // weight 16 beats 10

            Assert.AreSame(fighter, SpecialistIdentity.MostDecorated());
        }

        [Test]
        public void Reset_ClearsTheRoster()
        {
            SpecialistIdentity.Create(SpecialistClass.EngineerBot);
            SpecialistIdentity.Reset();

            Assert.AreEqual(0, SpecialistIdentity.All.Count);
            Assert.IsNull(SpecialistIdentity.MostDecorated());
        }
    }

    public class AchievementTests
    {
        [SetUp]
        public void SetUp()
        {
            Achievements.ResetAll();
            Achievements.BeginRun();
            SpecialistIdentity.Reset();
        }

        [TearDown]
        public void TearDown() => Achievements.ResetAll();

        [Test]
        public void Evaluate_UnlocksTheFirstContract()
        {
            Achievements.Evaluate(new RunStats { FlagsCompleted = 1 });

            Assert.IsTrue(Achievements.IsUnlocked(AchievementId.FirstContract));
        }

        [Test]
        public void Evaluate_DoesNotUnlockUnearnedAchievements()
        {
            Achievements.Evaluate(new RunStats { FlagsCompleted = 1 });

            Assert.IsFalse(Achievements.IsUnlocked(AchievementId.SolarConquest));
        }

        [Test]
        public void Evaluate_RaisesEarnedOncePerAchievement()
        {
            int raised = 0;
            void Handler(AchievementDef _) => raised++;

            Achievements.Earned += Handler;
            try
            {
                var stats = new RunStats { FlagsCompleted = 1 };
                Achievements.Evaluate(stats);
                Achievements.Evaluate(stats);
                Achievements.Evaluate(stats);
            }
            finally
            {
                Achievements.Earned -= Handler;
            }

            Assert.AreEqual(1, raised, "an achievement must not re-fire every evaluation");
        }

        /// <summary>Skinflint rewards a cheap run, so an expensive one must not qualify.</summary>
        [Test]
        public void Skinflint_RequiresACheapRun()
        {
            Achievements.Evaluate(new RunStats { BodiesConquered = 1, HighestBounty = 300 });
            Assert.IsFalse(Achievements.IsUnlocked(AchievementId.Skinflint));

            Achievements.Evaluate(new RunStats { BodiesConquered = 1, HighestBounty = 80 });
            Assert.IsTrue(Achievements.IsUnlocked(AchievementId.Skinflint));
        }

        [Test]
        public void Veteran_ReadsTheSpecialistRoster()
        {
            Achievements.Evaluate(new RunStats());
            Assert.IsFalse(Achievements.IsUnlocked(AchievementId.Veteran));

            var record = SpecialistIdentity.Create(SpecialistClass.EngineerBot);
            record.FlagsCompleted = 40;

            Achievements.Evaluate(new RunStats());
            Assert.IsTrue(Achievements.IsUnlocked(AchievementId.Veteran));
        }

        [Test]
        public void UnlockedThisRun_TracksNewUnlocksOnly()
        {
            Achievements.Evaluate(new RunStats { FlagsCompleted = 1 });
            Assert.AreEqual(1, Achievements.UnlockedThisRun.Count);

            Achievements.BeginRun();
            Achievements.Evaluate(new RunStats { FlagsCompleted = 1 });
            Assert.AreEqual(0, Achievements.UnlockedThisRun.Count, "already-earned achievements are not re-listed");
        }

        [Test]
        public void EveryDefinitionHasTitleDescriptionAndTest()
        {
            var defs = Achievements.Definitions;
            Assert.Greater(defs.Count, 5);

            for (int i = 0; i < defs.Count; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(defs[i].Title), $"{defs[i].Id} has no title");
                Assert.IsFalse(string.IsNullOrEmpty(defs[i].Description), $"{defs[i].Id} has no description");
                Assert.IsNotNull(defs[i].Test, $"{defs[i].Id} has no test");
                Assert.IsNotNull(Achievements.Get(defs[i].Id));
            }
        }

        [Test]
        public void Evaluate_NullStats_IsSafe()
        {
            Assert.DoesNotThrow(() => Achievements.Evaluate(null));
        }
    }
}
