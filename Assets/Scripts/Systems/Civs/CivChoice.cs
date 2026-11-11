namespace SolarMajesty
{
    /// <summary>
    /// Carries the civ picked on the new-game dialog across the scene reload, the same way
    /// <see cref="BodySeed"/> carries the chosen world: one number in <see cref="GamePrefs"/>.
    /// It also keeps the campaign's civ when travel or a retry boots a world that has no save yet.
    ///
    /// This is a hand-off, not the live civ. It holds nothing in memory; the civ in play is the
    /// <see cref="CivModifiers"/> its owner holds, and a loaded save always wins over this value.
    /// TODO(multiplayer): one choice per player slot, sent by the lobby instead of read from prefs.
    /// </summary>
    public static class CivChoice
    {
        public const string PrefsKey = "SM_Civ";

        /// <summary>The civ the next boot should start with. None when nothing was picked.</summary>
        public static CivId Load() => CivCatalog.FromSaved(GamePrefs.GetInt(PrefsKey, (int)CivId.None));

        /// <summary>
        /// True when a new campaign dropping on <paramref name="drop"/> is the guided Earth start
        /// (the tutorial). It always plays as the neutral civ (decision 1), and carries that on to
        /// Luna with no prompt to pick (decision 6), because the carried choice stays None.
        /// </summary>
        public static bool IsGuidedStart(CelestialBodyId drop, bool guidedStart) =>
            guidedStart && drop == CelestialBodyId.Earth;

        /// <summary>The civ a new campaign starts with: the pick, except the guided Earth start plays as None.</summary>
        public static CivId ForNewGame(CivId picked, CelestialBodyId drop, bool guidedStart) =>
            IsGuidedStart(drop, guidedStart) ? CivId.None : CivCatalog.FromSaved((int)picked);

        /// <summary>Remember <paramref name="civ"/> for the next boot. An unknown value is stored as None.</summary>
        public static void Set(CivId civ)
        {
            GamePrefs.SetInt(PrefsKey, (int)CivCatalog.FromSaved((int)civ));
            GamePrefs.Save();
        }
    }
}
