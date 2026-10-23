using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Why a boot did or did not earn the automatic intro.</summary>
    public readonly struct IntroBootContext
    {
        public readonly bool BootStraightIntoPlay;
        public readonly bool ReturningFromSession;
        public readonly bool ContinueOrLoadReady;
        public readonly bool StillCapture;

        public IntroBootContext(
            bool bootStraightIntoPlay,
            bool returningFromSession,
            bool continueOrLoadReady,
            bool stillCapture)
        {
            BootStraightIntoPlay = bootStraightIntoPlay;
            ReturningFromSession = returningFromSession;
            ContinueOrLoadReady = continueOrLoadReady;
            StillCapture = stillCapture;
        }

        /// <summary>Cold open onto the title, with nothing to Continue or Load.</summary>
        public static IntroBootContext ColdTitle =>
            new IntroBootContext(false, false, false, false);
    }

    /// <summary>
    /// First-launch gate. The intro plays once, on a cold open to the title.
    /// Continue, Load, New Game reloads, and returning from a colony never wait on it.
    /// </summary>
    public static class IntroLaunch
    {
        public const string SeenKey = "SM_IntroSeen";
        public const string PlayOnLaunchKey = "SM_IntroOnLaunch";

        public static bool HasSeen => GamePrefs.GetInt(SeenKey, 0) == 1;

        /// <summary>Missing key stays on. A stored 0 is an explicit off.</summary>
        public static bool PlayOnLaunch =>
            !GamePrefs.HasKey(PlayOnLaunchKey) || GamePrefs.GetInt(PlayOnLaunchKey, 1) != 0;

        public static void SetPlayOnLaunch(bool on)
        {
            GamePrefs.SetInt(PlayOnLaunchKey, on ? 1 : 0);
            GamePrefs.Save();
        }

        public static void MarkSeen()
        {
            if (HasSeen) return;
            GamePrefs.SetInt(SeenKey, 1);
            GamePrefs.Save();
        }

        /// <summary>
        /// QA and capture runs start on a throwaway profile, so they would always look like a first
        /// launch: <c>-nointro</c> and every harness flag (autoplay, benchmark, store/beat/HUD shots)
        /// skip the automatic intro. Settings → Replay intro still works.
        /// </summary>
        public static bool SkippedByCommandLine(string[] args)
        {
            if (args == null) return false;
            foreach (string a in args)
            {
                if (string.IsNullOrEmpty(a)) continue;
                switch (a.ToLowerInvariant())
                {
                    case "-nointro": case "-autoplay": case "-benchmark": case "-storeshots":
                    case "-beatshots": case "-smhudshot": case "-batchmode":
                        return true;
                }
            }
            return false;
        }

        public static bool ShouldAutoPlay(IntroBootContext boot)
        {
            if (boot.BootStraightIntoPlay) return false;
            if (boot.ReturningFromSession) return false;
            if (boot.ContinueOrLoadReady) return false;
            if (boot.StillCapture) return false;
            if (!PlayOnLaunch) return false;
            if (HasSeen) return false;
            return true;
        }
    }
}
