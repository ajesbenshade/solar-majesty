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
    /// Layered adaptive score.
    ///
    /// Each world ships four authored stems rendered offline by Tools/audio/render_music.py:
    /// bed (harmonic foundation), rhythm (work pulse), harmony (the hopeful leitmotif) and threat
    /// (percussion and low swells). They share key, tempo and exact sample length, so they are
    /// started on one DSP tick and crossfaded by the colony's mood. The score responds to play
    /// instead of repeating one track, which is what a colony sim score is supposed to do.
    ///
    /// The original procedural sine stems are kept as a fallback. A world with any stem missing
    /// uses the whole procedural set, because mixing two keys would be worse than a thin score.
    /// </summary>
    public sealed class AdaptiveMusic : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const float LoopSeconds = 16f;
        private const int StemCount = 4;

        private const string ResourceRoot = "Audio/Music/";
        private const string TitleClip = "title_theme";
        private const string ProceduralKey = "procedural";

        /// <summary>Resource suffixes, in stem index order (bed, rhythm, harmony, threat).</summary>
        private static readonly string[] StemNames = { "bed", "rhythm", "harmony", "threat" };

        /// <summary>
        /// Per-mood stem levels for the authored stems, rows Calm/Work/Tension/Crisis.
        ///
        /// Must match MOOD_LEVELS in Tools/audio/render_music.py. The renderer normalises a world so
        /// the Work row lands near -17 LUFS, and proves that every layer subset at the per-stem
        /// maximum of this table peaks under -1 dBFS. A mix is linear in these levels, so that also
        /// covers every value a crossfade passes through: no mood change can clip.
        /// </summary>
        private static readonly float[,] AuthoredLevels =
        {
            { 1.00f, 0.00f, 0.70f, 0.00f },
            { 0.90f, 0.90f, 0.75f, 0.00f },
            { 0.85f, 0.60f, 0.45f, 0.65f },
            { 0.75f, 1.00f, 0.25f, 1.00f }
        };

        /// <summary>The original balance, tuned for the quieter procedural stems.</summary>
        private static readonly float[,] ProceduralLevels =
        {
            { 0.50f, 0.00f, 0.34f, 0.00f },
            { 0.55f, 0.42f, 0.30f, 0.00f },
            { 0.60f, 0.30f, 0.45f, 0.18f },
            { 0.45f, 0.62f, 0.20f, 0.55f }
        };

        /// <summary>A world change is a scene change; a long overlap hides the key change.</summary>
        private const float WorldFadeSeconds = 3f;
        private const float TitleFadeSeconds = 2.5f;

        /// <summary>
        /// Head start for PlayScheduled. Streamed clips need their first buffer decoded before the
        /// scheduled tick, or a stem that was late would start out of phase.
        /// </summary>
        private const double ScheduleLead = 0.25;
        private const float LoadTimeout = 5f;

        /// <summary>
        /// Stems further apart than this are resynced. Far above any timeSamples readback jitter,
        /// so it only fires when a stem really started late (a load hitch), never in steady state.
        /// </summary>
        private const int DriftToleranceSamples = SampleRate / 4;

        /// <summary>Stinger over a ducked bed: 0.85 + 0.2 of two -1 dBFS files stays under 0 dBFS.</summary>
        private const float StingLevel = 0.85f;
        private const float StingDuck = 0.2f;

        /// <summary>A, C, D, E, G around A2: the fallback set's A minor pentatonic.</summary>
        private static readonly float[] Scale = { 110.00f, 130.81f, 146.83f, 164.81f, 196.00f };

        private static AdaptiveMusic _instance;
        private static AudioClip[] _proceduralClips;

        /// <summary>One world's four phase-locked stems and its share of a world crossfade.</summary>
        private sealed class StemSet
        {
            public string Key;
            public bool Authored;
            public readonly AudioSource[] Sources = new AudioSource[StemCount];
            public float Gain;
            public bool Scheduled;
            public double StartDsp;
            public float WaitedSeconds;
        }

        private StemSet _active;
        private StemSet _fading;
        private bool _hasWorld;
        private CelestialBodyId _world;

        private AudioSource _title;
        private bool _titleLoaded;
        private float _titleGain;

        private AudioSource _sting;
        private AudioClip _victoryClip;
        private AudioClip _defeatClip;
        private float _stingDuck = 1f;
        private float _stingHold;

        private float _driftTimer;
        private int _driftStrikes;

        private readonly float[] _targets = new float[StemCount];
        private readonly float[] _levels = new float[StemCount];

        private MusicMood _mood = MusicMood.Calm;
        private float _moodHold;

        public MusicMood Mood => _mood;

        /// <summary>Whether a world has been chosen yet, and which one.</summary>
        public bool HasWorld => _hasWorld;
        public CelestialBodyId World => _world;

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

        /// <summary>
        /// Play the victory or defeat stinger over the score, ducking the stems while it sounds.
        /// Safe to call before any world has loaded. Returns false if the stinger clip is missing, so
        /// the caller can fall back to the SFX fanfare instead of playing both.
        /// </summary>
        public static bool PlayStinger(bool victory)
        {
            var music = Ensure();
            return music != null && music.Stinger(victory);
        }

        private void Awake()
        {
            _instance = this;
            _sting = CreateSource("Stinger", loop: false);
            SetMood(MusicMood.Calm, force: true);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Pick the mood from colony state and follow the active world. Hysteresis stops the score
        /// flapping between stems every time a single mite wanders near the campus.
        /// </summary>
        public void Evaluate(GameLoop loop, float dt)
        {
            if (loop == null) return;

            var body = loop.BodyProfile;
            if (body != null) SetWorld(body.Id);

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
        }

        private static MusicMood Classify(GameLoop loop)
        {
            if (loop.Alerts != null && loop.Alerts.CriticalCount > 0)
                return MusicMood.Crisis;

            float threat = loop.Threat != null ? loop.Threat.Current : 0f;
            if (threat > 0.55f) return MusicMood.Crisis;
            if (threat > 0.22f) return MusicMood.Tension;

            if (loop.PayrollThin) return MusicMood.Tension;

            int modules = loop.Placer != null ? loop.Placer.Pieces.Count : 0;
            return modules > 2 ? MusicMood.Work : MusicMood.Calm;
        }

        public void SetMood(MusicMood mood, bool force)
        {
            _mood = mood;

            float[,] table = _active != null && !_active.Authored ? ProceduralLevels : AuthoredLevels;
            int row = (int)mood;
            for (int i = 0; i < StemCount; i++)
                _targets[i] = table[row, i];

            if (!force) return;
            for (int i = 0; i < _levels.Length; i++)
                _levels[i] = _targets[i];
        }

        /// <summary>
        /// Crossfade to a world's stems. Evaluate calls this every frame, so it does nothing unless
        /// the world actually changed.
        /// </summary>
        public void SetWorld(CelestialBodyId world)
        {
            if (_hasWorld && _world == world && _active != null) return;
            _hasWorld = true;
            _world = world;

            string key = WorldKey(world);
            if (_active != null && _active.Key == key) return;

            if (_fading != null && _fading.Key == key)
            {
                // Coming straight back: reverse the fade instead of loading the clips twice.
                var back = _fading;
                _fading = _active;
                _active = back;
                SetMood(_mood, force: false);
                return;
            }

            BeginSet(LoadSet(key));
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            TickTitle(dt);
            TrySchedule(_active, dt);
            TrySchedule(_fading, dt);
            TickCrossfade(dt);
            TickStinger(dt);
            GuardSync(dt);
            Apply(dt);
        }

        // ---- world sets ---------------------------------------------------------------------

        private static string WorldKey(CelestialBodyId world) => world.ToString().ToLowerInvariant();

        private void BeginSet(StemSet set)
        {
            // Only two sets ever overlap; a third request drops the one already fading out.
            if (_fading != null) Release(_fading);
            _fading = _active;
            _active = set;
            _active.Gain = 0f;
            SetMood(_mood, force: false);
        }

        private StemSet LoadSet(string key)
        {
            var clips = new AudioClip[StemCount];
            bool complete = key != ProceduralKey;
            for (int i = 0; complete && i < StemCount; i++)
            {
                clips[i] = Resources.Load<AudioClip>(ResourceRoot + key + "_" + StemNames[i]);
                if (clips[i] == null) complete = false;
            }

            if (!complete)
            {
                for (int i = 0; i < StemCount; i++)
                    if (clips[i] != null) Resources.UnloadAsset(clips[i]);
                clips = ProceduralClips();
                key = ProceduralKey;
            }
            else
            {
                for (int i = 1; i < StemCount; i++)
                {
                    if (clips[i].samples == clips[0].samples) continue;
                    Debug.LogWarning($"[AdaptiveMusic] {key} stems differ in length " +
                                     $"({clips[0].samples} vs {clips[i].samples}); they will drift.");
                    break;
                }
            }

            var set = new StemSet { Key = key, Authored = complete };
            for (int i = 0; i < StemCount; i++)
            {
                var src = CreateSource($"{key}_{StemNames[i]}", loop: true);
                src.clip = clips[i];
                if (complete) clips[i].LoadAudioData();
                set.Sources[i] = src;
            }
            return set;
        }

        /// <summary>
        /// Start all four stems on the same future DSP tick once their audio data is ready. Play()
        /// on each would start them in whichever audio frame each call landed in, so they would not
        /// line up. PlayScheduled is sample-accurate.
        /// </summary>
        private static void TrySchedule(StemSet set, float dt)
        {
            if (set == null || set.Scheduled) return;

            set.WaitedSeconds += dt;
            if (set.WaitedSeconds < LoadTimeout)
            {
                for (int i = 0; i < StemCount; i++)
                {
                    var clip = set.Sources[i].clip;
                    if (clip != null && clip.loadState == AudioDataLoadState.Loading) return;
                }
            }

            double start = AudioSettings.dspTime + ScheduleLead;
            for (int i = 0; i < StemCount; i++)
                set.Sources[i].PlayScheduled(start);
            set.Scheduled = true;
            set.StartDsp = start;
        }

        private void TickCrossfade(float dt)
        {
            if (_active == null) return;

            // The incoming set only starts fading in once it is actually sounding, so a slow load
            // does not fade the old world out into silence.
            if (_active.Scheduled && AudioSettings.dspTime >= _active.StartDsp)
                _active.Gain = Mathf.MoveTowards(_active.Gain, 1f, dt / WorldFadeSeconds);

            if (_fading == null) return;

            // Gains always sum to at most 1: a linear crossfade can never peak above either world.
            _fading.Gain = Mathf.Min(_fading.Gain, 1f - _active.Gain);
            if (_fading.Gain <= 0f)
            {
                Release(_fading);
                _fading = null;
            }
        }

        private void Release(StemSet set)
        {
            if (set == null) return;
            bool shared = (_active != null && _active != set && _active.Key == set.Key)
                          || (_fading != null && _fading != set && _fading.Key == set.Key);

            for (int i = 0; i < StemCount; i++)
            {
                var src = set.Sources[i];
                if (src == null) continue;
                var clip = src.clip;
                src.Stop();
                src.clip = null;
                if (set.Authored && !shared && clip != null) Resources.UnloadAsset(clip);
                Destroy(src.gameObject);
            }
        }

        /// <summary>
        /// Safety net only: resync stems that are grossly apart. Needs two consecutive strikes, so a
        /// single coarse timeSamples readback on a streamed clip cannot trigger a seek.
        /// </summary>
        private void GuardSync(float dt)
        {
            var set = _active;
            if (set == null || !set.Authored || !set.Scheduled) return;
            if (AudioSettings.dspTime < set.StartDsp + 1.0) return;

            _driftTimer -= dt;
            if (_driftTimer > 0f) return;
            _driftTimer = 2f;

            var lead = set.Sources[0];
            if (lead == null || lead.clip == null || !lead.isPlaying) return;

            int length = lead.clip.samples;
            int reference = lead.timeSamples;
            bool drifted = false;
            for (int i = 1; i < StemCount; i++)
            {
                var src = set.Sources[i];
                if (src == null || !src.isPlaying) continue;
                int d = Mathf.Abs(src.timeSamples - reference);
                d = Mathf.Min(d, length - d);
                if (d > DriftToleranceSamples) drifted = true;
            }

            if (!drifted)
            {
                _driftStrikes = 0;
                return;
            }

            if (++_driftStrikes < 2) return;
            _driftStrikes = 0;
            reference = lead.timeSamples;
            for (int i = 1; i < StemCount; i++)
                if (set.Sources[i] != null) set.Sources[i].timeSamples = reference;
            Debug.LogWarning($"[AdaptiveMusic] Resynced {set.Key} stems.");
        }

        // ---- title theme --------------------------------------------------------------------

        /// <summary>
        /// The orrery screen gets the full statement of the theme. This reads the title view rather
        /// than being hooked into it, so the menu code does not need to know music exists.
        /// </summary>
        private void TickTitle(float dt)
        {
            var view = SolarSystemTitleView.Instance;
            bool shown = view != null && view.IsShown;

            if (shown && !_titleLoaded)
            {
                _titleLoaded = true;
                var clip = Resources.Load<AudioClip>(ResourceRoot + TitleClip);
                if (clip != null)
                {
                    _title = CreateSource("Title", loop: true);
                    _title.clip = clip;
                }
            }

            // No title theme: a quiet procedural bed beats a silent menu.
            if (shown && _title == null && _active == null)
                BeginSet(LoadSet(ProceduralKey));

            float target = shown && _title != null ? 1f : 0f;
            if (target > 0f && !_title.isPlaying) _title.Play();
            _titleGain = Mathf.MoveTowards(_titleGain, target, dt / TitleFadeSeconds);
            if (_title != null && _titleGain <= 0f && _title.isPlaying) _title.Stop();
        }

        // ---- stinger ------------------------------------------------------------------------

        private bool Stinger(bool victory)
        {
            AudioClip clip = victory ? _victoryClip : _defeatClip;
            if (clip == null)
            {
                clip = Resources.Load<AudioClip>(ResourceRoot + (victory ? "sting_victory" : "sting_defeat"));
                if (victory) _victoryClip = clip;
                else _defeatClip = clip;
            }
            if (clip == null || _sting == null) return false;

            _sting.Stop();
            _sting.clip = clip;
            _sting.volume = StingLevel * SoundBus.Volume(SoundChannel.Music);
            _sting.Play();

            // Hold the duck for the body of the stinger and let the score swell back under its tail.
            _stingHold = Mathf.Max(0.5f, clip.length - 1.5f);
            return true;
        }

        private void TickStinger(float dt)
        {
            if (_stingHold > 0f)
            {
                _stingHold -= dt;
                _stingDuck = Mathf.MoveTowards(_stingDuck, StingDuck, dt * 5f);
            }
            else
            {
                _stingDuck = Mathf.MoveTowards(_stingDuck, 1f, dt * 0.4f);
            }
        }

        // ---- mix ----------------------------------------------------------------------------

        private void Apply(float dt)
        {
            // Slow crossfades: a score that snaps between layers sounds like a bug.
            float rate = dt * 0.55f;
            for (int i = 0; i < _levels.Length; i++)
                _levels[i] = Mathf.MoveTowards(_levels[i], _targets[i], rate);

            float bus = SoundBus.Volume(SoundChannel.Music);
            float music = bus * _stingDuck;
            float worlds = music * (1f - _titleGain);

            ApplySet(_active, worlds);
            ApplySet(_fading, worlds);
            if (_title != null) _title.volume = _titleGain * music;
            if (_sting != null && _sting.isPlaying) _sting.volume = StingLevel * bus;
        }

        private void ApplySet(StemSet set, float mul)
        {
            if (set == null) return;
            for (int i = 0; i < StemCount; i++)
            {
                var src = set.Sources[i];
                if (src != null) src.volume = _levels[i] * set.Gain * mul;
            }
        }

        private AudioSource CreateSource(string label, bool loop)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);

            var src = child.AddComponent<AudioSource>();
            src.loop = loop;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            // Music must never be the voice Unity steals when many SFX play at once.
            src.priority = 0;
            src.ignoreListenerPause = true;
            return src;
        }

        // ---- procedural fallback stems ------------------------------------------------------

        private static AudioClip[] ProceduralClips()
        {
            if (_proceduralClips != null) return _proceduralClips;
            _proceduralClips = new[] { BuildDrone(), BuildPulse(), BuildPad(), BuildSwell() };
            return _proceduralClips;
        }

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
