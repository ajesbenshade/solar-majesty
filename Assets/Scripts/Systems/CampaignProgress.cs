using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Campaign spine is Luna (skipable first hour) → Mars (the colony).
    /// Earth, Belt, and Europa stay in the catalog for debug / later expansion — parked, not deleted.
    /// Title orrery can still drop onto an unlocked world.
    /// </summary>
    public static class CampaignProgress
    {
        private const string MaxKey = "SM_CampaignMaxBody";
        private const string FreshKey = "SM_CampaignInitialized";
        private const string TravelLogKey = "SM_PendingTravelLog";
        private const string CutsKey = "SM_NarrativeCutsShown";
        private const string LunaHourKey = "SM_LunaHourCleared";

        public static CelestialBodyId HighestUnlocked { get; private set; } = CelestialBodyId.Luna;

        public static bool IsOnSpine(CelestialBodyId id) =>
            id == CelestialBodyId.Luna || id == CelestialBodyId.Mars;

        public static bool IsParked(CelestialBodyId id) => !IsOnSpine(id);

        /// <summary>Survives New Game. Returning players skip the Luna hour.</summary>
        public static bool HasClearedLunaHour =>
            PlayerPrefs.GetInt(LunaHourKey, 0) == 1;

        public static CelestialBodyId NewGameBody =>
            HasClearedLunaHour ? CelestialBodyId.Mars : CelestialBodyId.Luna;

        public static void Ensure()
        {
            if (!PlayerPrefs.HasKey(FreshKey))
            {
                HighestUnlocked = CelestialBodyId.Luna;
                PlayerPrefs.SetInt(MaxKey, (int)CelestialBodyId.Luna);
                PlayerPrefs.SetInt(FreshKey, 1);
                BodySeed.SetBody(CelestialBodyId.Luna);
                PlayerPrefs.Save();
                return;
            }

            HighestUnlocked = (CelestialBodyId)PlayerPrefs.GetInt(MaxKey, (int)CelestialBodyId.Luna);
            HighestUnlocked = MigrateHighest(HighestUnlocked);
            int last = (int)CelestialBodyCatalog.Last;
            if ((int)HighestUnlocked < 0 || (int)HighestUnlocked > last)
                HighestUnlocked = CelestialBodyId.Luna;
            PlayerPrefs.SetInt(MaxKey, (int)HighestUnlocked);
        }

        /// <summary>
        /// Old first-run prefs stored Earth as the default unlock. That picnic is parked.
        /// Debug-all (Belt/Europa) is left alone.
        /// </summary>
        public static CelestialBodyId MigrateHighest(CelestialBodyId stored)
        {
            if (stored == CelestialBodyId.Earth)
                return CelestialBodyId.Luna;
            return stored;
        }

        public static bool IsUnlocked(CelestialBodyId id)
        {
            if (DebugUnlockedAll)
                return true;
            if (IsParked(id))
                return false;
            return SpineRank(id) <= SpineRank(HighestUnlocked);
        }

        public static int SpineRank(CelestialBodyId id)
        {
            switch (id)
            {
                case CelestialBodyId.Luna: return 0;
                case CelestialBodyId.Mars: return 1;
                default: return -1;
            }
        }

        public static CelestialBodyId? NextAfter(CelestialBodyId current)
        {
            if (current == CelestialBodyId.Earth || current == CelestialBodyId.Luna)
                return CelestialBodyId.Mars;
            return null;
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
            if (conquered == CelestialBodyId.Luna || conquered == CelestialBodyId.Earth)
                NoteLunaHourCleared();

            var next = NextAfter(conquered);
            if (!next.HasValue) return;
            if (DebugUnlockedAll) return;
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

        public static string DropButtonLabel(CelestialBodyId drop) =>
            drop == CelestialBodyId.Mars ? "NEW GAME  ·  Mars drop" : "NEW GAME  ·  Luna drop";

        public static string DropConfirmLabel(CelestialBodyId drop) =>
            drop == CelestialBodyId.Mars ? "WIPE AND DROP MARS" : "WIPE AND DROP LUNA";

        public static string DropConfirmDetail(CelestialBodyId drop) =>
            drop == CelestialBodyId.Mars
                ? "This wipes the continue slot and campaign unlocks, then drops you on Mars. Earth picnic is parked."
                : "This wipes the continue slot and campaign unlocks, then drops you on Luna. Earth picnic is parked.";

        /// <summary>EditMode helper. Clears campaign prefs including the veteran Luna-hour flag.</summary>
        public static void ResetAllForTests()
        {
            HighestUnlocked = CelestialBodyId.Luna;
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
