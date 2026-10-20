using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// The real pooled 3D player and DemoAudio routing, against a camera at the default overseer
    /// pose (22 m up, pitch 30, ortho 18). EditMode covers the maths; this covers the wiring.
    /// </summary>
    public class SpatialAudioPlayTests
    {
        private GameObject _camera;
        private float _master;
        private float _sfx;

        [SetUp]
        public void SetUp()
        {
            _master = DemoSettings.Master;
            _sfx = DemoSettings.Sfx;
            DemoSettings.Master = 1f;
            DemoSettings.Sfx = 1f;
            SoundBus.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            DemoSettings.Master = _master;
            DemoSettings.Sfx = _sfx;
            if (_camera != null) Object.Destroy(_camera);
            foreach (string name in new[] { "SM_SpatialAudio", "DemoAudio" })
            {
                var go = GameObject.Find(name);
                if (go != null) Object.Destroy(go);
            }
        }

        private Camera MakeOverseerCamera(out Vector3 focus)
        {
            _camera = new GameObject("SpatialAudioTestCamera") { tag = "MainCamera" };
            var cam = _camera.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ColonyLayout.CameraOrthoSize;
            _camera.transform.SetPositionAndRotation(new Vector3(0f, 22f, 0f), Quaternion.Euler(30f, 45f, 0f));
            Assume.That(Camera.main, Is.EqualTo(cam), "another MainCamera is in the test scene");
            Assert.IsTrue(IsometricCameraController.TryGroundFocus(
                _camera.transform.position, _camera.transform.forward, out focus));
            return cam;
        }

        private static AudioSource[] Voices()
        {
            var go = GameObject.Find("SM_SpatialAudio");
            Assert.IsNotNull(go, "SpatialAudio pool");
            return go.GetComponentsInChildren<AudioSource>(true);
        }

        [UnityTest]
        public IEnumerator Voices_AreLevelledByGroundDistanceFromScreenCentre()
        {
            Camera cam = MakeOverseerCamera(out Vector3 focus);
            yield return null;

            Vector3 right = cam.transform.right;
            right.y = 0f;
            right.Normalize();
            var r = SfxRouting.ComputeRolloff(cam.orthographicSize, cam.aspect);
            var centre = AudioClip.Create("test_centre", 22050, 1, 44100, false);
            var edge = AudioClip.Create("test_edge", 22050, 1, 44100, false);
            var far = AudioClip.Create("test_far", 22050, 1, 44100, false);

            SpatialAudio.PlayAt(centre, focus, 0.8f, SoundChannel.Sfx, 0f);
            SpatialAudio.PlayAt(edge, focus + right * r.Edge, 0.8f, SoundChannel.Sfx, 0f);
            SpatialAudio.PlayAt(far, focus + right * (r.Far + 20f), 0.8f, SoundChannel.Sfx, 0f);

            AudioSource[] voices = Voices();
            float bus = SoundBus.Volume(SoundChannel.Sfx);
            AudioSource a = voices.First(s => s.clip == centre);
            AudioSource b = voices.First(s => s.clip == edge);
            Assert.AreEqual(0.8f * bus, a.volume, 1e-3f, "screen centre plays at the 2D level");
            Assert.AreEqual(0.8f * bus * SfxRouting.EdgeGain, b.volume, 1e-3f, "screen edge");
            Assert.IsFalse(voices.Any(s => s.clip == far), "an inaudible sound must not take a voice");

            Assert.AreEqual(1f, a.spatialBlend, "still panned in 3D");
            Assert.AreEqual(AudioRolloffMode.Custom, a.rolloffMode);
            Assert.AreEqual(1f, a.GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(0.9f), 1e-4f,
                "Unity's own distance attenuation is flat; SfxRouting sets the level");

            Object.Destroy(centre);
            Object.Destroy(edge);
            Object.Destroy(far);
        }

        [UnityTest]
        public IEnumerator DemoAudio_WorldEventTakesA3DVoice_ColonyEventStays2D()
        {
            MakeOverseerCamera(out Vector3 focus);
            yield return null;

            DemoAudio.PlayExtract(focus);
            DemoAudio.PlayResearch();
            yield return null;

            AudioSource[] voices = Voices();
            Assert.IsTrue(voices.Any(s => s.clip != null && s.clip.name.StartsWith("sfx_extract_")),
                "extract at a position plays through SpatialAudio with an authored take");
            Assert.IsFalse(voices.Any(s => s.clip != null && s.clip.name.StartsWith("sfx_research_")),
                "research is colony-wide and stays 2D");
        }
    }
}
