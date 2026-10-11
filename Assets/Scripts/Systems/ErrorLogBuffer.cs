using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>One distinct log line and how many times it was seen.</summary>
    public sealed class ErrorLogEntry
    {
        public LogType Type;
        public string Message;
        public string StackTrace;
        /// <summary>Seconds since the session began, first and most recent occurrence.</summary>
        public double FirstSeconds;
        public double LastSeconds;
        public int Count;

        public bool IsError => ErrorLogBuffer.IsError(Type);

        public ErrorLogEntry Clone() => (ErrorLogEntry)MemberwiseClone();
    }

    /// <summary>
    /// Recent log lines for a crash/bug report, deduplicated. A repeated message bumps its count and
    /// moves to the end instead of filling the buffer, so one spamming warning cannot push the first
    /// exception out. When full, the oldest non-error line goes first; errors only fall out when the
    /// buffer is nothing but errors. Pure C#; not thread-safe on its own (<see cref="ErrorLog"/> locks).
    /// </summary>
    public sealed class ErrorLogBuffer
    {
        public const int DefaultCapacity = 256;
        public const int MaxMessageChars = 2000;
        public const int MaxStackChars = 4000;

        private readonly LinkedList<ErrorLogEntry> _order = new LinkedList<ErrorLogEntry>();
        private readonly Dictionary<string, LinkedListNode<ErrorLogEntry>> _byKey =
            new Dictionary<string, LinkedListNode<ErrorLogEntry>>(StringComparer.Ordinal);

        public ErrorLogBuffer(int capacity = DefaultCapacity)
        {
            Capacity = Math.Max(1, capacity);
        }

        public int Capacity { get; }

        /// <summary>Distinct lines currently held.</summary>
        public int Count => _order.Count;

        /// <summary>Every line received this session, repeats included.</summary>
        public int TotalLines { get; private set; }

        /// <summary>Error, exception and assert lines received this session, repeats included.</summary>
        public int TotalErrors { get; private set; }

        public int TotalWarnings { get; private set; }

        /// <summary>Distinct lines dropped to stay under <see cref="Capacity"/>.</summary>
        public int Evicted { get; private set; }

        public static bool IsError(LogType type) =>
            type == LogType.Error || type == LogType.Exception || type == LogType.Assert;

        public void Add(LogType type, string message, string stackTrace, double seconds)
        {
            message = Clip(message, MaxMessageChars);
            TotalLines++;
            if (IsError(type)) TotalErrors++;
            else if (type == LogType.Warning) TotalWarnings++;

            string key = KeyFor(type, message);
            if (_byKey.TryGetValue(key, out var node))
            {
                var seen = node.Value;
                seen.Count++;
                seen.LastSeconds = seconds;
                if (string.IsNullOrEmpty(seen.StackTrace) && !string.IsNullOrEmpty(stackTrace))
                    seen.StackTrace = Clip(stackTrace, MaxStackChars);
                _order.Remove(node);
                _order.AddLast(node);
                return;
            }

            if (_order.Count >= Capacity)
                EvictOne();

            var entry = new ErrorLogEntry
            {
                Type = type,
                Message = message,
                StackTrace = Clip(stackTrace, MaxStackChars),
                FirstSeconds = seconds,
                LastSeconds = seconds,
                Count = 1
            };
            _byKey[key] = _order.AddLast(entry);
        }

        /// <summary>Copies of the held lines, least recently seen first.</summary>
        public List<ErrorLogEntry> Snapshot()
        {
            var list = new List<ErrorLogEntry>(_order.Count);
            foreach (var e in _order)
                list.Add(e.Clone());
            return list;
        }

        public void Clear()
        {
            _order.Clear();
            _byKey.Clear();
            TotalLines = 0;
            TotalErrors = 0;
            TotalWarnings = 0;
            Evicted = 0;
        }

        private void EvictOne()
        {
            var victim = _order.First;
            for (var n = _order.First; n != null; n = n.Next)
            {
                if (!n.Value.IsError)
                {
                    victim = n;
                    break;
                }
            }
            if (victim == null) return;
            _byKey.Remove(KeyFor(victim.Value.Type, victim.Value.Message));
            _order.Remove(victim);
            Evicted++;
        }

        private static string KeyFor(LogType type, string message) => ((int)type).ToString() + "|" + message;

        private static string Clip(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= max ? text : text.Substring(0, max) + " …[truncated]";
        }
    }
}
