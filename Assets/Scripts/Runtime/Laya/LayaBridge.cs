using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Optional link to a local Laya typed-decision server (see Docs/LAYA_LOCAL_AI.md).
    /// Off unless enabled by <c>-laya [url]</c>, <c>SOLAR_LAYA_URL</c>, or the
    /// <see cref="PrefKey"/> PlayerPref. Requests run off the main thread; agents poll their
    /// <see cref="LayaTicket"/> on later think ticks and fall back to the utility brain whenever
    /// the server is slow, offline, or unsure. Nothing here ever blocks a frame.
    /// </summary>
    public sealed class LayaBridge : MonoBehaviour
    {
        public const string PrefKey = "SM_Set_LayaAI";
        public const string DefaultUrl = "http://127.0.0.1:8765";

        [Tooltip("Base URL of laya-serve or Tools/laya_sidecar.")]
        public string baseUrl = DefaultUrl;
        [Tooltip("Top probability Laya must reach before its pick replaces the utility pick.")]
        [Range(0f, 1f)] public float minProbability = 0.35f;
        [Tooltip("Concurrent requests in flight across all agents.")]
        [Min(1)] public int maxInFlight = 6;
        [Min(0.05f)] public float timeoutSeconds = 1.0f;
        [Tooltip("Seconds between /health probes while offline.")]
        [Min(1f)] public float probeInterval = 10f;

        static LayaBridge _instance;
        static bool _resolved;

        HttpClient _http;
        volatile bool _online;
        volatile bool _probing;
        float _probeTimer;
        int _inFlight;

        // Stats — read from the main thread, written by request tasks.
        int _answered;
        int _failed;
        double _latencyMsTotal;

        public bool Online => _online;
        public int Answered => _answered;
        public int Failed => _failed;
        public float MeanLatencyMs => _answered > 0 ? (float)(_latencyMsTotal / _answered) : 0f;
        public int Overrides { get; internal set; }

        /// <summary>The live bridge, or null when Laya is not enabled for this run.</summary>
        public static LayaBridge Instance
        {
            get
            {
                if (_resolved) return _instance;
                _resolved = true;
                string url = ResolveUrl();
                if (url == null) return null;
                var go = new GameObject("LayaBridge");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<LayaBridge>();
                _instance.baseUrl = url.TrimEnd('/');
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            _resolved = false;
        }

        /// <summary>Ready to take a request right now.</summary>
        public bool CanAsk => _online && System.Threading.Volatile.Read(ref _inFlight) < maxInFlight;

        static string ResolveUrl()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-laya", StringComparison.OrdinalIgnoreCase)) continue;
                return i + 1 < args.Length && args[i + 1].StartsWith("http") ? args[i + 1] : DefaultUrl;
            }
            string env = Environment.GetEnvironmentVariable("SOLAR_LAYA_URL");
            if (!string.IsNullOrEmpty(env)) return env;
            return PlayerPrefs.GetInt(PrefKey, 0) == 1 ? DefaultUrl : null;
        }

        void Awake()
        {
            // Local server: never route through a system HTTP proxy.
            _http = new HttpClient(new HttpClientHandler { UseProxy = false })
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
            _probeTimer = 0f;
        }

        void OnDestroy()
        {
            _http?.Dispose();
            _http = null;
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            if (_online || _probing) return;
            _probeTimer -= Time.unscaledDeltaTime;
            if (_probeTimer > 0f) return;
            _probeTimer = probeInterval;
            _ = Probe();
        }

        async Task Probe()
        {
            _probing = true;
            try
            {
                using var res = await _http.GetAsync(baseUrl + "/health").ConfigureAwait(false);
                bool ok = res.IsSuccessStatusCode;
                if (ok && !_online) Debug.Log($"[Laya] online at {baseUrl}");
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

        /// <summary>Starts one choice request. Returns null if the bridge is offline or saturated.</summary>
        public LayaTicket Ask(string requestJson)
        {
            if (!CanAsk || _http == null) return null;
            var ticket = new LayaTicket();
            System.Threading.Interlocked.Increment(ref _inFlight);
            _ = Send(requestJson, ticket);
            return ticket;
        }

        async Task Send(string body, LayaTicket ticket)
        {
            var started = DateTime.UtcNow;
            try
            {
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var res = await _http.PostAsync(baseUrl + LayaProtocol.Route, content).ConfigureAwait(false);
                string text = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (res.IsSuccessStatusCode && LayaProtocol.TryParseChoice(text, out var choice))
                {
                    lock (this)
                    {
                        _answered++;
                        _latencyMsTotal += (DateTime.UtcNow - started).TotalMilliseconds;
                    }
                    ticket.Complete(choice);
                    return;
                }
                lock (this) _failed++;
                ticket.Complete(null);
            }
            catch (Exception)
            {
                // Connection refused / timeout: go back to probing; agents keep the utility brain.
                lock (this) _failed++;
                _online = false;
                ticket.Complete(null);
            }
            finally
            {
                System.Threading.Interlocked.Decrement(ref _inFlight);
            }
        }
    }

    /// <summary>One in-flight Laya request, polled from the main thread.</summary>
    public sealed class LayaTicket
    {
        volatile bool _done;
        LayaChoice _answer;

        public bool Done => _done;
        /// <summary>Null when the request failed or was malformed. Valid once <see cref="Done"/>.</summary>
        public LayaChoice Answer => _done ? _answer : null;

        internal void Complete(LayaChoice answer)
        {
            _answer = answer;
            _done = true;
        }
    }
}
