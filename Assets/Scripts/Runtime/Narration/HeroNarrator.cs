using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Hero voices from a small local LLM behind any OpenAI-compatible server (llama.cpp
    /// llama-server, Ollama, mlx_lm.server, LM Studio). Off unless enabled by Settings → HERO
    /// VOICES, <c>-narrator [url]</c>, or <c>SOLAR_NARRATOR_URL</c>. Lines replace the hero's
    /// template flavour line when they arrive in time; with no server the templates stay.
    /// Presentation only — the model voices decisions, it never makes them.
    /// See Docs/HERO_NARRATION.md.
    /// </summary>
    public sealed class HeroNarrator : MonoBehaviour
    {
        public const string DefaultUrl = "http://127.0.0.1:8080";
        public const string DefaultModel = "local";

        private static HeroNarrator _instance;
        private static string _cliUrl;
        private static string _cliModel;
        private static bool _argsRead;

        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private readonly NarrationScheduler _scheduler = new NarrationScheduler();
        private HttpClient _http;
        private string _baseUrl = DefaultUrl;
        private string _model = DefaultModel;
        private volatile bool _online;
        private volatile bool _probing;
        private float _probeTimer;
        private GameLoop _loop;

        public bool Online => _online;
        public int Spoken { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _argsRead = false;
        }

        /// <summary>True when narration is wanted for this run (setting, argument or environment).</summary>
        public static bool Enabled
        {
            get
            {
                ReadArgs();
                // Spoken lines need text lines to speak, so SPOKEN implies the narrator.
                return DemoSettings.HeroVoices || DemoSettings.HeroSpeech || _cliUrl != null;
            }
        }

        private static void ReadArgs()
        {
            if (_argsRead) return;
            _argsRead = true;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-narrator", StringComparison.OrdinalIgnoreCase))
                    _cliUrl = i + 1 < args.Length && args[i + 1].StartsWith("http") ? args[i + 1] : DefaultUrl;
                else if (string.Equals(args[i], "-narrator-model", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    _cliModel = args[i + 1];
            }
            string envUrl = Environment.GetEnvironmentVariable("SOLAR_NARRATOR_URL");
            if (_cliUrl == null && !string.IsNullOrEmpty(envUrl)) _cliUrl = envUrl;
            string envModel = Environment.GetEnvironmentVariable("SOLAR_NARRATOR_MODEL");
            if (_cliModel == null && !string.IsNullOrEmpty(envModel)) _cliModel = envModel;
        }

        private static HeroNarrator Instance
        {
            get
            {
                if (!Enabled) return null;
                if (_instance != null) return _instance;
                var go = new GameObject("HeroNarrator");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<HeroNarrator>();
                _instance._baseUrl = (_cliUrl ?? DefaultUrl).TrimEnd('/');
                _instance._model = _cliModel ?? DefaultModel;
                HeroSpeaker.Warm();
                return _instance;
            }
        }

        /// <summary>Brain decision → narration kind.</summary>
        public static NarrationKind KindFor(in BrainDecision d) => d.Action switch
        {
            SpecialistAction.PursueFlag => NarrationKind.Claim,
            SpecialistAction.Flee => NarrationKind.Flee,
            SpecialistAction.Hunt => NarrationKind.Hunt,
            SpecialistAction.Repair => NarrationKind.Repair,
            SpecialistAction.Rest => NarrationKind.Rest,
            _ => NarrationKind.Wander
        };

        /// <summary>
        /// Something happened to this hero. Cheap no-op when narration is off, the server is down,
        /// or the scheduler decides this moment isn't worth the model's time. Returns true when a
        /// line was requested, so the hero's voice can wait for it instead of barking.
        /// </summary>
        public static bool Report(SpecialistAgent agent, NarrationKind kind, FlagHandle flag = null)
        {
            if (agent == null || agent.Data == null || !agent.IsAlive) return false;
            var n = Instance;
            if (n == null || !n._online || n._http == null) return false;
            if (n._loop == null) n._loop = FindAnyObjectByType<GameLoop>();
            bool selected = n._loop != null && n._loop.IsSelected(agent);
            int id = agent.GetHashCode();
            if (!n._scheduler.TryStart(id, kind, selected, Time.unscaledTime)) return false;

            var moment = BuildMoment(agent, kind, flag ?? agent.ActiveFlag, n._loop);
            int stamp = agent.NarrationStamp;
            string body = HeroNarration.BuildRequest(n._model, moment);
            _ = n.Send(body, reply =>
            {
                n._scheduler.Finish();
                if (!HeroNarration.TryParseLine(reply, out string line)) return;
                if (agent == null || !agent.IsAlive) return;
                // A line about a decision the hero has already dropped would be wrong; level-ups
                // and refusals are events, not states, so they stand.
                bool stateful = kind != NarrationKind.LevelUp && kind != NarrationKind.Refused;
                if (stateful && agent.NarrationStamp != stamp) return;
                agent.SetNarratedLine(line);
                CharacterVoice.SpeakNarrated(agent, line); // aloud too, when SPOKEN · LOCAL TTS is up
                n.Spoken++;
                if (kind == NarrationKind.LevelUp || (selected && NarrationScheduler.IsBigMoment(kind)))
                    n._loop?.LogOverseer($"{agent.Data.displayName} L{agent.Level}: “{line}”");
            });
            return true;
        }

        private static HeroMoment BuildMoment(SpecialistAgent a, NarrationKind kind, FlagHandle flag, GameLoop loop)
        {
            var d = a.Data;
            var m = new HeroMoment
            {
                Kind = kind,
                ClassName = d.displayName,
                Level = a.Level,
                Greed = d.baseGreed,
                Courage = a.EffectiveCourage,
                Workaholic = d.workaholicBias,
                Health01 = a.HealthNormalized,
                Fatigue01 = a.Fatigue,
                Credits = Mathf.RoundToInt(a.Credits),
                World = loop != null && loop.BodyProfile != null ? loop.BodyProfile.DisplayName : null,
                Reason = a.LastReason
            };
            if (flag?.Data != null && (kind == NarrationKind.Claim || kind == NarrationKind.Refused))
            {
                m.FlagType = SpecialistFlavor.FlagShort(flag.Data.flagType);
                m.Bounty = Mathf.RoundToInt(flag.CurrentBounty);
                m.Orders = flag.Orders != null ? flag.Orders.text : null;
            }
            return m;
        }

        private void Awake()
        {
            // Local server: never through a system proxy. Small models on CPU can take a few seconds.
            _http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(8) };
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
                // /v1/models is served by llama.cpp, Ollama, mlx_lm.server and LM Studio alike.
                using var res = await _http.GetAsync(_baseUrl + "/v1/models").ConfigureAwait(false);
                bool ok = res.IsSuccessStatusCode;
                if (ok && !_online) Debug.Log($"[Narrator] online at {_baseUrl} (model \"{_model}\")");
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

        private async Task Send(string body, Action<string> onMainThread)
        {
            string reply = null;
            try
            {
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var res = await _http.PostAsync(_baseUrl + HeroNarration.Route, content).ConfigureAwait(false);
                string text = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (res.IsSuccessStatusCode) reply = text;
            }
            catch (Exception)
            {
                _online = false;
            }
            _mainThread.Enqueue(() => onMainThread(reply));
        }
    }
}
