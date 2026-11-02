using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SolarMajesty
{
    /// <summary>
    /// Runs the bundled AI pack (the <c>ai</c> folder next to the game), so a player installs nothing:
    /// llama.cpp serves the Qwen3 lines and chat models, Kokoro speaks, and <see cref="HeroNarrator"/>
    /// and <see cref="HeroSpeaker"/> talk to them on 127.0.0.1. The plan (<see cref="LocalAiPlan"/>) is
    /// made at launch; the processes start only while the player has BOT LINES or SPOKEN on in
    /// Settings (both off by default), and stop again a little after both are turned off.
    ///
    /// The model processes are tied to the game: a Windows job object kills them if the game
    /// crashes or is killed, and they run below normal priority so frames stay smooth. Missing
    /// parts, or a PC with too little memory, simply leave the baked lines and the built-in brain in
    /// charge. Off with <c>-noai</c> or <c>SOLAR_NO_AI=1</c>. A developer's own servers
    /// (<c>-narrator</c>, <c>SOLAR_NARRATOR_URL</c>, <c>-voice</c>, <c>SOLAR_VOICE_URL</c>) win.
    /// Logs: <c>%USERPROFILE%\AppData\LocalLow\SolarMajesty\Solar Majesty\ai\*.log</c>.
    /// See Docs/LOCAL_AI_PACK.md.
    /// </summary>
    public sealed class LocalAiHost : MonoBehaviour
    {
        public enum State { Off, Idle, Starting, Online, Partial, Failed }

        public const string PackFolder = "ai";
        private const float StartupTimeoutSeconds = 240f;
        /// <summary>Settings turned off this long stop the models (several GB of RAM); a quick re-toggle keeps them.</summary>
        private const float StopAfterSeconds = 30f;

        private static LocalAiHost _instance;

        private sealed class Running
        {
            public LocalAiService Spec;
            public Process Process;
            public StreamWriter Log;
            public volatile bool Up;
            public volatile bool Died;
            public double StartedAt;
        }

        private readonly List<Running> _running = new List<Running>(4);
        private LocalAiPlan _plan;
        private string _pack;
        private HttpClient _http;
        private string _logDir;
        private CancellationTokenSource _cts;
        private float _unwantedFor;
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        public static State Status { get; private set; } = State.Off;
        /// <summary>One line for the log and a debug panel.</summary>
        public static string StatusLine { get; private set; } = "Local AI: not installed";

        /// <summary>The pack is present and planned: the narrator and speaker use the URLs below.</summary>
        public static bool Installed => _instance != null && _instance._plan != null;
        /// <summary>Base URL for lines and chat (the router, or the lines server alone), or null.</summary>
        public static string NarratorUrl => Installed ? _instance._plan.NarratorUrl : null;
        public static string NarratorModel => Installed ? _instance._plan.NarratorModel : null;
        /// <summary>Null when the chat model is left out; conversation then uses the small model.</summary>
        public static string ChatModel => Installed ? _instance._plan.ChatModel : null;
        public static string VoiceUrl => Installed ? _instance._plan.VoiceUrl : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            Status = State.Off;
            StatusLine = "Local AI: not installed";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            try
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                string pack = FindPack();
                if (pack == null) return;
                if (Disabled(out string why))
                {
                    StatusLine = "Local AI: off (" + why + ")";
                    Debug.Log("[LocalAI] " + StatusLine);
                    return;
                }
                var go = new GameObject("LocalAiHost");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<LocalAiHost>();
                _instance.Plan(pack);
#endif
            }
            catch (Exception e)
            {
                Status = State.Failed;
                StatusLine = "Local AI: could not start";
                Debug.LogWarning("[LocalAI] " + e.Message);
            }
        }

        private static string FindPack()
        {
            string env = Environment.GetEnvironmentVariable("SOLAR_AI_DIR");
            if (!string.IsNullOrEmpty(env)) return Directory.Exists(env) ? env : null;
            // The editor plays from the project; only an explicit SOLAR_AI_DIR enables the pack there.
            if (Application.isEditor) return null;
            string exeDir = Path.GetDirectoryName(Application.dataPath);
            string dir = Path.Combine(exeDir ?? "", PackFolder);
            return Directory.Exists(dir) ? dir : null;
        }

        private static bool Disabled(out string why)
        {
            why = null;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "-noai", StringComparison.OrdinalIgnoreCase)) { why = "-noai"; return true; }
                if (string.Equals(a, "-narrator", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "-voice", StringComparison.OrdinalIgnoreCase))
                {
                    why = "servers set on the command line";
                    return true;
                }
            }
            if (Environment.GetEnvironmentVariable("SOLAR_NO_AI") == "1") { why = "SOLAR_NO_AI"; return true; }
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SOLAR_NARRATOR_URL")) ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SOLAR_VOICE_URL")))
            {
                why = "servers set in the environment";
                return true;
            }
            return false;
        }

        private void Plan(string pack)
        {
            _pack = pack;
            _logDir = Path.Combine(Application.persistentDataPath, "ai");
            var plan = LocalAiPlan.Build(pack, SystemInfo.systemMemorySize, SystemInfo.processorCount, FreePort, File.Exists);
            foreach (string note in plan.Notes) Debug.Log("[LocalAI] " + note);
            if (!plan.Any)
            {
                Status = State.Off;
                StatusLine = "Local AI: off (" + plan.SkipReason + ")";
                Debug.Log("[LocalAI] " + StatusLine);
                Destroy(gameObject);
                return;
            }
            _plan = plan;
            Status = State.Idle;
            StatusLine = "Local AI: ready, starts when bot lines or spoken lines are on";
            Debug.Log($"[LocalAI] pack at {pack}: lines {plan.HasLines}, chat {plan.HasChat}, voice {plan.HasVoice}");
        }

        private void Update()
        {
            if (_plan == null) return;
            bool lines = DemoSettings.HeroVoices, spoken = DemoSettings.HeroSpeech;
            bool anyWanted = false;
            foreach (var spec in _plan.Services)
            {
                if (!LocalAiPlan.Wanted(spec.Role, lines, spoken)) continue;
                anyWanted = true;
                if (Find(spec) == null) StartService(spec);
            }
            if (anyWanted)
            {
                _unwantedFor = 0f;
                if (_cts == null && _running.Count > 0)
                {
                    _cts = new CancellationTokenSource();
                    _ = Watch(_cts.Token);
                }
                return;
            }
            if (_running.Count == 0) return;
            _unwantedFor += Time.unscaledDeltaTime;
            if (_unwantedFor < StopAfterSeconds) return;
            Debug.Log("[LocalAI] bot lines and spoken lines are off: stopping the models");
            StopAll();
            Status = State.Idle;
            StatusLine = "Local AI: stopped (settings off)";
        }

        private Running Find(LocalAiService spec)
        {
            for (int i = 0; i < _running.Count; i++)
                if (_running[i].Spec == spec) return _running[i];
            return null;
        }

        private void StartService(LocalAiService spec)
        {
            if (_running.Count == 0)
            {
                Directory.CreateDirectory(_logDir);
                ChildJob.Ensure();
                _http ??= new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(2) };
            }
            Status = State.Starting;
            StatusLine = "Local AI: loading models…";

            var run = new Running { Spec = spec, StartedAt = _clock.Elapsed.TotalSeconds };
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = spec.Exe,
                    Arguments = spec.Arguments,
                    WorkingDirectory = Path.GetDirectoryName(spec.Exe) ?? _pack,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
                psi.EnvironmentVariables["PYTHONNOUSERSITE"] = "1";
                // A player's own Python settings must not leak into the bundled one.
                psi.EnvironmentVariables.Remove("PYTHONHOME");
                psi.EnvironmentVariables.Remove("PYTHONPATH");

                run.Log = new StreamWriter(Path.Combine(_logDir, spec.LogName + ".log"), false, new UTF8Encoding(false))
                {
                    AutoFlush = true
                };
                run.Log.WriteLine($"{DateTime.Now:s} {spec.Exe} {spec.Arguments}");
                run.Process = Process.Start(psi);
                if (run.Process == null) throw new InvalidOperationException("process did not start");
                ChildJob.Add(run.Process);
                if (spec.BelowNormalPriority)
                {
                    try { run.Process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (Exception) { }
                }
                var log = run.Log;
                DataReceivedEventHandler pipe = (s, e) =>
                {
                    if (e.Data == null) return;
                    lock (log) { try { log.WriteLine(e.Data); } catch (Exception) { } }
                };
                run.Process.OutputDataReceived += pipe;
                run.Process.ErrorDataReceived += pipe;
                run.Process.BeginOutputReadLine();
                run.Process.BeginErrorReadLine();
                Debug.Log($"[LocalAI] starting {spec.Name} on :{spec.Port}");
            }
            catch (Exception e)
            {
                run.Died = true;
                Debug.LogWarning($"[LocalAI] {spec.Name} did not start: {e.Message}");
            }
            lock (_running) _running.Add(run);
        }

        /// <summary>Polls each service's health URL until it answers; notes any that exit.</summary>
        private async Task Watch(CancellationToken ct)
        {
            double started = _clock.Elapsed.TotalSeconds;
            bool reported = false;
            int lastTotal = 0;
            var snapshot = new List<Running>(4);
            while (!ct.IsCancellationRequested)
            {
                snapshot.Clear();
                lock (_running) snapshot.AddRange(_running);
                int up = 0, dead = 0;
                foreach (var r in snapshot)
                {
                    if (r.Died || (r.Process != null && SafeExited(r.Process)))
                    {
                        if (!r.Died)
                        {
                            r.Died = true;
                            r.Up = false;
                            Debug.LogWarning($"[LocalAI] {r.Spec.Name} stopped (see {r.Spec.LogName}.log)");
                        }
                        dead++;
                        continue;
                    }
                    if (!r.Up)
                    {
                        try
                        {
                            using var res = await _http.GetAsync(r.Spec.Url + r.Spec.HealthPath, ct).ConfigureAwait(false);
                            if (res.IsSuccessStatusCode)
                            {
                                r.Up = true;
                                Debug.Log($"[LocalAI] {r.Spec.Name} online ({_clock.Elapsed.TotalSeconds - r.StartedAt:0.0}s)");
                            }
                        }
                        catch (Exception) { }
                    }
                    if (r.Up) up++;
                }
                if (ct.IsCancellationRequested) return;

                int total = snapshot.Count;
                if (total != lastTotal)
                {
                    // SPOKEN turned on later adds the voice server: give it its own startup window.
                    lastTotal = total;
                    started = _clock.Elapsed.TotalSeconds;
                    reported = false;
                }
                State s = up == total ? State.Online : dead > 0 ? (up > 0 ? State.Partial : State.Failed) : State.Starting;
                bool timedOut = _clock.Elapsed.TotalSeconds - started > StartupTimeoutSeconds;
                if (s == State.Starting && timedOut) s = up > 0 ? State.Partial : State.Failed;
                Status = s;
                StatusLine = s switch
                {
                    State.Online => "Local AI: online",
                    State.Partial => $"Local AI: {up} of {total} parts online",
                    State.Failed => "Local AI: not running (see ai logs)",
                    _ => "Local AI: loading models…"
                };
                if (!reported && s != State.Starting)
                {
                    reported = true;
                    Debug.Log("[LocalAI] " + StatusLine);
                }
                try { await Task.Delay(s == State.Starting ? 1000 : 5000, ct).ConfigureAwait(false); }
                catch (TaskCanceledException) { return; }
            }
        }

        private static bool SafeExited(Process p)
        {
            try { return p.HasExited; }
            catch (Exception) { return true; }
        }

        private static int FreePort()
        {
            var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            l.Start();
            int port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private void StopAll()
        {
            try { _cts?.Cancel(); } catch (Exception) { }
            _cts = null;
            lock (_running)
            {
                foreach (var r in _running)
                {
                    try
                    {
                        if (r.Process != null && !r.Process.HasExited) r.Process.Kill();
                    }
                    catch (Exception) { }
                    try { r.Process?.Dispose(); } catch (Exception) { }
                    if (r.Log != null)
                        lock (r.Log) { try { r.Log.Dispose(); } catch (Exception) { } }
                }
                _running.Clear();
            }
            _unwantedFor = 0f;
        }

        private void OnApplicationQuit() => StopAll();

        private void OnDestroy()
        {
            StopAll();
            _http?.Dispose();
            _http = null;
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// A Windows job object that kills its processes when the game's handle closes, so a crash
        /// or a killed game never leaves several gigabytes of models running.
        /// </summary>
        private static class ChildJob
        {
            private static IntPtr _job;
            private static bool _tried;

            [StructLayout(LayoutKind.Sequential)]
            private struct BasicLimit
            {
                public long PerProcessUserTimeLimit;
                public long PerJobUserTimeLimit;
                public uint LimitFlags;
                public UIntPtr MinimumWorkingSetSize;
                public UIntPtr MaximumWorkingSetSize;
                public uint ActiveProcessLimit;
                public UIntPtr Affinity;
                public uint PriorityClass;
                public uint SchedulingClass;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct IoCounters
            {
                public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
                public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ExtendedLimit
            {
                public BasicLimit Basic;
                public IoCounters Io;
                public UIntPtr ProcessMemoryLimit;
                public UIntPtr JobMemoryLimit;
                public UIntPtr PeakProcessMemoryUsed;
                public UIntPtr PeakJobMemoryUsed;
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            private static extern IntPtr CreateJobObject(IntPtr attrs, string name);

            [DllImport("kernel32.dll")]
            private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint size);

            [DllImport("kernel32.dll")]
            private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

            private const int ExtendedLimitInformation = 9;
            private const uint KillOnJobClose = 0x2000;

            public static void Ensure()
            {
                if (_tried) return;
                _tried = true;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                try
                {
                    _job = CreateJobObject(IntPtr.Zero, null);
                    if (_job == IntPtr.Zero) return;
                    var info = new ExtendedLimit();
                    info.Basic.LimitFlags = KillOnJobClose;
                    int size = Marshal.SizeOf(typeof(ExtendedLimit));
                    IntPtr buf = Marshal.AllocHGlobal(size);
                    try
                    {
                        Marshal.StructureToPtr(info, buf, false);
                        if (!SetInformationJobObject(_job, ExtendedLimitInformation, buf, (uint)size)) _job = IntPtr.Zero;
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
                catch (Exception e)
                {
                    _job = IntPtr.Zero;
                    Debug.Log("[LocalAI] job object unavailable (" + e.Message + "); relying on quit cleanup");
                }
#endif
            }

            public static void Add(Process p)
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                if (_job == IntPtr.Zero) return;
                try { AssignProcessToJobObject(_job, p.Handle); }
                catch (Exception) { }
#endif
            }
        }
    }
}
