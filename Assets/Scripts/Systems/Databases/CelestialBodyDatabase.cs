using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Designer-editable copies of the world profiles. Optional: with no asset at
    /// <c>Resources/Data/CelestialBodyDatabase</c> the game uses <see cref="CelestialBodyCatalog"/>.
    /// Create one with <b>Solar Majesty &gt; Data &gt; Export catalogs to assets</b>.
    /// The set of bodies and the campaign order stay in code (<see cref="CelestialBodyId"/>);
    /// the asset retunes a body, it does not add one.
    /// </summary>
    [CreateAssetMenu(menuName = "Solar Majesty/Data/Celestial Body Database", fileName = "CelestialBodyDatabase")]
    public sealed class CelestialBodyDatabase : ScriptableObject
    {
        public const string ResourcePath = CatalogDatabase.ResourceFolder + "/CelestialBodyDatabase";

        public List<CelestialBodyProfile> bodies = new List<CelestialBodyProfile>();

        private static FieldInfo[] _fields;

        public static CelestialBodyDatabase FromProfiles(IReadOnlyList<CelestialBodyProfile> profiles)
        {
            var db = CreateInstance<CelestialBodyDatabase>();
            db.bodies = new List<CelestialBodyProfile>(profiles != null ? profiles.Count : 0);
            if (profiles == null) return db;
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] != null)
                    db.bodies.Add(Copy(profiles[i]));
            }
            return db;
        }

        /// <summary>
        /// The asset's profile for <paramref name="id"/> as a fresh copy, or null when the asset has
        /// none. Blank text and a malformed resource-weight table fall back to <paramref name="fallback"/>.
        /// </summary>
        public CelestialBodyProfile Find(CelestialBodyId id, CelestialBodyProfile fallback)
        {
            if (bodies == null) return null;
            for (int i = 0; i < bodies.Count; i++)
            {
                var p = bodies[i];
                if (p == null || p.Id != id) continue;
                var copy = Copy(p);
                if (fallback != null)
                {
                    copy.DisplayName = CatalogDatabase.TextOr(copy.DisplayName, fallback.DisplayName);
                    copy.ShortCode = CatalogDatabase.TextOr(copy.ShortCode, fallback.ShortCode);
                    copy.Briefing = CatalogDatabase.TextOr(copy.Briefing, fallback.Briefing);
                    copy.ArrivalLog = CatalogDatabase.TextOr(copy.ArrivalLog, fallback.ArrivalLog);
                    copy.VictoryLog = CatalogDatabase.TextOr(copy.VictoryLog, fallback.VictoryLog);
                    copy.FailLog = CatalogDatabase.TextOr(copy.FailLog, fallback.FailLog);
                    copy.EndlessLog = CatalogDatabase.TextOr(copy.EndlessLog, fallback.EndlessLog);
                    if (copy.ResourceWeights == null || copy.ResourceWeights.Length != 4)
                        copy.ResourceWeights = (int[])fallback.ResourceWeights?.Clone();
                }
                if (copy.GroundCover == null)
                    copy.GroundCover = new GroundCoverLayer[0];
                return copy;
            }
            return null;
        }

        /// <summary>Field-by-field copy; arrays are cloned so the copy never aliases the source.</summary>
        public static CelestialBodyProfile Copy(CelestialBodyProfile src)
        {
            if (src == null) return null;
            _fields ??= typeof(CelestialBodyProfile).GetFields(BindingFlags.Public | BindingFlags.Instance);
            var dst = new CelestialBodyProfile();
            for (int i = 0; i < _fields.Length; i++)
            {
                object v = _fields[i].GetValue(src);
                if (v is Array arr) v = arr.Clone();
                _fields[i].SetValue(dst, v);
            }
            return dst;
        }
    }
}
