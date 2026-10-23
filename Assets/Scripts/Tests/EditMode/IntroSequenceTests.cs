using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

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
            // Windows stores these as SM_IntroSeen_h<hash> / SM_IntroOnLaunch_h<hash>.
            // DeleteKey removes that registry value. Always run, including after a failed test.
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

                Step(intro, IntroShot.SkipArmSeconds);
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
                Step(intro, IntroShot.CameraSettle);
                Vector3 settled = cam.transform.position;
                Assert.Greater(Vector3.Distance(opened, settled), 5f);
                Assert.Less(Vector3.Distance(settled, IntroShot.FallbackEndPosition(focus)), 0.05f);
                Assert.IsTrue(intro.TitleRoot.activeSelf);
                Assert.IsNotNull(intro.TitleRoot.transform.Find(IntroAssets.PlaceholderGlyphs));

                Step(intro, IntroShot.Duration);
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
            Vector3 end = IntroShot.FallbackEndPosition(focus);
            Assert.Less(Vector3.Distance(settled.Position, end), 0.001f);
            Assert.AreEqual(173.05f, end.x, 0.001f);
            Assert.AreEqual(19.78f, end.y, 0.001f);
            Assert.AreEqual(171.05f, end.z, 0.001f);
            Assert.Less(Quaternion.Angle(settled.Rotation, Quaternion.Euler(IntroShot.FallbackEndPitch, 45f, 0f)), 0.05f);
            Assert.AreEqual(IntroShot.FallbackEndFov, settled.FieldOfView, 0.001f);

            var held = IntroShot.Sample(IntroShot.Duration, focus, false);
            Assert.Less(Vector3.Distance(held.Position, settled.Position), 0.001f, "holds from the settle to the fade");
            Assert.Less(Quaternion.Angle(held.Rotation, settled.Rotation), 0.05f);

            var reduced = IntroShot.Sample(0f, focus, true);
            Assert.Less(Vector3.Distance(reduced.Position, end), 0.001f, "reduce motion starts on the end pose");

            var slot = IntroShot.TitlePose(focus);
            var camGo = new GameObject("fallback-frustum");
            camGo.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(settled.Position, settled.Rotation);
                cam.fieldOfView = settled.FieldOfView;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 4000f;
                cam.aspect = 16f / 9f;
                Vector3 right = slot.Rotation * Vector3.right * (7.0f * 0.5f);
                Vector3 up = slot.Rotation * Vector3.up * (2.34f * 0.5f);
                var corners = new[]
                {
                    slot.Position - right - up, slot.Position + right - up,
                    slot.Position - right + up, slot.Position + right + up
                };
                foreach (var c in corners)
                {
                    Vector3 v = cam.WorldToViewportPoint(c);
                    Assert.Greater(v.z, 0f, "title in front of the fallback camera");
                    Assert.That(v.x, Is.InRange(0.02f, 0.98f), "title inside the fallback frame");
                    Assert.That(v.y, Is.InRange(0.02f, 0.98f), "title inside the fallback frame");
                }
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
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
            Vector3 anchorPos = IntroShot.ColonySettlePosition(focus);
            Quaternion anchorRot = Quaternion.Euler(30f, 45f, 0f);

            Assert.Greater(Vector3.Dot(slot.Rotation * Vector3.forward, anchorRot * Vector3.forward), 0.999f);
            Vector3 onAxis = anchorPos + anchorRot * Vector3.forward * IntroShot.TitleDistance;
            Vector3 lifted = onAxis + Vector3.up * IntroShot.TitleLift;
            Assert.Less(Vector3.Distance(slot.Position, lifted), 0.001f);
            Assert.AreEqual(IntroShot.TitleLift, slot.Position.y - onAxis.y, 0.0001f);
            // Colony focus (192, 0, 190), settle Euler (30, 45, 0), 11 m forward, +5 m world Y.
            Assert.AreEqual(180.7361f, slot.Position.x, 0.001f);
            Assert.AreEqual(21.5f, slot.Position.y, 0.001f);
            Assert.AreEqual(178.7361f, slot.Position.z, 0.001f);
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

        [Test]
        public void TimelineCamera_MatchesTheFallbackAndStaysAboveTheWater()
        {
            var timeline = IntroSequence.LoadTimeline();
            Assert.IsNotNull(timeline, "Resources/Intro/IntroTimeline is missing.");

            AnimationTrack cameraTrack = null;
            foreach (var output in timeline.outputs)
            {
                if (output.sourceObject is AnimationTrack track && track.name == IntroAssets.CameraTrack)
                    cameraTrack = track;
            }
            Assert.IsNotNull(cameraTrack);
            Assert.AreEqual(TrackOffset.ApplyTransformOffsets, cameraTrack.trackOffset);
            Assert.AreEqual(Vector3.zero, cameraTrack.position);
            foreach (var clip in cameraTrack.GetClips())
            {
                var asset = clip.asset as AnimationPlayableAsset;
                Assert.IsNotNull(asset);
                Assert.IsFalse(asset.removeStartOffset, "absolute poses, not a delta from the origin");
                Assert.AreEqual(Vector3.zero, asset.position);
                Assert.AreEqual(Vector3.zero, asset.eulerAngles);
            }

            var camGo = new GameObject("intro-timeline-cam");
            camGo.hideFlags = HideFlags.HideAndDontSave;
            var cam = camGo.AddComponent<Camera>();
            var animator = camGo.AddComponent<Animator>();
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var host = new GameObject("intro-timeline-director");
            host.hideFlags = HideFlags.HideAndDontSave;
            var director = host.AddComponent<PlayableDirector>();
            bool startedSampling = false;
            try
            {
                director.playableAsset = timeline;
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.Manual;
                director.extrapolationMode = DirectorWrapMode.Hold;
                director.SetGenericBinding(cameraTrack, animator);
                if (!AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    startedSampling = true;
                }
                director.RebuildGraph();
                director.Play();

                float[] times = { 0f, IntroShot.Duration * 0.5f, IntroShot.Duration };
                Vector3 focus = ColonyLayout.CameraFocus;
                for (int i = 0; i < times.Length; i++)
                {
                    director.time = times[i];
                    director.Evaluate();
                    if (director.playableGraph.IsValid())
                        director.playableGraph.Evaluate(0);
                    var expected = IntroShot.Sample(times[i], focus, false);
                    Assert.Greater(cam.transform.position.y, IntroShot.MinCameraHeight, "above ground and water at t=" + times[i]);
                    Assert.Less(Vector3.Distance(cam.transform.position, expected.Position), 0.75f, "position t=" + times[i]);
                    Assert.Greater(Mathf.Abs(Quaternion.Dot(cam.transform.rotation, expected.Rotation)), 0.99f, "rotation t=" + times[i]);
                }
            }
            finally
            {
                if (director != null)
                    director.Stop();
                if (startedSampling && AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
                if (host != null) Object.DestroyImmediate(host);
                if (camGo != null) Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void AuthoredCamera_IsLeftForTheArtist()
        {
            Assert.IsTrue(IntroAssets.IsAuthoredCameraTrack("Intro Camera (Authored)"));
            Assert.IsFalse(IntroAssets.IsAuthoredCameraTrack(IntroAssets.CameraTrack));
            Assert.IsFalse(IntroAssets.ShouldRebuildCameraTrack(true, "Intro Camera (Authored)"));
            Assert.IsTrue(IntroAssets.ShouldRebuildCameraTrack(true, IntroAssets.CameraTrack));
            Assert.IsTrue(IntroAssets.ShouldRebuildCameraTrack(false, null));
        }

        [Test]
        public void Crossfade_DoesNotHoldOnBlack()
        {
            Assert.AreEqual(0f, IntroShot.CrossfadeAlpha(0f), 0.0001f);
            Assert.AreEqual(1f, IntroShot.CrossfadeAlpha(IntroShot.CrossfadeSeconds), 0.0001f);
            Assert.AreEqual(0f, IntroShot.CrossfadeAlpha(IntroShot.CrossfadeSeconds * 2f), 0.0001f);
            float justAfter = IntroShot.CrossfadeAlpha(IntroShot.CrossfadeSeconds + 1f / 60f);
            Assert.Less(justAfter, 0.99f);
            Assert.Greater(justAfter, 0.9f);

            var intro = NewIntro();
            var cam = NewCamera();
            try
            {
                intro.PlayFallback(cam, ColonyLayout.CameraFocus, CelestialBodyCatalog.Earth());
                Step(intro, IntroShot.Duration);
                Assert.IsFalse(intro.IsPlaying);
                float peak = intro.OverlayAlpha;
                intro.AdvanceCrossfade(1f / 60f);
                Assert.Less(intro.OverlayAlpha, peak);
                Assert.Less(intro.OverlayAlpha, 0.99f);
            }
            finally
            {
                if (intro != null) Object.DestroyImmediate(intro.gameObject);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
            }
        }

        [Test]
        public void TitleReveal_StaggersSolarThenEmblemThenMajesty()
        {
            var intro = NewIntro();
            var cam = NewCamera();
            var title = new GameObject("IntroTitle");
            title.transform.SetParent(intro.transform, false);
            var solar = new GameObject(IntroAssets.WordSolar);
            solar.transform.SetParent(title.transform, false);
            var letter = new GameObject("Letter_SOLAR_0_S");
            letter.transform.SetParent(solar.transform, false);
            var emblem = new GameObject(IntroAssets.EmblemPlanet);
            emblem.transform.SetParent(solar.transform, false);
            var majesty = new GameObject(IntroAssets.WordMajesty);
            majesty.transform.SetParent(title.transform, false);
            var trim = new GameObject(IntroAssets.TrimRoot);
            trim.transform.SetParent(title.transform, false);
            var renderer = letter.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            try
            {
                intro.PlayFallback(cam, ColonyLayout.CameraFocus, CelestialBodyCatalog.Earth());
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode);
                Assert.IsFalse(renderer.receiveShadows);
                Assert.Less(letter.transform.localScale.x, 0.05f);

                Step(intro, IntroShot.TitleOn + IntroTitleMotion.RevealDuration);
                Assert.Greater(letter.transform.localScale.x, 0.9f);
                Assert.Less(emblem.transform.localScale.x, 0.55f);
                Assert.Less(majesty.transform.localScale.x, 0.05f);

                float majestyMid = IntroShot.TitleOn + IntroTitleMotion.RevealStep * 2f + IntroTitleMotion.RevealDuration * 0.45f;
                Step(intro, majestyMid - intro.Elapsed);
                Assert.Greater(emblem.transform.localScale.x, 0.9f);
                Assert.Greater(majesty.transform.localScale.x, 0.35f);
                Assert.Less(majesty.transform.localScale.x, 0.7f);
                Assert.Greater(trim.transform.localScale.x, 0.35f);
                Assert.Less(majesty.transform.localScale.x, letter.transform.localScale.x);
            }
            finally
            {
                if (intro != null) Object.DestroyImmediate(intro.gameObject);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
            }
        }

        [Test]
        public void TitleGlint_SweepsAPropertyBlockAndLeavesTheSharedMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                Assert.Inconclusive("URP shader unavailable.");

            var intro = NewIntro();
            var cam = NewCamera();
            var title = new GameObject("IntroTitle");
            title.transform.SetParent(intro.transform, false);
            var goldGo = new GameObject("GoldLetter");
            goldGo.transform.SetParent(title.transform, false);
            var renderer = goldGo.AddComponent<MeshRenderer>();
            var mat = new Material(shader) { name = IntroAssets.GoldMaterialName };
            Assert.IsTrue(mat.HasProperty("_BaseMap_ST"), shader.name + " has no _BaseMap_ST");
            var authored = new Vector4(1f, 3f, IntroTitleMotion.GlintFrom, 0f);
            mat.SetVector("_BaseMap_ST", authored);
            renderer.sharedMaterial = mat;
            float mid = IntroShot.TitleOn + IntroTitleMotion.GlintLead + IntroTitleMotion.GlintDuration * 0.5f;
            try
            {
                intro.PlayFallback(cam, ColonyLayout.CameraFocus, CelestialBodyCatalog.Earth());
                Step(intro, mid);
                Assert.AreEqual(authored.z, mat.GetVector("_BaseMap_ST").z, 0.0001f);
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                float swept = block.GetVector("_BaseMap_ST").z;
                Assert.Less(Mathf.Abs(swept), 0.2f);
                Assert.AreEqual(authored.x, block.GetVector("_BaseMap_ST").x, 0.001f);
                Assert.AreEqual(authored.y, block.GetVector("_BaseMap_ST").y, 0.001f);
            }
            finally
            {
                if (intro != null) Object.DestroyImmediate(intro.gameObject);
                if (mat != null) Object.DestroyImmediate(mat);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
            }
        }

        [Test]
        public void Dusk_RestoresSunAndAmbientOnSkip()
        {
            var intro = NewIntro();
            var cam = NewCamera();
            var sunGo = new GameObject("intro-dusk-sun");
            sunGo.hideFlags = HideFlags.HideAndDontSave;
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.9f, 0.95f, 1f, 1f);
            sun.intensity = 1.4f;
            sun.transform.rotation = Quaternion.Euler(50f, -20f, 0f);
            Color sunColor = sun.color;
            Quaternion sunRotation = sun.transform.rotation;

            var fillGo = new GameObject("intro-dusk-fill");
            fillGo.hideFlags = HideFlags.HideAndDontSave;
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.6f, 0.7f, 0.9f, 1f);
            fill.intensity = 0.4f;
            Color fillColor = fill.color;

            Color sky = RenderSettings.ambientSkyColor;
            Color equator = RenderSettings.ambientEquatorColor;
            Color ground = RenderSettings.ambientGroundColor;
            Color flat = RenderSettings.ambientLight;
            float ambIntensity = RenderSettings.ambientIntensity;
            var ambMode = RenderSettings.ambientMode;
            Color fog = RenderSettings.fogColor;
            var skybox = RenderSettings.skybox;
            Material skyProbe = null;
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                skyProbe = new Material(skyShader) { name = "SM_IntroDuskProbe" };
                skyProbe.SetColor("_SkyTint", new Color(0.62f, 0.78f, 1f, 1f));
                skyProbe.SetColor("_GroundColor", new Color(0.76f, 0.86f, 0.94f, 1f));
                skyProbe.SetFloat("_AtmosphereThickness", 1.28f);
                RenderSettings.skybox = skyProbe;
            }
            var liveSky = skyProbe != null ? skyProbe : skybox;
            bool hasTint = liveSky != null && liveSky.HasProperty("_SkyTint");
            bool hasGround = liveSky != null && liveSky.HasProperty("_GroundColor");
            bool hasAir = liveSky != null && liveSky.HasProperty("_AtmosphereThickness");
            Color skyTint = hasTint ? liveSky.GetColor("_SkyTint") : Color.black;
            Color skyGround = hasGround ? liveSky.GetColor("_GroundColor") : Color.black;
            float atmosphere = hasAir ? liveSky.GetFloat("_AtmosphereThickness") : 0f;

            intro.SetLightsForTests(sun, fill);
            try
            {
                intro.PlayFallback(cam, ColonyLayout.CameraFocus, CelestialBodyCatalog.Earth());
                Assert.Less(IntroDusk.Elevation(sun.transform.rotation), IntroDusk.MaxElevation + 0.05f);
                Assert.AreEqual(1.4f * 0.4f, sun.intensity, 0.001f);
                Assert.Greater(sun.color.r, sun.color.b);
                Assert.Greater(RenderSettings.ambientSkyColor.r, RenderSettings.ambientSkyColor.b);
                Assert.AreNotEqual(sky, RenderSettings.ambientSkyColor);

                Color paleFog = new Color(0.76f, 0.86f, 0.94f, 1f);
                Color duskFog = IntroDusk.TintFog(paleFog);
                Assert.Greater(duskFog.r, duskFog.b);
                Assert.Less(duskFog.b, paleFog.b);
                Assert.Greater(ColorGap(RenderSettings.fogColor, fog), 0.02f);
                Assert.Greater(RenderSettings.fogColor.r, RenderSettings.fogColor.b);
                if (hasTint)
                {
                    Color tintNow = liveSky.GetColor("_SkyTint");
                    Assert.Greater(tintNow.r, tintNow.b);
                    Assert.Greater(ColorGap(tintNow, skyTint), 0.05f);
                }
                if (hasGround)
                {
                    Color groundNow = liveSky.GetColor("_GroundColor");
                    Assert.Greater(groundNow.r, groundNow.b);
                }
                if (hasAir)
                    Assert.Less(liveSky.GetFloat("_AtmosphereThickness"), atmosphere - 0.2f);

                intro.Skip();
                Assert.AreEqual(1.4f, sun.intensity, 0.0001f);
                Assert.Less(ColorGap(sun.color, sunColor), 0.001f);
                Assert.Greater(Quaternion.Dot(sun.transform.rotation, sunRotation), 0.999f);
                Assert.Less(ColorGap(fill.color, fillColor), 0.001f);
                Assert.AreEqual(0.4f, fill.intensity, 0.0001f);
                Assert.Less(ColorGap(RenderSettings.ambientSkyColor, sky), 0.001f);
                Assert.Less(ColorGap(RenderSettings.ambientEquatorColor, equator), 0.001f);
                Assert.Less(ColorGap(RenderSettings.ambientGroundColor, ground), 0.001f);
                Assert.Less(ColorGap(RenderSettings.ambientLight, flat), 0.001f);
                Assert.AreEqual(ambIntensity, RenderSettings.ambientIntensity, 0.0001f);
                Assert.AreEqual(ambMode, RenderSettings.ambientMode);
                Assert.Less(ColorGap(RenderSettings.fogColor, fog), 0.001f);
                if (hasTint)
                    Assert.Less(ColorGap(liveSky.GetColor("_SkyTint"), skyTint), 0.001f);
                if (hasGround)
                    Assert.Less(ColorGap(liveSky.GetColor("_GroundColor"), skyGround), 0.001f);
                if (hasAir)
                    Assert.AreEqual(atmosphere, liveSky.GetFloat("_AtmosphereThickness"), 0.0001f);
            }
            finally
            {
                if (intro != null) Object.DestroyImmediate(intro.gameObject);
                RenderSettings.ambientMode = ambMode;
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientLight = flat;
                RenderSettings.ambientIntensity = ambIntensity;
                RenderSettings.fogColor = fog;
                RenderSettings.skybox = skybox;
                if (skyProbe == null && skybox != null)
                {
                    if (hasTint) skybox.SetColor("_SkyTint", skyTint);
                    if (hasGround) skybox.SetColor("_GroundColor", skyGround);
                    if (hasAir) skybox.SetFloat("_AtmosphereThickness", atmosphere);
                }
                if (skyProbe != null) Object.DestroyImmediate(skyProbe);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
                if (sunGo != null) Object.DestroyImmediate(sunGo);
                if (fillGo != null) Object.DestroyImmediate(fillGo);
            }
        }

        [Test]
        public void Sting_PlaysOnceWhenTheClockCrossesItAndStopsOnSkip()
        {
            var timeline = IntroSequence.LoadTimeline();
            Assert.IsNotNull(timeline, "Resources/Intro/IntroTimeline is missing.");
            AudioTrack stingTrack = null;
            foreach (var output in timeline.outputs)
            {
                if (output.sourceObject is AudioTrack track && track.name == IntroAssets.StingTrack)
                    stingTrack = track;
            }
            Assert.IsNotNull(stingTrack);
            Assert.IsTrue(stingTrack.muted, "a Manual director cannot play this; an unmuted track would double-play later");

            var intro = NewIntro();
            var cam = NewCamera();
            bool startedSampling = false;
            try
            {
                if (!AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    startedSampling = true;
                }

                intro.Play(cam, ColonyLayout.CameraFocus, CelestialBodyCatalog.Earth(), false);
                Assert.AreEqual(IntroPlayback.Timeline, intro.Playback);
                Step(intro, IntroShot.TitleOn - 0.05f);
                Assert.AreEqual(0, intro.StingPlays);
                Step(intro, 0.1f);
                Assert.AreEqual(1, intro.StingPlays);
                Assert.IsFalse(intro.StingStopped);
                Step(intro, 0.4f);
                Assert.AreEqual(1, intro.StingPlays, "crossing the sting time again must not retrigger");
                intro.Skip();
                Assert.IsTrue(intro.StingStopped);
                Assert.AreEqual(1, intro.StingPlays);
            }
            finally
            {
                if (intro != null && intro.IsPlaying)
                    intro.Abort();
                if (startedSampling && AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
                if (intro != null) Object.DestroyImmediate(intro.gameObject);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
            }
        }

        [Test]
        public void SlowFirstFrame_DoesNotSkipTheOpening()
        {
            Assert.AreEqual(IntroShot.MaxFrameDelta, IntroShot.ClampDelta(5f), 0.000001f);
            Assert.AreEqual(0f, IntroShot.ClampDelta(-2f), 0.000001f);
            Assert.AreEqual(0f, IntroCitadel.Weight(IntroCitadel.HoldStart), 0.0001f);
            Assert.AreEqual(1f, IntroCitadel.Weight(IntroShot.Duration), 0.0001f);

            var intro = NewIntro();
            var cam = NewCamera();
            bool startedSampling = false;
            try
            {
                if (!AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    startedSampling = true;
                }

                intro.Play(cam, ColonyLayout.CameraFocus, CelestialBodyCatalog.Earth(), false);
                intro.Advance(5f);
                Assert.IsTrue(intro.IsPlaying, "a 5s first frame must not finish the shot");
                Assert.Greater(intro.Elapsed, 0f);
                Assert.LessOrEqual(intro.Elapsed, IntroShot.MaxFrameDelta + 0.0001f);
                Assert.Less(intro.Elapsed, IntroShot.TitleOn);
                Assert.Less(intro.Elapsed, IntroCitadel.HoldStart);

                var settle = IntroShot.ColonySettlePosition(ColonyLayout.CameraFocus);
                Assert.Greater(Vector3.Distance(cam.transform.position, settle), 5f);

                if (intro.Playback == IntroPlayback.Timeline)
                {
                    var director = intro.GetComponent<PlayableDirector>();
                    Assert.IsNotNull(director);
                    Assert.AreEqual(DirectorUpdateMode.Manual, director.timeUpdateMode);
                    Assert.AreEqual(intro.Elapsed, (float)director.time, 0.0001f);
                }
            }
            finally
            {
                if (intro != null && intro.IsPlaying)
                    intro.Abort();
                if (startedSampling && AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
                if (intro != null) Object.DestroyImmediate(intro.gameObject);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
            }
        }

        [Test]
        public void CitadelWindows_DimDuringTheTitleHoldAndRestoreOnSkip()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");
            if (shader == null)
                Assert.Inconclusive("Lit shader unavailable.");

            var intro = NewIntro();
            var cam = NewCamera();
            var warmGo = new GameObject("citadel-window");
            warmGo.hideFlags = HideFlags.HideAndDontSave;
            var warm = warmGo.AddComponent<MeshRenderer>();
            var warmMat = new Material(shader) { name = "SM_TestWindow" };
            if (!warmMat.HasProperty("_EmissionColor"))
            {
                Object.DestroyImmediate(warmMat);
                Object.DestroyImmediate(warmGo);
                Object.DestroyImmediate(intro.gameObject);
                Object.DestroyImmediate(cam.gameObject);
                Assert.Inconclusive(shader.name + " has no _EmissionColor");
            }

            Color warmEmit = new Color(1.55f, 1.02f, 0.5f, 1f);
            warmMat.SetColor("_EmissionColor", warmEmit);
            warmMat.EnableKeyword("_EMISSION");
            warm.sharedMaterial = warmMat;

            var coolGo = new GameObject("citadel-cyan");
            coolGo.hideFlags = HideFlags.HideAndDontSave;
            var cool = coolGo.AddComponent<MeshRenderer>();
            var coolMat = new Material(shader) { name = "SM_TestCyan" };
            Color coolEmit = new Color(0.2f, 1.15f, 1.65f, 1f);
            coolMat.SetColor("_EmissionColor", coolEmit);
            cool.sharedMaterial = coolMat;

            var lampGo = new GameObject("citadel-lamp");
            lampGo.hideFlags = HideFlags.HideAndDontSave;
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.intensity = 3.2f;

            intro.SetCitadelForTests(new[] { warm, cool }, new[] { lamp });
            try
            {
                intro.PlayFallback(cam, ColonyLayout.CameraFocus, CelestialBodyCatalog.Earth());
                Assert.AreEqual(1, intro.CitadelDimRendererCount);
                Assert.AreEqual(1, intro.CitadelDimLightCount);
                Step(intro, 3.2f);
                Assert.AreEqual(3.2f, lamp.intensity, 0.001f);
                Assert.Less(ColorGap(warmMat.GetColor("_EmissionColor"), warmEmit), 0.001f);

                Step(intro, 4f - intro.Elapsed);
                Assert.IsTrue(intro.IsPlaying);
                float keep = IntroCitadel.Keep(intro.Elapsed);
                Assert.Less(keep, 0.2f);
                Assert.AreEqual(3.2f * keep, lamp.intensity, 0.001f);

                var block = new MaterialPropertyBlock();
                warm.GetPropertyBlock(block, 0);
                Color dimmed = block.GetColor("_EmissionColor");
                Assert.Less(ColorGap(dimmed, IntroCitadel.ScaleEmission(warmEmit, keep)), 0.02f);
                Assert.AreNotSame(warmMat, warm.sharedMaterial);
                Assert.Less(ColorGap(warm.sharedMaterial.GetColor("_EmissionColor"), IntroCitadel.ScaleEmission(warmEmit, keep)), 0.02f);
                Assert.Less(ColorGap(warmMat.GetColor("_EmissionColor"), warmEmit), 0.001f);

                cool.GetPropertyBlock(block);
                Assert.Greater(
                    ColorGap(block.GetColor("_EmissionColor"), IntroCitadel.ScaleEmission(coolEmit, keep)),
                    0.5f);
                Assert.Less(ColorGap(cool.sharedMaterial.GetColor("_EmissionColor"), coolEmit), 0.001f);

                intro.Skip();
                Assert.AreEqual(3.2f, lamp.intensity, 0.0001f);
                Assert.AreSame(warmMat, warm.sharedMaterial);
                warm.GetPropertyBlock(block, 0);
                Assert.Less(ColorGap(block.GetColor("_EmissionColor"), warmEmit), 0.02f);
                Assert.Less(ColorGap(warmMat.GetColor("_EmissionColor"), warmEmit), 0.001f);
                Assert.Less(ColorGap(cool.sharedMaterial.GetColor("_EmissionColor"), coolEmit), 0.001f);
            }
            finally
            {
                if (intro != null) Object.DestroyImmediate(intro.gameObject);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
                if (warmMat != null) Object.DestroyImmediate(warmMat);
                if (coolMat != null) Object.DestroyImmediate(coolMat);
                if (warmGo != null) Object.DestroyImmediate(warmGo);
                if (coolGo != null) Object.DestroyImmediate(coolGo);
                if (lampGo != null) Object.DestroyImmediate(lampGo);
            }
        }

        [Test]
        public void CitadelDim_DestroysItsInstancesOnFinishAbortAndAMissingRenderer()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");
            if (shader == null)
                Assert.Inconclusive("Lit shader unavailable.");

            Color warmEmit = new Color(1.55f, 1.02f, 0.5f, 1f);
            // 0 = the shot finishes on its own, 1 = abort, 2 = the window renderer is destroyed mid-dim.
            for (int path = 0; path < 3; path++)
            {
                var intro = NewIntro();
                var cam = NewCamera();
                var warmGo = new GameObject("citadel-window");
                warmGo.hideFlags = HideFlags.HideAndDontSave;
                var warm = warmGo.AddComponent<MeshRenderer>();
                var warmMat = new Material(shader) { name = "SM_TestWindow" };
                warmMat.SetColor("_EmissionColor", warmEmit);
                warmMat.EnableKeyword("_EMISSION");
                warm.sharedMaterials = new[] { warmMat };
                intro.SetCitadelForTests(new[] { warm }, new Light[0]);
                try
                {
                    intro.PlayFallback(cam, ColonyLayout.CameraFocus, CelestialBodyCatalog.Earth());
                    Step(intro, 4f);
                    Assert.IsTrue(intro.IsPlaying, "path " + path);
                    var instance = warm.sharedMaterial;
                    Assert.AreNotSame(warmMat, instance, "path " + path + ": the dim uses a private instance");
                    Assert.IsTrue(instance != null);

                    if (path == 0)
                    {
                        Step(intro, IntroShot.Duration);
                        Assert.IsFalse(intro.IsPlaying, "the shot should have finished");
                    }
                    else if (path == 1)
                    {
                        intro.Abort();
                    }
                    else
                    {
                        Object.DestroyImmediate(warmGo);
                        warmGo = null;
                        intro.Skip();
                    }

                    Assert.IsTrue(instance == null, "path " + path + ": the dim instance must be destroyed");
                    if (warmGo != null)
                    {
                        Assert.AreSame(warmMat, warm.sharedMaterial, "path " + path);
                        Assert.AreEqual(1, warm.sharedMaterials.Length);
                    }
                    Assert.Less(ColorGap(warmMat.GetColor("_EmissionColor"), warmEmit), 0.001f,
                        "path " + path + ": the shared material is never written");
                }
                finally
                {
                    if (intro != null) Object.DestroyImmediate(intro.gameObject);
                    if (cam != null) Object.DestroyImmediate(cam.gameObject);
                    if (warmMat != null) Object.DestroyImmediate(warmMat);
                    if (warmGo != null) Object.DestroyImmediate(warmGo);
                }
            }
        }

        private static float ColorGap(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a);
        }

        /// <summary>Walks the clamped frame clock to <paramref name="seconds"/> ahead. A single Advance cannot jump.</summary>
        private static void Step(IntroSequence intro, float seconds)
        {
            float target = intro.Elapsed + Mathf.Max(0f, seconds);
            int guard = 0;
            while (intro.IsPlaying && guard++ < 800)
            {
                float remain = target - intro.Elapsed;
                if (remain <= 0.00002f) break;
                float before = intro.Elapsed;
                intro.Advance(Mathf.Min(IntroShot.MaxFrameDelta, remain));
                if (intro.Elapsed <= before) break;
            }
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
