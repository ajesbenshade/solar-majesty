// What a world is like to build on — pure data.

using System;

namespace SolarMajesty
{
    public enum Atmosphere
    {
        None = 0,
        Thin = 1,
        Breathable = 2
    }

    /// <summary>
    /// The physical facts architecture answers to. A generated world carries these in place of a
    /// hand-written building set: <see cref="PlanetArchitecture.PresetFor"/> picks the set from them.
    /// </summary>
    [Serializable]
    public struct EnvironmentTraits
    {
        public Atmosphere Atmosphere;
        /// <summary>Surface gravity in Earth gravities.</summary>
        public float Gravity;
        /// <summary>Typical surface temperature in degrees Celsius.</summary>
        public float TemperatureC;
        /// <summary>Surface radiation, 0 (shielded like Earth) to 1 (Jupiter's belts).</summary>
        public float Radiation;

        public EnvironmentTraits(Atmosphere atmosphere, float gravity, float temperatureC, float radiation)
        {
            Atmosphere = atmosphere;
            Gravity = gravity;
            TemperatureC = temperatureC;
            Radiation = radiation;
        }
    }

    /// <summary>The five architectural sets. Each was authored for one Sol world and is named for what it answers.</summary>
    public enum ArchPreset
    {
        /// <summary>Open air and a biosphere: green roofs, glass, wind (Earth).</summary>
        Arcology = 0,
        /// <summary>Vacuum and radiation: regolith berms, foil, radiators (Luna).</summary>
        ShieldedOutpost = 1,
        /// <summary>Thin air, dust and cold: printed windbreaks and pressure domes (Mars).</summary>
        PrintedColony = 2,
        /// <summary>Almost no gravity: anchors, trusses, spin rings (Belt).</summary>
        AnchoredStation = 3,
        /// <summary>Deep cold: ice walls, shield domes, RTG power (Europa).</summary>
        CryoBase = 4
    }
}
