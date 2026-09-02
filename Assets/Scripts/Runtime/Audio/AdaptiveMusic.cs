using UnityEngine;

namespace SolarMajesty
{
    public enum MusicMood
    {
        /// <summary>Empty drop, nothing built, nothing threatening.</summary>
        Calm = 0,
        /// <summary>Colony is running: machinery, construction, trade.</summary>
        Work = 1,
        /// <summary>Fauna pressure rising or resources failing.</summary>
        Tension = 2,
        /// <summary>Something is actively being lost.</summary>
        Crisis = 3
    }

    /// <summary>
    /// Layered generative score.
    ///
    /// A composer is out of reach for this project, and silence reads as unfinished. Instead of
    /// looping one track, this synthesises four stems once at startup — a drone, a pulse, a pad, and
    /// a low brass swell — and crossfades their levels with the state of the colony. The result
    /// responds to play rather than repeating, which is what a colony sim score is supposed to do.
    ///
    /// All stems share a tempo and key so any combination is consonant.
    /// </summary>
    public sealed class AdaptiveMusic : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const float LoopSeconds = 16f;

        /// <summary>A minor: the "industrial but hopeful" brief lands between modal and minor.</summary>
        private static readonly float[] Scale = { 110.00f, 130.81f, 146.83f, 164.81f, 196.00f };

        private static AdaptiveMusic _instance;

        private AudioSource _drone;
        private AudioSource _pulse;
        private AudioSource _pad;
        private AudioSource _swell;

        private readonly float[] _targets = new float[4];
        private readonly float[] _levels = new float[4];

        private MusicMood _mood = MusicMood.Calm;
        private float _moodHold;

        public MusicMood Mood => _mood;

        public static AdaptiveMusic Ensure()
        {
            if (_instance != null) return _instance;

            var go = GameObject.Find("SM_Music");
            if (go == null)
            {
                go = new GameObject("SM_Music");
                DontDestroyOnLoad(go);
            }

            _instance = go.GetComponent<AdaptiveMusic>();
            if (_instance == null) _instance = go.AddComponent<AdaptiveMusic>();
            return _instance;
        }

        private void Awake()
        {
            _instance = this;
            _drone = BuildStem("Drone", BuildDrone());
            _pulse = BuildStem("Pulse", BuildPulse());
            _pad = BuildStem("Pad", BuildPad());
            _swell = BuildStem("Swell", BuildSwell());
            SetMood(MusicMood.Calm, force: true);
        }

        private AudioSource BuildStem(string label, AudioClip clip)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);

            var src = child.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            src.Play();
            return src;
        }

        /// <summary>
        /// Pick the mood from colony state. Hysteresis stops the score flapping between stems every
        /// time a single mite wanders near the campus.
        /// </summary>
        public void Evaluate(GameLoop loop, float dt)
        {
            if (loop == null) return;

            _moodHold -= dt;
            MusicMood next = Classify(loop);

            if (next > _mood)
            {
                // Escalate immediately; a crisis should be heard the moment it starts.
                SetMood(next, force: false);
                _moodHold = 6f;
            }
            else if (next < _mood && _moodHold <= 0f)
            {
                SetMood(next, force: false);
                _moodHold = 4f;
            }

            Apply(dt);
        }

        private static MusicMood Classify(GameLoop loop)
        {
            if (loop.Alerts != null && loop.Alerts.CriticalCount > 0)
                return MusicMood.Crisis;

            float threat = loop.Threat != null ? loop.Threat.Current : 0f;
            if (threat > 0.55f) return MusicMood.Crisis;
            if (threat > 0.22f) return MusicMood.Tension;

            bool ice = loop.Resources != null && loop.Resources.Get(ResourceId.WaterIce) < OverseerRules.IceDeathThreshold;
            if (ice) return MusicMood.Tension;

            int modules = loop.Placer != null ? loop.Placer.Pieces.Count : 0;
            return modules > 2 ? MusicMood.Work : MusicMood.Calm;
        }

        public void SetMood(MusicMood mood, bool force)
        {
            _mood = mood;

            switch (mood)
            {
                case MusicMood.Work:
                    Set(0.55f, 0.42f, 0.30f, 0f);
                    break;
                case MusicMood.Tension:
                    Set(0.60f, 0.30f, 0.45f, 0.18f);
                    break;
                case MusicMood.Crisis:
                    Set(0.45f, 0.62f, 0.20f, 0.55f);
                    break;
                default:
                    Set(0.50f, 0f, 0.34f, 0f);
                    break;
            }

            if (!force) return;
            for (int i = 0; i < _levels.Length; i++)
                _levels[i] = _targets[i];
        }

        private void Set(float drone, float pulse, float pad, float swell)
        {
            _targets[0] = drone;
            _targets[1] = pulse;
            _targets[2] = pad;
            _targets[3] = swell;
        }

        private void Apply(float dt)
        {
            // Slow crossfades: a score that snaps between layers sounds like a bug.
            float rate = dt * 0.55f;
            for (int i = 0; i < _levels.Length; i++)
                _levels[i] = Mathf.MoveTowards(_levels[i], _targets[i], rate);

            float bus = SoundBus.Volume(SoundChannel.Music);
            if (_drone != null) _drone.volume = _levels[0] * bus;
            if (_pulse != null) _pulse.volume = _levels[1] * bus;
            if (_pad != null) _pad.volume = _levels[2] * bus;
            if (_swell != null) _swell.volume = _levels[3] * bus;
        }

        // ---- stem synthesis --------------------------------------------------

        private static AudioClip MakeClip(string name, System.Func<float, int, float> sample)
        {
            int count = Mathf.RoundToInt(SampleRate * LoopSeconds);
            var data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = sample(t, i);
            }

            // Fade the loop seam so it does not click every 16 seconds.
            int fade = SampleRate / 8;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] *= k;
                data[count - 1 - i] *= k;
            }

            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Detuned low sine pair. The bed everything else sits on.</summary>
        private static AudioClip BuildDrone()
        {
            return MakeClip("music_drone", (t, i) =>
            {
                float a = Mathf.Sin(2f * Mathf.PI * Scale[0] * t);
                float b = Mathf.Sin(2f * Mathf.PI * (Scale[0] * 1.004f) * t);
                float sub = Mathf.Sin(2f * Mathf.PI * (Scale[0] * 0.5f) * t) * 0.5f;
                float breathe = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / LoopSeconds);
                return (a + b + sub) * 0.10f * breathe;
            });
        }

        /// <summary>Muted plucked eighths. Reads as machinery working.</summary>
        private static AudioClip BuildPulse()
        {
            const float bpm = 84f;
            float beat = 60f / bpm / 2f;

            return MakeClip("music_pulse", (t, i) =>
            {
                int step = Mathf.FloorToInt(t / beat);
                float phase = (t - step * beat) / beat;

                // Sparse, non-repeating pattern from a cheap hash rather than a fixed loop.
                float gate = ((step * 2654435761u) % 100u) / 100f;
                if (gate > 0.55f) return 0f;

                float note = Scale[(step * 3) % Scale.Length] * 2f;
                float env = Mathf.Exp(-phase * 7f);
                float tone = Mathf.Sin(2f * Mathf.PI * note * t);
                float click = Mathf.Sin(2f * Mathf.PI * note * 3f * t) * 0.2f;
                return (tone + click) * env * 0.09f;
            });
        }

        /// <summary>Slow chord pad. The hopeful colonisation half of the brief.</summary>
        private static AudioClip BuildPad()
        {
            return MakeClip("music_pad", (t, i) =>
            {
                float chord = 0f;
                chord += Mathf.Sin(2f * Mathf.PI * Scale[1] * 2f * t);
                chord += Mathf.Sin(2f * Mathf.PI * Scale[3] * 2f * t) * 0.8f;
                chord += Mathf.Sin(2f * Mathf.PI * Scale[4] * 2f * t) * 0.6f;

                float swellA = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / LoopSeconds - 1.2f);
                float shimmer = 0.9f + 0.1f * Mathf.Sin(2f * Mathf.PI * 0.7f * t);
                return chord * 0.045f * swellA * shimmer;
            });
        }

        /// <summary>Dissonant low swell with noise. Only present in crisis.</summary>
        private static AudioClip BuildSwell()
        {
            return MakeClip("music_swell", (t, i) =>
            {
                float low = Mathf.Sin(2f * Mathf.PI * Scale[0] * 0.75f * t);
                float clash = Mathf.Sin(2f * Mathf.PI * Scale[0] * 0.78f * t);
                float noise = (Mathf.PerlinNoise(t * 40f, 0.3f) - 0.5f) * 0.6f;
                float rise = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / LoopSeconds - 1.6f), 2f);
                return (low + clash + noise) * 0.075f * rise;
            });
        }
    }
}
