namespace SolarMajesty
{
    /// <summary>
    /// Status and defeat lines that name the colony's home. A named civ says its castle
    /// (Stilt Arcology, Ziggurat Keep, Ring Citadel, Dome Bunker). The neutral civ — the guided
    /// tutorial, a save from before civs, and a continue of that campaign onto Luna — keeps the
    /// Colony Commons sentences, so their existing translations stay.
    /// </summary>
    public static class CivHomeCopy
    {
        public const string NeutralHome = CivCatalog.NeutralHomeName;

        public static string HomeName(CivModifiers civ) =>
            civ == null || string.IsNullOrEmpty(civ.HomeName) ? NeutralHome : civ.HomeName;

        /// <summary>True when this colony's home is still the neutral Commons, not a civ castle.</summary>
        public static bool UsesNeutralHome(CivModifiers civ) =>
            civ == null || civ.Id == CivId.None || HomeName(civ) == NeutralHome;

        /// <summary>Colony log when the home is placed and the player may build anywhere nearby.</summary>
        public static string Placed(CivModifiers civ)
        {
            if (UsesNeutralHome(civ))
                return Loc.S("Colony Commons is down — build workshops, charging stations and shops anywhere nearby.");
            return Loc.F("{0} is down — build workshops, charging stations and shops anywhere nearby.", Loc.S(HomeName(civ)));
        }

        /// <summary>Short status toast used when a world has no briefing of its own.</summary>
        public static string PlacedBrief(CivModifiers civ)
        {
            if (UsesNeutralHome(civ))
                return Loc.S("Colony Commons is down. Build anywhere on open ground.");
            return Loc.F("{0} is down. Build anywhere on open ground.", Loc.S(HomeName(civ)));
        }

        /// <summary>Defeat line when the home itself has fallen.</summary>
        public static string Fell(CivModifiers civ)
        {
            if (UsesNeutralHome(civ))
                return Loc.S("The Colony Commons fell and the till with it. Retry this body, or New Game.");
            return Loc.F("The {0} fell and the till with it. Retry this body, or New Game.", Loc.S(HomeName(civ)));
        }
    }
}
