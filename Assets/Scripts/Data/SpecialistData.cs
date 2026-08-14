using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Autonomous specialist (“hero”) class definition.
    /// Greed, courage, and task preferences feed SpecialistBrain.
    /// </summary>
    [CreateAssetMenu(fileName = "Specialist_", menuName = "Solar Majesty/Specialist", order = 30)]
    public class SpecialistData : ScriptableObject
    {
        [Header("Identity")]
        public SpecialistClass specialistClass = SpecialistClass.EngineerBot;
        public string displayName = "Engineer Bot";
        [TextArea] public string description;

        [Header("Decision personality (0-1)")]
        [Range(0f, 1f)] public float baseGreed = 0.6f;        // higher = needs better bounties
        [Range(0f, 1f)] public float courage = 0.5f;          // 0 = coward, 1 = fearless
        [Range(0f, 1f)] public float workaholicBias = 0.4f;   // higher = resists resting

        [Header("Task preferences (0-1)")]
        [Range(0f, 1f)] public float explorePreference = 0.5f;
        [Range(0f, 1f)] public float buildPreference = 0.5f;
        [Range(0f, 1f)] public float combatPreference = 0.5f;
        [Range(0f, 1f)] public float extractPreference = 0.5f;
        [Range(0f, 1f)] public float defendPreference = 0.5f;

        [Header("Capabilities (runtime agent later)")]
        [Min(0.1f)] public float moveSpeed = 3.5f;
        [Min(0.1f)] public float workRate = 1f;
        [Min(1f)] public float maxEnergy = 100f;

        [Header("Upkeep (per in-game minute)")]
        public ResourceAmount[] upkeepPerMinute;

        [Header("Prefab (assign after drop-in)")]
        public GameObject prefab;

        /// <summary>Task preference for a flag type (0-1). Used by SpecialistBrain.</summary>
        public float GetPreference(FlagType type)
        {
            switch (type)
            {
                case FlagType.Explore: return explorePreference;
                case FlagType.Build: return buildPreference;
                case FlagType.ClearThreat: return combatPreference;
                case FlagType.DefendArea:
                    return defendPreference > 0.01f ? defendPreference : combatPreference;
                case FlagType.Extract: return extractPreference;
                case FlagType.ResearchSite: return explorePreference;
                case FlagType.EstablishOutpost: return extractPreference;
                case FlagType.Terraform: return buildPreference;
                default: return 0.4f;
            }
        }
    }
}
