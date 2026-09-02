using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// A robot's service record. Persists across downs and re-fabs so the same unit accumulates a
    /// history the player recognises.
    /// </summary>
    [Serializable]
    public sealed class SpecialistRecord
    {
        public string Name;
        public string Designation;
        public SpecialistClass Class;

        public int FlagsCompleted;
        public int Kills;
        public int TimesDowned;
        public int TimesScrapped;
        public int CreditsEarned;
        public float ServiceSeconds;

        /// <summary>Earned title, upgraded as the record grows.</summary>
        public string Rank => RankFor(FlagsCompleted, Kills);

        public string FullName => $"{Name} \"{Designation}\"";

        /// <summary>One-line service summary for the inspect panel and the death notice.</summary>
        public string Citation
        {
            get
            {
                if (FlagsCompleted == 0 && Kills == 0)
                    return "no record yet";

                var parts = new List<string>(4);
                if (FlagsCompleted > 0) parts.Add($"{FlagsCompleted} contract{(FlagsCompleted == 1 ? "" : "s")}");
                if (Kills > 0) parts.Add($"{Kills} kill{(Kills == 1 ? "" : "s")}");
                if (TimesDowned > 0) parts.Add($"down {TimesDowned}x");
                return string.Join(" · ", parts);
            }
        }

        private static string RankFor(int flags, int kills)
        {
            int weight = flags + kills * 2;
            if (weight >= 40) return "Veteran";
            if (weight >= 22) return "Seasoned";
            if (weight >= 10) return "Proven";
            if (weight >= 3) return "Serving";
            return "Fresh";
        }
    }

    /// <summary>
    /// Names and remembers individual robots.
    ///
    /// Majesty works because you learn who your heroes are. Until now every unit was
    /// "Engineer Bot", so a death cost the player a stat line rather than someone they knew.
    /// Names are drawn without repeats inside a run and the record survives re-fabrication, which
    /// is what turns the scrap rule from an accounting event into a loss.
    /// </summary>
    public static class SpecialistIdentity
    {
        /// <summary>Serial-style names: these are machines, not people.</summary>
        private static readonly string[] Names =
        {
            "Ash", "Bolt", "Cinder", "Dram", "Ember", "Flint", "Gauge", "Halcyon",
            "Ingot", "Jute", "Kelvin", "Lumen", "Mesa", "Nadir", "Onyx", "Pike",
            "Quill", "Rivet", "Slate", "Tessel", "Umber", "Vane", "Weld", "Xenon",
            "Yarrow", "Zinc", "Anvil", "Basalt", "Coil", "Delve", "Etch", "Ferrous"
        };

        /// <summary>Designations hint at the class's job without naming it outright.</summary>
        private static readonly string[] Designations =
        {
            "Longhaul", "Deadlift", "Patchwork", "Crosswind", "Nightshift", "Overtime",
            "Ledger", "Sandpiper", "Backorder", "Tallyman", "Understudy", "Fairweather",
            "Groundloop", "Hardline", "Sunward", "Deepcut"
        };

        private static readonly List<SpecialistRecord> Roster = new List<SpecialistRecord>(24);
        private static readonly HashSet<string> UsedNames = new HashSet<string>();
        private static System.Random _rng = new System.Random(7717);

        public static IReadOnlyList<SpecialistRecord> All => Roster;

        /// <summary>New run: forget everyone.</summary>
        public static void Reset(int seed = 7717)
        {
            Roster.Clear();
            UsedNames.Clear();
            _rng = new System.Random(seed);
        }

        public static SpecialistRecord Create(SpecialistClass cls)
        {
            var record = new SpecialistRecord
            {
                Name = NextName(),
                Designation = Designations[_rng.Next(Designations.Length)],
                Class = cls
            };
            Roster.Add(record);
            return record;
        }

        /// <summary>
        /// Names are unique while any remain, then fall back to a numbered suffix so a long
        /// Endless run cannot run out or start silently reusing names.
        /// </summary>
        private static string NextName()
        {
            if (UsedNames.Count < Names.Length)
            {
                for (int attempt = 0; attempt < 64; attempt++)
                {
                    string candidate = Names[_rng.Next(Names.Length)];
                    if (UsedNames.Add(candidate))
                        return candidate;
                }
            }

            string numbered = $"{Names[_rng.Next(Names.Length)]}-{UsedNames.Count + 1}";
            UsedNames.Add(numbered);
            return numbered;
        }

        /// <summary>The unit with the strongest record; the one a player is most likely to miss.</summary>
        public static SpecialistRecord MostDecorated()
        {
            SpecialistRecord best = null;
            int bestWeight = -1;
            for (int i = 0; i < Roster.Count; i++)
            {
                var r = Roster[i];
                int weight = r.FlagsCompleted + r.Kills * 2;
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    best = r;
                }
            }
            return best;
        }

        public static int TotalFlagsCompleted()
        {
            int n = 0;
            for (int i = 0; i < Roster.Count; i++) n += Roster[i].FlagsCompleted;
            return n;
        }
    }
}
