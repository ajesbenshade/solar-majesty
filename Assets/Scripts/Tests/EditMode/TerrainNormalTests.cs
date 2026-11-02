using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>Graded pads recompute normals locally from the height grid instead of the whole mesh.</summary>
    public class TerrainNormalTests
    {
        private static Vector3[] Grid(int n, System.Func<int, int, float> height)
        {
            var v = new Vector3[n * n];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                v[j * n + i] = new Vector3(i, height(i, j), j);
            return v;
        }

        [Test]
        public void FlatGround_PointsStraightUp()
        {
            var v = Grid(5, (i, j) => 2f);
            for (int j = 0; j < 5; j++)
            for (int i = 0; i < 5; i++)
                Assert.That(Vector3.Angle(TerrainMeshBuilder.GridNormal(v, 5, i, j, 1f, 1f), Vector3.up), Is.LessThan(0.01f));
        }

        [Test]
        public void SlopeRisingInX_TiltsTowardMinusX()
        {
            var v = Grid(5, (i, j) => i * 1f); // 45° ramp
            Vector3 n = TerrainMeshBuilder.GridNormal(v, 5, 2, 2, 1f, 1f);
            Assert.That(n.x, Is.LessThan(0f));
            Assert.That(Mathf.Abs(n.z), Is.LessThan(1e-5f));
            Assert.That(Vector3.Angle(n, Vector3.up), Is.EqualTo(45f).Within(0.1f));
        }

        [Test]
        public void Border_UsesOneSidedDifference()
        {
            var v = Grid(5, (i, j) => j * 0.5f);
            Vector3 edge = TerrainMeshBuilder.GridNormal(v, 5, 0, 0, 1f, 1f);
            Vector3 inner = TerrainMeshBuilder.GridNormal(v, 5, 2, 2, 1f, 1f);
            Assert.That(Vector3.Angle(edge, inner), Is.LessThan(0.01f), "a plane has one normal everywhere");
        }
    }
}
