using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class CameraMapClampTests
    {
        [Test]
        public void NorthwestPan_KeepsTheGroundFocusOnTheMap()
        {
            Vector3 forward = Quaternion.Euler(30f, 45f, 0f) * Vector3.forward;
            Vector3 cam = new Vector3(-40f, 22f, -40f);
            var min = new Vector2(-IsometricCameraController.MapEdgeMargin, -IsometricCameraController.MapEdgeMargin);
            var max = new Vector2(384f + IsometricCameraController.MapEdgeMargin, 384f + IsometricCameraController.MapEdgeMargin);

            Assert.IsTrue(IsometricCameraController.TryGroundFocus(cam, forward, out Vector3 before));
            Assert.Less(before.x, min.x, "this pose is looking past the NW edge");

            Vector3 clamped = IsometricCameraController.ClampPoseToMap(cam, forward, min, max);
            Assert.IsTrue(IsometricCameraController.TryGroundFocus(clamped, forward, out Vector3 after));
            Assert.GreaterOrEqual(after.x, min.x - 0.05f);
            Assert.GreaterOrEqual(after.z, min.y - 0.05f);
            Assert.LessOrEqual(after.x, max.x + 0.05f);
            Assert.LessOrEqual(after.z, max.y + 0.05f);
        }

        [Test]
        public void FocusAlreadyInside_DoesNotMove()
        {
            Vector3 forward = Quaternion.Euler(30f, 45f, 0f) * Vector3.forward;
            Vector3 focus = new Vector3(192f, 0f, 192f);
            float distance = 40f;
            Vector3 cam = focus - forward * distance;
            var min = new Vector2(-8f, -8f);
            var max = new Vector2(400f, 400f);
            Vector3 clamped = IsometricCameraController.ClampPoseToMap(cam, forward, min, max);
            Assert.AreEqual(cam.x, clamped.x, 0.02f);
            Assert.AreEqual(cam.z, clamped.z, 0.02f);
        }
    }
}
