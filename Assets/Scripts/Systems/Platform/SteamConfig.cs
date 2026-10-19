using System.Text;

namespace SolarMajesty
{
    /// <summary>
    /// Steam settings and the names the Steamworks partner site must match. Pure data so the
    /// mapping is unit-tested; <c>SteamService</c> (Runtime) is the only code that talks to Steam.
    ///
    /// Before release: replace <see cref="AppId"/> with the app ID from the Steamworks partner
    /// site and set <see cref="RelaunchThroughSteam"/> to true. Docs/STEAM_RELEASE.md has the list.
    /// </summary>
    public static class SteamConfig
    {
        /// <summary>480 is Valve's public test app (Spacewar). Swap for the real app ID.</summary>
        public const uint AppId = 480;

        /// <summary>True once <see cref="AppId"/> is the real app, not the 480 test app.</summary>
        public static bool IsTestApp => AppId == 480;

        /// <summary>
        /// When true, a release build started outside Steam asks Steam to relaunch it (Valve's
        /// RestartAppIfNecessary). Must stay false while <see cref="AppId"/> is 480, or the game
        /// would hand itself to Spacewar.
        /// </summary>
        public static bool RelaunchThroughSteam => !IsTestApp;

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
