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
    /// </summary>
    public sealed class SpatialAudio : MonoBehaviour
    {
        private const int PoolSize = 16;
        private const float MinDistance = 6f;
        private const float MaxDistance = 70f;

        private static SpatialAudio _instance;

        private readonly List<AudioSource> _pool = new List<AudioSource>(PoolSize);
        private int _next;

        public static SpatialAudio Ensure()
        {
            if (_instance != null) return _instance;

            var go = GameObject.Find("SM_SpatialAudio");
            if (go == null)
            {
                go = new GameObject("SM_SpatialAudio");
                DontDestroyOnLoad(go);
            }

            _instance = go.GetComponent<SpatialAudio>();
            if (_instance == null) _instance = go.AddComponent<SpatialAudio>();
            return _instance;
        }

        private void Awake()
        {
            _instance = this;
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
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = MinDistance;
            src.maxDistance = MaxDistance;
            src.dopplerLevel = 0f;   // Doppler on an isometric camera just sounds like a bug.
            return src;
        }

        /// <summary>Play a clip at a world position on the given channel.</summary>
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f,
            SoundChannel channel = SoundChannel.Sfx, float pitchJitter = 0.06f)
        {
            if (clip == null) return;

            SpatialAudio audio = Ensure();
            AudioSource src = audio.Take();
            if (src == null) return;

            src.transform.position = position;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume) * SoundBus.Volume(channel);
            // Slight pitch variation stops repeated hits sounding like a machine gun of one sample.
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.Play();
        }

        /// <summary>Round-robin, stealing the oldest voice when everything is busy.</summary>
        private AudioSource Take()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                int index = (_next + i) % _pool.Count;
                if (_pool[index] != null && !_pool[index].isPlaying)
                {
                    _next = (index + 1) % _pool.Count;
                    return _pool[index];
                }
            }

            AudioSource stolen = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            return stolen;
        }

        private void Update()
        {
            SoundBus.Tick(Time.unscaledDeltaTime);
        }
    }
}
