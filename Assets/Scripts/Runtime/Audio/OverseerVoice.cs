using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// The Overseer's (Grok's) voice.
    ///
    /// Scripted Grok lines are baked with Kokoro TTS (Tools/audio/render_voices.py) and found by
    /// their text, so anything that logs a catalogued line is spoken without knowing the bank
    /// exists. Lines with numbers in them go to the live voice server when it is up. Warnings with
    /// no voiced line get a short generic alert, and a build with no bank at all falls back to the
    /// original vocoder babble: formant bursts whose count and pitch derive from the text.
    /// </summary>
    public static class OverseerVoice
    {
        private const int SampleRate = 22050;

        /// <summary>Utterances are capped so a long log line does not talk over the game.</summary>
        private const int MaxSyllables = 7;

        /// <summary>A live line that takes longer than this to synthesise is stale advice; drop it.</summary>
        private const float LiveStaleSeconds = 6f;

        private static readonly Dictionary<int, AudioClip> Cache = new Dictionary<int, AudioClip>(32);
        private static readonly System.Random Rng = new System.Random();
        private static AudioSource _source;
        private static string _lastKey;
        private static float _lastSpoke = -100f;
        private static AudioClip _queued;
        private static int _queuedPriority;
        private static AlertSeverity _queuedSeverity;
        private static float _queuedUntil;
        private static float _queuedNotBefore;

        /// <summary>A line held for a busy Overseer is dropped if it can't start within this long.</summary>
        public const float QueueSeconds = 8f;

        /// <summary>The same line is not repeated within this window (log + alert paths both speak).</summary>
        public const float RepeatWindow = 4f;

        public static bool Enabled => DemoSettings.CharacterVoices;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _source = null;
            _lastKey = null;
            _lastSpoke = -100f;
            _queued = null;
            Cache.Clear();
        }

        /// <summary>Speak a HUD line. Severity sets priority, fallback, and how hard it ducks the bed.</summary>
        public static void Speak(string line, AlertSeverity severity = AlertSeverity.Info)
        {
            if (!Enabled || string.IsNullOrEmpty(line) || IsRepeat(line)) return;
            int priority = Priority(severity);

            if (CharacterVoice.Bank.TryGetLine(line, out VoiceClipInfo info))
            {
                PlayOrHold(CharacterVoice.LoadClip(info.Clip), priority, severity);
                return;
            }

            float asked = Time.unscaledTime;
            bool sent = VoiceServerLink.Request(VoiceBank.OverseerSpeaker, line, clip =>
            {
                if (clip != null && Time.unscaledTime - asked < LiveStaleSeconds && Play(clip, priority, severity))
                    return;
                Fallback(line, severity, priority);
            });
            if (!sent) Fallback(line, severity, priority);
        }

        /// <summary>
        /// Speak only if this exact line has a baked clip. For the Overseer log, where most lines are
        /// status chatter ("Haul deposited 40 EU") that should stay text.
        /// </summary>
        public static void SpeakIfScripted(string line)
        {
            if (!Enabled || string.IsNullOrEmpty(line)) return;
            if (!CharacterVoice.Bank.TryGetLine(line, out VoiceClipInfo info)) return;
            if (IsRepeat(line)) return;
            PlayOrHold(CharacterVoice.LoadClip(info.Clip), Priority(AlertSeverity.Info), AlertSeverity.Info);
        }

        /// <summary>
        /// Grok lessons often arrive back to back (first HAB, then first flag). Hold the newest
        /// scripted line for when the current one ends instead of losing it.
        /// </summary>
        private static void PlayOrHold(AudioClip clip, int priority, AlertSeverity severity)
        {
            if (clip == null || Play(clip, priority, severity)) return;
            Hold(clip, priority, severity, 0f);
        }

        private static void Hold(AudioClip clip, int priority, AlertSeverity severity, float delay)
        {
            // One slot: a newer line replaces an older held one unless the older one matters more.
            if (_queued != null && _queuedPriority > priority && Time.unscaledTime <= _queuedUntil) return;
            _queued = clip;
            _queuedPriority = priority;
            _queuedSeverity = severity;
            _queuedNotBefore = Time.unscaledTime + delay;
            _queuedUntil = _queuedNotBefore + QueueSeconds;
        }

        /// <summary>Start a held line once the Overseer is free. Called every frame by CharacterVoice.</summary>
        public static void Tick()
        {
            if (_queued == null) return;
            float now = Time.unscaledTime;
            if (now > _queuedUntil || !Enabled)
            {
                _queued = null;
                return;
            }
            if (now < _queuedNotBefore || CharacterVoice.Director.OverseerSpeaking(now)) return;
            AudioClip clip = _queued;
            _queued = null;
            Play(clip, _queuedPriority, _queuedSeverity);
        }

        /// <summary>
        /// Speak one of the Overseer's generic cues (victory, defeat, robot down, …). A delay holds the
        /// line back, e.g. until a music stinger has finished, so the voice isn't buried under it.
        /// </summary>
        public static void SpeakCue(VoiceCue cue, AlertSeverity severity = AlertSeverity.Warning, float delay = 0f)
        {
            if (!Enabled) return;
            if (!CharacterVoice.Bank.TryPickBark(VoiceBank.OverseerSpeaker, cue, Rng, out VoiceClipInfo info)) return;
            AudioClip clip = CharacterVoice.LoadClip(info.Clip);
            if (clip == null) return;
            if (delay <= 0f)
            {
                Play(clip, Priority(severity), severity);
                return;
            }
            Hold(clip, Priority(severity), severity, delay);
            CharacterVoice.Ensure(); // its Update drives Tick
        }

        private static int Priority(AlertSeverity severity) =>
            severity >= AlertSeverity.Critical ? 3 : severity >= AlertSeverity.Warning ? 2 : 1;

        private static bool IsRepeat(string line)
        {
            string key = VoiceBank.LineKey(line);
            float now = Time.unscaledTime;
            if (key == _lastKey && now - _lastSpoke < RepeatWindow) return true;
            _lastKey = key;
            _lastSpoke = now;
            return false;
        }

        private static void Fallback(string line, AlertSeverity severity, int priority)
        {
            if (severity < AlertSeverity.Warning) return;
            VoiceCue cue = line.IndexOf("destroyed", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? VoiceCue.ModuleLost
                : severity >= AlertSeverity.Critical ? VoiceCue.AlertCritical : VoiceCue.AlertWarning;
            if (CharacterVoice.Bank.TryPickBark(VoiceBank.OverseerSpeaker, cue, Rng, out VoiceClipInfo info) &&
                Play(CharacterVoice.LoadClip(info.Clip), priority, severity))
                return;
            Play(ClipFor(line, severity), priority, severity);
        }

        private static bool Play(AudioClip clip, int priority, AlertSeverity severity)
        {
            if (clip == null) return false;
            EnsureSource();
            if (_source == null) return false;
            if (!CharacterVoice.Director.TryStartOverseer(Time.unscaledTime, clip.length, priority)) return false;

            // Pull the bed down so the voice is intelligible over ambience and music.
            SoundBus.DuckBed(severity >= AlertSeverity.Warning ? 0.6f : 0.45f, clip.length + 0.35f);

            _source.Stop();
            _source.clip = clip;
            _source.volume = SoundBus.Volume(SoundChannel.Voice);
            _source.Play();
            return true;
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
