using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Designer-editable copy of the civilizations. Optional: with no asset at
    /// <c>Resources/Data/CivDatabase</c> the game uses the table in <see cref="CivCatalog"/>.
    /// A civ the asset leaves out keeps its built-in entry, and <see cref="CivId.None"/> is always
    /// the built-in neutral civ. Ids are saved by number; never renumber <see cref="CivId"/>.
    /// TODO: add this to the export menu (CatalogDatabaseExporter) and Docs/DATA_CATALOGS.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Solar Majesty/Data/Civ Database", fileName = "CivDatabase")]
    public sealed class CivDatabase : ScriptableObject
    {
        public const string ResourcePath = CatalogDatabase.ResourceFolder + "/CivDatabase";

        public List<CivDefinition> civs = new List<CivDefinition>();

        /// <summary>Fill a new database from definitions (copies, so the catalog's records stay untouched).</summary>
        public static CivDatabase FromDefs(IReadOnlyList<CivDefinition> defs)
        {
            var db = CreateInstance<CivDatabase>();
            db.civs = new List<CivDefinition>(defs != null ? defs.Count : 0);
            if (defs == null) return db;
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i] != null && defs[i].id != CivId.None)
                    db.civs.Add(defs[i].Clone());
            }
            return db;
        }

        /// <summary>
        /// Entries as definitions (copies, so play mode never writes back into the asset).
        /// Blank text falls back to the built-in entry of the same id.
        /// </summary>
        public List<CivDefinition> ToDefs(Func<CivId, CivDefinition> fallback)
        {
            var list = new List<CivDefinition>(civs != null ? civs.Count : 0);
            if (civs == null) return list;
            for (int i = 0; i < civs.Count; i++)
            {
                var entry = civs[i];
                if (entry == null) continue;
                var def = entry.Clone();
                var builtIn = fallback?.Invoke(def.id);
                def.displayName = CatalogDatabase.TextOr(def.displayName, builtIn != null ? builtIn.displayName : def.id.ToString());
                def.pitch = CatalogDatabase.TextOr(def.pitch, builtIn != null ? builtIn.pitch : "");
                def.summary = CatalogDatabase.TextOr(def.summary, builtIn != null ? builtIn.summary : "");
                def.castleName = CatalogDatabase.TextOr(def.castleName, builtIn != null ? builtIn.castleName : "");
                list.Add(def);
            }
            return list;
        }
    }
}
