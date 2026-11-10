// World archetypes for generated systems — pure C#, no MonoBehaviour.

using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Turns a generated body into a full <see cref="CelestialBodyProfile"/>. Each archetype
    /// starts from the Sol world closest to it, names which authored parts it wears
    /// (<see cref="CelestialBodyProfile.Authored"/>), then sets its own palette, light, layout,
    /// yields and threat from the star, the orbit and the seed.
    /// </summary>
    public static class WorldArchetypes
    {
        private struct Spec
        {
            /// <summary>The Sol world whose terrain, ground, fauna, sound and props this archetype wears.</summary>
            public CelestialBodyId Base;
            /// <summary>The Sol world whose sky it wears.</summary>
            public CelestialBodyId Sky;
            public Atmosphere Air;
            public float GravityMin, GravityMax;
            public float RadiationMin, RadiationMax;
            /// <summary>Degrees added to the bare-rock temperature (greenhouse, tides, lava).</summary>
            public float Warming;
            public float TempMin, TempMax;
            /// <summary>Regolith, metals, ice, fissile.</summary>
            public int[] Resources;
            public float Extract, Farm, Mine;
            public float Threat;
            public string Kind;
            public string Brief;

            public Spec(CelestialBodyId b, CelestialBodyId sky, Atmosphere air, float gMin, float gMax,
                float rMin, float rMax, float warming, float tMin, float tMax, int[] resources,
                float extract, float farm, float mine, float threat, string kind, string brief)
            {
                Base = b;
                Sky = sky;
                Air = air;
                GravityMin = gMin;
                GravityMax = gMax;
                RadiationMin = rMin;
                RadiationMax = rMax;
                Warming = warming;
                TempMin = tMin;
                TempMax = tMax;
                Resources = resources;
                Extract = extract;
                Farm = farm;
                Mine = mine;
                Threat = threat;
                Kind = kind;
                Brief = brief;
            }
        }

        // Indexed by WorldArchetype.
        private static readonly Spec[] Specs =
        {
            new Spec(CelestialBodyId.Mars, CelestialBodyId.Mars, Atmosphere.Thin, 0.25f, 0.9f, 0.2f, 0.5f,
                5f, -110f, 60f, new[] { 6, 4, 1, 2 }, 1f, 0.7f, 1.15f, 0.13f,
                "Rocky desert", "Dust and bare rock under thin air. Metals are close to the surface; water is not."),
            new Spec(CelestialBodyId.Europa, CelestialBodyId.Europa, Atmosphere.None, 0.08f, 0.3f, 0.3f, 0.8f,
                0f, -230f, -125f, new[] { 2, 2, 7, 2 }, 1.15f, 1.3f, 0.6f, 0.15f,
                "Ice shell", "A cracked shell of ice over a dark sea. Ice is everywhere; ore is thin and power is dear."),
            new Spec(CelestialBodyId.Belt, CelestialBodyId.Belt, Atmosphere.None, 0.005f, 0.04f, 0.4f, 0.7f,
                0f, -200f, 40f, new[] { 3, 7, 2, 3 }, 1.35f, 0.35f, 1.45f, 0.10f,
                "Asteroid cluster", "Rubble tied together by almost nothing. Rich ore, poor farms, and everything must be anchored."),
            new Spec(CelestialBodyId.Mars, CelestialBodyId.Mars, Atmosphere.Thin, 0.4f, 1.1f, 0.2f, 0.5f,
                70f, 80f, 420f, new[] { 3, 5, 0, 5 }, 1.1f, 0.4f, 1.35f, 0.17f,
                "Volcanic", "Black basalt and open vents. Fissile and metals run rich, ice is absent and the ground is hostile."),
            new Spec(CelestialBodyId.Earth, CelestialBodyId.Earth, Atmosphere.Breathable, 0.7f, 1.2f, 0f, 0.1f,
                20f, 5f, 30f, new[] { 4, 2, 4, 1 }, 0.9f, 1.35f, 0.75f, 0.11f,
                "Ocean archipelago", "Warm seas and scattered islands. Farms thrive, ore is scarce and water limits where you can build."),
            new Spec(CelestialBodyId.Luna, CelestialBodyId.Luna, Atmosphere.None, 0.3f, 0.9f, 0.5f, 0.9f,
                0f, -180f, 200f, new[] { 5, 4, 2, 3 }, 1.05f, 0.6f, 1.2f, 0.14f,
                "Tidally locked", "One face always toward its star. You build on the twilight line, in long shadow."),
            new Spec(CelestialBodyId.Luna, CelestialBodyId.Europa, Atmosphere.None, 0.1f, 0.25f, 0.7f, 1f,
                15f, -220f, -40f, new[] { 4, 3, 4, 3 }, 1.1f, 0.8f, 1f, 0.15f,
                "Gas-giant moon", "A cratered moon inside a giant's radiation belts. Crews tire fast away from shelter."),
            new Spec(CelestialBodyId.Earth, CelestialBodyId.Earth, Atmosphere.Breathable, 0.8f, 1.1f, 0f, 0.05f,
                25f, 10f, 24f, new[] { 5, 3, 3, 1 }, 1f, 1.25f, 0.9f, 0.12f,
                "Garden world", "Open air, forest and rivers. Easy living, and the wildlife knows it.")
        };

        /// <summary>The Sol world whose authored terrain, ground, fauna, sound and props an archetype wears.</summary>
        public static CelestialBodyId BaseOf(WorldArchetype archetype) => Specs[(int)archetype].Base;

        /// <summary>What the briefing calls this kind of world.</summary>
        public static string KindOf(WorldArchetype archetype) => Specs[(int)archetype].Kind;

        public static EnvironmentTraits RollTraits(SimRng rng, WorldArchetype archetype, float insolation)
        {
            Spec s = Specs[(int)archetype];
            float gravity = rng.Range(s.GravityMin, s.GravityMax);
            float radiation = rng.Range(s.RadiationMin, s.RadiationMax);
            float temp = Mathf.Clamp(StarSystemGenerator.EquilibriumC(insolation) + s.Warming, s.TempMin, s.TempMax);
            return new EnvironmentTraits(s.Air, gravity, Mathf.Round(temp), radiation);
        }

        public static CelestialBodyProfile Build(StarSystem system, SystemBody body, SimRng rng)
        {
            Spec s = Specs[(int)body.Archetype];
            CelestialBodyProfile p = CelestialBodyCatalog.Get(s.Base).Copy();

            p.Id = s.Base;
            p.MixFrom(s.Base);
            p.SkyFrom = s.Sky;
            // Belt and Europa carry no campaign cutscenes or advisor beats, so a generated world
            // tells its own story through its logs below.
            p.StoryFrom = CelestialBodyId.Europa;
            p.Key = body.Key;
            p.Seed = body.Seed;
            p.Traits = body.Traits;
            p.DisplayName = body.Name;
            p.ShortCode = WorldNames.ShortCode(system.Name, body.Index + 1);
            p.LaunchTech = TechId.None;

            Light(p, system, body, rng);
            Palette(p, body.Archetype, rng);
            Layout(p, body.Archetype, rng);

            p.ResourceWeights = (int[])s.Resources.Clone();
            p.ExtractYieldScale = Vary(rng, s.Extract);
            p.FarmYieldScale = Vary(rng, s.Farm);
            p.MineYieldScale = Vary(rng, s.Mine);
            // Less starlight, more of the grid spent on heat and light: Earth 1.0, Europa about 1.55.
            p.PowerDrawScale = Mathf.Clamp(1f - 0.12f * Mathf.Log(Mathf.Max(body.Insolation, 0.001f), 2f), 0.85f, 1.6f);
            p.ResearchRateMultiplier = Vary(rng, 1f);

            // Low gravity lets a robot bound along; heavy worlds and ice slow it.
            float move = 1.12f - 0.22f * body.Traits.Gravity;
            if (body.Archetype == WorldArchetype.IceShell) move -= 0.14f;
            p.MoveSpeedScale = Mathf.Clamp(move, 0.82f, 1.18f);
            // Only the worst belts wear a crew down in the open.
            p.RadiationDrainPerSecond = body.Traits.Radiation > 0.75f
                ? 0.012f * Mathf.InverseLerp(0.75f, 1f, body.Traits.Radiation)
                : 0f;

            p.AmbientThreat = Mathf.Clamp(s.Threat * rng.Range(0.85f, 1.2f), 0.08f, 0.22f);
            p.ExpansionThreat = rng.Range(0.016f, 0.024f);
            p.FaunaSpeedScale = Mathf.Clamp(p.MoveSpeedScale * rng.Range(0.95f, 1.1f), 0.85f, 1.3f);
            p.FaunaAggroScale = rng.Range(0.95f, 1.25f);
            p.LairStalkerBudget = rng.Next(2, 4);
            p.PopulationGoal = rng.Next(10, 17);
            p.SustainHoldSeconds = 40f;

            Briefing(p, system, body, s);
            return p;
        }

        private static float Vary(SimRng rng, float centre) => Mathf.Round(centre * rng.Range(0.9f, 1.1f) * 100f) / 100f;

        private static void Light(CelestialBodyProfile p, StarSystem system, SystemBody body, SimRng rng)
        {
            // A dim, distant star still has to light a readable scene, so brightness moves only a little.
            float strength = Mathf.Clamp(Mathf.Pow(Mathf.Max(body.Insolation, 0.001f), 0.12f), 0.78f, 1.22f);
            p.SunColor = system.SunColor;
            p.SunIntensity *= strength;
            p.FillColor = Color.Lerp(p.FillColor, Complement(system.SunColor), 0.25f);
            p.GradeFilter = Color.Lerp(p.GradeFilter, system.SunColor, 0.35f);
            float yaw = rng.Range(-70f, 70f);
            float pitch = rng.Range(34f, 58f);
            if (body.Archetype == WorldArchetype.TidallyLocked)
            {
                // The star sits on the horizon and never moves.
                pitch = rng.Range(9f, 16f);
                p.SunIntensity *= 1.1f;
            }
            p.SunEuler = new Vector3(pitch, yaw, 0f);
        }

        private static void Palette(CelestialBodyProfile p, WorldArchetype archetype, SimRng rng)
        {
            float hue = rng.Range(-0.06f, 0.06f);
            float sat = rng.Range(0.85f, 1.2f);
            float val = rng.Range(0.9f, 1.08f);
            Color pull = Color.clear;
            float pullAmount = 0f;
            switch (archetype)
            {
                case WorldArchetype.RockyDesert:
                    hue = rng.Range(-0.08f, 0.12f); // rust through ochre to sand
                    break;
                case WorldArchetype.Volcanic:
                    pull = new Color(0.10f, 0.09f, 0.10f);
                    pullAmount = 0.72f;
                    break;
                case WorldArchetype.TidallyLocked:
                    pull = new Color(0.30f, 0.24f, 0.22f);
                    pullAmount = 0.35f;
                    break;
                case WorldArchetype.GasGiantMoon:
                    pull = new Color(0.46f, 0.40f, 0.32f);
                    pullAmount = 0.4f;
                    break;
                case WorldArchetype.OceanArchipelago:
                    pull = new Color(0.78f, 0.72f, 0.56f); // sand
                    pullAmount = 0.3f;
                    break;
            }

            p.GroundLight = Shade(p.GroundLight, hue, sat, val, pull, pullAmount);
            p.GroundDark = Shade(p.GroundDark, hue, sat, val, pull, pullAmount);
            p.RockColor = Shade(p.RockColor, hue, sat, val, pull, pullAmount);
            p.CraterRim = Shade(p.CraterRim, hue, sat, val, pull, pullAmount);
            p.CraterFloor = Shade(p.CraterFloor, hue, sat, val, pull, pullAmount);
            p.DuneColor = Shade(p.DuneColor, hue, sat, val, pull, pullAmount);
            p.SoilNodeColor = Shade(p.SoilNodeColor, hue, sat, val, pull, pullAmount * 0.5f);
            p.Horizon = Shade(p.Horizon, hue, sat, val, pull, pullAmount);
            p.FogColor = Shade(p.FogColor, hue, sat, val, pull, pullAmount * 0.6f);
            p.SkyHorizon = Shade(p.SkyHorizon, hue, sat, 1f, pull, 0f);
            p.AmbientEquator = Shade(p.AmbientEquator, hue, sat, 1f, pull, pullAmount * 0.4f);
            p.AmbientGround = Shade(p.AmbientGround, hue, sat, 1f, pull, pullAmount * 0.4f);

            if (archetype == WorldArchetype.Volcanic)
            {
                // Vents glow: the pits and the haze carry the ember colour.
                var ember = new Color(0.95f, 0.36f, 0.10f);
                p.CraterFloor = Color.Lerp(p.CraterFloor, ember, 0.55f);
                p.LairPit = Color.Lerp(p.LairPit, ember, 0.35f);
                p.FogColor = Color.Lerp(p.FogColor, ember, 0.22f);
                p.SkyHorizon = Color.Lerp(p.SkyHorizon, ember, 0.3f);
                p.FogStart = 55f;
                p.FogEnd = 300f;
            }
            else if (archetype == WorldArchetype.OceanArchipelago || archetype == WorldArchetype.GardenWorld)
            {
                float leaf = rng.Range(-0.1f, 0.16f); // olive through green to teal
                p.ForestCanopy = Shade(p.ForestCanopy, leaf, sat, 1f, pull, 0f);
                p.WaterDeep = Shade(p.WaterDeep, rng.Range(-0.05f, 0.05f), 1f, 1f, pull, 0f);
                p.WaterShallow = Shade(p.WaterShallow, rng.Range(-0.05f, 0.05f), 1f, 1f, pull, 0f);
            }
        }

        private static void Layout(CelestialBodyProfile p, WorldArchetype archetype, SimRng rng)
        {
            p.CraterCount = Scaled(rng, p.CraterCount);
            p.RockCount = Scaled(rng, p.RockCount);
            p.DuneCount = Scaled(rng, p.DuneCount);
            p.RockFormationCount = Scaled(rng, p.RockFormationCount);
            p.ResourceNodeCount = Mathf.Max(10, Scaled(rng, p.ResourceNodeCount));
            p.LairCount = Mathf.Max(3, Scaled(rng, p.LairCount));
            p.AsteroidIsletCount = Scaled(rng, p.AsteroidIsletCount);
            p.IcePlateCount = Scaled(rng, p.IcePlateCount);

            switch (archetype)
            {
                case WorldArchetype.Volcanic:
                    p.CraterCount = Mathf.Max(p.CraterCount, rng.Next(50, 90)); // calderas and vents
                    p.DuneCount = 0;
                    p.RockFormationCount += rng.Next(4, 10);
                    break;
                case WorldArchetype.OceanArchipelago:
                    p.LakeCount = Mathf.Max(p.LakeCount, 4) + rng.Next(5, 10);
                    p.RiverCount = Scaled(rng, p.RiverCount);
                    p.ForestPatchCount = Mathf.Max(2, Scaled(rng, p.ForestPatchCount) / 2);
                    break;
                case WorldArchetype.GardenWorld:
                    p.LakeCount = Scaled(rng, p.LakeCount);
                    p.RiverCount = Scaled(rng, p.RiverCount) + rng.Next(0, 2);
                    p.ForestPatchCount = Scaled(rng, p.ForestPatchCount) + rng.Next(2, 6);
                    break;
                case WorldArchetype.GasGiantMoon:
                    p.CraterCount += rng.Next(10, 40);
                    break;
            }
        }

        private static int Scaled(SimRng rng, int count) =>
            count <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(count * rng.Range(0.7f, 1.3f)));

        // English, like every profile text: the display properties pass them through Loc.S.
        private static void Briefing(CelestialBodyProfile p, StarSystem system, SystemBody body, Spec s)
        {
            string temp = Mathf.RoundToInt(body.Traits.TemperatureC) + " C";
            string gravity = body.Traits.Gravity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " g";
            p.Briefing = s.Kind + ", " + temp + ", " + gravity + ". " + s.Brief + " Hold pop " + p.PopulationGoal + ".";
            p.ArrivalLog = body.Name + " insertion complete. " + s.Kind + " in the " + system.Name + " system, seed " + system.Code + ".";
            p.VictoryLog = body.Name + " holds. The " + system.Name + " system has a colony.";
            p.FailLog = body.Name + " plaza is overrun. Revive or reseed this world.";
            p.EndlessLog = body.Name + " holds. Keep colonizing; rating stands.";
        }

        private static Color Shade(Color c, float hueShift, float satMul, float valMul, Color pull, float pullAmount)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            h = Mathf.Repeat(h + hueShift, 1f);
            Color shaded = Color.HSVToRGB(h, Mathf.Clamp01(s * satMul), Mathf.Clamp01(v * valMul));
            if (pullAmount > 0f) shaded = Color.Lerp(shaded, pull, pullAmount);
            shaded.a = c.a;
            return shaded;
        }

        private static Color Complement(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB(Mathf.Repeat(h + 0.5f, 1f), Mathf.Clamp01(s + 0.25f), v);
        }
    }
}
