using System;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Handheld defaults: the Steam Deck, or any PC started with <c>-handheld</c>. A handheld gets
    /// a 100% HUD at 1280×800 (<see cref="HudScaleMath.Handheld"/>), the Medium quality tier on
    /// first launch, and (in <c>GamepadInput</c>) starts in controller mode. The player's own
    /// choices in Settings always win.
    /// </summary>
    public static class HandheldMode
    {
        /// <summary>Quality tier a handheld starts on: 0 Very Low, 1 Low, 2 Medium, 3 High, 4 Very High, 5 Ultra.</summary>
        public const int DefaultQualityLevel = 2;

        private static bool _detected;

        /// <summary>Running on a Steam Deck (from the hardware, or confirmed by Steam).</summary>
        public static bool OnSteamDeck { get; private set; }

        /// <summary>Handheld layout in use (Deck or <c>-handheld</c>).</summary>
        public static bool Active => HudScaleMath.Handheld;

        /// <summary>Look at the command line, environment and GPU once. Cheap after the first call.</summary>
        public static void DetectOnce()
        {
            if (_detected) return;
            _detected = true;
            string env = null;
            try
            {
                env = Environment.GetEnvironmentVariable("SteamDeck");
            }
            catch (Exception)
            {
                // No environment access: the GPU name and Steam still decide.
            }
            Apply(SteamConfig.HandheldByArgs(Environment.GetCommandLineArgs()),
                SteamConfig.LooksLikeSteamDeck(SystemInfo.graphicsDeviceName, env));
        }

        /// <summary>Steam says this is a Deck (SteamService, once Steam is up).</summary>
        public static void ConfirmSteamDeck() => Apply(false, true);

        private static void Apply(bool byArgs, bool deck)
        {
            if (deck) OnSteamDeck = true;
            if (byArgs || deck) HudScaleMath.Handheld = true;
        }

        /// <summary>
        /// The quality tier to start on. A saved choice always wins; otherwise a handheld gets
        /// <see cref="DefaultQualityLevel"/> and anything else the project default.
        /// </summary>
        public static int StartingQuality(bool hasSaved, int saved, int projectDefault, bool handheld, int levelCount)
        {
            int max = Math.Max(0, levelCount - 1);
            int pick = hasSaved ? saved : handheld ? DefaultQualityLevel : projectDefault;
            return Mathf.Clamp(pick, 0, max);
        }

        /// <summary>For tests: forget what was detected.</summary>
        public static void ResetForTests()
        {
            _detected = false;
            OnSteamDeck = false;
            HudScaleMath.Handheld = false;
        }
    }
}
