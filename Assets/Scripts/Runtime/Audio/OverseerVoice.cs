using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Synthesised speech for the Overseer and its robots.
    ///
    /// The player character is an AI and the workforce is machines, so vocoded synthesis is the
    /// correct voice for this fiction rather than a budget compromise for missing voice actors.
    /// Utterances are built from formant bursts whose count and pitch derive from the line's text,
    /// which means the same message always sounds the same and longer messages take longer to say —
    /// enough for the ear to read it as language without any recorded audio.
    /// </summary>
    public static class OverseerVoice
    {
        private const int SampleRate = 22050;

        /// <summary>Utterances are capped so a long log line does not talk over the game.</summary>
        private const int MaxSyllables = 7;

        private static readonly Dictionary<int, AudioClip> Cache = new Dictionary<int, AudioClip>(32);
        private static AudioSource _source;
        private static float _lastSpoke;

        /// <summary>Minimum gap between lines; the Overseer should not gabble.</summary>
        public const float Cooldown = 1.4f;

        public static bool Enabled { get; set; } = true;

        /// <summary>Speak a HUD line. Severity sets pitch and how hard it ducks the bed.</summary>
        public static void Speak(string line, AlertSeverity severity = AlertSeverity.Info)
        {
            if (!Enabled || string.IsNullOrEmpty(line)) return;
            if (Time.unscaledTime - _lastSpoke < Cooldown) return;

            EnsureSource();
            if (_source == null) return;

            _lastSpoke = Time.unscaledTime;

            AudioClip clip = ClipFor(line, severity);
            if (clip == null) return;

            // Pull the bed down so the voice is intelligible over ambience and music.
            SoundBus.DuckBed(severity >= AlertSeverity.Warning ? 0.6f : 0.4f, clip.length + 0.35f);

            _source.volume = SoundBus.Volume(SoundChannel.Voice) * 0.55f;
            _source.PlayOneShot(clip);
        }

        /// <summary>Short affirmative chirp for a robot accepting work.</summary>
        public static void Chirp(Vector3 at, bool positive)
        {
            if (!Enabled) return;
            AudioClip clip = ChirpClip(positive);
            SpatialAudio.PlayAt(clip, at, 0.5f, SoundChannel.Voice, 0.10f);
        }

        private static void EnsureSource()
        {
            if (_source != null) return;

            var go = GameObject.Find("SM_Voice");
            if (go == null)
            {
                go = new GameObject("SM_Voice");
                Object.DontDestroyOnLoad(go);
            }

            _source = go.GetComponent<AudioSource>();
            if (_source == null) _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.loop = false;
        }

        private static AudioClip ClipFor(string line, AlertSeverity severity)
        {
            int key = line.GetHashCode() ^ ((int)severity << 24);
            if (Cache.TryGetValue(key, out AudioClip cached) && cached != null)
                return cached;

            AudioClip clip = Synthesize(line, severity);
            if (Cache.Count > 48) Cache.Clear();
            Cache[key] = clip;
            return clip;
        }

        /// <summary>
        /// One formant burst per syllable, pitch-walked by a hash of the text so each line has a
        /// stable "intonation". Ring modulation gives the metallic vocoder character.
        /// </summary>
        private static AudioClip Synthesize(string line, AlertSeverity severity)
        {
            int syllables = Mathf.Clamp(1 + CountWords(line), 2, MaxSyllables);
            float syllableSeconds = 0.085f;
            int perSyllable = Mathf.RoundToInt(SampleRate * syllableSeconds);
            int count = perSyllable * syllables;
            var data = new float[count];

            // Urgency raises the base pitch; a crisis line should sound clipped and higher.
            float baseHz = severity >= AlertSeverity.Warning ? 168f : 132f;
            uint hash = (uint)line.GetHashCode();

            for (int s = 0; s < syllables; s++)
            {
                hash = hash * 1664525u + 1013904223u;
                float step = ((hash >> 16) % 7) - 3f;
                float hz = baseHz * Mathf.Pow(1.0595f, step);

                for (int i = 0; i < perSyllable; i++)
                {
                    float t = i / (float)SampleRate;
                    float phase = 2f * Mathf.PI * hz * t;

                    // Two formants plus a carrier: cheap vowel-ish colour.
                    float f0 = Mathf.Sin(phase);
                    float f1 = Mathf.Sin(phase * 2.4f) * 0.5f;
                    float f2 = Mathf.Sin(phase * 3.9f) * 0.25f;
                    float carrier = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 92f * t));

                    // Attack-decay so each burst reads as a struck consonant, not a tone.
                    float k = i / (float)perSyllable;
                    float env = Mathf.Min(k * 12f, 1f) * Mathf.Exp(-k * 3.4f);

                    data[s * perSyllable + i] = (f0 + f1 + f2) * carrier * env * 0.22f;
                }
            }

            var clip = AudioClip.Create("voice_" + syllables, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip ChirpClip(bool positive)
        {
            int key = positive ? -1 : -2;
            if (Cache.TryGetValue(key, out AudioClip cached) && cached != null)
                return cached;

            int count = SampleRate / 12;
            var data = new float[count];
            float from = positive ? 640f : 520f;
            float to = positive ? 980f : 360f;

            for (int i = 0; i < count; i++)
            {
                float k = i / (float)count;
                float t = i / (float)SampleRate;
                float hz = Mathf.Lerp(from, to, k);
                float env = Mathf.Min(k * 20f, 1f) * Mathf.Exp(-k * 5f);
                data[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * env * 0.3f;
            }

            var clip = AudioClip.Create(positive ? "chirp_yes" : "chirp_no", count, 1, SampleRate, false);
            clip.SetData(data, 0);
            Cache[key] = clip;
            return clip;
        }

        private static int CountWords(string line)
        {
            int words = 0;
            bool inWord = false;
            for (int i = 0; i < line.Length; i++)
            {
                bool space = char.IsWhiteSpace(line[i]);
                if (!space && !inWord) words++;
                inWord = !space;
            }
            return words;
        }
    }
}
