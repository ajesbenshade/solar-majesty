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
            var modifiers = new HashSet<WorldModifier>();
            for (int seed = 1; seed <= 1000; seed++)
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
                    Assert.That(p.MoveSpeedScale, Is.InRange(0.77f, 1.18f), where);
                    Assert.That(p.PowerDrawScale, Is.InRange(0.85f, 1.8f), where);
                    Assert.That(p.AmbientThreat, Is.InRange(0.08f, 0.27f), where);
                    Assert.That(p.Modifiers.Length, Is.InRange(WorldModifiers.MinPerWorld, WorldModifiers.MaxPerWorld), where);
                    for (int m = 0; m < p.Modifiers.Length; m++)
                    {
                        modifiers.Add(p.Modifiers[m]);
                        Assert.IsTrue(WorldModifiers.Fits(p.Modifiers[m], body.Traits), where + " " + p.Modifiers[m]);
                        if (m > 0) Assert.Less((int)p.Modifiers[m - 1], (int)p.Modifiers[m], where + ": no repeats, fixed order");
                        StringAssert.Contains(WorldModifiers.Name(p.Modifiers[m]), p.Briefing, where);
                    }
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

            Assert.AreEqual(8, archetypes.Count, "every archetype turns up in 1,000 systems");
            Assert.AreEqual(5, stars.Count, "and every star class");
            Assert.AreEqual(6, modifiers.Count, "and every modifier");
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
                Assert.GreaterOrEqual(WithoutStorms(system.Bodies[i].Profile), WithoutStorms(system.Bodies[i - 1].Profile) - 0.0001f,
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

        /// <summary>A generated world wears a Sol world's ground in its own colours.</summary>
        [Test]
        public void GeneratedWorld_RepaintsTheGroundItBorrows()
        {
            var rust = new Color(0.58f, 0.27f, 0.12f);
            Assert.IsFalse(CelestialBodyCatalog.Mars().Repainted);
            Assert.AreEqual(rust, CelestialBodyCatalog.Mars().Repaint(rust), "a Sol world keeps its authored colours");

            CelestialBodyProfile volcanic = FirstOf(WorldArchetype.Volcanic).Profile;
            Assert.IsTrue(volcanic.Repainted);
            Color basalt = volcanic.Repaint(rust);
            Color.RGBToHSV(basalt, out _, out _, out float value);
            Assert.Less(value, 0.32f, "Mars rust becomes dark basalt");
            Assert.AreEqual(1f, basalt.a);

            // A desert is never the pink side of red, and is not simply Mars again.
            int different = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                foreach (SystemBody body in StarSystemGenerator.Generate(seed).Bodies)
                {
                    Assert.IsTrue(body.Profile.Repainted, $"seed {seed}");
                    if (body.Archetype != WorldArchetype.RockyDesert) continue;
                    Color sand = body.Profile.Repaint(rust);
                    Color.RGBToHSV(sand, out float hue, out _, out _);
                    Assert.That(hue, Is.InRange(0.02f, 0.2f), $"seed {seed}");
                    if (Mathf.Abs(sand.r - rust.r) + Mathf.Abs(sand.g - rust.g) + Mathf.Abs(sand.b - rust.b) > 0.12f) different++;
                }
            }
            Assert.Greater(different, 20, "most deserts are visibly not Mars");

            // The undergrowth is repainted too, on a copy: Mars keeps its own.
            Assert.AreNotSame(CelestialBodyCatalog.Mars().GroundCover, volcanic.GroundCover);
            Assert.AreEqual(CelestialBodyCatalog.Mars().GroundCover.Length, volcanic.GroundCover.Length);
            Assert.AreEqual(CelestialBodyCatalog.BuiltIn(CelestialBodyId.Mars).GroundCover[0].ColorA,
                CelestialBodyCatalog.Mars().GroundCover[0].ColorA);
        }

        /// <summary>What a briefing promises must be true of the world.</summary>
        [Test]
        public void Briefings_SayOnlyWhatTheNumbersDo()
        {
            for (int seed = 1; seed <= 300; seed++)
            {
                foreach (SystemBody body in StarSystemGenerator.Generate(seed).Bodies)
                {
                    CelestialBodyProfile p = body.Profile;
                    string where = $"seed {seed} {body.Archetype}";
                    switch (body.Archetype)
                    {
                        case WorldArchetype.GasGiantMoon:
                            Assert.Greater(p.RadiationDrainPerSecond, 0f, where + ": crews tire away from shelter");
                            break;
                        case WorldArchetype.Volcanic:
                            Assert.AreEqual(0, p.ResourceWeights[2], where + ": ice is absent");
                            break;
                        case WorldArchetype.IceShell:
                            Assert.Greater(p.ResourceWeights[2], p.ResourceWeights[1], where + ": ice is everywhere, ore is thin");
                            break;
                        case WorldArchetype.AsteroidCluster:
                            Assert.Less(p.FarmYieldScale, 0.6f, where + ": poor farms");
                            Assert.Greater(p.MineYieldScale, 1.2f, where + ": rich ore");
                            break;
                        case WorldArchetype.OceanArchipelago:
                            Assert.Greater(p.FarmYieldScale, 1.1f, where + ": farms thrive");
                            Assert.GreaterOrEqual(p.LakeCount, 9, where + ": water limits where you can build");
                            break;
                    }
                    if (System.Array.IndexOf(p.Modifiers, WorldModifier.StellarFlares) >= 0)
                        Assert.Greater(p.RadiationDrainPerSecond, 0f, where + ": flares");
                    StringAssert.Contains("\u00b0C", p.Briefing, where);
                }
            }
        }

        [Test]
        public void ExampleCode_IsARealCode()
        {
            Assert.IsTrue(SystemSeedCode.TryParse("1K2M-9AX", out int seed));
            Assert.AreEqual("1K2M-9AX", SystemSeedCode.Format(seed));
            Assert.IsFalse(SystemSeedCode.TryParse("7K2M-9AX", out _), "a code starts with 0 to 3: seven characters hold 35 bits");
            Assert.IsTrue(StarSystemGenerator.TryParseKey("gen.1k2m9ax.2", out int keySeed, out int index));
            Assert.AreEqual(seed, keySeed);
            Assert.AreEqual(1, index);
        }

        /// <summary>A system must not read as a row of identical temperatures.</summary>
        [Test]
        public void Temperatures_SpreadInsideAnArchetypesRange()
        {
            var ocean = new HashSet<float>();
            var ice = new HashSet<float>();
            for (int seed = 1; seed <= 300; seed++)
            {
                foreach (SystemBody body in StarSystemGenerator.Generate(seed).Bodies)
                {
                    if (body.Archetype == WorldArchetype.OceanArchipelago)
                    {
                        ocean.Add(body.Traits.TemperatureC);
                        Assert.That(body.Traits.TemperatureC, Is.InRange(5f, 30f), $"seed {seed}");
                    }
                    else if (body.Archetype == WorldArchetype.IceShell)
                    {
                        ice.Add(body.Traits.TemperatureC);
                        Assert.That(body.Traits.TemperatureC, Is.InRange(-230f, -125f), $"seed {seed}");
                    }
                }
            }
            Assert.GreaterOrEqual(ocean.Count, 8, "ocean worlds are not all at the top of their range");
            Assert.GreaterOrEqual(ice.Count, 12, "ice shells are not all at -125");
        }

        /// <summary>A world outside Sol is secured under its own name and leads nowhere in the campaign.</summary>
        [Test]
        public void Victory_OnAGeneratedWorld_OffersOnlyTheMainMenu()
        {
            SystemBody body = FirstOf(WorldArchetype.Volcanic);
            Assert.AreEqual(CelestialBodyId.Mars, body.Profile.Id, "it wears Mars, whose victory offers the Belt");
            Assert.IsFalse(VictoryOffer.InCampaign(body.Profile));
            Assert.IsTrue(VictoryOffer.InCampaign(CelestialBodyCatalog.Mars()));
            Assert.IsFalse(VictoryOffer.NextIsBuilt(body.Profile));

            VictoryOffer.View offer = VictoryOffer.For(body.Profile, nextBuilt: true, endless: false);
            Assert.IsTrue(offer.HoldSim);
            Assert.IsFalse(offer.CanContinue, "no next planet");
            Assert.IsNull(offer.ComingSoon);
            Assert.IsTrue(offer.ShowMainMenu);
            Assert.AreEqual(body.Name.ToUpperInvariant() + " SECURED", offer.Headline);

            VictoryOffer.View mars = VictoryOffer.For(CelestialBodyCatalog.Mars(), nextBuilt: true, endless: false);
            Assert.AreEqual(VictoryOffer.For(CelestialBodyId.Mars, true, false).Headline, mars.Headline);
            Assert.AreEqual(VictoryOffer.For(CelestialBodyId.Mars, true, false).ContinueLabel, mars.ContinueLabel);
            Assert.AreEqual(offer.Body, mars.Body);
            Assert.AreEqual(VictoryOffer.For(CelestialBodyId.Mars, true, true).Headline,
                VictoryOffer.For(body.Profile, true, endless: true).Headline, "endless is the same everywhere");
        }

        /// <summary>The launch gate reads the world's own launch tech, not the Sol world it wears.</summary>
        [Test]
        public void LaunchGate_OnAGeneratedWorld_NeedsNoSolTech()
        {
            SystemBody body = FirstOf(WorldArchetype.Volcanic);
            var research = new ResearchManager(new ResourceManager());
            Assert.AreNotEqual(TechId.None, research.LaunchTechId(CelestialBodyId.Mars));
            Assert.AreEqual(research.LaunchTechId(CelestialBodyId.Mars), research.LaunchTechId(CelestialBodyCatalog.Mars()));
            Assert.AreEqual(research.LaunchTechLabel(CelestialBodyId.Mars), research.LaunchTechLabel(CelestialBodyCatalog.Mars()));

            Assert.AreEqual(TechId.None, research.LaunchTechId(body.Profile));
            Assert.IsTrue(research.HasLaunchUnlockFor(body.Profile));
            Assert.AreEqual(research.LaunchTechLabel((CelestialBodyProfile)null), research.LaunchTechLabel(body.Profile));
            Assert.AreNotEqual(research.LaunchTechLabel(CelestialBodyId.Mars), research.LaunchTechLabel(body.Profile));
        }

        [Test]
        public void SaveOnAGeneratedWorld_IsListedUnderItsOwnName()
        {
            var sol = new SaveGame { body = (int)CelestialBodyId.Mars };
            StringAssert.StartsWith("Mars ·", sol.Describe());
            var beyond = new SaveGame { body = (int)CelestialBodyId.Mars, bodyKey = "gen.1k2m9ax.2", bodyName = "Thraen II" };
            StringAssert.StartsWith("Thraen II ·", beyond.Describe());
            SaveGame back = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(beyond));
            Assert.AreEqual("Thraen II", back.bodyName);
        }

        /// <summary>Continue names the generated world, and finds it again with nothing registered.</summary>
        [Test]
        public void SavedWorld_IsTheGeneratedOne_WhenThatIsWhatWasPlayed()
        {
            DeveloperPrefsShield.Capture();
            try
            {
                BodySeed.SetBody(CelestialBodyId.Mars);
                Assert.AreSame(CelestialBodyCatalog.Mars(), DemoSettings.SavedWorld());

                SystemBody body = FirstOf(WorldArchetype.IceShell);
                StarSystemGenerator.EnsureRegistered(body.Key);
                Assert.IsTrue(CelestialBodyCatalog.TryGetRegistered(body.Key, out CelestialBodyProfile world));
                BodySeed.SetWorld(world);
                CelestialBodyCatalog.ClearRegistered(); // as after a restart
                Assert.AreEqual(body.Key, DemoSettings.SavedWorld().Key);
                Assert.AreEqual(body.Name, DemoSettings.SavedWorld().DisplayName);

                BodySeed.SetBody(CelestialBodyId.Earth);
                Assert.AreSame(CelestialBodyCatalog.Earth(), DemoSettings.SavedWorld());
            }
            finally
            {
                BodySeed.SetBody(CelestialBodyId.Earth);
                DeveloperPrefsShield.Restore();
            }
        }

        /// <summary>A new colony stands on the world's own ground, whatever was played there before.</summary>
        [Test]
        public void NewColony_GoesBackToTheWorldsOwnSeed()
        {
            DeveloperPrefsShield.Capture();
            SystemBody body = FirstOf(WorldArchetype.RockyDesert);
            string stored = "SM_BodySeed_" + body.Key;
            try
            {
                StarSystemGenerator.EnsureRegistered(body.Key);
                CelestialBodyCatalog.TryGetRegistered(body.Key, out CelestialBodyProfile world);

                BodySeed.SetWorld(world, newColony: true);
                BodySeed.Ensure(world);
                Assert.AreEqual(body.Seed, BodySeed.Current);

                BodySeed.AdvanceForNextConquest(); // a retry moves the stored seed on
                Assert.AreNotEqual(body.Seed, BodySeed.Current);
                BodySeed.SetWorld(world);
                BodySeed.Ensure(world);
                Assert.AreNotEqual(body.Seed, BodySeed.Current, "carrying on keeps the ground the colony stands on");

                BodySeed.SetWorld(world, newColony: true);
                BodySeed.Ensure(world);
                Assert.AreEqual(body.Seed, BodySeed.Current, "a new colony starts from the shared seed again");
            }
            finally
            {
                GamePrefs.DeleteKey(stored);
                BodySeed.SetBody(CelestialBodyId.Earth);
                DeveloperPrefsShield.Restore();
            }
        }

        private static SystemBody FirstOf(WorldArchetype archetype)
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                foreach (SystemBody body in StarSystemGenerator.Generate(seed).Bodies)
                {
                    if (body.Archetype == archetype) return body;
                }
            }
            return null;
        }

        private static float WithoutStorms(CelestialBodyProfile p) =>
            System.Array.IndexOf(p.Modifiers, WorldModifier.DustStorms) >= 0 ? p.PowerDrawScale / 1.12f : p.PowerDrawScale;

        /// <summary>A modifier changes the numbers the game reads, and only the ones it names.</summary>
        [Test]
        public void Modifiers_ChangeTheProfile()
        {
            CelestialBodyProfile plain = CelestialBodyCatalog.Mars().Copy();
            foreach (WorldModifier m in System.Enum.GetValues(typeof(WorldModifier)))
            {
                CelestialBodyProfile changed = plain.Copy();
                WorldModifiers.Apply(changed, m);
                Assert.AreNotEqual(JsonUtility.ToJson(plain), JsonUtility.ToJson(changed), m.ToString());
                Assert.IsNotEmpty(WorldModifiers.Name(m));
                Assert.IsNotEmpty(WorldModifiers.Effect(m));
            }

            CelestialBodyProfile hives = plain.Copy();
            WorldModifiers.Apply(hives, WorldModifier.HostileHives);
            Assert.Greater(hives.LairCount, plain.LairCount);
            Assert.Greater(hives.AmbientThreat, plain.AmbientThreat);
            Assert.AreEqual(plain.MineYieldScale, hives.MineYieldScale);

            Assert.IsFalse(WorldModifiers.Fits(WorldModifier.DustStorms, new EnvironmentTraits(Atmosphere.None, 0.2f, -20f, 0.5f)));
            Assert.IsEmpty(CelestialBodyCatalog.Mars().Modifiers, "Sol worlds have none");
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
