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
                return Loc.S("Falling back to the rest beacon.");
            if (action == SpecialistAction.Rest)
                return reason != null && reason.IndexOf("seeking", System.StringComparison.Ordinal) >= 0
                    ? Loc.S("Heading to the inn.")
                    : Loc.S("Resting. Shop if the EU is there.");
            if (action == SpecialistAction.Repair)
                return Loc.S("Patching a hull. Pay me when it holds.");
            return IdleLine(cls, reason);
        }

        public static string ClaimLine(string displayName, SpecialistClass cls, FlagType type)
        {
            string who = string.IsNullOrEmpty(displayName) ? ClassCallsign(cls) : Loc.S(displayName);
            switch (cls)
            {
                case SpecialistClass.EngineerBot:
                    return Loc.F("{0}: Build's on the books. Don't cheap out mid-weld.", who);
                case SpecialistClass.DefenseMech:
                    return type == FlagType.DefendArea
                        ? Loc.F("{0}: Rim watch. Nothing crosses.", who)
                        : Loc.F("{0}: Hunting the den. Stay behind the shield.", who);
                case SpecialistClass.SentinelMech:
                    return Loc.F("{0}: Perimeter locked. I hold. I do not wander.", who);
                case SpecialistClass.Medic:
                    return Loc.F("{0}: On the wounded. I am not a hunter.", who);
                case SpecialistClass.HarvesterBot:
                    return Loc.F("{0}: Ore first. Dens can wait.", who);
                case SpecialistClass.GeologistBot:
                    return type == FlagType.ResearchSite
                        ? Loc.F("{0}: Core sample logged. Lab can eat this.", who)
                        : Loc.F("{0}: Reading the crust. Haul follows.", who);
                case SpecialistClass.SurveyorBot:
                    return type == FlagType.ResearchSite
                        ? Loc.F("{0}: Site mapped. Science into the tree.", who)
                        : Loc.F("{0}: Charting the apron. No fights.", who);
                case SpecialistClass.ScoutDrone:
                    return Loc.F("{0}: Cheap Explore. Forty EU and I'm gone.", who);
                case SpecialistClass.CourierBot:
                    return type == FlagType.EstablishOutpost
                        ? Loc.F("{0}: Claim the cyan disc. Freight after.", who)
                        : Loc.F("{0}: Hauling the flag. Keep the path clear.", who);
                case SpecialistClass.TerraformerBot:
                    return Loc.F("{0}: Greening this crust. Slow work. Worth it.", who);
                default:
                    return Loc.F("{0} took {1}.", who, FlagLabel(type));
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

        /// <summary>
        /// English flag-type word for ids, logs and model prompts. Show the player
        /// <see cref="FlagLabel"/> instead.
        /// </summary>
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

        /// <summary><see cref="FlagShort"/> in the current language, for on-screen labels.</summary>
        public static string FlagLabel(FlagType type)
        {
            switch (type)
            {
                case FlagType.Explore: return Loc.S("Explore");
                case FlagType.ClearThreat: return Loc.S("Clear Threat");
                case FlagType.Build: return Loc.S("Build");
                case FlagType.Extract: return Loc.S("Extract");
                case FlagType.DefendArea: return Loc.S("Defend");
                case FlagType.ResearchSite: return Loc.S("Research Site");
                case FlagType.EstablishOutpost: return Loc.S("Outpost");
                case FlagType.Terraform: return Loc.S("Terraform");
                default: return Loc.S("flag");
            }
        }

        private static string FlagWorkLine(SpecialistClass cls, FlagType type)
        {
            string job = FlagLabel(type);
            switch (cls)
            {
                case SpecialistClass.EngineerBot: return Loc.F("Welding {0}. Pay holds.", job);
                case SpecialistClass.DefenseMech: return type == FlagType.DefendArea
                    ? Loc.S("Holding the rim.")
                    : Loc.S("Clearing the den.");
                case SpecialistClass.SentinelMech: return Loc.S("Watching the perimeter.");
                case SpecialistClass.Medic: return Loc.S("Covering the wounded.");
                case SpecialistClass.HarvesterBot: return Loc.S("Stripping the node.");
                case SpecialistClass.GeologistBot: return type == FlagType.ResearchSite
                    ? Loc.S("Sampling for the lab.")
                    : Loc.S("Reading ore.");
                case SpecialistClass.SurveyorBot: return type == FlagType.ResearchSite
                    ? Loc.S("Logging the site.")
                    : Loc.S("Charting ahead.");
                case SpecialistClass.CourierBot: return type == FlagType.EstablishOutpost
                    ? Loc.S("Staking the outpost.")
                    : Loc.S("Running freight.");
                case SpecialistClass.TerraformerBot: return Loc.S("Weaving the crust.");
                default: return Loc.F("On {0}.", job);
            }
        }

        private static string HuntLine(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.DefenseMech: return Loc.S("Hunting fauna. No bounty needed.");
                case SpecialistClass.SentinelMech: return Loc.S("Pest on the rim — engaging.");
                default: return Loc.S("Chasing yard pests.");
            }
        }

        private static string IdleLine(SpecialistClass cls, string reason)
        {
            if (reason != null)
            {
                if (reason.IndexOf("workshop", System.StringComparison.Ordinal) >= 0)
                    return Loc.S("At the workshop. Flags nearby pull harder.");
                if (reason.IndexOf("patrol", System.StringComparison.Ordinal) >= 0)
                    return Loc.S("Patrolling the Commons.");
                if (reason.IndexOf("tinker", System.StringComparison.Ordinal) >= 0)
                    return Loc.S("Tinkering in town. Raise EU for Build.");
                if (reason.IndexOf("triage", System.StringComparison.Ordinal) >= 0)
                    return Loc.S("Triage at the inn.");
                if (reason.IndexOf("party", System.StringComparison.Ordinal) >= 0)
                    return Loc.S("Following the party.");
                if (reason.IndexOf("levy_home", System.StringComparison.Ordinal) >= 0)
                    return Loc.S("Walking the purse home. Do not click me.");
                if (reason.IndexOf("levy_collect", System.StringComparison.Ordinal) >= 0)
                    return Loc.S("EU is sitting in building purses. Haul will get around to it.");
                if (reason.IndexOf("frontier", System.StringComparison.Ordinal) >= 0)
                    return Loc.S("Wandering the apron.");
            }

            switch (cls)
            {
                case SpecialistClass.EngineerBot: return Loc.S("Idle. Build flags need real pay.");
                case SpecialistClass.DefenseMech: return Loc.S("Idle. Post Clear Threat or Defend.");
                case SpecialistClass.SentinelMech: return Loc.S("Idle. Defend is cheap for me.");
                case SpecialistClass.Medic: return Loc.S("Idle near the wounded.");
                case SpecialistClass.HarvesterBot: return Loc.S("Idle. Extract is cheap.");
                case SpecialistClass.GeologistBot: return Loc.S("Idle. Extract or Research Site.");
                case SpecialistClass.SurveyorBot: return Loc.S("Idle. Explore or Research Site.");
                case SpecialistClass.CourierBot: return Loc.S("Idle. Explore, Outpost, or walk the levy.");
                case SpecialistClass.TerraformerBot: return Loc.S("Idle. Terraform is cheap.");
                default: return Loc.S("Idle. Cheap Explore will do.");
            }
        }
    }
}
