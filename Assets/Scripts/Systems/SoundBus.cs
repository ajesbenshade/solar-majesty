using UnityEngine;

namespace SolarMajesty
{
    public enum SoundChannel
    {
        Ui = 0,
        Sfx = 1,
        Ambient = 2,
        Music = 3,
        Voice = 4
    }

    /// <summary>
    /// Mixing buses with ducking.
    ///
    /// Every AudioSource in the project used to set its own raw volume, so there was no way to make
    /// the Overseer audible over combat or to drop the ambient bed when something important happens.
    /// This is a code mixer rather than an AudioMixer asset: a solo project gets the same ducking
    /// behaviour without an asset that cannot be diffed or unit tested.
    /// </summary>
    public static class SoundBus
    {
        private const int ChannelCount = 5;

        private static readonly float[] Levels = { 0.9f, 1f, 0.55f, 0.45f, 1f };
        private static readonly float[] Duck = { 1f, 1f, 1f, 1f, 1f };
        private static readonly float[] DuckTarget = { 1f, 1f, 1f, 1f, 1f };
        private static readonly float[] DuckHold = new float[ChannelCount];

        /// <summary>How fast a duck engages and releases, in units per second.</summary>
        public const float AttackPerSecond = 6f;
        public const float ReleasePerSecond = 1.4f;

        /// <summary>Static level for a channel before user volume and ducking.</summary>
        public static float Level(SoundChannel channel) => Levels[(int)channel];

        public static void SetLevel(SoundChannel channel, float level) =>
            Levels[(int)channel] = Mathf.Clamp01(level);

        /// <summary>Final multiplier for a source on this channel right now.</summary>
        public static float Volume(SoundChannel channel)
        {
            int i = (int)channel;
            float user = UserVolume(channel);
            return Mathf.Clamp01(Levels[i] * user * Duck[i]) * Mathf.Clamp01(DemoSettings.Master);
        }

        private static float UserVolume(SoundChannel channel)
        {
            switch (channel)
            {
                case SoundChannel.Ambient:
                case SoundChannel.Music:
                    return Mathf.Clamp01(DemoSettings.Ambient);
                default:
                    return Mathf.Clamp01(DemoSettings.Sfx);
            }
        }

        /// <summary>
        /// Pull a channel down for a moment. Used so the Overseer and critical alerts cut through
        /// ambience and music instead of competing with them.
        /// </summary>
        public static void DuckFor(SoundChannel channel, float amount, float seconds)
        {
            int i = (int)channel;
            float target = Mathf.Clamp01(1f - Mathf.Clamp01(amount));
            // Never let a shallower duck cancel a deeper one that is still holding.
            DuckTarget[i] = Mathf.Min(DuckTarget[i], target);
            DuckHold[i] = Mathf.Max(DuckHold[i], seconds);
        }

        /// <summary>Duck ambience and music together, which is the usual case.</summary>
        public static void DuckBed(float amount, float seconds)
        {
            DuckFor(SoundChannel.Ambient, amount, seconds);
            DuckFor(SoundChannel.Music, amount, seconds);
        }

        public static void Tick(float dt)
        {
            if (dt <= 0f) return;

            for (int i = 0; i < ChannelCount; i++)
            {
                // Move toward the target before expiring the hold. Expiring first meant a duck
                // shorter than one frame was released before it ever engaged, so a quick alert
                // silently failed to drop the bed.
                float rate = Duck[i] > DuckTarget[i] ? AttackPerSecond : ReleasePerSecond;
                Duck[i] = Mathf.MoveTowards(Duck[i], DuckTarget[i], rate * dt);

                if (DuckHold[i] <= 0f) continue;

                DuckHold[i] -= dt;
                if (DuckHold[i] <= 0f)
                {
                    DuckHold[i] = 0f;
                    DuckTarget[i] = 1f;
                }
            }
        }

        /// <summary>Current duck multiplier, for tests and debug readouts.</summary>
        public static float DuckAmount(SoundChannel channel) => Duck[(int)channel];

        public static void ResetAll()
        {
            for (int i = 0; i < ChannelCount; i++)
            {
                Duck[i] = 1f;
                DuckTarget[i] = 1f;
                DuckHold[i] = 0f;
            }
        }
    }
}
