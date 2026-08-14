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
        private static DemoAudioHost _host;
        private static bool _ready;
        private static int _campus = 0;
        private static float _hum = 55f;

        public static void ApplyVolumes()
        {
            Ensure();
            if (_sfx != null)
                _sfx.volume = Mathf.Clamp01(DemoSettings.Master);
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
            _sfx.volume = Mathf.Clamp01(DemoSettings.Master);
            _ready = true;
            EnsureAmbientBeds(go);
            _host = go.GetComponent<DemoAudioHost>();
            if (_host == null) _host = go.AddComponent<DemoAudioHost>();
            _host.Bind(_ambientA, _ambientB);
        }

        /// <summary>Retune procedural beds to the active body (Earth air / Luna vacuum / Mars dust).</summary>
        public static void SetBody(CelestialBodyProfile body)
        {
            Ensure();
            _hum = body != null ? body.AmbientHum : 55f;
            if (IsProcedural(_ambientA))
            {
                _ambientA.clip = BuildAmbientClip(0);
                if (!_ambientA.isPlaying) _ambientA.Play();
            }
            if (IsProcedural(_ambientB))
            {
                _ambientB.clip = BuildAmbientClip(1);
                if (!_ambientB.isPlaying) _ambientB.Play();
            }
            ApplyCampusVolumes(_campus, instant: true);
        }

        private static bool IsProcedural(AudioSource src) =>
            src != null && src.clip != null && src.clip.name.StartsWith("ambient_");

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
            float targetA = campusIndex <= 0 ? 0.045f * DemoSettings.Ambient * DemoSettings.Master : 0f;
            float targetB = campusIndex > 0 ? 0.04f * DemoSettings.Ambient * DemoSettings.Master : 0f;
            if (_host != null)
            {
                _host.SetTargets(targetA, targetB, instant);
                return;
            }
            if (_ambientA != null) _ambientA.volume = targetA;
            if (_ambientB != null) _ambientB.volume = targetB;
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

        public static void PlayExtract()
        {
            if (!TryPlay("sfx_extract", 0.38f))
                Chord(new[] { 392f, 523f, 659f }, 0.12f, 0.22f);
        }

        public static void PlayBuildComplete()
        {
            if (!TryPlay("sfx_build_complete", 0.4f))
                Chord(new[] { 330f, 440f, 554f }, 0.16f, 0.24f);
        }

        public static void PlayResearch()
        {
            if (!TryPlay("sfx_research", 0.38f))
                Chord(new[] { 587f, 740f, 880f }, 0.2f, 0.22f);
        }

        private static bool TryPlay(string resourceName, float volume)
        {
            var clip = Resources.Load<AudioClip>("Audio/" + resourceName);
            if (clip == null) return false;
            Ensure();
            if (_sfx == null) return false;
            _sfx.PlayOneShot(clip, volume * DemoSettings.Sfx * DemoSettings.Master);
            return true;
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
            _sfx.PlayOneShot(clip, volume * DemoSettings.Sfx * DemoSettings.Master);
        }

        private static void Beep(float hz, float duration, float volume)
        {
            Chord(new[] { hz }, duration, volume);
        }
    }

    /// <summary>Lerps campus ambient beds so F6/F7 crossfade instead of hard-cut.</summary>
    public sealed class DemoAudioHost : MonoBehaviour
    {
        private AudioSource _a;
        private AudioSource _b;
        private float _targetA;
        private float _targetB;

        public void Bind(AudioSource a, AudioSource b)
        {
            _a = a;
            _b = b;
        }

        public void SetTargets(float a, float b, bool instant)
        {
            _targetA = a;
            _targetB = b;
            if (!instant) return;
            if (_a != null) _a.volume = a;
            if (_b != null) _b.volume = b;
        }

        private void Update()
        {
            float k = 1f - Mathf.Exp(-3.2f * Time.unscaledDeltaTime);
            if (_a != null)
                _a.volume = Mathf.Lerp(_a.volume, _targetA, k);
            if (_b != null)
                _b.volume = Mathf.Lerp(_b.volume, _targetB, k);
        }
    }
}
