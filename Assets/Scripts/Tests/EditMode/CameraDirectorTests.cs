using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>The camera rig hands a scripted beat its pose and gets exactly its own back.</summary>
    public class CameraDirectorTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private IsometricCameraController MakeRig(out Camera cam)
        {
            _go = new GameObject("CameraDirectorTestRig");
            cam = _go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 17.5f;
            _go.transform.SetPositionAndRotation(new Vector3(3f, 40f, -7f), Quaternion.Euler(30f, 45f, 0f));
            return _go.AddComponent<IsometricCameraController>();
        }

        [Test]
        public void DirectedPose_IsUndoneExactly()
        {
            var rig = MakeRig(out var cam);
            Vector3 pos = _go.transform.position;
            Quaternion rot = _go.transform.rotation;

            Assert.IsTrue(rig.BeginDirected());
            Assert.IsTrue(rig.IsDirected);
            Assert.IsFalse(rig.BeginDirected(), "one beat at a time");

            rig.ApplyDirectedPose(new Vector3(120f, 90f, 60f), Quaternion.Euler(35f, 120f, 0f), 31f);
            Assert.AreEqual(31f, cam.orthographicSize, 1e-4f);
            Assert.AreEqual(120f, _go.transform.position.x, 1e-3f);

            rig.EndDirected();
            Assert.IsFalse(rig.IsDirected);
            Assert.AreEqual(17.5f, cam.orthographicSize);
            Assert.AreEqual(pos.x, _go.transform.position.x, 1e-5f);
            Assert.AreEqual(pos.y, _go.transform.position.y, 1e-5f);
            Assert.AreEqual(pos.z, _go.transform.position.z, 1e-5f);
            Assert.AreEqual(rot.x, _go.transform.rotation.x, 1e-5f);
            Assert.AreEqual(rot.y, _go.transform.rotation.y, 1e-5f);
            Assert.AreEqual(rot.z, _go.transform.rotation.z, 1e-5f);
            Assert.AreEqual(rot.w, _go.transform.rotation.w, 1e-5f);
        }

        [Test]
        public void DirectedPose_IsIgnoredWhenNoBeatOwnsTheRig()
        {
            var rig = MakeRig(out var cam);
            rig.ApplyDirectedPose(new Vector3(120f, 90f, 60f), Quaternion.identity, 31f);
            Assert.AreEqual(17.5f, cam.orthographicSize);
            Assert.AreEqual(3f, _go.transform.position.x);
            rig.EndDirected(); // harmless when nothing began
            Assert.AreEqual(17.5f, cam.orthographicSize);
        }

        [Test]
        public void HardSnap_EndsABeatFirst()
        {
            var rig = MakeRig(out _);
            Assert.IsTrue(rig.BeginDirected());
            rig.SnapToTarget();
            Assert.IsFalse(rig.IsDirected);
        }

        [Test]
        public void Beats_NeverRunOutsidePlayMode_OrWithoutARig()
        {
            Assert.IsFalse(CameraDirector.TryPlayVictory(null, Vector3.zero));
            Assert.IsFalse(CameraDirector.TryPlayLaunch(null, Vector3.zero, null));
            Assert.IsFalse(CameraDirector.IsPlaying(null));

            var rig = MakeRig(out _);
            Assert.IsFalse(CameraDirector.TryPlayVictory(rig, Vector3.zero), "edit mode / batch is a no-op");
            Assert.IsFalse(CameraDirector.TryPlayLaunch(rig, Vector3.zero, null));
            Assert.IsFalse(rig.IsDirected);
            Assert.IsNull(rig.GetComponent<CameraDirector>());
        }
    }
}
