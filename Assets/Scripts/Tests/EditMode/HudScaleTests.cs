using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class HudScaleTests
    {
        [Test]
        public void AutoForHeight_Maps1080_1440_2160_And720()
        {
            Assert.AreEqual(1f, HudScaleMath.AutoForHeight(1080), 0.001f);
            Assert.AreEqual(1.35f, HudScaleMath.AutoForHeight(1440), 0.001f);
            Assert.AreEqual(2f, HudScaleMath.AutoForHeight(2160), 0.001f);
            Assert.AreEqual(0.75f, HudScaleMath.AutoForHeight(720), 0.001f);
            Assert.AreEqual(1f, HudScaleMath.AutoForHeight(0), 0.001f);
        }

        [Test]
        public void Effective_Clamps200PercentOn1080p_AndKeepsItOn4KAndUltrawide()
        {
            float fit = HudScaleMath.Effective(false, 2f, 1920, 1080);
            Assert.Less(fit, 2f);
            Assert.LessOrEqual(fit, 1080f / HudScaleMath.MinViewHeight + 0.001f);
            Assert.Greater(fit, 1.6f);

            Assert.AreEqual(2f, HudScaleMath.Effective(false, 2f, 3840, 2160), 0.001f);
            Assert.AreEqual(2f, HudScaleMath.Effective(false, 2f, 3440, 1440), 0.001f);
        }

        [Test]
        public void ExplicitChoice_PersistsApartFromAuto()
        {
            // SaveSettings writes the whole settings block, including resolution and UI-scale auto.
            string[] floats =
            {
                DemoSettings.MasterKey, DemoSettings.SfxKey, DemoSettings.AmbientKey,
                DemoSettings.MusicKey, DemoSettings.VoiceKey, DemoSettings.HudKey
            };
            string[] ints =
            {
                DemoSettings.HudAutoKey, DemoSettings.InvertKey, DemoSettings.QualityKey,
                DemoSettings.FullscreenKey, DemoSettings.ResolutionWKey, DemoSettings.ResolutionHKey,
                DemoSettings.EdgeScrollKey, DemoSettings.ReduceMotionKey, DemoSettings.ColorBlindKey,
                DemoSettings.FrameCapKey, DemoSettings.DayCycleKey, DemoSettings.DioramaCameraKey,
                DemoSettings.TiltShiftKey, DemoSettings.CloudShadowsKey, DemoSettings.HeroVoicesKey,
                DemoSettings.CharacterVoicesKey, DemoSettings.HeroSpeechKey, DemoSettings.FirstHourKey,
                DemoSettings.MarsGrokLessonsKey, DemoSettings.BootPlayKey, DemoSettings.TutorialKey,
                DemoSettings.SaveFlagKey, DemoSettings.PlanetArchitectureKey,
                ReplayRules.ModeKey, ReplayRules.ChallengeKey, ReplayRules.StanceKey, ReplayRules.IronmanKey
            };
            bool[] hadFloat = new bool[floats.Length];
            float[] prevFloat = new float[floats.Length];
            bool[] hadInt = new bool[ints.Length];
            int[] prevInt = new int[ints.Length];
            for (int i = 0; i < floats.Length; i++)
            {
                hadFloat[i] = PlayerPrefs.HasKey(floats[i]);
                prevFloat[i] = PlayerPrefs.GetFloat(floats[i], 0f);
            }
            for (int i = 0; i < ints.Length; i++)
            {
                hadInt[i] = PlayerPrefs.HasKey(ints[i]);
                prevInt[i] = PlayerPrefs.GetInt(ints[i], 0);
            }

            try
            {
                PlayerPrefs.DeleteKey(DemoSettings.HudAutoKey);
                PlayerPrefs.Save();
                DemoSettings.Load();
                Assert.IsTrue(DemoSettings.HudScaleAuto, "A missing auto key stays on auto.");

                DemoSettings.SetHudScaleExplicit(1.5f);
                DemoSettings.SaveSettings();
                DemoSettings.HudScaleAuto = true;
                DemoSettings.HudScaleExplicit = 1f;
                DemoSettings.HudScale = 1f;
                DemoSettings.Load();
                Assert.IsFalse(DemoSettings.HudScaleAuto);
                Assert.AreEqual(1.5f, DemoSettings.HudScaleExplicit, 0.001f);
            }
            finally
            {
                Restore();
                PlayerPrefs.Save();
                DemoSettings.Load();
                // Load consumes the boot-play key when it is set. Put every snapshot back after that.
                Restore();
                PlayerPrefs.Save();
            }

            for (int i = 0; i < floats.Length; i++)
            {
                if (hadFloat[i])
                    Assert.AreEqual(prevFloat[i], PlayerPrefs.GetFloat(floats[i]), 0.0001f);
                else
                    Assert.IsFalse(PlayerPrefs.HasKey(floats[i]), floats[i]);
            }
            for (int i = 0; i < ints.Length; i++)
            {
                if (hadInt[i])
                    Assert.AreEqual(prevInt[i], PlayerPrefs.GetInt(ints[i]));
                else
                    Assert.IsFalse(PlayerPrefs.HasKey(ints[i]), ints[i]);
            }

            void Restore()
            {
                for (int i = 0; i < floats.Length; i++)
                {
                    if (hadFloat[i]) PlayerPrefs.SetFloat(floats[i], prevFloat[i]);
                    else PlayerPrefs.DeleteKey(floats[i]);
                }
                for (int i = 0; i < ints.Length; i++)
                {
                    if (hadInt[i]) PlayerPrefs.SetInt(ints[i], prevInt[i]);
                    else PlayerPrefs.DeleteKey(ints[i]);
                }
            }
        }
    }
}
