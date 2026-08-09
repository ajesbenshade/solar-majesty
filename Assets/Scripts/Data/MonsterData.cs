using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Non-sentient threat definition (e.g. Lunar Dust Stalker).
    /// Runtime AI agents will consume this later; content stays data-driven now.
    /// </summary>
    [CreateAssetMenu(fileName = "Monster_", menuName = "Solar Majesty/Monster", order = 40)]
    public class MonsterData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Dust Stalker";
        [TextArea] public string description =
            "Non-sentient lunar fauna that harasses outposts.";

        [Header("Combat / pressure")]
        [Min(1f)] public float maxHealth = 40f;
        [Min(0.1f)] public float moveSpeed = 2.8f;
        [Min(0f)] public float attackDamage = 6f;
        [Min(0.1f)] public float attackInterval = 1.4f;
        [Min(0.5f)] public float aggroRange = 10f;
        [Min(0.5f)] public float leashRange = 24f;

        [Tooltip("Added into ClearThreat flag risk when this threat is present.")]
        [Range(0f, 1f)] public float threatRiskBonus = 0.35f;

        [Header("Prefab (assign after drop-in)")]
        public GameObject prefab;
    }
}
