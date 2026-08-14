using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Locked Phase 1 personality matrices. Data values only — SpecialistBrain scoring is unchanged.
    /// Call Apply() after loading SOs so Play Mode always matches the demo roster.
    /// </summary>
    public static class SpecialistPersonality
    {
        public static void Apply(SpecialistData s)
        {
            if (s == null) return;
            switch (s.specialistClass)
            {
                case SpecialistClass.ScoutDrone:
                    s.displayName = "Scout Drone";
                    s.description = "Curious cheapskate. Takes modest Explore; ignores fights.";
                    s.baseGreed = 0.32f;
                    s.courage = 0.52f;
                    s.workaholicBias = 0.28f;
                    s.explorePreference = 1f;
                    s.buildPreference = 0.12f;
                    s.combatPreference = 0.10f;
                    s.defendPreference = 0.18f;
                    s.extractPreference = 0.38f;
                    s.moveSpeed = 4.4f;
                    s.workRate = 1f;
                    s.upkeepPerMinute = Upkeep(ResourceId.Power, 1);
                    break;
                case SpecialistClass.EngineerBot:
                    s.displayName = "Engineer Bot";
                    s.description = "Greedy builder. Ignores cheap flags. Loves Build once pay is right.";
                    s.baseGreed = 0.88f;
                    s.courage = 0.22f;
                    s.workaholicBias = 0.72f;
                    s.explorePreference = 0.08f;
                    s.buildPreference = 1f;
                    s.combatPreference = 0.05f;
                    s.defendPreference = 0.10f;
                    s.extractPreference = 0.62f;
                    s.moveSpeed = 3.1f;
                    s.workRate = 1.35f;
                    s.upkeepPerMinute = Upkeep(ResourceId.Metals, 1);
                    break;
                case SpecialistClass.DefenseMech:
                    s.displayName = "Defense Mech";
                    s.description = "Duty-bound. Cheap Clear Threat is fine; will not wander or tinker.";
                    s.baseGreed = 0.22f;
                    s.courage = 0.94f;
                    s.workaholicBias = 0.48f;
                    s.explorePreference = 0.08f;
                    s.buildPreference = 0.06f;
                    s.combatPreference = 1f;
                    s.defendPreference = 0.92f;
                    s.extractPreference = 0.12f;
                    s.moveSpeed = 3f;
                    s.workRate = 1.15f;
                    s.upkeepPerMinute = Upkeep(ResourceId.Power, 2);
                    break;
                default:
                    s.displayName = "Medic";
                    s.description = "Stays near the wounded. Defends; will not hunt dens.";
                    s.baseGreed = 0.24f;
                    s.courage = 0.42f;
                    s.workaholicBias = 0.58f;
                    s.explorePreference = 0.18f;
                    s.buildPreference = 0.08f;
                    s.combatPreference = 0.06f;
                    s.defendPreference = 0.90f;
                    s.extractPreference = 0.12f;
                    s.moveSpeed = 3.6f;
                    s.workRate = 1.1f;
                    s.upkeepPerMinute = Upkeep(ResourceId.WaterIce, 1);
                    break;
            }
        }

        private static ResourceAmount[] Upkeep(ResourceId id, int amount) =>
            new[] { new ResourceAmount(id, amount) };

        public static SpecialistClass[] Attracts(FlagType type)
        {
            switch (type)
            {
                case FlagType.Explore: return new[] { SpecialistClass.ScoutDrone };
                case FlagType.Build: return new[] { SpecialistClass.EngineerBot };
                case FlagType.Extract: return new[] { SpecialistClass.EngineerBot, SpecialistClass.ScoutDrone };
                case FlagType.ClearThreat: return new[] { SpecialistClass.DefenseMech };
                case FlagType.DefendArea: return new[] { SpecialistClass.DefenseMech, SpecialistClass.Medic };
                default: return new SpecialistClass[0];
            }
        }

        public static void ApplyFlagAffinity(FlagData flag)
        {
            if (flag == null) return;
            flag.stronglyAttracts = Attracts(flag.flagType);
        }
    }
}
