using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>How often one sound-effect event may fire.</summary>
    public struct SfxLimit
    {
        /// <summary>Minimum seconds between two plays of the event.</summary>
        public float MinGap;

        /// <summary>Plays allowed back to back (after MinGap) before the bucket runs dry.</summary>
        public int Burst;

        /// <summary>How quickly spent plays come back, per second.</summary>
        public float RefillPerSecond;

        public SfxLimit(float minGap, int burst, float refillPerSecond)
        {
            MinGap = minGap;
            Burst = burst < 1 ? 1 : burst;
            RefillPerSecond = refillPerSecond;
        }

        /// <summary>No limit beyond a tiny gap, for one-off UI and outcome sounds.</summary>
        public static SfxLimit Free => new SfxLimit(0.02f, 8, 20f);
    }

    /// <summary>
    /// Per-event voice limiter: a minimum gap plus a token bucket.
    ///
    /// Gameplay code reports events, not sounds. When twenty robots claim flags in one frame, or a
    /// stalker calls ApplyDamage every frame while it chews on a mech, playing a clip per report
    /// stacks into one loud smear and drowns everything else. The gap stops same-frame stacking;
    /// the bucket lets a short flurry through and then thins a sustained stream to a steady rate.
    /// Time is passed in (unscaled seconds) so this is independent of Unity and unit testable.
    /// </summary>
    public sealed class SfxRateLimiter
    {
        private sealed class State
        {
            public float LastPlay = float.NegativeInfinity;
            public float Tokens;
            public float LastRefill;
        }

        private readonly Dictionary<string, State> _states = new Dictionary<string, State>();

        /// <summary>True (and the play is recorded) when the event may sound now.</summary>
        public bool TryAcquire(string key, float now, SfxLimit limit)
        {
            if (string.IsNullOrEmpty(key)) return true;

            if (!_states.TryGetValue(key, out State s))
            {
                s = new State { Tokens = limit.Burst, LastRefill = now };
                _states[key] = s;
            }

            float elapsed = now - s.LastRefill;
            if (elapsed > 0f)
            {
                s.Tokens += elapsed * limit.RefillPerSecond;
                if (s.Tokens > limit.Burst) s.Tokens = limit.Burst;
                s.LastRefill = now;
            }

            if (now - s.LastPlay < limit.MinGap) return false;
            if (s.Tokens < 1f) return false;

            s.Tokens -= 1f;
            s.LastPlay = now;
            return true;
        }

        public void Reset() => _states.Clear();
    }
}
