using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Procedural one-shot tones for Phase 2 juice (no external audio assets required).
    /// </summary>
    public static class DemoAudio
    {
        private static AudioSource _src;
        private static bool _ready;

        public static void Ensure()
        {
            if (_ready && _src != null) return;
            var go = GameObject.Find("DemoAudio");
            if (go == null)
            {
                go = new GameObject("DemoAudio");
                Object.DontDestroyOnLoad(go);
            }
            _src = go.GetComponent<AudioSource>();
            if (_src == null) _src = go.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
            _ready = true;
            EnsureAmbient(go);
        }

        private static AudioSource _ambient;

        private static void EnsureAmbient(GameObject host)
        {
            if (_ambient != null) return;
            _ambient = host.GetComponent<AudioSource>();
            // Separate quiet loop source.
            var ambGo = host.transform.Find("Ambient");
            GameObject child;
            if (ambGo == null)
            {
                child = new GameObject("Ambient");
                child.transform.SetParent(host.transform, false);
            }
            else child = ambGo.gameObject;

            _ambient = child.GetComponent<AudioSource>();
            if (_ambient == null) _ambient = child.AddComponent<AudioSource>();
            _ambient.playOnAwake = false;
            _ambient.loop = true;
            _ambient.spatialBlend = 0f;
            _ambient.volume = 0.045f;
            if (_ambient.clip == null)
                _ambient.clip = BuildAmbientClip();
            if (!_ambient.isPlaying)
                _ambient.Play();
        }

        private static AudioClip BuildAmbientClip()
        {
            const int hz = 44100;
            const float seconds = 4f;
            int samples = Mathf.CeilToInt(hz * seconds);
            var clip = AudioClip.Create("ambient_hum", samples, 1, hz, false);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)hz;
                // Soft dual-tone lunar hum (very quiet procedural bed).
                float a = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.35f;
                float b = Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.2f;
                float n = (Mathf.PerlinNoise(t * 0.7f, 0.13f) - 0.5f) * 0.15f;
                data[i] = (a + b + n) * 0.25f;
            }
            clip.SetData(data, 0);
            return clip;
        }

        public static void PlayFlagPost() => Chord(new[] { 523f, 659f }, 0.08f, 0.2f);
        public static void PlayClaim() => Chord(new[] { 784f, 988f }, 0.07f, 0.18f);
        public static void PlayBite() => Beep(110f, 0.09f, 0.32f);
        public static void PlayStalkerDeath() => Chord(new[] { 196f, 147f, 98f }, 0.22f, 0.3f);
        public static void PlayBuildPlace() => Chord(new[] { 440f, 554f }, 0.09f, 0.2f);
        public static void PlayFail() => Chord(new[] { 82f, 65f }, 0.4f, 0.42f);
        public static void PlayRetry() => Chord(new[] { 659f, 784f, 988f }, 0.14f, 0.25f);
        public static void PlayVictory() => Chord(new[] { 523f, 659f, 784f, 1046f }, 0.35f, 0.32f);

        private static void Chord(float[] hz, float duration, float volume)
        {
            Ensure();
            if (_src == null || hz == null || hz.Length == 0) return;
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
            _src.PlayOneShot(clip, volume);
        }

        private static void Beep(float hz, float duration, float volume)
        {
            Chord(new[] { hz }, duration, volume);
        }
    }
}
