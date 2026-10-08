using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Guards the hand-authored "Intro Camera (Authored)" track. IntroBuilder.BuildAll leaves it alone,
    /// so nothing regenerates it if the title slot moves. The track ends on its own pose, not the code
    /// settle pose: 11 m in front of <see cref="IntroShot.TitlePose"/> on the slot's yaw, looking slightly
    /// up so the lifted letters read against the dusk sky. If the title pose in IntroShot changes,
    /// these fail and the authored camera needs re-keying (see Docs/Art/INTRO_CAMERA.md).
    /// </summary>
    public class IntroAuthoredCameraTests
    {
        private const string AuthoredTrackName = "Intro Camera (Authored)";

        // SM_Title_SolarMajesty (and the placeholder) at slot scale 1: about 7.0 m wide, 2.34 m tall.
        private const float TitleWidth = 7.0f;
        private const float TitleHeight = 2.34f;

        private static AnimationTrack FindAuthored(TimelineAsset timeline)
        {
            foreach (var output in timeline.outputs)
            {
                if (output.sourceObject is AnimationTrack track && track.name == AuthoredTrackName)
                    return track;
            }
            return null;
        }

        [Test]
        public void AuthoredCamera_IsMarkedForTheBuilderAndPlaysAbsolutePoses()
        {
            var timeline = IntroSequence.LoadTimeline();
            Assert.IsNotNull(timeline, "Resources/Intro/IntroTimeline is missing.");
            var track = FindAuthored(timeline);
            Assert.IsNotNull(track, "authored camera track missing");
            Assert.IsTrue(IntroAssets.IsAuthoredCameraTrack(track.name));
            Assert.IsFalse(IntroAssets.ShouldRebuildCameraTrack(true, track.name));
            Assert.AreEqual(TrackOffset.ApplyTransformOffsets, track.trackOffset);
            int clips = 0;
            foreach (var clip in track.GetClips())
            {
                clips++;
                var asset = clip.asset as AnimationPlayableAsset;
                Assert.IsNotNull(asset);
                Assert.IsNotNull(asset.clip);
                Assert.IsFalse(asset.removeStartOffset, "absolute poses, not a delta from the origin");
                Assert.AreEqual(0d, clip.start, 1e-4);
                Assert.GreaterOrEqual(clip.end, IntroShot.Duration - 1e-4);
            }
            Assert.AreEqual(1, clips);
        }

        [Test]
        public void AuthoredCamera_PassesEarthThenHoldsOnTheTitleFrame()
        {
            var timeline = IntroSequence.LoadTimeline();
            Assert.IsNotNull(timeline);
            var track = FindAuthored(timeline);
            Assert.IsNotNull(track);

            var camGo = new GameObject("intro-authored-cam");
            camGo.hideFlags = HideFlags.HideAndDontSave;
            var cam = camGo.AddComponent<Camera>();
            var animator = camGo.AddComponent<Animator>();
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var host = new GameObject("intro-authored-director");
            host.hideFlags = HideFlags.HideAndDontSave;
            var director = host.AddComponent<PlayableDirector>();
            bool startedSampling = false;
            try
            {
                director.playableAsset = timeline;
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.Manual;
                director.extrapolationMode = DirectorWrapMode.Hold;
                director.SetGenericBinding(track, animator);
                if (!AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    startedSampling = true;
                }
                director.RebuildGraph();
                director.Play();

                Vector3 focus = ColonyLayout.CameraFocus;
                Vector3 earth = IntroShot.EarthPosition(focus);
                float earthRadius = IntroShot.EarthScale * 0.5f;
                var slot = IntroShot.TitlePose(focus);

                for (float t = 0f; t <= IntroShot.Duration + 1e-4f; t += 0.1f)
                {
                    Sample(director, t);
                    Vector3 p = cam.transform.position;
                    Assert.Greater(p.y, IntroShot.MinCameraHeight, "above ground and water at t=" + t);
                    Assert.Greater(Vector3.Distance(p, earth), earthRadius + 10f, "keeps clear of the Earth globe at t=" + t);
                }

                // Opening: the Earth globe is in frame.
                Sample(director, 0f);
                Vector3 toEarth = (earth - cam.transform.position).normalized;
                float halfFov = cam.fieldOfView * 0.5f;
                Assert.Greater(Vector3.Dot(cam.transform.forward, toEarth), Mathf.Cos(Mathf.Deg2Rad * halfFov), "Earth in frame at t=0");

                // The re-key assumes the lifted slot (TitleLift 5). If this moves, re-key the end pose.
                Assert.AreEqual(180.736f, slot.Position.x, 0.01f, "title slot x");
                Assert.AreEqual(21.5f, slot.Position.y, 0.01f, "title slot y");
                Assert.AreEqual(178.736f, slot.Position.z, 0.01f, "title slot z");

                // Holds still for at least a second before the fade.
                float holdFrom = IntroShot.FadeStart - 1f;
                Sample(director, holdFrom);
                Vector3 holdPos = cam.transform.position;
                Quaternion holdRot = cam.transform.rotation;
                for (float t = holdFrom; t <= IntroShot.Duration + 1e-4f; t += 0.2f)
                {
                    Sample(director, t);
                    Assert.Less(Vector3.Distance(cam.transform.position, holdPos), 0.01f, "held position t=" + t);
                    Assert.Greater(Mathf.Abs(Quaternion.Dot(cam.transform.rotation, holdRot)), 0.99999f, "held rotation t=" + t);
                    Assert.AreEqual(35f, cam.fieldOfView, 0.05f, "held FOV t=" + t);
                }

                Sample(director, holdFrom);
                Vector3 fwd = cam.transform.forward;
                Vector3 toTitle = slot.Position - cam.transform.position;
                Assert.Less(Vector3.Angle(fwd, toTitle), 0.5f, "title centred");
                Assert.AreEqual(IntroShot.TitleDistance, Vector3.Dot(toTitle, fwd), 0.1f, "title 11 m down the view axis");
                Vector3 slotFwd = slot.Rotation * Vector3.forward;
                float camYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                float slotYaw = Mathf.Atan2(slotFwd.x, slotFwd.z) * Mathf.Rad2Deg;
                Assert.AreEqual(slotYaw, camYaw, 0.5f, "square to the letters (no horizontal skew)");
                Assert.AreEqual(0f, cam.transform.eulerAngles.z > 180f ? cam.transform.eulerAngles.z - 360f : cam.transform.eulerAngles.z, 0.2f, "no roll at the hold");
                float pitchUp = Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg;
                Assert.That(pitchUp, Is.InRange(0f, 14f), "level or slightly up, so the letters sit against sky");

                Vector3 right = slot.Rotation * Vector3.right * (TitleWidth * 0.5f);
                Vector3 up = slot.Rotation * Vector3.up * (TitleHeight * 0.5f);
                var corners = new[] { slot.Position - right - up, slot.Position + right - up, slot.Position - right + up, slot.Position + right + up };
                Vector3 horizonPoint = cam.transform.position + new Vector3(fwd.x, 0f, fwd.z).normalized * 2000f;
                horizonPoint.y = 0f;
                foreach (float aspect in new[] { 16f / 9f, 16f / 10f })
                {
                    cam.aspect = aspect;
                    float minX = 1f, maxX = 0f, minY = 1f;
                    foreach (var c in corners)
                    {
                        Vector3 v = cam.WorldToViewportPoint(c);
                        Assert.Greater(v.z, 0f);
                        Assert.That(v.x, Is.InRange(0.02f, 0.98f), "title inside the frame at aspect " + aspect);
                        Assert.That(v.y, Is.InRange(0.02f, 0.98f), "title inside the frame at aspect " + aspect);
                        minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minY = Mathf.Min(minY, v.y);
                    }
                    if (Mathf.Abs(aspect - 16f / 9f) < 0.01f)
                        Assert.That(maxX - minX, Is.InRange(0.55f, 0.63f), "title fills 55-63% of a 16:9 frame");
                    float horizonY = cam.WorldToViewportPoint(horizonPoint).y;
                    Assert.Greater(minY, horizonY + 0.03f, "letters above the horizon at aspect " + aspect);
                }
                cam.ResetAspect();
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

        private static void Sample(PlayableDirector director, float t)
        {
            director.time = t;
            director.Evaluate();
            if (director.playableGraph.IsValid())
                director.playableGraph.Evaluate(0);
        }
    }
}
