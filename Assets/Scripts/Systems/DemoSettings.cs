using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// PlayerPrefs-backed demo settings, tutorial, and a single continue slot.
    /// </summary>
    public static class DemoSettings
    {
        public const string MasterKey = "SM_Set_Master";
        public const string SfxKey = "SM_Set_Sfx";
        public const string AmbientKey = "SM_Set_Ambient";
        public const string HudKey = "SM_Set_Hud";
        /// <summary>1 (or a missing key) follows screen height. 0 is an explicit slider choice.</summary>
        public const string HudAutoKey = "SM_Set_HudAuto";
        public const string InvertKey = "SM_Set_InvertPan";
        public const string TutorialKey = "SM_TutorialDone";
        public const string SaveFlagKey = "SM_SaveExists";
        public const string SaveRegKey = "SM_Save_Regolith";
        public const string SaveIceKey = "SM_Save_Ice";
        public const string SaveMetKey = "SM_Save_Metals";
        public const string SavePwrKey = "SM_Save_Power";
        public const string BootPlayKey = "SM_BootPlay";
        public const string FirstHourKey = "SM_FirstHourDemo";
        public const string QualityKey = "SM_Set_Quality";
        public const string FullscreenKey = "SM_Set_Fullscreen";
        public const string ResolutionWKey = "SM_Set_ResW";
        public const string ResolutionHKey = "SM_Set_ResH";
        public const string CampusKeyPrefix = "SM_Campus_";
        public const string EdgeScrollKey = "SM_Set_EdgeScroll";
        public const string ReduceMotionKey = "SM_Set_ReduceMotion";
        public const string ColorBlindKey = "SM_Set_ColorBlind";
        public const string FrameCapKey = "SM_Set_FrameCap";
        public const string MarsJexLessonsKey = "SM_Set_MarsGrokLessons";
        public const string DayCycleKey = "SM_Set_DayCycle";
        public const string DioramaCameraKey = "SM_Set_DioramaCamera";
        public const string TiltShiftKey = "SM_Set_TiltShift";
        public const string CloudShadowsKey = "SM_Set_CloudShadows";
        public const string HeroVoicesKey = "SM_Set_HeroVoices";
        public const string MusicKey = "SM_Set_Music";
        public const string VoiceKey = "SM_Set_Voice";
        public const string CharacterVoicesKey = "SM_Set_CharacterVoices";
        public const string HeroSpeechKey = "SM_Set_HeroSpeech";
        public const string PlanetArchitectureKey = "SM_Set_PlanetArchitecture";
        public const string RosterKeyPrefix = "SM_Roster_";

        public static float Master = 1f;
        public static float Sfx = 1f;
        public static float Ambient = 1f;
        public static float Music = 1f;
        public static float Voice = 1f;
        /// <summary>Applied IMGUI scale for this frame. Refreshed from auto or the explicit choice.</summary>
        public static float HudScale = 1f;
        /// <summary>True until the player drags the UI scale slider.</summary>
        public static bool HudScaleAuto = true;
        /// <summary>Last slider choice, in 5% steps. Stored even while auto is on.</summary>
        public static float HudScaleExplicit = 1f;
        public static bool InvertPan;
        public static bool TutorialDone;
        public static bool SaveExists;
        public static string SaveLoadNotice = "";
        public static int QualityIndex;
        public static bool Fullscreen = true;

        /// <summary>0 means keep the monitor's current mode. Otherwise an applied window size.</summary>
        public static int ResolutionWidth;
        public static int ResolutionHeight;

        public static readonly int[] ResolutionWidths = { 1280, 1280, 1366, 1600, 1920, 1920, 2560, 2560, 3840 };
        public static readonly int[] ResolutionHeights = { 720, 800, 768, 900, 1080, 1200, 1440, 1600, 2160 };

        /// <summary>One entry per width×height. Refresh rate is ignored so the list does not repeat.</summary>
        public readonly struct DisplayMode
        {
            public readonly int Width;
            public readonly int Height;
            public DisplayMode(int width, int height)
            {
                Width = width;
                Height = height;
            }
        }

        static List<DisplayMode> _displayModes;

        /// <summary>On unless the player has saved a choice. Missing key stays on; a stored 0 stays off.</summary>
        public static bool EdgeScroll = true;

        /// <summary>Accessibility: suppresses camera shake and non-essential pulsing.</summary>
        public static bool ReduceMotion;

        /// <summary>Accessibility palette. 0 off, 1 deuteranopia, 2 protanopia, 3 tritanopia.</summary>
        public static int ColorBlindMode;

        /// <summary>0 uses the platform default; otherwise a target frame rate.</summary>
        public static int FrameCap;

        /// <summary>When true, skip title after a New Game reload.</summary>
        public static bool BootStraightIntoPlay;

        /// <summary>
        /// Earth greed-beat demo. Default on. Settings can open the full campaign
        /// without deleting guilds, Belt, or Europa from the repo.
        /// </summary>
        public static bool FirstHourDemo;
        /// <summary>Mars optional training wheels. Default off — failure asides only.</summary>
        public static bool MarsJexLessons;

        /// <summary>Sun moves through golden hour and a blue-hour night. Off = the tuned still look.</summary>
        public static bool DayCycle = true;

        /// <summary>
        /// Perspective "diorama" camera: the sky and horizon appear when zoomed out. Off = the classic
        /// orthographic overseer view. Also forced on for one run by the <c>-diorama</c> argument.
        /// </summary>
        public static bool DioramaCamera;

        /// <summary>Miniature-style depth of field when zoomed in.</summary>
        public static bool TiltShift = true;

        /// <summary>Drifting cloud shadows on worlds with weather.</summary>
        public static bool CloudShadows = true;

        /// <summary>Hero lines from a small local LLM (needs a local server; see Docs/HERO_NARRATION.md).</summary>
        public static bool HeroVoices;

        /// <summary>Baked character voices: hero barks and the Overseer (Docs/AUDIO.md). No server needed.</summary>
        public static bool CharacterVoices = true;
        /// <summary>Speak hero lines aloud via a local TTS server (see Docs/HERO_NARRATION.md).</summary>
        public static bool HeroSpeech;

        /// <summary>Per-world building architecture (see Docs/PLANET_ARCHITECTURE.md). <c>-classic-buildings</c> turns it off.</summary>
        public static bool PlanetArchitecture = true;

        /// <summary>Missing preference is on. A stored 0 is an explicit off and stays off.</summary>
        public static bool ResolveEdgeScroll(bool hasSavedChoice, int stored) =>
            !hasSavedChoice || stored != 0;

        public static string ResolutionLabel =>
            ResolutionWidth >= 640 && ResolutionHeight >= 480
                ? $"{ResolutionWidth}×{ResolutionHeight}"
                : "DISPLAY";

        public static int ResolutionIndex()
        {
            for (int i = 0; i < ResolutionWidths.Length; i++)
            {
                if (ResolutionWidths[i] == ResolutionWidth && ResolutionHeights[i] == ResolutionHeight)
                    return i;
            }
            return -1;
        }

        /// <summary>Step to the next preset. The first click leaves "display" for 1280×720, and the last wraps.</summary>
        public static void StepResolution()
        {
            int i = ResolutionIndex();
            int next = i < 0 ? 0 : (i + 1) % ResolutionWidths.Length;
            ResolutionWidth = ResolutionWidths[next];
            ResolutionHeight = ResolutionHeights[next];
        }

        /// <summary>
        /// Native display size, <see cref="Screen.currentResolution"/>, and every reported
        /// <see cref="Screen.resolutions"/> entry, deduped by width×height. No 16:9 snap.
        /// An empty report falls back to the built-in presets.
        /// </summary>
        public static List<DisplayMode> CollectResolutions(
            int currentW, int currentH, int systemW, int systemH, Resolution[] reported)
        {
            var list = new List<DisplayMode>(16);
            AddMode(list, currentW, currentH);
            AddMode(list, systemW, systemH);
            if (reported != null)
            {
                for (int i = 0; i < reported.Length; i++)
                    AddMode(list, reported[i].width, reported[i].height);
            }
            if (list.Count == 0)
            {
                for (int i = 0; i < ResolutionWidths.Length; i++)
                    AddMode(list, ResolutionWidths[i], ResolutionHeights[i]);
            }
            list.Sort(CompareModes);
            return list;
        }

        public static int IndexOfMode(IReadOnlyList<DisplayMode> modes, int width, int height)
        {
            if (modes == null) return -1;
            for (int i = 0; i < modes.Count; i++)
            {
                if (modes[i].Width == width && modes[i].Height == height)
                    return i;
            }
            return -1;
        }

        /// <summary>Leaving "display" lands on the monitor's own size when that size is in the list.</summary>
        public static int FirstExplicitIndex(IReadOnlyList<DisplayMode> modes, int nativeW, int nativeH)
        {
            int native = IndexOfMode(modes, nativeW, nativeH);
            return native >= 0 ? native : 0;
        }

        public static void RefreshDisplayModes()
        {
            int currentW = Screen.currentResolution.width;
            int currentH = Screen.currentResolution.height;
            int systemW = 0;
            int systemH = 0;
            var display = Display.main;
            if (display != null)
            {
                systemW = display.systemWidth;
                systemH = display.systemHeight;
            }
            _displayModes = CollectResolutions(currentW, currentH, systemW, systemH, Screen.resolutions);
        }

        public static void CycleResolution()
        {
            if (_displayModes == null || _displayModes.Count == 0)
                RefreshDisplayModes();
            int i = IndexOfMode(_displayModes, ResolutionWidth, ResolutionHeight);
            int next;
            if (i < 0)
            {
                int nativeW = Screen.currentResolution.width;
                int nativeH = Screen.currentResolution.height;
                var display = Display.main;
                if (display != null && display.systemWidth >= 640 && display.systemHeight >= 480)
                {
                    nativeW = display.systemWidth;
                    nativeH = display.systemHeight;
                }
                next = FirstExplicitIndex(_displayModes, nativeW, nativeH);
            }
            else
            {
                next = (i + 1) % _displayModes.Count;
            }
            ResolutionWidth = _displayModes[next].Width;
            ResolutionHeight = _displayModes[next].Height;
            ApplyDisplay();
        }

        /// <summary>True for a mode the settings list is allowed to offer. Tiny modes such as 640×480 are not.</summary>
        public static bool IsListedResolution(int width, int height) =>
            width >= OverseerRules.MinResolutionWidth && height >= OverseerRules.MinResolutionHeight;

        static void AddMode(List<DisplayMode> list, int width, int height)
        {
            if (!IsListedResolution(width, height)) return;
            if (IndexOfMode(list, width, height) >= 0) return;
            list.Add(new DisplayMode(width, height));
        }

        static int CompareModes(DisplayMode a, DisplayMode b)
        {
            int c = a.Width.CompareTo(b.Width);
            return c != 0 ? c : a.Height.CompareTo(b.Height);
        }

        /// <summary>Read the flags play-mode tests rewrite, without applying display or consuming boot.</summary>
        public static void ReloadFlagsFromPrefs()
        {
            TutorialDone = GamePrefs.GetInt(TutorialKey, 0) == 1;
            SaveExists = GamePrefs.GetInt(SaveFlagKey, 0) == 1;
            BootStraightIntoPlay = GamePrefs.GetInt(BootPlayKey, 0) == 1;
            FirstHourDemo = GamePrefs.GetInt(FirstHourKey, 1) == 1;
            EdgeScroll = ResolveEdgeScroll(GamePrefs.HasKey(EdgeScrollKey), GamePrefs.GetInt(EdgeScrollKey, 1));
        }

        public static void Load()
        {
            Master = GamePrefs.GetFloat(MasterKey, 1f);
            Sfx = GamePrefs.GetFloat(SfxKey, 1f);
            Ambient = GamePrefs.GetFloat(AmbientKey, 1f);
            // Music used to ride the ambience slider; inherit it so an old mute stays muted.
            Music = GamePrefs.GetFloat(MusicKey, Ambient);
            Voice = GamePrefs.GetFloat(VoiceKey, 1f);
            // A missing auto key stays on auto. SaveSettings always writes HudKey, so an old
            // 0.85–1.25 value must not count as an explicit choice.
            HudScaleAuto = GamePrefs.GetInt(HudAutoKey, 1) == 1;
            HudScaleExplicit = HudScaleMath.RoundToStep(GamePrefs.GetFloat(HudKey, 1f));
            RefreshHudScale(Screen.width, Screen.height);
            InvertPan = GamePrefs.GetInt(InvertKey, 0) == 1;
            TutorialDone = GamePrefs.GetInt(TutorialKey, 0) == 1;
            SaveExists = GamePrefs.GetInt(SaveFlagKey, 0) == 1;
            QualityIndex = GamePrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel());
            Fullscreen = GamePrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            ResolutionWidth = GamePrefs.GetInt(ResolutionWKey, 0);
            ResolutionHeight = GamePrefs.GetInt(ResolutionHKey, 0);
            EdgeScroll = ResolveEdgeScroll(GamePrefs.HasKey(EdgeScrollKey), GamePrefs.GetInt(EdgeScrollKey, 1));
            ReduceMotion = GamePrefs.GetInt(ReduceMotionKey, 0) == 1;
            ColorBlindMode = GamePrefs.GetInt(ColorBlindKey, 0);
            FrameCap = GamePrefs.GetInt(FrameCapKey, 0);
            MarsJexLessons = GamePrefs.GetInt(MarsJexLessonsKey, 0) == 1;
            DayCycle = GamePrefs.GetInt(DayCycleKey, 1) == 1;
            DioramaCamera = GamePrefs.GetInt(DioramaCameraKey, 0) == 1 || HasArg("-diorama");
            TiltShift = GamePrefs.GetInt(TiltShiftKey, 1) == 1;
            CloudShadows = GamePrefs.GetInt(CloudShadowsKey, 1) == 1;
            HeroVoices = GamePrefs.GetInt(HeroVoicesKey, 0) == 1;
            CharacterVoices = GamePrefs.GetInt(CharacterVoicesKey, 1) == 1;
            HeroSpeech = GamePrefs.GetInt(HeroSpeechKey, 0) == 1;
            PlanetArchitecture = GamePrefs.GetInt(PlanetArchitectureKey, 1) == 1 && !HasArg("-classic-buildings");
            BootStraightIntoPlay = GamePrefs.GetInt(BootPlayKey, 0) == 1;
            FirstHourDemo = GamePrefs.GetInt(FirstHourKey, 1) == 1;
            ReplayRules.Load();
            if (FirstHourDemo)
                ClampReplayForDemo();
            if (BootStraightIntoPlay)
            {
                GamePrefs.DeleteKey(BootPlayKey);
                GamePrefs.Save();
                BootStraightIntoPlay = true;
            }
            ApplyDisplay();
        }

        private static bool HasArg(string flag)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], flag, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Writes <see cref="HudScale"/> from auto-by-height or the explicit slider, then fit-clamps.</summary>
        public static void RefreshHudScale(int screenWidth, int screenHeight)
        {
            HudScale = HudScaleMath.Effective(HudScaleAuto, HudScaleExplicit, screenWidth, screenHeight);
        }

        /// <summary>Player dragged the slider. Auto stays off until they press AUTO.</summary>
        public static void SetHudScaleExplicit(float value)
        {
            HudScaleAuto = false;
            HudScaleExplicit = HudScaleMath.RoundToStep(value);
            RefreshHudScale(Screen.width, Screen.height);
        }

        public static void SetHudScaleAuto()
        {
            HudScaleAuto = true;
            RefreshHudScale(Screen.width, Screen.height);
        }

        /// <summary>The AUTO chip. On turns it off at the current auto scale; off turns it on.</summary>
        public static void ToggleHudScaleAuto(int screenWidth, int screenHeight)
        {
            if (HudScaleAuto)
                SetHudScaleExplicit(HudScaleMath.AutoForHeight(screenHeight));
            else
                SetHudScaleAuto();
            RefreshHudScale(screenWidth, screenHeight);
        }

        public static void ApplyDisplay()
        {
            var names = QualitySettings.names;
            if (names != null && names.Length > 0)
            {
                QualityIndex = Mathf.Clamp(QualityIndex, 0, names.Length - 1);
                if (QualitySettings.GetQualityLevel() != QualityIndex)
                    QualitySettings.SetQualityLevel(QualityIndex, true);
            }
            // Exact width×height. The HUD is IMGUI in screen pixels (scaled by HudScale),
            // not a 16:9 canvas, so an ultrawide mode is not pillarboxed by the UI.
            bool sized = ResolutionWidth >= 640 && ResolutionHeight >= 480;
            if (sized)
                Screen.SetResolution(ResolutionWidth, ResolutionHeight, Fullscreen);
            else if (Screen.fullScreen != Fullscreen)
                Screen.fullScreen = Fullscreen;

            // 0 means "let the platform decide"; anything else is an explicit cap.
            Application.targetFrameRate = FrameCap > 0 ? FrameCap : -1;
        }

        public static void SaveSettings()
        {
            GamePrefs.SetFloat(MasterKey, Master);
            GamePrefs.SetFloat(SfxKey, Sfx);
            GamePrefs.SetFloat(AmbientKey, Ambient);
            GamePrefs.SetFloat(MusicKey, Music);
            GamePrefs.SetFloat(VoiceKey, Voice);
            GamePrefs.SetFloat(HudKey, HudScaleExplicit);
            GamePrefs.SetInt(HudAutoKey, HudScaleAuto ? 1 : 0);
            GamePrefs.SetInt(InvertKey, InvertPan ? 1 : 0);
            GamePrefs.SetInt(QualityKey, QualityIndex);
            GamePrefs.SetInt(FullscreenKey, Fullscreen ? 1 : 0);
            GamePrefs.SetInt(ResolutionWKey, ResolutionWidth);
            GamePrefs.SetInt(ResolutionHKey, ResolutionHeight);
            GamePrefs.SetInt(EdgeScrollKey, EdgeScroll ? 1 : 0);
            GamePrefs.SetInt(ReduceMotionKey, ReduceMotion ? 1 : 0);
            GamePrefs.SetInt(ColorBlindKey, ColorBlindMode);
            GamePrefs.SetInt(FrameCapKey, FrameCap);
            GamePrefs.SetInt(DayCycleKey, DayCycle ? 1 : 0);
            GamePrefs.SetInt(DioramaCameraKey, DioramaCamera ? 1 : 0);
            GamePrefs.SetInt(TiltShiftKey, TiltShift ? 1 : 0);
            GamePrefs.SetInt(CloudShadowsKey, CloudShadows ? 1 : 0);
            GamePrefs.SetInt(HeroVoicesKey, HeroVoices ? 1 : 0);
            GamePrefs.SetInt(CharacterVoicesKey, CharacterVoices ? 1 : 0);
            GamePrefs.SetInt(HeroSpeechKey, HeroSpeech ? 1 : 0);
            GamePrefs.SetInt(FirstHourKey, FirstHourDemo ? 1 : 0);
            // The demo forces campaign / no challenge / balanced in memory.
            // Do not write that over a saved full-campaign stance.
            if (!FirstHourDemo)
                ReplayRules.Save();
            GamePrefs.SetInt(MarsJexLessonsKey, MarsJexLessons ? 1 : 0);
            GamePrefs.Save();
        }

        /// <summary>Open or close the Earth demo. Does not wipe the continue slot.</summary>
        public static void SetFirstHourDemo(bool on)
        {
            FirstHourDemo = on;
            GamePrefs.SetInt(FirstHourKey, on ? 1 : 0);
            GamePrefs.Save();
            if (on)
                ClampReplayForDemo();
            else
                ReplayRules.Load();
        }

        /// <summary>
        /// Open Hands would let the Engineer take a $70 Build and skip the lesson.
        /// Clamped in memory only.
        /// </summary>
        public static void ClampReplayForDemo()
        {
            ReplayRules.Mode = ColonyRunMode.Campaign;
            ReplayRules.Challenge = ChallengeId.None;
            ReplayRules.Stance = DoctrineStance.Balanced;
        }

        public static void MarkTutorialDone()
        {
            TutorialDone = true;
            GamePrefs.SetInt(TutorialKey, 1);
            GamePrefs.Save();
        }

        public static void ResetTutorial()
        {
            TutorialDone = false;
            GamePrefs.DeleteKey(TutorialKey);
            GamePrefs.Save();
        }

        public static void WriteStockpile(ResourceManager resources)
        {
            if (resources == null) return;
            GamePrefs.SetInt(SaveFlagKey, 1);
            GamePrefs.SetInt(SaveRegKey, resources.Get(ResourceId.Regolith));
            GamePrefs.SetInt(SaveIceKey, resources.Get(ResourceId.WaterIce));
            GamePrefs.SetInt(SaveMetKey, resources.Get(ResourceId.Metals));
            GamePrefs.SetInt(SavePwrKey, resources.Get(ResourceId.Power));
            SaveExists = true;
            GamePrefs.Save();
        }

        public static bool TryLoadStockpile(ResourceManager resources)
        {
            if (resources == null || GamePrefs.GetInt(SaveFlagKey, 0) == 0)
                return false;
            resources.Set(ResourceId.Regolith, GamePrefs.GetInt(SaveRegKey, 0));
            resources.Set(ResourceId.WaterIce, GamePrefs.GetInt(SaveIceKey, 0));
            resources.Set(ResourceId.Metals, GamePrefs.GetInt(SaveMetKey, 0));
            resources.Set(ResourceId.Power, GamePrefs.GetInt(SavePwrKey, 0));
            SaveExists = true;
            return true;
        }

        public static void ClearSave()
        {
            SaveExists = false;
            GamePrefs.DeleteKey(SaveFlagKey);
            GamePrefs.DeleteKey(SaveRegKey);
            GamePrefs.DeleteKey(SaveIceKey);
            GamePrefs.DeleteKey(SaveMetKey);
            GamePrefs.DeleteKey(SavePwrKey);
            ClearCampus();
            ClearRoster();
            GamePrefs.Save();
        }

        public static string CampusKey(CelestialBodyId body) => CampusKeyPrefix + (int)body;

        public static void WriteCampus(CelestialBodyId body, string blob)
        {
            if (string.IsNullOrEmpty(blob))
                GamePrefs.DeleteKey(CampusKey(body));
            else
                GamePrefs.SetString(CampusKey(body), blob);
            GamePrefs.Save();
        }

        public static string LoadCampus(CelestialBodyId body) =>
            GamePrefs.GetString(CampusKey(body), "");

        public static void ClearCampus()
        {
            var bodies = CelestialBodyCatalog.All;
            for (int i = 0; i < bodies.Length; i++)
                GamePrefs.DeleteKey(CampusKey(bodies[i]));
        }

        public static string RosterKey(CelestialBodyId body) => RosterKeyPrefix + (int)body;

        public static void WriteRoster(CelestialBodyId body, string blob)
        {
            if (string.IsNullOrEmpty(blob))
                GamePrefs.DeleteKey(RosterKey(body));
            else
                GamePrefs.SetString(RosterKey(body), blob);
            GamePrefs.Save();
        }

        public static string LoadRoster(CelestialBodyId body) =>
            GamePrefs.GetString(RosterKey(body), "");

        public static void ClearRoster()
        {
            var bodies = CelestialBodyCatalog.All;
            for (int i = 0; i < bodies.Length; i++)
                GamePrefs.DeleteKey(RosterKey(bodies[i]));
        }

        public static void RequestBootIntoPlay()
        {
            GamePrefs.SetInt(BootPlayKey, 1);
            GamePrefs.Save();
        }

        public static string ContinueButtonLabel()
        {
            if (!SaveExists) return "CONTINUE  ·  no save";
            var body = CelestialBodyCatalog.Get(BodySeed.LoadSavedBody());
            string name = body != null ? body.DisplayName : "last drop";
            int met = GamePrefs.GetInt(SaveMetKey, 0);
            return $"CONTINUE  ·  {name}  ·  EU {met}";
        }

        public static string ContinueDetail()
        {
            if (!string.IsNullOrEmpty(SaveLoadNotice)) return SaveLoadNotice;
            if (!SaveExists)
                return "No continue slot yet. New Game drops Luna (Mars if you already skipped or cleared the hour). Continue restores that body's campus.";
            int met = GamePrefs.GetInt(SaveMetKey, 0);
            int tech = ResearchManager.SavedUnlockCount();
            int modules = CampusSnapshot.SlotCount(LoadCampus(BodySeed.LoadSavedBody()));
            string campus = modules > 0 ? $"{modules} modules" : "empty campus";
            return $"EU {met}  ·  {campus}  ·  {tech} techs";
        }
    }
}
