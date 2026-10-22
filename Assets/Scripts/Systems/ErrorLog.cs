using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Keeps exceptions and errors from player sessions in <c>persistentDataPath/Logs</c>.
    ///
    /// Unity's Player.log is overwritten on every launch, so the session a player is reporting
    /// is usually gone by the time they write in. This file keeps the last few sessions, names
    /// the build, and de-duplicates repeats so a per-frame error cannot fill the disk.
    /// Local only: nothing is uploaded. The settings screen opens the folder for the player.
    /// </summary>
    public static class ErrorLog
    {
        public const string FolderName = "Logs";
        private const string Prefix = "errors-";
        private const int KeepSessions = 5;
        private const int MaxRepeatsPerMessage = 3;
        private const long MaxBytesPerSession = 2 * 1024 * 1024;

        private static readonly object Gate = new object();
        private static readonly Dictionary<int, int> Seen = new Dictionary<int, int>();
        private static string _path;
        private static long _written;
        private static bool _installed;

        /// <summary>Exceptions and errors recorded this session (repeats included).</summary>
        public static int Count { get; private set; }

        /// <summary>First new problem this session, for a one-time notice. Cleared by <see cref="TakeFirstNotice"/>.</summary>
        private static string _pendingNotice;

        public static string Directory => Path.Combine(Application.persistentDataPath, FolderName);

        public static string CurrentFile => _path;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            if (Application.isEditor) return; // the console already does this job in the editor
            lock (Gate)
            {
                if (_installed) return;
                _installed = true;
                Count = 0;
                Seen.Clear();
                _pendingNotice = null;
                _written = 0;
                try
                {
                    System.IO.Directory.CreateDirectory(Directory);
                    Prune(Directory, KeepSessions - 1);
                    _path = Path.Combine(Directory, $"{Prefix}{DateTime.Now:yyyyMMdd-HHmmss}.log");
                }
                catch
                {
                    _path = null;
                }
            }
            Application.logMessageReceivedThreaded += OnLog;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            Record(condition, stackTrace, type);
        }

        /// <summary>Exposed for tests; the log hook is the normal caller.</summary>
        public static bool Record(string condition, string stackTrace, LogType type)
        {
            lock (Gate)
            {
                Count++;
                int key = Hash(condition, stackTrace);
                Seen.TryGetValue(key, out int repeats);
                Seen[key] = repeats + 1;
                if (repeats >= MaxRepeatsPerMessage) return false;
                if (_pendingNotice == null && Count == 1)
                    _pendingNotice = condition;
                if (_path == null || _written > MaxBytesPerSession) return false;

                var sb = new StringBuilder(256);
                if (_written == 0)
                {
                    sb.Append("Solar Majesty ").Append(Application.version)
                      .Append("  unity ").Append(Application.unityVersion)
                      .Append("  build ").Append(Application.buildGUID)
                      .Append("  os ").Append(SystemInfo.operatingSystem)
                      .Append("  gpu ").Append(SystemInfo.graphicsDeviceName)
                      .Append("  ram ").Append(SystemInfo.systemMemorySize).Append(" MB\n\n");
                }
                sb.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ").Append(type).Append(": ")
                  .Append(condition).Append('\n');
                if (!string.IsNullOrEmpty(stackTrace)) sb.Append(stackTrace.TrimEnd()).Append('\n');
                if (repeats + 1 == MaxRepeatsPerMessage) sb.Append("(further repeats of this message are not logged)\n");
                sb.Append('\n');

                try
                {
                    string text = sb.ToString();
                    File.AppendAllText(_path, text);
                    _written += text.Length;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>The first problem of the session, once. Null when there is nothing new to say.</summary>
        public static string TakeFirstNotice()
        {
            lock (Gate)
            {
                string n = _pendingNotice;
                _pendingNotice = null;
                return n;
            }
        }

        public static void OpenFolder()
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                Application.OpenURL("file:///" + Directory.Replace('\\', '/'));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ErrorLog] Could not open " + Directory + ": " + e.Message);
            }
        }

        /// <summary>Keep the newest <paramref name="keep"/> session files.</summary>
        public static void Prune(string folder, int keep)
        {
            if (!System.IO.Directory.Exists(folder)) return;
            var files = new List<FileInfo>(new DirectoryInfo(folder).GetFiles(Prefix + "*.log"));
            files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            for (int i = Math.Max(0, keep); i < files.Count; i++)
            {
                try { files[i].Delete(); } catch { /* in use or read-only: leave it */ }
            }
        }

        private static int Hash(string a, string b)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (a?.GetHashCode() ?? 0);
                h = h * 31 + (b?.GetHashCode() ?? 0);
                return h;
            }
        }
    }
}
