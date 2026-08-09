using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player-postable bounty flag type definition.
    /// Runtime bounty is chosen by the player on each FlagHandle.
    /// </summary>
    [CreateAssetMenu(fileName = "Flag_", menuName = "Solar Majesty/Flag", order = 20)]
    public class FlagData : ScriptableObject
    {
        [Header("Identity")]
        public FlagType flagType = FlagType.Explore;
        public string displayName = "Explore";
        [TextArea] public string description;

        [Header("Bounty defaults")]
        [Min(0)] public int defaultBounty = 50;
        [Min(0)] public int minBounty = 10;
        [Min(0)] public int maxBounty = 500;

        [Header("Work & risk")]
        [Tooltip("Work units required to complete the flag.")]
        [Min(0.1f)] public float workRequired = 5f;

        [Tooltip("Base risk [0..1] before body danger is applied.")]
        [Range(0f, 1f)] public float baseRisk = 0.1f;

        [Header("Affinity hints")]
        public SpecialistClass[] stronglyAttracts;

        [Header("Presentation")]
        public Color bannerColor = Color.yellow;
        public GameObject prefab;
    }
}
