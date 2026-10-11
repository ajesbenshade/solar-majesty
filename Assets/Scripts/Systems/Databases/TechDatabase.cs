using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Designer-editable copy of the research tree. Optional: with no asset at
    /// <c>Resources/Data/TechDatabase</c> the game uses the table in <see cref="TechCatalog"/>.
    /// Create one with <b>Solar Majesty &gt; Data &gt; Export catalogs to assets</b>.
    /// Ids are saved by number; never renumber <see cref="TechId"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Solar Majesty/Data/Tech Database", fileName = "TechDatabase")]
    public sealed class TechDatabase : ScriptableObject
    {
        public const string ResourcePath = CatalogDatabase.ResourceFolder + "/TechDatabase";

        public List<TechEntry> techs = new List<TechEntry>();

        /// <summary>Fill a new database from definitions (the export generator and tests use this).</summary>
        public static TechDatabase FromDefs(IReadOnlyList<TechDef> defs)
        {
            var db = CreateInstance<TechDatabase>();
            db.techs = new List<TechEntry>(defs != null ? defs.Count : 0);
            if (defs == null) return db;
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i] != null)
                    db.techs.Add(TechEntry.From(defs[i]));
            }
            return db;
        }

        /// <summary>Entries as definitions. Blank text falls back to the built-in entry of the same id.</summary>
        public List<TechDef> ToDefs(Func<TechId, TechDef> fallback)
        {
            var list = new List<TechDef>(techs != null ? techs.Count : 0);
            if (techs == null) return list;
            for (int i = 0; i < techs.Count; i++)
            {
                var e = techs[i];
                if (e == null) continue;
                list.Add(e.ToDef(fallback?.Invoke(e.id)));
            }
            return list;
        }
    }

    /// <summary>Serializable mirror of <see cref="TechDef"/>.</summary>
    [Serializable]
    public sealed class TechEntry
    {
        public TechId id;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public float scienceCost = 1f;
        public TechId[] prerequisites = Array.Empty<TechId>();
        [Tooltip("CRED paid when research completes. Empty = free.")]
        public ResourceAmount[] completeCost = Array.Empty<ResourceAmount>();
        public bool unlocksLaunch;
        public float researchRateBonus;
        public bool secretProject;

        public static TechEntry From(TechDef def)
        {
            return new TechEntry
            {
                id = def.Id,
                displayName = def.DisplayName,
                description = def.Description,
                scienceCost = def.ScienceCost,
                prerequisites = def.Prerequisites != null
                    ? (TechId[])def.Prerequisites.Clone()
                    : Array.Empty<TechId>(),
                completeCost = def.CompleteCost != null
                    ? (ResourceAmount[])def.CompleteCost.Clone()
                    : Array.Empty<ResourceAmount>(),
                unlocksLaunch = def.UnlocksLaunch,
                researchRateBonus = def.ResearchRateBonus,
                secretProject = def.SecretProject
            };
        }

        /// <summary>
        /// An empty cost becomes null, which every reader already treats as "no cost"
        /// (Unity serializes a null array as an empty one, so the two cannot be told apart).
        /// </summary>
        public TechDef ToDef(TechDef fallback)
        {
            return new TechDef(
                id,
                CatalogDatabase.TextOr(displayName, fallback != null ? fallback.DisplayName : id.ToString()),
                CatalogDatabase.TextOr(description, fallback != null ? fallback.Description : ""),
                scienceCost,
                prerequisites != null ? (TechId[])prerequisites.Clone() : null,
                completeCost != null && completeCost.Length > 0 ? (ResourceAmount[])completeCost.Clone() : null,
                unlocksLaunch,
                researchRateBonus,
                secretProject);
        }
    }
}
