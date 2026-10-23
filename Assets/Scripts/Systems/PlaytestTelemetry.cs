using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
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

        /// <summary>
        /// When set, session logs are written here instead of
        /// <c>Application.persistentDataPath/Playtest</c>. Tests use a temp folder.
        /// </summary>
        public static string DirectoryOverride { get; set; }

        public static string Directory =>
            string.IsNullOrEmpty(DirectoryOverride)
                ? Path.Combine(Application.persistentDataPath, FolderName)
                : DirectoryOverride;

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
                _errors = 0;
                Application.logMessageReceived -= OnLogMessage;
                Application.logMessageReceived += OnLogMessage;

                Record("session_start", new[]
                {
                    ("build", buildLabel),
                    ("build_guid", Application.buildGUID),
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

            Buffer.Append("{\"t\":").Append(Time.realtimeSinceStartup.ToString("F1", CultureInfo.InvariantCulture));
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
            Record(eventName, new[] { (key, value.ToString("F2", CultureInfo.InvariantCulture)) });

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
                ("play_seconds", playSeconds.ToString("F0", CultureInfo.InvariantCulture)),
                ("errors", _errors.ToString(CultureInfo.InvariantCulture))
            });
            Flush();
        }

        // ---- First hour --------------------------------------------------------------------
        // Field names here are the contract for Tools/playtest/summarize.py. Change both together.

        /// <summary>Tutorial bar is up for this session (fresh start or Continue before it was done).</summary>
        public static void RecordTutorialStart(string flow, int step, int goal)
        {
            Record("tutorial_start", new[]
            {
                ("flow", flow),
                ("step", Int(step)),
                ("goal", Int(goal))
            });
        }

        /// <summary>
        /// Left step <paramref name="from"/> for <paramref name="to"/>. Keeps the original
        /// <c>step</c> field (the step entered); <c>seconds</c> is play time spent on <c>from</c>.
        /// </summary>
        public static void RecordTutorialStep(string flow, int from, int to, float secondsOnFrom, int goal)
        {
            Record("tutorial_step", new[]
            {
                ("step", Int(to)),
                ("from", Int(from)),
                ("seconds", Secs(secondsOnFrom)),
                ("goal", Int(goal)),
                ("flow", flow)
            });
        }

        public static void RecordTutorialSkip(string flow, int step, int goal, float secondsOnStep)
        {
            Record("tutorial_skip", new[]
            {
                ("flow", flow),
                ("step", Int(step)),
                ("goal", Int(goal)),
                ("seconds", Secs(secondsOnStep))
            });
        }

        public static void RecordTutorialRestart(string flow, int fromStep)
        {
            Record("tutorial_restart", new[]
            {
                ("flow", flow),
                ("from", Int(fromStep))
            });
        }

        /// <summary>
        /// No meaningful input for <paramref name="seconds"/> while the world ran.
        /// <paramref name="ended"/> is input, pause, or quit. <c>step</c> is -1 once the tutorial is done.
        /// </summary>
        public static void RecordIdleGap(float seconds, int step, string ended)
        {
            Record("idle_gap", new[]
            {
                ("seconds", Secs(seconds)),
                ("step", Int(step)),
                ("ended", ended)
            });
        }

        /// <summary>The tutorial nudged a quiet player.</summary>
        public static void RecordIdleHint(int step, string hint)
        {
            Record("idle_hint", new[]
            {
                ("step", Int(step)),
                ("hint", hint)
            });
        }

        /// <summary>A click on a red ghost, or a placement the placer refused.</summary>
        public static void RecordPlacementInvalid(string building, string reason)
        {
            Record("placement_invalid", new[]
            {
                ("building", building),
                ("reason", reason)
            });
        }

        /// <summary>+ / − on a posted pole. <c>interest</c> is how many robots it tempts afterwards.</summary>
        public static void RecordFlagBounty(string type, float from, float to, int interest)
        {
            Record("flag_bounty", new[]
            {
                ("type", type),
                ("from", from.ToString("F0", CultureInfo.InvariantCulture)),
                ("to", to.ToString("F0", CultureInfo.InvariantCulture)),
                ("interest", Int(interest))
            });
        }

        public static void RecordFlagCancelled(string type, float bounty, int refund)
        {
            Record("flag_cancelled", new[]
            {
                ("type", type),
                ("bounty", bounty.ToString("F0", CultureInfo.InvariantCulture)),
                ("refund", Int(refund))
            });
        }

        /// <summary>A flag click that posted nothing. <c>reason</c> is "treasury" today.</summary>
        public static void RecordFlagPostFailed(string type, int cost, string reason)
        {
            Record("flag_post_failed", new[]
            {
                ("type", type),
                ("bounty", Int(cost)),
                ("reason", reason)
            });
        }

        /// <summary>Errors and exceptions logged since <see cref="Begin"/>. Written on session_quit.</summary>
        public static int ErrorCount => _errors;

        private const int MaxErrorEvents = 5;
        private const int MaxErrorChars = 160;
        private static int _errors;

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            _errors++;
            // The first few, so a report can say what broke. The count covers the rest.
            if (_errors > MaxErrorEvents) return;
            string msg = condition ?? "";
            if (msg.Length > MaxErrorChars) msg = msg.Substring(0, MaxErrorChars);
            Record("error", new[]
            {
                ("type", type.ToString()),
                ("msg", msg)
            });
        }

        private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Secs(float value) =>
            Math.Max(0f, value).ToString("F1", CultureInfo.InvariantCulture);

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
            return raw.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ").Replace("\t", "\\t");
        }
    }
}
