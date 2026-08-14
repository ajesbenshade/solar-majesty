using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    public readonly struct OverseerLogEntry
    {
        public readonly string Line;
        public readonly float Time;

        public OverseerLogEntry(string line, float time)
        {
            Line = line;
            Time = time;
        }
    }

    /// <summary>
    /// SpaceXAI overseer voice — drops, gates, travel. Informational only; never a unit command.
    /// </summary>
    public sealed class OverseerLog
    {
        private readonly List<OverseerLogEntry> _entries = new List<OverseerLogEntry>(12);

        public IReadOnlyList<OverseerLogEntry> Entries => _entries;
        public string Latest => _entries.Count > 0 ? _entries[_entries.Count - 1].Line : "";

        public void Push(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _entries.Add(new OverseerLogEntry(line, UnityEngine.Time.unscaledTime));
            while (_entries.Count > 8)
                _entries.RemoveAt(0);
            Debug.Log($"[Overseer] {line}");
        }
    }
}
