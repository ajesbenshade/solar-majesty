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
        public const string QualityKey = "SM_Set_Quality";
        public const string FullscreenKey = "SM_Set_Fullscreen";
        public const string CampusKeyPrefix = "SM_Campus_";

        public static float Master = 1f;
        public static float Sfx = 1f;
        public static float Ambient = 1f;
        public static float HudScale = 1f;
        public static bool InvertPan;
        public static bool TutorialDone;
        public static bool SaveExists;
        public static int QualityIndex;
        public static bool Fullscreen = true;

        /// <summary>When true, skip title after a New Game reload.</summary>
        public static bool BootStraightIntoPlay;

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
            BootStraightIntoPlay = PlayerPrefs.GetInt(BootPlayKey, 0) == 1;
            ReplayRules.Load();
            if (BootStraightIntoPlay)
            {
                PlayerPrefs.DeleteKey(BootPlayKey);
                PlayerPrefs.Save();
                BootStraightIntoPlay = true;
            }
            ApplyDisplay();
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
            ReplayRules.Save();
            PlayerPrefs.Save();
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
            return $"CONTINUE  ·  {name}  ·  MET {met}";
        }

        public static string ContinueDetail()
        {
            if (!SaveExists)
                return "No continue slot yet. New Game drops Earth; Continue restores that body's campus.";
            int reg = PlayerPrefs.GetInt(SaveRegKey, 0);
            int ice = PlayerPrefs.GetInt(SaveIceKey, 0);
            int met = PlayerPrefs.GetInt(SaveMetKey, 0);
            int pwr = PlayerPrefs.GetInt(SavePwrKey, 0);
            int tech = ResearchManager.SavedUnlockCount();
            int modules = CampusSnapshot.SlotCount(LoadCampus(BodySeed.LoadSavedBody()));
            string campus = modules > 0 ? $"{modules} modules" : "empty campus";
            return $"REG {reg}  ICE {ice}  MET {met}  PWR {pwr}  ·  {campus}  ·  {tech} techs";
        }
    }
}
