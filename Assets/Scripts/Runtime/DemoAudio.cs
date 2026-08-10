using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// One-shots + dual campus ambient beds. Prefers Resources/Audio; else procedural.
    /// Phase 5E: Ambient A/B crossfade on F6/F7 (SFX stays on a separate source).
    /// </summary>
    public static class DemoAudio
    {
        private static AudioSource _sfx;
        private static AudioSource _ambientA;
        private static AudioSource _ambientB;
        private static bool _ready;
        private static int _campus = 0;

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
        }

        private static void EnsureAmbientBeds(GameObject host)
        {
            _ambientA = EnsureBed(host, "AmbientA", 0, 0.045f);
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
            float targetA = campusIndex <= 0 ? 0.045f : 0f;
            float targetB = campusIndex > 0 ? 0.04f : 0f;
            if (_ambientA != null)
                _ambientA.volume = targetA;
            if (_ambientB != null)
                _ambientB.volume = targetB;
        }

        public static void PlayFlagPost()
        {
            if (!TryPlay("sfx_flag_post", 0.35f))
                Chord(new[] { 523f, 659f }, 0.08f, 0.2f);
        }

        public static void PlayClaim()
        {
            if (!TryPlay("sfx_claim", 0.4f))
                Chord(new[] { 784f, 988f }, 0.07f, 0.18f);
        }

        public static void PlayBite()
        {
            if (!TryPlay("sfx_bite", 0.45f))
                Beep(110f, 0.09f, 0.32f);
        }

        public static void PlayStalkerDeath()
        {
            if (!TryPlay("sfx_stalker_death", 0.4f))
                Chord(new[] { 196f, 147f, 98f }, 0.22f, 0.3f);
        }

        public static void PlayBuildPlace()
        {
            if (!TryPlay("sfx_build_place", 0.35f))
                Chord(new[] { 440f, 554f }, 0.09f, 0.2f);
        }

        public static void PlayFail()
        {
            if (!TryPlay("sfx_fail", 0.5f))
                Chord(new[] { 82f, 65f }, 0.4f, 0.42f);
        }

        public static void PlayRetry()
        {
            if (!TryPlay("sfx_retry", 0.4f))
                Chord(new[] { 659f, 784f, 988f }, 0.14f, 0.25f);
        }

        public static void PlayVictory()
        {
            if (!TryPlay("sfx_victory", 0.45f))
                Chord(new[] { 523f, 659f, 784f, 1046f }, 0.35f, 0.32f);
        }

        private static bool TryPlay(string resourceName, float volume)
        {
            var clip = Resources.Load<AudioClip>("Audio/" + resourceName);
            if (clip == null) return false;
            Ensure();
            if (_sfx == null) return false;
            _sfx.PlayOneShot(clip, volume);
            return true;
        }

        private static AudioClip BuildAmbientClip(int campusIndex)
        {
            const int hz = 44100;
            const float seconds = 4f;
            int samples = Mathf.CeilToInt(hz * seconds);
            var clip = AudioClip.Create(campusIndex > 0 ? "ambient_b" : "ambient_a", samples, 1, hz, false);
            var data = new float[samples];
            float f0 = campusIndex > 0 ? 62f : 55f;
            float f1 = campusIndex > 0 ? 93f : 82.5f;
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

        private static void Chord(float[] hz, float duration, float volume)
        {
            Ensure();
            if (_sfx == null || hz == null || hz.Length == 0) return;
            int samples = Mathf.CeilToInt(44100 * duration);
            var clip = AudioClip.Create("chord", samples, 1, 44100, false);
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
            _sfx.PlayOneShot(clip, volume);
        }

        private static void Beep(float hz, float duration, float volume)
        {
            Chord(new[] { hz }, duration, volume);
        }
    }
}
