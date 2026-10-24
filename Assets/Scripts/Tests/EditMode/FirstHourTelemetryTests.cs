using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// First-hour playtest instrumentation and the Earth-demo friction nudges. The JSONL field
    /// names checked here are what Tools/playtest/summarize.py reads.
    /// </summary>
    public class FirstHourTelemetryTests
    {
        private string _root;
        private bool _enabled;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "sm-firsthour-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            PlaytestTelemetry.DirectoryOverride = _root;
            _enabled = PlaytestTelemetry.Enabled;
        }

        [TearDown]
        public void TearDown()
        {
            PlaytestTelemetry.Enabled = _enabled;
            PlaytestTelemetry.DirectoryOverride = null;
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        // ---- Idle gaps ---------------------------------------------------------------------

        [Test]
        public void Idle_CrossesOnce_ThenReportsTheGapWhenInputReturns()
        {
            var idle = new PlaytestIdleTracker(30f);
            int crossed = 0;
            for (int i = 0; i < 45; i++)
            {
                if (idle.Tick(1f, counting: true, input: false) == PlaytestIdleTracker.Change.Crossed)
                    crossed++;
            }
            Assert.AreEqual(1, crossed, "one nudge per gap, not one per frame");
            Assert.IsTrue(idle.InGap);

            Assert.AreEqual(PlaytestIdleTracker.Change.Ended, idle.Tick(0.016f, true, input: true));
            Assert.AreEqual(45f, idle.LastGapSeconds, 0.01f);
            Assert.AreEqual(0f, idle.Quiet);
        }

        [Test]
        public void Idle_ShortQuietSpells_AreNotGaps()
        {
            var idle = new PlaytestIdleTracker(30f);
            for (int i = 0; i < 29; i++)
                Assert.AreEqual(PlaytestIdleTracker.Change.None, idle.Tick(1f, true, false));
            Assert.AreEqual(PlaytestIdleTracker.Change.None, idle.Tick(0.016f, true, true),
                "29 s then a key press is reading, not a stall");
            Assert.AreEqual(0f, idle.LastGapSeconds);
        }

        [Test]
        public void Idle_FreezesWhilePausedOrHeld()
        {
            var idle = new PlaytestIdleTracker(30f);
            idle.Tick(1f, true, false);
            for (int i = 0; i < 120; i++)
                Assert.AreEqual(PlaytestIdleTracker.Change.None, idle.Tick(1f, counting: false, input: false));
            Assert.AreEqual(1f, idle.Quiet, 0.001f, "held world or pause menu does not count as idle");
        }

        [Test]
        public void Idle_LongFrameCountsAsAtMostOneSecond()
        {
            var idle = new PlaytestIdleTracker(30f);
            // Alt-tab back after ten minutes: one giant frame, not a 600 s stall.
            Assert.AreEqual(PlaytestIdleTracker.Change.None, idle.Tick(600f, true, false));
            Assert.AreEqual(PlaytestIdleTracker.MaxStepSeconds, idle.Quiet, 0.001f);
        }

        [Test]
        public void Idle_PauseOrQuitClosesAnOpenGap_Once()
        {
            var idle = new PlaytestIdleTracker(30f);
            for (int i = 0; i < 40; i++) idle.Tick(1f, true, false);
            Assert.IsTrue(idle.TryClose(out float seconds));
            Assert.AreEqual(40f, seconds, 0.01f);
            Assert.IsFalse(idle.TryClose(out _), "the same gap is not reported twice");
            Assert.AreEqual(PlaytestIdleTracker.Change.None, idle.Tick(0.016f, true, true));
        }

        [Test]
        public void Idle_BadThreshold_FallsBackToThirtySeconds()
        {
            Assert.AreEqual(PlaytestIdleTracker.DefaultThresholdSeconds, new PlaytestIdleTracker(0f).Threshold);
            Assert.AreEqual(30f, PlaytestIdleTracker.DefaultThresholdSeconds);
        }

        // ---- Step clock --------------------------------------------------------------------

        [Test]
        public void StepClock_ReturnsSecondsOnTheStepBeingLeft()
        {
            var clock = new TutorialStepClock();
            Assert.AreEqual(0f, clock.Since(50.0), "nothing timed before Reset");
            clock.Reset(0, 10.0);
            Assert.AreEqual(32.5f, clock.Enter(1, 42.5), 0.001f);
            Assert.AreEqual(1, clock.Step);
            Assert.AreEqual(7.5f, clock.Since(50.0), 0.001f);
            Assert.AreEqual(0f, clock.Enter(2, 40.0), "a clock that went backwards (new scene) never goes negative");
        }

        // ---- Nudges and copy ---------------------------------------------------------------

        [Test]
        public void IdleHint_FollowsTheEarthBeat()
        {
            Assert.AreEqual(FirstHourHints.PlaceWorkshop, FirstHourHints.IdleHint(0, false, false));
            Assert.AreEqual(FirstHourHints.WorkshopRising, FirstHourHints.IdleHint(0, true, false),
                "a placed workshop needs reassurance, not 'press B' again");
            Assert.AreEqual(FirstHourHints.WorkshopHireShort, FirstHourHints.IdleHint(0, true, true));
            Assert.AreEqual(FirstHourHints.PostCheapBuild, FirstHourHints.IdleHint(1, true, false));
            Assert.AreEqual(FirstHourHints.RaiseBuildPrice, FirstHourHints.IdleHint(2, true, false));
            Assert.AreEqual(FirstHourHints.PostDefend, FirstHourHints.IdleHint(FirstHourTutorial.PestStep, true, false));
            Assert.IsNull(FirstHourHints.IdleHint(FirstHourTutorial.Goal, true, false));
            Assert.IsNull(FirstHourHints.IdleHint(-1, true, false));
        }

        [Test]
        public void IdleHints_NameTheKeysTheTutorialUses()
        {
            StringAssert.Contains("B, then 1", FirstHourHints.PlaceWorkshop);
            StringAssert.Contains("F3", FirstHourHints.PostCheapBuild);
            StringAssert.Contains("400", FirstHourHints.PostCheapBuild);
            StringAssert.Contains("Click the Build pole", FirstHourHints.RaiseBuildPrice);
            StringAssert.Contains("+", FirstHourHints.RaiseBuildPrice);
            StringAssert.Contains("F5", FirstHourHints.PostDefend);
        }

        [Test]
        public void TutorialBar_SaysWhichKeyPicksBuild_AndHowToSelectThePole()
        {
            string cheap = FirstHourTutorial.Beat(1, highBuildAlready: false);
            StringAssert.Contains("F3", cheap);
            StringAssert.Contains("700", cheap);
            StringAssert.Contains("Click that Build pole", FirstHourTutorial.Beat(2, false));
            StringAssert.Contains("F5", FirstHourTutorial.Beat(FirstHourTutorial.PestStep, false));
        }

        [Test]
        public void BountyKeys_WithNothingSelected_ExplainInsteadOfDoingNothing()
        {
            Assert.AreEqual(FirstHourHints.BountyNeedsPole, FirstHourHints.BountyKeyHint(false, false));
            Assert.IsNull(FirstHourHints.BountyKeyHint(true, false), "a selected pole takes the nudge");
            Assert.IsNull(FirstHourHints.BountyKeyHint(false, true), "flag tool open sets the next post");
            Assert.IsNull(FirstHourHints.BountyKeyHint(true, true));
        }

        [Test]
        public void TreasuryShortPost_SaysWhyAndHowToFixIt()
        {
            string line = FirstHourHints.PostRefused("Defend", 300);
            StringAssert.Contains("Not enough EU", line);
            StringAssert.Contains("300", line);
            StringAssert.Contains("Right-click", line);
            StringAssert.Contains("flag", FirstHourHints.PostRefused(null, 50));
        }

        // ---- JSONL contract ----------------------------------------------------------------

        [Test]
        public void FirstHourEvents_WriteTheFieldsTheSummarizerReads()
        {
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                PlaytestTelemetry.Enabled = true;
                PlaytestTelemetry.Begin("first-hour");
                PlaytestTelemetry.RecordTutorialStart("first_hour", 0, 4);
                PlaytestTelemetry.RecordTutorialStep("first_hour", 0, 1, 83.4f, 4);
                PlaytestTelemetry.RecordTutorialSkip("first_hour", 1, 4, 12f);
                PlaytestTelemetry.RecordTutorialRestart("first_hour", 4);
                PlaytestTelemetry.RecordIdleGap(41.5f, 2, "input");
                PlaytestTelemetry.RecordIdleHint(2, FirstHourHints.RaiseBuildPrice);
                PlaytestTelemetry.RecordPlacementInvalid("Engineer Workshop", "Blocked by a building");
                PlaytestTelemetry.RecordFlagBounty("Build", 700f, 750f, 0);
                PlaytestTelemetry.RecordFlagCancelled("Build", 900f, 900);
                PlaytestTelemetry.RecordFlagPostFailed("DefendArea", 300, "treasury");
                PlaytestTelemetry.RecordQuit("Playing", CelestialBodyId.Earth, 3, 4, 512.4);

                string[] lines = File.ReadAllLines(PlaytestTelemetry.SessionPath);
                string all = string.Join("\n", lines);
                StringAssert.Contains("\"e\":\"tutorial_start\",\"flow\":\"first_hour\",\"step\":\"0\",\"goal\":\"4\"", all);
                StringAssert.Contains("\"e\":\"tutorial_step\",\"step\":\"1\",\"from\":\"0\",\"seconds\":\"83.4\",\"goal\":\"4\",\"flow\":\"first_hour\"", all);
                StringAssert.Contains("\"e\":\"tutorial_skip\",\"flow\":\"first_hour\",\"step\":\"1\",\"goal\":\"4\",\"seconds\":\"12.0\"", all);
                StringAssert.Contains("\"e\":\"tutorial_restart\",\"flow\":\"first_hour\",\"from\":\"4\"", all);
                StringAssert.Contains("\"e\":\"idle_gap\",\"seconds\":\"41.5\",\"step\":\"2\",\"ended\":\"input\"", all);
                StringAssert.Contains("\"e\":\"idle_hint\",\"step\":\"2\"", all);
                StringAssert.Contains("\"e\":\"placement_invalid\",\"building\":\"Engineer Workshop\",\"reason\":\"Blocked by a building\"", all);
                StringAssert.Contains("\"e\":\"flag_bounty\",\"type\":\"Build\",\"from\":\"700\",\"to\":\"750\",\"interest\":\"0\"", all);
                StringAssert.Contains("\"e\":\"flag_cancelled\",\"type\":\"Build\",\"bounty\":\"900\",\"refund\":\"900\"", all);
                StringAssert.Contains("\"e\":\"flag_post_failed\",\"type\":\"DefendArea\",\"bounty\":\"300\",\"reason\":\"treasury\"", all);
                StringAssert.Contains("\"play_seconds\":\"512\",\"errors\":\"", all);
                foreach (string line in lines)
                {
                    StringAssert.StartsWith("{\"t\":", line);
                    StringAssert.EndsWith("}", line);
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        [Test]
        public void TutorialStep_KeepsTheOriginalStepField()
        {
            // Older notes and the 2026-09-17 zips read "step" as the step entered. Still true.
            PlaytestTelemetry.Enabled = true;
            PlaytestTelemetry.Begin("compat");
            PlaytestTelemetry.RecordTutorialStep("campaign", 2, 3, 5f, 6);
            PlaytestTelemetry.Flush();
            StringAssert.Contains("\"e\":\"tutorial_step\",\"step\":\"3\"", File.ReadAllText(PlaytestTelemetry.SessionPath));
        }
    }
}
