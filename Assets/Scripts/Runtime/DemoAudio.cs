using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Sound effects and the per-world ambient beds.
    ///
    /// Clips come from Resources/Audio/Sfx/sfx_&lt;event&gt;_&lt;n&gt; (rendered by
    /// Tools/audio/render_sfx.py, several takes per event), then the legacy single clip
    /// Resources/Audio/sfx_&lt;event&gt;, then a procedural chord so a missing file never goes silent.
    /// Everything is routed through <see cref="SoundBus"/> so the settings sliders and ducking
    /// apply. World-position overloads go through <see cref="SpatialAudio"/>; UI, outcomes and
    /// alerts stay 2D because they are about the colony, not a spot on the map.
    /// Each event is rate limited, so a frame in which twenty robots claim flags plays one claim.
    /// Phase 5E: Ambient A/B crossfade on F6/F7 (SFX stays on a separate source).
    /// </summary>
    public static class DemoAudio
    {
        private const string SfxFolder = "Audio/Sfx/";
        private const string LegacyFolder = "Audio/";
        private const int MaxVariants = 6;

        /// <summary>
        /// The listener rides the camera ~30 m above the ground and SpatialAudio rolls off
        /// linearly from 6 to 70 m, so a positioned sound at screen centre lands ~4 dB under the
        /// same clip played 2D. This makes up most of that.
        /// </summary>
        private const float SpatialMakeup = 1.35f;

        /// <summary>Spatial pitch jitter; small because variants already supply the variety.</summary>
        private const float SpatialJitter = 0.03f;

        // Ambient gains before the SoundBus Ambient level. The legacy 4 s beds are kept at their
        // old loudness (0.045 of the slider, bus level 0.55); the rendered world beds are mastered
        // to about -31 LUFS and sit ~14 dB under gameplay effects at these gains.
        private const float LegacyBedGainA = 0.045f / 0.55f;
        private const float LegacyBedGainB = 0.04f / 0.55f;
        private const float WorldBedGainA = 0.34f;
        private const float WorldBedGainB = 0.3f;
        // "ambient" in the name is what Editor/AudioImportRules keys on to keep beds stereo.
        private const string WorldBedPrefix = "sfx_ambient_";

        private static AudioSource _sfx;
        private static AudioSource _ambientA;
        private static AudioSource _ambientB;
        private static DemoAudioHost _host;
        private static bool _ready;
        private static int _campus = 0;
        private static float _hum = 55f;
        private static int _uiFrame = -1;

        private static readonly Dictionary<string, AudioClip[]> Clips = new Dictionary<string, AudioClip[]>();
        private static readonly Dictionary<string, AudioClip> Procedural = new Dictionary<string, AudioClip>();

        /// <summary>
        /// Beds loaded by <see cref="SetBody"/>. Tracked by reference, not name: the legacy
        /// "sfx_ambient_b" shares the world-bed prefix and must never be unloaded or re-gained.
        /// </summary>
        private static readonly HashSet<AudioClip> WorldBeds = new HashSet<AudioClip>();
        private static readonly SfxVariantPicker Picker = new SfxVariantPicker();
        private static readonly SfxRateLimiter Limiter = new SfxRateLimiter();

        /// <summary>
        /// One event: which clips, how loud relative to the others, which bus, how often, and
        /// the procedural chord used when no clip exists.
        /// </summary>
        private sealed class SfxEvent
        {
            public readonly string Key;
            public readonly float Gain;
            public readonly SoundChannel Channel;
            public readonly SfxLimit Limit;
            public readonly float[] FallbackHz;
            public readonly float FallbackSeconds;
            public readonly float FallbackVolume;

            public SfxEvent(string key, float gain, SoundChannel channel, SfxLimit limit,
                float[] fallbackHz, float fallbackSeconds, float fallbackVolume)
            {
                Key = key;
                Gain = gain;
                Channel = channel;
                Limit = limit;
                FallbackHz = fallbackHz;
                FallbackSeconds = fallbackSeconds;
                FallbackVolume = fallbackVolume;
            }
        }

        // Rendered clips carry the loudness hierarchy themselves (UI quietest, alerts and victory
        // loudest), so gains here are close to uniform. Fallback chords keep their original tones.
        private static readonly SfxEvent UiHover = new SfxEvent("ui_hover", 0.8f, SoundChannel.Ui,
            new SfxLimit(0.06f, 2, 8f), new[] { 2400f }, 0.03f, 0.05f);
        private static readonly SfxEvent UiClick = new SfxEvent("ui_click", 0.85f, SoundChannel.Ui,
            new SfxLimit(0.04f, 4, 10f), new[] { 1800f }, 0.03f, 0.12f);
        private static readonly SfxEvent Retry = new SfxEvent("retry", 0.85f, SoundChannel.Ui,
            new SfxLimit(0.15f, 2, 2f), new[] { 659f, 784f, 988f }, 0.14f, 0.25f);
        private static readonly SfxEvent FlagPost = new SfxEvent("flag_post", 0.85f, SoundChannel.Sfx,
            new SfxLimit(0.08f, 3, 3f), new[] { 523f, 659f }, 0.08f, 0.2f);
        private static readonly SfxEvent Claim = new SfxEvent("claim", 0.8f, SoundChannel.Sfx,
            new SfxLimit(0.07f, 3, 2.5f), new[] { 784f, 988f }, 0.07f, 0.18f);
        private static readonly SfxEvent Bite = new SfxEvent("bite", 0.8f, SoundChannel.Sfx,
            new SfxLimit(0.12f, 3, 3f), new[] { 110f }, 0.09f, 0.32f);
        private static readonly SfxEvent RobotHit = new SfxEvent("robot_hit", 0.8f, SoundChannel.Sfx,
            new SfxLimit(0.1f, 3, 3f), new[] { 220f, 330f }, 0.08f, 0.25f);
        private static readonly SfxEvent StalkerAggro = new SfxEvent("stalker_growl", 0.8f, SoundChannel.Sfx,
            new SfxLimit(0.4f, 2, 0.5f), new[] { 98f, 104f }, 0.3f, 0.25f);
        private static readonly SfxEvent StalkerDeath = new SfxEvent("stalker_death", 0.8f, SoundChannel.Sfx,
            new SfxLimit(0.08f, 4, 3f), new[] { 196f, 147f, 98f }, 0.22f, 0.3f);
        private static readonly SfxEvent BuildPlace = new SfxEvent("build_place", 0.85f, SoundChannel.Sfx,
            new SfxLimit(0.08f, 3, 3f), new[] { 440f, 554f }, 0.09f, 0.2f);
        private static readonly SfxEvent BuildComplete = new SfxEvent("build_complete", 0.85f, SoundChannel.Sfx,
            new SfxLimit(0.25f, 2, 1f), new[] { 330f, 440f, 554f }, 0.16f, 0.24f);
        private static readonly SfxEvent ConstructionTick = new SfxEvent("construction_tick", 0.75f, SoundChannel.Sfx,
            new SfxLimit(0.13f, 3, 4f), new[] { 1400f }, 0.03f, 0.1f);
        private static readonly SfxEvent Extract = new SfxEvent("extract", 0.8f, SoundChannel.Sfx,
            new SfxLimit(0.1f, 3, 2f), new[] { 392f, 523f, 659f }, 0.12f, 0.22f);
        private static readonly SfxEvent Research = new SfxEvent("research", 0.85f, SoundChannel.Sfx,
            new SfxLimit(0.5f, 1, 0.5f), new[] { 587f, 740f, 880f }, 0.2f, 0.22f);
        private static readonly SfxEvent LevelUp = new SfxEvent("level_up", 0.85f, SoundChannel.Sfx,
            new SfxLimit(0.2f, 2, 1f), new[] { 784f, 1047f, 1319f }, 0.2f, 0.22f);
        private static readonly SfxEvent Credits = new SfxEvent("credits", 0.8f, SoundChannel.Sfx,
            new SfxLimit(0.1f, 3, 2.5f), new[] { 1568f, 2093f }, 0.08f, 0.15f);
        private static readonly SfxEvent RobotDown = new SfxEvent("robot_down", 0.85f, SoundChannel.Sfx,
            new SfxLimit(0.2f, 2, 1f), new[] { 220f, 147f, 110f }, 0.3f, 0.3f);
        private static readonly SfxEvent RobotSpawn = new SfxEvent("robot_spawn", 0.8f, SoundChannel.Sfx,
            new SfxLimit(0.15f, 3, 2f), new[] { 660f, 990f, 1320f }, 0.12f, 0.2f);
        private static readonly SfxEvent PartyForm = new SfxEvent("party_form", 0.8f, SoundChannel.Sfx,
            new SfxLimit(0.3f, 2, 1f), new[] { 392f, 587f }, 0.2f, 0.2f);
        private static readonly SfxEvent Repair = new SfxEvent("repair", 0.8f, SoundChannel.Sfx,
            new SfxLimit(0.15f, 3, 2f), new[] { 1319f, 1760f }, 0.12f, 0.18f);
        private static readonly SfxEvent Heal = new SfxEvent("heal", 0.8f, SoundChannel.Sfx,
            new SfxLimit(0.15f, 3, 2f), new[] { 1047f, 1319f, 1568f }, 0.2f, 0.16f);
        private static readonly SfxEvent Equip = new SfxEvent("equip", 0.8f, SoundChannel.Sfx,
            new SfxLimit(0.12f, 3, 2f), new[] { 700f, 1500f }, 0.08f, 0.18f);
        private static readonly SfxEvent LaunchReady = new SfxEvent("launch_ready", 0.85f, SoundChannel.Sfx,
            new SfxLimit(1f, 1, 0.5f), new[] { 880f, 1319f }, 0.25f, 0.22f);
        private static readonly SfxEvent Launch = new SfxEvent("launch", 0.9f, SoundChannel.Sfx,
            new SfxLimit(3f, 1, 0.1f), new[] { 55f, 82f }, 1.2f, 0.4f);
        private static readonly SfxEvent AlertWarning = new SfxEvent("alert_warning", 0.8f, SoundChannel.Sfx,
            new SfxLimit(1.2f, 1, 0.5f), new[] { 880f, 659f }, 0.12f, 0.25f);
        private static readonly SfxEvent AlertCritical = new SfxEvent("alert_critical", 0.85f, SoundChannel.Sfx,
            new SfxLimit(2f, 1, 0.4f), new[] { 523f, 440f }, 0.2f, 0.3f);
        private static readonly SfxEvent Fail = new SfxEvent("fail", 0.9f, SoundChannel.Sfx,
            new SfxLimit(1f, 1, 0.3f), new[] { 82f, 65f }, 0.4f, 0.42f);
        private static readonly SfxEvent Victory = new SfxEvent("victory", 0.9f, SoundChannel.Sfx,
            new SfxLimit(1.5f, 1, 0.2f), new[] { 523f, 659f, 784f, 1046f }, 0.35f, 0.32f);

        public static void ApplyVolumes()
        {
            Ensure();
            // One-shots take their level from SoundBus at play time (it already includes Master).
            if (_sfx != null)
                _sfx.volume = 1f;
            ApplyCampusVolumes(_campus, instant: true);
        }

        public static void Ensure()
        {
            if (_ready && _sfx != null) return;
            var go = GameObject.Find("DemoAudio");
            if (go == null)
            {
                go = new GameObject("DemoAudio");
                Object.DontDestroyOnLoad(go);
            }
            _sfx = go.GetComponent<AudioSource>();
            if (_sfx == null) _sfx = go.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;
            _sfx.volume = 1f;
            _ready = true;
            EnsureAmbientBeds(go);
            _host = go.GetComponent<DemoAudioHost>();
            if (_host == null) _host = go.AddComponent<DemoAudioHost>();
            _host.Bind(_ambientA, _ambientB);
            ApplyCampusVolumes(_campus, instant: true);
            // SpatialAudio is also what ticks SoundBus ducking, so it must exist even before the
            // first positioned sound plays.
            SpatialAudio.Ensure();
        }

        /// <summary>
        /// Switch the beds to the active body's rendered loops (Resources/Audio/Sfx/sfx_ambient_&lt;id&gt;
        /// and _b). Without them, retune the procedural beds (Earth air / Luna vacuum / Mars dust).
        /// </summary>
        public static void SetBody(CelestialBodyProfile body)
        {
            Ensure();
            _hum = body != null ? body.AmbientHum : 55f;
            string id = body != null ? body.Id.ToString().ToLowerInvariant() : null;
            AudioClip worldA = id != null ? Resources.Load<AudioClip>(SfxFolder + WorldBedPrefix + id) : null;
            AudioClip worldB = id != null ? Resources.Load<AudioClip>(SfxFolder + WorldBedPrefix + id + "_b") : null;
            if (worldB == null) worldB = worldA;
            if (worldA != null) WorldBeds.Add(worldA);
            if (worldB != null) WorldBeds.Add(worldB);

            AudioClip oldA = _ambientA != null ? _ambientA.clip : null;
            AudioClip oldB = _ambientB != null ? _ambientB.clip : null;
            AssignBed(_ambientA, worldA, 0);
            AssignBed(_ambientB, worldB, 1);
            ReleaseWorldBed(oldA);
            ReleaseWorldBed(oldB);
            ApplyCampusVolumes(_campus, instant: true);
        }

        private static void AssignBed(AudioSource src, AudioClip world, int campusIndex)
        {
            if (src == null) return;
            if (world != null)
            {
                if (src.clip != world)
                {
                    src.clip = world;
                    src.Play();
                    // Campus B may share campus A's loop; start it half a loop away so the two
                    // never play in phase during the crossfade.
                    if (campusIndex > 0) src.time = world.length * 0.5f;
                }
            }
            else if (IsWorldBed(src.clip) || IsProcedural(src.clip) || src.clip == null)
            {
                string res = campusIndex > 0 ? "Audio/sfx_ambient_b" : "Audio/sfx_ambient";
                var authored = Resources.Load<AudioClip>(res);
                src.clip = authored != null ? authored : BuildAmbientClip(campusIndex);
            }
            if (!src.isPlaying) src.Play();
        }

        /// <summary>World beds are large; unload the previous world's once nothing plays it.</summary>
        private static void ReleaseWorldBed(AudioClip clip)
        {
            if (!IsWorldBed(clip)) return;
            if ((_ambientA != null && _ambientA.clip == clip) || (_ambientB != null && _ambientB.clip == clip))
                return;
            WorldBeds.Remove(clip);
            Resources.UnloadAsset(clip);
        }

        private static bool IsWorldBed(AudioClip clip) => clip != null && WorldBeds.Contains(clip);

        private static bool IsProcedural(AudioClip clip) =>
            clip != null && clip.name.StartsWith("ambient_");

        private static void EnsureAmbientBeds(GameObject host)
        {
            _ambientA = EnsureBed(host, "AmbientA", 0, 0f);
            _ambientB = EnsureBed(host, "AmbientB", 1, 0f);
            ApplyCampusVolumes(_campus, instant: true);
        }

        private static AudioSource EnsureBed(GameObject host, string childName, int campusIndex, float startVolume)
        {
            var t = host.transform.Find(childName);
            GameObject child;
            if (t == null)
            {
                child = new GameObject(childName);
                child.transform.SetParent(host.transform, false);
            }
            else child = t.gameObject;

            var src = child.GetComponent<AudioSource>();
            if (src == null) src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.pitch = 1f;
            src.volume = startVolume;
            if (src.clip == null)
            {
                string res = campusIndex > 0 ? "Audio/sfx_ambient_b" : "Audio/sfx_ambient";
                var authored = Resources.Load<AudioClip>(res);
                src.clip = authored != null ? authored : BuildAmbientClip(campusIndex);
            }
            if (!src.isPlaying)
                src.Play();
            return src;
        }

        /// <summary>Crossfade Ambient A ↔ B when focusing campuses.</summary>
        public static void SetCampusAmbient(int campusIndex)
        {
            Ensure();
            _campus = campusIndex > 0 ? 1 : 0;
            ApplyCampusVolumes(_campus, instant: false);
        }

        private static void ApplyCampusVolumes(int campusIndex, bool instant)
        {
            float gainA = IsWorldBed(_ambientA != null ? _ambientA.clip : null) ? WorldBedGainA : LegacyBedGainA;
            float gainB = IsWorldBed(_ambientB != null ? _ambientB.clip : null) ? WorldBedGainB : LegacyBedGainB;
            float targetA = campusIndex <= 0 ? gainA : 0f;
            float targetB = campusIndex > 0 ? gainB : 0f;
            if (_host != null)
            {
                _host.SetTargets(targetA, targetB, instant);
                return;
            }
            float bus = SoundBus.Volume(SoundChannel.Ambient);
            if (_ambientA != null) _ambientA.volume = targetA * bus;
            if (_ambientB != null) _ambientB.volume = targetB * bus;
        }

        // ------------------------------------------------------------------------------------
        // Command and UI (2D)
        // ------------------------------------------------------------------------------------

        /// <summary>Soft tick for a HUD control being pressed.</summary>
        public static void PlayUiClick()
        {
            // A button whose action already made a UI sound (confirm, fail) should not also click.
            if (_uiFrame == Time.frameCount) return;
            Play(UiClick);
        }

        /// <summary>Barely-there glint when the pointer enters a HUD button.</summary>
        public static void PlayUiHover() => Play(UiHover);

        /// <summary>Retry / confirm / travel acknowledgement.</summary>
        public static void PlayRetry()
        {
            if (Play(Retry)) _uiFrame = Time.frameCount;
        }

        /// <summary>Alias for <see cref="PlayRetry"/> for call sites that mean "confirmed".</summary>
        public static void PlayUiConfirm() => PlayRetry();

        public static void PlayFlagPost() => Play(FlagPost);

        public static void PlayFlagPost(Vector3 at) => PlayAt(FlagPost, at);

        public static void PlayBuildPlace() => Play(BuildPlace);

        public static void PlayBuildPlace(Vector3 at) => PlayAt(BuildPlace, at);

        // ------------------------------------------------------------------------------------
        // Robots
        // ------------------------------------------------------------------------------------

        public static void PlayClaim() => Play(Claim);

        /// <summary>A robot accepted a bounty.</summary>
        public static void PlayClaim(Vector3 at) => PlayAt(Claim, at);

        public static void PlayBite() => Play(Bite);

        /// <summary>
        /// Fauna bite landing on a robot. Safe to call every frame from damage code: the limiter
        /// turns a continuous stream into a steady few chomps a second.
        /// </summary>
        public static void PlayBite(Vector3 at) => PlayAt(Bite, at);

        /// <summary>Metal impact: a mech weapon striking, or a hard hit on a chassis.</summary>
        public static void PlayRobotHit(Vector3 at) => PlayAt(RobotHit, at);

        /// <summary>A robot is incapacitated or scrapped.</summary>
        public static void PlayRobotDown(Vector3 at) => PlayAt(RobotDown, at);

        /// <summary>A robot hero gained a level.</summary>
        public static void PlayLevelUp() => Play(LevelUp);

        public static void PlayLevelUp(Vector3 at) => PlayAt(LevelUp, at);

        /// <summary>Credits paid out (bounty completed, trade gold landed).</summary>
        public static void PlayCredits() => Play(Credits);

        public static void PlayCredits(Vector3 at) => PlayAt(Credits, at);

        /// <summary>A robot was fabricated or hired at a workshop.</summary>
        public static void PlayRobotSpawn(Vector3 at) => PlayAt(RobotSpawn, at);

        /// <summary>Robots grouped into a party.</summary>
        public static void PlayPartyForm(Vector3 at) => PlayAt(PartyForm, at);

        /// <summary>A structure repair finished.</summary>
        public static void PlayRepair(Vector3 at) => PlayAt(Repair, at);

        /// <summary>A robot was patched up (aid station, medic).</summary>
        public static void PlayHeal(Vector3 at) => PlayAt(Heal, at);

        /// <summary>A robot bought and equipped an item.</summary>
        public static void PlayEquip(Vector3 at) => PlayAt(Equip, at);

        public static void PlayExtract() => Play(Extract);

        /// <summary>Resource extracted or a haul delivered.</summary>
        public static void PlayExtract(Vector3 at) => PlayAt(Extract, at);

        // ------------------------------------------------------------------------------------
        // Fauna
        // ------------------------------------------------------------------------------------

        /// <summary>A stalker has noticed a robot and is coming for it.</summary>
        public static void PlayStalkerAggro(Vector3 at) => PlayAt(StalkerAggro, at);

        public static void PlayStalkerDeath() => Play(StalkerDeath);

        public static void PlayStalkerDeath(Vector3 at) => PlayAt(StalkerDeath, at);

        // ------------------------------------------------------------------------------------
        // Construction and progress
        // ------------------------------------------------------------------------------------

        public static void PlayBuildComplete() => Play(BuildComplete);

        public static void PlayBuildComplete(Vector3 at) => PlayAt(BuildComplete, at);

        /// <summary>Rivet/weld tick; call on each labour pulse, the limiter paces it.</summary>
        public static void PlayConstructionTick(Vector3 at) => PlayAt(ConstructionTick, at);

        public static void PlayResearch() => Play(Research);

        /// <summary>The departure craft is staged on the pad.</summary>
        public static void PlayLaunchReady(Vector3 at) => PlayAt(LaunchReady, at);

        /// <summary>
        /// Rocket launch rumble. 2D on purpose: it is the climax of a world and should fill the
        /// room, not fall off with distance from the pad. The ambient bed steps aside for it.
        /// </summary>
        public static void PlayLaunch()
        {
            if (Play(Launch))
                SoundBus.DuckFor(SoundChannel.Ambient, 0.6f, 5f);
        }

        // ------------------------------------------------------------------------------------
        // Alerts and outcomes (2D)
        // ------------------------------------------------------------------------------------

        /// <summary>Alert chime by severity. Good and Info stay silent; the feed shows them.</summary>
        public static void PlayAlert(AlertSeverity severity)
        {
            if (severity == AlertSeverity.Critical) PlayAlertCritical();
            else if (severity == AlertSeverity.Warning) PlayAlertWarning();
        }

        public static void PlayAlertWarning() => Play(AlertWarning);

        public static void PlayAlertCritical()
        {
            if (Play(AlertCritical))
                SoundBus.DuckFor(SoundChannel.Ambient, 0.4f, 1.2f);
        }

        public static void PlayFail()
        {
            if (Play(Fail)) _uiFrame = Time.frameCount;
        }

        public static void PlayVictory()
        {
            if (Play(Victory))
                SoundBus.DuckFor(SoundChannel.Ambient, 0.5f, 3f);
        }

        // ------------------------------------------------------------------------------------
        // Playback
        // ------------------------------------------------------------------------------------

        /// <summary>2D one-shot. Returns false when rate limited.</summary>
        private static bool Play(SfxEvent e)
        {
            if (!Limiter.TryAcquire(e.Key, Time.unscaledTime, e.Limit)) return false;
            Ensure();
            if (_sfx == null) return false;

            AudioClip clip = PickClip(e, out bool procedural);
            if (clip == null) return false;
            float gain = procedural ? e.FallbackVolume : e.Gain;
            _sfx.PlayOneShot(clip, Mathf.Clamp01(gain) * SoundBus.Volume(e.Channel));
            return true;
        }

        /// <summary>Positioned one-shot through the pooled 3D voices.</summary>
        private static bool PlayAt(SfxEvent e, Vector3 at)
        {
            if (!Limiter.TryAcquire(e.Key, Time.unscaledTime, e.Limit)) return false;
            Ensure();

            AudioClip clip = PickClip(e, out bool procedural);
            if (clip == null) return false;
            float gain = procedural ? e.FallbackVolume : e.Gain * SpatialMakeup;
            SpatialAudio.PlayAt(clip, at, Mathf.Clamp01(gain), e.Channel, SpatialJitter);
            return true;
        }

        private static AudioClip PickClip(SfxEvent e, out bool procedural)
        {
            AudioClip[] set = Variants(e.Key);
            if (set.Length > 0)
            {
                procedural = false;
                return set[Picker.Next(e.Key, set.Length, Random.value)];
            }

            procedural = true;
            return ProceduralFor(e);
        }

        /// <summary>
        /// Load (once) every take of an event. Missing assets are cached as an empty set, so a
        /// build without the rendered clips does not hit Resources.Load on every call.
        /// </summary>
        private static AudioClip[] Variants(string key)
        {
            if (Clips.TryGetValue(key, out AudioClip[] cached)) return cached;

            var found = new List<AudioClip>(3);
            for (int i = 1; i <= MaxVariants; i++)
            {
                var clip = Resources.Load<AudioClip>(SfxFolder + "sfx_" + key + "_" + i);
                if (clip == null) break;
                found.Add(clip);
            }
            if (found.Count == 0)
            {
                var legacy = Resources.Load<AudioClip>(LegacyFolder + "sfx_" + key);
                if (legacy != null) found.Add(legacy);
            }

            AudioClip[] set = found.ToArray();
            Clips[key] = set;
            return set;
        }

        private static AudioClip ProceduralFor(SfxEvent e)
        {
            if (e.FallbackHz == null || e.FallbackHz.Length == 0) return null;
            if (Procedural.TryGetValue(e.Key, out AudioClip clip) && clip != null) return clip;
            clip = BuildChord("chord_" + e.Key, e.FallbackHz, e.FallbackSeconds, e.FallbackVolume);
            Procedural[e.Key] = clip;
            return clip;
        }

        private static AudioClip BuildAmbientClip(int campusIndex)
        {
            const int hz = 44100;
            const float seconds = 4f;
            int samples = Mathf.CeilToInt(hz * seconds);
            var clip = AudioClip.Create(campusIndex > 0 ? "ambient_b" : "ambient_a", samples, 1, hz, false);
            var data = new float[samples];
            float f0 = _hum * (campusIndex > 0 ? 1.12f : 1f);
            float f1 = f0 * 1.5f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)hz;
                float a = Mathf.Sin(2f * Mathf.PI * f0 * t) * 0.35f;
                float b = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.2f;
                float n = (Mathf.PerlinNoise(t * 0.7f, 0.13f + campusIndex) - 0.5f) * 0.15f;
                data[i] = (a + b + n) * 0.25f;
            }
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// The original fallback tone: decaying sines. Built once per event and cached; it used to
        /// allocate a fresh AudioClip on every call.
        /// </summary>
        private static AudioClip BuildChord(string name, float[] hz, float duration, float volume)
        {
            int samples = Mathf.CeilToInt(44100 * duration);
            var clip = AudioClip.Create(name, samples, 1, 44100, false);
            var data = new float[samples];
            float inv = 1f / hz.Length;
            for (int i = 0; i < samples; i++)
            {
                float t = i / 44100f;
                float env = 1f - (t / duration);
                float sum = 0f;
                for (int h = 0; h < hz.Length; h++)
                    sum += Mathf.Sin(2f * Mathf.PI * hz[h] * t);
                data[i] = sum * inv * env * volume;
            }
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// <summary>
    /// Lerps campus ambient beds so F6/F7 crossfade instead of hard-cut, and re-reads the
    /// SoundBus Ambient level every frame so ducking and the settings slider apply live.
    /// </summary>
    public sealed class DemoAudioHost : MonoBehaviour
    {
        private AudioSource _a;
        private AudioSource _b;
        private float _targetA;
        private float _targetB;
        private float _gainA;
        private float _gainB;

        public void Bind(AudioSource a, AudioSource b)
        {
            _a = a;
            _b = b;
        }

        /// <summary>Bed gains before the SoundBus Ambient level (0 = silent).</summary>
        public void SetTargets(float a, float b, bool instant)
        {
            _targetA = a;
            _targetB = b;
            if (instant)
            {
                _gainA = a;
                _gainB = b;
            }
            Apply();
        }

        private void Update()
        {
            float k = 1f - Mathf.Exp(-3.2f * Time.unscaledDeltaTime);
            _gainA = Mathf.Lerp(_gainA, _targetA, k);
            _gainB = Mathf.Lerp(_gainB, _targetB, k);
            Apply();
        }

        private void Apply()
        {
            float bus = SoundBus.Volume(SoundChannel.Ambient);
            if (_a != null) _a.volume = _gainA * bus;
            if (_b != null) _b.volume = _gainB * bus;
        }
    }
}
