using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Worlds beyond the five, as an asset. Each entry is a full <see cref="CelestialBodyProfile"/>
    /// with its own key; its <see cref="CelestialBodyProfile.Id"/> names the Sol world whose
    /// authored content it borrows. An asset at <c>Resources/Data/WorldDatabase</c> is registered
    /// with <see cref="CelestialBodyCatalog"/> when the game boots.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldDatabase", menuName = "Solar Majesty/World Database", order = 41)]
    public class WorldDatabase : ScriptableObject
    {
        public const string ResourcePath = "Data/WorldDatabase";

        public List<CelestialBodyProfile> worlds = new List<CelestialBodyProfile>();

        /// <summary>Register every world not already known. Returns how many were added.</summary>
        public int RegisterAll()
        {
            int added = 0;
            for (int i = 0; i < worlds.Count; i++)
            {
                if (CelestialBodyCatalog.Register(worlds[i])) added++;
            }
            return added;
        }

        /// <summary>Load the shipped asset, if there is one, and register its worlds.</summary>
        public static int RegisterShipped()
        {
            var shipped = Resources.Load<WorldDatabase>(ResourcePath);
            return shipped != null ? shipped.RegisterAll() : 0;
        }
    }
}
