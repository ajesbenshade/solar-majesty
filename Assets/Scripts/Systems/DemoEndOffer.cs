namespace SolarMajesty
{
    /// <summary>
    /// The end-of-demo screen of the Steam demo: shown instead of the "Continue to Luna" verdict
    /// when Earth is won. Three buttons: wishlist the full game on Steam, keep playing Earth, quit.
    /// Copy and rules live here so they are tested; <c>OverseerHud</c> draws it.
    /// </summary>
    public static class DemoEndOffer
    {
        public enum Choice
        {
            Wishlist,
            KeepPlaying,
            Quit
        }

        /// <summary>Buttons left to right. Wishlist first: it is the point of a Next Fest demo.</summary>
        public static readonly Choice[] Buttons = { Choice.Wishlist, Choice.KeepPlaying, Choice.Quit };

        /// <summary>A won campaign world whose win ends the demo, not an endless sandbox.</summary>
        public static bool Applies(bool demo, CelestialBodyId body, bool won, bool endless) =>
            won && !endless && DemoBuild.EndsDemo(demo, body);

        public static bool Applies(CelestialBodyId body, bool won, bool endless) =>
            Applies(DemoBuild.Active, body, won, endless);

        public static string Headline => Loc.S("END OF THE DEMO");

        public static string Body => Loc.S(
            "Earth is secured. Thank you for playing the Solar Majesty demo. In the full game the Colony Lifter flies on to Luna, Mars, the Belt and Europa, with guilds, markets and the whole tech tree. Wishlist it to hear the moment it launches.");

        public static string Label(Choice choice)
        {
            switch (choice)
            {
                case Choice.Wishlist: return Loc.S("WISHLIST ON STEAM");
                case Choice.KeepPlaying: return Loc.S("KEEP PLAYING EARTH");
                default: return Loc.S("QUIT");
            }
        }

        /// <summary>Toast after the wishlist button, in case the overlay or browser opens behind the game.</summary>
        public static string WishlistOpened => Loc.S("Opening the Solar Majesty store page…");

        /// <summary>Title or debug hop to a world the demo does not have.</summary>
        public static string WorldLocked(string worldName) =>
            Loc.F("{0} is in the full game. This demo is Earth only.", worldName ?? "");

        /// <summary>Settings line in place of the guided-start / full-campaign switches.</summary>
        public static string SettingsNote => Loc.S(
            "Demo: Earth only. Luna, Mars, the Belt, Europa, guilds and replay modes are in the full game.");
    }
}
