// The structures a colony's commands can act on — pure C#, no MonoBehaviour.

using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// What a sim knows about its finished buildings when no scene holds them: where each stands,
    /// what it is, whether its lasers are armed and which robot class it calls for. A sim run
    /// without a scene fills this as construction completes, so commands that name a building by
    /// position (<see cref="OverseerCommandKind.ArmWatchtower"/> and the like) have something to
    /// act on. In a scene the buildings are <c>ColonyStructure</c> objects and this stays empty.
    /// </summary>
    public sealed class StructureBoard
    {
        /// <summary>A command names a structure by position; this is how close counts as the same one.</summary>
        public const float FindTolerance = 0.5f;

        public sealed class Entry
        {
            public Vector3 At;
            public BuildingCategory Category;
            public bool LaserArmed;
            public bool HasPreferredClass;
            public SpecialistClass PreferredClass;
        }

        private readonly List<Entry> _entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>Record a finished building. A second note for the same spot returns the first.</summary>
        public Entry Note(Vector3 at, BuildingData data)
        {
            Entry existing = Find(at);
            if (existing != null || data == null) return existing;
            var entry = new Entry { At = at, Category = data.category };
            if (data.category != BuildingCategory.Habitat &&
                data.preferredOccupants != null && data.preferredOccupants.Length > 0)
            {
                entry.PreferredClass = data.preferredOccupants[0];
                entry.HasPreferredClass = true;
            }
            _entries.Add(entry);
            return entry;
        }

        public bool Remove(Vector3 at)
        {
            Entry entry = Find(at);
            return entry != null && _entries.Remove(entry);
        }

        public Entry Find(Vector3 at)
        {
            Entry best = null;
            float bestSqr = FindTolerance * FindTolerance;
            for (int i = 0; i < _entries.Count; i++)
            {
                Vector3 d = _entries[i].At - at;
                d.y = 0f;
                if (d.sqrMagnitude > bestSqr) continue;
                bestSqr = d.sqrMagnitude;
                best = _entries[i];
            }
            return best;
        }

        public void Clear() => _entries.Clear();

        /// <summary>The flag a workplace posts to draw in the class it calls for.</summary>
        public static FlagType AttractFlagFor(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.EngineerBot: return FlagType.Build;
                case SpecialistClass.DefenseMech: return FlagType.DefendArea;
                case SpecialistClass.Medic: return FlagType.DefendArea;
                case SpecialistClass.HarvesterBot: return FlagType.Extract;
                case SpecialistClass.SurveyorBot: return FlagType.ResearchSite;
                case SpecialistClass.TerraformerBot: return FlagType.Terraform;
                case SpecialistClass.CourierBot: return FlagType.EstablishOutpost;
                case SpecialistClass.GeologistBot: return FlagType.Extract;
                case SpecialistClass.SentinelMech: return FlagType.DefendArea;
                default: return FlagType.Explore;
            }
        }

        /// <summary>As above for a building: farms, mines and regolith camps always call for extraction.</summary>
        public static FlagType AttractFlagFor(BuildingCategory category, bool hasClass, SpecialistClass cls)
        {
            if (category == BuildingCategory.Farm || category == BuildingCategory.Mine ||
                category == BuildingCategory.RegolithCamp)
                return FlagType.Extract;
            return AttractFlagFor(hasClass ? cls : SpecialistClass.ScoutDrone);
        }
    }
}
