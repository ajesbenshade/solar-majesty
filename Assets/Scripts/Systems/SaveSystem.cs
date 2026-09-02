using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Slot-based save file storage. Writes are atomic (temp file then replace) so a crash or a
    /// pulled power cable during an autosave cannot leave a half-written slot that fails to load.
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>Slot 0 is the rotating autosave; 1..3 are player slots.</summary>
        public const int AutosaveSlot = 0;
        public const int PlayerSlotCount = 3;
        public const int SlotCount = PlayerSlotCount + 1;

        private const string FolderName = "Saves";
        private const string Extension = ".json";

        public static string SaveDirectory =>
            Path.Combine(Application.persistentDataPath, FolderName);

        public static string SlotPath(int slot) =>
            Path.Combine(SaveDirectory, $"slot{Mathf.Clamp(slot, 0, SlotCount - 1)}{Extension}");

        public static bool Exists(int slot) => File.Exists(SlotPath(slot));

        public static bool AnyExists()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (Exists(i)) return true;
            }
            return false;
        }

        public static bool Write(int slot, SaveGame save)
        {
            if (save == null) return false;

            save.version = SaveGame.CurrentVersion;
            save.gameVersion = Application.version;
            save.savedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

            string path = SlotPath(slot);
            string temp = path + ".tmp";

            try
            {
                Directory.CreateDirectory(SaveDirectory);
                File.WriteAllText(temp, JsonUtility.ToJson(save, prettyPrint: true));

                // Replace is atomic where the platform supports it; Delete+Move is the fallback.
                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Write to slot {slot} failed: {e.Message}");
                TryDelete(temp);
                return false;
            }
        }

        public static bool TryRead(int slot, out SaveGame save)
        {
            save = null;
            string path = SlotPath(slot);
            if (!File.Exists(path)) return false;

            try
            {
                var loaded = JsonUtility.FromJson<SaveGame>(File.ReadAllText(path));
                if (loaded == null)
                {
                    Debug.LogWarning($"[SaveSystem] Slot {slot} did not parse.");
                    return false;
                }

                if (!TryMigrate(loaded, slot))
                    return false;

                save = loaded;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Read of slot {slot} failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Bring an older save forward. A save from a newer build is refused rather than guessed at —
        /// loading it with missing fields would silently corrupt a player's colony.
        /// </summary>
        private static bool TryMigrate(SaveGame save, int slot)
        {
            if (save.version > SaveGame.CurrentVersion)
            {
                Debug.LogWarning(
                    $"[SaveSystem] Slot {slot} was written by a newer build " +
                    $"(v{save.version} > v{SaveGame.CurrentVersion}); refusing to load it.");
                return false;
            }

            if (save.version < SaveGame.CurrentVersion)
            {
                Debug.Log($"[SaveSystem] Migrating slot {slot} from v{save.version} to v{SaveGame.CurrentVersion}.");
                save.version = SaveGame.CurrentVersion;
            }

            save.stockpile ??= new SaveStockpile();
            save.settlement ??= new SaveSettlementState();
            save.research ??= new SaveResearchState();
            save.mission ??= new SaveMissionState();
            save.replay ??= new SaveReplayState();
            save.buildings ??= new List<SaveBuilding>();
            save.flags ??= new List<SaveFlag>();
            save.agents ??= new List<SaveAgent>();
            save.fauna ??= new List<SaveFauna>();
            save.nodes ??= new List<SaveNode>();
            save.lairs ??= new List<SaveLair>();
            save.research.unlocked ??= new List<int>();
            return true;
        }

        public static void Delete(int slot) => TryDelete(SlotPath(slot));

        public static void DeleteAll()
        {
            for (int i = 0; i < SlotCount; i++)
                Delete(i);
        }

        /// <summary>Header info for every populated slot, for a load menu.</summary>
        public static List<SaveSlotInfo> ListSlots()
        {
            var list = new List<SaveSlotInfo>(SlotCount);
            for (int i = 0; i < SlotCount; i++)
            {
                if (!TryRead(i, out SaveGame save))
                {
                    list.Add(new SaveSlotInfo { Slot = i, Occupied = false });
                    continue;
                }

                list.Add(new SaveSlotInfo
                {
                    Slot = i,
                    Occupied = true,
                    Summary = save.Describe(),
                    SavedAtUtc = save.savedAtUtc,
                    Body = (CelestialBodyId)save.body,
                    PlaySeconds = save.playSeconds
                });
            }
            return list;
        }

        /// <summary>Most recently written slot, or -1 when nothing is saved.</summary>
        public static int MostRecentSlot()
        {
            int best = -1;
            string bestStamp = null;
            for (int i = 0; i < SlotCount; i++)
            {
                if (!TryRead(i, out SaveGame save)) continue;
                if (bestStamp == null || string.CompareOrdinal(save.savedAtUtc, bestStamp) > 0)
                {
                    bestStamp = save.savedAtUtc;
                    best = i;
                }
            }
            return best;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Could not delete {path}: {e.Message}");
            }
        }
    }

    public struct SaveSlotInfo
    {
        public int Slot;
        public bool Occupied;
        public string Summary;
        public string SavedAtUtc;
        public CelestialBodyId Body;
        public double PlaySeconds;

        public string Label => Slot == SaveSystem.AutosaveSlot ? "AUTO" : $"SLOT {Slot}";
    }
}
