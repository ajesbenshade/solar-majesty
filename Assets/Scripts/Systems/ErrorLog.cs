using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Who and where, written at the top of every session log.</summary>
    public sealed class ErrorLogSession
    {
        public string GameVersion = "";
        public string BuildGuid = "";
        public string UnityVersion = "";
        public string Platform = "";
        public string OperatingSystem = "";
        public string Device = "";
        public string Graphics = "";
        public int MemoryMb;
        public DateTime StartedUtc;

        // Refreshed from the main thread while the session runs.
        public string World = "";
        public int Seed;
        public int Sol;
        public string Screen = "";
    }

    /// <summary>
    /// Crash and bug-report log. Keeps the recent log lines (<see cref="ErrorLogBuffer"/>) and writes
    /// them, under a session header, to <c>persistentDataPath/Logs/session-*.log</c>. Each write
    /// replaces the whole file through a temp file, so a crash mid-write leaves the previous complete
    /// copy. Files are capped at <see cref="MaxFileBytes"/> and only the newest <see cref="KeepFiles"/>
    /// sessions stay on disk. Nothing leaves the machine; the player attaches the file to a report.
    ///
    /// <see cref="Record"/> is safe from any thread (Unity's threaded log callback). Nothing in here
    /// calls <c>Debug.Log</c>: a log call from inside the log hook would feed itself.
    /// </summary>
    public static class ErrorLog
    {
        public const string FolderName = "Logs";
        public const string FilePrefix = "session-";
        public const string FileExtension = ".log";
        public const int DefaultKeepFiles = 10;
        public const int DefaultMaxFileBytes = 512 * 1024;

        /// <summary>An error writes at once unless the last write was sooner than this.</summary>
        public const double ErrorFlushIntervalSeconds = 0.5;

        private static readonly object Gate = new object();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static ErrorLogBuffer _buffer = new ErrorLogBuffer();
        private static ErrorLogSession _session = new ErrorLogSession { StartedUtc = DateTime.UtcNow };
        private static double _sessionStart;
        private static double _lastFlush = -1000.0;
        private static string _fileName;
        private static string _prunedDirectory;
        private static bool _dirty;

        public static bool Enabled { get; set; } = true;

        public static int KeepFiles { get; set; } = DefaultKeepFiles;

        public static int MaxFileBytes { get; set; } = DefaultMaxFileBytes;

        /// <summary>
        /// When set, logs are written here instead of <c>Application.persistentDataPath/Logs</c>.
        /// Tests point this at a temp folder. Read at every write, so it can change mid-session.
        /// </summary>
        public static string DirectoryOverride { get; set; }

        /// <summary>
        /// The real folder, captured on the main thread at boot. <c>Application.persistentDataPath</c>
        /// throws off the main thread, and an error can arrive from any thread.
        /// </summary>
        public static string DefaultDirectory { get; set; }

        public static string Directory =>
            !string.IsNullOrEmpty(DirectoryOverride) ? DirectoryOverride
            : !string.IsNullOrEmpty(DefaultDirectory) ? DefaultDirectory
            : Path.Combine(Application.persistentDataPath, FolderName);

        /// <summary>Where this session writes, or null before <see cref="Begin"/>.</summary>
        public static string SessionPath
        {
            get
            {
                lock (Gate)
                    return _fileName == null ? null : Path.Combine(Directory, _fileName);
            }
        }

        /// <summary>The last file actually written this session, or null.</summary>
        public static string LastWrittenPath { get; private set; }

        /// <summary>The last write failure, for the pause menu or a test; empty when writes work.</summary>
        public static string LastWriteError { get; private set; } = "";

        public static bool Dirty
        {
            get { lock (Gate) return _dirty; }
        }

        public static int TotalErrors
        {
            get { lock (Gate) return _buffer.TotalErrors; }
        }

        /// <summary>Seconds on the thread-safe session clock.</summary>
        public static double Now => Clock.Elapsed.TotalSeconds;

        /// <summary>Start a session: fresh buffer, fresh file name. Nothing touches disk until a flush.</summary>
        public static void Begin(ErrorLogSession session, DateTime utcNow, int capacity = ErrorLogBuffer.DefaultCapacity)
        {
            lock (Gate)
            {
                _session = session ?? new ErrorLogSession();
                _session.StartedUtc = utcNow;
                _buffer = new ErrorLogBuffer(capacity);
                _sessionStart = Now;
                _lastFlush = -1000.0;
                _fileName = FileNameFor(utcNow);
                _prunedDirectory = null;
                _dirty = false;
                LastWrittenPath = null;
                LastWriteError = "";
            }
        }

        /// <summary>Main thread: what the player was doing, for the header.</summary>
        public static void SetContext(string world, int seed, int sol, string screen)
        {
            lock (Gate)
            {
                _session.World = world ?? "";
                _session.Seed = seed;
                _session.Sol = sol;
                _session.Screen = screen ?? "";
            }
        }

        /// <summary>
        /// Any thread. Errors write straight away (rate limited) so a crash right after keeps them;
        /// other lines wait for the next <see cref="FlushIfDirty"/>.
        /// </summary>
        public static void Record(LogType type, string message, string stackTrace)
        {
            if (!Enabled) return;
            lock (Gate)
            {
                if (_fileName == null) return;
                double now = Now;
                _buffer.Add(type, message, stackTrace, now - _sessionStart);
                _dirty = true;
                if (ErrorLogBuffer.IsError(type) && now - _lastFlush >= ErrorFlushIntervalSeconds)
                    FlushLocked(now);
            }
        }

        /// <summary>Write if anything arrived and the last write is at least this old.</summary>
        public static bool FlushIfDirty(double minIntervalSeconds)
        {
            lock (Gate)
            {
                double now = Now;
                if (!_dirty || now - _lastFlush < minIntervalSeconds) return false;
                return FlushLocked(now);
            }
        }

        /// <summary>Write now, dirty or not (quit, crash, the bug-report button).</summary>
        public static bool Flush()
        {
            lock (Gate)
                return FlushLocked(Now);
        }

        private static bool FlushLocked(double now)
        {
            if (!Enabled || _fileName == null) return false;
            _lastFlush = now;
            string path = null;
            try
            {
                string dir = Directory;
                path = Path.Combine(dir, _fileName);
                System.IO.Directory.CreateDirectory(dir);
                string text = Compose(_session, _buffer, now - _sessionStart, MaxFileBytes);
                WriteReplacing(path, text);
                _dirty = false;
                LastWrittenPath = path;
                LastWriteError = "";
                if (_prunedDirectory != dir)
                {
                    _prunedDirectory = dir;
                    PruneOldLogs(dir, KeepFiles, path);
                }
                return true;
            }
            catch (Exception e)
            {
                // Never Debug.Log here; keep the reason for the pause menu and stop retrying every line.
                LastWriteError = e.Message;
                _dirty = false;
                return false;
            }
        }

        private static void WriteReplacing(string path, string text)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            if (!File.Exists(path))
            {
                File.Move(temp, path);
                return;
            }
            try
            {
                File.Replace(temp, path, null);
            }
            catch (PlatformNotSupportedException)
            {
                File.Delete(path);
                File.Move(temp, path);
            }
            catch (IOException)
            {
                File.Delete(path);
                File.Move(temp, path);
            }
        }

        /// <summary><c>session-20261011-153012-123.log</c>: sorts by start time as plain text.</summary>
        public static string FileNameFor(DateTime utc) =>
            FilePrefix + utc.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + FileExtension;

        public static bool IsSessionFile(string fileName) =>
            !string.IsNullOrEmpty(fileName)
            && fileName.StartsWith(FilePrefix, StringComparison.Ordinal)
            && fileName.EndsWith(FileExtension, StringComparison.Ordinal);

        /// <summary>
        /// Delete the oldest session logs so at most <paramref name="keep"/> remain. Never deletes
        /// <paramref name="keepPath"/>. Returns how many files went.
        /// </summary>
        public static int PruneOldLogs(string directory, int keep, string keepPath = null)
        {
            if (string.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory)) return 0;
            var names = new List<string>();
            foreach (string file in System.IO.Directory.GetFiles(directory))
            {
                string name = Path.GetFileName(file);
                if (IsSessionFile(name)) names.Add(name);
            }
            names.Sort(StringComparer.Ordinal);
            string keepName = string.IsNullOrEmpty(keepPath) ? null : Path.GetFileName(keepPath);
            int excess = names.Count - Math.Max(1, keep);
            int removed = 0;
            for (int i = 0; i < names.Count && removed < excess; i++)
            {
                if (names[i] == keepName) continue;
                try
                {
                    File.Delete(Path.Combine(directory, names[i]));
                    removed++;
                }
                catch (Exception)
                {
                    // A locked file (open in an editor) stays; the next session tries again.
                }
            }
            return removed;
        }

        /// <summary>Newest session log in the folder (this session's if it has been written), or null.</summary>
        public static string LatestLogPath()
        {
            string dir = Directory;
            if (!System.IO.Directory.Exists(dir)) return null;
            string best = null;
            foreach (string file in System.IO.Directory.GetFiles(dir))
            {
                string name = Path.GetFileName(file);
                if (!IsSessionFile(name)) continue;
                if (best == null || string.CompareOrdinal(name, Path.GetFileName(best)) > 0)
                    best = file;
            }
            return best;
        }

        /// <summary><c>file:///…</c> URL for a folder, for <c>Application.OpenURL</c>.</summary>
        public static string FolderUrl(string directory)
        {
            string full = Path.GetFullPath(directory);
            if (!full.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                full += Path.DirectorySeparatorChar;
            return new Uri(full).AbsoluteUri;
        }

        public static string FormatHeader(ErrorLogSession s, double uptimeSeconds, ErrorLogBuffer buffer)
        {
            var sb = new StringBuilder(512);
            sb.Append("Solar Majesty session log\n");
            sb.Append("version:   ").Append(s.GameVersion).Append(" (build ").Append(s.BuildGuid).Append(")\n");
            sb.Append("unity:     ").Append(s.UnityVersion).Append('\n');
            sb.Append("platform:  ").Append(s.Platform).Append(" · ").Append(s.OperatingSystem).Append('\n');
            sb.Append("device:    ").Append(s.Device).Append(" · ").Append(s.Graphics)
                .Append(" · ").Append(s.MemoryMb.ToString(CultureInfo.InvariantCulture)).Append(" MB\n");
            sb.Append("started:   ").Append(s.StartedUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                .Append(" UTC · up ").Append(FormatSeconds(uptimeSeconds)).Append('\n');
            sb.Append("world:     ").Append(string.IsNullOrEmpty(s.World) ? "-" : s.World)
                .Append(" · seed ").Append(s.Seed.ToString(CultureInfo.InvariantCulture))
                .Append(" · sol ").Append(s.Sol.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(s.Screen))
                sb.Append(" · ").Append(s.Screen);
            sb.Append('\n');
            if (buffer != null)
            {
                sb.Append("lines:     ").Append(buffer.TotalLines.ToString(CultureInfo.InvariantCulture))
                    .Append(" · errors ").Append(buffer.TotalErrors.ToString(CultureInfo.InvariantCulture))
                    .Append(" · warnings ").Append(buffer.TotalWarnings.ToString(CultureInfo.InvariantCulture));
                if (buffer.Evicted > 0)
                    sb.Append(" · ").Append(buffer.Evicted.ToString(CultureInfo.InvariantCulture)).Append(" older lines dropped");
                sb.Append('\n');
            }
            sb.Append(new string('-', 72)).Append('\n');
            return sb.ToString();
        }

        public static string FormatEntry(ErrorLogEntry e)
        {
            var sb = new StringBuilder(e.Message.Length + 64);
            sb.Append('[').Append(FormatSeconds(e.FirstSeconds).PadLeft(9)).Append("] ");
            sb.Append(Label(e.Type).PadRight(9)).Append(' ').Append(e.Message.Replace("\r", ""));
            if (e.Count > 1)
                sb.Append("  (x").Append(e.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(", last ").Append(FormatSeconds(e.LastSeconds)).Append(')');
            sb.Append('\n');
            if (e.IsError && !string.IsNullOrEmpty(e.StackTrace))
            {
                foreach (string line in e.StackTrace.Replace("\r", "").Split('\n'))
                {
                    if (line.Length == 0) continue;
                    sb.Append("    ").Append(line).Append('\n');
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Header plus the newest lines that fit in <paramref name="maxBytes"/> (UTF-8). Older lines are
        /// dropped first and the file says how many.
        /// </summary>
        public static string Compose(ErrorLogSession session, ErrorLogBuffer buffer, double uptimeSeconds, int maxBytes)
        {
            string header = FormatHeader(session ?? new ErrorLogSession(), uptimeSeconds, buffer);
            var entries = buffer != null ? buffer.Snapshot() : new List<ErrorLogEntry>();
            var utf8 = Encoding.UTF8;
            const string omittedReserve = "(9999 older lines omitted to stay under the size cap)\n";
            int budget = Math.Max(0, maxBytes - utf8.GetByteCount(header) - utf8.GetByteCount(omittedReserve));
            var kept = new List<string>();
            int used = 0;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                string line = FormatEntry(entries[i]);
                int bytes = utf8.GetByteCount(line);
                if (used + bytes > budget) break;
                used += bytes;
                kept.Add(line);
            }
            var sb = new StringBuilder(header.Length + used + 64);
            sb.Append(header);
            int omitted = entries.Count - kept.Count;
            if (omitted > 0)
                sb.Append('(').Append(omitted.ToString(CultureInfo.InvariantCulture))
                    .Append(" older lines omitted to stay under the size cap)\n");
            for (int i = kept.Count - 1; i >= 0; i--)
                sb.Append(kept[i]);
            return sb.ToString();
        }

        private static string Label(LogType type)
        {
            switch (type)
            {
                case LogType.Error: return "ERROR";
                case LogType.Assert: return "ASSERT";
                case LogType.Warning: return "WARN";
                case LogType.Exception: return "EXCEPTION";
                default: return "LOG";
            }
        }

        private static string FormatSeconds(double seconds) =>
            seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";
    }
}
