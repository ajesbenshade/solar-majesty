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
            PlayerPrefs.Save();
        }

        public static void RequestBootIntoPlay()
        {
            PlayerPrefs.SetInt(BootPlayKey, 1);
            PlayerPrefs.Save();
        }
    }
}
