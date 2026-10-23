using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>Controller stick, trigger and shoulder-button shaping (pure math).</summary>
    public class GamepadMathTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void StickDeadzone_SwallowsDrift()
        {
            var v = GamepadMath.Stick(0.12f, -0.1f);
            Assert.AreEqual(0f, v.x, Eps);
            Assert.AreEqual(0f, v.y, Eps);
            Assert.AreEqual(0f, GamepadMath.Axis(0.19f), Eps);
            Assert.IsFalse(GamepadMath.IsDeflected(0.1f, 0.1f));
            Assert.IsTrue(GamepadMath.IsDeflected(0.5f, 0f));
        }

        [Test]
        public void StickCurve_IsContinuousFromTheDeadzoneEdge_AndFullAtFullTilt()
        {
            Assert.AreEqual(0f, GamepadMath.Axis(GamepadMath.StickDeadzone + 1e-5f), 0.001f);
            Assert.AreEqual(1f, GamepadMath.Axis(1f), Eps);
            Assert.AreEqual(-1f, GamepadMath.Axis(-1f), Eps);
            // Half the live travel gives less than half speed (fine control near centre).
            float half = GamepadMath.StickDeadzone + (1f - GamepadMath.StickDeadzone) * 0.5f;
            Assert.Less(GamepadMath.Axis(half), 0.5f);
            Assert.Greater(GamepadMath.Axis(half), 0f);
            float prev = 0f;
            for (int i = 0; i <= 50; i++)
            {
                float v = GamepadMath.Axis(i / 50f);
                Assert.GreaterOrEqual(v, prev - Eps);
                prev = v;
            }
        }

        [Test]
        public void StickDiagonal_IsNotFasterThanStraight_AndKeepsDirection()
        {
            var straight = GamepadMath.Stick(1f, 0f);
            var diag = GamepadMath.Stick(1f, 1f); // raw corner, length 1.41
            float len = Mathf.Sqrt(diag.x * diag.x + diag.y * diag.y);
            Assert.AreEqual(1f, straight.x, Eps);
            Assert.AreEqual(1f, len, Eps);
            Assert.AreEqual(diag.x, diag.y, Eps);
            var down = GamepadMath.Stick(0f, -0.9f);
            Assert.AreEqual(0f, down.x, Eps);
            Assert.Less(down.y, 0f);
        }

        [Test]
        public void ZoomDrive_TriggersAndStick_MatchTheKeySigns()
        {
            Assert.AreEqual(0f, GamepadMath.Zoom(0f, 0f, 0f), Eps);
            Assert.AreEqual(0f, GamepadMath.Zoom(0.1f, 0.05f, 0.05f), Eps);
            Assert.Greater(GamepadMath.Zoom(0f, 1f, 0f), 0.99f, "LT zooms out, like Q");
            Assert.Less(GamepadMath.Zoom(0f, 0f, 1f), -0.99f, "RT zooms in, like E");
            Assert.Less(GamepadMath.Zoom(1f, 0f, 0f), -0.99f, "stick up zooms in");
            Assert.Greater(GamepadMath.Zoom(-1f, 0f, 0f), 0.99f, "stick down zooms out");
            Assert.AreEqual(0f, GamepadMath.Zoom(0f, 1f, 1f), Eps, "both triggers cancel");
            Assert.AreEqual(1f, GamepadMath.Zoom(-1f, 1f, 0f), Eps, "clamped");
        }

        [Test]
        public void ShoulderButtons_StepSpeedOnce()
        {
            Assert.AreEqual(0, GamepadMath.SpeedStep(false, false));
            Assert.AreEqual(1, GamepadMath.SpeedStep(false, true));
            Assert.AreEqual(-1, GamepadMath.SpeedStep(true, false));
            Assert.AreEqual(0, GamepadMath.SpeedStep(true, true));
        }
    }
}
