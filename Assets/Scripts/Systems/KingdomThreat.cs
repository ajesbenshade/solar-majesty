using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>What has to be true before a spawn entry runs.</summary>
    public enum KingdomSpawnTrigger
    {
        Always,
        /// <summary>Only after at least one robot has gone down (Majesty 2 graveyards).</summary>
        AfterHeroDeath
    }

    /// <summary>Where the colony places a spawn.</summary>
    public enum KingdomSpawnSite
    {
        /// <summary>Next to a random colony building (Majesty 2 sewers: pests from inside the town).</summary>
        Burrow,
        /// <summary>Just outside the outermost building, so the raider walks in (Majesty 2 dragon / ogre attackers).</summary>
        ColonyEdge,
        /// <summary>At the Fobot Yard scrap pile (Majesty 2 graveyards).</summary>
        Scrapyard
    }

    /// <summary>
    /// One row of the kingdom spawn table, modelled on Majesty 2's global spawn settings:
    /// a kind, a kingdom-value band, a spawn interval, a count per spawn and a live cap.
    /// </summary>
    [Serializable]
    public struct KingdomSpawnEntry
    {
        public string id;
        public FaunaKind kind;
        public KingdomSpawnSite site;
        public KingdomSpawnTrigger trigger;
        [Tooltip("Entry is active while kingdom value (weighted building sum, Commons = 1) is within [min, max].")]
        public float minValue;
        public float maxValue;
        [Tooltip("Seconds between spawns while under the cap.")]
        [Min(1f)] public float intervalSeconds;
        [Min(1)] public int countMin;
        [Min(1)] public int countMax;
        [Tooltip("Live units this entry may have at once.")]
        [Min(1)] public int cap;
        [Tooltip("Spawn as a boss-grade elite (more health, bite and size).")]
        public bool elite;
    }

    /// <summary>
    /// What one building adds to kingdom value, and the safety field it gives heroes nearby
    /// (Majesty 2 building_value and safety_emitter).
    /// </summary>
    [Serializable]
    public struct BuildingWorth
    {
        public BuildingCategory category;
        [Tooltip("Kingdom value this building adds (Majesty 2: palace 1, guild 0.5, smithy 0.2, house 0).")]
        public float kingdomValue;
        [Tooltip("Safety field strength at the building (palace 10, guild 5, house 3).")]
        public float safetyPower;
        [Tooltip("Safety field radius in metres; falls off linearly to zero.")]
        public float safetyRadius;
    }

    /// <summary>Inspector-editable kingdom threat table. Held by GameLoop.</summary>
    [Serializable]
    public sealed class KingdomThreatTuning
    {
        public bool enabled = true;
        [Tooltip("Elite health multiplier.")]
        public float eliteHealthMul = 4f;
        public float eliteBiteMul = 1.8f;
        public float eliteScaleMul = 1.6f;
        [Tooltip("Elite kill purse / XP multiplier.")]
        public float eliteRewardMul = 4f;
        [Tooltip("Metres beyond the outermost building where edge raiders appear.")]
        public float edgeSpawnDistance = 26f;
        [Tooltip("Metres from the chosen building where burrow pests appear.")]
        public float burrowSpawnDistance = 5f;

        [Header("Kingdom value and safety fields")]
        [Tooltip("Per-category worth. Categories not listed use the fallback values below.")]
        public BuildingWorth[] buildingWorth = DefaultWorth();
        public float fallbackValue = 0.2f;
        public float fallbackSafetyPower = 3f;
        public float fallbackSafetyRadius = 10f;
        [Tooltip("Safety field strength that counts as fully safe (the Commons emits 10).")]
        public float safetyFieldFull = 10f;

        [Header("Spawn table")]
        public KingdomSpawnEntry[] entries = DefaultEntries();

        public BuildingWorth WorthOf(BuildingCategory cat)
        {
            if (buildingWorth != null)
                for (int i = 0; i < buildingWorth.Length; i++)
                    if (buildingWorth[i].category == cat) return buildingWorth[i];
            return new BuildingWorth
            {
                category = cat, kingdomValue = fallbackValue,
                safetyPower = fallbackSafetyPower, safetyRadius = fallbackSafetyRadius
            };
        }

        /// <summary>Safety field contribution (0..1) of one building at a distance.</summary>
        public float SafetyFrom(BuildingCategory cat, float distance)
        {
            var w = WorthOf(cat);
            if (w.safetyRadius <= 0f || w.safetyPower <= 0f || distance >= w.safetyRadius) return 0f;
            float falloff = 1f - Mathf.Max(0f, distance) / w.safetyRadius;
            return w.safetyPower * falloff / Mathf.Max(0.01f, safetyFieldFull);
        }

        /// <summary>Majesty 2 values mapped onto our buildings (houses and producers add nothing).</summary>
        public static BuildingWorth[] DefaultWorth()
        {
            BuildingWorth W(BuildingCategory c, float v, float p, float r) =>
                new BuildingWorth { category = c, kingdomValue = v, safetyPower = p, safetyRadius = r };
            return new[]
            {
                W(BuildingCategory.Commons, 1f, 10f, 30f),          // palace
                W(BuildingCategory.Habitat, 0f, 3f, 10f),           // peasant house
                W(BuildingCategory.Power, 0f, 2f, 10f),             // mill
                W(BuildingCategory.Farm, 0f, 2f, 10f),
                W(BuildingCategory.Mine, 0f, 2f, 10f),
                W(BuildingCategory.RegolithCamp, 0f, 2f, 10f),
                W(BuildingCategory.Mining, 0f, 2f, 10f),
                W(BuildingCategory.Market, 0.5f, 5f, 10f),
                W(BuildingCategory.Blacksmith, 0.2f, 5f, 20f),
                W(BuildingCategory.Inn, 0.5f, 5f, 20f),             // tavern
                W(BuildingCategory.Watchtower, 0.2f, 5f, 10f),      // guard tower
                W(BuildingCategory.LandingPad, 0.2f, 5f, 20f),      // trading post
                W(BuildingCategory.Defense, 0.2f, 7f, 20f),         // wizard tower
                W(BuildingCategory.GuildHall, 0.2f, 5f, 20f),       // hall of lords
                W(BuildingCategory.Laboratory, 0.5f, 5f, 20f),
                W(BuildingCategory.FobotYard, 0.2f, 5f, 10f),
                W(BuildingCategory.AidStation, 0.5f, 5f, 20f),
                W(BuildingCategory.ScoutWorkshop, 0.5f, 5f, 20f),   // guilds
                W(BuildingCategory.EngineerWorkshop, 0.5f, 5f, 20f),
                W(BuildingCategory.DefenseWorkshop, 0.5f, 5f, 20f),
                W(BuildingCategory.MedicWorkshop, 0.5f, 5f, 20f),
                W(BuildingCategory.HarvesterWorkshop, 0.5f, 5f, 20f),
                W(BuildingCategory.SurveyorWorkshop, 0.5f, 5f, 20f),
                W(BuildingCategory.TerraformerWorkshop, 0.5f, 5f, 20f),
                W(BuildingCategory.CourierWorkshop, 0.5f, 5f, 20f),
                W(BuildingCategory.GeologistWorkshop, 0.5f, 5f, 20f),
                W(BuildingCategory.SentinelWorkshop, 0.5f, 10f, 20f),
                W(BuildingCategory.ClimateLoom, 0.5f, 10f, 20f),    // temples
                W(BuildingCategory.AegisSpire, 0.5f, 10f, 20f),
                W(BuildingCategory.DeepArchive, 0.5f, 10f, 20f),
            };
        }

        /// <summary>
        /// Our analog of the Majesty 2 table: town burrows open as the colony grows, boss raiders
        /// come on long timers once it is worth raiding, and wrecks keep spawning junk after a death.
        /// </summary>
        public static KingdomSpawnEntry[] DefaultEntries() => new[]
        {
            new KingdomSpawnEntry { id = "burrow_1", kind = FaunaKind.Mite, site = KingdomSpawnSite.Burrow,
                minValue = 2f, maxValue = 999f, intervalSeconds = 60f, countMin = 1, countMax = 1, cap = 1 },
            new KingdomSpawnEntry { id = "burrow_2", kind = FaunaKind.Hopper, site = KingdomSpawnSite.Burrow,
                minValue = 4f, maxValue = 999f, intervalSeconds = 75f, countMin = 1, countMax = 1, cap = 1 },
            new KingdomSpawnEntry { id = "burrow_3", kind = FaunaKind.Leech, site = KingdomSpawnSite.Burrow,
                minValue = 6f, maxValue = 999f, intervalSeconds = 60f, countMin = 1, countMax = 2, cap = 2 },
            new KingdomSpawnEntry { id = "raider_1", kind = FaunaKind.Stalker, site = KingdomSpawnSite.ColonyEdge,
                minValue = 3f, maxValue = 999f, intervalSeconds = 300f, countMin = 1, countMax = 1, cap = 1, elite = true },
            new KingdomSpawnEntry { id = "raider_2", kind = FaunaKind.Stalker, site = KingdomSpawnSite.ColonyEdge,
                minValue = 6f, maxValue = 999f, intervalSeconds = 240f, countMin = 1, countMax = 1, cap = 1, elite = true },
            new KingdomSpawnEntry { id = "wreck_1", kind = FaunaKind.JunkBot, site = KingdomSpawnSite.Scrapyard,
                trigger = KingdomSpawnTrigger.AfterHeroDeath,
                minValue = 4f, maxValue = 999f, intervalSeconds = 120f, countMin = 1, countMax = 1, cap = 1 },
        };
    }

    /// <summary>A spawn the director wants; GameLoop places and creates it.</summary>
    public struct KingdomSpawnRequest
    {
        public int EntryIndex;
        public FaunaKind Kind;
        public KingdomSpawnSite Site;
        public bool Elite;
        public int Count;
    }

    /// <summary>
    /// Timer logic for <see cref="KingdomThreatTuning"/>. Pure C#: GameLoop supplies the kingdom
    /// value, death count and how many of each entry's units are alive, and spawns what comes back.
    /// Each entry's clock only runs while the entry is active and under its cap, so the first
    /// spawn of a newly unlocked tier comes one full interval after unlock.
    /// </summary>
    public sealed class KingdomThreatDirector
    {
        private float[] _timers = Array.Empty<float>();
        private KingdomThreatTuning _tuning;

        public KingdomThreatDirector(KingdomThreatTuning tuning) => SetTuning(tuning);

        public void SetTuning(KingdomThreatTuning tuning)
        {
            _tuning = tuning ?? new KingdomThreatTuning();
            int n = _tuning.entries?.Length ?? 0;
            if (_timers.Length != n)
            {
                _timers = new float[n];
                for (int i = 0; i < n; i++) _timers[i] = Interval(_tuning.entries[i]);
            }
        }

        public static bool IsActive(in KingdomSpawnEntry e, float kingdomValue, int heroDeaths)
        {
            if (kingdomValue < e.minValue || kingdomValue > Mathf.Max(e.minValue, e.maxValue)) return false;
            if (e.trigger == KingdomSpawnTrigger.AfterHeroDeath && heroDeaths <= 0) return false;
            return true;
        }

        /// <summary>Seconds until the entry next spawns (for HUD / debug); infinity when inactive.</summary>
        public float SecondsUntil(int index) =>
            index >= 0 && index < _timers.Length ? Mathf.Max(0f, _timers[index]) : float.PositiveInfinity;

        public void Tick(
            float dt, float kingdomValue, int heroDeaths,
            Func<int, int> aliveForEntry, List<KingdomSpawnRequest> into, Func<float, float> rng = null)
        {
            into.Clear();
            if (!(dt > 0f) || !_tuning.enabled || _tuning.entries == null) return;
            if (_timers.Length != _tuning.entries.Length) SetTuning(_tuning);

            for (int i = 0; i < _tuning.entries.Length; i++)
            {
                var e = _tuning.entries[i];
                float interval = Interval(e);
                if (!IsActive(e, kingdomValue, heroDeaths))
                {
                    _timers[i] = interval; // re-arm: a tier that unlocks later waits a full interval
                    continue;
                }

                int alive = aliveForEntry != null ? aliveForEntry(i) : 0;
                int cap = Mathf.Max(1, e.cap);
                if (alive >= cap)
                {
                    _timers[i] = interval; // the clock restarts once something is killed
                    continue;
                }

                _timers[i] -= dt;
                if (_timers[i] > 0f) continue;
                _timers[i] = interval;

                int lo = Mathf.Max(1, e.countMin);
                int hi = Mathf.Max(lo, e.countMax);
                float roll = rng != null ? rng(1f) : UnityEngine.Random.value;
                int count = lo + Mathf.Min(hi - lo, Mathf.FloorToInt(roll * (hi - lo + 1)));
                count = Mathf.Min(count, cap - alive);
                if (count <= 0) continue;

                into.Add(new KingdomSpawnRequest
                {
                    EntryIndex = i, Kind = e.kind, Site = e.site, Elite = e.elite, Count = count
                });
            }
        }

        private static float Interval(in KingdomSpawnEntry e) => Mathf.Max(1f, e.intervalSeconds);
    }
}
