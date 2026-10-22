using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>Launch and victory camera beats: state machine, shots, and the director's handoff.</summary>
    public class CameraBeatTests
    {
        private static BeatContext Ctx(BeatPose start, Vector3 subject, float h, Vector3 lifterBase) => new BeatContext
        {
            Start = start,
            Subject = subject,
            LifterHeight = h,
            LifterBase = lifterBase,
            ViewFlat = Vector3.forward,
            LaunchYaw = CameraBeatTuning.LaunchYawDefault
        };

        // ---- state machine -------------------------------------------------------------------------

        [Test]
        public void FullBeats_RunFiveToEightSeconds()
        {
            Assert.That(CameraBeatTuning.Launch.Total, Is.InRange(5f, 8f));
            Assert.That(CameraBeatTuning.Victory.Total, Is.InRange(5f, 8f));
        }

        [Test]
        public void Sequence_WalksPhasesInOrder_AndReportsCompletionOnce()
        {
            var seq = new CameraBeatSequence();
            seq.Begin(CameraBeatKind.Victory, reduceMotion: false);
            var p = CameraBeatTuning.Victory;
            Assert.AreEqual(CameraBeatPhase.Intro, seq.Phase);
            Assert.IsTrue(seq.Active);

            seq.Tick(p.Intro + 0.01f, true);
            Assert.AreEqual(CameraBeatPhase.Main, seq.Phase, "zero-length Hold falls straight through");
            seq.Tick(p.Main, true);
            Assert.AreEqual(CameraBeatPhase.Return, seq.Phase);
            Assert.IsFalse(seq.TakeCompletion(), "no completion while running");
            seq.Tick(p.Return, true);

            Assert.IsTrue(seq.Finished);
            Assert.AreEqual(CameraBeatEnd.Completed, seq.End);
            Assert.IsTrue(seq.TakeCompletion());
            Assert.IsFalse(seq.TakeCompletion(), "completion is reported exactly once");
        }

        [Test]
        public void Sequence_LargeFrameStep_CannotOvershootIntoALoop()
        {
            var seq = new CameraBeatSequence();
            seq.Begin(CameraBeatKind.Launch, false);
            seq.Tick(1000f, true);
            Assert.AreEqual(CameraBeatEnd.Completed, seq.End);
            seq.Tick(1f, true); // ticking a finished beat is harmless
            Assert.AreEqual(CameraBeatPhase.Done, seq.Phase);
        }

        [Test]
        public void Skip_IsIgnoredInTheGraceWindow_ThenEndsTheBeat()
        {
            var seq = new CameraBeatSequence();
            seq.Begin(CameraBeatKind.Launch, false);
            Assert.IsFalse(seq.Skip(), "the click that launched must not skip the launch");
            seq.Tick(CameraBeatTuning.SkipGrace + 0.01f, true);
            Assert.IsTrue(seq.Skip());
            Assert.AreEqual(CameraBeatEnd.Skipped, seq.End);
            Assert.IsTrue(seq.TakeCompletion(), "a skipped launch still hands off to the next world");
            Assert.IsFalse(seq.Skip(), "skipping a finished beat does nothing");
        }

        [Test]
        public void TargetLost_EndsAtOnce_AndStillHandsOff()
        {
            var seq = new CameraBeatSequence();
            seq.Begin(CameraBeatKind.Launch, false);
            seq.Tick(0.5f, true);
            seq.Tick(0.016f, targetAlive: false);
            Assert.AreEqual(CameraBeatEnd.TargetLost, seq.End);
            Assert.IsFalse(seq.Active);
            Assert.IsTrue(seq.TakeCompletion());
        }

        [Test]
        public void Abort_EndsWithoutCompletion()
        {
            var seq = new CameraBeatSequence();
            seq.Begin(CameraBeatKind.Victory, false);
            seq.Abort();
            Assert.AreEqual(CameraBeatEnd.Aborted, seq.End);
            Assert.IsFalse(seq.TakeCompletion(), "a scene change must not run the handoff a second time");
        }

        [Test]
        public void ReduceMotion_IsShorter_Still_AndShakeFree()
        {
            Assert.Less(CameraBeatTuning.LaunchReduced.Total, CameraBeatTuning.Launch.Total);
            Assert.Less(CameraBeatTuning.VictoryReduced.Total, CameraBeatTuning.Victory.Total);
            Assert.IsFalse(CameraBeatTuning.LaunchReduced.CameraMoves);
            Assert.IsFalse(CameraBeatTuning.VictoryReduced.CameraMoves);
            Assert.IsFalse(CameraBeatTuning.LaunchReduced.Shake);
            Assert.AreSame(CameraBeatTuning.LaunchReduced, CameraBeatTuning.For(CameraBeatKind.Launch, true));

            var seq = new CameraBeatSequence();
            seq.Begin(CameraBeatKind.Launch, reduceMotion: true);
            Assert.AreEqual(CameraBeatPhase.Hold, seq.Phase, "no travel in: the beat starts on the still shot");

            var start = new BeatPose(new Vector3(0f, 40f, -40f), Vector3.zero, 36f);
            var pad = new Vector3(10f, 0f, 10f);
            BeatPose a = CameraBeatShots.Evaluate(seq, Ctx(start, pad, 3f, pad));
            seq.Tick(1.5f, true);
            BeatPose b = CameraBeatShots.Evaluate(seq, Ctx(start, pad, 3f, pad + Vector3.up * 20f));
            Assert.AreEqual(a.Position, b.Position, "the reduced camera never moves");
            Assert.AreEqual(a.LookAt, b.LookAt, "nor tilts to follow the climb");
        }

        // ---- shots ---------------------------------------------------------------------------------

        [Test]
        public void Launch_StartsOnThePlayersView_AndEndsLookingUpAtTheClimb()
        {
            var seq = new CameraBeatSequence();
            seq.Begin(CameraBeatKind.Launch, false);
            var start = new BeatPose(new Vector3(-30f, 40f, -30f), new Vector3(5f, 0f, 5f), 36f);
            var pad = new Vector3(20f, 0f, 20f);
            const float h = 3f;

            BeatPose first = CameraBeatShots.Evaluate(seq, Ctx(start, pad, h, pad));
            Assert.AreEqual(start.Position, first.Position);

            seq.Tick(CameraBeatTuning.Launch.Intro + CameraBeatTuning.Launch.Hold + CameraBeatTuning.Launch.Main * 0.99f, true);
            Vector3 lifter = pad + Vector3.up * CameraBeatTuning.ClimbAltitude(seq.MainT, h);
            BeatPose climb = CameraBeatShots.Evaluate(seq, Ctx(start, pad, h, lifter));
            Assert.Greater(climb.LookAt.y, climb.Position.y, "the camera tilts up to the climbing lifter");
            Assert.Greater(lifter.y, 20f);
        }

        [Test]
        public void LaunchClimb_NeverTiltsPastTheCap_SoTheHorizonStaysInFrame()
        {
            var seq = new CameraBeatSequence();
            seq.Begin(CameraBeatKind.Launch, false);
            var start = new BeatPose(new Vector3(-30f, 40f, -30f), Vector3.zero, 36f);
            var pad = new Vector3(20f, 0f, 20f);
            const float h = 3.5f;
            seq.Tick(CameraBeatTuning.Launch.Intro + CameraBeatTuning.Launch.Hold, true);
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                Vector3 lifter = pad + Vector3.up * CameraBeatTuning.ClimbAltitude(t, h);
                BeatPose p = CameraBeatShots.LaunchClimb(
                    CameraBeatShots.LaunchPad(pad, h, Vector3.forward, false), lifter, h, t);
                Vector3 d = p.LookAt - p.Position;
                float pitchUp = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                Assert.LessOrEqual(pitchUp, CameraBeatTuning.LaunchMaxTiltUp + 0.01f, $"t={t}");
            }
        }

        [Test]
        public void CapTilt_LeavesLowLooksAlone_AndNeverLooksStraightUp()
        {
            var from = Vector3.zero;
            var low = new Vector3(10f, 2f, 0f);
            Assert.AreEqual(low, CameraBeatShots.CapTilt(from, low, 30f));
            Vector3 capped = CameraBeatShots.CapTilt(from, new Vector3(10f, 50f, 0f), 30f);
            Assert.AreEqual(10f * Mathf.Tan(30f * Mathf.Deg2Rad), capped.y, 1e-4f);
            Vector3 overhead = CameraBeatShots.CapTilt(from, new Vector3(0f, 50f, 0f), 30f);
            Assert.AreEqual(0f, overhead.y, 1e-4f);
        }

        [Test]
        public void TerrainClear_SeesTheHillBetweenCameraAndLifter()
        {
            float Hill(float x, float z) => Mathf.Abs(x - 5f) < 1f ? 6f : 0f;
            Assert.IsFalse(CameraBeatShots.TerrainClear(new Vector3(0f, 2f, 0f), new Vector3(10f, 2f, 0f), Hill, 0.2f));
            Assert.IsTrue(CameraBeatShots.TerrainClear(new Vector3(0f, 9f, 0f), new Vector3(10f, 9f, 0f), Hill, 0.2f));
        }

        [Test]
        public void LaunchPad_HeadingCandidatesAreDistinctAndStartWithTheDefault()
        {
            var yaws = CameraBeatTuning.LaunchYawCandidates;
            Assert.AreEqual(CameraBeatTuning.LaunchYawDefault, yaws[0]);
            var pad = new Vector3(5f, 0f, 5f);
            var a = CameraBeatShots.LaunchPad(pad, 3f, Vector3.forward, false, yaws[0]);
            var b = CameraBeatShots.LaunchPad(pad, 3f, Vector3.forward, false, yaws[1]);
            Assert.Greater(Vector3.Distance(a.Position, b.Position), 1f);
            Assert.AreEqual(a.LookAt, b.LookAt, "every heading frames the same lifter");
            Assert.AreEqual(Vector3.Distance(a.Position, a.LookAt), Vector3.Distance(b.Position, b.LookAt), 1e-3f);
        }

        [Test]
        public void Victory_ReturnEndsOnThePlayersView()
        {
            var seq = new CameraBeatSequence();
            seq.Begin(CameraBeatKind.Victory, false);
            var p = CameraBeatTuning.Victory;
            seq.Tick(p.Intro + p.Hold + p.Main + p.Return * 0.9999f, true);
            Assert.AreEqual(CameraBeatPhase.Return, seq.Phase);
            var start = new BeatPose(new Vector3(-30f, 40f, -30f), new Vector3(5f, 0f, 5f), 36f);
            BeatPose end = CameraBeatShots.Evaluate(seq, Ctx(start, Vector3.zero, 0f, Vector3.zero));
            Assert.That(Vector3.Distance(end.Position, start.Position), Is.LessThan(0.01f));
        }

        [Test]
        public void VictoryOrbit_PullsBackAndRises()
        {
            BeatPose a = CameraBeatShots.VictoryOrbit(Vector3.zero, Vector3.forward, 0f);
            BeatPose b = CameraBeatShots.VictoryOrbit(Vector3.zero, Vector3.forward, 1f);
            Vector2 fa = new Vector2(a.Position.x, a.Position.z), fb = new Vector2(b.Position.x, b.Position.z);
            Assert.Greater(fb.magnitude, fa.magnitude);
            Assert.Greater(b.Position.y, a.Position.y);
        }

        [Test]
        public void ClimbAltitude_StartsOnThePad_AndOnlyRises()
        {
            Assert.AreEqual(0f, CameraBeatTuning.ClimbAltitude(0f, 3f), 1e-5f);
            float prev = 0f;
            for (int i = 1; i <= 20; i++)
            {
                float a = CameraBeatTuning.ClimbAltitude(i / 20f, 3f);
                Assert.Greater(a, prev);
                prev = a;
            }
        }

        [Test]
        public void FromCamera_OrthoView_KeepsTheSameFramingAtTheFocus()
        {
            var rot = Quaternion.Euler(30f, 45f, 0f);
            Vector3 focus = new Vector3(100f, 0f, 100f);
            Vector3 pos = focus - rot * Vector3.forward * 50f;
            BeatPose pose = CameraBeatShots.FromCamera(pos, rot, true, 18f, 60f, 36f);
            Assert.That(Vector3.Distance(pose.LookAt, focus), Is.LessThan(0.01f));
            float halfHeight = Vector3.Distance(pose.Position, focus) * Mathf.Tan(18f * Mathf.Deg2Rad);
            Assert.AreEqual(18f, halfHeight, 0.01f, "perspective half-height at the focus equals the ortho size");
        }

        [Test]
        public void AboveGround_LiftsTheCameraOverTerrain_IncludingTheRingAroundIt()
        {
            // A ridge 2 m east of the camera, 10 m tall.
            float Ridge(float x, float z) => x > 1f ? 10f : 0f;
            Vector3 p = CameraBeatShots.AboveGround(new Vector3(0f, 3f, 0f), Ridge, 1.5f);
            Assert.AreEqual(11.5f, p.y, 1e-4f);
            Vector3 high = CameraBeatShots.AboveGround(new Vector3(-10f, 30f, 0f), Ridge, 1.5f, 1f);
            Assert.AreEqual(30f, high.y, "never pulls a camera down");
        }

        // ---- director handoff ----------------------------------------------------------------------

        private GameObject _camGo, _target, _director;

        [TearDown]
        public void TearDown()
        {
            if (_director != null) Object.DestroyImmediate(_director);
            if (_target != null) Object.DestroyImmediate(_target);
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            SpecialistStatusDisplay.HideForCapture = false;
        }

        private (Camera cam, IsometricCameraController ctrl, CameraBeatDirector dir) Rig()
        {
            _camGo = new GameObject("BeatTestCam");
            var cam = _camGo.AddComponent<Camera>();
            var ctrl = _camGo.AddComponent<IsometricCameraController>();
            cam.orthographic = true;
            cam.orthographicSize = 17.25f;
            cam.fieldOfView = 47f;
            cam.nearClipPlane = 0.31f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.2f, 0.3f, 0.4f, 1f);
            _camGo.transform.SetPositionAndRotation(new Vector3(170f, 42f, 160f), Quaternion.Euler(30f, 45f, 0f));
            _director = new GameObject("BeatTestDirector");
            return (cam, ctrl, _director.AddComponent<CameraBeatDirector>());
        }

        private static void AssertRestored(Camera cam, CameraSnapshot before)
        {
            Assert.AreEqual(before.Position, cam.transform.position);
            Assert.AreEqual(before.Rotation, cam.transform.rotation);
            Assert.AreEqual(before.Orthographic, cam.orthographic);
            Assert.AreEqual(before.OrthographicSize, cam.orthographicSize);
            Assert.AreEqual(before.FieldOfView, cam.fieldOfView);
            Assert.AreEqual(before.NearClip, cam.nearClipPlane);
            Assert.AreEqual(before.ClearFlags, cam.clearFlags);
            Assert.AreEqual(before.BackgroundColor, cam.backgroundColor);
        }

        [Test]
        public void Snapshot_PutsBackEverythingTheBeatChanges()
        {
            var (cam, _, _) = Rig();
            var before = CameraSnapshot.Capture(cam);
            cam.orthographic = false;
            cam.fieldOfView = 36f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 900f;
            cam.clearFlags = CameraClearFlags.SolidColor; // the beat's sky fallback
            cam.backgroundColor = Color.red;
            cam.transform.SetPositionAndRotation(Vector3.one, Quaternion.Euler(-10f, 3f, 0f));
            before.Apply(cam);
            AssertRestored(cam, before);
            Assert.AreEqual(before.FarClip, cam.farClipPlane);
        }

        [Test]
        public void Director_LaunchTargetDestroyed_RestoresCameraAndHandsOffOnce()
        {
            var (cam, ctrl, dir) = Rig();
            _target = new GameObject("Lifter");
            _target.transform.position = new Vector3(200f, 0f, 192f);
            var before = CameraSnapshot.Capture(cam);
            int ignitions = 0, done = 0;

            Assert.IsTrue(dir.Begin(CameraBeatKind.Launch, cam, ctrl, null, _target.transform, _target.transform.position,
                false, "LIFTOFF", () => ignitions++, () => done++));
            Assert.IsFalse(ctrl.enabled, "the player's camera stands down during the beat");
            Assert.IsFalse(cam.orthographic, "the beat films through a perspective lens");
            Assert.IsTrue(CameraBeatDirector.Active);
            Assert.IsTrue(CameraBeatDirector.HidesHud);
            Assert.IsTrue(CameraBeatDirector.BlocksInput);
            Assert.IsTrue(SpecialistStatusDisplay.HideForCapture, "name tags hide with the HUD");

            dir.Step(CameraBeatTuning.Launch.Intro + 0.1f, false);
            Assert.AreEqual(1, ignitions, "engines light when the hold begins");
            dir.Step(CameraBeatTuning.Launch.Hold + 1f, false);
            Assert.Greater(_target.transform.position.y, 0f, "the lifter climbs during Main");

            Object.DestroyImmediate(_target);
            dir.Step(0.016f, false);

            Assert.IsFalse(CameraBeatDirector.Active);
            Assert.AreEqual(CameraBeatEnd.TargetLost, dir.Sequence.End);
            Assert.AreEqual(1, done, "a lost lifter still departs: no softlock");
            Assert.IsTrue(ctrl.enabled);
            Assert.IsFalse(SpecialistStatusDisplay.HideForCapture);
            AssertRestored(cam, before);

            dir.Step(1f, true);
            Assert.AreEqual(1, done, "handoff never runs twice");
        }

        [Test]
        public void Director_SkippedVictory_RestoresExactly_AndPutsTheLifterBack()
        {
            var (cam, ctrl, dir) = Rig();
            var before = CameraSnapshot.Capture(cam);
            int done = 0;
            dir.Begin(CameraBeatKind.Victory, cam, ctrl, null, null, new Vector3(192f, 0f, 192f), false, "WON",
                null, () => done++);
            dir.Step(0.1f, skip: true);
            Assert.IsTrue(CameraBeatDirector.Active, "a skip inside the grace window is ignored");
            dir.Step(1f, skip: true);

            Assert.AreEqual(CameraBeatEnd.Skipped, dir.Sequence.End);
            Assert.AreEqual(1, done);
            Assert.IsTrue(CameraBeatDirector.BlocksInput, "the skip key is swallowed on the frame it lands");
            AssertRestored(cam, before);

            // A skipped launch also returns the lifter to its pad (for callers that do not leave the world).
            _target = new GameObject("Lifter");
            _target.transform.position = new Vector3(200f, 0.5f, 192f);
            dir.Begin(CameraBeatKind.Launch, cam, ctrl, null, _target.transform, _target.transform.position, false, "",
                null, null);
            dir.Step(CameraBeatTuning.Launch.Intro + CameraBeatTuning.Launch.Hold + 1f, false);
            Assert.Greater(_target.transform.position.y, 0.5f);
            dir.Step(0f, skip: true);
            Assert.AreEqual(0.5f, _target.transform.position.y, 1e-5f);
            AssertRestored(cam, before);
        }

        [Test]
        public void Director_ReducedMotion_UsesTheShortStillProfile()
        {
            var (cam, ctrl, dir) = Rig();
            var before = CameraSnapshot.Capture(cam);
            _target = new GameObject("Lifter");
            dir.Begin(CameraBeatKind.Launch, cam, ctrl, null, _target.transform, Vector3.zero, true, "", null, null);
            Assert.AreSame(CameraBeatTuning.LaunchReduced, dir.Sequence.Profile);
            Vector3 framed = cam.transform.position;
            dir.Step(CameraBeatTuning.LaunchReduced.Total + 0.1f, false);
            Assert.AreEqual(CameraBeatEnd.Completed, dir.Sequence.End);
            Assert.AreNotEqual(before.Position, framed, "it cuts to the still shot");
            AssertRestored(cam, before);
        }

        [Test]
        public void Director_RefusesWithoutACamera()
        {
            _director = new GameObject("BeatTestDirector");
            var dir = _director.AddComponent<CameraBeatDirector>();
            Assert.IsFalse(dir.Begin(CameraBeatKind.Victory, null, null, null, null, Vector3.zero, false, "", null, null));
            Assert.IsFalse(CameraBeatDirector.Active);
        }
    }
}
