using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Keeps decorative IMGUI failures from aborting OnGUI, and logs each distinct one once.</summary>
    internal static class HudSafety
    {
        private static readonly HashSet<string> Seen = new HashSet<string>();

        /// <summary>Logs the exception the first time <paramref name="key"/> is seen. Returns true on that first time.</summary>
        internal static bool LogOnce(string key, Exception e)
        {
            if (!Seen.Add(key ?? string.Empty)) return false;
            Debug.LogException(e);
            return true;
        }

        internal static void Reset() => Seen.Clear();
    }
}
