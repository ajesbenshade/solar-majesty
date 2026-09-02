using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    public enum AchievementId
    {
        FirstContract = 0,
        HardBargain = 1,
        FullCrew = 2,
        CleanSheet = 3,
        DenBreaker = 4,
        SelfSufficient = 5,
        Skinflint = 6,
        Rebuilder = 7,
        Homesteader = 8,
        SolarConquest = 9,
        Veteran = 10,
        Ironman = 11
    }

    public sealed class AchievementDef
    {
        public AchievementId Id;
        public string Title;
        public string Description;
        /// <summary>Hidden until earned, for the ones that would spoil a surprise.</summary>
        public bool Secret;
        public Func<RunStats, bool> Test;
    }

    /// <summary>
    /// Achievements evaluated against <see cref="RunStats"/>.
    ///
    /// Deliberately behavioural rather than grind-based: each one names a way of playing the
    /// overseer role, so the list doubles as a hint sheet for strategies a player might not have
    /// tried. Unlocks persist across runs.
    /// </summary>
    public static class Achievements
    {
        private const string PrefsKey = "SM_Achievements";

        private static readonly List<AchievementDef> Defs = new List<AchievementDef>
        {
            new AchievementDef
            {
                Id = AchievementId.FirstContract,
                Title = "Terms Accepted",
                Description = "Have a robot complete its first bounty.",
                Test = s => s.FlagsCompleted >= 1
            },
            new AchievementDef
            {
                Id = AchievementId.HardBargain,
                Title = "Hard Bargain",
                Description = "Post a bounty of 250 MET or more.",
                Test = s => s.HighestBounty >= 250
            },
            new AchievementDef
            {
                Id = AchievementId.Skinflint,
                Title = "Skinflint",
                Description = "Clear a body having never posted a bounty above 90 MET.",
                Test = s => s.BodiesConquered >= 1 && s.HighestBounty > 0 && s.HighestBounty <= 90
            },
            new AchievementDef
            {
                Id = AchievementId.FullCrew,
                Title = "Full Crew",
                Description = "Have eight robots fabricated in one run.",
                Test = s => s.RobotsFabricated >= 8
            },
            new AchievementDef
            {
                Id = AchievementId.CleanSheet,
                Title = "Clean Sheet",
                Description = "Clear a body without losing a single module.",
                Test = s => s.BodiesConquered >= 1 && s.ModulesLost == 0
            },
            new AchievementDef
            {
                Id = AchievementId.DenBreaker,
                Title = "Den Breaker",
                Description = "Clear ten dens in one run.",
                Test = s => s.DensCleared >= 10
            },
            new AchievementDef
            {
                Id = AchievementId.SelfSufficient,
                Title = "Self-Sufficient",
                Description = "Return 200 MET in payroll tithe.",
                Test = s => s.TitheCollected >= 200
            },
            new AchievementDef
            {
                Id = AchievementId.Rebuilder,
                Title = "Rebuilder",
                Description = "Re-fabricate five scrapped robots and still clear the body.",
                Test = s => s.RobotsScrapped >= 5 && s.BodiesConquered >= 1
            },
            new AchievementDef
            {
                Id = AchievementId.Homesteader,
                Title = "Homesteader",
                Description = "Reach a population of 20.",
                Test = s => s.PeakPopulation >= 20
            },
            new AchievementDef
            {
                Id = AchievementId.Veteran,
                Title = "Long Service",
                Description = "Have one robot complete 40 contracts.",
                Test = _ =>
                {
                    var best = SpecialistIdentity.MostDecorated();
                    return best != null && best.FlagsCompleted >= 40;
                }
            },
            new AchievementDef
            {
                Id = AchievementId.SolarConquest,
                Title = "Solar Conquest",
                Description = "Take every body from Earth to Europa.",
                Test = s => s.BodiesConquered >= 5
            },
            new AchievementDef
            {
                Id = AchievementId.Ironman,
                Title = "No Second Draft",
                Description = "Clear a body in Ironman.",
                Secret = true,
                Test = s => s.BodiesConquered >= 1 && IronmanActive
            }
        };

        /// <summary>Set by the run configuration; Ironman disables manual saves and reloads.</summary>
        public static bool IronmanActive { get; set; }

        private static readonly HashSet<AchievementId> Unlocked = new HashSet<AchievementId>();
        private static bool _loaded;

        public static IReadOnlyList<AchievementDef> Definitions => Defs;

        /// <summary>Achievements unlocked this session, for the summary screen to show.</summary>
        public static readonly List<AchievementId> UnlockedThisRun = new List<AchievementId>();

        public static event Action<AchievementDef> Earned;

        public static bool IsUnlocked(AchievementId id)
        {
            Load();
            return Unlocked.Contains(id);
        }

        public static int UnlockedCount
        {
            get
            {
                Load();
                return Unlocked.Count;
            }
        }

        public static AchievementDef Get(AchievementId id)
        {
            for (int i = 0; i < Defs.Count; i++)
            {
                if (Defs[i].Id == id) return Defs[i];
            }
            return null;
        }

        /// <summary>Re-test everything. Cheap enough to call on milestone events, not every frame.</summary>
        public static void Evaluate(RunStats stats)
        {
            if (stats == null) return;
            Load();

            for (int i = 0; i < Defs.Count; i++)
            {
                AchievementDef def = Defs[i];
                if (Unlocked.Contains(def.Id)) continue;
                if (def.Test == null || !def.Test(stats)) continue;

                Unlocked.Add(def.Id);
                UnlockedThisRun.Add(def.Id);
                Earned?.Invoke(def);
            }

            Save();
        }

        public static void BeginRun()
        {
            UnlockedThisRun.Clear();
            Load();
        }

        public static void ResetAll()
        {
            Load();
            Unlocked.Clear();
            UnlockedThisRun.Clear();
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            string raw = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(raw)) return;

            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], out int v) && Enum.IsDefined(typeof(AchievementId), v))
                    Unlocked.Add((AchievementId)v);
            }
        }

        private static void Save()
        {
            if (Unlocked.Count == 0)
            {
                PlayerPrefs.DeleteKey(PrefsKey);
                return;
            }

            var sb = new System.Text.StringBuilder();
            bool first = true;
            foreach (AchievementId id in Unlocked)
            {
                if (!first) sb.Append(',');
                sb.Append((int)id);
                first = false;
            }

            PlayerPrefs.SetString(PrefsKey, sb.ToString());
            PlayerPrefs.Save();
        }
    }
}
