using System;
using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>
    /// Versioned full-world snapshot. Continue applies flags (with remaining work), specialist
    /// combat state, and living fauna. Campus / stockpile / research still also live in the
    /// legacy PlayerPrefs blobs so an older slot without this file still loads the settlement.
    /// Shape is JsonUtility-friendly: concrete [Serializable] classes and Lists only, no dictionaries.
    /// </summary>
    [Serializable]
    public sealed class SaveGame
    {
        /// <summary>Bump when a field's meaning changes. Readers reject unknown future versions.</summary>
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public string gameVersion = "";
        public string savedAtUtc = "";
        public string label = "";
        public double playSeconds;
        public long simSteps;

        public int body;
        public int seed;
        public int highestUnlocked;

        public SaveStockpile stockpile = new SaveStockpile();
        public SaveSettlementState settlement = new SaveSettlementState();
        public SaveResearchState research = new SaveResearchState();
        public SaveMissionState mission = new SaveMissionState();
        public SaveReplayState replay = new SaveReplayState();

        public List<SaveBuilding> buildings = new List<SaveBuilding>();
        public List<SaveFlag> flags = new List<SaveFlag>();
        public List<SaveAgent> agents = new List<SaveAgent>();
        public List<SaveFauna> fauna = new List<SaveFauna>();
        public List<SaveNode> nodes = new List<SaveNode>();
        public List<SaveLair> lairs = new List<SaveLair>();
        public List<SaveParty> parties = new List<SaveParty>();

        /// <summary>Human-readable one-liner for a load menu row.</summary>
        public string Describe()
        {
            string when = string.IsNullOrEmpty(savedAtUtc) ? "unknown" : savedAtUtc;
            var hours = TimeSpan.FromSeconds(playSeconds);
            return $"{BodyName((CelestialBodyId)body)} · {buildings.Count} modules · " +
                   $"{agents.Count} robots · {hours:hh\\:mm} · {when}";
        }

        private static string BodyName(CelestialBodyId id)
        {
            switch (id)
            {
                case CelestialBodyId.Earth: return "Earth";
                case CelestialBodyId.Luna: return "Luna";
                case CelestialBodyId.Mars: return "Mars";
                case CelestialBodyId.Belt: return "Belt";
                case CelestialBodyId.Europa: return "Europa";
                default: return id.ToString();
            }
        }
    }

    [Serializable]
    public sealed class SaveStockpile
    {
        public int regolith;
        public int waterIce;
        public int metals;
        public int power;
    }

    [Serializable]
    public sealed class SaveSettlementState
    {
        public int population;
        public int populationGoal;
        public int villageHabs;
        public int bonusBeds;
        public bool hasOutpost;
        public bool everHadHab;
    }

    [Serializable]
    public sealed class SaveResearchState
    {
        /// <summary>Unlocked tech ids as ints.</summary>
        public List<int> unlocked = new List<int>();
        public int activeTech;
        public float activeProgress;
        public float bankedScience;
    }

    [Serializable]
    public sealed class SaveMissionState
    {
        public int state;
        public float elapsed;
        public float sustainHold;
        public bool densCleared;
        public bool sustainMet;
        public bool launchReady;
    }

    [Serializable]
    public sealed class SaveReplayState
    {
        public int mode;
        public int challenge;
        public int stance;
    }

    [Serializable]
    public sealed class SaveBuilding
    {
        public int category;
        public int x;
        public int y;
        public int w;
        public int h;
        /// <summary>Construction progress in thousandths; 1000 means finished.</summary>
        public int progressMilli;
        public bool villageHab;
        public float health;
        public int levyPurse;
        public bool laserArmed;
    }

    [Serializable]
    public sealed class SaveFlag
    {
        public int flagType;
        public float px;
        public float py;
        public float pz;
        public float bounty;
        public int escrowMetals;
        public float workDone;
        public float postedWork;
        /// <summary>Soft-claim count at save time. Restored by rebinding specialists, not copied blindly.</summary>
        public int claimCount;
    }

    [Serializable]
    public sealed class SaveAgent
    {
        public int specialistClass;
        public float px;
        public float py;
        public float pz;
        public float health;
        public float fatigue;
        public int credits;
        public bool downed;
        public float downedTimer;
        public int downCount;
        /// <summary>Index into <see cref="SaveGame.flags"/> the robot was soft-claiming, or -1.</summary>
        public int claimedFlagIndex = -1;
    }

    [Serializable]
    public sealed class SaveFauna
    {
        public int kind;
        public float px;
        public float py;
        public float pz;
        public float health;
    }

    [Serializable]
    public sealed class SaveNode
    {
        public int nodeType;
        public float px;
        public float py;
        public float pz;
        public int remaining;
    }

    [Serializable]
    public sealed class SaveLair
    {
        public float px;
        public float py;
        public float pz;
        public bool cleared;
        public bool scouted;
    }

    [Serializable]
    public sealed class SaveParty
    {
        public int leaderClass;
        public List<int> memberClasses = new List<int>();
    }
}
