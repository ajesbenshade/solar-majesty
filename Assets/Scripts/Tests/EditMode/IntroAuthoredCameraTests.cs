using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Guards the hand-authored "Intro Camera (Authored)" track. IntroBuilder.BuildAll leaves it alone,
    /// so nothing regenerates it if the settle pose moves. If the settle or title pose in IntroShot changes,
    /// these fail and the authored camera needs re-keying (see Docs/Art/INTRO_CAMERA.md).
    /// </summary>
    public class IntroAuthoredCameraTests
    {
        private const string AuthoredTrackName = "Intro Camera (Authored)";

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
                var settle = IntroShot.Sample(IntroShot.CameraSettle, focus, false);

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

                // Settled on the pose the title slot is placed from, at 35 degrees, before the fade starts.
                float holdFrom = IntroShot.FadeStart - 1f;
                for (float t = holdFrom; t <= IntroShot.Duration + 1e-4f; t += 0.2f)
                {
                    Sample(director, t);
                    Assert.Less(Vector3.Distance(cam.transform.position, settle.Position), 0.05f, "settled position t=" + t);
                    Assert.Greater(Mathf.Abs(Quaternion.Dot(cam.transform.rotation, settle.Rotation)), 0.99995f, "settled rotation t=" + t);
                    Assert.AreEqual(settle.FieldOfView, cam.fieldOfView, 0.05f, "settled FOV t=" + t);
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

        private static void Sample(PlayableDirector director, float t)
        {
            director.time = t;
            director.Evaluate();
            if (director.playableGraph.IsValid())
                director.playableGraph.Evaluate(0);
        }
    }
}
