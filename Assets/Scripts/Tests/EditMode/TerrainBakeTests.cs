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

            Assert.Greater(bake.SampleHeight(mesa.x, mesa.z), 4f, "seeded mesa lip");
            float craterH = bake.SampleHeight(crater.x, crater.z);
            Assert.Less(craterH, -2f, $"signature crater bowl sampled={craterH:0.###}");
            float canyonH = bake.SampleHeight(canyon.x, canyon.z);
            float canyonRaw = TerrainDataBake.Height(canyon.x, canyon.z, 11, CelestialBodyId.Mars);
            Assert.Less(canyonH, -2f, $"seeded canyon sampled={canyonH:0.###} raw={canyonRaw:0.###}");

            // Natural bowl: the steep part of the wall is near the rim (~0.8 R), not mid-floor.
            // West wall — away from the yard-pad blend and clear of the channel to the north.
            Vector3 wall = crater + new Vector3(-0.97f, 0f, -0.24f) * (7.4f * 0.8f);
            int n = bake.Resolution;
            int px = Mathf.RoundToInt(wall.x / bake.WorldWidth * (n - 1));
            int pz = Mathf.RoundToInt(wall.z / bake.WorldHeight * (n - 1));
            Color splat = bake.SplatMap.GetPixel(px, pz);
            Assert.Greater(splat.g, 0.45f, $"crater walls read rock (splat {splat})");
        }

        [Test]
        public void MarsAndLuna_ScatterCraters()
        {
            Assert.Greater(CelestialBodyCatalog.Mars().CraterCount, 0);
            Assert.Greater(CelestialBodyCatalog.Luna().CraterCount, 0);
        }

        [Test]
        public void DifferentSeeds_ChangeFarRelief()
        {
            var a = TerrainDataBake.Generate(384f, 384f, 7, CelestialBodyCatalog.Mars());
            var b = TerrainDataBake.Generate(384f, 384f, 99, CelestialBodyCatalog.Mars());
            Vector3 far = ColonyLayout.CampusOrigin + new Vector3(64f, 0f, 48f);
            float ha = a.SampleHeight(far.x, far.z);
            float hb = b.SampleHeight(far.x, far.z);
            Assert.Greater(Mathf.Abs(ha - hb), 0.05f, "AoE2-style maps must reroll off-campus terrain with the seed");
        }

        [Test]
        public void Luna_CampusPadStaysFlat_AndBowlIsDeep()
        {
            var bake = TerrainDataBake.Generate(384f, 384f, 7, CelestialBodyCatalog.Luna());
            Vector3 origin = ColonyLayout.CampusOrigin;
            Assert.AreEqual(0f, Mathf.Abs(bake.SampleHeight(origin.x, origin.z)), 0.05f);
            Assert.AreEqual(0f, Mathf.Abs(bake.SampleHeight(LaunchSite.PadWorld.x, LaunchSite.PadWorld.z)), 0.08f);
            Vector3 crater = origin + TerrainDataBake.SignatureCraterLocal;
            Assert.Less(bake.SampleHeight(crater.x, crater.z), -2f, "signature Luna bowl");
            float minH = float.MaxValue;
            float maxH = float.MinValue;
            for (int i = 0; i < bake.Heights.Length; i++)
            {
                if (bake.Heights[i] < minH) minH = bake.Heights[i];
                if (bake.Heights[i] > maxH) maxH = bake.Heights[i];
            }
            Assert.Greater(maxH - minH, 2.5f, "Luna height field has AoE2-style relief");

            Color ridge = TerrainDataBake.SplatFor(CelestialBodyId.Luna, 0.9f, 0.1f);
            Assert.Less(ridge.b, 0.12f, "Luna must not lay a grass cap on high flats");
            Assert.Greater(ridge.r, 0.50f, "Luna high flats stay pale ejecta");
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
            Assert.Less(ridge.b, 0.02f, "Mars must not lay a snow/grass cap on high flats");
            Assert.Greater(ridge.r, 0.55f, "Mars high flats stay dusty rust");
            Assert.Less(ridge.a, 0.08f, "high flats are not crater-floor wet");

            Color bowl = TerrainDataBake.SplatFor(CelestialBodyId.Mars, 0.08f, 0.1f);
            Assert.Less(bowl.b, 0.02f, "Mars bowls must not pick up snow/grass");
            Assert.Greater(bowl.r, 0.55f, "Mars bowls stay rust dust, not a cyan wet cap");
            Assert.Less(bowl.a, 0.28f);
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

        [Test]
        public void Europa_HasNoLiquidSurfaceWater()
        {
            var europa = CelestialBodyCatalog.Europa();
            Assert.AreEqual(0, europa.LakeCount, "Europa's surface is frozen");
            Assert.AreEqual(0, europa.RiverCount);
            var bake = TerrainDataBake.Generate(384f, 384f, 7, europa);
            Assert.AreEqual(0, bake.Water.Count);
        }

        [Test]
        public void Earth_LakesFillCarvedBasins()
        {
            var earth = CelestialBodyCatalog.Earth();
            var bake = TerrainDataBake.Generate(384f, 384f, 7, earth);
            int lakes = 0;
            foreach (var w in bake.Water)
            {
                if (!w.IsLake) continue;
                lakes++;
                float floor = bake.SampleHeight(w.Center.x, w.Center.z);
                Assert.Less(floor, w.Center.y - 0.2f, "lake bed sits below its water level");
            }
            Assert.Greater(lakes, 0, "Earth gets lakes in natural low ground");
            Assert.LessOrEqual(lakes, earth.LakeCount);
        }

        [Test]
        public void Luna_FreshCratersBakeBrightEjecta()
        {
            var bake = TerrainDataBake.Generate(384f, 384f, 7, CelestialBodyCatalog.Luna());
            float maxFresh = 0f;
            for (int i = 0; i < bake.Fresh.Length; i++)
                maxFresh = Mathf.Max(maxFresh, bake.Fresh[i]);
            Assert.Greater(maxFresh, 0.5f, "young craters carry bright ejecta in MaskMap.G");
        }

        [Test]
        public void LevelFootprint_GradesAFlatPad()
        {
            var bake = TerrainDataBake.Generate(384f, 384f, 7, CelestialBodyCatalog.Mars());
            Vector3 site = ColonyLayout.CampusOrigin + new Vector3(-30f, 0f, -34f);
            Assert.IsTrue(TerrainDataBake.LevelFootprint(bake, site, new Vector2(3f, 3f), 3f)
                          || Mathf.Abs(bake.SampleHeight(site.x, site.z)) < 0.01f);
            Assert.AreEqual(0f, bake.SampleHeight(site.x, site.z), 0.02f);
            Assert.AreEqual(0f, bake.SampleHeight(site.x + 2.5f, site.z - 2.5f), 0.05f);
        }

        [Test]
        public void Noise_IsSeededAndBounded()
        {
            float a = TerrainNoise.Fbm(12.3f, 45.6f, 7, 4);
            float b = TerrainNoise.Fbm(12.3f, 45.6f, 8, 4);
            Assert.AreNotEqual(a, b, "seed changes the field");
            Assert.AreEqual(a, TerrainNoise.Fbm(12.3f, 45.6f, 7, 4), 1e-6f, "deterministic");
            for (int i = 0; i < 200; i++)
            {
                float v = TerrainNoise.Gradient(i * 0.37f, i * 0.91f, 3);
                Assert.LessOrEqual(Mathf.Abs(v), 1.05f);
            }
        }
    }
}
