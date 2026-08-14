using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Campaign unlock spine: Earth tutorial → Luna → Mars.
    /// Persists highest unlocked body; free body-hopping is limited to unlocked worlds.
    /// </summary>
    public static class CampaignProgress
    {
        private const string MaxKey = "SM_CampaignMaxBody";
        private const string FreshKey = "SM_CampaignInitialized";
        private const string TravelLogKey = "SM_PendingTravelLog";

        public static CelestialBodyId HighestUnlocked { get; private set; } = CelestialBodyId.Earth;

        public static void Ensure()
        {
            if (!PlayerPrefs.HasKey(FreshKey))
            {
                HighestUnlocked = CelestialBodyId.Earth;
                PlayerPrefs.SetInt(MaxKey, (int)CelestialBodyId.Earth);
                PlayerPrefs.SetInt(FreshKey, 1);
                BodySeed.SetBody(CelestialBodyId.Earth);
                PlayerPrefs.Save();
                return;
            }

            HighestUnlocked = (CelestialBodyId)PlayerPrefs.GetInt(MaxKey, (int)CelestialBodyId.Earth);
            if ((int)HighestUnlocked < (int)CelestialBodyId.Earth ||
                (int)HighestUnlocked > (int)CelestialBodyId.Mars)
                HighestUnlocked = CelestialBodyId.Earth;
        }

        public static bool IsUnlocked(CelestialBodyId id) =>
            (int)id <= (int)HighestUnlocked;

        public static CelestialBodyId? NextAfter(CelestialBodyId current)
        {
            int n = (int)current + 1;
            if (n > (int)CelestialBodyId.Mars) return null;
            return (CelestialBodyId)n;
        }

        /// <summary>Call when the current body is conquered (all gates met / win dismissed into next).</summary>
        public static void UnlockNextFrom(CelestialBodyId conquered)
        {
            var next = NextAfter(conquered);
            if (!next.HasValue) return;
            if ((int)next.Value > (int)HighestUnlocked)
            {
                HighestUnlocked = next.Value;
                PlayerPrefs.SetInt(MaxKey, (int)HighestUnlocked);
                PlayerPrefs.Save();
                Debug.Log($"[Campaign] Unlocked {HighestUnlocked}");
            }
        }

        public static void QueueTravelLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            PlayerPrefs.SetString(TravelLogKey, line);
            PlayerPrefs.Save();
        }

        public static string ConsumeTravelLog()
        {
            if (!PlayerPrefs.HasKey(TravelLogKey)) return null;
            string line = PlayerPrefs.GetString(TravelLogKey, "");
            PlayerPrefs.DeleteKey(TravelLogKey);
            PlayerPrefs.Save();
            return string.IsNullOrEmpty(line) ? null : line;
        }

        public static void ResetCampaign()
        {
            HighestUnlocked = CelestialBodyId.Earth;
            PlayerPrefs.SetInt(MaxKey, (int)CelestialBodyId.Earth);
            PlayerPrefs.SetInt(FreshKey, 1);
            PlayerPrefs.DeleteKey(TravelLogKey);
            BodySeed.SetBody(CelestialBodyId.Earth);
            PlayerPrefs.Save();
        }

        /// <summary>Debug cheat (Shift+F10): unlock Earth → Luna → Mars.</summary>
        public static void DebugUnlockAll()
        {
            HighestUnlocked = CelestialBodyId.Mars;
            PlayerPrefs.SetInt(MaxKey, (int)CelestialBodyId.Mars);
            PlayerPrefs.SetInt(FreshKey, 1);
            PlayerPrefs.Save();
            Debug.Log("[Campaign] Debug unlocked all bodies through Mars.");
        }
    }
}
