// Seeded simulation randomness — pure C#, no MonoBehaviour.

namespace SolarMajesty
{
    /// <summary>
    /// The simulation's own random stream (xorshift64*). Same seed, same sequence, on every
    /// machine, and <see cref="State"/> goes into the save so a reload continues the sequence.
    /// Cosmetic randomness (dressing, VFX, audio takes) must not draw from it.
    /// </summary>
    public sealed class SimRng
    {
        private ulong _state;

        public SimRng(int seed) => Reseed(seed);

        /// <summary>Round-trips through a save. Zero is not a valid state and reads as a fixed one.</summary>
        public long State
        {
            get => unchecked((long)_state);
            set => _state = value != 0 ? unchecked((ulong)value) : Scramble(0);
        }

        public void Reseed(int seed) => _state = Scramble(unchecked((ulong)seed));

        public uint NextUInt()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return (uint)((_state * 2685821657736338717UL) >> 32);
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float Value() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform in [0, <paramref name="max"/>).</summary>
        public float Range(float max) => Value() * max;

        /// <summary>Uniform in [<paramref name="min"/>, <paramref name="max"/>).</summary>
        public float Range(float min, float max) => min + Value() * (max - min);

        /// <summary>Uniform point inside the unit circle.</summary>
        public UnityEngine.Vector2 InsideUnitCircle()
        {
            // Rejection sampling: about 4 in 5 tries land inside, and no trig is involved.
            while (true)
            {
                float x = Value() * 2f - 1f;
                float y = Value() * 2f - 1f;
                if (x * x + y * y <= 1f) return new UnityEngine.Vector2(x, y);
            }
        }

        /// <summary>Uniform integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }

        // SplitMix64 finaliser: nearby seeds (1, 2, 3...) start far apart, and never at zero.
        private static ulong Scramble(ulong seed)
        {
            ulong z = seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return z != 0 ? z : 0x9E3779B97F4A7C15UL;
        }
    }
}
