using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Live text-to-speech from the local voice server (Tools/audio/voice_server.py, Kokoro-82M).
    /// Speaks lines that could not be baked: the hero narrator's LLM lines and Overseer lines with
    /// numbers in them. Off unless Settings → HERO VOICES · LOCAL AI, <c>-voice-server [url]</c>,
    /// or <c>SOLAR_VOICE_URL</c>. With no server every line falls back to the baked bank.
    /// See Docs/AUDIO.md.
    /// </summary>
    public sealed class VoiceServerLink : MonoBehaviour
    {
        public const string DefaultUrl = "http://127.0.0.1:8081";
        public const string Route = "/v1/audio/speech";
        public const int MaxChars = 240;

        private static VoiceServerLink _instance;
        private static string _cliUrl;
        private static bool _argsRead;

        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private HttpClient _http;
        private string _baseUrl = DefaultUrl;
        private volatile bool _online;
        private volatile bool _probing;
        private volatile int _inFlight;
        private float _probeTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _argsRead = false;
            _cliUrl = null;
        }

        public static bool Enabled
        {
            get
            {
                ReadArgs();
                return DemoSettings.HeroVoices || _cliUrl != null;
            }
        }

        /// <summary>True once the server has answered a health probe.</summary>
        public static bool Online => Enabled && Instance != null && _instance._online;

        private static void ReadArgs()
        {
            if (_argsRead) return;
            _argsRead = true;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-voice-server", StringComparison.OrdinalIgnoreCase)) continue;
                _cliUrl = i + 1 < args.Length && args[i + 1].StartsWith("http") ? args[i + 1] : DefaultUrl;
            }
            string env = Environment.GetEnvironmentVariable("SOLAR_VOICE_URL");
            if (_cliUrl == null && !string.IsNullOrEmpty(env)) _cliUrl = env;
        }

        private static VoiceServerLink Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (!Enabled) return null;
                var go = new GameObject("SM_VoiceServerLink");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<VoiceServerLink>();
                _instance._baseUrl = (_cliUrl ?? DefaultUrl).TrimEnd('/');
                return _instance;
            }
        }

        /// <summary>
        /// Ask for <paramref name="text"/> in <paramref name="speaker"/>'s voice (a cast.json key such
        /// as "overseer" or "hero.EngineerBot"). <paramref name="onClip"/> runs on the main thread
        /// with the clip, or null on failure. Returns false when nothing was sent.
        /// </summary>
        public static bool Request(string speaker, string text, Action<AudioClip> onClip)
        {
            var link = Instance;
            if (link == null || !link._online || link._http == null) return false;
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(speaker)) return false;
            // A laptop CPU synthesises roughly one line a second; queueing more only makes them late.
            if (link._inFlight >= 2) return false;
            if (text.Length > MaxChars) text = text.Substring(0, MaxChars);

            var sb = new StringBuilder(text.Length + 96);
            sb.Append("{\"model\":\"kokoro\",\"response_format\":\"wav\",\"voice\":");
            LocalJson.AppendString(sb, speaker);
            sb.Append(",\"input\":");
            LocalJson.AppendString(sb, text);
            sb.Append('}');

            link._inFlight++;
            _ = link.Send(sb.ToString(), speaker, onClip);
            return true;
        }

        private void Awake()
        {
            _http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(10) };
        }

        private void OnDestroy()
        {
            _http?.Dispose();
            _http = null;
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            while (_mainThread.TryDequeue(out var cb))
            {
                try { cb(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            if (!Enabled || _online || _probing) return;
            _probeTimer -= Time.unscaledDeltaTime;
            if (_probeTimer > 0f) return;
            _probeTimer = 10f;
            _ = Probe();
        }

        private async Task Probe()
        {
            _probing = true;
            try
            {
                using var res = await _http.GetAsync(_baseUrl + "/health").ConfigureAwait(false);
                bool ok = res.IsSuccessStatusCode;
                if (ok && !_online) Debug.Log($"[Voice] live TTS online at {_baseUrl}");
                _online = ok;
            }
            catch (Exception)
            {
                _online = false;
            }
            finally
            {
                _probing = false;
            }
        }

        private async Task Send(string body, string speaker, Action<AudioClip> onClip)
        {
            byte[] wav = null;
            try
            {
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var res = await _http.PostAsync(_baseUrl + Route, content).ConfigureAwait(false);
                if (res.IsSuccessStatusCode)
                    wav = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                _online = false;
            }

            // Decode off the main thread; only AudioClip creation has to happen on it.
            bool ok = WavDecoder.TryDecode(wav, out float[] samples, out int channels, out int rate);
            _mainThread.Enqueue(() =>
            {
                _inFlight = Mathf.Max(0, _inFlight - 1);
                AudioClip clip = null;
                if (ok && samples.Length >= channels)
                {
                    clip = AudioClip.Create("live_" + speaker, samples.Length / channels, channels, rate, false);
                    clip.SetData(samples, 0);
                }
                onClip?.Invoke(clip);
            });
        }
    }
}
