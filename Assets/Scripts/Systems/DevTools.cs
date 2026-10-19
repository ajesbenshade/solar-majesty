using System;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Gate for developer shortcuts: world hopping (F10 / Shift+F10), Shift+click unlocks on the
    /// title, the debug overlay (F8), the outpost bounty seed (F9) and the fatigue test key (R).
    ///
    /// On in the editor and development builds. Off in release builds unless the game is started
    /// with <c>-devtools</c>, so players cannot unlock worlds or achievements by accident, and
    /// the store build never advertises a cheat in its UI.
    /// </summary>
    public static class DevTools
    {
        public const string Arg = "-devtools";

        private static bool? _enabled;

        public static bool Enabled
        {
            get
            {
                if (!_enabled.HasValue)
                    _enabled = Resolve(Application.isEditor, Debug.isDebugBuild, Environment.GetCommandLineArgs());
                return _enabled.Value;
            }
        }

        /// <summary>Tests force a value; null goes back to the build/command-line default.</summary>
        public static void Override(bool? enabled) => _enabled = enabled;

        public static bool Resolve(bool isEditor, bool isDevelopmentBuild, string[] args)
        {
            if (isEditor || isDevelopmentBuild) return true;
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], Arg, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _enabled = null;
    }
}
