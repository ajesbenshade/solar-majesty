using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    public enum AlertSeverity
    {
        /// <summary>Something good happened. Fades on its own.</summary>
        Good = 0,
        /// <summary>Worth knowing. Fades on its own.</summary>
        Info = 1,
        /// <summary>Needs attention soon.</summary>
        Warning = 2,
        /// <summary>Actively losing the colony. Sticks until resolved or acknowledged.</summary>
        Critical = 3
    }

    /// <summary>
    /// One entry in the alert feed. Alerts with the same <see cref="Key"/> collapse into a single
    /// row with a count, which is what stops "wisp draining power" from printing forty times.
    /// </summary>
    public sealed class Alert
    {
        public string Key;
        public string Message;
        public AlertSeverity Severity;
        public Vector3 WorldPosition;
        public bool HasPosition;
        public int Count = 1;
        public float FirstSeen;
        public float LastSeen;
        public bool Acknowledged;

        public string DisplayMessage => Count > 1 ? $"{Message}  x{Count}" : Message;
    }

    /// <summary>
    /// Prioritised, deduplicated notifications with a jump target.
    ///
    /// The Overseer log is a flat scroll where a colonist dying looks the same as a flag being
    /// posted, so players read neither. This ranks by severity then recency, collapses repeats, and
    /// remembers where each thing happened so the camera can be sent there.
    /// </summary>
    public sealed class AlertFeed
    {
        /// <summary>Rows kept. Beyond this the least important, oldest entry is dropped.</summary>
        public int Capacity { get; set; } = 6;

        /// <summary>Seconds a non-critical alert survives without being repeated.</summary>
        public float FadeSeconds { get; set; } = 22f;

        /// <summary>Repeats inside this window bump the count instead of adding a row.</summary>
        public float CollapseWindow { get; set; } = 30f;

        private readonly List<Alert> _alerts = new List<Alert>(12);

        public IReadOnlyList<Alert> Alerts => _alerts;

        public int CriticalCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _alerts.Count; i++)
                {
                    if (_alerts[i].Severity == AlertSeverity.Critical && !_alerts[i].Acknowledged)
                        n++;
                }
                return n;
            }
        }

        public void Push(string key, string message, AlertSeverity severity, float now)
        {
            Push(key, message, severity, now, Vector3.zero, false);
        }

        public void Push(string key, string message, AlertSeverity severity, float now, Vector3 at)
        {
            Push(key, message, severity, now, at, true);
        }

        private void Push(string key, string message, AlertSeverity severity, float now, Vector3 at, bool hasPos)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (string.IsNullOrEmpty(key)) key = message;

            for (int i = 0; i < _alerts.Count; i++)
            {
                Alert existing = _alerts[i];
                if (existing.Key != key) continue;
                if (now - existing.LastSeen > CollapseWindow) break;

                existing.Count++;
                existing.LastSeen = now;
                existing.Message = message;
                existing.WorldPosition = hasPos ? at : existing.WorldPosition;
                existing.HasPosition |= hasPos;

                // An escalating situation should re-raise, not stay filed under its calmer first report.
                if (severity > existing.Severity)
                {
                    existing.Severity = severity;
                    existing.Acknowledged = false;
                }
                return;
            }

            _alerts.Add(new Alert
            {
                Key = key,
                Message = message,
                Severity = severity,
                WorldPosition = at,
                HasPosition = hasPos,
                FirstSeen = now,
                LastSeen = now
            });

            Trim(now);
        }

        /// <summary>Drop faded entries. Critical alerts never expire on their own.</summary>
        public void Tick(float now)
        {
            for (int i = _alerts.Count - 1; i >= 0; i--)
            {
                Alert a = _alerts[i];
                if (a.Severity == AlertSeverity.Critical && !a.Acknowledged) continue;
                if (now - a.LastSeen >= FadeSeconds)
                    _alerts.RemoveAt(i);
            }
        }

        /// <summary>Most important first; ties broken by most recent.</summary>
        public void Sorted(List<Alert> into)
        {
            if (into == null) return;
            into.Clear();
            into.AddRange(_alerts);
            into.Sort((a, b) =>
            {
                int bySeverity = b.Severity.CompareTo(a.Severity);
                return bySeverity != 0 ? bySeverity : b.LastSeen.CompareTo(a.LastSeen);
            });
        }

        /// <summary>The alert the "jump to trouble" key should take the player to.</summary>
        public Alert MostUrgentWithPosition()
        {
            Alert best = null;
            for (int i = 0; i < _alerts.Count; i++)
            {
                Alert a = _alerts[i];
                if (!a.HasPosition || a.Acknowledged) continue;
                if (best == null ||
                    a.Severity > best.Severity ||
                    (a.Severity == best.Severity && a.LastSeen > best.LastSeen))
                {
                    best = a;
                }
            }
            return best;
        }

        public void Acknowledge(Alert alert)
        {
            if (alert == null) return;
            alert.Acknowledged = true;
        }

        public void Clear() => _alerts.Clear();

        private void Trim(float now)
        {
            while (_alerts.Count > Capacity)
            {
                int worst = -1;
                for (int i = 0; i < _alerts.Count; i++)
                {
                    if (worst < 0)
                    {
                        worst = i;
                        continue;
                    }

                    Alert a = _alerts[i];
                    Alert b = _alerts[worst];
                    if (a.Severity < b.Severity || (a.Severity == b.Severity && a.LastSeen < b.LastSeen))
                        worst = i;
                }

                if (worst < 0) break;
                _alerts.RemoveAt(worst);
            }
        }
    }
}
