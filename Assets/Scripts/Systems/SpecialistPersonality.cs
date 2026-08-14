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
                    s.description = "Horizon. Cheap Explore. Ignores fights.";
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
                    s.description = "Anvil. Greedy builder. Ignores cheap flags until pay is right.";
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
                    s.description = "Aegis. Cheap Clear Threat is fine; will not wander or tinker.";
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
                case SpecialistClass.Medic:
                    s.displayName = "Medic";
                    s.description = "Triage. Stays near the wounded. Defends; will not hunt dens.";
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
                case SpecialistClass.HarvesterBot:
                    s.displayName = "Harvester Bot";
                    s.description = "Strip. Ore-hungry. Takes Extract cheap; ignores dens and tubes.";
                    s.baseGreed = 0.42f;
                    s.courage = 0.38f;
                    s.workaholicBias = 0.64f;
                    s.explorePreference = 0.22f;
                    s.buildPreference = 0.18f;
                    s.combatPreference = 0.08f;
                    s.defendPreference = 0.12f;
                    s.extractPreference = 1f;
                    s.moveSpeed = 3.4f;
                    s.workRate = 1.28f;
                    s.upkeepPerMinute = Upkeep(ResourceId.Metals, 1);
                    break;
                case SpecialistClass.SurveyorBot:
                    s.displayName = "Surveyor Bot";
                    s.description = "Chart. Maps and samples. Loves Explore and Research Site; shy of fights.";
                    s.baseGreed = 0.28f;
                    s.courage = 0.48f;
                    s.workaholicBias = 0.36f;
                    s.explorePreference = 1f;
                    s.buildPreference = 0.14f;
                    s.combatPreference = 0.08f;
                    s.defendPreference = 0.16f;
                    s.extractPreference = 0.44f;
                    s.moveSpeed = 4.1f;
                    s.workRate = 1.08f;
                    s.upkeepPerMinute = Upkeep(ResourceId.Power, 1);
                    break;
                case SpecialistClass.TerraformerBot:
                    s.displayName = "Terraformer Bot";
                    s.description = "Bloom. Greens the crust. Takes Terraform cheap; slow to fight or wander.";
                    s.baseGreed = 0.34f;
                    s.courage = 0.40f;
                    s.workaholicBias = 0.70f;
                    s.explorePreference = 0.16f;
                    s.buildPreference = 0.82f;
                    s.combatPreference = 0.06f;
                    s.defendPreference = 0.14f;
                    s.extractPreference = 0.55f;
                    s.moveSpeed = 3.0f;
                    s.workRate = 1.22f;
                    s.upkeepPerMinute = Upkeep(ResourceId.WaterIce, 1);
                    break;
                case SpecialistClass.CourierBot:
                    s.displayName = "Courier Bot";
                    s.description = "Haul. Cheap Explore and Outpost; will not hunt dens.";
                    s.baseGreed = 0.30f;
                    s.courage = 0.44f;
                    s.workaholicBias = 0.40f;
                    s.explorePreference = 0.92f;
                    s.buildPreference = 0.16f;
                    s.combatPreference = 0.08f;
                    s.defendPreference = 0.18f;
                    s.extractPreference = 0.72f;
                    s.moveSpeed = 4.3f;
                    s.workRate = 1.05f;
                    s.upkeepPerMinute = Upkeep(ResourceId.Power, 1);
                    break;
                case SpecialistClass.GeologistBot:
                    s.displayName = "Geologist Bot";
                    s.description = "Core. Takes Extract and Research Site cheap; shy of dens.";
                    s.baseGreed = 0.36f;
                    s.courage = 0.40f;
                    s.workaholicBias = 0.58f;
                    s.explorePreference = 0.70f;
                    s.buildPreference = 0.14f;
                    s.combatPreference = 0.08f;
                    s.defendPreference = 0.14f;
                    s.extractPreference = 0.92f;
                    s.moveSpeed = 3.3f;
                    s.workRate = 1.18f;
                    s.upkeepPerMinute = Upkeep(ResourceId.Metals, 1);
                    break;
                case SpecialistClass.SentinelMech:
                    s.displayName = "Sentinel Mech";
                    s.description = "Rim. Cheap Defend Area; will not wander or tinker.";
                    s.baseGreed = 0.18f;
                    s.courage = 0.88f;
                    s.workaholicBias = 0.52f;
                    s.explorePreference = 0.06f;
                    s.buildPreference = 0.05f;
                    s.combatPreference = 0.62f;
                    s.defendPreference = 1f;
                    s.extractPreference = 0.10f;
                    s.moveSpeed = 2.9f;
                    s.workRate = 1.12f;
                    s.upkeepPerMinute = Upkeep(ResourceId.Power, 2);
                    break;
                default:
                    // Do not relabel unknown classes as Scout — keep authored identity.
                    if (string.IsNullOrEmpty(s.displayName))
                        s.displayName = s.specialistClass.ToString();
                    break;
            }
        }

        private static ResourceAmount[] Upkeep(ResourceId id, int amount) =>
            new[] { new ResourceAmount(id, amount) };

        public static SpecialistClass[] Attracts(FlagType type)
        {
            switch (type)
            {
                case FlagType.Explore: return new[] { SpecialistClass.ScoutDrone, SpecialistClass.SurveyorBot, SpecialistClass.CourierBot };
                case FlagType.Build: return new[] { SpecialistClass.EngineerBot };
                case FlagType.Extract: return new[] { SpecialistClass.GeologistBot, SpecialistClass.HarvesterBot, SpecialistClass.EngineerBot, SpecialistClass.ScoutDrone };
                case FlagType.ClearThreat: return new[] { SpecialistClass.DefenseMech, SpecialistClass.SentinelMech };
                case FlagType.DefendArea: return new[] { SpecialistClass.SentinelMech, SpecialistClass.DefenseMech, SpecialistClass.Medic };
                case FlagType.ResearchSite: return new[] { SpecialistClass.SurveyorBot, SpecialistClass.GeologistBot, SpecialistClass.ScoutDrone };
                case FlagType.EstablishOutpost: return new[] { SpecialistClass.CourierBot, SpecialistClass.EngineerBot, SpecialistClass.HarvesterBot, SpecialistClass.ScoutDrone };
                case FlagType.Terraform: return new[] { SpecialistClass.TerraformerBot, SpecialistClass.EngineerBot, SpecialistClass.HarvesterBot };
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
