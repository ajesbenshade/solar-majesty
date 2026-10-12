using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Shared rules for the designer-editable catalog databases (<see cref="TechDatabase"/>,
    /// <see cref="ShopDatabase"/>, <see cref="CelestialBodyDatabase"/>).
    ///
    /// The hardcoded tables in <see cref="TechCatalog"/>, <see cref="ShopCatalog"/> and
    /// <see cref="CelestialBodyCatalog"/> stay the defaults. A database asset under
    /// <c>Resources/Data/</c> overrides them entry by entry. An id the asset leaves out keeps its
    /// default, so a half-finished asset can never make a saved tech, item or body vanish.
    /// </summary>
    public static class CatalogDatabase
    {
        /// <summary>Resources folder the databases load from (and the generator writes to).</summary>
        public const string ResourceFolder = "Data";

        /// <summary>
        /// Resources.Load the database at <paramref name="path"/>. Null when there is none.
        /// <paramref name="settled"/> is false when Unity refused the load (called from a
        /// constructor or serialization callback); the caller should not cache that answer.
        /// </summary>
        public static T Load<T>(string path, out bool settled) where T : ScriptableObject
        {
            settled = true;
            try
            {
                return Resources.Load<T>(path);
            }
            catch (Exception e)
            {
                settled = false;
                Debug.LogWarning($"[Catalog] Could not load {path} yet ({e.GetType().Name}); using built-in data.");
                return null;
            }
        }

        /// <summary>
        /// Asset entries first, in the asset's order. Then every default whose id the asset did not
        /// mention, in default order. Null entries, invalid ids and repeated ids are skipped.
        /// </summary>
        public static List<TDef> Merge<TDef, TId>(
            IReadOnlyList<TDef> defaults,
            IReadOnlyList<TDef> overrides,
            Func<TDef, TId> idOf,
            Func<TId, bool> validId,
            string label)
            where TDef : class
        {
            var result = new List<TDef>(defaults != null ? defaults.Count : 0);
            var seen = new HashSet<TId>();
            if (overrides != null)
            {
                for (int i = 0; i < overrides.Count; i++)
                {
                    var def = overrides[i];
                    if (def == null) continue;
                    TId id = idOf(def);
                    if (!validId(id))
                    {
                        Debug.LogWarning($"[Catalog] {label}: entry {i} has no usable id; skipped.");
                        continue;
                    }
                    if (!seen.Add(id))
                    {
                        Debug.LogWarning($"[Catalog] {label}: {id} is listed twice; the first entry wins.");
                        continue;
                    }
                    result.Add(def);
                }
            }

            if (defaults == null) return result;
            for (int i = 0; i < defaults.Count; i++)
            {
                var def = defaults[i];
                if (def == null) continue;
                TId id = idOf(def);
                if (seen.Contains(id)) continue;
                seen.Add(id);
                result.Add(def);
            }
            return result;
        }

        /// <summary>Non-empty <paramref name="value"/>, else <paramref name="fallback"/>.</summary>
        public static string TextOr(string value, string fallback) =>
            string.IsNullOrEmpty(value) ? fallback : value;
    }
}
