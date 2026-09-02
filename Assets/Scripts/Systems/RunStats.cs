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
                    ? $"{(int)span.TotalHours}h {span.Minutes}m"
                    : $"{span.Minutes}m {span.Seconds}s";
            }
        }

        /// <summary>Rows for the end-of-run screen, in the order they should be read.</summary>
        public List<(string Label, string Value)> SummaryRows()
        {
            return new List<(string, string)>
            {
                ("Time served", Duration),
                ("Bounties posted", FlagsPosted.ToString()),
                ("Bounties completed", $"{FlagsCompleted}  ({CompletionRate * 100f:F0}%)"),
                ("Bounties refused", FlagsRefused.ToString()),
                ("Highest bounty", $"{HighestBounty} MET"),
                ("Paid out", $"{BountyPaid} MET"),
                ("Tithe returned", $"{TitheCollected} MET"),
                ("Modules built", ModulesBuilt.ToString()),
                ("Modules lost", ModulesLost.ToString()),
                ("Robots fabricated", RobotsFabricated.ToString()),
                ("Robots scrapped", $"{RobotsScrapped}  ({ScrapRate * 100f:F0}%)"),
                ("Fauna cleared", FaunaKilled.ToString()),
                ("Dens cleared", DensCleared.ToString()),
                ("Peak population", PeakPopulation.ToString()),
                ("Tech unlocked", TechUnlocked.ToString())
            };
        }

        /// <summary>
        /// A one-line read on how the player actually played, for the summary header. Uses the
        /// most distinctive extreme rather than a generic score.
        /// </summary>
        public string PlaystyleVerdict()
        {
            if (FlagsPosted == 0)
                return "You posted nothing. The colony built itself or it did not build at all.";
            if (ScrapRate > 0.45f)
                return "You spent robots like ammunition.";
            if (FlagsRefused > FlagsCompleted && FlagsCompleted > 0)
                return "Your workforce turned down more than it took. A tight purse, or a hard sell.";
            if (CompletionRate > 0.85f && HighestBounty > 140)
                return "You paid whatever it cost and the work always got done.";
            if (ModulesLost == 0 && FaunaKilled > 10)
                return "Nothing you built was ever lost.";
            if (CompletionRate > 0.7f)
                return "A steady administration. The bounties were priced about right.";
            return "A working colony, run on a thin margin.";
        }
    }
}
