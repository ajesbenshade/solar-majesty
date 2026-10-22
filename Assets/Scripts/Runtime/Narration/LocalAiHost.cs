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
    /// Starts the bundled AI pack (the <c>ai</c> folder next to the game) when the game launches, so
    /// a tester only double-clicks SolarMajesty.exe: llama.cpp serves the Qwen3 lines and chat models,
    /// Kokoro speaks, and the narrator, chat and speaker find them through the environment variables
    /// they already read. Everything is local (127.0.0.1) and nothing needs installing.
    ///
    /// The model processes are tied to the game: a Windows job object kills them if the game
    /// crashes or is killed, and they run below normal priority so frames stay smooth. Missing
    /// parts, or a PC with too little memory, simply leave the baked lines and the built-in brain in
    /// charge. Off with <c>-noai</c> or <c>SOLAR_NO_AI=1</c>. A developer's own servers
    /// (<c>-narrator</c>, <c>SOLAR_NARRATOR_URL</c>, <c>-voice</c>, <c>SOLAR_VOICE_URL</c>) win.
    /// Logs: <c>%LOCALAPPDATA%Low\SolarMajesty\Solar Majesty\ai\*.log</c>.
    /// </summary>
    public sealed class LocalAiHost : MonoBehaviour
    {
        public enum State { Off, Starting, Online, Partial, Failed }

        public const string PackFolder = "ai";
        private const float StartupTimeoutSeconds = 240f;

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
        private HttpClient _http;
        private string _logDir;
        private CancellationTokenSource _cts;
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        public static State Status { get; private set; } = State.Off;
        /// <summary>One line for a settings or debug panel.</summary>
        public static string StatusLine { get; private set; } = "Local AI: not installed";

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
                _instance.Begin(pack);
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

        private void Begin(string pack)
        {
            _logDir = Path.Combine(Application.persistentDataPath, "ai");
            Directory.CreateDirectory(_logDir);

            _plan = LocalAiPlan.Build(pack, SystemInfo.systemMemorySize, SystemInfo.processorCount, FreePort, File.Exists);
            foreach (string note in _plan.Notes) Debug.Log("[LocalAI] " + note);
            if (!_plan.Any)
            {
                Status = State.Off;
                StatusLine = "Local AI: off (" + _plan.SkipReason + ")";
                Debug.Log("[LocalAI] " + StatusLine);
                Destroy(gameObject);
                return;
            }

            // The narrator, chat and speaker read these on first use, which is after this runs.
            foreach (var kv in _plan.Environment())
                Environment.SetEnvironmentVariable(kv.Key, kv.Value);

            _http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(2) };
            _cts = new CancellationTokenSource();
            Status = State.Starting;
            StatusLine = "Local AI: loading models…";

            ChildJob.Ensure();
            foreach (var spec in _plan.Services) StartService(spec);
            _ = Watch(_cts.Token);
        }

        private void StartService(LocalAiService spec)
        {
            var run = new Running { Spec = spec, StartedAt = _clock.Elapsed.TotalSeconds };
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = spec.Exe,
                    Arguments = spec.Arguments,
                    WorkingDirectory = Path.GetDirectoryName(spec.Exe) ?? "",
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
                // A tester's own Python settings must not leak into the bundled one.
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
                DataReceivedEventHandler pipe = (s, e) =>
                {
                    if (e.Data == null) return;
                    lock (run.Log) { try { run.Log.WriteLine(e.Data); } catch (Exception) { } }
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
            _running.Add(run);
        }

        /// <summary>Polls each service's health URL until it answers; notes any that exit.</summary>
        private async Task Watch(CancellationToken ct)
        {
            double started = _clock.Elapsed.TotalSeconds;
            bool reported = false;
            while (!ct.IsCancellationRequested)
            {
                int up = 0, dead = 0;
                foreach (var r in _running)
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

                int total = _running.Count;
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
            foreach (var r in _running)
            {
                try
                {
                    if (r.Process != null && !r.Process.HasExited) r.Process.Kill();
                }
                catch (Exception) { }
                try { r.Log?.Dispose(); } catch (Exception) { }
            }
            _running.Clear();
            _http?.Dispose();
            _http = null;
        }

        private void OnApplicationQuit() => StopAll();

        private void OnDestroy()
        {
            StopAll();
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
