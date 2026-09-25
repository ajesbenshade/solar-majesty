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
        public const string CampusKeyPrefix = "SM_Campus_";
        public const string EdgeScrollKey = "SM_Set_EdgeScroll";
        public const string ReduceMotionKey = "SM_Set_ReduceMotion";
        public const string ColorBlindKey = "SM_Set_ColorBlind";
        public const string FrameCapKey = "SM_Set_FrameCap";
        public const string MarsGrokLessonsKey = "SM_Set_MarsGrokLessons";
        public const string DayCycleKey = "SM_Set_DayCycle";
        public const string DioramaCameraKey = "SM_Set_DioramaCamera";
        public const string TiltShiftKey = "SM_Set_TiltShift";
        public const string CloudShadowsKey = "SM_Set_CloudShadows";
        public const string HeroVoicesKey = "SM_Set_HeroVoices";
        public const string HeroSpeechKey = "SM_Set_HeroSpeech";
        public const string PlanetArchitectureKey = "SM_Set_PlanetArchitecture";
        public const string RosterKeyPrefix = "SM_Roster_";

        public static float Master = 1f;
        public static float Sfx = 1f;
        public static float Ambient = 1f;
        public static float HudScale = 1f;
        public static bool InvertPan;
        public static bool TutorialDone;
        public static bool SaveExists;
        public static string SaveLoadNotice = "";
        public static int QualityIndex;
        public static bool Fullscreen = true;

        /// <summary>Off by default: edge scroll fights flag placement near the screen border.</summary>
        public static bool EdgeScroll;

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
        public static bool MarsGrokLessons;

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

        /// <summary>Speak hero lines aloud via a local TTS server (see Docs/HERO_NARRATION.md).</summary>
        public static bool HeroSpeech;

        /// <summary>Per-world building architecture (see Docs/PLANET_ARCHITECTURE.md). <c>-classic-buildings</c> turns it off.</summary>
        public static bool PlanetArchitecture = true;

        public static void Load()
        {
            Master = PlayerPrefs.GetFloat(MasterKey, 1f);
            Sfx = PlayerPrefs.GetFloat(SfxKey, 1f);
            Ambient = PlayerPrefs.GetFloat(AmbientKey, 1f);
            HudScale = Mathf.Clamp(PlayerPrefs.GetFloat(HudKey, 1f), 0.85f, 1.25f);
            InvertPan = PlayerPrefs.GetInt(InvertKey, 0) == 1;
            TutorialDone = PlayerPrefs.GetInt(TutorialKey, 0) == 1;
            SaveExists = PlayerPrefs.GetInt(SaveFlagKey, 0) == 1;
            QualityIndex = PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel());
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            EdgeScroll = PlayerPrefs.GetInt(EdgeScrollKey, 0) == 1;
            ReduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;
            ColorBlindMode = PlayerPrefs.GetInt(ColorBlindKey, 0);
            FrameCap = PlayerPrefs.GetInt(FrameCapKey, 0);
            MarsGrokLessons = PlayerPrefs.GetInt(MarsGrokLessonsKey, 0) == 1;
            DayCycle = PlayerPrefs.GetInt(DayCycleKey, 1) == 1;
            DioramaCamera = PlayerPrefs.GetInt(DioramaCameraKey, 0) == 1 || HasArg("-diorama");
            TiltShift = PlayerPrefs.GetInt(TiltShiftKey, 1) == 1;
            CloudShadows = PlayerPrefs.GetInt(CloudShadowsKey, 1) == 1;
            HeroVoices = PlayerPrefs.GetInt(HeroVoicesKey, 0) == 1;
            HeroSpeech = PlayerPrefs.GetInt(HeroSpeechKey, 0) == 1;
            PlanetArchitecture = PlayerPrefs.GetInt(PlanetArchitectureKey, 1) == 1 && !HasArg("-classic-buildings");
            BootStraightIntoPlay = PlayerPrefs.GetInt(BootPlayKey, 0) == 1;
            FirstHourDemo = PlayerPrefs.GetInt(FirstHourKey, 1) == 1;
            ReplayRules.Load();
            if (FirstHourDemo)
                ClampReplayForDemo();
            if (BootStraightIntoPlay)
            {
                PlayerPrefs.DeleteKey(BootPlayKey);
                PlayerPrefs.Save();
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

        public static void ApplyDisplay()
        {
            var names = QualitySettings.names;
            if (names != null && names.Length > 0)
            {
                QualityIndex = Mathf.Clamp(QualityIndex, 0, names.Length - 1);
                if (QualitySettings.GetQualityLevel() != QualityIndex)
                    QualitySettings.SetQualityLevel(QualityIndex, true);
            }
            if (Screen.fullScreen != Fullscreen)
                Screen.fullScreen = Fullscreen;

            // 0 means "let the platform decide"; anything else is an explicit cap.
            Application.targetFrameRate = FrameCap > 0 ? FrameCap : -1;
        }

        public static void SaveSettings()
        {
            PlayerPrefs.SetFloat(MasterKey, Master);
            PlayerPrefs.SetFloat(SfxKey, Sfx);
            PlayerPrefs.SetFloat(AmbientKey, Ambient);
            PlayerPrefs.SetFloat(HudKey, HudScale);
            PlayerPrefs.SetInt(InvertKey, InvertPan ? 1 : 0);
            PlayerPrefs.SetInt(QualityKey, QualityIndex);
            PlayerPrefs.SetInt(FullscreenKey, Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(EdgeScrollKey, EdgeScroll ? 1 : 0);
            PlayerPrefs.SetInt(ReduceMotionKey, ReduceMotion ? 1 : 0);
            PlayerPrefs.SetInt(ColorBlindKey, ColorBlindMode);
            PlayerPrefs.SetInt(FrameCapKey, FrameCap);
            PlayerPrefs.SetInt(DayCycleKey, DayCycle ? 1 : 0);
            PlayerPrefs.SetInt(DioramaCameraKey, DioramaCamera ? 1 : 0);
            PlayerPrefs.SetInt(TiltShiftKey, TiltShift ? 1 : 0);
            PlayerPrefs.SetInt(CloudShadowsKey, CloudShadows ? 1 : 0);
            PlayerPrefs.SetInt(HeroVoicesKey, HeroVoices ? 1 : 0);
            PlayerPrefs.SetInt(HeroSpeechKey, HeroSpeech ? 1 : 0);
            PlayerPrefs.SetInt(FirstHourKey, FirstHourDemo ? 1 : 0);
            // The demo forces campaign / no challenge / balanced in memory.
            // Do not write that over a saved full-campaign stance.
            if (!FirstHourDemo)
                ReplayRules.Save();
            PlayerPrefs.SetInt(MarsGrokLessonsKey, MarsGrokLessons ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Open or close the Earth demo. Does not wipe the continue slot.</summary>
        public static void SetFirstHourDemo(bool on)
        {
            FirstHourDemo = on;
            PlayerPrefs.SetInt(FirstHourKey, on ? 1 : 0);
            PlayerPrefs.Save();
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
            PlayerPrefs.SetInt(TutorialKey, 1);
            PlayerPrefs.Save();
        }

        public static void ResetTutorial()
        {
            TutorialDone = false;
            PlayerPrefs.DeleteKey(TutorialKey);
            PlayerPrefs.Save();
        }

        public static void WriteStockpile(ResourceManager resources)
        {
            if (resources == null) return;
            PlayerPrefs.SetInt(SaveFlagKey, 1);
            PlayerPrefs.SetInt(SaveRegKey, resources.Get(ResourceId.Regolith));
            PlayerPrefs.SetInt(SaveIceKey, resources.Get(ResourceId.WaterIce));
            PlayerPrefs.SetInt(SaveMetKey, resources.Get(ResourceId.Metals));
            PlayerPrefs.SetInt(SavePwrKey, resources.Get(ResourceId.Power));
            SaveExists = true;
            PlayerPrefs.Save();
        }

        public static bool TryLoadStockpile(ResourceManager resources)
        {
            if (resources == null || PlayerPrefs.GetInt(SaveFlagKey, 0) == 0)
                return false;
            resources.Set(ResourceId.Regolith, PlayerPrefs.GetInt(SaveRegKey, 0));
            resources.Set(ResourceId.WaterIce, PlayerPrefs.GetInt(SaveIceKey, 0));
            resources.Set(ResourceId.Metals, PlayerPrefs.GetInt(SaveMetKey, 0));
            resources.Set(ResourceId.Power, PlayerPrefs.GetInt(SavePwrKey, 0));
            SaveExists = true;
            return true;
        }

        public static void ClearSave()
        {
            SaveExists = false;
            PlayerPrefs.DeleteKey(SaveFlagKey);
            PlayerPrefs.DeleteKey(SaveRegKey);
            PlayerPrefs.DeleteKey(SaveIceKey);
            PlayerPrefs.DeleteKey(SaveMetKey);
            PlayerPrefs.DeleteKey(SavePwrKey);
            ClearCampus();
            ClearRoster();
            PlayerPrefs.Save();
        }

        public static string CampusKey(CelestialBodyId body) => CampusKeyPrefix + (int)body;

        public static void WriteCampus(CelestialBodyId body, string blob)
        {
            if (string.IsNullOrEmpty(blob))
                PlayerPrefs.DeleteKey(CampusKey(body));
            else
                PlayerPrefs.SetString(CampusKey(body), blob);
            PlayerPrefs.Save();
        }

        public static string LoadCampus(CelestialBodyId body) =>
            PlayerPrefs.GetString(CampusKey(body), "");

        public static void ClearCampus()
        {
            var bodies = CelestialBodyCatalog.All;
            for (int i = 0; i < bodies.Length; i++)
                PlayerPrefs.DeleteKey(CampusKey(bodies[i]));
        }

        public static string RosterKey(CelestialBodyId body) => RosterKeyPrefix + (int)body;

        public static void WriteRoster(CelestialBodyId body, string blob)
        {
            if (string.IsNullOrEmpty(blob))
                PlayerPrefs.DeleteKey(RosterKey(body));
            else
                PlayerPrefs.SetString(RosterKey(body), blob);
            PlayerPrefs.Save();
        }

        public static string LoadRoster(CelestialBodyId body) =>
            PlayerPrefs.GetString(RosterKey(body), "");

        public static void ClearRoster()
        {
            var bodies = CelestialBodyCatalog.All;
            for (int i = 0; i < bodies.Length; i++)
                PlayerPrefs.DeleteKey(RosterKey(bodies[i]));
        }

        public static void RequestBootIntoPlay()
        {
            PlayerPrefs.SetInt(BootPlayKey, 1);
            PlayerPrefs.Save();
        }

        public static string ContinueButtonLabel()
        {
            if (!SaveExists) return "CONTINUE  ·  no save";
            var body = CelestialBodyCatalog.Get(BodySeed.LoadSavedBody());
            string name = body != null ? body.DisplayName : "last drop";
            int met = PlayerPrefs.GetInt(SaveMetKey, 0);
            return $"CONTINUE  ·  {name}  ·  EU {met}";
        }

        public static string ContinueDetail()
        {
            if (!string.IsNullOrEmpty(SaveLoadNotice)) return SaveLoadNotice;
            if (!SaveExists)
                return "No continue slot yet. New Game drops Luna (Mars if you already skipped or cleared the hour). Continue restores that body's campus.";
            int reg = PlayerPrefs.GetInt(SaveRegKey, 0);
            int ice = PlayerPrefs.GetInt(SaveIceKey, 0);
            int met = PlayerPrefs.GetInt(SaveMetKey, 0);
            int pwr = PlayerPrefs.GetInt(SavePwrKey, 0);
            int tech = ResearchManager.SavedUnlockCount();
            int modules = CampusSnapshot.SlotCount(LoadCampus(BodySeed.LoadSavedBody()));
            string campus = modules > 0 ? $"{modules} modules" : "empty campus";
            return $"REG {reg}  ICE {ice}  EU {met}  PWR {pwr}  ·  {campus}  ·  {tech} techs";
        }
    }
}
