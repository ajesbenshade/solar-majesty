using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Modular building definition. All costs and stats are data-driven.
    /// </summary>
    [CreateAssetMenu(fileName = "Building_", menuName = "Solar Majesty/Building", order = 10)]
    public class BuildingData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Module";
        public BuildingCategory category = BuildingCategory.Utility;
        [TextArea] public string description;

        [Header("Footprint (grid cells)")]
        [Min(1)] public int footprintWidth = 1;
        [Min(1)] public int footprintHeight = 1;

        [Header("Construction")]
        public ResourceAmount[] buildCost;
        [Min(0.1f)] public float buildTimeSeconds = 8f;

        [Header("Operation")]
        [Min(0)] public int powerDraw;
        [Min(0)] public int housingSlots;
        [Range(0f, 2f)] public float attractionWeight = 1f;
        public SpecialistClass[] preferredOccupants;

        [Header("Prefab (assign after drop-in)")]
        public GameObject prefab;
    }
}
