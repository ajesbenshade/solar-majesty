using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Per-run tallies.
    ///
    /// Feeds the end-of-run summary, achievement checks, and the playtest telemetry. Kept as plain
    /// counters with no Unity dependencies beyond Mathf so it can be asserted in tests.
    /// </summary>
    [Serializable]
    public sealed class RunStats
    {
        public float Seconds;

        public int FlagsPosted;
        public int FlagsCompleted;
        public int FlagsCancelled;
        public int FlagsRefused;

        public int MetalsEarned;
        public int MetalsSpent;
        public int BountyPaid;
        public int TitheCollected;

        public int ModulesBuilt;
        public int ModulesLost;

        public int RobotsFabricated;
        public int RobotsDowned;
        public int RobotsScrapped;
        public int RobotsRevived;

        public int FaunaKilled;
        public int DensCleared;

        public int ColonistsBorn;
        public int ColonistsLost;

        public int TechUnlocked;
        public int BodiesConquered;

        public int PeakPopulation;
        public int PeakModules;

        /// <summary>Highest bounty the player ever had to offer. A read on how stubborn the AI got.</summary>
        public int HighestBounty;

        public void Tick(float dt) => Seconds += dt;

        public void NotePopulation(int pop) => PeakPopulation = Mathf.Max(PeakPopulation, pop);

        public void NoteModules(int modules) => PeakModules = Mathf.Max(PeakModules, modules);

        public void NoteBounty(int bounty) => HighestBounty = Mathf.Max(HighestBounty, bounty);

        /// <summary>Share of posted flags that were actually taken and finished.</summary>
        public float CompletionRate =>
            FlagsPosted <= 0 ? 0f : Mathf.Clamp01(FlagsCompleted / (float)FlagsPosted);

        /// <summary>Share of robots lost outright. High values mean the player under-defended.</summary>
        public float ScrapRate =>
            RobotsFabricated <= 0 ? 0f : Mathf.Clamp01(RobotsScrapped / (float)RobotsFabricated);

        public string Duration
        {
            get
            {
                var span = TimeSpan.FromSeconds(Seconds);
                return span.TotalHours >= 1d
                    ? Loc.F("{0}h {1}m", (int)span.TotalHours, span.Minutes)
                    : Loc.F("{0}m {1}s", span.Minutes, span.Seconds);
            }
        }

        /// <summary>Rows for the end-of-run screen, in the order they should be read.</summary>
        public List<(string Label, string Value)> SummaryRows()
        {
            return new List<(string, string)>
            {
                (Loc.S("Time served"), Duration),
                (Loc.S("Bounties posted"), FlagsPosted.ToString()),
                (Loc.S("Bounties completed"), Loc.F("{0}  ({1:F0}%)", FlagsCompleted, CompletionRate * 100f)),
                (Loc.S("Bounties refused"), FlagsRefused.ToString()),
                (Loc.S("Highest bounty"), Loc.F("{0} EU", HighestBounty)),
                (Loc.S("Paid out"), Loc.F("{0} EU", BountyPaid)),
                (Loc.S("Till share"), Loc.F("{0} EU", TitheCollected)),
                (Loc.S("Modules built"), ModulesBuilt.ToString()),
                (Loc.S("Modules lost"), ModulesLost.ToString()),
                (Loc.S("Robots fabricated"), RobotsFabricated.ToString()),
                (Loc.S("Robots scrapped"), Loc.F("{0}  ({1:F0}%)", RobotsScrapped, ScrapRate * 100f)),
                (Loc.S("Fauna cleared"), FaunaKilled.ToString()),
                (Loc.S("Dens cleared"), DensCleared.ToString()),
                (Loc.S("Peak population"), PeakPopulation.ToString()),
                (Loc.S("Tech unlocked"), TechUnlocked.ToString())
            };
        }

        /// <summary>
        /// A one-line read on how the player actually played, for the summary header. Uses the
        /// most distinctive extreme rather than a generic score.
        /// </summary>
        public string PlaystyleVerdict()
        {
            if (FlagsPosted == 0)
                return Loc.S("You posted nothing. The colony built itself or it did not build at all.");
            if (ScrapRate > 0.45f)
                return Loc.S("You spent robots like ammunition.");
            if (FlagsRefused > FlagsCompleted && FlagsCompleted > 0)
                return Loc.S("Your workforce turned down more than it took. A tight purse, or a hard sell.");
            if (CompletionRate > 0.85f && HighestBounty > 1400)
                return Loc.S("You paid whatever it cost and the work always got done.");
            if (ModulesLost == 0 && FaunaKilled > 10)
                return Loc.S("Nothing you built was ever lost.");
            if (CompletionRate > 0.7f)
                return Loc.S("A steady administration. The bounties were priced about right.");
            return Loc.S("A working colony, run on a thin margin.");
        }
    }
}
