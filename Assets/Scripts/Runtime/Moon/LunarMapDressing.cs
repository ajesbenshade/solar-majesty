using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Compatibility facade — prefer <see cref="PlanetaryMapDressing"/>.</summary>
    public static class LunarMapDressing
    {
        public static void Apply(Transform parent, IsoGrid grid) =>
            PlanetaryMapDressing.Apply(parent, grid, CelestialBodyCatalog.Luna());
    }
}
