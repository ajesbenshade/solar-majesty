namespace SolarMajesty
{
    /// <summary>
    /// The end-of-demo screen of the Steam demo: shown instead of the "Continue to Luna" verdict
    /// when Earth is won. Three buttons: wishlist the full game on Steam, keep playing Earth, quit.
    /// It follows the campaign verdict's rule (<see cref="VictoryOffer.ShowPanel"/>): only while
    /// the won world is held, so a continued won save plays on without it.
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

        /// <summary>Buttons in drawing order. Wishlist first: it is the point of a Next Fest demo.</summary>
        public static readonly Choice[] Buttons = { Choice.Wishlist, Choice.KeepPlaying, Choice.Quit };

        /// <summary>A won campaign world whose win ends the demo, not an endless sandbox.</summary>
        public static bool Applies(bool demo, CelestialBodyId body, bool won, bool endless) =>
            won && !endless && DemoBuild.EndsDemo(demo, body);

        public static bool Applies(CelestialBodyId body, bool won, bool endless) =>
            Applies(DemoBuild.Active, body, won, endless);

        /// <summary>
        /// Draw the end screen this frame: the demo's Earth win, behind the same latch as the
        /// campaign verdict (held, not endless, not dismissed).
        /// </summary>
        public static bool Show(bool demo, CelestialBodyId body, bool won, bool held, bool endless, bool dismissed) =>
            Applies(demo, body, won, endless) && VictoryOffer.ShowPanel(won, held, endless, dismissed);

        public const string Headline = "END OF THE DEMO";

        public const string Body =
            "Earth is secured. Thank you for playing the Solar Majesty demo. In the full game the Colony Lifter flies on to Luna and Mars, with guilds, markets and the whole tech tree. Wishlist it to hear the moment it launches.";

        public static string Label(Choice choice)
        {
            switch (choice)
            {
                case Choice.Wishlist: return "WISHLIST ON STEAM";
                case Choice.KeepPlaying: return "KEEP PLAYING EARTH";
                default: return "QUIT";
            }
        }

        /// <summary>Toast after the wishlist button, in case the overlay or browser opens behind the game.</summary>
        public const string WishlistOpened = "Opening the Solar Majesty store page…";

        /// <summary>Title or debug hop to a world the demo does not have.</summary>
        public static string WorldLocked(string worldName) =>
            (string.IsNullOrEmpty(worldName) ? "That world" : worldName) + " is in the full game. This demo is Earth only.";

        /// <summary>Settings line in place of the guided-start / full-campaign switches.</summary>
        public const string SettingsNote =
            "Demo: Earth only. Luna, Mars, guilds and replay modes are in the full game.";
    }
}
