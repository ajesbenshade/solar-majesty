using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Local-only playtest instrumentation. Answers the questions a 45-90 minute external test needs:
    /// where did they stop, which bounties got refused, which win gate stalled, and what did the
    /// metals curve look like. Writes one JSON-lines file per session next to the saves so a tester
    /// can zip and send it back. Nothing leaves the machine on its own.
    /// </summary>
    public static class PlaytestTelemetry
    {
        private const string FolderName = "Playtest";

        public static bool Enabled { get; set; } = true;

        private static string _path;
        private static readonly StringBuilder Buffer = new StringBuilder(8192);
        private static float _lastFlush;
        private static int _lines;

        public static string SessionPath => _path;

        public static string Directory => Path.Combine(Application.persistentDataPath, FolderName);

        public static void Begin(string buildLabel)
        {
            if (!Enabled) return;

            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                _path = Path.Combine(Directory, $"session-{stamp}.jsonl");
                Buffer.Clear();
                _lines = 0;

                Record("session_start", new[]
                {
                    ("build", buildLabel),
                    ("unity", Application.unityVersion),
                    ("platform", Application.platform.ToString())
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Telemetry] Could not start a session log: {e.Message}");
                Enabled = false;
            }
        }

        /// <summary>Append one event. Values are written as-is; keep them short and flat.</summary>
        public static void Record(string eventName, IReadOnlyList<(string key, string value)> fields = null)
        {
            if (!Enabled || string.IsNullOrEmpty(_path)) return;

            Buffer.Append("{\"t\":").Append(Time.realtimeSinceStartup.ToString("F1"));
            Buffer.Append(",\"e\":\"").Append(Escape(eventName)).Append('"');

            if (fields != null)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    Buffer.Append(",\"").Append(Escape(fields[i].key)).Append("\":\"");
                    Buffer.Append(Escape(fields[i].value)).Append('"');
                }
            }

            Buffer.Append("}\n");
            _lines++;

            // Flush on a timer so a hard crash loses seconds, not the whole session.
            if (Time.realtimeSinceStartup - _lastFlush > 10f)
                Flush();
        }

        public static void Record(string eventName, string key, string value) =>
            Record(eventName, new[] { (key, value) });

        public static void Record(string eventName, string key, float value) =>
            Record(eventName, new[] { (key, value.ToString("F2")) });

        public static void Record(string eventName, string key, int value) =>
            Record(eventName, new[] { (key, value.ToString()) });

        /// <summary>The most important event in the file: how far they got before stopping.</summary>
        public static void RecordQuit(string screen, CelestialBodyId body, int population, int modules, double playSeconds)
        {
            Record("session_quit", new[]
            {
                ("screen", screen),
                ("body", body.ToString()),
                ("pop", population.ToString()),
                ("modules", modules.ToString()),
                ("play_seconds", playSeconds.ToString("F0"))
            });
            Flush();
        }

        public static void Flush()
        {
            if (!Enabled || string.IsNullOrEmpty(_path) || Buffer.Length == 0) return;

            try
            {
                File.AppendAllText(_path, Buffer.ToString());
                Buffer.Clear();
                _lastFlush = Time.realtimeSinceStartup;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Telemetry] Flush failed: {e.Message}");
                Enabled = false;
            }
        }

        public static int PendingLines => _lines;

        private static string Escape(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            return raw.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
        }
    }
}
