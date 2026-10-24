namespace SolarMajesty
{
    /// <summary>
    /// Earth demo beats. The Engineer has to be alive before a cheap Build
    /// flag counts, and a refused Build flag opens the price lesson.
    /// An already attractive Build flag completes that lesson without forcing a repost.
    /// </summary>
    public static class FirstHourTutorial
    {
        public const int Goal = 4;
        public const int PestStep = 3;

        // Key names come from the live bindings: {GameAction} tokens are filled by KeyHints.
        public static string WorkshopBeat => KeyHints.Fill(WorkshopBeatText);
        public static string CheapBuildBeat => KeyHints.Fill(CheapBuildBeatText);
        public static string RaisePriceBeat => KeyHints.Fill(RaisePriceBeatText);
        public static string PestBeat => KeyHints.Fill(PestBeatText);

        private const string WorkshopBeatText =
            "1/4  WORKSHOP — press {ToolBuild}, then {BuildSlot1}. Place the Engineer workshop anywhere near the Commons. Wait until the robot is standing.";
        private const string CheapBuildBeatText =
            "2/4  Press {ToolFlag}, then {FlagBuild} (Build), and click open ground. Leave the bounty at 400. If the Engineer ignores it, raise the bounty.";
        public const string CancelHighBeat =
            "2/4  That Build flag has attracted an Engineer. The colony is ready for the next lesson.";
        private const string RaisePriceBeatText =
            "3/4  Click that Build pole to select it, then press {BountyUp} until the pole says tempted.";
        private const string PestBeatText =
            "4/4  A soil creeper is on the yard. Press {ToolFlag}, {FlagDefend} Defend, and post it on the bug. If the pole says ignored, build a Defense workshop (key {BuildSlot2}).";

        public static int Advance(
            int step,
            bool engineerAlive,
            bool refusedBuild,
            bool temptedBuild,
            bool defendPosted)
        {
            if (step < 0) step = 0;
            if (step >= Goal) return Goal;

            if (step == 0)
                return engineerAlive ? 1 : 0;

            if (step == 1)
                return temptedBuild ? PestStep : (refusedBuild ? 2 : 1);

            if (step == 2)
            {
                if (!refusedBuild && !temptedBuild) return 1;
                if (temptedBuild) return PestStep;
                return 2;
            }

            if (step == PestStep)
                return defendPosted ? Goal : PestStep;

            return step;
        }

        public static string Beat(int step, bool highBuildAlready)
        {
            if (step <= 0) return WorkshopBeat;
            if (step == 1) return highBuildAlready ? CancelHighBeat : CheapBuildBeat;
            if (step == 2) return RaisePriceBeat;
            return PestBeat;
        }
    }
}
