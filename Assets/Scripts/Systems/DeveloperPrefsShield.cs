using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Snapshot of the PlayerPrefs keys play-mode campaign tests rewrite, restored on teardown
    /// so a local run does not leave the developer's settings or campaign progress wiped.
    /// </summary>
    public static class DeveloperPrefsShield
    {
        private struct IntSlot
        {
            public string Key;
            public bool Had;
            public int Value;
        }

        private struct FloatSlot
        {
            public string Key;
            public bool Had;
            public float Value;
        }

        private struct StringSlot
        {
            public string Key;
            public bool Had;
            public string Value;
        }

        private static List<IntSlot> _ints;
        private static List<FloatSlot> _floats;
        private static List<StringSlot> _strings;
        private static bool _held;

        public static void Capture()
        {
            _ints = new List<IntSlot>();
            _floats = new List<FloatSlot>();
            _strings = new List<StringSlot>();
            CaptureInts();
            CaptureFloats();
            CaptureStrings();
            _held = true;
        }

        public static void Restore()
        {
            if (!_held) return;
            for (int i = 0; i < _ints.Count; i++)
                WriteInt(_ints[i]);
            for (int i = 0; i < _floats.Count; i++)
                WriteFloat(_floats[i]);
            for (int i = 0; i < _strings.Count; i++)
                WriteString(_strings[i]);
            GamePrefs.Save();
            DemoSettings.ReloadFlagsFromPrefs();
            CampaignProgress.ReloadFromPrefs();
            BodySeed.ReloadFromPrefs();
            SimSpeed.Load();
            ReplayRules.Load();
            ReplayRules.LatchRun();
            _held = false;
        }

        private static void CaptureInts()
        {
            AddInt(DemoSettings.InvertKey);
            AddInt(DemoSettings.TutorialKey);
            AddInt(DemoSettings.SaveFlagKey);
            AddInt(DemoSettings.SaveRegKey);
            AddInt(DemoSettings.SaveIceKey);
            AddInt(DemoSettings.SaveMetKey);
            AddInt(DemoSettings.SavePwrKey);
            AddInt(DemoSettings.BootPlayKey);
            AddInt(DemoSettings.FirstHourKey);
            AddInt(DemoSettings.QualityKey);
            AddInt(DemoSettings.FullscreenKey);
            AddInt(DemoSettings.ResolutionWKey);
            AddInt(DemoSettings.ResolutionHKey);
            AddInt(DemoSettings.EdgeScrollKey);
            AddInt(DemoSettings.ReduceMotionKey);
            AddInt(DemoSettings.ColorBlindKey);
            AddInt(DemoSettings.FrameCapKey);
            AddInt(DemoSettings.MarsJexLessonsKey);
            AddInt(DemoSettings.DayCycleKey);
            AddInt(DemoSettings.DioramaCameraKey);
            AddInt(DemoSettings.TiltShiftKey);
            AddInt(DemoSettings.CloudShadowsKey);
            AddInt(DemoSettings.HeroVoicesKey);
            AddInt(DemoSettings.CharacterVoicesKey);
            AddInt(DemoSettings.HeroSpeechKey);
            AddInt(DemoSettings.PlanetArchitectureKey);
            AddInt(IntroLaunch.SeenKey);
            AddInt(IntroLaunch.PlayOnLaunchKey);
            AddInt(ReplayRules.ModeKey);
            AddInt(ReplayRules.ChallengeKey);
            AddInt(ReplayRules.StanceKey);
            AddInt(ReplayRules.IronmanKey);
            AddInt(CampaignSlots.FreshKey);
            AddInt(CampaignSlots.ActiveSlotKey);
            AddInt(CampaignSlots.PendingLoadKey);
            AddInt("SM_CampaignMaxBody");
            AddInt(CampaignProgress.ConqueredKey);
            AddInt("SM_CampaignInitialized");
            AddInt("SM_LunaHourCleared");
            AddInt("SM_CelestialBody");
            AddInt("SM_GameSpeed_v2");
            string[] bodies = { "Earth", "Luna", "Mars", "Belt", "Europa" };
            for (int i = 0; i < bodies.Length; i++)
                AddInt("SM_BodySeed_" + bodies[i]);
        }

        private static void CaptureFloats()
        {
            AddFloat(DemoSettings.MasterKey);
            AddFloat(DemoSettings.SfxKey);
            AddFloat(DemoSettings.AmbientKey);
            AddFloat(DemoSettings.MusicKey);
            AddFloat(DemoSettings.VoiceKey);
            AddFloat(DemoSettings.HudKey);
        }

        private static void CaptureStrings()
        {
            AddString("SM_CampaignResearch");
            AddString("SM_ResearchUnlocks");
            AddString("SM_PendingTravelLog");
            AddString("SM_NarrativeCutsShown");
            AddString(CampaignSlots.StampKey);
            AddString("SM_Bindings");
            AddString("SM_Achievements");
            AddString("SM_Language");
            var bodies = CelestialBodyCatalog.All;
            for (int i = 0; i < bodies.Length; i++)
            {
                AddString(DemoSettings.CampusKey(bodies[i]));
                AddString(DemoSettings.RosterKey(bodies[i]));
            }
        }

        private static void AddInt(string key) =>
            _ints.Add(new IntSlot { Key = key, Had = GamePrefs.HasKey(key), Value = GamePrefs.GetInt(key, 0) });

        private static void AddFloat(string key) =>
            _floats.Add(new FloatSlot { Key = key, Had = GamePrefs.HasKey(key), Value = GamePrefs.GetFloat(key, 0f) });

        private static void AddString(string key) =>
            _strings.Add(new StringSlot { Key = key, Had = GamePrefs.HasKey(key), Value = GamePrefs.GetString(key, "") });

        private static void WriteInt(IntSlot slot)
        {
            if (slot.Had) GamePrefs.SetInt(slot.Key, slot.Value);
            else GamePrefs.DeleteKey(slot.Key);
        }

        private static void WriteFloat(FloatSlot slot)
        {
            if (slot.Had) GamePrefs.SetFloat(slot.Key, slot.Value);
            else GamePrefs.DeleteKey(slot.Key);
        }

        private static void WriteString(StringSlot slot)
        {
            if (slot.Had) GamePrefs.SetString(slot.Key, slot.Value ?? "");
            else GamePrefs.DeleteKey(slot.Key);
        }
    }
}
