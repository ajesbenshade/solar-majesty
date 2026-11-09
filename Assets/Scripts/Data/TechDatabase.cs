using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// The tech tree as an asset. When one sits at <c>Resources/Data/TechDatabase</c>,
    /// <see cref="TechCatalog"/> reads it in place of the built-in tree, so the tree can be tuned
    /// without a code change. With no asset the built-in tree is used, as before.
    /// </summary>
    [CreateAssetMenu(fileName = "TechDatabase", menuName = "Solar Majesty/Tech Database", order = 40)]
    public class TechDatabase : ScriptableObject
    {
        public const string ResourcePath = "Data/TechDatabase";

        [Serializable]
        public sealed class Entry
        {
            public TechId id;
            public string displayName;
            [TextArea] public string description;
            [Min(1f)] public float scienceCost = 20f;
            public TechId[] prerequisites = new TechId[0];
            public ResourceAmount[] completeCost = new ResourceAmount[0];
            public bool unlocksLaunch;
            public float researchRateBonus;
            public bool secretProject;

            public TechDef ToDef() => new TechDef(
                id, displayName, description, scienceCost, prerequisites,
                completeCost != null && completeCost.Length > 0 ? completeCost : null,
                unlocksLaunch, researchRateBonus, secretProject);

            public static Entry From(TechDef def) => new Entry
            {
                id = def.Id,
                displayName = def.DisplayName,
                description = def.Description,
                scienceCost = def.ScienceCost,
                prerequisites = def.Prerequisites ?? new TechId[0],
                completeCost = def.CompleteCost ?? new ResourceAmount[0],
                unlocksLaunch = def.UnlocksLaunch,
                researchRateBonus = def.ResearchRateBonus,
                secretProject = def.SecretProject
            };
        }

        public List<Entry> entries = new List<Entry>();

        public TechDef[] ToDefs()
        {
            var defs = new TechDef[entries.Count];
            for (int i = 0; i < entries.Count; i++)
                defs[i] = entries[i].ToDef();
            return defs;
        }

        /// <summary>Fill this asset from a tree (the exporter uses the built-in one).</summary>
        public void CopyFrom(IReadOnlyList<TechDef> defs)
        {
            entries.Clear();
            for (int i = 0; i < defs.Count; i++)
                entries.Add(Entry.From(defs[i]));
        }
    }
}
