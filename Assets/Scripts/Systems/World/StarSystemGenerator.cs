// Generated star systems — pure C#, no MonoBehaviour.

using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    public enum StarClass
    {
        RedDwarf = 0,
        Orange = 1,
        Yellow = 2,
        White = 3,
        Blue = 4
    }

    /// <summary>What kind of world a generated body is. Each builds a full <see cref="CelestialBodyProfile"/>.</summary>
    public enum WorldArchetype
    {
        RockyDesert = 0,
        IceShell = 1,
        AsteroidCluster = 2,
        Volcanic = 3,
        OceanArchipelago = 4,
        TidallyLocked = 5,
        GasGiantMoon = 6,
        GardenWorld = 7
    }

    /// <summary>One conquerable body of a generated system.</summary>
    public sealed class SystemBody
    {
        /// <summary>Place outward from the star, starting at 0.</summary>
        public int Index;
        public string Name;
        /// <summary>World key ("gen.7k2m9ax.2"). The key alone is enough to build the world again.</summary>
        public string Key;
        public WorldArchetype Archetype;
        /// <summary>Orbit radius in astronomical units.</summary>
        public float OrbitAu;
        /// <summary>Starlight reaching the body, where 1 is what Earth gets from the Sun.</summary>
        public float Insolation;
        public EnvironmentTraits Traits;
        /// <summary>Seed for this body and its terrain.</summary>
        public int Seed;
        public CelestialBodyProfile Profile;
    }

    public sealed class StarSystem
    {
        public int Seed;
        /// <summary>The seed as a player shares it ("7K2M-9AX").</summary>
        public string Code;
        public string Name;
        public StarClass Star;
        /// <summary>Output in Suns.</summary>
        public float Luminosity;
        public Color SunColor;
        public readonly List<SystemBody> Bodies = new List<SystemBody>();
    }

    /// <summary>
    /// Builds a star system from a seed: a star, then three to nine bodies outward from it. The
    /// star class sets the colour and strength of the light; the orbit sets how much of it
    /// arrives, and so the temperature and what kind of world can be there. The same seed gives
    /// the same system on every machine.
    /// </summary>
    public static class StarSystemGenerator
    {
        public const int MinBodies = 3;
        public const int MaxBodies = 9;
        /// <summary>Generated world keys start with this.</summary>
        public const string KeyPrefix = "gen.";

        private struct StarSpec
        {
            public int Weight;
            public float Luminosity;
            public Color Color;
            public StarSpec(int weight, float luminosity, Color color)
            {
                Weight = weight;
                Luminosity = luminosity;
                Color = color;
            }
        }

        // Indexed by StarClass. Small stars are common, as they are in the sky.
        private static readonly StarSpec[] Stars =
        {
            new StarSpec(34, 0.06f, new Color(1f, 0.62f, 0.45f)),
            new StarSpec(26, 0.35f, new Color(1f, 0.82f, 0.62f)),
            new StarSpec(22, 1f, new Color(1f, 0.95f, 0.86f)),
            new StarSpec(12, 2.6f, new Color(0.95f, 0.97f, 1f)),
            new StarSpec(6, 12f, new Color(0.76f, 0.85f, 1f))
        };

        // Insolation bands, hottest first. A body draws its archetype from the band it orbits in.
        public const float HotAbove = 2.2f;
        public const float TemperateAbove = 0.45f;
        public const float CoolAbove = 0.08f;

        public static StarSystem Generate(int seed)
        {
            var rng = new SimRng(seed);
            var system = new StarSystem
            {
                Seed = seed,
                Code = SystemSeedCode.Format(seed),
                Star = PickStar(rng)
            };
            StarSpec star = Stars[(int)system.Star];
            // Stars of one class are not all alike.
            system.Luminosity = star.Luminosity * rng.Range(0.8f, 1.25f);
            system.SunColor = star.Color;
            system.Name = WorldNames.Star(rng);

            int count = rng.Next(MinBodies, MaxBodies + 1);
            // The first orbit scales with the star so every class has bodies near its warm band.
            float au = Mathf.Sqrt(system.Luminosity) * rng.Range(0.3f, 0.55f);
            string compact = SystemSeedCode.Compact(seed).ToLowerInvariant();
            for (int i = 0; i < count; i++)
            {
                var body = new SystemBody
                {
                    Index = i,
                    OrbitAu = au,
                    Insolation = system.Luminosity / (au * au),
                    Name = system.Name + " " + WorldNames.Roman(i + 1),
                    Key = KeyPrefix + compact + "." + (i + 1),
                    Seed = (int)(rng.NextUInt() & 0x3FFFFFFF) | 1
                };
                body.Archetype = PickArchetype(rng, system.Star, body.Insolation);
                body.Traits = WorldArchetypes.RollTraits(rng, body.Archetype, body.Insolation);
                body.Profile = WorldArchetypes.Build(system, body, rng);
                system.Bodies.Add(body);
                au *= rng.Range(1.45f, 1.95f);
            }
            return system;
        }

        /// <summary>Equilibrium temperature in Celsius for a bare rock at this insolation.</summary>
        public static float EquilibriumC(float insolation) =>
            278f * Mathf.Pow(Mathf.Max(insolation, 0.0001f), 0.25f) - 273f;

        /// <summary>Split a generated world key into its system seed and body index (from 0).</summary>
        public static bool TryParseKey(string key, out int seed, out int index)
        {
            seed = 0;
            index = -1;
            if (string.IsNullOrEmpty(key) || !key.StartsWith(KeyPrefix, System.StringComparison.Ordinal)) return false;
            int dot = key.LastIndexOf('.');
            if (dot <= KeyPrefix.Length) return false;
            if (!SystemSeedCode.TryParse(key.Substring(KeyPrefix.Length, dot - KeyPrefix.Length), out seed)) return false;
            if (!int.TryParse(key.Substring(dot + 1), out int place) || place < 1 || place > MaxBodies) return false;
            index = place - 1;
            return true;
        }

        /// <summary>
        /// Add every body of a system to the catalog. Bodies already registered are left as they
        /// are. Returns how many were added.
        /// </summary>
        public static int Register(StarSystem system)
        {
            int added = 0;
            for (int i = 0; i < system.Bodies.Count; i++)
            {
                if (CelestialBodyCatalog.Register(system.Bodies[i].Profile)) added++;
            }
            return added;
        }

        /// <summary>
        /// Make sure the generated world a key names is in the catalog, building its system again
        /// from the seed in the key. This is how a generated world comes back after a restart:
        /// nothing but its key is stored. False when the key is not a generated one.
        /// </summary>
        public static bool EnsureRegistered(string key)
        {
            if (!TryParseKey(key, out int seed, out int index)) return false;
            if (CelestialBodyCatalog.TryGetRegistered(key, out _)) return true;
            StarSystem system = Generate(seed);
            if (index >= system.Bodies.Count) return false;
            Register(system);
            return CelestialBodyCatalog.TryGetRegistered(key, out _);
        }

        /// <summary>Launch argument: <c>-system 7K2M-9AX</c> (or any text) plays a generated system.</summary>
        public const string SystemArgument = "-system";
        /// <summary>Launch argument: <c>-world 3</c> picks the third body out. Without it, the most temperate one.</summary>
        public const string WorldArgument = "-world";

        /// <summary>
        /// Read <see cref="SystemArgument"/> and <see cref="WorldArgument"/> from a command line
        /// and generate the system. False when no system was asked for.
        /// </summary>
        public static bool TryFromArgs(string[] args, out StarSystem system, out SystemBody body)
        {
            system = null;
            body = null;
            if (args == null) return false;
            string seedText = null;
            int place = 0;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (string.Equals(args[i], SystemArgument, System.StringComparison.OrdinalIgnoreCase))
                    seedText = args[i + 1];
                else if (string.Equals(args[i], WorldArgument, System.StringComparison.OrdinalIgnoreCase))
                    int.TryParse(args[i + 1], out place);
            }
            if (string.IsNullOrEmpty(seedText)) return false;
            system = Generate(SystemSeedCode.FromText(seedText));
            body = place >= 1 && place <= system.Bodies.Count ? system.Bodies[place - 1] : Homeliest(system);
            return true;
        }

        /// <summary>The body easiest to start on: the one whose starlight is nearest to what Earth gets.</summary>
        public static SystemBody Homeliest(StarSystem system)
        {
            SystemBody best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < system.Bodies.Count; i++)
            {
                float distance = Mathf.Abs(Mathf.Log(Mathf.Max(system.Bodies[i].Insolation, 0.0001f)));
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = system.Bodies[i];
            }
            return best;
        }

        private static StarClass PickStar(SimRng rng)
        {
            int total = 0;
            for (int i = 0; i < Stars.Length; i++) total += Stars[i].Weight;
            int roll = rng.Next(0, total);
            for (int i = 0; i < Stars.Length; i++)
            {
                roll -= Stars[i].Weight;
                if (roll < 0) return (StarClass)i;
            }
            return StarClass.Yellow;
        }

        // Weights per archetype in enum order: desert, ice, asteroid, volcanic, ocean, locked, moon, garden.
        private static readonly int[] HotWeights = { 3, 0, 0, 4, 0, 3, 0, 0 };
        private static readonly int[] TemperateWeights = { 3, 0, 0, 1, 3, 0, 0, 3 };
        private static readonly int[] CoolWeights = { 3, 2, 3, 0, 0, 0, 2, 0 };
        private static readonly int[] ColdWeights = { 0, 4, 3, 0, 0, 0, 3, 0 };

        private static WorldArchetype PickArchetype(SimRng rng, StarClass star, float insolation)
        {
            int[] weights = insolation > HotAbove ? HotWeights
                : insolation > TemperateAbove ? TemperateWeights
                : insolation > CoolAbove ? CoolWeights
                : ColdWeights;
            int total = 0;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            // The warm band of a red dwarf is close enough in to lock a world to its star.
            int locked = star == StarClass.RedDwarf && weights == TemperateWeights ? 4 : 0;
            int roll = rng.Next(0, total + locked);
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0) return (WorldArchetype)i;
            }
            return WorldArchetype.TidallyLocked;
        }
    }
}
