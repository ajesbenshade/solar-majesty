namespace SolarMajesty
{
    /// <summary>
    /// Authored voice and HUD copy. Presentation only — SpecialistBrain scoring is unchanged.
    /// </summary>
    public static class SpecialistFlavor
    {
        public static string CardLine(
            SpecialistClass cls,
            SpecialistAction action,
            string reason,
            FlagType? flag)
        {
            if (action == SpecialistAction.PursueFlag && flag.HasValue)
                return FlagWorkLine(cls, flag.Value);
            if (action == SpecialistAction.Hunt)
                return HuntLine(cls);
            if (action == SpecialistAction.Flee)
                return "Falling back to the rest beacon.";
            if (action == SpecialistAction.Rest)
                return reason != null && reason.IndexOf("seeking", System.StringComparison.Ordinal) >= 0
                    ? "Heading to the inn."
                    : "Resting. Shop if the credits are there.";
            if (action == SpecialistAction.Repair)
                return "Patching a hull. Pay me when it holds.";
            return IdleLine(cls, reason);
        }

        public static string ClaimLine(string displayName, SpecialistClass cls, FlagType type)
        {
            string who = string.IsNullOrEmpty(displayName) ? ClassCallsign(cls) : displayName;
            switch (cls)
            {
                case SpecialistClass.EngineerBot:
                    return $"{who}: Build's on the books. Don't cheap out mid-weld.";
                case SpecialistClass.DefenseMech:
                    return type == FlagType.DefendArea
                        ? $"{who}: Rim watch. Nothing crosses."
                        : $"{who}: Hunting the den. Stay behind the shield.";
                case SpecialistClass.SentinelMech:
                    return $"{who}: Perimeter locked. I hold. I do not wander.";
                case SpecialistClass.Medic:
                    return $"{who}: On the wounded. I am not a hunter.";
                case SpecialistClass.HarvesterBot:
                    return $"{who}: Ore first. Dens can wait.";
                case SpecialistClass.GeologistBot:
                    return type == FlagType.ResearchSite
                        ? $"{who}: Core sample logged. Lab can eat this."
                        : $"{who}: Reading the crust. Haul follows.";
                case SpecialistClass.SurveyorBot:
                    return type == FlagType.ResearchSite
                        ? $"{who}: Site mapped. Science into the tree."
                        : $"{who}: Charting the apron. No fights.";
                case SpecialistClass.ScoutDrone:
                    return $"{who}: Cheap Explore. Forty credits and I'm gone.";
                case SpecialistClass.CourierBot:
                    return type == FlagType.EstablishOutpost
                        ? $"{who}: Claim the cyan disc. Freight after."
                        : $"{who}: Hauling the flag. Keep the path clear.";
                case SpecialistClass.TerraformerBot:
                    return $"{who}: Greening this crust. Slow work. Worth it.";
                default:
                    return $"{who} took {FlagShort(type)}.";
            }
        }

        public static string ClassCallsign(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.EngineerBot: return "Anvil";
                case SpecialistClass.DefenseMech: return "Aegis";
                case SpecialistClass.Medic: return "Triage";
                case SpecialistClass.HarvesterBot: return "Strip";
                case SpecialistClass.SurveyorBot: return "Chart";
                case SpecialistClass.TerraformerBot: return "Bloom";
                case SpecialistClass.CourierBot: return "Haul";
                case SpecialistClass.GeologistBot: return "Core";
                case SpecialistClass.SentinelMech: return "Rim";
                default: return "Horizon";
            }
        }

        public static string FlagShort(FlagType type)
        {
            switch (type)
            {
                case FlagType.Explore: return "Explore";
                case FlagType.ClearThreat: return "Clear Threat";
                case FlagType.Build: return "Build";
                case FlagType.Extract: return "Extract";
                case FlagType.DefendArea: return "Defend";
                case FlagType.ResearchSite: return "Research Site";
                case FlagType.EstablishOutpost: return "Outpost";
                case FlagType.Terraform: return "Terraform";
                default: return "flag";
            }
        }

        private static string FlagWorkLine(SpecialistClass cls, FlagType type)
        {
            string job = FlagShort(type);
            switch (cls)
            {
                case SpecialistClass.EngineerBot: return $"Welding {job}. Pay holds.";
                case SpecialistClass.DefenseMech: return type == FlagType.DefendArea
                    ? "Holding the rim."
                    : "Clearing the den.";
                case SpecialistClass.SentinelMech: return "Watching the perimeter.";
                case SpecialistClass.Medic: return "Covering the wounded.";
                case SpecialistClass.HarvesterBot: return "Stripping the node.";
                case SpecialistClass.GeologistBot: return type == FlagType.ResearchSite
                    ? "Sampling for the lab."
                    : "Reading ore.";
                case SpecialistClass.SurveyorBot: return type == FlagType.ResearchSite
                    ? "Logging the site."
                    : "Charting ahead.";
                case SpecialistClass.CourierBot: return type == FlagType.EstablishOutpost
                    ? "Staking the outpost."
                    : "Running freight.";
                case SpecialistClass.TerraformerBot: return "Weaving the crust.";
                default: return $"On {job}.";
            }
        }

        private static string HuntLine(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.DefenseMech: return "Hunting fauna. No bounty needed.";
                case SpecialistClass.SentinelMech: return "Pest on the rim — engaging.";
                default: return "Chasing campus pests.";
            }
        }

        private static string IdleLine(SpecialistClass cls, string reason)
        {
            if (reason != null)
            {
                if (reason.IndexOf("workshop", System.StringComparison.Ordinal) >= 0)
                    return "At the workshop. Flags nearby pull harder.";
                if (reason.IndexOf("patrol", System.StringComparison.Ordinal) >= 0)
                    return "Patrolling the Commons.";
                if (reason.IndexOf("tinker", System.StringComparison.Ordinal) >= 0)
                    return "Tinkering in town. Raise $ for Build.";
                if (reason.IndexOf("triage", System.StringComparison.Ordinal) >= 0)
                    return "Triage at the inn.";
                if (reason.IndexOf("party", System.StringComparison.Ordinal) >= 0)
                    return "Following the party.";
                if (reason.IndexOf("frontier", System.StringComparison.Ordinal) >= 0)
                    return "Wandering the apron.";
            }

            switch (cls)
            {
                case SpecialistClass.EngineerBot: return "Idle. Build flags need real pay.";
                case SpecialistClass.DefenseMech: return "Idle. Post Clear Threat or Defend.";
                case SpecialistClass.SentinelMech: return "Idle. Defend is cheap for me.";
                case SpecialistClass.Medic: return "Idle near the wounded.";
                case SpecialistClass.HarvesterBot: return "Idle. Extract is cheap.";
                case SpecialistClass.GeologistBot: return "Idle. Extract or Research Site.";
                case SpecialistClass.SurveyorBot: return "Idle. Explore or Research Site.";
                case SpecialistClass.CourierBot: return "Idle. Explore or Outpost.";
                case SpecialistClass.TerraformerBot: return "Idle. Terraform is cheap.";
                default: return "Idle. Cheap Explore will do.";
            }
        }
    }
}
