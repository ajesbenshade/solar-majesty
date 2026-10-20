using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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

        /// <summary>"Saves", or "DemoSaves" in the Steam demo so demo colonies never share the game's slots.</summary>
        private static string FolderName => DemoBuild.SaveFolder(DemoBuild.CompiledAsDemo);
        private const string Extension = ".json";

        /// <summary>
        /// When set, saves are read and written here instead of
        /// <c>Application.persistentDataPath/Saves</c>. Tests point this at a temp folder so a
        /// crashed run cannot touch the player's real colony files. A player build sets it from
        /// <c>-saveDir</c> before the first load; with no argument it stays null.
        /// </summary>
        public static string DirectoryOverride
        {
            get => _directoryOverride;
            set
            {
                // Let queued autosaves land where they were aimed before anything moves or deletes it.
                Flush();
                _directoryOverride = value;
            }
        }

        private static string _directoryOverride;

        private static bool _loggedSaveDirectory;

        public static string SaveDirectory =>
            string.IsNullOrEmpty(DirectoryOverride)
                ? Path.Combine(Application.persistentDataPath, FolderName)
                : DirectoryOverride;

        /// <summary>
        /// Read <c>-saveDir &lt;path&gt;</c> or <c>-saveDir=&lt;path&gt;</c>. A missing or empty
        /// value does not count. Does not change <see cref="DirectoryOverride"/>.
        /// </summary>
        public static bool TryParseSaveDirArgument(string[] args, out string path)
        {
            path = null;
            if (args == null) return false;
            const string name = "-saveDir";
            const string prefix = "-saveDir=";
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.IsNullOrEmpty(arg)) continue;
                if (arg.Length > prefix.Length &&
                    arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string value = arg.Substring(prefix.Length).Trim();
                    if (value.Length == 0) continue;
                    path = value;
                    return true;
                }

                if (!string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) continue;
                if (i + 1 >= args.Length) return false;
                string next = args[i + 1];
                if (string.IsNullOrWhiteSpace(next) || next.StartsWith("-", StringComparison.Ordinal))
                    continue;
                path = next.Trim();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Point <see cref="DirectoryOverride"/> at <c>-saveDir</c> when that argument is present.
        /// Absent, empty, or flag-like values leave the current directory alone, including the
        /// null default that resolves to <c>persistentDataPath/Saves</c>.
        /// </summary>
        public static void ApplySaveDirArgument(string[] args)
        {
            if (TryParseSaveDirArgument(args, out string path))
                DirectoryOverride = path;
        }

        /// <summary>Log <see cref="SaveDirectory"/> the first time this process asks.</summary>
        public static void LogSaveDirectoryOnce()
        {
            if (_loggedSaveDirectory) return;
            _loggedSaveDirectory = true;
            Debug.Log("[SaveSystem] Save directory: " + SaveDirectory);
        }

        // Player builds only. Editor play mode and EditMode tests own DirectoryOverride;
        // the editor command line must not retarget the player's saves.
#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ApplyCommandLineSaveDirectory()
        {
            ApplySaveDirArgument(System.Environment.GetCommandLineArgs());
            LogSaveDirectoryOnce();
        }
#endif

        public static string SlotPath(int slot) =>
            Path.Combine(SaveDirectory, $"slot{Mathf.Clamp(slot, 0, SlotCount - 1)}{Extension}");

        public static bool Exists(int slot)
        {
            Flush();
            return File.Exists(SlotPath(slot)) || File.Exists(SlotPath(slot) + ".bak");
        }

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
            Flush();
            TryReadFile(path, out _, out bool newer);
            return newer;
        }

        // ---- background autosave writes --------------------------------------------------------

        // Files this session wrote, with the write time it left on them. Guarded by WriteGate (the
        // background writer records into it). A file whose time changed since is checked again.
        private static readonly Dictionary<string, DateTime> OwnPaths = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private static void MarkOwn(string path)
        {
            try
            {
                DateTime t = File.GetLastWriteTimeUtc(path);
                lock (WriteGate) OwnPaths[path] = t;
            }
            catch
            {
                lock (WriteGate) OwnPaths.Remove(path);
            }
        }

        private static bool IsOwnUnchanged(string path)
        {
            DateTime t;
            lock (WriteGate)
            {
                if (!OwnPaths.TryGetValue(path, out t)) return false;
            }
            try { return File.Exists(path) && File.GetLastWriteTimeUtc(path) == t; }
            catch { return false; }
        }
        private static readonly object WriteGate = new object();
        private static Task _writes = Task.CompletedTask;

        /// <summary>
        /// Autosave: serialise once on the main thread, then write every path on one background
        /// queue. The main-thread version cost ~35 ms and set off a large GC every 20 s, which the
        /// benchmark measured as a ~115 ms hitch. Every read, delete and listing waits for the queue,
        /// and quitting flushes it, so nothing ever sees a half-finished autosave.
        /// </summary>
        public static void WriteAutosave(SaveGame save, IList<string> paths)
        {
            if (save == null || paths == null || paths.Count == 0) return;

            var targets = new List<string>(paths.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                string p = paths[i];
                if (string.IsNullOrEmpty(p) || targets.Contains(p)) continue;
                // A file this session already wrote is this build's version: skip re-reading and
                // parsing it. Autosave used to parse each target and its .bak every 20 s.
                if (!IsOwnUnchanged(p) && (IsNewerFile(p) || IsNewerFile(p + ".bak")))
                {
                    Debug.LogWarning($"[SaveSystem] Refusing to overwrite a newer-build save at {p}.");
                    continue;
                }
                targets.Add(p);
            }
            if (targets.Count == 0) return;

            Stamp(save);
            string json = JsonUtility.ToJson(save, prettyPrint: true);
            string dir = SaveDirectory;

            lock (WriteGate)
            {
                _writes = _writes.ContinueWith(_ =>
                {
                    try { Directory.CreateDirectory(dir); }
                    catch (Exception e) { Debug.LogError($"[SaveSystem] Cannot create {dir}: {e.Message}"); return; }
                    for (int i = 0; i < targets.Count; i++)
                    {
                        if (WriteText(targets[i], json)) MarkOwn(targets[i]);
                    }
                }, TaskScheduler.Default);
            }
        }

        /// <summary>Wait for queued autosave writes. Cheap when nothing is pending.</summary>
        public static void Flush()
        {
            Task pending;
            lock (WriteGate) pending = _writes;
            if (pending.IsCompleted) return;
            try { pending.Wait(); }
            catch (Exception e) { Debug.LogError("[SaveSystem] Background save failed: " + e.Message); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookQuit()
        {
            lock (WriteGate) OwnPaths.Clear();
            Application.quitting -= Flush;
            Application.quitting += Flush;
        }

        private static void Stamp(SaveGame save)
        {
            save.version = SaveGame.CurrentVersion;
            save.gameVersion = Application.version;
            save.buildGuid = Application.buildGUID;
            save.savedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>Atomic write: temp file, then replace keeping the previous file as .bak.</summary>
        private static bool WriteText(string path, string json)
        {
            string temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, json);
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

        private static bool WritePath(string path, SaveGame save)
        {
            if (save == null) return false;
            if (IsNewerFile(path) || IsNewerFile(path + ".bak"))
            {
                Debug.LogWarning($"[SaveSystem] Refusing to overwrite a newer-build save at {path}.");
                return false;
            }

            Stamp(save);
            Flush(); // keep order with any queued autosave of the same file
            try
            {
                Directory.CreateDirectory(SaveDirectory);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Cannot create {SaveDirectory}: {e.Message}");
                return false;
            }
            bool ok = WriteText(path, JsonUtility.ToJson(save, prettyPrint: true));
            if (ok) MarkOwn(path);
            return ok;
        }

        public static bool TryRead(int slot, out SaveGame save)
        {
            return TryReadPath(SlotPath(slot), out save);
        }

        private static bool TryReadPath(string path, out SaveGame save)
        {
            Flush();
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
        /// <summary>v5 → v6: every stored gold amount moves onto the Majesty 2 scale.</summary>
        private static void MigrateGoldScale(SaveGame save)
        {
            save.stockpile.metals = MajestyEconomy.MigrateGold(save.stockpile.metals);
            foreach (var b in save.buildings)
                if (b != null) b.levyPurse = MajestyEconomy.MigrateGold(b.levyPurse);
            foreach (var f in save.flags)
            {
                if (f == null) continue;
                f.bounty = MajestyEconomy.MigrateGold(f.bounty);
                f.escrowMetals = MajestyEconomy.MigrateGold(f.escrowMetals);
            }
            foreach (var a in save.agents)
            {
                if (a == null) continue;
                a.credits = MajestyEconomy.MigrateGold(a.credits);
                a.levyCarry = MajestyEconomy.MigrateGold(a.levyCarry);
                if (a.hasVeteranRecord)
                    a.veteran.Credits = MajestyEconomy.MigrateGold(a.veteran.Credits);
            }
            foreach (var r in save.roster)
                if (r != null) r.credits = MajestyEconomy.MigrateGold(r.credits);
            if (!string.IsNullOrEmpty(save.rosterBlob))
            {
                var records = new List<SpecialistRecord>();
                if (SpecialistRoster.TryDecode(save.rosterBlob, records))
                {
                    for (int i = 0; i < records.Count; i++)
                    {
                        var rec = records[i];
                        rec.Credits = MajestyEconomy.MigrateGold(rec.Credits);
                        records[i] = rec;
                    }
                    save.rosterBlob = SpecialistRoster.Encode(records);
                }
            }
        }

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

            // JsonUtility creates inline objects even for absent fields. Use the schema
            // version to distinguish missing legacy state from a saved empty roster.
            if (save.version < 7)
            {
                save.villageGrowth = null;
                save.collectors = null;
            }
            bool legacyFaunaOwnership = save.version < 4;
            bool legacyGoldScale = save.version < MajestyEconomy.GoldScaleSaveVersion;
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
            if (legacyGoldScale)
                MigrateGoldScale(save);
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
            Flush();
            lock (WriteGate) OwnPaths.Remove(path);
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

        public string Label => Slot == SaveSystem.AutosaveSlot ? Loc.S("AUTO") : Loc.F("SLOT {0}", Slot);
    }
}
