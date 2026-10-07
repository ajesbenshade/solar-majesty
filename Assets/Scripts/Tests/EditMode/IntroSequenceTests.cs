using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class IntroSequenceTests
    {
        private bool _hadSeen;
        private bool _hadLaunch;
        private int _seen;
        private int _launch;

        [SetUp]
        public void SetUp()
        {
            _hadSeen = PlayerPrefs.HasKey(IntroLaunch.SeenKey);
            _hadLaunch = PlayerPrefs.HasKey(IntroLaunch.PlayOnLaunchKey);
            _seen = PlayerPrefs.GetInt(IntroLaunch.SeenKey, 0);
            _launch = PlayerPrefs.GetInt(IntroLaunch.PlayOnLaunchKey, 1);
            PlayerPrefs.DeleteKey(IntroLaunch.SeenKey);
            PlayerPrefs.DeleteKey(IntroLaunch.PlayOnLaunchKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadSeen) PlayerPrefs.SetInt(IntroLaunch.SeenKey, _seen);
            else PlayerPrefs.DeleteKey(IntroLaunch.SeenKey);
            if (_hadLaunch) PlayerPrefs.SetInt(IntroLaunch.PlayOnLaunchKey, _launch);
            else PlayerPrefs.DeleteKey(IntroLaunch.PlayOnLaunchKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void FirstLaunch_PlaysOnAColdTitle()
        {
            Assert.IsFalse(IntroLaunch.HasSeen);
            Assert.IsTrue(IntroLaunch.PlayOnLaunch);
            Assert.IsTrue(IntroLaunch.ShouldAutoPlay(IntroBootContext.ColdTitle));
        }

        [Test]
        public void FirstLaunch_SeenFlagBlocksTheNextColdBoot()
        {
            IntroLaunch.MarkSeen();
            Assert.IsTrue(IntroLaunch.HasSeen);
            Assert.IsFalse(IntroLaunch.ShouldAutoPlay(IntroBootContext.ColdTitle));
        }

        [Test]
        public void FirstLaunch_OptionOffSkipsEvenWhenUnseen()
        {
            IntroLaunch.SetPlayOnLaunch(false);
            Assert.IsFalse(IntroLaunch.PlayOnLaunch);
            Assert.IsFalse(IntroLaunch.ShouldAutoPlay(IntroBootContext.ColdTitle));

            IntroLaunch.SetPlayOnLaunch(true);
            Assert.IsTrue(IntroLaunch.ShouldAutoPlay(IntroBootContext.ColdTitle));
        }

        [Test]
        public void FirstLaunch_ContinueLoadAndReturnNeverWait()
        {
            Assert.IsFalse(IntroLaunch.ShouldAutoPlay(new IntroBootContext(true, false, false, false)));
            Assert.IsFalse(IntroLaunch.ShouldAutoPlay(new IntroBootContext(false, true, false, false)));
            Assert.IsFalse(IntroLaunch.ShouldAutoPlay(new IntroBootContext(false, false, true, false)));
            Assert.IsFalse(IntroLaunch.ShouldAutoPlay(new IntroBootContext(false, false, false, true)));
        }

        [Test]
        public void Skip_RequiresAKeyClickOrGamepadButton()
        {
            Assert.IsFalse(IntroSkip.ShouldSkip(new IntroInputSample(false, false, false)));
            Assert.IsTrue(IntroSkip.ShouldSkip(new IntroInputSample(true, false, false)));
            Assert.IsTrue(IntroSkip.ShouldSkip(new IntroInputSample(false, true, false)));
            Assert.IsTrue(IntroSkip.ShouldSkip(new IntroInputSample(false, false, true)));
        }

        [Test]
        public void Skip_ArmsBeforeItCuts()
        {
            var intro = NewIntro();
            var cam = NewCamera();
            try
            {
                intro.PlayFallback(cam, ColonyLayout.CameraFocus, CelestialBodyCatalog.Earth());
                intro.NotifyInput(new IntroInputSample(true, false, false));
                Assert.IsTrue(intro.IsPlaying, "a click on the opening instant does not skip");

                intro.Advance(IntroShot.SkipArmSeconds);
                intro.NotifyInput(new IntroInputSample(false, true, false));
                Assert.IsFalse(intro.IsPlaying);
                Assert.AreEqual(0f, intro.OverlayAlpha, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(intro.gameObject);
                Object.DestroyImmediate(cam.gameObject);
            }
        }

        [Test]
        public void Playback_FallsBackWhenNoTimelineIsPresent()
        {
            Assert.AreEqual(IntroPlayback.Fallback, IntroSequence.ChoosePlayback(false));
            Assert.AreEqual(IntroPlayback.Timeline, IntroSequence.ChoosePlayback(true));
            Assert.AreEqual("Intro/IntroTimeline", IntroAssets.TimelineResource);
        }

        [Test]
        public void Fallback_SweepsWithoutATimelineAsset()
        {
            var intro = NewIntro();
            var cam = NewCamera();
            try
            {
                Vector3 focus = ColonyLayout.CameraFocus;
                intro.PlayFallback(cam, focus, CelestialBodyCatalog.Earth());
                Assert.IsTrue(intro.IsPlaying);
                Assert.AreEqual(IntroPlayback.Fallback, intro.Playback);
                Assert.IsFalse(intro.TitleRoot.activeSelf);

                Vector3 opened = cam.transform.position;
                intro.Advance(IntroShot.CameraSettle);
                Vector3 settled = cam.transform.position;
                Assert.Greater(Vector3.Distance(opened, settled), 5f);
                Assert.Less(Vector3.Distance(settled, IntroShot.ColonySettlePosition(focus)), 0.05f);
                Assert.IsTrue(intro.TitleRoot.activeSelf);
                Assert.IsNotNull(intro.TitleRoot.transform.Find(IntroAssets.PlaceholderGlyphs));

                intro.Advance(IntroShot.Duration);
                Assert.IsFalse(intro.IsPlaying);
                Assert.IsFalse(intro.TitleRoot.activeSelf);
            }
            finally
            {
                if (intro != null) Object.DestroyImmediate(intro.gameObject);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
            }
        }

        [Test]
        public void FallbackShot_PassesEarthThenSettles()
        {
            Vector3 focus = ColonyLayout.CameraFocus;
            Vector3 earth = IntroShot.EarthPosition(focus);
            var opened = IntroShot.Sample(0f, focus, false);
            var settled = IntroShot.Sample(IntroShot.CameraSettle, focus, false);

            Assert.Less(Vector3.Distance(opened.Position, earth), Vector3.Distance(settled.Position, earth));
            Vector3 toEarth = (earth - opened.Position).normalized;
            Assert.Greater(Vector3.Dot(opened.Rotation * Vector3.forward, toEarth), 0.9f);
            Assert.Less(Vector3.Distance(settled.Position, IntroShot.ColonySettlePosition(focus)), 0.001f);
        }

        [Test]
        public void Dusk_IsLowAndGolden()
        {
            var body = CelestialBodyCatalog.Get(CelestialBodyId.Earth);
            var dusk = SunPath.Evaluate(body, SunPath.DuskElapsed(body));
            Assert.Greater(dusk.Golden, 0.75f);
            Assert.AreEqual(0f, dusk.Night, 0.001f);
            Assert.Less(dusk.Euler.x, body.SunEuler.x);
        }

        [Test]
        public void TitleSlot_FacesNegativeZFromTheSettledCamera()
        {
            Vector3 focus = ColonyLayout.CameraFocus;
            var slot = IntroShot.TitlePose(focus);
            var cam = IntroShot.Sample(IntroShot.CameraSettle, focus, false);

            Assert.Greater(Vector3.Dot(slot.Rotation * Vector3.forward, cam.Rotation * Vector3.forward), 0.999f);
            Vector3 toCamera = (cam.Position - slot.Position).normalized;
            Assert.Greater(Vector3.Dot(toCamera, slot.Rotation * Vector3.back), 0.999f);
        }

        [Test]
        public void PlaceholderTitle_MatchesTheModelFacingAndCentrePivot()
        {
            var host = new GameObject("intro-title-host");
            host.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var title = IntroSequence.CreatePlaceholderTitle(host.transform);
                var pose = IntroShot.TitlePose(ColonyLayout.CameraFocus);
                title.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
                title.transform.localScale = Vector3.one;

                var glyphs = title.transform.Find(IntroAssets.PlaceholderGlyphs);
                Assert.IsNotNull(glyphs);
                Assert.AreEqual(Vector3.zero, glyphs.localPosition);
                var mesh = glyphs.GetComponent<TextMesh>();
                Assert.IsNotNull(mesh);
                Assert.AreEqual(TextAnchor.MiddleCenter, mesh.anchor);

                Vector3 readable = glyphs.rotation * Vector3.forward;
                Vector3 slotBack = title.transform.rotation * Vector3.back;
                Assert.Greater(Vector3.Dot(readable.normalized, slotBack.normalized), 0.999f);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TitlePrefab_IsOptInUntilTheFileExists()
        {
            Assert.AreEqual("Assets/Art/Intro/SM_Title_SolarMajesty.prefab", IntroAssets.TitlePrefabPath);
            Assert.IsFalse(IntroAssets.UseAuthoredTitle(false));
            Assert.IsTrue(IntroAssets.UseAuthoredTitle(true));
        }

        private static IntroSequence NewIntro()
        {
            var go = new GameObject("intro-test");
            go.hideFlags = HideFlags.HideAndDontSave;
            return go.AddComponent<IntroSequence>();
        }

        private static Camera NewCamera()
        {
            var go = new GameObject("intro-cam");
            go.hideFlags = HideFlags.HideAndDontSave;
            return go.AddComponent<Camera>();
        }
    }
}
