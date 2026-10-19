using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Drop-in replacement for <see cref="PlayerPrefs"/> that keeps campaign progress, settings and
    /// unlocks in a file under <c>persistentDataPath/Profile</c> instead of the Windows registry.
    ///
    /// Why: Steam Cloud can only sync files, and the campaign (unlocked worlds, research, per-world
    /// campus, achievements) lived in the registry, so a cloud-restored save came back without the
    /// campaign it belonged to. Display settings that should not roam between PCs (resolution,
    /// fullscreen, quality, frame cap) stay machine-local in PlayerPrefs.
    ///
    /// Existing players lose nothing: a key missing from the file is read once from the old
    /// PlayerPrefs value and copied across. Deletes clear both, so a migrated key cannot come back.
    /// Writes are atomic with a <c>.bak</c>, matching <see cref="SaveSystem"/>.
    /// </summary>
    public static class GamePrefs
    {
        private const string FolderName = "Profile";
        private const string EditorFolderName = "Profile-Editor";
        private const string FileName = "prefs.json";
        private const int FileVersion = 1;

        /// <summary>Keys that describe this PC's display, not the player's progress.</summary>
        private static readonly HashSet<string> MachineKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            DemoSettings.QualityKey,
            DemoSettings.FullscreenKey,
            DemoSettings.ResolutionWKey,
            DemoSettings.ResolutionHKey,
            DemoSettings.FrameCapKey
        };

        private enum Kind : byte { Int = 0, Float = 1, String = 2 }

        private struct Entry
        {
            public Kind Kind;
            public int I;
            public float F;
            public string S;
        }

        [Serializable]
        private sealed class Row
        {
            public string k;
            public int t;
            public int i;
            public float f;
            public string s;
        }

        [Serializable]
        private sealed class FileData
        {
            public int version = FileVersion;
            public List<Row> rows = new List<Row>();
        }

        private static readonly Dictionary<string, Entry> Store = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static bool _loaded;
        private static bool _dirty;
        private static string _directoryOverride;

        /// <summary>
        /// Tests point this at a temp folder so they cannot touch a real profile. Changing it
        /// drops the in-memory copy; the next access loads from the new place.
        /// </summary>
        public static string DirectoryOverride
        {
            get => _directoryOverride;
            set
            {
                if (_directoryOverride == value) return;
                _directoryOverride = value;
                Store.Clear();
                _loaded = false;
                _dirty = false;
            }
        }

        public static string Directory =>
            !string.IsNullOrEmpty(_directoryOverride)
                ? _directoryOverride
                : Path.Combine(Application.persistentDataPath, Application.isEditor ? EditorFolderName : FolderName);

        public static string FilePath => Path.Combine(Directory, FileName);

        /// <summary>True when there are changes not yet written to disk.</summary>
        public static bool IsDirty => _dirty;

        public static bool IsMachineKey(string key) => key != null && MachineKeys.Contains(key);

        // ---- PlayerPrefs-compatible surface --------------------------------------------------

        public static int GetInt(string key, int defaultValue = 0)
        {
            if (IsMachineKey(key)) return PlayerPrefs.GetInt(key, defaultValue);
            if (TryGet(key, out Entry e))
                return e.Kind == Kind.Int ? e.I : defaultValue;
            if (!PlayerPrefs.HasKey(key)) return defaultValue;
            int legacy = PlayerPrefs.GetInt(key, defaultValue);
            Put(key, new Entry { Kind = Kind.Int, I = legacy });
            return legacy;
        }

        public static float GetFloat(string key, float defaultValue = 0f)
        {
            if (IsMachineKey(key)) return PlayerPrefs.GetFloat(key, defaultValue);
            if (TryGet(key, out Entry e))
                return e.Kind == Kind.Float ? e.F : defaultValue;
            if (!PlayerPrefs.HasKey(key)) return defaultValue;
            float legacy = PlayerPrefs.GetFloat(key, defaultValue);
            Put(key, new Entry { Kind = Kind.Float, F = legacy });
            return legacy;
        }

        public static string GetString(string key, string defaultValue = "")
        {
            if (IsMachineKey(key)) return PlayerPrefs.GetString(key, defaultValue);
            if (TryGet(key, out Entry e))
                return e.Kind == Kind.String ? e.S : defaultValue;
            if (!PlayerPrefs.HasKey(key)) return defaultValue;
            string legacy = PlayerPrefs.GetString(key, defaultValue);
            Put(key, new Entry { Kind = Kind.String, S = legacy });
            return legacy;
        }

        public static void SetInt(string key, int value)
        {
            if (IsMachineKey(key)) { PlayerPrefs.SetInt(key, value); return; }
            Put(key, new Entry { Kind = Kind.Int, I = value });
        }

        public static void SetFloat(string key, float value)
        {
            if (IsMachineKey(key)) { PlayerPrefs.SetFloat(key, value); return; }
            Put(key, new Entry { Kind = Kind.Float, F = value });
        }

        public static void SetString(string key, string value)
        {
            if (IsMachineKey(key)) { PlayerPrefs.SetString(key, value); return; }
            Put(key, new Entry { Kind = Kind.String, S = value ?? "" });
        }

        public static bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (IsMachineKey(key)) return PlayerPrefs.HasKey(key);
            return TryGet(key, out _) || PlayerPrefs.HasKey(key);
        }

        public static void DeleteKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            Load();
            if (Store.Remove(key)) _dirty = true;
            // Also clear the legacy copy, or the next read would migrate it straight back.
            PlayerPrefs.DeleteKey(key);
        }

        public static void DeleteAll()
        {
            Load();
            Store.Clear();
            _dirty = true;
            PlayerPrefs.DeleteAll();
        }

        /// <summary>Write pending changes. Safe to call often; does nothing when clean.</summary>
        public static void Save()
        {
            PlayerPrefs.Save();
            if (!_loaded || !_dirty) return;
            if (Write()) _dirty = false;
        }

        /// <summary>Drop the in-memory copy so the next access re-reads the file (after a cloud sync).</summary>
        public static void Reload()
        {
            Store.Clear();
            _loaded = false;
            _dirty = false;
        }

        // ---- storage --------------------------------------------------------------------------

        private static bool TryGet(string key, out Entry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(key)) return false;
            Load();
            return Store.TryGetValue(key, out entry);
        }

        private static void Put(string key, Entry entry)
        {
            if (string.IsNullOrEmpty(key)) return;
            Load();
            if (Store.TryGetValue(key, out Entry old) && Same(old, entry)) return;
            Store[key] = entry;
            _dirty = true;
        }

        private static bool Same(Entry a, Entry b)
        {
            if (a.Kind != b.Kind) return false;
            switch (a.Kind)
            {
                case Kind.Int: return a.I == b.I;
                case Kind.Float: return a.F.Equals(b.F);
                default: return string.Equals(a.S, b.S, StringComparison.Ordinal);
            }
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            Store.Clear();
            _dirty = false;

            string path = FilePath;
            if (TryReadFile(path, out FileData data) || TryReadFile(path + ".bak", out data))
            {
                for (int i = 0; i < data.rows.Count; i++)
                {
                    Row r = data.rows[i];
                    if (r == null || string.IsNullOrEmpty(r.k)) continue;
                    Store[r.k] = new Entry { Kind = (Kind)Mathf.Clamp(r.t, 0, 2), I = r.i, F = r.f, S = r.s ?? "" };
                }
            }
        }

        private static bool TryReadFile(string path, out FileData data)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) return false;
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return false;
                data = JsonUtility.FromJson<FileData>(json);
                if (data == null || data.rows == null) return false;
                if (data.version > FileVersion)
                    Debug.LogWarning($"[GamePrefs] {path} was written by a newer build (v{data.version}); reading what this build understands.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GamePrefs] Could not read {path}: {e.Message}");
                data = null;
                return false;
            }
        }

        private static bool Write()
        {
            string path = FilePath;
            string temp = path + ".tmp";
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                var data = new FileData();
                var keys = new List<string>(Store.Keys);
                keys.Sort(StringComparer.Ordinal); // stable order keeps cloud diffs and reviews readable
                for (int i = 0; i < keys.Count; i++)
                {
                    Entry e = Store[keys[i]];
                    data.rows.Add(new Row { k = keys[i], t = (int)e.Kind, i = e.I, f = e.F, s = e.Kind == Kind.String ? e.S : null });
                }

                File.WriteAllText(temp, JsonUtility.ToJson(data, prettyPrint: true));
                if (File.Exists(path))
                    File.Replace(temp, path, path + ".bak");
                else
                    File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GamePrefs] Could not write {path}: {e.Message}");
                try { if (File.Exists(temp)) File.Delete(temp); } catch { /* best effort */ }
                return false;
            }
        }

        // ---- flushing in players ------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain reload may be off in the editor; never carry a stale copy into play mode.
            _directoryOverride = null;
            Reload();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallFlusher()
        {
            Application.quitting -= Save;
            Application.quitting += Save;
            var go = new GameObject("[GamePrefs]") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<GamePrefsFlusher>();
        }
    }

    /// <summary>
    /// Writes pending <see cref="GamePrefs"/> changes a moment after they happen, so a crash
    /// loses at most a second of settings even where code forgot to call Save.
    /// </summary>
    internal sealed class GamePrefsFlusher : MonoBehaviour
    {
        private const float Interval = 1f;
        private float _next;

        private void Update()
        {
            if (!GamePrefs.IsDirty) return;
            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + Interval;
            GamePrefs.Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) GamePrefs.Save();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) GamePrefs.Save();
        }
    }
}
