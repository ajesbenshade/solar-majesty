// World modifiers for generated systems — pure C#, no MonoBehaviour.

using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>A standing condition of a generated world. Each world has two or three, named in its briefing.</summary>
    public enum WorldModifier
    {
        DustStorms = 0,
        StellarFlares = 1,
        Quakes = 2,
        RichVeins = 3,
        AncientRuins = 4,
        HostileHives = 5
    }

    /// <summary>
    /// Rolls a world's modifiers and applies them to its profile. A modifier here is a standing
    /// change to the numbers the game already reads (yields, power, threat, layout); none of them
    /// is an event that comes and goes during play.
    /// </summary>
    public static class WorldModifiers
    {
        public const int MinPerWorld = 2;
        public const int MaxPerWorld = 3;
        private const int Count = 6;

        public static string Name(WorldModifier m)
        {
            switch (m)
            {
                case WorldModifier.DustStorms: return "Dust storms";
                case WorldModifier.StellarFlares: return "Stellar flares";
                case WorldModifier.Quakes: return "Quakes";
                case WorldModifier.RichVeins: return "Rich veins";
                case WorldModifier.AncientRuins: return "Ancient ruins";
                default: return "Hostile hives";
            }
        }

        /// <summary>One line for the briefing: what the modifier does to the colony.</summary>
        public static string Effect(WorldModifier m)
        {
            switch (m)
            {
                case WorldModifier.DustStorms: return "Power costs more, crews move slower and you see less far.";
                case WorldModifier.StellarFlares: return "Crews in the open tire from radiation.";
                case WorldModifier.Quakes: return "Broken ground: more exposed ore, slower building sites.";
                case WorldModifier.RichVeins: return "More resource nodes and better mines; expansion draws more attention.";
                case WorldModifier.AncientRuins: return "More sites to explore and faster research.";
                default: return "More lairs, and each one holds more.";
            }
        }

        /// <summary>True when this kind of world can have the modifier: no dust without air to carry it.</summary>
        public static bool Fits(WorldModifier m, EnvironmentTraits traits) =>
            m != WorldModifier.DustStorms || traits.Atmosphere != Atmosphere.None;

        /// <summary>Two or three different modifiers, in enum order so the briefing reads the same every time.</summary>
        public static WorldModifier[] Roll(SimRng rng, StarClass star, EnvironmentTraits traits)
        {
            int want = rng.Next(MinPerWorld, MaxPerWorld + 1);
            var weights = new int[Count];
            for (int i = 0; i < Count; i++)
                weights[i] = Fits((WorldModifier)i, traits) ? 3 : 0;
            // Small stars flare; so do the hot blue ones.
            if (star == StarClass.RedDwarf || star == StarClass.Blue) weights[(int)WorldModifier.StellarFlares] += 3;

            var picked = new List<WorldModifier>(want);
            for (int n = 0; n < want; n++)
            {
                int total = 0;
                for (int i = 0; i < Count; i++) total += weights[i];
                if (total <= 0) break;
                int roll = rng.Next(0, total);
                for (int i = 0; i < Count; i++)
                {
                    roll -= weights[i];
                    if (roll >= 0) continue;
                    picked.Add((WorldModifier)i);
                    weights[i] = 0;
                    break;
                }
            }
            picked.Sort();
            return picked.ToArray();
        }

        public static void Apply(CelestialBodyProfile p, WorldModifier m)
        {
            switch (m)
            {
                case WorldModifier.DustStorms:
                    p.PowerDrawScale *= 1.12f;
                    p.MoveSpeedScale *= 0.95f;
                    p.FogEnd *= 0.7f;
                    p.FogStart *= 0.7f;
                    break;
                case WorldModifier.StellarFlares:
                    p.RadiationDrainPerSecond += 0.004f;
                    break;
                case WorldModifier.Quakes:
                    p.MineYieldScale *= 1.12f;
                    p.ExtractYieldScale *= 1.08f;
                    p.RockFormationCount += 6;
                    p.MinSpacing += 1.5f;
                    break;
                case WorldModifier.RichVeins:
                    p.ResourceNodeCount = Mathf.RoundToInt(p.ResourceNodeCount * 1.3f);
                    p.MineYieldScale *= 1.2f;
                    p.ExpansionThreat *= 1.15f;
                    break;
                case WorldModifier.AncientRuins:
                    p.PoiCount += 5;
                    p.ResearchRateMultiplier *= 1.2f;
                    break;
                case WorldModifier.HostileHives:
                    p.LairCount = Mathf.RoundToInt(p.LairCount * 1.4f);
                    p.LairStalkerBudget += 1;
                    p.AmbientThreat *= 1.2f;
                    break;
            }
        }
    }
}
