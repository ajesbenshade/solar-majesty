using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class GroundCoverTests
    {
        [Test]
        public void EveryWorld_HasUndergrowthLandmarksAndOutcrops()
        {
            foreach (var id in CelestialBodyCatalog.All)
            {
                var body = CelestialBodyCatalog.Get(id);
                Assert.IsNotNull(body.GroundCover, id.ToString());
                Assert.Greater(body.GroundCover.Length, 2, $"{id} ground cover layers");
                Assert.Greater(body.PoiCount, 0, $"{id} points of interest");
                Assert.Greater(body.RockFormationCount, 0, $"{id} rock formations");
                foreach (var layer in body.GroundCover)
                {
                    Assert.Greater(layer.Density, 0f, $"{id} {layer.Kind} density");
                    Assert.LessOrEqual(layer.ScaleMin, layer.ScaleMax, $"{id} {layer.Kind} scale range");
                }
            }
        }

        [Test]
        public void OnlyEarth_GrowsPlants()
        {
            var plants = new[]
            {
                GroundCoverKind.GrassTuft, GroundCoverKind.TallGrass, GroundCoverKind.Flowers, GroundCoverKind.Bush,
                GroundCoverKind.Fern, GroundCoverKind.Mushrooms, GroundCoverKind.Reeds
            };
            foreach (var id in CelestialBodyCatalog.All)
            {
                if (id == CelestialBodyId.Earth) continue;
                foreach (var layer in CelestialBodyCatalog.Get(id).GroundCover)
                    Assert.IsFalse(System.Array.IndexOf(plants, layer.Kind) >= 0, $"{id} grows {layer.Kind}");
            }
        }

        [Test]
        public void Layer_WelcomeFollowsSplatAffinity()
        {
            var reeds = GroundCoverLayer.Of(GroundCoverKind.Reeds, 1f, 1f, 1f, Color.green, Color.green,
                new Vector4(0f, 0f, 0f, 1f));
            Assert.AreEqual(1f, reeds.Welcome(new Color(0f, 0f, 0f, 1f)), 1e-4f, "wet ground");
            Assert.AreEqual(0f, reeds.Welcome(new Color(1f, 0f, 0f, 0f)), 1e-4f, "dry dust");
            Assert.AreEqual(0.5f, reeds.Welcome(new Color(0.5f, 0f, 0f, 0.5f)), 1e-4f, "half wet");
        }

        [Test]
        public void Layer_PatchIsBareBelowThresholdAndFullAbove()
        {
            var patchy = GroundCoverLayer.Of(GroundCoverKind.Flowers, 1f, 1f, 1f, Color.red, Color.red,
                Vector4.one, clusterScale: 8f, clusterThreshold: 0.6f);
            Assert.AreEqual(0f, patchy.Patch(0.3f), 1e-4f);
            Assert.AreEqual(1f, patchy.Patch(0.9f), 1e-4f);
            var even = GroundCoverLayer.Of(GroundCoverKind.GrassTuft, 1f, 1f, 1f, Color.green, Color.green, Vector4.one);
            Assert.AreEqual(1f, even.Patch(0f), 1e-4f, "unclustered layers cover everywhere");
        }

        [Test]
        public void EveryKind_BuildsAMesh()
        {
            foreach (GroundCoverKind kind in System.Enum.GetValues(typeof(GroundCoverKind)))
            {
                var mesh = GroundCoverMeshes.Get(kind);
                Assert.IsNotNull(mesh, kind.ToString());
                Assert.Greater(mesh.bounds.size.y, 0.01f, $"{kind} has height");
                Assert.Less(mesh.bounds.size.magnitude, 4f, $"{kind} is about a metre (scale sets the size)");
            }
        }

        [Test]
        public void ClearRect_RemovesOnlyInstancesInsideTheFootprint()
        {
            var go = new GameObject("CoverTest");
            try
            {
                var cover = go.AddComponent<GroundCover>();
                for (int x = 0; x < 20; x++)
                for (int z = 0; z < 20; z++)
                    cover.Add(GroundCoverKind.GrassTuft, new Vector3(100f + x, 0f, 100f + z), Quaternion.identity,
                        Vector3.one * 0.5f, Color.green, false);
                Assert.AreEqual(400, cover.InstanceCount);

                // A 4 x 4 m building footprint centred on (105.5, 105.5) covers x,z in 104..107.
                int removed = cover.ClearRect(new Vector3(105.5f, 0f, 105.5f), 2f, 2f);
                Assert.AreEqual(16, removed);
                Assert.AreEqual(384, cover.InstanceCount);
                Assert.AreEqual(0, cover.ClearRect(new Vector3(105.5f, 0f, 105.5f), 2f, 2f), "already cleared");

                int disc = cover.ClearDisc(new Vector3(115f, 0f, 115f), 1.01f);
                Assert.AreEqual(5, disc, "centre plus four neighbours within 1 m");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TerrainBake_ExposesSplatForScatter()
        {
            var bake = TerrainDataBake.Generate(384f, 384f, 5, CelestialBodyCatalog.Earth());
            Assert.IsNotNull(bake.Splat);
            Assert.AreEqual(bake.Resolution * bake.Resolution, bake.Splat.Length);
            Color s = bake.SampleSplat(200f, 200f);
            Assert.AreEqual(1f, s.r + s.g + s.b + s.a, 0.02f, "splat weights sum to one");
            Assert.Greater(bake.SampleNormal(200f, 200f).y, 0f, "normals point up");
        }
    }
}
