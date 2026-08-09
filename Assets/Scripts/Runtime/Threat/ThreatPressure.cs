using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Minimal runtime threat service. Aggregates local danger (0–1) from Dust Stalkers
    /// so SpecialistAgent can pass it as bodyDanger into SpecialistBrain.Evaluate.
    /// Pure Systems/ is untouched — this is Runtime only.
    /// </summary>
    public sealed class ThreatPressure
    {
        private readonly Dictionary<object, float> _contributions = new Dictionary<object, float>();

        /// <summary>Background danger when nothing is aggroing (calm outpost).</summary>
        public float Ambient { get; set; } = 0.18f;

        /// <summary>
        /// Current bodyDanger-style value: ambient + strongest active contribution, clamped 0–1.
        /// Higher pressure increases risk cost in the brain (hurts low-courage) and
        /// makes ClearThreat relatively more attractive for high-courage Defense Mechs.
        /// </summary>
        public float Current
        {
            get
            {
                float peak = 0f;
                foreach (var kv in _contributions)
                {
                    if (kv.Value > peak)
                        peak = kv.Value;
                }

                return Mathf.Clamp01(Ambient + peak);
            }
        }

        public int ActiveSources
        {
            get
            {
                int n = 0;
                foreach (var kv in _contributions)
                {
                    if (kv.Value > 0.01f) n++;
                }
                return n;
            }
        }

        /// <summary>Stalkers call this each frame (or on state change) with their local pressure add-on.</summary>
        public void Report(object sourceId, float pressure01)
        {
            if (sourceId == null) return;

            if (pressure01 <= 0.001f)
            {
                _contributions.Remove(sourceId);
                return;
            }

            _contributions[sourceId] = Mathf.Clamp01(pressure01);
        }

        public void Clear(object sourceId)
        {
            if (sourceId == null) return;
            _contributions.Remove(sourceId);
        }

        public void ClearAll()
        {
            _contributions.Clear();
        }

        public string DebugLine() =>
            $"threat={Current:F2} ambient={Ambient:F2} sources={ActiveSources}";
    }
}
