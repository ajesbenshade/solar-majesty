using System.Text;

namespace SolarMajesty
{
    /// <summary>
    /// Steam settings and the names the Steamworks partner site must match. Pure data so the
    /// mapping is unit-tested; <c>SteamService</c> (Runtime) is the only code that talks to Steam.
    ///
    /// Before release: replace <see cref="FullGameAppId"/> (and <see cref="DemoAppId"/> for the
    /// Next Fest demo) with the app IDs from the Steamworks partner site. Relaunching through
    /// Steam turns itself on for any ID other than 480. Docs/STEAM_RELEASE.md has the list.
    /// </summary>
    public static class SteamConfig
    {
        /// <summary>Valve's public test app (Spacewar).</summary>
        public const uint TestAppId = 480;

        /// <summary>480 is Valve's public test app (Spacewar). Swap for the real app ID.</summary>
        public const uint FullGameAppId = TestAppId;

        /// <summary>
        /// The Steam demo is its own app with its own App ID and depot (Steamworks: the game's
        /// "Create a demo" page). TODO(steam): replace 480 with the demo App ID once the demo app
        /// exists. It must differ from <see cref="FullGameAppId"/>; SteamConfigTests enforces that.
        /// </summary>
        public const uint DemoAppId = TestAppId;

        /// <summary>App ID of the build that is running: the demo's in the demo player.</summary>
        public static uint AppId => AppIdFor(DemoBuild.CompiledAsDemo);

        /// <summary>App ID for a flavour. The editor's build script asks for the demo's this way.</summary>
        public static uint AppIdFor(bool demo) => demo ? DemoAppId : FullGameAppId;

        /// <summary>True while <see cref="AppId"/> is the 480 test app, not the real one.</summary>
        public static bool IsTestApp => IsTestAppId(AppId);

        public static bool IsTestAppId(uint appId) => appId == TestAppId;

        /// <summary>
        /// When true, a release build started outside Steam asks Steam to relaunch it (Valve's
        /// RestartAppIfNecessary). Must stay false while <see cref="AppId"/> is 480, or the game
        /// would hand itself to Spacewar.
        /// </summary>
        public static bool RelaunchThroughSteam => RelaunchThroughSteamFor(AppId);

        public static bool RelaunchThroughSteamFor(uint appId) => !IsTestAppId(appId);

        /// <summary>
        /// The full game's store page: where the demo's "Wishlist on Steam" button goes. Always
        /// the full game, never the demo app. TODO(steam): follows <see cref="FullGameAppId"/>,
        /// so it points at Spacewar's page until the real App ID is in.
        /// </summary>
        public static string StorePageUrl => StorePageUrlFor(FullGameAppId);

        public static string StorePageUrlFor(uint appId) => "https://store.steampowered.com/app/" + appId + "/";

        /// <summary>Command-line switch that skips Steam entirely (offline QA, the AI pack, CI).</summary>
        public const string NoSteamArg = "-nosteam";

        /// <summary>
        /// Steam API name for an achievement: ACH_ plus the enum name in upper snake case
        /// (FirstContract → ACH_FIRST_CONTRACT). These are the names to enter on the partner site.
        /// Renaming an <see cref="AchievementId"/> renames its Steam achievement, so don't.
        /// </summary>
        public static string AchievementApiName(AchievementId id) => "ACH_" + UpperSnake(id.ToString());

        /// <summary>Rich-presence token for the screen the player is on (see Tools/steam/rich_presence_english.vdf).</summary>
        public static string PresenceToken(bool inGame, bool paused) =>
            !inGame ? "#AtTitle" : paused ? "#Paused" : "#Governing";

        public static string UpperSnake(string pascal)
        {
            if (string.IsNullOrEmpty(pascal)) return "";
            var sb = new StringBuilder(pascal.Length + 8);
            for (int i = 0; i < pascal.Length; i++)
            {
                char c = pascal[i];
                bool boundary = i > 0 && char.IsUpper(c) &&
                                (char.IsLower(pascal[i - 1]) ||
                                 char.IsDigit(pascal[i - 1]) ||
                                 (i + 1 < pascal.Length && char.IsLower(pascal[i + 1]) && char.IsUpper(pascal[i - 1])));
                if (boundary) sb.Append('_');
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary><c>-handheld</c>: the Deck layout on other PC handhelds, or to preview it on a desktop.</summary>
        public static bool HandheldByArgs(string[] args)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-handheld", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>True when the command line asks for no Steam.</summary>
        public static bool SteamDisabledByArgs(string[] args)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], NoSteamArg, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
