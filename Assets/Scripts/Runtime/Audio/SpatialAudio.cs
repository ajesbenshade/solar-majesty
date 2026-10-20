using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Pooled 3D one-shot player.
    ///
    /// Every sound in the project played at spatialBlend 0, so a stalker biting a robot on the far
    /// side of the map was exactly as loud as one at your feet. That flattens the whole soundscape
    /// and removes the main cue that something is happening somewhere. These sources are positioned,
    /// distance-attenuated, and routed through <see cref="SoundBus"/>.
    ///
    /// Unity pans each voice from the AudioListener on the camera, but the level comes from
    /// <see cref="SfxRouting"/>: ground distance from the point at screen centre, scaled to how much
    /// of the map is on screen. The old fixed 6–70 m rolloff measured from the listener, which sits
    /// 44 m from the ground (63 m in the diorama lens), so screen centre played ~6 dB under 2D
    /// (~17 dB in the diorama lens), the top edge of the default view (77 m away) was silent while
    /// the bottom edge (22 m) was nearly full, and anything past 70 m was silent even while it was
    /// on screen at max zoom-out.
    /// </summary>
    public sealed class SpatialAudio : MonoBehaviour
    {
        private const int PoolSize = 16;

        /// <summary>Below this a voice is not worth taking from the pool.</summary>
        private const float MinAudibleGain = 0.01f;

        private static SpatialAudio _instance;

        /// <summary>
        /// Unity's own distance attenuation is turned off (flat custom curve): attenuating from the
        /// listener as well would bring back the depth bias SfxRouting removes.
        /// </summary>
        private static AnimationCurve _flat;

        private readonly List<AudioSource> _pool = new List<AudioSource>(PoolSize);
        private readonly float[] _volume = new float[PoolSize];
        private readonly SoundChannel[] _channel = new SoundChannel[PoolSize];
        private int _next;

        private Camera _camera;
        private bool _hasFrame;
        private Vector3 _focus;
        private SfxRouting.Rolloff _rolloff;

        /// <summary>True when a positioned sound can actually play (play mode, pool built).</summary>
        public static bool Ready
        {
            get
            {
                SpatialAudio audio = Ensure();
                return audio != null && audio._pool.Count > 0;
            }
        }

        public static SpatialAudio Ensure()
        {
            // EditMode tests and other non-play callers cannot DontDestroyOnLoad.
            if (!Application.isPlaying) return _instance;
            if (_instance != null) return _instance;

            var go = GameObject.Find("SM_SpatialAudio");
            if (go == null)
            {
                go = new GameObject("SM_SpatialAudio");
                if (Application.isPlaying) DontDestroyOnLoad(go);
            }

            _instance = go.GetComponent<SpatialAudio>();
            if (_instance == null) _instance = go.AddComponent<SpatialAudio>();
            return _instance;
        }

        private void Awake()
        {
            _instance = this;
            if (_flat == null)
                _flat = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f));
            for (int i = 0; i < PoolSize; i++)
                _pool.Add(CreateSource(i));
        }

        private AudioSource CreateSource(int index)
        {
            var child = new GameObject($"Voice{index}");
            child.transform.SetParent(transform, false);

            var src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 1f;   // panning from the listener; level from SfxRouting
            src.rolloffMode = AudioRolloffMode.Custom;
            src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, _flat);
            src.minDistance = 1f;
            src.maxDistance = 500f;
            src.dopplerLevel = 0f;   // Doppler on an isometric camera just sounds like a bug.
            return src;
        }

        /// <summary>Play a clip at a world position on the given channel.</summary>
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f,
            SoundChannel channel = SoundChannel.Sfx, float pitchJitter = 0.06f)
        {
            if (clip == null) return;

            SpatialAudio audio = Ensure();
            if (audio == null) return;

            audio.RefreshFrame();
            float gain = audio.GainAt(position);
            // Far off screen: inaudible, so do not steal a voice from something the player can hear.
            if (gain < MinAudibleGain) return;

            int slot = audio.TakeSlot();
            if (slot < 0) return;
            AudioSource src = audio._pool[slot];

            audio._volume[slot] = Mathf.Clamp01(volume);
            audio._channel[slot] = channel;
            src.transform.position = position;
            src.clip = clip;
            src.volume = audio._volume[slot] * SoundBus.Volume(channel) * gain;
            // Slight pitch variation stops repeated hits sounding like a machine gun of one sample.
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.Play();
        }

        /// <summary>Where the screen centre meets the ground, and how much map the view shows.</summary>
        private void RefreshFrame()
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
                _camera = Camera.main;
            if (_camera == null)
            {
                _hasFrame = false;
                return;
            }

            Transform t = _camera.transform;
            if (!IsometricCameraController.TryGroundFocus(t.position, t.forward, out Vector3 focus))
            {
                // Looking at or above the horizon: no ground focus, so no attenuation.
                _hasFrame = false;
                return;
            }

            float focusDistance = Vector3.Distance(t.position, focus);
            float halfHeight = SfxRouting.VisibleHalfHeight(
                _camera.orthographic, _camera.orthographicSize, _camera.fieldOfView, focusDistance);
            _focus = focus;
            _rolloff = SfxRouting.ComputeRolloff(halfHeight, _camera.aspect);
            _hasFrame = true;
        }

        /// <summary>Level for a position under the current frame; 1 when there is no camera.</summary>
        private float GainAt(Vector3 position) =>
            _hasFrame ? SfxRouting.Gain(_rolloff, SfxRouting.GroundDistance(_focus, position)) : 1f;

        /// <summary>Round-robin, stealing the oldest voice when everything is busy.</summary>
        private int TakeSlot()
        {
            // Awake builds the pool; it has not run on an EditMode AddComponent.
            if (_pool.Count == 0) return -1;
            for (int i = 0; i < _pool.Count; i++)
            {
                int index = (_next + i) % _pool.Count;
                if (_pool[index] != null && !_pool[index].isPlaying)
                {
                    _next = (index + 1) % _pool.Count;
                    return index;
                }
            }

            int stolen = _next;
            _next = (_next + 1) % _pool.Count;
            return _pool[stolen] != null ? stolen : -1;
        }

        private void Update()
        {
            SoundBus.Tick(Time.unscaledDeltaTime);

            // Follow the camera while voices ring out, so panning or zooming during a two-second
            // completion chord changes its level the way it changes its pan. Also picks up the
            // settings sliders and ducking live, as the beds do.
            bool framed = false;
            for (int i = 0; i < _pool.Count; i++)
            {
                AudioSource src = _pool[i];
                if (src == null || !src.isPlaying) continue;
                if (!framed)
                {
                    RefreshFrame();
                    framed = true;
                }
                src.volume = _volume[i] * SoundBus.Volume(_channel[i]) * GainAt(src.transform.position);
            }
        }
    }
}
