using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Hero voices and hero conversation from a small local LLM behind any OpenAI-compatible
    /// server (llama.cpp llama-server, Ollama, mlx_lm.server, LM Studio). Off unless enabled by
    /// Settings → HERO VOICES, <c>-narrator [url]</c>, or <c>SOLAR_NARRATOR_URL</c>. Ambient lines
    /// replace the hero's template flavour line when they arrive in time; the player can also talk
    /// to a selected hero, with replies streamed word by word. With no server the templates stay.
    /// Presentation only — the model voices decisions, it never makes them.
    /// See Docs/HERO_NARRATION.md.
    /// </summary>
    public sealed class HeroNarrator : MonoBehaviour
    {
        public const string DefaultUrl = "http://127.0.0.1:8080";
        public const string OllamaUrl = "http://127.0.0.1:11434";
        public const string DefaultModel = "local";

        /// <summary>Ambient one-liners: small models that answer in well under a second on a laptop, best first.</summary>
        private static readonly string[] PreferredModels = { "qwen3:1.7b", "qwen3:1.7b-q4_K_M", "qwen3:0.6b" };

        /// <summary>
        /// Conversation: the 4B instruct build holds a character far better and still streams its
        /// first word in ~0.15 s on Apple Silicon. Never Ollama's plain <c>qwen3:4b</c> tag — that is
        /// the thinking-only build and cannot be told to stop reasoning. Falls back to the ambient model.
        /// </summary>
        private static readonly string[] PreferredChatModels =
            { "qwen3:4b-instruct-2507-q4_K_M", "qwen3:4b-instruct", "qwen3:4b-instruct-2507-q8_0" };

        private static HeroNarrator _instance;
        private static string _cliUrl;
        private static string _cliModel;
        private static string _cliChatModel;
        private static bool _argsRead;

        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private readonly NarrationScheduler _scheduler = new NarrationScheduler();
        private readonly Dictionary<SpecialistAgent, HeroChatLog> _logs = new Dictionary<SpecialistAgent, HeroChatLog>();
        private HttpClient _http;
        private volatile string _baseUrl = DefaultUrl;
        private volatile string _model = DefaultModel;
        private volatile string _chatModel = DefaultModel;
        private volatile bool _online;
        private volatile bool _probing;
        private volatile bool _isOllama;
        private float _probeTimer;
        private float _warmTimer;
        private GameLoop _loop;

        // Conversation state (main thread).
        private SpecialistAgent _chatAgent;
        private CancellationTokenSource _chatCts;
        private bool _replying;
        private string _pendingRaw = "";
        private float _sentAt;
        private float _firstWordAt;
        private float _repliedAt = float.NegativeInfinity;
        private string _lastError;

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
                else if (string.Equals(args[i], "-chat-model", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    _cliChatModel = args[i + 1];
            }
            string envUrl = Environment.GetEnvironmentVariable("SOLAR_NARRATOR_URL");
            if (_cliUrl == null && !string.IsNullOrEmpty(envUrl)) _cliUrl = envUrl;
            string envModel = Environment.GetEnvironmentVariable("SOLAR_NARRATOR_MODEL");
            if (_cliModel == null && !string.IsNullOrEmpty(envModel)) _cliModel = envModel;
            string envChat = Environment.GetEnvironmentVariable("SOLAR_CHAT_MODEL");
            if (_cliChatModel == null && !string.IsNullOrEmpty(envChat)) _cliChatModel = envChat;
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
                _instance._chatModel = _cliChatModel ?? _instance._model;
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
        /// Something happened to this hero. Always remembered for conversation; voiced only when
        /// the server is up, nobody is mid-conversation, and the scheduler thinks it's worth it.
        /// </summary>
        public static void Report(SpecialistAgent agent, NarrationKind kind, FlagHandle flag = null)
        {
            if (agent == null || agent.Data == null || !agent.IsAlive) return;
            var n = Instance;
            if (n == null) return;
            if (n._loop == null) n._loop = FindAnyObjectByType<GameLoop>();
            var moment = BuildMoment(agent, kind, flag ?? agent.ActiveFlag, n._loop);
            n.LogOf(agent).Remember(HeroConversation.EventLine(moment));

            // Conversation owns the model while it is open: ambient lines would queue ahead of replies.
            if (!n._online || n._http == null || n._chatAgent != null) return;
            bool selected = n._loop != null && n._loop.IsSelected(agent);
            int id = agent.GetHashCode();
            if (!n._scheduler.TryStart(id, kind, selected, Time.unscaledTime)) return;

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
                HeroSpeaker.Speak(agent, line); // aloud too, when the local TTS server is up
                n.Spoken++;
                if (kind == NarrationKind.LevelUp || (selected && NarrationScheduler.IsBigMoment(kind)))
                    n._loop?.LogOverseer($"{agent.Data.displayName} L{agent.Level}: “{line}”");
            });
        }

        // ---- conversation ------------------------------------------------------------------

        /// <summary>The server answered its last probe.</summary>
        public static bool ChatOnline => _instance != null && _instance._online;
        /// <summary>A reply to the open conversation is on its way.</summary>
        public static bool Replying => _instance != null && _instance._replying;
        /// <summary>Seconds the current reply has been pending.</summary>
        public static float ReplyWait => Replying ? Time.unscaledTime - _instance._sentAt : 0f;
        /// <summary>The first words have arrived for the current reply.</summary>
        public static bool ReplyStarted => Replying && _instance._firstWordAt > 0f;
        /// <summary>Seconds since the last reply finished.</summary>
        public static float SinceReply => _instance != null ? Time.unscaledTime - _instance._repliedAt : float.PositiveInfinity;
        /// <summary>Last server error for the open conversation, or null.</summary>
        public static string LastChatError => _instance?._lastError;
        /// <summary>Where the model is being served from, for the chat panel's status line.</summary>
        public static string ServerLabel => _instance == null ? "" : $"{_instance._chatModel} · {_instance._baseUrl.Replace("http://", "")}";

        /// <summary>What the hero has said so far in the reply being streamed, tidied for display.</summary>
        public static string PendingReply(SpecialistAgent agent) =>
            _instance == null || !_instance._replying || agent != _instance._chatAgent
                ? ""
                : HeroConversation.StreamingView(_instance._pendingRaw, agent.Record.Name);

        public static HeroChatLog LogFor(SpecialistAgent agent)
        {
            var n = Instance;
            return n == null || agent == null ? null : n.LogOf(agent);
        }

        /// <summary>Open a conversation: keeps the model loaded and primes this hero's prompt so the first reply is quick.</summary>
        public static void OpenChat(SpecialistAgent agent)
        {
            var n = Instance;
            if (n == null || agent == null) return;
            if (n._chatAgent != agent) n.CancelReply();
            n._chatAgent = agent;
            n._lastError = null;
            if (n._loop == null) n._loop = FindAnyObjectByType<GameLoop>();
            if (!n._online) return; // primed as soon as the probe finds the server
            n.KeepWarm();
            n.Prime(agent);
        }

        /// <summary>
        /// One-token request over this hero's prompt so the server caches it: the player's first
        /// message then only pays for its own words, not for reading the whole persona.
        /// </summary>
        private void Prime(SpecialistAgent agent)
        {
            if (agent == null || _replying || !_online) return;
            string prime = HeroConversation.BuildRequest(_chatModel, BuildPersona(agent), LogOf(agent).Lines, stream: false, maxTokens: 1);
            _ = Send(prime, _ => { });
        }

        public static void CloseChat()
        {
            var n = _instance;
            if (n == null) return;
            n.CancelReply();
            n._chatAgent = null;
        }

        /// <summary>The player says something to the hero in the open conversation.</summary>
        public static bool Say(SpecialistAgent agent, string text)
        {
            var n = Instance;
            string line = HeroConversation.CleanPlayerLine(text);
            if (n == null || agent == null || line == null || !n._online || n._replying) return false;
            n._chatAgent = agent;
            var log = n.LogOf(agent);
            log.Add(true, line);
            var persona = n.BuildPersona(agent);
            string body = HeroConversation.BuildRequest(n._chatModel, persona, log.Lines);

            n._replying = true;
            n._pendingRaw = "";
            n._sentAt = Time.unscaledTime;
            n._firstWordAt = 0f;
            n._lastError = null;
            n._chatCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            _ = n.StreamChat(body, agent, n._chatCts.Token);
            return true;
        }

        private void CancelReply()
        {
            if (_chatCts != null)
            {
                _chatCts.Cancel();
                _chatCts.Dispose();
                _chatCts = null;
            }
            _replying = false;
            _pendingRaw = "";
        }

        private HeroChatLog LogOf(SpecialistAgent agent)
        {
            if (!_logs.TryGetValue(agent, out var log))
            {
                // Destroyed heroes compare equal to null; drop their logs when a new one starts.
                var dead = new List<SpecialistAgent>();
                foreach (var key in _logs.Keys)
                    if (key == null) dead.Add(key);
                for (int i = 0; i < dead.Count; i++) _logs.Remove(dead[i]);
                log = new HeroChatLog();
                _logs[agent] = log;
            }
            return log;
        }

        private HeroPersona BuildPersona(SpecialistAgent a)
        {
            var kind = KindFor(a.LastDecision);
            var m = BuildMoment(a, kind, a.ActiveFlag, _loop);
            var record = a.Record;
            return new HeroPersona
            {
                Name = record.Name,
                Designation = record.Designation,
                ClassName = m.ClassName,
                Level = m.Level,
                Rank = record.Rank,
                Greed = m.Greed,
                Courage = m.Courage,
                Workaholic = m.Workaholic,
                Health01 = m.Health01,
                Fatigue01 = m.Fatigue01,
                Credits = m.Credits,
                World = m.World,
                Situation = a.IsIncapacitated
                    ? "knocked out cold in the dirt, waiting for the Fobot Yard to stand you back up"
                    : HeroNarration.Situation(m),
                Recent = LogOf(a).Events
            };
        }

        private async Task StreamChat(string body, SpecialistAgent agent, CancellationToken ct)
        {
            var raw = new StringBuilder(256);
            var whole = new StringBuilder(256);
            bool sawDelta = false;
            string error = null;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, _baseUrl + HeroNarration.Route)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                using var stream = await res.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                using (ct.Register(() => res.Dispose()))
                {
                    string line;
                    while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        whole.Append(line).Append('\n');
                        if (!HeroConversation.TryParseDelta(line, out string piece, out bool done)) continue;
                        sawDelta = true;
                        if (!string.IsNullOrEmpty(piece))
                        {
                            raw.Append(piece);
                            string snapshot = raw.ToString();
                            _mainThread.Enqueue(() => OnReplyPiece(agent, snapshot));
                        }
                        if (done) break;
                    }
                }
                if (!res.IsSuccessStatusCode)
                    error = HeroConversation.ErrorMessage(whole.ToString()) ?? $"server said {(int)res.StatusCode}";
                else if (!sawDelta && HeroConversation.TryParseMessage(whole.ToString(), out string full))
                    raw.Append(full); // server ignored "stream": one JSON body instead
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception e)
            {
                if (!ct.IsCancellationRequested)
                {
                    error = "lost the local model";
                    _online = false;
                    Debug.LogWarning($"[Chat] {e.GetType().Name}: {e.Message}");
                }
            }
            string final = raw.ToString();
            bool cancelled = ct.IsCancellationRequested;
            _mainThread.Enqueue(() => OnReplyDone(agent, final, error, cancelled));
        }

        private void OnReplyPiece(SpecialistAgent agent, string snapshot)
        {
            if (!_replying || agent != _chatAgent) return;
            _pendingRaw = snapshot;
            if (_firstWordAt <= 0f && HeroConversation.StreamingView(snapshot).Length > 0)
                _firstWordAt = Time.unscaledTime;
        }

        private void OnReplyDone(SpecialistAgent agent, string final, string error, bool cancelled)
        {
            if (!_replying || agent != _chatAgent) return;
            _replying = false;
            _pendingRaw = "";
            if (_chatCts != null) { _chatCts.Dispose(); _chatCts = null; }
            if (cancelled && string.IsNullOrEmpty(final))
            {
                _lastError = "no answer in time";
                return;
            }
            if (agent == null) return;
            var log = LogOf(agent);
            string reply = HeroConversation.CleanReply(final, agent.Record.Name, log.Lines);
            if (reply == null)
            {
                _lastError = error ?? "the line went quiet — say that again?";
                return;
            }
            log.Add(false, reply);
            _repliedAt = Time.unscaledTime;
            string card = HeroNarration.Sanitize(reply);
            if (card != null) agent.SetNarratedLine(card);
            float first = _firstWordAt > 0f ? _firstWordAt - _sentAt : 0f;
            Debug.Log($"[Chat] {agent.Record.Name}: first word {first * 1000f:F0} ms, full {(Time.unscaledTime - _sentAt) * 1000f:F0} ms");
        }

        // ---- plumbing ----------------------------------------------------------------------

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
            CancelReply();
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
            if (_chatAgent == null && _replying) CancelReply();

            if (_online && _isOllama)
            {
                // Ollama unloads an idle model after five minutes; reloading costs seconds on the next line.
                _warmTimer -= Time.unscaledDeltaTime;
                if (_warmTimer <= 0f) KeepWarm();
            }

            if (!Enabled || _online || _probing) return;
            _probeTimer -= Time.unscaledDeltaTime;
            if (_probeTimer > 0f) return;
            _probeTimer = 10f;
            _ = Probe();
        }

        private void KeepWarm()
        {
            _warmTimer = 240f;
            if (!_isOllama || _http == null) return;
            WarmModel(_model);
            if (_chatModel != _model) WarmModel(_chatModel);
        }

        private void WarmModel(string model)
        {
            var sb = new StringBuilder(96);
            sb.Append("{\"model\":");
            LocalJson.AppendString(sb, model);
            sb.Append(",\"keep_alive\":\"30m\"}");
            _ = PostQuiet(_baseUrl + "/api/generate", sb.ToString());
        }

        private async Task PostQuiet(string url, string body)
        {
            try
            {
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var res = await _http.PostAsync(url, content).ConfigureAwait(false);
            }
            catch (Exception) { }
        }

        private async Task Probe()
        {
            _probing = true;
            try
            {
                // With no explicit URL, try llama.cpp's port, then Ollama's.
                string[] candidates = _cliUrl != null
                    ? new[] { _baseUrl }
                    : new[] { DefaultUrl, OllamaUrl };
                for (int i = 0; i < candidates.Length && !_online; i++)
                {
                    string url = candidates[i].TrimEnd('/');
                    try
                    {
                        // /v1/models is served by llama.cpp, Ollama, mlx_lm.server and LM Studio alike.
                        using var res = await _http.GetAsync(url + "/v1/models").ConfigureAwait(false);
                        if (!res.IsSuccessStatusCode) continue;
                        string models = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                        _baseUrl = url;
                        if (_cliModel == null)
                        {
                            string pick = HeroConversation.PickModel(models, PreferredModels);
                            if (pick != null) _model = pick;
                        }
                        // An explicit -narrator-model means "this one" for chat too, unless -chat-model says otherwise.
                        if (_cliChatModel != null) _chatModel = _cliChatModel;
                        else if (_cliModel != null) _chatModel = _model;
                        else _chatModel = HeroConversation.PickModel(models, PreferredChatModels) ?? _model;
                        _isOllama = await IsOllama(url).ConfigureAwait(false);
                        _online = true;
                        Debug.Log($"[Narrator] online at {_baseUrl} (lines \"{_model}\", chat \"{_chatModel}\"{(_isOllama ? ", Ollama" : "")})");
                    }
                    catch (Exception) { }
                }
                if (_online)
                    _mainThread.Enqueue(() =>
                    {
                        KeepWarm();
                        if (_chatAgent != null) Prime(_chatAgent);
                    });
            }
            finally
            {
                _probing = false;
            }
        }

        private async Task<bool> IsOllama(string url)
        {
            try
            {
                using var res = await _http.GetAsync(url + "/api/version").ConfigureAwait(false);
                return res.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
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
