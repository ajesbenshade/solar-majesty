namespace SolarMajesty
{
    /// <summary>
    /// Earth demo beats. The Engineer has to be alive before a cheap Build
    /// flag counts, and a refused Build flag opens the price lesson.
    /// An already attractive Build flag completes that lesson without forcing a repost.
    /// Beats name keys as <c>{key:Action}</c>; the HUD expands them to the player's bindings.
    /// </summary>
    public static class FirstHourTutorial
    {
        public const int Goal = 4;
        public const int PestStep = 3;

        public const string WorkshopBeat =
            "1/4  WORKSHOP — press {key:ToolBuild}, then 1. Place the Engineer workshop anywhere near the Commons. Wait until the robot is standing.";
        public const string CheapBuildBeat =
            "2/4  Press {key:ToolFlag}, choose Build, leave the bounty at 700. If the Engineer ignores it, raise the bounty.";
        public const string CancelHighBeat =
            "2/4  That Build flag has attracted an Engineer. The colony is ready for the next lesson.";
        public const string RaisePriceBeat =
            "3/4  Select that flag and press {key:BountyUp} until the pole says tempted.";
        public const string PestBeat =
            "4/4  A soil creeper is on the yard. Press {key:ToolFlag}, {key:FlagDefend} Defend, and post it on the bug. If the pole says ignored, build a Defense workshop (key 2).";

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
            // The consts stay English (tests and key-token checks read them); the beat shown is translated.
            if (step <= 0) return Loc.S(WorkshopBeat);
            if (step == 1) return highBuildAlready ? Loc.S(CancelHighBeat) : Loc.S(CheapBuildBeat);
            if (step == 2) return Loc.S(RaisePriceBeat);
            return Loc.S(PestBeat);
        }
    }
}
