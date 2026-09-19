using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Campaign unlock spine: Earth → Luna → Mars → Belt → Europa.
    /// Persists highest unlocked body; free body-hopping is limited to unlocked worlds.
    /// </summary>
    public static class CampaignProgress
    {
        private const string MaxKey = "SM_CampaignMaxBody";
        private const string FreshKey = "SM_CampaignInitialized";
        private const string TravelLogKey = "SM_PendingTravelLog";
        private const string CutsKey = "SM_NarrativeCutsShown";

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
            int last = (int)CelestialBodyCatalog.Last;
            if ((int)HighestUnlocked < (int)CelestialBodyId.Earth || (int)HighestUnlocked > last)
                HighestUnlocked = CelestialBodyId.Earth;
        }

        public static bool IsUnlocked(CelestialBodyId id) =>
            (int)id <= (int)HighestUnlocked;

        public static CelestialBodyId? NextAfter(CelestialBodyId current)
        {
            int n = (int)current + 1;
            if (n > (int)CelestialBodyCatalog.Last) return null;
            return (CelestialBodyId)n;
        }

        /// <summary>
        /// Raise the campaign spine through <paramref name="id"/> (inclusive). Used when a
        /// fresh title click starts on an outer world so Earth…id are all playable.
        /// </summary>
        public static void UnlockThrough(CelestialBodyId id)
        {
            if ((int)id <= (int)HighestUnlocked) return;
            int last = (int)CelestialBodyCatalog.Last;
            int next = Mathf.Clamp((int)id, (int)CelestialBodyId.Earth, last);
            HighestUnlocked = (CelestialBodyId)next;
            PlayerPrefs.SetInt(MaxKey, next);
            PlayerPrefs.SetInt(FreshKey, 1);
            PlayerPrefs.Save();
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
            PlayerPrefs.DeleteKey(CutsKey);
            ResearchManager.WipeUnlocks();
            BodySeed.SetBody(CelestialBodyId.Earth);
            PlayerPrefs.Save();
        }

        public static bool WasCutShown(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            string raw = PlayerPrefs.GetString(CutsKey, "");
            if (string.IsNullOrEmpty(raw)) return false;
            string[] parts = raw.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == id) return true;
            }
            return false;
        }

        public static void NoteCutShown(string id)
        {
            if (string.IsNullOrEmpty(id) || WasCutShown(id)) return;
            string raw = PlayerPrefs.GetString(CutsKey, "");
            raw = string.IsNullOrEmpty(raw) ? id : raw + "|" + id;
            PlayerPrefs.SetString(CutsKey, raw);
            PlayerPrefs.Save();
        }

        /// <summary>Debug cheat (Shift+F10): unlock the full campaign spine.</summary>
        public static void DebugUnlockAll()
        {
            HighestUnlocked = CelestialBodyCatalog.Last;
            PlayerPrefs.SetInt(MaxKey, (int)HighestUnlocked);
            PlayerPrefs.SetInt(FreshKey, 1);
            PlayerPrefs.Save();
            Debug.Log($"[Campaign] Debug unlocked all bodies through {HighestUnlocked}.");
        }
    }
}
