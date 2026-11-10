using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>The terrain kits of generated worlds: lava channels, a sea with joined islands, dunes.</summary>
    public class TerrainKitTests
    {
        private const float World = 384f;

        private static CelestialBodyProfile With(CelestialBodyId sol, TerrainKit kit)
        {
            CelestialBodyProfile p = CelestialBodyCatalog.Get(sol).Copy();
            p.Kit = kit;
            return p;
        }

        [Test]
        public void SolWorlds_HaveNoKitFeatures()
        {
            TerrainBake mars = TerrainDataBake.Generate(World, World, 7, CelestialBodyCatalog.Mars());
            Assert.IsNull(mars.Lava);
            Assert.IsNull(mars.LooseSand);
            Assert.IsFalse(mars.HasSea);
            Assert.IsEmpty(mars.LavaFlows);
        }

        [Test]
        public void Volcanic_CutsLavaChannels_AwayFromTheColony()
        {
            foreach (int seed in new[] { 3, 11, 4242 })
            {
                CelestialBodyProfile body = With(CelestialBodyId.Mars, TerrainKit.Volcanic);
                TerrainBake bake = TerrainDataBake.Generate(World, World, seed, body);
                TerrainBake plain = TerrainDataBake.Generate(World, World, seed, CelestialBodyCatalog.Mars());
                Assert.AreEqual(CelestialBodyId.Mars, bake.BodyId, "it still wears Mars's ground");
                Assert.IsNotNull(bake.Lava);
                Assert.GreaterOrEqual(bake.LavaFlows.Count, 2, $"seed {seed}");

                int n = bake.Resolution, hot = 0;
                for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int i = z * n + x;
                    float lava = bake.Lava[i];
                    Assert.That(lava, Is.InRange(0f, 1f));
                    if (lava <= TerrainDataBake.LavaEdge) continue;
                    hot++;
                    float wx = x * World / (n - 1), wz = z * World / (n - 1);
                    float home = Vector2.Distance(new Vector2(wx, wz),
                        new Vector2(ColonyLayout.CampusOrigin.x, ColonyLayout.CampusOrigin.z));
                    Assert.Greater(home, TerrainDataBake.HomeRadius, $"seed {seed}: lava at {wx:F0},{wz:F0}");
                    Assert.Less(bake.Heights[i], plain.Heights[i], "lava lies in a carved channel");
                }
                Assert.Greater(hot, 400, $"seed {seed}: there is lava to see");
                Assert.Less(hot, n * n / 12, $"seed {seed}: and it is channels, not a lake of it");

                foreach (TerrainRiver flow in bake.LavaFlows)
                {
                    Assert.GreaterOrEqual(flow.Points.Count, 4);
                    Assert.AreEqual(flow.Points.Count, flow.HalfWidths.Count);
                    Vector3 mid = flow.Points[flow.Points.Count / 2];
                    Assert.Greater(bake.Sample(bake.Lava, mid.x, mid.z), TerrainDataBake.LavaEdge, "the ribbon runs where the lava is");
                }
                Assert.AreEqual(0f, bake.SampleHeight(ColonyLayout.CampusOrigin.x, ColonyLayout.CampusOrigin.z), 0.01f);
            }
        }

        [Test]
        public void Archipelago_IsOneLandmass_WithTheColonyOnIt()
        {
            foreach (int seed in new[] { 1, 9, 77, 31337, 900001 })
            {
                CelestialBodyProfile body = With(CelestialBodyId.Earth, TerrainKit.Archipelago);
                TerrainBake bake = TerrainDataBake.Generate(World, World, seed, body);
                Assert.IsTrue(bake.HasSea);
                int c = TerrainDataBake.SeaCellsPerSide;
                Assert.AreEqual(c * c, bake.SeaCells.Length);

                int sea = 0;
                for (int i = 0; i < bake.SeaCells.Length; i++) if (bake.SeaCells[i]) sea++;
                float share = sea / (float)(c * c);
                int under = 0;
                for (int i = 0; i < bake.Heights.Length; i++) if (bake.Heights[i] < TerrainDataBake.SeaLevel) under++;
                Assert.That(share, Is.InRange(0.12f, 0.8f), $"seed {seed}: a sea with land in it (min {bake.MinHeight:F2}, max {bake.MaxHeight:F2}, texels under {under})");

                // Flood the walkable cells from the Commons: every one must be reached.
                float cell = World / c;
                int home = Mathf.FloorToInt(ColonyLayout.CampusOrigin.z / cell) * c + Mathf.FloorToInt(ColonyLayout.CampusOrigin.x / cell);
                Assert.IsFalse(bake.SeaCells[home], "the Commons stands on land");
                var seen = new HashSet<int> { home };
                var open = new Queue<int>();
                open.Enqueue(home);
                while (open.Count > 0)
                {
                    int k = open.Dequeue();
                    int cx = k % c, cz = k / c;
                    foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = cx + dx, nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= c || nz >= c) continue;
                        int nk = nz * c + nx;
                        if (bake.SeaCells[nk] || !seen.Add(nk)) continue;
                        open.Enqueue(nk);
                    }
                }
                Assert.AreEqual(c * c - sea, seen.Count, $"seed {seed}: no island is cut off");

                // The colony's own ground is dry: Commons, second campus, pad and the village rings.
                foreach (Vector3 at in new[] { ColonyLayout.CampusOrigin, ColonyLayout.CampusBOrigin, LaunchSite.PadWorld, ColonyLayout.PartySpawn })
                    Assert.GreaterOrEqual(bake.SampleHeight(at.x, at.z), TerrainDataBake.SeaLevel, $"seed {seed}: {at}");
                for (int a = 0; a < 16; a++)
                {
                    float ang = a * Mathf.PI / 8f;
                    Vector3 ring = ColonyLayout.CampusOrigin + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 46f;
                    Assert.GreaterOrEqual(bake.SampleHeight(ring.x, ring.z), TerrainDataBake.SeaLevel, $"seed {seed}: village ring");
                }
                Assert.AreEqual(0f, bake.SampleHeight(ColonyLayout.CampusOrigin.x, ColonyLayout.CampusOrigin.z), 0.01f);
                Assert.IsEmpty(bake.Water, "the sea replaces lakes and rivers");
            }
        }

        [Test]
        public void DuneSea_MarksLooseSand_OnTheDunesOnly()
        {
            CelestialBodyProfile body = With(CelestialBodyId.Mars, TerrainKit.DuneSea);
            TerrainBake bake = TerrainDataBake.Generate(World, World, 21, body);
            Assert.IsNotNull(bake.LooseSand);
            Assert.IsNull(bake.Lava);
            int n = bake.Resolution, loose = 0, firm = 0;
            for (int i = 0; i < bake.LooseSand.Length; i++)
            {
                Assert.That(bake.LooseSand[i], Is.InRange(0f, 1f));
                if (bake.LooseSand[i] > 0.6f) loose++;
                if (bake.LooseSand[i] < 0.05f) firm++;
            }
            Assert.Greater(loose, n * n / 12, "the dunes are there");
            Assert.Greater(firm, n * n / 5, "and so are firm lanes between them");
            Assert.AreEqual(0f, bake.Sample(bake.LooseSand, ColonyLayout.CampusOrigin.x, ColonyLayout.CampusOrigin.z), 0.001f);
            Assert.AreEqual(0f, bake.Sample(bake.LooseSand, ColonyLayout.CampusOrigin.x + 30f, ColonyLayout.CampusOrigin.z), 0.001f,
                "the colony's ground is firm");
            Assert.Greater(bake.MaxHeight - bake.MinHeight, 3f, "dunes have height");
            Assert.AreEqual(0f, bake.SampleHeight(ColonyLayout.CampusOrigin.x, ColonyLayout.CampusOrigin.z), 0.01f);

            TerrainBake again = TerrainDataBake.Generate(World, World, 21, body);
            CollectionAssert.AreEqual(bake.Heights, again.Heights, "same seed, same dunes");
        }

        [Test]
        public void Archetypes_GetTheirKits()
        {
            int dunes = 0, plainDeserts = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                foreach (SystemBody body in StarSystemGenerator.Generate(seed).Bodies)
                {
                    string where = $"seed {seed} {body.Archetype}";
                    switch (body.Archetype)
                    {
                        case WorldArchetype.Volcanic:
                            Assert.AreEqual(TerrainKit.Volcanic, body.Profile.Kit, where);
                            break;
                        case WorldArchetype.OceanArchipelago:
                            Assert.AreEqual(TerrainKit.Archipelago, body.Profile.Kit, where);
                            break;
                        case WorldArchetype.RockyDesert:
                            if (body.Profile.Kit == TerrainKit.DuneSea) dunes++;
                            else
                            {
                                Assert.AreEqual(TerrainKit.Standard, body.Profile.Kit, where);
                                plainDeserts++;
                            }
                            break;
                        case WorldArchetype.GardenWorld:
                        case WorldArchetype.TidallyLocked:
                        case WorldArchetype.GasGiantMoon:
                            Assert.AreEqual(TerrainKit.Standard, body.Profile.Kit, where);
                            break;
                    }
                    string note = WorldArchetypes.KitNote(body.Profile.Kit);
                    if (note.Length > 0) StringAssert.Contains(note, body.Profile.Briefing, where);
                }
            }
            Assert.Greater(dunes, 20, "some deserts are dune seas");
            Assert.Greater(plainDeserts, 20, "and some are not");
            Assert.IsEmpty(WorldArchetypes.KitNote(TerrainKit.Standard));
        }
    }
}
