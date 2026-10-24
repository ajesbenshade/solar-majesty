using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Campaign spine is Earth → Luna → Mars → Belt → Europa.
    /// Title orrery can still drop onto an unlocked world.
    /// </summary>
    public static class CampaignProgress
    {
        public const string MaxKey = "SM_CampaignMaxBody";
        private const string FreshKey = "SM_CampaignInitialized";
        private const string TravelLogKey = "SM_PendingTravelLog";
        private const string CutsKey = "SM_NarrativeCutsShown";
        private const string LunaHourKey = "SM_LunaHourCleared";
        public const string ConqueredKey = "SM_CampaignConquered";

        public static CelestialBodyId HighestUnlocked { get; private set; } = CelestialBodyId.Earth;

        public static bool IsOnSpine(CelestialBodyId id) =>
            (int)id >= (int)CelestialBodyId.Earth && (int)id <= (int)CelestialBodyCatalog.Last;

        public static bool IsParked(CelestialBodyId id) => !IsOnSpine(id);

        /// <summary>Survives New Game. Returning players skip the Luna hour.</summary>
        public static bool HasClearedLunaHour =>
            GamePrefs.GetInt(LunaHourKey, 0) == 1;

        /// <summary>Bit per world secured in this campaign. Cleared by a new campaign.</summary>
        public static int ConqueredMask => GamePrefs.GetInt(ConqueredKey, 0);

        public static bool HasConquered(CelestialBodyId id) => (ConqueredMask & (1 << (int)id)) != 0;

        /// <summary>Every world on the spine secured in this campaign (the Solar Conquest achievement).</summary>
        public static bool ConqueredEveryWorld
        {
            get
            {
                var all = CelestialBodyCatalog.All;
                if (all == null || all.Length == 0) return false;
                for (int i = 0; i < all.Length; i++)
                {
                    if (IsOnSpine(all[i]) && !HasConquered(all[i])) return false;
                }
                return true;
            }
        }

        public static void MarkConquered(CelestialBodyId id)
        {
            int mask = ConqueredMask | (1 << (int)id);
            if (mask == ConqueredMask) return;
            GamePrefs.SetInt(ConqueredKey, mask);
            GamePrefs.Save();
        }

        public static CelestialBodyId NewGameBody =>
            CelestialBodyId.Earth;

        /// <summary>Match <see cref="HighestUnlocked"/> to prefs without writing them.</summary>
        public static void ReloadFromPrefs()
        {
            if (!GamePrefs.HasKey(FreshKey))
            {
                HighestUnlocked = CelestialBodyId.Earth;
                return;
            }

            HighestUnlocked = (CelestialBodyId)GamePrefs.GetInt(MaxKey, (int)CelestialBodyId.Earth);
            HighestUnlocked = MigrateHighest(HighestUnlocked);
        }

        public static void Ensure()
        {
            if (!GamePrefs.HasKey(FreshKey))
            {
                HighestUnlocked = CelestialBodyId.Earth;
                GamePrefs.SetInt(MaxKey, (int)CelestialBodyId.Earth);
                GamePrefs.SetInt(FreshKey, 1);
                BodySeed.SetBody(CelestialBodyId.Earth);
                GamePrefs.Save();
                return;
            }

            HighestUnlocked = (CelestialBodyId)GamePrefs.GetInt(MaxKey, (int)CelestialBodyId.Earth);
            HighestUnlocked = MigrateHighest(HighestUnlocked);
            int last = (int)CelestialBodyCatalog.Last;
            if ((int)HighestUnlocked < 0 || (int)HighestUnlocked > last)
                HighestUnlocked = CelestialBodyId.Earth;
            GamePrefs.SetInt(MaxKey, (int)HighestUnlocked);
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
            GamePrefs.SetInt(MaxKey, next);
            GamePrefs.SetInt(FreshKey, 1);
            GamePrefs.Save();
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
                GamePrefs.SetInt(MaxKey, (int)HighestUnlocked);
                GamePrefs.Save();
                Debug.Log($"[Campaign] Unlocked {HighestUnlocked}");
            }
        }

        public static void NoteLunaHourCleared()
        {
            GamePrefs.SetInt(LunaHourKey, 1);
            GamePrefs.Save();
        }

        public static void QueueTravelLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            GamePrefs.SetString(TravelLogKey, line);
            GamePrefs.Save();
        }

        public static string ConsumeTravelLog()
        {
            if (!GamePrefs.HasKey(TravelLogKey)) return null;
            string line = GamePrefs.GetString(TravelLogKey, "");
            GamePrefs.DeleteKey(TravelLogKey);
            GamePrefs.Save();
            return string.IsNullOrEmpty(line) ? null : line;
        }

        /// <summary>Wipe campaign unlocks and cuts. Does not clear the Luna-hour veteran flag.</summary>
        public static void ResetCampaign()
        {
            HighestUnlocked = NewGameBody;
            GamePrefs.SetInt(MaxKey, (int)HighestUnlocked);
            GamePrefs.SetInt(FreshKey, 1);
            GamePrefs.DeleteKey(TravelLogKey);
            GamePrefs.DeleteKey(CutsKey);
            GamePrefs.DeleteKey(ConqueredKey);
            ResearchManager.WipeUnlocks();
            BodySeed.SetBody(HighestUnlocked);
            GamePrefs.Save();
        }

        /// <summary>New Game drop. Mars notes the Luna hour so the next New Game stays on Mars.</summary>
        public static void BeginNewGame(CelestialBodyId drop)
        {
            if (drop == CelestialBodyId.Mars)
                NoteLunaHourCleared();

            HighestUnlocked = IsOnSpine(drop) ? drop : NewGameBody;
            GamePrefs.SetInt(MaxKey, (int)HighestUnlocked);
            GamePrefs.SetInt(FreshKey, 1);
            GamePrefs.DeleteKey(TravelLogKey);
            GamePrefs.DeleteKey(CutsKey);
            GamePrefs.DeleteKey(ConqueredKey);
            ResearchManager.WipeUnlocks();
            BodySeed.SetBody(drop);
            GamePrefs.Save();
        }

        public static bool WasCutShown(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            string raw = GamePrefs.GetString(CutsKey, "");
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
            string raw = GamePrefs.GetString(CutsKey, "");
            raw = string.IsNullOrEmpty(raw) ? id : raw + "|" + id;
            GamePrefs.SetString(CutsKey, raw);
            GamePrefs.Save();
        }

        /// <summary>Debug cheat (Shift+F10): unlock the full catalog including parked bodies.</summary>
        public static void DebugUnlockAll()
        {
            HighestUnlocked = CelestialBodyCatalog.Last;
            GamePrefs.SetInt(MaxKey, (int)HighestUnlocked);
            GamePrefs.SetInt(FreshKey, 1);
            GamePrefs.Save();
            Debug.Log($"[Campaign] Debug unlocked all bodies through {HighestUnlocked}.");
        }

        private static bool DebugUnlockedAll =>
            HighestUnlocked == CelestialBodyId.Belt || HighestUnlocked == CelestialBodyId.Europa;

        public static string DropButtonLabel(CelestialBodyId drop) => $"NEW GAME  ·  {drop} drop";

        public static string DropConfirmLabel(CelestialBodyId drop) => $"START  ·  {drop.ToString().ToUpperInvariant()}";

        public static string DropConfirmDetail(CelestialBodyId drop) =>
            $"Starts a new campaign on {drop}. Pick a slot — only that slot is replaced. Other saves stay.";

        /// <summary>EditMode helper. Clears campaign prefs including the veteran Luna-hour flag.</summary>
        public static void ResetAllForTests()
        {
            HighestUnlocked = CelestialBodyId.Earth;
            GamePrefs.DeleteKey(MaxKey);
            GamePrefs.DeleteKey(FreshKey);
            GamePrefs.DeleteKey(TravelLogKey);
            GamePrefs.DeleteKey(CutsKey);
            GamePrefs.DeleteKey(LunaHourKey);
            GamePrefs.DeleteKey("SM_CelestialBody");
            GamePrefs.Save();
        }
    }
}
