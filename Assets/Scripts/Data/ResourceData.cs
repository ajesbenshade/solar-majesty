using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Definition for a stockpile resource (Regolith, Water Ice, Metals, Power).
    /// Balances live in ResourceManager; this asset is pure content.
    /// </summary>
    [CreateAssetMenu(fileName = "Resource_", menuName = "Solar Majesty/Resource", order = 0)]
    public class ResourceData : ScriptableObject
    {
        [Header("Identity")]
        public ResourceId id = ResourceId.Regolith;
        public string displayName = "Regolith";
        [TextArea] public string description;

        [Header("Presentation (bind art later)")]
        public Color uiColor = Color.gray;

        [Header("Defaults")]
        [Tooltip("Suggested starting stock when bootstrapping a body/outpost.")]
        [Min(0)] public int startingAmount;

        [Tooltip("If true, soft deficit is allowed for warnings (e.g. Power).")]
        public bool allowSoftDeficit;
    }
}
