using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// The Steam demo (Next Fest) is a separate player built by
    /// <c>BuildCommands.BuildWindowsDemo</c> with the <see cref="Define"/> scripting define. That
    /// define is passed to that one build only (<c>BuildPlayerOptions.extraScriptingDefines</c>);
    /// ProjectSettings never carry it, so the editor, the tests and the full game compile without it.
    ///
    /// The demo is Earth to its win, then an end-of-demo screen (wishlist, keep playing Earth, quit).
    /// Every rule is a pure function of a <c>demo</c> flag so EditMode tests cover the gating
    /// without a demo compile; <see cref="Active"/> is the flag the game reads.
    ///
    /// Docs/DEMO.md ("Steam demo build") and Docs/STEAM_RELEASE.md ("Next Fest demo") have the rest.
    /// </summary>
    public static class DemoBuild
    {
        /// <summary>Scripting define set only for the demo player.</summary>
        public const string Define = "SM_DEMO";

        /// <summary>Window title, product name and the LocalLow folder of the demo player.</summary>
        public const string ProductName = "Solar Majesty Demo";

        /// <summary>Exe name of the demo player (the full game ships SolarMajesty.exe).</summary>
        public const string ExeName = "SolarMajestyDemo.exe";

        /// <summary>Bundle identifier of the demo player.</summary>
        public const string BundleId = "com.solarmajesty.solarmajesty.demo";

        /// <summary>The demo's only world, and the one whose win ends it.</summary>
        public const CelestialBodyId DemoWorld = CelestialBodyId.Earth;

#if SM_DEMO
        /// <summary>True in the demo player, fixed at compile time.</summary>
        public const bool CompiledAsDemo = true;
#else
        /// <summary>True in the demo player, fixed at compile time.</summary>
        public const bool CompiledAsDemo = false;
#endif

        private static bool? _override;

        /// <summary>The flag the game reads: the compile-time define, or a test override.</summary>
        public static bool Active => _override ?? CompiledAsDemo;

        /// <summary>Tests force the demo rules on or off; null goes back to the compiled flavour.</summary>
        public static void Override(bool? demo) => _override = demo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _override = null;

        // ---------------------------------------------------------------- worlds

        /// <summary>Earth only in the demo; every world in the full game.</summary>
        public static bool BodyInDemo(bool demo, CelestialBodyId id) => !demo || id == DemoWorld;

        public static bool BodyInDemo(CelestialBodyId id) => BodyInDemo(Active, id);

        /// <summary>A saved or requested world the demo cannot play falls back to Earth.</summary>
        public static CelestialBodyId ClampBody(bool demo, CelestialBodyId id) =>
            BodyInDemo(demo, id) ? id : DemoWorld;

        /// <summary>True when winning <paramref name="conquered"/> ends the demo (the end screen, not a launch).</summary>
        public static bool EndsDemo(bool demo, CelestialBodyId conquered) => demo && conquered == DemoWorld;

        public static bool EndsDemo(CelestialBodyId conquered) => EndsDemo(Active, conquered);

        // ---------------------------------------------------------------- achievements

        /// <summary>
        /// Achievements that need more than Earth (the whole campaign) or a mode the demo locks
        /// (Ironman) are left out of the demo's list and never unlock there.
        /// </summary>
        public static bool AchievementInDemo(bool demo, AchievementId id)
        {
            if (!demo) return true;
            switch (id)
            {
                case AchievementId.SolarConquest:
                case AchievementId.Ironman:
                    return false;
                default:
                    return true;
            }
        }

        public static bool AchievementInDemo(AchievementId id) => AchievementInDemo(Active, id);

        /// <summary>
        /// The demo app has no achievements configured on the partner site, so the demo keeps its
        /// unlocks local instead of calling SetAchievement on an app that would reject them.
        /// </summary>
        public static bool PushAchievementsToSteam(bool demo) => !demo;

        // ---------------------------------------------------------------- files

        /// <summary>Folder under persistentDataPath for full-world saves. The demo never shares the game's.</summary>
        public static string SaveFolder(bool demo) => demo ? "DemoSaves" : "Saves";

        /// <summary>Folder under persistentDataPath for campaign progress and settings (GamePrefs).</summary>
        public static string ProfileFolder(bool demo) => demo ? "DemoProfile" : "Profile";
    }
}
