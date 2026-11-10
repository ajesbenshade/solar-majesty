using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// The worlds as an asset. <see cref="sol"/> holds the five hand-authored worlds, which
    /// <see cref="CelestialBodyCatalog"/> reads in place of its built-in profiles. <see cref="worlds"/>
    /// holds worlds beyond the five: each is a full <see cref="CelestialBodyProfile"/> with its
    /// own key, its <see cref="CelestialBodyProfile.Id"/> names the Sol world whose authored
    /// content it borrows, and it is registered when the game boots. The asset lives at
    /// <c>Resources/Data/WorldDatabase</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldDatabase", menuName = "Solar Majesty/World Database", order = 41)]
    public class WorldDatabase : ScriptableObject
    {
        public const string ResourcePath = "Data/WorldDatabase";

        [Tooltip("The five Sol worlds. A world missing here uses its built-in profile.")]
        public List<CelestialBodyProfile> sol = new List<CelestialBodyProfile>();

        [Tooltip("Worlds beyond the five, registered at boot.")]
        public List<CelestialBodyProfile> worlds = new List<CelestialBodyProfile>();

        /// <summary>
        /// The shipped profile of a Sol world, or null when no asset carries it. A copy, so play
        /// never writes into the asset.
        /// </summary>
        public static CelestialBodyProfile ShippedSol(string key)
        {
            var shipped = Resources.Load<WorldDatabase>(ResourcePath);
            return shipped != null ? shipped.FindSol(key) : null;
        }

        public CelestialBodyProfile FindSol(string key)
        {
            for (int i = 0; i < sol.Count; i++)
            {
                if (sol[i] != null && sol[i].Key == key) return sol[i].Copy();
            }
            return null;
        }

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
