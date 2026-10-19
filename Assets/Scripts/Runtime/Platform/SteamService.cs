#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace SolarMajesty
{
    /// <summary>
    /// The game's only link to Steam: startup, achievements, overlay pause and rich presence.
    ///
    /// Steam is optional at runtime. If the client is not running, the DLL is missing, or the
    /// player passed <c>-nosteam</c>, the game runs normally and achievements stay local (they
    /// are pushed to Steam the next time it is available). Cloud saves need no code: the
    /// Steamworks Auto-Cloud rules in Docs/STEAM_RELEASE.md sync the Saves and Profile folders.
    /// </summary>
    public sealed class SteamService : MonoBehaviour
    {
        public static SteamService Instance { get; private set; }

        /// <summary>True when the Steam API started and callbacks are flowing.</summary>
        public static bool Ready { get; private set; }

        /// <summary>Why Steam is not running, for the log and the settings screen.</summary>
        public static string Status { get; private set; } = "Not started";

        /// <summary>Running on a Steam Deck (controller first, 1280x800).</summary>
        public static bool OnSteamDeck { get; private set; }

        /// <summary>Raised when the Steam overlay opens (true) or closes (false).</summary>
        public static event Action<bool> OverlayToggled;

        private const float PresenceInterval = 5f;
        private float _nextPresence;
        private bool _statsDirty;
        private GameLoop _loop;

#if !DISABLESTEAMWORKS
        private Callback<GameOverlayActivated_t> _overlay;
        private Callback<UserStatsReceived_t> _statsReceived;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Ready = false;
            Status = "Not started";
            OverlayToggled = null;
            OnSteamDeck = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("[Steam]");
            DontDestroyOnLoad(go);
            go.AddComponent<SteamService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            TryStart();
        }

        /// <summary>Bigger default HUD for a small screen held close; the player's slider still wins.</summary>
        private static void UseHandheldLayout()
        {
            HudScaleMath.Handheld = true;
            DemoSettings.RefreshHudScale(Screen.width, Screen.height);
        }

        private void TryStart()
        {
            if (SteamConfig.HandheldByArgs(Environment.GetCommandLineArgs())) UseHandheldLayout();
#if DISABLESTEAMWORKS
            Status = "Steam is not supported on this platform";
#else
            if (SteamConfig.SteamDisabledByArgs(Environment.GetCommandLineArgs()))
            {
                Status = "Disabled with " + SteamConfig.NoSteamArg;
                Debug.Log("[Steam] " + Status);
                return;
            }

            try
            {
                if (!Packsize.Test())
                {
                    Status = "Steamworks.NET was built for the wrong platform";
                    Debug.LogError("[Steam] " + Status);
                    return;
                }
                if (!DllCheck.Test())
                {
                    Status = "steam_api64 library is the wrong version";
                    Debug.LogError("[Steam] " + Status);
                    return;
                }

                if (!Application.isEditor && SteamConfig.RelaunchThroughSteam &&
                    SteamAPI.RestartAppIfNecessary(new AppId_t(SteamConfig.AppId)))
                {
                    // Started from the exe directly; Steam relaunches it with the overlay and DRM.
                    Status = "Relaunching through Steam";
                    Application.Quit();
                    return;
                }

                ESteamAPIInitResult result = SteamAPI.InitEx(out string error);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    Status = $"Steam unavailable ({result}): {error}";
                    Debug.Log("[Steam] " + Status + " — playing offline; achievements stay local.");
                    return;
                }
            }
            catch (DllNotFoundException e)
            {
                Status = "steam_api library not found";
                Debug.LogWarning("[Steam] " + Status + ": " + e.Message);
                return;
            }
            catch (Exception e)
            {
                Status = "Steam failed to start: " + e.Message;
                Debug.LogWarning("[Steam] " + Status);
                return;
            }

            Ready = true;
            OnSteamDeck = SteamUtils.IsSteamRunningOnSteamDeck();
            if (OnSteamDeck) UseHandheldLayout();
            Status = SteamConfig.IsTestApp ? "Connected (test app 480)" : "Connected";
            if (OnSteamDeck) Status += " on Steam Deck";
            Debug.Log($"[Steam] {Status} as {SteamFriends.GetPersonaName()} — app {SteamUtils.GetAppID()}");

            _overlay = Callback<GameOverlayActivated_t>.Create(OnOverlay);
            _statsReceived = Callback<UserStatsReceived_t>.Create(OnStatsReceived);
            Achievements.Earned += OnAchievementEarned;
            PushLocalAchievements();
            UpdatePresence(force: true);
#endif
        }

        private void Update()
        {
#if !DISABLESTEAMWORKS
            if (!Ready) return;
            SteamAPI.RunCallbacks();

            if (_statsDirty)
            {
                _statsDirty = false;
                SteamUserStats.StoreStats();
            }

            if (Time.unscaledTime >= _nextPresence)
                UpdatePresence(force: false);
#endif
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Shutdown();
            Instance = null;
        }

        private void OnApplicationQuit() => Shutdown();

        private void Shutdown()
        {
#if !DISABLESTEAMWORKS
            if (!Ready) return;
            Achievements.Earned -= OnAchievementEarned;
            if (_statsDirty) SteamUserStats.StoreStats();
            SteamFriends.ClearRichPresence();
            SteamAPI.Shutdown();
            Ready = false;
            Status = "Shut down";
#endif
        }

#if !DISABLESTEAMWORKS
        // ---- achievements ------------------------------------------------------------------------

        private void OnAchievementEarned(AchievementDef def)
        {
            if (def == null) return;
            Unlock(def.Id);
        }

        /// <summary>
        /// Achievements earned while offline (or before the Steam build) are pushed on every start,
        /// so a player never loses one because Steam was closed at the time.
        /// </summary>
        private void PushLocalAchievements()
        {
            var defs = Achievements.Definitions;
            for (int i = 0; i < defs.Count; i++)
            {
                if (Achievements.IsUnlocked(defs[i].Id))
                    Unlock(defs[i].Id);
            }
        }

        private void Unlock(AchievementId id)
        {
            string api = SteamConfig.AchievementApiName(id);
            if (SteamUserStats.GetAchievement(api, out bool already) && already) return;
            if (SteamUserStats.SetAchievement(api))
                _statsDirty = true;
            else if (!SteamConfig.IsTestApp)
                Debug.LogWarning($"[Steam] Achievement {api} is not defined on the partner site.");
        }

        private void OnStatsReceived(UserStatsReceived_t data)
        {
            if (data.m_nGameID != SteamUtils.GetAppID().m_AppId) return;
            if (data.m_eResult == EResult.k_EResultOK)
                PushLocalAchievements();
        }

        // ---- overlay -----------------------------------------------------------------------------

        private void OnOverlay(GameOverlayActivated_t data)
        {
            bool open = data.m_bActive != 0;
            if (open)
            {
                // Indirect control keeps running without the player; never let the overlay cost a colony.
                Loop()?.PausePlay();
            }
            OverlayToggled?.Invoke(open);
        }

        // ---- rich presence -----------------------------------------------------------------------

        private void UpdatePresence(bool force)
        {
            _nextPresence = Time.unscaledTime + PresenceInterval;
            GameLoop loop = Loop();
            bool inGame = loop != null && loop.Screen != DemoScreen.Title;
            bool paused = loop != null && (loop.Screen == DemoScreen.Paused || loop.Screen == DemoScreen.Settings);

            SteamFriends.SetRichPresence("world", loop != null ? loop.ActiveBody.ToString() : "Earth");
            SteamFriends.SetRichPresence("steam_display", SteamConfig.PresenceToken(inGame, paused));
        }
#endif

        private GameLoop Loop()
        {
            if (_loop == null) _loop = FindAnyObjectByType<GameLoop>();
            return _loop;
        }

        /// <summary>
        /// Steam's on-screen keyboard for a text field (hero chat, flag orders) when there is no
        /// physical keyboard: on the Deck or in Big Picture. It types into the focused field. The
        /// fields sit low on screen, so the keyboard is told to keep the bottom strip clear.
        /// </summary>
        public static void ShowFloatingKeyboard()
        {
#if !DISABLESTEAMWORKS
            if (!Ready) return;
            if (!OnSteamDeck && !SteamUtils.IsSteamInBigPictureMode()) return;
            int h = Screen.height / 4;
            SteamUtils.ShowFloatingGamepadTextInput(
                EFloatingGamepadTextInputMode.k_EFloatingGamepadTextInputModeModeSingleLine,
                0, Screen.height - h, Screen.width, h);
#endif
        }

        /// <summary>Open a Steam store or web page in the overlay, or the system browser without Steam.</summary>
        public static void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
#if !DISABLESTEAMWORKS
            if (Ready && SteamUtils.IsOverlayEnabled())
            {
                SteamFriends.ActivateGameOverlayToWebPage(url);
                return;
            }
#endif
            Application.OpenURL(url);
        }
    }
}
