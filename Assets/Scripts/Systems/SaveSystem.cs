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

        public static bool Exists(int slot) => File.Exists(SlotPath(slot)) || File.Exists(SlotPath(slot) + ".bak");

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
            return WritePath(SlotPath(slot), save);
        }

        public static string WorldPath(CelestialBodyId body) =>
            Path.Combine(SaveDirectory, $"world{(int)body}.json");

        public static bool WriteWorld(SaveGame save) =>
            save != null && WritePath(WorldPath((CelestialBodyId)save.body), save);

        public static bool TryReadWorld(CelestialBodyId body, out SaveGame save) =>
            TryReadPath(WorldPath(body), out save) && save.body == (int)body;

        public static void DeleteWorld(CelestialBodyId body) => DeleteSnapshot(WorldPath(body));

        public static bool IsNewerVersion(int slot) =>
            IsNewerFile(SlotPath(slot)) || IsNewerFile(SlotPath(slot) + ".bak");

        private static bool IsNewerFile(string path)
        {
            TryReadFile(path, out _, out bool newer);
            return newer;
        }

        private static bool WritePath(string path, SaveGame save)
        {
            if (save == null) return false;
            if (IsNewerFile(path) || IsNewerFile(path + ".bak"))
            {
                Debug.LogWarning($"[SaveSystem] Refusing to overwrite a newer-build save at {path}.");
                return false;
            }

            save.version = SaveGame.CurrentVersion;
            save.gameVersion = Application.version;
            save.buildGuid = Application.buildGUID;
            save.savedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

            string temp = path + ".tmp";

            try
            {
                Directory.CreateDirectory(SaveDirectory);
                File.WriteAllText(temp, JsonUtility.ToJson(save, prettyPrint: true));

                // Keep the previous complete snapshot while atomically replacing the current one.
                if (File.Exists(path))
                    File.Replace(temp, path, path + ".bak");
                else
                    File.Move(temp, path);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Write to {path} failed: {e.Message}");
                TryDelete(temp);
                return false;
            }
        }

        public static bool TryRead(int slot, out SaveGame save)
        {
            return TryReadPath(SlotPath(slot), out save);
        }

        private static bool TryReadPath(string path, out SaveGame save)
        {
            if (TryReadFile(path, out save, out bool newer)) return true;
            if (newer) return false; // Do not roll a newer-build save back behind the player's back.
            if (TryReadFile(path + ".bak", out save, out _))
            {
                Debug.LogWarning($"[SaveSystem] Recovered previous snapshot for {path}.");
                return true;
            }
            return false;
        }

        private static bool TryReadFile(string path, out SaveGame save, out bool newer)
        {
            save = null;
            newer = false;
            if (!File.Exists(path)) return false;

            try
            {
                string json = File.ReadAllText(path);
                // The stabilization branch used an encoded string; main used a structured array.
                // Preserve both v3/v4 formats without asking JsonUtility to parse the wrong type.
                json = System.Text.RegularExpressions.Regex.Replace(json,
                    @"""roster""\s*:\s*(?="")", "\"rosterBlob\":");
                var loaded = JsonUtility.FromJson<SaveGame>(json);
                if (loaded == null)
                {
                    Debug.LogWarning($"[SaveSystem] Save {path} did not parse.");
                    return false;
                }

                newer = loaded.version > SaveGame.CurrentVersion;
                if (!TryMigrate(loaded, path))
                    return false;

                save = loaded;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Read of {path} failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Bring an older save forward. A save from a newer build is refused rather than guessed at —
        /// loading it with missing fields would silently corrupt a player's colony.
        /// </summary>
        private static bool TryMigrate(SaveGame save, string slot)
        {
            if (save.version > SaveGame.CurrentVersion)
            {
                Debug.LogWarning(
                    $"[SaveSystem] Slot {slot} was written by a newer build " +
                    $"(v{save.version} > v{SaveGame.CurrentVersion}); refusing to load it.");
                return false;
            }

            if (save.version < 1 || save.seed == 0 || !Enum.IsDefined(typeof(CelestialBodyId), save.body) ||
                double.IsNaN(save.playSeconds) || double.IsInfinity(save.playSeconds))
            {
                Debug.LogWarning($"[SaveSystem] Invalid world header in {slot}; refusing to restore it.");
                return false;
            }

            bool legacyFaunaOwnership = save.version < 4;
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
            save.parties ??= new List<SaveParty>();
            save.roster ??= new List<SaveRosterEntry>();
            save.research.unlocked ??= new List<int>();
            save.research.progress ??= new List<SaveResearchProgress>();
            if (save.buildings.Exists(b => b == null || !Enum.IsDefined(typeof(BuildingCategory), b.category) || b.w < 1 || b.h < 1) ||
                save.flags.Exists(f => f == null || !Enum.IsDefined(typeof(FlagType), f.flagType)) ||
                save.agents.Exists(a => a == null || !Enum.IsDefined(typeof(SpecialistClass), a.specialistClass)) ||
                save.fauna.Exists(f => f == null || !Enum.IsDefined(typeof(FaunaKind), f.kind)) ||
                save.nodes.Exists(n => n == null) || save.lairs.Exists(l => l == null) || save.parties.Exists(p => p == null))
            {
                Debug.LogWarning($"[SaveSystem] Invalid entity record in {slot}; refusing to restore it.");
                return false;
            }
            if (legacyFaunaOwnership)
            {
                // Old files have no ownership IDs. Approximate only those files using the nearest
                // uncleared den; current snapshots preserve exact owners, including roaming fauna.
                foreach (var fauna in save.fauna)
                {
                    fauna.lairIndex = -1;
                    if (fauna.kind != (int)FaunaKind.Stalker) continue;
                    float distance = float.PositiveInfinity;
                    for (int i = 0; i < save.lairs.Count; i++)
                    {
                        var lair = save.lairs[i];
                        if (lair.cleared) continue;
                        float dx = fauna.px - lair.px, dz = fauna.pz - lair.pz;
                        float squared = dx * dx + dz * dz;
                        if (squared >= distance) continue;
                        distance = squared;
                        fauna.lairIndex = i;
                    }
                }
            }
            foreach (var party in save.parties)
            {
                party.memberIndices ??= new List<int>();
                party.memberClasses ??= new List<int>();
            }
            return true;
        }

        public static void Delete(int slot) => DeleteSnapshot(SlotPath(slot));

        private static void DeleteSnapshot(string path)
        {
            TryDelete(path);
            TryDelete(path + ".bak");
            TryDelete(path + ".tmp");
        }

        public static void DeleteAll()
        {
            for (int i = 0; i < SlotCount; i++)
                Delete(i);
            foreach (CelestialBodyId body in Enum.GetValues(typeof(CelestialBodyId)))
                DeleteWorld(body);
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
