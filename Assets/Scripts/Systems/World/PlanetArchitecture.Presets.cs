using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    public static partial class PlanetArchitecture
    {
        /// <summary>Below this gravity a building has to be tied down before anything else matters.</summary>
        public const float MicrogravityG = 0.05f;

        /// <summary>Colder than this, ice is the cheapest wall there is.</summary>
        public const float CryoTemperatureC = -120f;

        /// <summary>
        /// Pick the architectural set a world's conditions call for. The order is the order of
        /// what would kill a building first: no gravity to hold it, then whether the air can be
        /// used, then cold, then whether there is any air at all.
        /// </summary>
        public static ArchPreset PresetFor(EnvironmentTraits traits)
        {
            if (traits.Gravity < MicrogravityG) return ArchPreset.AnchoredStation;
            if (traits.Atmosphere == Atmosphere.Breathable) return ArchPreset.Arcology;
            if (traits.TemperatureC < CryoTemperatureC) return ArchPreset.CryoBase;
            if (traits.Atmosphere == Atmosphere.Thin) return ArchPreset.PrintedColony;
            return ArchPreset.ShieldedOutpost;
        }

        /// <summary>The set each Sol world was authored with.</summary>
        public static ArchPreset PresetOf(CelestialBodyId id) => id switch
        {
            CelestialBodyId.Earth => ArchPreset.Arcology,
            CelestialBodyId.Luna => ArchPreset.ShieldedOutpost,
            CelestialBodyId.Mars => ArchPreset.PrintedColony,
            CelestialBodyId.Belt => ArchPreset.AnchoredStation,
            CelestialBodyId.Europa => ArchPreset.CryoBase,
            _ => ArchPreset.ShieldedOutpost
        };

        // The Sol worlds by id. Each forwards to its preset, so nothing about them changes.

        public static ArchStyle Style(CelestialBodyId id) => Style(PresetOf(id));

        public static bool TryRemap(CelestialBodyId id, Color c, out Color result) =>
            TryRemap(PresetOf(id), c, out result);

        public static bool TryRemapMaterial(CelestialBodyId id, string materialName, Color current, out Color result) =>
            TryRemapMaterial(PresetOf(id), materialName, current, out result);

        public static List<ArchPart> Adapt(CelestialBodyId id, ArchArchetype a, KitShape shape, int seed) =>
            Adapt(PresetOf(id), a, shape, seed);

        public static List<ArchPart> Adapt(CelestialBodyId id, ArchArchetype a, float w, float d, float h, int seed) =>
            Adapt(PresetOf(id), a, w, d, h, seed);
    }
}
