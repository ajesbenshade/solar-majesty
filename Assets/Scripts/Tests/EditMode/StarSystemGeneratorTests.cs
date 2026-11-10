using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>Generated star systems: seed codes, the generator and the world archetypes.</summary>
    public class StarSystemGeneratorTests
    {
        [SetUp]
        public void SetUp() => CelestialBodyCatalog.ClearRegistered();

        [TearDown]
        public void TearDown() => CelestialBodyCatalog.ClearRegistered();

        [Test]
        public void SeedCode_RoundTrips_AndForgivesHowItIsTyped()
        {
            foreach (int seed in new[] { 0, 1, -1, 424242, int.MaxValue, int.MinValue, 987654321 })
            {
                string code = SystemSeedCode.Format(seed);
                Assert.AreEqual(8, code.Length, code);
                Assert.IsTrue(SystemSeedCode.TryParse(code, out int back), code);
                Assert.AreEqual(seed, back, code);
                Assert.IsTrue(SystemSeedCode.TryParse(code.ToLowerInvariant().Replace("-", " "), out back));
                Assert.AreEqual(seed, back);
            }

            Assert.IsTrue(SystemSeedCode.TryParse("OIL0-000", out int typed), "O reads as 0, I and L as 1");
            Assert.IsTrue(SystemSeedCode.TryParse("0110-000", out int meant));
            Assert.AreEqual(meant, typed);

            Assert.IsFalse(SystemSeedCode.TryParse("", out _));
            Assert.IsFalse(SystemSeedCode.TryParse("ABC", out _));
            Assert.IsFalse(SystemSeedCode.TryParse("ABCD-EFGH", out _), "eight characters");
            Assert.IsFalse(SystemSeedCode.TryParse("ZZZZ-ZZZ", out _), "more than 32 bits");
            Assert.IsFalse(SystemSeedCode.TryParse("AB!D-EFG", out _));

            Assert.AreEqual(SystemSeedCode.FromText("My Homeworld"), SystemSeedCode.FromText(" my homeworld "));
            Assert.AreNotEqual(SystemSeedCode.FromText("my homeworld"), SystemSeedCode.FromText("my homeworlds"));
            Assert.AreEqual(424242, SystemSeedCode.FromText(SystemSeedCode.Format(424242)), "a code is read as a code");
        }

        [Test]
        public void SameSeed_GivesTheSameSystem()
        {
            StarSystem a = StarSystemGenerator.Generate(771);
            StarSystem b = StarSystemGenerator.Generate(771);
            Assert.AreEqual(Describe(a), Describe(b));
            Assert.AreNotEqual(Describe(a), Describe(StarSystemGenerator.Generate(772)));
        }

        [Test]
        public void EverySystem_IsWellFormed()
        {
            var archetypes = new HashSet<WorldArchetype>();
            var stars = new HashSet<StarClass>();
            var keys = new HashSet<string>();
            for (int seed = 1; seed <= 400; seed++)
            {
                StarSystem system = StarSystemGenerator.Generate(seed);
                stars.Add(system.Star);
                Assert.IsNotEmpty(system.Name);
                Assert.That(system.Bodies.Count, Is.InRange(StarSystemGenerator.MinBodies, StarSystemGenerator.MaxBodies));
                float lastOrbit = 0f;
                for (int i = 0; i < system.Bodies.Count; i++)
                {
                    SystemBody body = system.Bodies[i];
                    string where = $"seed {seed} body {i} ({body.Archetype})";
                    archetypes.Add(body.Archetype);
                    Assert.Greater(body.OrbitAu, lastOrbit, where);
                    lastOrbit = body.OrbitAu;
                    Assert.IsTrue(keys.Add(body.Key), where + ": keys are unique");
                    Assert.IsTrue(StarSystemGenerator.TryParseKey(body.Key, out int keySeed, out int keyIndex), where);
                    Assert.AreEqual(seed, keySeed, where);
                    Assert.AreEqual(i, keyIndex, where);

                    CelestialBodyProfile p = body.Profile;
                    Assert.AreEqual(body.Key, p.Key, where);
                    Assert.AreNotEqual(0, p.Seed, where);
                    Assert.IsTrue(p.MixedContent, where);
                    Assert.AreEqual(4, p.ResourceWeights.Length, where);
                    Assert.Greater(p.ResourceWeights[0] + p.ResourceWeights[1] + p.ResourceWeights[2] + p.ResourceWeights[3], 0, where);
                    Assert.That(p.PopulationGoal, Is.InRange(10, 16), where);
                    Assert.That(p.MoveSpeedScale, Is.InRange(0.82f, 1.18f), where);
                    Assert.That(p.PowerDrawScale, Is.InRange(0.85f, 1.6f), where);
                    Assert.That(p.AmbientThreat, Is.InRange(0.08f, 0.22f), where);
                    Assert.That(p.SunIntensity, Is.InRange(0.6f, 2.6f), where);
                    Assert.GreaterOrEqual(p.ResourceNodeCount, 10, where);
                    Assert.GreaterOrEqual(p.LairCount, 3, where);
                    Assert.AreEqual(TechId.None, p.LaunchTech, where);
                    Assert.IsNotEmpty(p.Briefing, where);
                    Assert.IsNotEmpty(p.ShortCode, where);
                    Assert.IsFalse(JexAdvisor.TrainingWheels(p.Authored(WorldAspect.Story)), where + ": no Sol tutorial");
                    Assert.IsNull(CampaignCutsceneCatalog.ArrivalKey(p.Authored(WorldAspect.Story)), where + ": no Sol cutscene");
                }
            }

            Assert.AreEqual(8, archetypes.Count, "every archetype turns up in 400 systems");
            Assert.AreEqual(5, stars.Count, "and every star class");
        }

        /// <summary>The buildings a world gets must be the set that suits the terrain it wears.</summary>
        [Test]
        public void Archetype_GetsTheArchitectureItsTerrainWasBuiltFor()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                foreach (SystemBody body in StarSystemGenerator.Generate(seed).Bodies)
                {
                    ArchPreset preset = PlanetArchitecture.PresetFor(body.Traits);
                    string where = $"seed {seed} {body.Archetype} {body.Traits.TemperatureC} C";
                    switch (body.Archetype)
                    {
                        case WorldArchetype.AsteroidCluster:
                            Assert.AreEqual(ArchPreset.AnchoredStation, preset, where);
                            Assert.AreEqual(TerrainKit.AsteroidField, body.Profile.Kit, where);
                            break;
                        case WorldArchetype.IceShell:
                            Assert.AreEqual(ArchPreset.CryoBase, preset, where);
                            Assert.AreEqual(TerrainKit.IceCrust, body.Profile.Kit, where);
                            break;
                        case WorldArchetype.GardenWorld:
                        case WorldArchetype.OceanArchipelago:
                            Assert.AreEqual(ArchPreset.Arcology, preset, where);
                            break;
                        case WorldArchetype.RockyDesert:
                        case WorldArchetype.Volcanic:
                            Assert.AreEqual(ArchPreset.PrintedColony, preset, where);
                            break;
                        default:
                            Assert.That(preset, Is.EqualTo(ArchPreset.ShieldedOutpost).Or.EqualTo(ArchPreset.CryoBase), where);
                            break;
                    }
                }
            }
        }

        [Test]
        public void Orbit_SetsTemperatureAndPower()
        {
            Assert.AreEqual(5f, StarSystemGenerator.EquilibriumC(1f), 0.5f, "a bare rock at one Earth of starlight");
            Assert.Less(StarSystemGenerator.EquilibriumC(0.04f), -140f);

            StarSystem system = StarSystemGenerator.Generate(31337);
            for (int i = 1; i < system.Bodies.Count; i++)
            {
                Assert.Less(system.Bodies[i].Insolation, system.Bodies[i - 1].Insolation);
                Assert.GreaterOrEqual(system.Bodies[i].Profile.PowerDrawScale, system.Bodies[i - 1].Profile.PowerDrawScale,
                    "farther out never draws less power");
            }
        }

        [Test]
        public void GeneratedWorld_ComesBackFromItsKeyAlone()
        {
            StarSystem system = StarSystemGenerator.Generate(5150);
            SystemBody body = system.Bodies[system.Bodies.Count - 1];

            Assert.IsFalse(CelestialBodyCatalog.TryGetRegistered(body.Key, out _));
            Assert.IsTrue(StarSystemGenerator.EnsureRegistered(body.Key));
            Assert.IsTrue(CelestialBodyCatalog.TryGetRegistered(body.Key, out CelestialBodyProfile back));
            Assert.AreEqual(system.Bodies.Count, CelestialBodyCatalog.RegisteredCount, "the whole system is registered");
            Assert.AreEqual(body.Profile.DisplayName, back.DisplayName);
            Assert.AreEqual(body.Profile.Seed, back.Seed);
            Assert.AreEqual(body.Profile.GroundLight, back.GroundLight);
            Assert.IsTrue(StarSystemGenerator.EnsureRegistered(body.Key), "asking twice is harmless");
            Assert.AreEqual(system.Bodies.Count, CelestialBodyCatalog.RegisteredCount);

            Assert.IsFalse(StarSystemGenerator.EnsureRegistered("sol.mars"));
            Assert.IsFalse(StarSystemGenerator.EnsureRegistered("test.ares"));
            Assert.IsFalse(StarSystemGenerator.EnsureRegistered("gen.nonsense"));
            string beyond = body.Key.Substring(0, body.Key.LastIndexOf('.') + 1) + (system.Bodies.Count + 1);
            if (system.Bodies.Count < StarSystemGenerator.MaxBodies)
                Assert.IsFalse(StarSystemGenerator.EnsureRegistered(beyond), "no such body in that system");

            // Its files and prefs are its own.
            Assert.AreNotEqual(CelestialBodyCatalog.StorageId(back), CelestialBodyCatalog.StorageId(CelestialBodyCatalog.Get(back.Id)));
        }

        [Test]
        public void LaunchArguments_PickASystemAndABody()
        {
            Assert.IsFalse(StarSystemGenerator.TryFromArgs(null, out _, out _));
            Assert.IsFalse(StarSystemGenerator.TryFromArgs(new[] { "game.exe", "-world", "2" }, out _, out _));

            string code = SystemSeedCode.Format(90210);
            Assert.IsTrue(StarSystemGenerator.TryFromArgs(new[] { "game.exe", "-system", code, "-world", "2" },
                out StarSystem system, out SystemBody second));
            Assert.AreEqual(90210, system.Seed);
            Assert.AreSame(system.Bodies[1], second);

            Assert.IsTrue(StarSystemGenerator.TryFromArgs(new[] { "-SYSTEM", code }, out system, out SystemBody home));
            Assert.AreSame(StarSystemGenerator.Homeliest(system), home);
            Assert.IsTrue(StarSystemGenerator.TryFromArgs(new[] { "-system", code, "-world", "99" }, out system, out home));
            Assert.AreSame(StarSystemGenerator.Homeliest(system), home, "a place that does not exist falls back");

            Assert.IsTrue(StarSystemGenerator.TryFromArgs(new[] { "-system", "my homeworld" }, out system, out _));
            Assert.AreEqual(SystemSeedCode.FromText("my homeworld"), system.Seed);
        }

        private static string Describe(StarSystem s)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(s.Name).Append('|').Append(s.Star).Append('|').Append(s.Luminosity.ToString("R"));
            foreach (SystemBody b in s.Bodies)
            {
                sb.Append('|').Append(b.Key).Append(',').Append(b.Archetype).Append(',').Append(b.OrbitAu.ToString("R"))
                    .Append(',').Append(b.Seed).Append(',').Append(JsonUtility.ToJson(b.Profile));
            }
            return sb.ToString();
        }
    }
}
