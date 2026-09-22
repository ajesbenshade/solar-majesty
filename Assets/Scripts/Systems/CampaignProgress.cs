using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Campaign spine is Earth → Luna → Mars → Belt → Europa.
    /// Title orrery can still drop onto an unlocked world.
    /// </summary>
    public static class CampaignProgress
    {
        private const string MaxKey = "SM_CampaignMaxBody";
        private const string FreshKey = "SM_CampaignInitialized";
        private const string TravelLogKey = "SM_PendingTravelLog";
        private const string CutsKey = "SM_NarrativeCutsShown";
        private const string LunaHourKey = "SM_LunaHourCleared";

        public static CelestialBodyId HighestUnlocked { get; private set; } = CelestialBodyId.Earth;

        public static bool IsOnSpine(CelestialBodyId id) =>
            (int)id >= (int)CelestialBodyId.Earth && (int)id <= (int)CelestialBodyCatalog.Last;

        public static bool IsParked(CelestialBodyId id) => !IsOnSpine(id);

        /// <summary>Survives New Game. Returning players skip the Luna hour.</summary>
        public static bool HasClearedLunaHour =>
            PlayerPrefs.GetInt(LunaHourKey, 0) == 1;

        public static CelestialBodyId NewGameBody =>
            CelestialBodyId.Earth;

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
            HighestUnlocked = MigrateHighest(HighestUnlocked);
            int last = (int)CelestialBodyCatalog.Last;
            if ((int)HighestUnlocked < 0 || (int)HighestUnlocked > last)
                HighestUnlocked = CelestialBodyId.Earth;
            PlayerPrefs.SetInt(MaxKey, (int)HighestUnlocked);
        }

        /// <summary>Preserve existing five-world unlocks.</summary>
        public static CelestialBodyId MigrateHighest(CelestialBodyId stored) => stored;

        public static bool IsUnlocked(CelestialBodyId id)
        {
            if (IsParked(id))
                return false;
            return SpineRank(id) <= SpineRank(HighestUnlocked);
        }

        public static int SpineRank(CelestialBodyId id) => IsOnSpine(id) ? (int)id : -1;

        public static CelestialBodyId? NextAfter(CelestialBodyId current)
        {
            if (!IsOnSpine(current) || current == CelestialBodyCatalog.Last) return null;
            return (CelestialBodyId)((int)current + 1);
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
            if (conquered == CelestialBodyId.Luna)
                NoteLunaHourCleared();

            var next = NextAfter(conquered);
            if (!next.HasValue) return;
            if (SpineRank(next.Value) > SpineRank(HighestUnlocked))
            {
                HighestUnlocked = next.Value;
                PlayerPrefs.SetInt(MaxKey, (int)HighestUnlocked);
                PlayerPrefs.Save();
                Debug.Log($"[Campaign] Unlocked {HighestUnlocked}");
            }
        }

        public static void NoteLunaHourCleared()
        {
            PlayerPrefs.SetInt(LunaHourKey, 1);
            PlayerPrefs.Save();
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

        /// <summary>Wipe campaign unlocks and cuts. Does not clear the Luna-hour veteran flag.</summary>
        public static void ResetCampaign()
        {
            HighestUnlocked = NewGameBody;
            PlayerPrefs.SetInt(MaxKey, (int)HighestUnlocked);
            PlayerPrefs.SetInt(FreshKey, 1);
            PlayerPrefs.DeleteKey(TravelLogKey);
            PlayerPrefs.DeleteKey(CutsKey);
            ResearchManager.WipeUnlocks();
            BodySeed.SetBody(HighestUnlocked);
            PlayerPrefs.Save();
        }

        /// <summary>New Game drop. Mars notes the Luna hour so the next New Game stays on Mars.</summary>
        public static void BeginNewGame(CelestialBodyId drop)
        {
            if (drop == CelestialBodyId.Mars)
                NoteLunaHourCleared();

            HighestUnlocked = IsOnSpine(drop) ? drop : NewGameBody;
            PlayerPrefs.SetInt(MaxKey, (int)HighestUnlocked);
            PlayerPrefs.SetInt(FreshKey, 1);
            PlayerPrefs.DeleteKey(TravelLogKey);
            PlayerPrefs.DeleteKey(CutsKey);
            ResearchManager.WipeUnlocks();
            BodySeed.SetBody(drop);
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

        /// <summary>Debug cheat (Shift+F10): unlock the full catalog including parked bodies.</summary>
        public static void DebugUnlockAll()
        {
            HighestUnlocked = CelestialBodyCatalog.Last;
            PlayerPrefs.SetInt(MaxKey, (int)HighestUnlocked);
            PlayerPrefs.SetInt(FreshKey, 1);
            PlayerPrefs.Save();
            Debug.Log($"[Campaign] Debug unlocked all bodies through {HighestUnlocked}.");
        }

        private static bool DebugUnlockedAll =>
            HighestUnlocked == CelestialBodyId.Belt || HighestUnlocked == CelestialBodyId.Europa;

        public static string DropButtonLabel(CelestialBodyId drop) => $"NEW GAME  ·  {drop} drop";

        public static string DropConfirmLabel(CelestialBodyId drop) => $"WIPE AND DROP {drop.ToString().ToUpperInvariant()}";

        public static string DropConfirmDetail(CelestialBodyId drop) =>
            $"This wipes the continue slot and campaign unlocks, then drops you on {drop}.";

        /// <summary>EditMode helper. Clears campaign prefs including the veteran Luna-hour flag.</summary>
        public static void ResetAllForTests()
        {
            HighestUnlocked = CelestialBodyId.Earth;
            PlayerPrefs.DeleteKey(MaxKey);
            PlayerPrefs.DeleteKey(FreshKey);
            PlayerPrefs.DeleteKey(TravelLogKey);
            PlayerPrefs.DeleteKey(CutsKey);
            PlayerPrefs.DeleteKey(LunaHourKey);
            PlayerPrefs.DeleteKey("SM_CelestialBody");
            PlayerPrefs.Save();
        }
    }
}
