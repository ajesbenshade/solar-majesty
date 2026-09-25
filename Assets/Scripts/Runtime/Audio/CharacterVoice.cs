using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Spoken hero barks and the shared voice bank.
    ///
    /// Heroes act on their own, so their voices are how the player hears the colony think:
    /// "Paid work. Finally." when a bounty is taken tells you more than a ring on the ground.
    /// Barks are baked offline (Tools/audio/render_voices.py) and ship in Resources, so this works
    /// with nothing else running. When the local LLM narrator and voice server are both up, a
    /// hero's big moments are spoken in their own words instead, with the bark as the fallback.
    ///
    /// Voices follow the robot that is speaking, partly spatialised so a claim across the map is
    /// still heard but reads as "over there". The selected hero is closer to 2D, like a radio link.
    /// </summary>
    public sealed class CharacterVoice : MonoBehaviour
    {
        private const int HeroVoices = 3;
        /// <summary>How long a hero waits for its LLM line before falling back to a bark.</summary>
        private const float NarrationWait = 4.5f;

        private static CharacterVoice _instance;
        private static VoiceBank _bank;
        private static bool _bankLoaded;

        private readonly VoiceDirector _director = new VoiceDirector();
        private readonly System.Random _rng = new System.Random(20260925);
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(64);
        private readonly Dictionary<int, Pending> _pending = new Dictionary<int, Pending>();
        private readonly List<int> _expired = new List<int>(4);
        private readonly AudioSource[] _sources = new AudioSource[HeroVoices];
        private readonly Transform[] _following = new Transform[HeroVoices];
        private GameLoop _loop;

        private struct Pending
        {
            public SpecialistAgent Agent;
            public VoiceCue Cue;
            public int Stamp;
            public float Deadline;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _bank = null;
            _bankLoaded = false;
        }

        /// <summary>Baked voices on (Settings → CHARACTER VOICES).</summary>
        public static bool Enabled => DemoSettings.CharacterVoices;

        /// <summary>The baked bank, loaded once from Resources/Audio/Voices/voices.json.</summary>
        public static VoiceBank Bank
        {
            get
            {
                if (_bankLoaded) return _bank;
                _bankLoaded = true;
                var manifest = Resources.Load<TextAsset>("Audio/Voices/voices");
                _bank = VoiceBank.Parse(manifest != null ? manifest.text : null);
                if (_bank.Count == 0)
                    Debug.LogWarning("[Voice] no baked voice bank; run Tools/audio/render_voices.py");
                return _bank;
            }
        }

        /// <summary>Shared rationing between hero barks and the Overseer.</summary>
        public static VoiceDirector Director => Ensure()._director;

        public static CharacterVoice Ensure()
        {
            if (_instance != null) return _instance;
            var go = GameObject.Find("SM_CharacterVoice");
            if (go == null)
            {
                go = new GameObject("SM_CharacterVoice");
                DontDestroyOnLoad(go);
            }
            _instance = go.GetComponent<CharacterVoice>();
            if (_instance == null) _instance = go.AddComponent<CharacterVoice>();
            return _instance;
        }

        /// <summary>Load (and cache) a baked clip by its resource name.</summary>
        public static AudioClip LoadClip(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var self = Ensure();
            if (self._clips.TryGetValue(name, out AudioClip clip)) return clip;
            clip = Resources.Load<AudioClip>("Audio/Voices/" + name);
            self._clips[name] = clip;
            return clip;
        }

        /// <summary>
        /// Something happened to this hero worth a bark. With <paramref name="awaitNarration"/>
        /// (the LLM narrator accepted the moment) and the live voice server up, the hero waits for
        /// its own line and only barks if that line doesn't arrive in time.
        /// </summary>
        public static void Cue(SpecialistAgent agent, VoiceCue cue, bool awaitNarration = false)
        {
            if (!Enabled || agent == null || agent.Data == null) return;
            if (cue != VoiceCue.Down && !agent.IsAlive) return;
            var self = Ensure();

            if (awaitNarration && HeroSpeaker.Ready)
            {
                self._pending[agent.GetHashCode()] = new Pending
                {
                    Agent = agent,
                    Cue = cue,
                    Stamp = agent.NarrationStamp,
                    Deadline = Time.unscaledTime + NarrationWait
                };
                return;
            }
            self.Bark(agent, cue);
        }

        /// <summary>
        /// The narrator produced a line for a moment we deferred. Voice it live, or bark on failure.
        /// Lines for moments that were never deferred are text-only, as before.
        /// </summary>
        public static void SpeakNarrated(SpecialistAgent agent, string line)
        {
            if (_instance == null || agent == null) return;
            var self = _instance;
            int id = agent.GetHashCode();
            if (!self._pending.TryGetValue(id, out Pending p)) return;
            self._pending.Remove(id);

            HeroVoiceSpec voice = LiveVoice(agent, out float playbackPitch);
            bool sent = HeroSpeaker.Request(voice, line, clip =>
            {
                if (agent == null) return;
                if (clip == null)
                {
                    self.Bark(agent, p.Cue);
                    return;
                }
                self.Play(agent, p.Cue, clip, null, playbackPitch);
                Destroy(clip, clip.length / playbackPitch + 1f);
            });
            if (!sent) self.Bark(agent, p.Cue);
        }

        /// <summary>
        /// The live voice for a hero: the same cast entry the baked barks were rendered with, so a
        /// robot sounds like itself either way. Without a cast in the manifest, HeroSpeech's own
        /// casting is the fallback.
        /// </summary>
        private static HeroVoiceSpec LiveVoice(SpecialistAgent agent, out float playbackPitch)
        {
            if (Bank.TryGetCast(VoiceBank.SpeakerFor(agent.Data.specialistClass), out VoiceCast cast))
                return VoiceBank.LiveSpec(cast, out playbackPitch);
            playbackPitch = 1f;
            return HeroSpeech.VoiceFor(agent.Data.specialistClass, agent.GetHashCode(), agent.Data.workaholicBias);
        }

        private void Bark(SpecialistAgent agent, VoiceCue cue)
        {
            var bank = Bank;
            string speaker = VoiceBank.SpeakerFor(agent.Data.specialistClass);
            if (!bank.TryPickBark(speaker, cue, _rng, out VoiceClipInfo info)) return;
            AudioClip clip = LoadClip(info.Clip);
            if (clip == null) return;
            Play(agent, cue, clip, info.Text);
        }

        private void Play(SpecialistAgent agent, VoiceCue cue, AudioClip clip, string subtitle, float pitch = 1f)
        {
            if (_loop == null) _loop = FindAnyObjectByType<GameLoop>();
            bool selected = _loop != null && _loop.IsSelected(agent);
            float now = Time.unscaledTime;
            if (!_director.TryStartHero(agent.GetHashCode(), cue, selected, now, clip.length / pitch)) return;

            int slot = FreeSlot();
            AudioSource src = _sources[slot];
            if (src == null) return;

            src.Stop();
            src.clip = clip;
            // Every robot of a class shares barks; a stable per-robot pitch keeps two Anvils apart.
            int h = agent.GetHashCode() * 7919;
            src.pitch = pitch * (1f + (((h % 9) + 9) % 9 - 4) * 0.012f);
            src.spatialBlend = selected ? 0.25f : 0.7f;
            src.volume = SoundBus.Volume(SoundChannel.Voice) * (selected ? 0.95f : 0.8f);
            src.transform.position = agent.transform.position;
            _following[slot] = agent.transform;
            src.Play();

            SoundBus.DuckBed(selected ? 0.3f : 0.18f, clip.length / pitch + 0.2f);

            // The card line matches what was said, except for pain noises and hellos.
            if (!string.IsNullOrEmpty(subtitle) && cue != VoiceCue.Hurt && cue != VoiceCue.Select && cue != VoiceCue.Down)
                agent.SetNarratedLine(subtitle);
        }

        private int FreeSlot()
        {
            // All busy: steal the voice that has been talking longest; it is nearly done.
            int oldest = 0;
            float longest = -1f;
            for (int i = 0; i < _sources.Length; i++)
            {
                if (_sources[i] == null) continue;
                if (!_sources[i].isPlaying) return i;
                if (_sources[i].time <= longest) continue;
                longest = _sources[i].time;
                oldest = i;
            }
            return oldest;
        }

        private void Awake()
        {
            _instance = this;
            for (int i = 0; i < _sources.Length; i++)
            {
                var child = new GameObject($"HeroVoice{i}");
                child.transform.SetParent(transform, false);
                var src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 12f;
                src.maxDistance = 110f;
                src.dopplerLevel = 0f;
                _sources[i] = src;
            }
        }

        private void Update()
        {
            OverseerVoice.Tick();
            for (int i = 0; i < _sources.Length; i++)
            {
                if (_sources[i] == null || !_sources[i].isPlaying || _following[i] == null) continue;
                _sources[i].transform.position = _following[i].position;
            }

            if (_pending.Count == 0) return;
            float now = Time.unscaledTime;
            _expired.Clear();
            foreach (var kv in _pending)
                if (now >= kv.Value.Deadline) _expired.Add(kv.Key);
            for (int i = 0; i < _expired.Count; i++)
            {
                Pending p = _pending[_expired[i]];
                _pending.Remove(_expired[i]);
                bool stateful = p.Cue != VoiceCue.LevelUp && p.Cue != VoiceCue.Refused;
                if (p.Agent != null && (!stateful || p.Agent.NarrationStamp == p.Stamp))
                    Bark(p.Agent, p.Cue);
            }
        }
    }
}
