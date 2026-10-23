using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Boots <see cref="ErrorLog"/> before the first scene: hooks Unity's threaded log callback and
    /// the unhandled-exception events, keeps the header's world/seed/sol current, and writes on a
    /// timer, on errors, on pause/quit. The pause menu's bug-report chip calls <see cref="OpenLogsFolder"/>.
    /// </summary>
    public sealed class ErrorLogHook : MonoBehaviour
    {
        /// <summary>Ordinary lines reach disk at most this often; errors go at once.</summary>
        private const float PeriodicFlushSeconds = 10f;
        private const float ContextSeconds = 1f;

        private static ErrorLogHook _instance;
        private static bool _hooked;

        private GameLoop _loop;
        private float _nextContext;
        private float _nextLoopLookup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain reload may be off in the editor: drop last session's handlers before re-hooking.
            Unhook();
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (!ErrorLog.Enabled) return;
            try
            {
                ErrorLog.DefaultDirectory = Path.Combine(Application.persistentDataPath, ErrorLog.FolderName);
            }
            catch (Exception)
            {
                ErrorLog.Enabled = false;
                return;
            }
            ErrorLog.Begin(CaptureSession(), DateTime.UtcNow);
            Hook();
            if (_instance == null)
            {
                var go = new GameObject("ErrorLogHook");
                go.hideFlags = HideFlags.HideInHierarchy;
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ErrorLogHook>();
            }
        }

        private static ErrorLogSession CaptureSession() => new ErrorLogSession
        {
            GameVersion = Application.version,
            BuildGuid = Application.buildGUID,
            UnityVersion = Application.unityVersion,
            Platform = Application.platform.ToString(),
            OperatingSystem = SystemInfo.operatingSystem,
            Device = SystemInfo.deviceModel,
            Graphics = SystemInfo.graphicsDeviceName,
            MemoryMb = SystemInfo.systemMemorySize,
            World = BodySeed.Body.ToString(),
            Seed = BodySeed.Current
        };

        private static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            Application.logMessageReceivedThreaded += OnLog;
            Application.quitting += OnQuitting;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
            TaskScheduler.UnobservedTaskException += OnUnobservedTask;
        }

        private static void Unhook()
        {
            if (!_hooked) return;
            _hooked = false;
            Application.logMessageReceivedThreaded -= OnLog;
            Application.quitting -= OnQuitting;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandled;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTask;
        }

        // Any thread. ErrorLog locks and never logs back into Unity.
        private static void OnLog(string condition, string stackTrace, LogType type) =>
            ErrorLog.Record(type, condition, stackTrace);

        private static void OnUnhandled(object sender, UnhandledExceptionEventArgs args)
        {
            var e = args.ExceptionObject as Exception;
            ErrorLog.Record(LogType.Exception,
                "[Unhandled" + (args.IsTerminating ? ", terminating" : "") + "] " +
                (e != null ? e.GetType().Name + ": " + e.Message : Convert.ToString(args.ExceptionObject)),
                e != null ? e.StackTrace : "");
            ErrorLog.Flush();
        }

        private static void OnUnobservedTask(object sender, UnobservedTaskExceptionEventArgs args)
        {
            var e = args.Exception != null ? args.Exception.GetBaseException() : null;
            ErrorLog.Record(LogType.Exception,
                "[Unobserved task] " + (e != null ? e.GetType().Name + ": " + e.Message : "unknown"),
                e != null ? e.StackTrace : "");
        }

        private static void OnQuitting() => ErrorLog.Flush();

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now >= _nextContext)
            {
                _nextContext = now + ContextSeconds;
                RefreshContext(now);
            }
            ErrorLog.FlushIfDirty(PeriodicFlushSeconds);
        }

        private void RefreshContext(float now)
        {
            if (_loop == null && now >= _nextLoopLookup)
            {
                _nextLoopLookup = now + 5f;
                _loop = FindFirstObjectByType<GameLoop>();
            }
            int sol = 0;
            string screen = "";
            if (_loop != null)
            {
                float elapsed = _loop.Mission != null ? _loop.Mission.MissionElapsed : 0f;
                sol = 1 + Mathf.FloorToInt(elapsed / MajestyEconomy.DaySeconds);
                screen = _loop.Screen.ToString();
            }
            ErrorLog.SetContext(BodySeed.Body.ToString(), BodySeed.Current, sol, screen);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) ErrorLog.Flush();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                ErrorLog.Flush();
                _instance = null;
            }
        }

        /// <summary>
        /// Pause menu "report a bug": write the log now, put its path on the clipboard, and open the
        /// folder. Returns the path that was copied (null if nothing could be written).
        /// </summary>
        public static string OpenLogsFolder()
        {
            ErrorLog.Flush();
            string dir = ErrorLog.Directory;
            string latest = ErrorLog.LastWrittenPath ?? ErrorLog.LatestLogPath();
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception)
            {
                // OpenURL below still shows the nearest folder the OS can find.
            }
            GUIUtility.systemCopyBuffer = !string.IsNullOrEmpty(latest) ? latest : dir;
            Application.OpenURL(ErrorLog.FolderUrl(dir));
            return latest;
        }
    }
}
