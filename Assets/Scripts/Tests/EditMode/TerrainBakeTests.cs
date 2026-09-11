using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class TerrainBakeTests
    {
        [Test]
        public void CampusPad_StaysFlat()
        {
            var bake = TerrainDataBake.Generate(384f, 384f, 7, CelestialBodyCatalog.Mars());
            Vector3 origin = ColonyLayout.CampusOrigin;
            Assert.AreEqual(0f, Mathf.Abs(bake.SampleHeight(origin.x, origin.z)), 0.02f);
            Assert.AreEqual(0f, TerrainDataBake.CampusFlatten(origin.x, origin.z), 0.001f);
            Assert.AreEqual(0f, Mathf.Abs(bake.SampleHeight(LaunchSite.PadWorld.x, LaunchSite.PadWorld.z)), 0.05f);
            Assert.Greater(TerrainDataBake.CampusFlatten(origin.x + 80f, origin.z + 80f), 0.85f);
        }

        [Test]
        public void StillFarThird_HasReliefOutsideYards()
        {
            var bake = TerrainDataBake.Generate(384f, 384f, 7, CelestialBodyCatalog.Mars());
            Vector3 origin = ColonyLayout.CampusOrigin;
            float north = bake.SampleHeight(origin.x, origin.z + 20f);
            Assert.Greater(Mathf.Abs(north), 1.5f, "20 m north of Commons is inside the still, outside yard pads");
            Assert.Greater(TerrainDataBake.CampusFlatten(origin.x, origin.z + 20f), 0.85f);
        }

        [Test]
        public void Mars_MesaIsHigh_CanyonIsDeep()
        {
            var bake = TerrainDataBake.Generate(384f, 384f, 11, CelestialBodyCatalog.Mars());
            Vector3 origin = ColonyLayout.CampusOrigin;
            Vector3 mesa = origin + TerrainDataBake.MesaLipLocal;
            Vector3 crater = origin + TerrainDataBake.SignatureCraterLocal;
            Vector3 canyon = origin + TerrainDataBake.CanyonLocal;

            Assert.Greater(bake.SampleHeight(mesa.x, mesa.z), 4f, "DEM mesa lip");
            float craterH = bake.SampleHeight(crater.x, crater.z);
            Assert.Less(craterH, -2f, $"DEM crater bowl sampled={craterH:0.###}");
            float canyonH = bake.SampleHeight(canyon.x, canyon.z);
            float canyonRaw = TerrainDataBake.Height(canyon.x, canyon.z, 11, CelestialBodyId.Mars);
            Assert.Less(canyonH, -2f, $"DEM canyon sampled={canyonH:0.###} raw={canyonRaw:0.###}");

            Vector3 wall = crater + new Vector3(0.53f, 0f, -0.85f) * 3.2f;
            const float e = 1.6f;
            float hL = bake.SampleHeight(wall.x - e, wall.z);
            float hR = bake.SampleHeight(wall.x + e, wall.z);
            float hD = bake.SampleHeight(wall.x, wall.z - e);
            float hU = bake.SampleHeight(wall.x, wall.z + e);
            Vector3 n = new Vector3(hL - hR, e * 2f, hD - hU).normalized;
            float slope = 1f - Mathf.Clamp01(n.y);
            float h01 = Mathf.InverseLerp(-bake.Amplitude, bake.Amplitude, bake.SampleHeight(wall.x, wall.z));
            Color splat = TerrainDataBake.SplatFor(CelestialBodyId.Mars, h01, slope);
            Assert.Greater(splat.g, 0.45f, "crater / canyon walls read rock");
        }

        [Test]
        public void Mars_HasNoCraterMeshes()
        {
            Assert.AreEqual(0, CelestialBodyCatalog.Mars().CraterCount);
        }

        [Test]
        public void Bake_EmitsTdbStyleMaps()
        {
            var bake = TerrainDataBake.Generate(128f, 128f, 3, CelestialBodyCatalog.Earth());
            Assert.AreEqual(TerrainDataBake.MapResolution, bake.Resolution);
            Assert.IsNotNull(bake.HeightMap);
            Assert.IsNotNull(bake.NormalMap);
            Assert.IsNotNull(bake.SplatMap);
            Assert.IsNotNull(bake.MaskMap);
            Assert.AreEqual(CelestialBodyId.Earth, bake.BodyId);

            Color n = bake.NormalMap.GetPixel(64, 64);
            Assert.AreEqual(1f, n.g, 0.02f, "TDB world-space packing stores 1 in G");
            float nx = n.r * 2f - 1f;
            float nz = n.b * 2f - 1f;
            float ny = Mathf.Sqrt(Mathf.Clamp01(1f - nx * nx - nz * nz));
            Assert.Greater(ny, 0.2f);
        }

        [Test]
        public void SplatWeights_SumToOne()
        {
            Color s = TerrainDataBake.SplatFor(CelestialBodyId.Earth, 0.5f, 0.1f);
            Assert.AreEqual(1f, s.r + s.g + s.b + s.a, 0.02f);
            Assert.Greater(s.b, 0.3f, "Earth flats should carry grass");

            Color rock = TerrainDataBake.SplatFor(CelestialBodyId.Mars, 0.6f, 0.9f);
            Assert.AreEqual(1f, rock.r + rock.g + rock.b + rock.a, 0.02f);
            Assert.Greater(rock.g, 0.45f, "steep Mars should read rock");

            Color ridge = TerrainDataBake.SplatFor(CelestialBodyId.Mars, 0.9f, 0.1f);
            Assert.Less(ridge.b, 0.12f, "Mars must not lay a snow/grass cap on high flats");
            Assert.Greater(ridge.r, 0.55f, "Mars high flats stay dusty sand");
        }

        [Test]
        public void Mesh_CampusVertexOnGradeZero()
        {
            var bake = TerrainDataBake.Generate(384f, 384f, 11, CelestialBodyCatalog.Mars());
            var mesh = TerrainMeshBuilder.Build(bake);
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 1000);
            Vector3 origin = ColonyLayout.CampusOrigin;
            float closestY = 99f;
            float closest = 999f;
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                float dx = verts[i].x - origin.x;
                float dz = verts[i].z - origin.z;
                float d = dx * dx + dz * dz;
                if (d < closest)
                {
                    closest = d;
                    closestY = verts[i].y;
                }
            }
            Assert.Less(closest, 9f, "a vertex should sit near the campus origin");
            Assert.AreEqual(0f, closestY, 0.05f);
        }
    }
}
