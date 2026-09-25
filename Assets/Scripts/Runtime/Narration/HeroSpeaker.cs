using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Live text-to-speech link to a local Kokoro server (Tools/local_ai/voice_server.py or
    /// Kokoro-FastAPI; both serve <c>POST /v1/audio/speech</c>). Off unless enabled by Settings →
    /// SPOKEN · LOCAL TTS, <c>-voice [url]</c>, or <c>SOLAR_VOICE_URL</c>. Speaks what can't be
    /// baked: the narrator's LLM lines and Overseer alerts with names in them. Machines get the
    /// helmet-radio filter. One line in flight at a time; without a server, baked voices remain.
    /// See Docs/AUDIO.md.
    /// </summary>
    public sealed class HeroSpeaker : MonoBehaviour
    {
        public const string DefaultUrl = "http://127.0.0.1:8880";

        private static HeroSpeaker _instance;
        private static string _cliUrl;
        private static bool _argsRead;

        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private HttpClient _http;
        private string _baseUrl = DefaultUrl;
        private volatile bool _online;
        private volatile bool _probing;
        private volatile bool _busy;
        private float _probeTimer;

        public bool Online => _online;
        public int Spoken { get; private set; }

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
                if (!_argsRead)
                {
                    _argsRead = true;
                    string[] args = Environment.GetCommandLineArgs();
                    for (int i = 0; i < args.Length; i++)
                        if (string.Equals(args[i], "-voice", StringComparison.OrdinalIgnoreCase))
                            _cliUrl = i + 1 < args.Length && args[i + 1].StartsWith("http") ? args[i + 1] : DefaultUrl;
                    string env = Environment.GetEnvironmentVariable("SOLAR_VOICE_URL");
                    if (_cliUrl == null && !string.IsNullOrEmpty(env)) _cliUrl = env;
                }
                return DemoSettings.HeroSpeech || _cliUrl != null;
            }
        }

        private static HeroSpeaker Instance
        {
            get
            {
                if (!Enabled) return null;
                if (_instance != null) return _instance;
                var go = new GameObject("HeroSpeaker");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<HeroSpeaker>();
                _instance._baseUrl = (_cliUrl ?? DefaultUrl).TrimEnd('/');
                return _instance;
            }
        }

        /// <summary>Makes sure the server probe is running (call when narration turns on).</summary>
        public static void Warm() => _ = Instance;

        /// <summary>True once the TTS server has answered a probe.</summary>
        public static bool Ready => Enabled && Instance != null && _instance._online && _instance._http != null;

        /// <summary>
        /// Synthesise <paramref name="line"/> in <paramref name="voice"/> and hand back a clip on the
        /// main thread (null on failure). Playback, placement and rationing belong to the caller
        /// (CharacterVoice for heroes, OverseerVoice for Grok), so live lines obey the same
        /// VoiceDirector as baked barks and are never spoken twice. Returns false when nothing was
        /// sent: speech off, server down, another line in flight, or voices muted.
        /// </summary>
        public static bool Request(in HeroVoiceSpec voice, string line, Action<AudioClip> onClip)
        {
            if (string.IsNullOrEmpty(line)) return false;
            var s = Instance;
            if (s == null || !s._online || s._http == null || s._busy) return false;
            if (SoundBus.Volume(SoundChannel.Voice) <= 0.001f) return false;

            s._busy = true;
            _ = s.Fetch(HeroSpeech.BuildRequest(line, voice), voice.Robot, clipData =>
            {
                s._busy = false;
                AudioClip clip = null;
                if (clipData != null)
                {
                    clip = AudioClip.Create("HeroLine", clipData.Samples.Length / clipData.Channels,
                        clipData.Channels, clipData.Rate, false);
                    clip.SetData(clipData.Samples, 0);
                    s.Spoken++;
                }
                onClip?.Invoke(clip);
            });
            return true;
        }

        private sealed class ClipData
        {
            public float[] Samples;
            public int Channels;
            public int Rate;
        }

        private void Awake()
        {
            _http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(6) };
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
                using var res = await _http.GetAsync(_baseUrl + "/v1/models").ConfigureAwait(false);
                bool ok = res.IsSuccessStatusCode;
                if (ok && !_online) Debug.Log($"[Voice] online at {_baseUrl}");
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

        private async Task Fetch(string body, float robot, Action<ClipData> onMainThread)
        {
            ClipData data = null;
            try
            {
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var res = await _http.PostAsync(_baseUrl + HeroSpeech.Route, content).ConfigureAwait(false);
                if (res.IsSuccessStatusCode)
                {
                    byte[] wav = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    // Decode + filter here, off the main thread; Unity objects are made on the main thread.
                    if (HeroSpeech.TryDecodeWav(wav, out float[] samples, out int ch, out int rate) &&
                        samples.Length / (float)(ch * rate) <= HeroSpeech.MaxSeconds)
                    {
                        HeroSpeech.ApplyRobot(samples, ch, rate, robot);
                        data = new ClipData { Samples = samples, Channels = ch, Rate = rate };
                    }
                }
            }
            catch (Exception)
            {
                _online = false;
            }
            _mainThread.Enqueue(() => onMainThread(data));
        }
    }
}
