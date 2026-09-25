using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>
    /// Chooses which recorded variant of a sound effect to play next.
    ///
    /// Every event ships with a few takes (see Tools/audio/render_sfx.py). Picking uniformly still
    /// plays the same take twice in a row a third of the time, and that back-to-back repeat is
    /// exactly what the ear flags as "machine gun". This never repeats the previous take while
    /// staying uniform over the others. Randomness is passed in so the rule is unit testable.
    /// </summary>
    public sealed class SfxVariantPicker
    {
        private readonly Dictionary<string, int> _last = new Dictionary<string, int>();

        /// <summary>
        /// Index in [0, count) for <paramref name="key"/>, never equal to the previous pick when
        /// count is at least 2. <paramref name="random01"/> is a uniform sample in [0, 1).
        /// </summary>
        public int Next(string key, int count, float random01)
        {
            if (count <= 1)
            {
                Remember(key, 0);
                return 0;
            }

            float r = random01 < 0f ? 0f : (random01 >= 1f ? 0.99999f : random01);
            int last = Last(key);
            int pick;
            if (last < 0 || last >= count)
            {
                pick = (int)(r * count);
            }
            else
            {
                // Draw from the other count-1 slots, then step over the previous one.
                pick = (int)(r * (count - 1));
                if (pick >= last) pick++;
            }

            if (pick >= count) pick = count - 1;
            Remember(key, pick);
            return pick;
        }

        /// <summary>The previous pick for a key, or -1 when nothing has been played yet.</summary>
        public int Last(string key) =>
            key != null && _last.TryGetValue(key, out int last) ? last : -1;

        public void Reset() => _last.Clear();

        private void Remember(string key, int index)
        {
            if (key != null) _last[key] = index;
        }
    }
}
